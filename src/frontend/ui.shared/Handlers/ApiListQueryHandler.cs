using System.Net.Http.Json;
using Contracts;

namespace UI.Shared.Handlers;

public abstract class ApiListQueryHandler<TRequest, TDto>(HttpClient httpClient)
    : ApiHandler<TRequest, IReadOnlyCollection<TDto>>
    where TRequest : ListQuery<TDto>
    where TDto : Dto
{
    public override async ValueTask<IReadOnlyCollection<TDto>> Handle(
        TRequest request,
        CancellationToken cancellationToken)
        => await httpClient.GetFromJsonAsync<IReadOnlyCollection<TDto>>(BuildQueryString(request), cancellationToken)
           ?? [];
}
