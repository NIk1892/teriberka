using System.Net;
using Application;
using Chat.Application.Abstract;
using Chat.Application.Notifications;
using Chat.Contracts;
using Domain;

namespace Chat.Application.Handlers;

/// <summary>
/// Сообщение посетителя. Диалог опознаётся по токену из cookie; неизвестный или пустой
/// токен заводит новый диалог, а не отвергает запрос — иначе первое сообщение было бы
/// невозможно отправить.
/// </summary>
public class ChatSendHandler(
    IChatRepository repository,
    IServiceProvider serviceProvider,
    IChatNotificationQueue notifications)
    : CommandHandler<ChatSendCommand, IChatRepository>(repository, serviceProvider)
{
    protected override async Task<ExecuteRequestResult> ExecuteCommand(ChatSendCommand request,
        CancellationToken cancellationToken)
    {
        var text = request.Text!.Trim();

        var session = string.IsNullOrEmpty(request.SessionToken)
            ? null
            : await Repository.FindSessionByTokenAsync(request.SessionToken, cancellationToken);

        if (session is null)
        {
            session = Repository.CreateSession(request.Culture, request.Page);
        }
        else
        {
            if (session.MessageCount >= ChatLimits.MaxMessagesPerSession)
                throw new ExcecuteCommandException(HttpStatusCode.TooManyRequests,
                    "В этом диалоге слишком много сообщений");

            // Ограничение по IP живёт на сайте и на шлюзе, но диалог переживает смену адреса,
            // поэтому флуд внутри одной сессии считаем отдельно.
            var since = DateTime.UtcNow.AddMinutes(-ChatLimits.BurstWindowMinutes);
            var recent = await Repository.CountRecentVisitorMessagesAsync(session.Id, since, cancellationToken);

            if (recent >= ChatLimits.MaxMessagesPerWindow)
                throw new ExcecuteCommandException(HttpStatusCode.TooManyRequests,
                    "Слишком много сообщений подряд. Подождите немного");
        }

        var message = Repository.AddMessage(session, ChatDirection.Visitor, text, tgMessageId: null);

        // Value — токен сессии: сайт кладёт его в cookie. Hash — Ordinal, чтобы клиент
        // сразу знал курсор и не получил своё же сообщение повторно первым опросом.
        return new ExecuteRequestResult(HttpStatusCode.Created, message.Id, session.Token, (uint)message.Ordinal);
    }

    protected override ValueTask AfterCommit(ChatSendCommand request, ExecuteRequestResult result)
    {
        // Только после успешного коммита: иначе бот мог бы отнести в группу сообщение,
        // которого в базе нет.
        if (result.Id is { } messageId)
            notifications.Enqueue(messageId);

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Ответ гида из Telegram-группы. Эндпоинта у команды нет — её шлёт только бот
/// внутри этого же сервиса.
/// </summary>
public class ChatAdminReplyHandler(
    IChatRepository repository,
    IServiceProvider serviceProvider)
    : CommandHandler<ChatAdminReplyCommand, IChatRepository>(repository, serviceProvider)
{
    protected override async Task<ExecuteRequestResult> ExecuteCommand(ChatAdminReplyCommand request,
        CancellationToken cancellationToken)
    {
        // Telegram повторяет недоставленные апдейты, поэтому один и тот же ответ может
        // прийти дважды. Уникальный индекс по TgMessageId это тоже ловит, но 409 в ответ
        // боту выглядел бы как ошибка, а здесь ничего не сломалось.
        if (await Repository.AdminReplyExistsAsync(request.TgMessageId, cancellationToken))
            return new ExecuteRequestResult(HttpStatusCode.OK);

        var session = await Repository.FindSessionByIdAsync(request.SessionId, cancellationToken)
                      ?? throw new ExcecuteCommandException(HttpStatusCode.NotFound, "Диалог не найден");

        var message = Repository.AddMessage(session, ChatDirection.Admin, request.Text!.Trim(), request.TgMessageId);

        return new ExecuteRequestResult(HttpStatusCode.Created, message.Id, null, (uint)message.Ordinal);
    }
}
