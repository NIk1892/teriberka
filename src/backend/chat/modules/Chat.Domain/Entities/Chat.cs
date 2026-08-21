using System.Buffers.Text;
using System.Security.Cryptography;
using Domain;

namespace Chat.Domain;

/// <summary>
/// Диалог посетителя сайта. Никакой авторизации нет, поэтому единственный ключ к
/// переписке — секретный <see cref="Token"/> из cookie chat_sid. Контакты посетителя
/// сознательно не хранятся (решение владельца), IP и User-Agent тоже.
/// </summary>
public record ChatSessionEntity : AuditableEntity
{
    /// <summary>
    /// Секрет из cookie: 32 байта CSPRNG в base64url. Именно отдельный токен, а не Id —
    /// Id генерируется последовательным Guid'ом (NpgsqlSequentialGuidValueGenerator)
    /// и предсказуем по соседним записям.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Язык сайта в момент старта диалога (ru/en/zh).</summary>
    public string? Culture { get; set; }

    /// <summary>Страница, с которой начали писать — попадает в шапку сессии в Telegram.</summary>
    public string? Page { get; set; }

    /// <summary>Счётчик сообщений диалога; из него же берётся следующий Ordinal.</summary>
    public int MessageCount { get; set; }

    public DateTime LastMessageAt { get; set; }

    /// <summary>Id «шапки» диалога в группе гидов — запасной путь сопоставления reply с сессией.</summary>
    public long? TopicMessageId { get; set; }

    /// <summary>В какую группу ушёл диалог: смена TG_ADMIN_CHAT_ID не должна ломать старые переписки.</summary>
    public long? AdminChatId { get; set; }
}

/// <summary>
/// Одно сообщение диалога. Связь с сессией — плоским <see cref="SessionId"/>, без
/// навигационных свойств: в проекте нет ни одной связи между сущностями, а чистка
/// по сроку хранения делается одним ExecuteDelete без каскадов.
/// </summary>
public record ChatMessageEntity : AuditableEntity
{
    public Guid SessionId { get; set; }

    /// <summary>
    /// Порядковый номер внутри диалога, 1..N. Курсор поллинга — именно он, а не время:
    /// Audit.CreatedAt приходит из NOW() базы, и две вставки в одну микросекунду
    /// заставили бы фильтр «строго больше» пропустить сообщение.
    /// </summary>
    public int Ordinal { get; set; }

    public ChatDirection Direction { get; set; }

    public string? Text { get; set; }

    /// <summary>
    /// Id сообщения в группе гидов. У сообщения посетителя это то, на что гид отвечает reply;
    /// NULL у Visitor-сообщения означает «ещё не доставлено в Telegram» — это и есть outbox.
    /// </summary>
    public long? TgMessageId { get; set; }
}

public static class ChatTokens
{
    /// <summary>32 байта из CSPRNG в base64url — 256 бит энтропии, подбор невозможен.</summary>
    public static string New() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
}
