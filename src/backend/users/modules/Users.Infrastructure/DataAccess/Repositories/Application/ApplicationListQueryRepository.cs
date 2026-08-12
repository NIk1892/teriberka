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
}
