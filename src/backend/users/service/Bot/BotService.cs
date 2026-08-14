using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Update = Telegram.Bot.Types.Update;

namespace Users.Bot;

/// <summary>
/// Первая версия Telegram-бота: на любое сообщение отвечает локализованным
/// приветствием с одной кнопкой-ссылкой на сайт. Живёт внутри users-сервиса,
/// чтобы дальше читать заявки напрямую через репозитории, без похода в шлюз.
/// Long polling — исходящее соединение, поэтому закрытость сервиса от внешней
/// сети боту не мешает. Но polling должен работать ровно в одном экземпляре:
/// при горизонтальном масштабировании users бота придётся выносить отдельно.
/// </summary>
public sealed class BotService(
    IConfiguration configuration,
    ILogger<BotService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = configuration["TG_BOT_TOKEN"];
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogInformation("TG_BOT_TOKEN не задан — Telegram-бот выключен, сервис работает без него");
            return;
        }

        var siteUrl = configuration["SITE_URL"];
        if (string.IsNullOrWhiteSpace(siteUrl))
        {
            logger.LogError("SITE_URL не задан — кнопке бота некуда вести, бот выключен");
            return;
        }

        var bot = new TelegramBotClient(token);

        try
        {
            var me = await bot.GetMe(stoppingToken);
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
            AllowedUpdates = [UpdateType.Message],
            // Накопившиеся за простой сообщения не обрабатываем: отвечать
            // на вчерашние «/start» пачкой при рестарте бессмысленно.
            DropPendingUpdates = true,
        };

        await bot.ReceiveAsync(
            (client, update, ct) => OnUpdate(client, update, siteUrl, ct),
            OnError,
            options,
            stoppingToken);
    }

    private async Task OnUpdate(ITelegramBotClient bot, Update update, string siteUrl, CancellationToken ct)
    {
        if (update.Message is not { } message)
        {
            return;
        }

        var lang = message.From?.LanguageCode;
        var keyboard = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithUrl(BotTexts.OpenSiteButton(lang), siteUrl));

        await bot.SendMessage(message.Chat, BotTexts.Greeting(lang), replyMarkup: keyboard, cancellationToken: ct);

        logger.LogInformation("Бот ответил в чат {ChatId} (язык клиента: {Lang})", message.Chat.Id, lang ?? "—");
    }

    private async Task OnError(ITelegramBotClient bot, Exception exception, CancellationToken ct)
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
