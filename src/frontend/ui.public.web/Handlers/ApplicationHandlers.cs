using UI.Shared.Handlers;
using Applications.Contracts;

namespace UI.Public.Web.Handlers;

public class ApplicationCreateCommandHandler(HttpClient httpClient)
    : ApiCommandHandler<ApplicationCreateCommand>(httpClient)
{
    protected override string ApiPath => "api/public/application/create";
}
