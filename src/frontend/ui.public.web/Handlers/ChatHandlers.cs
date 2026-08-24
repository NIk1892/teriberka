using System.Net;
using System.Net.Http.Json;
using Chat.Contracts;
using Domain;
using Mediator;
using UI.Shared.Handlers;

namespace UI.Public.Web.Handlers;

/// <summary>
/// Отправка сообщения в chat-сервис. Handle переопределён целиком: базовый
/// ApiCommandHandler превращает любой неуспех в Exception со строкой, а виджету
/// нужен код — 429 «слишком часто» и 503 «сервис недоступен» показываются
/// по-разному.
/// </summary>
public class ChatSendCommandHandler(HttpClient httpClient) : ApiCommandHandler<ChatSendCommand>(httpClient)
{
    protected override string ApiPath => "api/public/chat/send";

    public override async ValueTask<ExecuteRequestResult> Handle(ChatSendCommand request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await httpClient.PostAsJsonAsync(ApiPath, request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // chat лежит или сеть до шлюза недоступна — для посетителя это «попробуйте позже»
            return new ExecuteRequestResult(HttpStatusCode.ServiceUnavailable);
        }

        if (!response.IsSuccessStatusCode)
            return new ExecuteRequestResult(response.StatusCode);

        return await response.Content.ReadFromJsonAsync<ExecuteRequestResult>(cancellationToken)
               ?? new ExecuteRequestResult(HttpStatusCode.InternalServerError);
    }
}

/// <summary>
/// Чтение переписки. BuildQueryString переопределён: QueryBuilder знает только
/// Limit/Offset/Text/Sorting и молча потерял бы token с after — вышел бы пустой
/// чат без единой ошибки.
/// </summary>
public class ChatMessageListQueryHandler(HttpClient httpClient)
    : ApiListQueryHandler<ChatMessageListQuery, ChatMessageDto>(httpClient)
{
    protected override string ApiPath => "api/public/chat/messages";

    protected override string BuildQueryString(IBaseRequest request)
    {
        var query = (ChatMessageListQuery)request;

        return $"{ApiPath}?token={Uri.EscapeDataString(query.Token ?? string.Empty)}" +
               $"&after={query.After}&limit={query.Limit ?? ChatLimits.PageSize}";
    }
}
