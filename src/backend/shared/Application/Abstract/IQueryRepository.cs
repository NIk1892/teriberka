
using Contracts;
using Domain;

namespace Application;

public interface IQueryRepository<in TQuery, TDto, TEntity>
    where TDto : IDto
    where TEntity : IEntity
    where TQuery : Query<TDto>
{
    Task<TDto?> SingleAsync(TQuery query, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(TQuery query, CancellationToken cancellationToken = default);
}