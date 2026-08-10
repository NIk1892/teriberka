
using Contracts;
using Domain;

namespace Application;

public interface IListQueryRepository<in TQuery, TDto, TEntity>
    where TDto : IDto
    where TQuery : ListQuery<TDto>
    where TEntity : IEntity
{
    Task<IReadOnlyCollection<TDto>> ListAsync(TQuery query, CancellationToken cancellationToken = default);
    Task<PagedList<TDto>> PagedListAsync(int pageIndex, int pageSize, TQuery query,
        CancellationToken cancellationToken = default);
}