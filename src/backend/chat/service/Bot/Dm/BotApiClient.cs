using System.Net;
using System.Net.Http.Json;
using Applications.Contracts;
using Domain;
using Microsoft.AspNetCore.Mvc;

namespace Chat.Bot.Dm;

public enum SubmitOutcome
{
    Created,

    /// <summary>Сервер отверг данные (400/422) — Title из ProblemDetails говорит, что именно.</summary>
    Invalid,

    RateLimited,

    /// <summary>Шлюз или users недоступны, токен отвергнут, таймаут — черновик сохраняется, пробовать позже.</summary>
    Unavailable
}

public sealed record SubmitResult(SubmitOutcome Outcome, string? Title = null, Guid? Id = null);

/// <summary>
/// Заявка из бота уходит в users только через шлюз — правило проекта: сервисы друг в
/// друга напрямую не ходят. Маршрут приватный, со служебным JWT (API_TOKEN), без
/// лимитера public-form: шлюз видит один IP контейнера chat на всех пользователей бота.
/// Клиент обычный, без ProxyHttpClient — шлюз внутри compose-сети.
/// </summary>
public sealed class BotApiClient(HttpClient http, ILogger<BotApiClient> logger)
{
    private const string CreatePath = "api/private/application/create";

    public async Task<SubmitResult> CreateApplicationAsync(ApplicationCreateCommand command, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await http.PostAsJsonAsync(CreatePath, command, cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "Шлюз не ответил — заявка из бота не отправлена, черновик сохранён");
            return new SubmitResult(SubmitOutcome.Unavailable);
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.Created or HttpStatusCode.OK:
                {
                    var result = await ReadAsync<ExecuteRequestResult>(response, cancellationToken);
                    logger.LogInformation("Заявка {ApplicationId} из бота принята шлюзом", result?.Id);
                    return new SubmitResult(SubmitOutcome.Created, null, result?.Id);
                }

                case HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity:
                {
                    // Тексты валидатора без персональных данных — их можно и в лог, и пользователю.
                    var problem = await ReadAsync<ProblemDetails>(response, cancellationToken);
                    logger.LogWarning("Шлюз отверг заявку из бота ({StatusCode}): {Title}", (int)response.StatusCode, problem?.Title);
                    return new SubmitResult(SubmitOutcome.Invalid, problem?.Title);
                }

                case HttpStatusCode.TooManyRequests:
                    logger.LogWarning("Шлюз ограничил заявки из бота (429)");
                    return new SubmitResult(SubmitOutcome.RateLimited);

                case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                    logger.LogError("Шлюз отверг API_TOKEN бота ({StatusCode}) — проверьте токен и JWT_KEY", (int)response.StatusCode);
                    return new SubmitResult(SubmitOutcome.Unavailable);

                default:
                    logger.LogWarning("Шлюз ответил {StatusCode} на заявку из бота", (int)response.StatusCode);
                    return new SubmitResult(SubmitOutcome.Unavailable);
            }
        }
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Пустое или не-JSON тело: статуса для решения достаточно.
            return default;
        }
    }
}
