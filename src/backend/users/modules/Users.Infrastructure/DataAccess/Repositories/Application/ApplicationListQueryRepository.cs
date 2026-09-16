using Applications.Contracts;
using Infrastructure.DataAccess;
using Infrastructure.Mappers;
using Users.Domain;

namespace Users.Infrastructure.DataAccess;

public class ApplicationListQueryRepository(
    ReadApplicationDbContext dbContext,
    IEntityToDtoMapper<ApplicationDto, ApplicationEntity> mapper)
    : ListQueryRepository<ApplicationListQuery, ApplicationDto, ApplicationEntity>(dbContext, mapper)
{
    // Оператор читает заявки как ленту, поэтому по умолчанию — свежие сверху,
    // а не сортировка по Id из базового репозитория.
    protected override IQueryable<ApplicationEntity> ProcessSorting(string sorting,
        IQueryable<ApplicationEntity> dbQuery)
        => dbQuery.OrderByDescending(x => x.Audit!.CreatedAt);

    protected override IQueryable<ApplicationEntity> ProcessDbQuery(ApplicationListQuery query,
        IQueryable<ApplicationEntity> dbQuery) => query.Status switch
    {
        ApplicationStatuses.New => dbQuery.Where(x => x.ProcessedAt == null),
        ApplicationStatuses.Done => dbQuery.Where(x => x.ProcessedAt != null),
        _ => dbQuery
    };
}
