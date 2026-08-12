using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain;
using Mediator;

namespace UI.Shared.Handlers;

public abstract class ApiCommandHandler<TRequest>(HttpClient httpClient)
    : ApiHandler<TRequest, ExecuteRequestResult> where TRequest : IRequest<ExecuteRequestResult>
{
    private record ProblemDetails(string? Title);

    public override async ValueTask<ExecuteRequestResult> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var response = await ExecuteHttpRequest(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new Exception(await ReadErrorMessageAsync(response, cancellationToken));

        return await response.Content.ReadFromJsonAsync<ExecuteRequestResult>(cancellationToken)
               ?? new ExecuteRequestResult(HttpStatusCode.Created, Guid.Empty);
    }

    protected virtual Task<HttpResponseMessage> ExecuteHttpRequest(TRequest request,
        CancellationToken cancellationToken) => httpClient.PostAsJsonAsync(ApiPath, request, cancellationToken);

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return "Слишком много заявок с вашего адреса. Попробуйте позже.";

        // Ответ бывает и без тела — например, когда запрос отсекается ограничителем на шлюзе,
        // — поэтому разбор JSON не должен превращаться в непонятную пользователю ошибку.
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);

            if (!string.IsNullOrWhiteSpace(problem?.Title))
                return problem.Title;
        }
        catch (JsonException)
        {
            // тело не JSON — покажем общее сообщение ниже
        }

        return "Не удалось отправить заявку. Попробуйте ещё раз.";
    }
}
