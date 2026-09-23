using Contracts;

namespace Chat.Contracts;

/// <summary>Сообщение посетителя сайта. Авторизации нет — диалог опознаётся по токену из cookie.</summary>
public record ChatSendCommand : Command
{
    /// <summary>Токен сессии из cookie chat_sid. Пусто или неизвестен — начинается новый диалог.</summary>
    public string? SessionToken { get; set; }

    public string? Text { get; set; }

    /// <summary>Язык сайта в момент старта диалога (ru/en/zh) — гиду видно, на каком языке отвечать.</summary>
    public string? Culture { get; set; }

    /// <summary>Страница, с которой написали; уходит в шапку сессии в Telegram.</summary>
    public string? Page { get; set; }
}

/// <summary>
/// Сообщение посетителя из лички Telegram-бота («написать менеджеру»). Наружу не
/// публикуется: команду шлёт только бот внутри chat-сервиса. Именно отдельная команда,
/// а не поле TgChatId в публичной <see cref="ChatSendCommand"/>: подделанный POST с сайта
/// привязал бы диалог к чужому чату, и ответы гида уходили бы туда.
/// </summary>
public record ChatTelegramSendCommand : Command
{
    /// <summary>Id лички в Telegram — туда же бот отправит ответ гида.</summary>
    public long TgChatId { get; set; }

    public string? Text { get; set; }

    /// <summary>Язык клиента Telegram (ru/en/zh) — гиду видно, на каком языке отвечать.</summary>
    public string? Lang { get; set; }

    /// <summary>Username без «@», если есть, — гиду видно, кто пишет.</summary>
    public string? Username { get; set; }
}

/// <summary>
/// Ответ гида, пришедший из Telegram-группы. Наружу не публикуется: команду шлёт
/// только бот внутри chat-сервиса, эндпоинта у неё нет.
/// </summary>
public record ChatAdminReplyCommand : Command
{
    public Guid SessionId { get; set; }

    public string? Text { get; set; }

    /// <summary>Id сообщения гида в группе — по нему отсекается повторная доставка апдейта.</summary>
    public long TgMessageId { get; set; }
}
