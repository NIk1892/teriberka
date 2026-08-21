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
/// Ответ гида, пришедший из Telegram-группы. Наружу не публикуется: команду шлёт
/// только бот внутри users-сервиса, эндпоинта у неё нет.
/// </summary>
public record ChatAdminReplyCommand : Command
{
    public Guid SessionId { get; set; }

    public string? Text { get; set; }

    /// <summary>Id сообщения гида в группе — по нему отсекается повторная доставка апдейта.</summary>
    public long TgMessageId { get; set; }
}
