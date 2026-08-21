using Chat.Contracts;
using Chat.Domain;
using Infrastructure.DataAccess;
using Infrastructure.Mappers;

namespace Chat.Infrastructure.DataAccess;

public class ChatMessageListQueryRepository(
    ReadChatDbContext dbContext,
    IEntityToDtoMapper<ChatMessageDto, ChatMessageEntity> mapper)
    : ListQueryRepository<ChatMessageListQuery, ChatMessageDto, ChatMessageEntity>(dbContext, mapper)
{
    private readonly ReadChatDbContext _dbContext = dbContext;

    protected override IQueryable<ChatMessageEntity> ProcessDbQuery(ChatMessageListQuery query,
        IQueryable<ChatMessageEntity> dbQuery)
    {
        // Единственный ключ к переписке — секрет из cookie. Нет токена — нет и выборки:
        // запрос без него не должен возвращать чужие сообщения ни при каких условиях.
        if (string.IsNullOrEmpty(query.Token))
            return dbQuery.Where(_ => false);

        var sessionIds = _dbContext.Set<ChatSessionEntity>()
            .Where(s => s.Token == query.Token && !s.IsDeleted)
            .Select(s => s.Id);

        return dbQuery.Where(m => sessionIds.Contains(m.SessionId) && m.Ordinal > query.After);
    }

    // Базовая реализация ищет по Title, которого у сообщений чата нет.
    protected override IQueryable<ChatMessageEntity> ProcessTextQuery(ChatMessageListQuery query,
        IQueryable<ChatMessageEntity> dbQuery) => dbQuery;

    // Переписка читается лентой снизу вверх по порядковому номеру — сортировка по Id
    // из базового репозитория здесь бессмысленна.
    protected override IQueryable<ChatMessageEntity> ProcessSorting(string sorting,
        IQueryable<ChatMessageEntity> dbQuery) => dbQuery.OrderBy(x => x.Ordinal);
}
