using Application;
using Chat.Domain;
using Domain;

namespace Chat.Application.Abstract;

/// <summary>Сообщение вместе с диалогом, которому оно принадлежит — всё, что нужно для доставки в Telegram.</summary>
public sealed record ChatDelivery(ChatMessageEntity Message, ChatSessionEntity Session);

/// <summary>
/// Запись чата. Собственный репозиторий, а не generic CommandRepository: сообщение
/// нельзя создать в отрыве от диалога — нужно найти или завести сессию, взять
/// следующий Ordinal и обновить счётчики в той же транзакции.
/// </summary>
public interface IChatRepository : IUnitOfWorkRepository
{
    Task<ChatSessionEntity?> FindSessionByTokenAsync(string token, CancellationToken cancellationToken);

    Task<ChatSessionEntity?> FindSessionByIdAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Заводит диалог с новым секретным токеном. Сохраняется общим CommitAsync.</summary>
    ChatSessionEntity CreateSession(string? culture, string? page);

    /// <summary>Сколько сообщений посетитель прислал в этот диалог начиная с указанного момента.</summary>
    Task<int> CountRecentVisitorMessagesAsync(Guid sessionId, DateTime since, CancellationToken cancellationToken);

    /// <summary>Ответ гида с таким Id уже сохранён — значит Telegram доставил апдейт повторно.</summary>
    Task<bool> AdminReplyExistsAsync(long tgMessageId, CancellationToken cancellationToken);

    /// <summary>Добавляет сообщение, присваивает Ordinal и двигает счётчики сессии.</summary>
    ChatMessageEntity AddMessage(ChatSessionEntity session, ChatDirection direction, string text, long? tgMessageId);

    #region Доставка в Telegram

    Task<ChatDelivery?> GetDeliveryAsync(Guid messageId, CancellationToken cancellationToken);

    /// <summary>
    /// Outbox: сообщения посетителей, которые ещё не уехали в группу гидов. Очередь живёт
    /// в памяти и не переживает рестарт — недоставленное видно только здесь.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> FindUndeliveredMessageIdsAsync(DateTime since, int limit,
        CancellationToken cancellationToken);

    void MarkDelivered(ChatMessageEntity message, long tgMessageId);

    void SetSessionTopic(ChatSessionEntity session, long topicMessageId, long adminChatId);

    #endregion

    #region Приём ответа гида

    /// <summary>Гид ответил reply на сообщение посетителя — ищем диалог по Id этого сообщения в группе.</summary>
    Task<Guid?> FindSessionIdByTgMessageIdAsync(long tgMessageId, CancellationToken cancellationToken);

    /// <summary>Гид ответил reply на «шапку» диалога — запасной путь.</summary>
    Task<Guid?> FindSessionIdByTopicMessageIdAsync(long tgMessageId, CancellationToken cancellationToken);

    #endregion

    /// <summary>
    /// Жёстко удаляет переписку, которую пора забыть. Именно жёстко: мягкое удаление
    /// оставило бы персональные данные в базе навсегда.
    /// </summary>
    Task<int> DeleteExpiredAsync(DateTime edge, CancellationToken cancellationToken);
}
