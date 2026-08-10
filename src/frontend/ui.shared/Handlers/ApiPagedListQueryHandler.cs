using System.Net.Http.Json;
using Contracts;
using Mediator;
using UI.Shared.Helpers;

namespace UI.Shared.Handlers;

public abstract class ApiPagedListQueryHandler<TPagedRequest, TRequest, TDto>(HttpClient httpClient)
    : ApiHandler<TPagedRequest, PagedList<TDto>>
    where TPagedRequest : PagedListQuery<TDto, TRequest>
    where TRequest : ListQuery<TDto>
    where TDto : Dto
{
    public override async ValueTask<PagedList<TDto>> Handle(
        TPagedRequest request,
        CancellationToken cancellationToken)
        => await httpClient.GetFromJsonAsync<PagedList<TDto>>(BuildQueryString(request), cancellationToken)
           ?? new PagedList<TDto>([], 0, 0, 0);

    protected override string BuildQueryString(IBaseRequest request)
        => ((TPagedRequest)request).Query.ToQueryString(base.BuildQueryString(request), true);
}
