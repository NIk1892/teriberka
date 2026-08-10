using Mediator;
using Microsoft.Extensions.Logging;
using Contracts;
using Domain;

namespace Application;

public abstract class SingleQueryHandler<TRequest, TDto, TEntity, TRepository>(
    TRepository repository)
    : IRequestHandler<TRequest, TDto>
    where TRequest : Query<TDto>
    where TDto : IDto
    where TEntity : IEntity
    where TRepository : IQueryRepository<TRequest, TDto,TEntity>
{
    public ValueTask<TDto> Handle(TRequest request, CancellationToken cancellationToken) =>
        new(repository.SingleAsync(request,cancellationToken));
}