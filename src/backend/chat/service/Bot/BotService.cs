using Chat.Application.Abstract;
using Chat.Contracts;
using Domain;
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
/// горизонтально (users, где бот жил раньше, — тоже нет: там отбивка заявок в канал).
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

    /// <summary>Потолок паузы между попытками достучаться до Telegram при старте.</summary>
    private static readonly TimeSpan MaxStartBackoff = TimeSpan.FromMinutes(5);

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

        if (!await WaitForTelegramAsync(client, siteUrl, stoppingToken))
            return;

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

    /// <summary>
    /// Проверка токена при старте. Отказ Telegram (401/404) выключает бота. Сетевой сбой —
    /// лёг туннель или сам Telegram — не должен ронять chat-сервис: исключение из
    /// ExecuteAsync останавливает хост, контейнер уходит в рестарты, и чат на сайте
    /// перестаёт принимать сообщения. Поэтому ждём связь с растущей паузой.
    /// </summary>
    private async Task<bool> WaitForTelegramAsync(ITelegramBotClient client, string siteUrl, CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(5);

        while (true)
        {
            try
            {
                var me = await client.GetMe(stoppingToken);
                logger.LogInformation("Telegram-бот @{Username} запущен, кнопка ведёт на {SiteUrl}", me.Username, siteUrl);
                return true;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return false;
            }
            catch (ApiRequestException e) when (e.ErrorCode is 401 or 404)
            {
                logger.LogError(e, "Telegram отверг токен бота — проверь TG_BOT_TOKEN; бот выключен");
                return false;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Telegram недоступен при старте бота (туннель?) — повтор через {Delay}", delay);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            delay = delay * 2 > MaxStartBackoff ? MaxStartBackoff : delay * 2;
        }
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

        // Переписываться в личке бот пока не умеет (бронирование через бота — позже),
        // поэтому ведёт сразу в чат на сайте: ?chat=open открывает панель и без JavaScript.
        var keyboard = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithUrl(BotTexts.OpenSiteButton(lang), $"{siteUrl.TrimEnd('/')}/?chat=open"));

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

        // Отказ не должен быть молчаливым: без подсказки менеджер уверен, что ответ ушёл.
        // Длину проверяем сами, чтобы сказать, что именно не так, — валидатор команды
        // отверг бы такой ответ безлико.
        if (text.Length > ChatLimits.MaxTextLength)
        {
            await ReplyToManagerAsync(client, message, BotTexts.ReplyTooLong(bot.AdminLanguage, ChatLimits.MaxTextLength), ct);
            return;
        }

        ExecuteRequestResult result;

        try
        {
            // Через Mediator, а не напрямую в репозиторий: так работают ValidatorBehavior,
            // общий CommitAsync и защита от повторной доставки апдейта.
            result = await mediator.Send(new ChatAdminReplyCommand
            {
                SessionId = sessionId.Value,
                Text = text,
                TgMessageId = message.MessageId
            }, ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            // Упавший обработчик апдейта Telegram.Bot считает обработанным — повтора не будет,
            // поэтому о сбое (например, базы) менеджер должен узнать сразу.
            logger.LogWarning(e, "Ответ гида в диалог {SessionId} не сохранён", sessionId);
            await ReplyToManagerAsync(client, message, BotTexts.ReplyNotSaved(bot.AdminLanguage), ct);
            return;
        }

        if (!result.IsSuccess)
        {
            logger.LogWarning("Ответ гида в диалог {SessionId} не сохранён: {StatusCode}",
                sessionId, result.StatusCode);
            await ReplyToManagerAsync(client, message, BotTexts.ReplyNotSaved(bot.AdminLanguage), ct);
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

    /// <summary>Служебный ответ менеджеру reply на его сообщение — видно, к какому ответу он относится.</summary>
    private static Task ReplyToManagerAsync(ITelegramBotClient client, Message message, string text, CancellationToken ct) =>
        client.SendMessage(message.Chat, text,
            replyParameters: new ReplyParameters { MessageId = message.MessageId, AllowSendingWithoutReply = true },
            cancellationToken: ct);

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
