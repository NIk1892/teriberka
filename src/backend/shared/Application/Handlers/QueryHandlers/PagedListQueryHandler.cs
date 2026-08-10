using Mediator;
using Contracts;
using Domain;

namespace Application;

public abstract class PagedListQueryHandler<TPagedRequest, TRequest, TDto, TEntity, TRepository>(
    TRepository repository) : IRequestHandler<TPagedRequest, PagedList<TDto>>
    where TPagedRequest : PagedListQuery<TDto, TRequest>
    where TRequest : ListQuery<TDto>
    where TDto : IDto
    where TEntity : IEntity
    where TRepository : IListQueryRepository<TRequest, TDto,TEntity>
{
    public ValueTask<PagedList<TDto>> Handle(TPagedRequest request, CancellationToken cancellationToken)
    {
        if (request.PageSize > Constants.LimitSelectRecords)
            throw new RequestValidationException($"{nameof(request.PageSize)} value is exceeded");

        if (request.PageIndex < 1)
            throw new RequestValidationException($"{nameof(request.PageIndex)} value is wrong");

        if (request.PageSize < 1)
            throw new RequestValidationException($"{nameof(request.PageSize)} value is wrong");

        return new(repository.PagedListAsync(request.PageIndex, request.PageSize, request.Query, cancellationToken));
    }
}