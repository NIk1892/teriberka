using Mediator;
using Contracts;
using Domain;

namespace Application;

public abstract class ListQueryHandler<TRequest, TDto, TEntity, TRepository>(
    TRepository repository)
    : IRequestHandler<TRequest, IReadOnlyCollection<TDto>>
    where TRequest : ListQuery<TDto>
    where TDto : IDto
    where TEntity : IEntity
    where TRepository : IListQueryRepository<TRequest, TDto, TEntity>
{
    public virtual ValueTask<IReadOnlyCollection<TDto>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        if (request.Limit > Constants.LimitSelectRecords)
            throw new RequestValidationException("Limit value is exceeded");

        return new(repository.ListAsync(request, cancellationToken));
    }
}