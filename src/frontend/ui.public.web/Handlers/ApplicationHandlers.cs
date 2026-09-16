using System.Net.Http.Json;
using Mediator;
using UI.Shared.Handlers;
using Applications.Contracts;

namespace UI.Public.Web.Handlers;

public class ApplicationCreateCommandHandler(HttpClient httpClient)
    : ApiCommandHandler<ApplicationCreateCommand>(httpClient)
{
    protected override string ApiPath => "api/public/application/create";
}

// Страница заявок /manager. Admin-маршруты шлюза закрыты политикой Admin — пропускает
// служебный токен API_TOKEN, который AuthorizationHeaderHandler кладёт в каждый запрос.
public class ApplicationPagedListQueryHandler(HttpClient httpClient)
    : ApiPagedListQueryHandler<ApplicationPagedListQuery, ApplicationListQuery, ApplicationDto>(httpClient)
{
    protected override string ApiPath => "api/admin/application/pagedList";

    // Общий построитель строки запроса знает только limit/offset/text/sorting — фильтр
    // по статусу он бы потерял, поэтому адрес собирается здесь целиком.
    protected override string BuildQueryString(IBaseRequest request)
    {
        var paged = (ApplicationPagedListQuery)request;
        var status = paged.Query?.Status is { Length: > 0 } value ? "&status=" + Uri.EscapeDataString(value) : "";
        return $"{ApiPath}?pageIndex={paged.PageIndex}&pageSize={paged.PageSize}{status}";
    }
}

public class ApplicationProcessCommandHandler(HttpClient httpClient)
    : ApiCommandHandler<ApplicationProcessCommand>(httpClient)
{
    protected override string ApiPath => "api/admin/application/update";

    protected override Task<HttpResponseMessage> ExecuteHttpRequest(ApplicationProcessCommand request,
        CancellationToken cancellationToken) => httpClient.PutAsJsonAsync(ApiPath, request, cancellationToken);
}
