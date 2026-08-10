using System.Net.Http.Json;
using Contracts;
using Domain;

namespace UI.Shared.Handlers;

public abstract class ApiSingleQueryHandler<TRequest, TDto>(HttpClient httpClient)
    : ApiHandler<TRequest, TDto>
    where TRequest : Query<TDto>
    where TDto : IDto
{
    public override async ValueTask<TDto> Handle(TRequest request, CancellationToken cancellationToken)
        => await httpClient.GetFromJsonAsync<TDto>(BuildQueryString(request), cancellationToken)
           ?? throw new InvalidOperationException("Empty API response");
}
