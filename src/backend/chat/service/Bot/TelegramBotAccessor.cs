using Telegram.Bot;

namespace Chat.Bot;

/// <summary>
/// Единственный на процесс клиент Telegram и настройки группы гидов. Клиент один
/// намеренно: у него внутри свой HttpClient, а long polling и доставка сообщений
/// должны ходить через одно соединение.
///
/// Без TG_BOT_TOKEN сервис работает как обычно — переписка сохраняется, просто ничего
/// не уезжает в Telegram. Так локальная разработка не требует настоящего бота.
/// </summary>
public sealed class TelegramBotAccessor
{
    public TelegramBotAccessor(IConfiguration configuration, ILogger<TelegramBotAccessor> logger)
    {
        var token = configuration["TG_BOT_TOKEN"];
        Client = string.IsNullOrWhiteSpace(token) ? null : new TelegramBotClient(token);

        AdminChatId = long.TryParse(configuration["TG_ADMIN_CHAT_ID"], out var chatId) ? chatId : null;
        AdminLanguage = configuration["TG_ADMIN_LANG"] is { Length: > 0 } lang ? lang : "ru";

        if (Client is null)
            logger.LogInformation("TG_BOT_TOKEN не задан — Telegram-бот выключен, сервис работает без него");
        else if (AdminChatId is null)
            logger.LogWarning(
                "TG_ADMIN_CHAT_ID не задан — сообщения чата сохраняются, но в группу гидов не уходят. " +
                "Добавьте бота в приватную группу и возьмите её id из лога");
    }

    public TelegramBotClient? Client { get; }

    /// <summary>Id приватной группы гидов, куда уходят сообщения посетителей.</summary>
    public long? AdminChatId { get; }

    /// <summary>Язык служебных сообщений в группе; к языку посетителя отношения не имеет.</summary>
    public string AdminLanguage { get; }

    public bool IsEnabled => Client is not null;

    /// <summary>Доставка возможна, только когда известны и токен, и куда писать.</summary>
    public bool CanDeliver => Client is not null && AdminChatId is not null;
}
