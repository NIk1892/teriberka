using Chat.Application.Abstract;
using Chat.Contracts;
using Mediator;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Update = Telegram.Bot.Types.Update;

namespace Chat.Bot;

/// <summary>
/// Telegram-бот проекта: на сообщение в личке отвечает локализованным приветствием
/// с кнопкой-ссылкой на сайт, а из группы гидов принимает ответы посетителям.
///
/// Живёт внутри chat-сервиса, потому что именно чат — его основная работа, а long
/// polling обязан работать ровно в одном экземпляре: два процесса с одним токеном
/// начнут отбирать апдейты друг у друга. Отсюда правило: chat-сервис не масштабируется
/// горизонтально (users, где бот жил раньше, — теперь может).
///
/// Long polling — исходящее соединение, поэтому закрытость сервиса от внешней сети
/// боту не мешает.
/// </summary>
public sealed class BotService(
    IConfiguration configuration,
    TelegramBotAccessor bot,
    IServiceScopeFactory scopeFactory,
    ILogger<BotService> logger) : BackgroundService
{
    /// <summary>
    /// Насколько старое сообщение из лички ещё стоит приветствовать. Апдейты больше не
    /// отбрасываются при старте (иначе терялись бы ответы гидов), поэтому от пачки
    /// приветствий на вчерашние «/start» защищает возраст сообщения.
    /// </summary>
    private static readonly TimeSpan GreetingFreshness = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (bot.Client is not { } client)
        {
            // Сообщение в лог уже написал TelegramBotAccessor.
            return;
        }

        var siteUrl = configuration["SITE_URL"];
        if (string.IsNullOrWhiteSpace(siteUrl))
        {
            logger.LogError("SITE_URL не задан — кнопке бота некуда вести, бот выключен");
            return;
        }

        try
        {
            var me = await client.GetMe(stoppingToken);
            logger.LogInformation("Telegram-бот @{Username} запущен, кнопка ведёт на {SiteUrl}", me.Username, siteUrl);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (ApiRequestException e)
        {
            logger.LogError(e, "Telegram отверг токен бота — проверь TG_BOT_TOKEN; бот выключен");
            return;
        }

        var options = new ReceiverOptions
        {
            // MyChatMember нужен, чтобы в лог попал id группы в момент добавления бота —
            // это единственный удобный способ узнать TG_ADMIN_CHAT_ID.
            AllowedUpdates = [UpdateType.Message, UpdateType.MyChatMember],

            // Раньше было true: пачка ответов на вчерашние «/start» никому не нужна.
            // Но теперь тем же каналом приходят ОТВЕТЫ ГИДОВ — их терять нельзя,
            // поэтому апдейты забираем все, а приветствия фильтруем по возрасту.
            DropPendingUpdates = false,
        };

        await client.ReceiveAsync(
            (c, update, ct) => OnUpdate(c, update, siteUrl, ct),
            OnError,
            options,
            stoppingToken);
    }

    private async Task OnUpdate(ITelegramBotClient client, Update update, string siteUrl, CancellationToken ct)
    {
        if (update.MyChatMember is { } membership)
        {
            // Подсказка при настройке: добавили бота в группу — её id сразу видно в логе.
            logger.LogInformation("Бота добавили в чат {ChatId} «{Title}» ({Type}) — этот id идёт в TG_ADMIN_CHAT_ID",
                membership.Chat.Id, membership.Chat.Title ?? "—", membership.Chat.Type);
            return;
        }

        if (update.Message is not { } message)
        {
            return;
        }

        if (bot.AdminChatId is { } adminChatId && message.Chat.Id == adminChatId)
        {
            await HandleAdminReplyAsync(client, message, ct);
            return;
        }

        if (message.Chat.Type is ChatType.Group or ChatType.Supergroup or ChatType.Channel)
        {
            // В чужих группах бот молчит, но id пишет: так его можно узнать, даже если
            // момент добавления пропущен.
            logger.LogInformation("Сообщение из группы {ChatId} «{Title}» — не админская, игнорирую",
                message.Chat.Id, message.Chat.Title ?? "—");
            return;
        }

        if (DateTime.UtcNow - message.Date > GreetingFreshness)
        {
            return;
        }

        var lang = message.From?.LanguageCode;
        var keyboard = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithUrl(BotTexts.OpenSiteButton(lang), siteUrl));

        await client.SendMessage(message.Chat, BotTexts.Greeting(lang), replyMarkup: keyboard, cancellationToken: ct);

        logger.LogInformation("Бот ответил в чат {ChatId} (язык клиента: {Lang})", message.Chat.Id, lang ?? "—");
    }

    /// <summary>
    /// Ответ гида посетителю. Гид отвечает reply на сообщение в группе — по Id того
    /// сообщения находим диалог. Свободные сообщения бот сопоставить не может (и, при
    /// включённом privacy mode, даже не увидит).
    /// </summary>
    private async Task HandleAdminReplyAsync(ITelegramBotClient client, Message message, CancellationToken ct)
    {
        if (message.From?.IsBot == true)
        {
            // Собственные сообщения бота (шапки диалогов и подсказки) обратно не читаем.
            return;
        }

        if (message.Text is not { Length: > 0 } text)
        {
            await client.SendMessage(message.Chat, BotTexts.UnsupportedContent(bot.AdminLanguage),
                cancellationToken: ct);
            return;
        }

        if (message.ReplyToMessage is not { } repliedTo)
        {
            // Команды и обычную болтовню гидов не трогаем — подсказку даём только на то,
            // что похоже на попытку ответить посетителю.
            if (!text.StartsWith('/'))
                await client.SendMessage(message.Chat, BotTexts.ReplyHint(bot.AdminLanguage), cancellationToken: ct);

            return;
        }

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IChatRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Сначала по сообщению посетителя, потом по «шапке» диалога — гид мог ответить и на неё.
        var sessionId = await repository.FindSessionIdByTgMessageIdAsync(repliedTo.MessageId, ct)
                        ?? await repository.FindSessionIdByTopicMessageIdAsync(repliedTo.MessageId, ct);

        if (sessionId is null)
        {
            await client.SendMessage(message.Chat, BotTexts.SessionNotFound(bot.AdminLanguage), cancellationToken: ct);
            return;
        }

        // Через Mediator, а не напрямую в репозиторий: так работают ValidatorBehavior,
        // общий CommitAsync и защита от повторной доставки апдейта.
        var result = await mediator.Send(new ChatAdminReplyCommand
        {
            SessionId = sessionId.Value,
            Text = text,
            TgMessageId = message.MessageId
        }, ct);

        if (!result.IsSuccess)
        {
            logger.LogWarning("Ответ гида в диалог {SessionId} не сохранён: {StatusCode}",
                sessionId, result.StatusCode);
            return;
        }

        // Видимое подтверждение, что ответ ушёл посетителю — гиду не нужно гадать.
        try
        {
            await client.SetMessageReaction(message.Chat, message.MessageId,
                [new ReactionTypeEmoji { Emoji = "👍" }], cancellationToken: ct);
        }
        catch (ApiRequestException e)
        {
            // Реакции могут быть запрещены настройками чата — это не повод считать ответ неудачным.
            logger.LogDebug(e, "Не удалось поставить реакцию на ответ гида");
        }

        logger.LogInformation("Ответ гида сохранён в диалог {SessionId}", sessionId);
    }

    private async Task OnError(ITelegramBotClient client, Exception exception, CancellationToken ct)
    {
        logger.LogWarning(exception, "Ошибка long polling Telegram, продолжаю через 5 секунд");
        try
        {
            // Пауза, чтобы при лежащей сети не крутить горячий цикл реконнектов.
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
