using Microsoft.Extensions.Configuration;

namespace UI.Shared.Interceptors;

public class AuthorizationHeaderHandler(IConfiguration configuration) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = configuration["API_TOKEN"];
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new("Bearer", token);

        return base.SendAsync(request, cancellationToken);
    }
}
