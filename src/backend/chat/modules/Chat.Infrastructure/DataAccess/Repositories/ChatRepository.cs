using Chat.Application.Abstract;
using Chat.Domain;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Chat.Infrastructure.DataAccess;

/// <summary>
/// Запись чата. Не наследник generic CommandRepository: сообщение создаётся не из команды
/// один-в-один, а вместе с обновлением счётчиков диалога — и обязательно в одной транзакции,
/// иначе два одновременных сообщения получили бы одинаковый Ordinal.
///
/// Регистрируется вручную в Configurator: Scrutor сканирует только наследников
/// CommandRepository/QueryRepository.
/// </summary>
public class ChatRepository(WriteChatDbContext context) : IChatRepository
{
    private readonly DbSet<ChatSessionEntity> _sessions = context.Set<ChatSessionEntity>();
    private readonly DbSet<ChatMessageEntity> _messages = context.Set<ChatMessageEntity>();

    public Task<ChatSessionEntity?> FindSessionByTokenAsync(string token, CancellationToken cancellationToken)
        => _sessions.FirstOrDefaultAsync(s => s.Token == token && !s.IsDeleted, cancellationToken);

    public Task<ChatSessionEntity?> FindSessionByIdAsync(Guid sessionId, CancellationToken cancellationToken)
        => _sessions.FirstOrDefaultAsync(s => s.Id == sessionId && !s.IsDeleted, cancellationToken);

    public ChatSessionEntity CreateSession(string? culture, string? page)
    {
        var session = new ChatSessionEntity
        {
            Token = ChatTokens.New(),
            Culture = culture,
            Page = page,
            LastMessageAt = DateTime.UtcNow
        };

        // Add, а не AddAsync: Id генерируется NpgsqlSequentialGuidValueGenerator сразу,
        // и он нужен следующей строкой — сообщению этой сессии.
        _sessions.Add(session);

        return session;
    }

    public Task<int> CountRecentVisitorMessagesAsync(Guid sessionId, DateTime since,
        CancellationToken cancellationToken)
        => _messages.CountAsync(
            m => m.SessionId == sessionId
                 && m.Direction == ChatDirection.Visitor
                 && m.Audit!.CreatedAt >= since,
            cancellationToken);

    public Task<bool> AdminReplyExistsAsync(long tgMessageId, CancellationToken cancellationToken)
        => _messages.AnyAsync(m => m.TgMessageId == tgMessageId, cancellationToken);

    public ChatMessageEntity AddMessage(ChatSessionEntity session, ChatDirection direction, string text,
        long? tgMessageId)
    {
        session.MessageCount += 1;
        session.LastMessageAt = DateTime.UtcNow;

        var message = new ChatMessageEntity
        {
            SessionId = session.Id,
            Ordinal = session.MessageCount,
            Direction = direction,
            Text = text,
            TgMessageId = tgMessageId
        };

        _messages.Add(message);

        return message;
    }

    public async Task<ChatDelivery?> GetDeliveryAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await _messages.FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);
        if (message is null)
            return null;

        var session = await _sessions.FirstOrDefaultAsync(s => s.Id == message.SessionId, cancellationToken);

        return session is null ? null : new ChatDelivery(message, session);
    }

    public async Task<IReadOnlyCollection<Guid>> FindUndeliveredMessageIdsAsync(DateTime since, int limit,
        CancellationToken cancellationToken)
        => await _messages
            .AsNoTracking()
            .Where(m => m.TgMessageId == null
                        && m.Direction == ChatDirection.Visitor
                        && !m.IsDeleted
                        && m.Audit!.CreatedAt >= since)
            .OrderBy(m => m.Audit!.CreatedAt)
            .Take(limit)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

    public void MarkDelivered(ChatMessageEntity message, long tgMessageId)
        => message.TgMessageId = tgMessageId;

    public void SetSessionTopic(ChatSessionEntity session, long topicMessageId, long adminChatId)
    {
        session.TopicMessageId = topicMessageId;
        session.AdminChatId = adminChatId;
    }

    public Task<Guid?> FindSessionIdByTgMessageIdAsync(long tgMessageId, CancellationToken cancellationToken)
        => _messages
            .AsNoTracking()
            .Where(m => m.TgMessageId == tgMessageId)
            .Select(m => (Guid?)m.SessionId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Guid?> FindSessionIdByTopicMessageIdAsync(long tgMessageId, CancellationToken cancellationToken)
        => _sessions
            .AsNoTracking()
            .Where(s => s.TopicMessageId == tgMessageId && !s.IsDeleted)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int> DeleteExpiredAsync(DateTime edge, CancellationToken cancellationToken)
    {
        // Сначала сообщения, потом сами диалоги: внешнего ключа между таблицами нет,
        // порядок держит базу консистентной, даже если процесс упадёт между шагами.
        var expired = _sessions.Where(s => s.LastMessageAt < edge).Select(s => s.Id);

        await _messages
            .Where(m => expired.Contains(m.SessionId))
            .ExecuteDeleteAsync(cancellationToken);

        return await _sessions
            .Where(s => s.LastMessageAt < edge)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
