using System.Net;
using Application;
using Chat.Application.Abstract;
using Chat.Application.Notifications;
using Chat.Contracts;
using Domain;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Chat.Bot;

/// <summary>
/// Относит сообщения посетителей в группу гидов. Отдельный BackgroundService, а не
/// прямой вызов в обработчике команды: отправка в Telegram — сетевой вызов на сотни
/// миллисекунд, держать на нём запрос посетителя нельзя, а недоступность мессенджера
/// не должна превращаться в ошибку отправки сообщения.
///
/// Надёжность держится на самой БД: сообщение с TgMessageId IS NULL — это и есть
/// невывезенный outbox. Очередь в памяти лишь ускоряет доставку свежих; всё, что
/// потерялось при рестарте или падении Telegram, подбирает подметание.
/// </summary>
public sealed class ChatNotificationDispatcher(
    IChatNotificationQueue queue,
    TelegramBotAccessor bot,
    IServiceScopeFactory scopeFactory,
    ILogger<ChatNotificationDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    /// <summary>Насколько старые недоставленные сообщения ещё имеет смысл отправлять.</summary>
    private static readonly TimeSpan SweepDepth = TimeSpan.FromHours(24);

    private const int SweepBatch = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!bot.CanDeliver)
        {
            logger.LogInformation("Доставка сообщений чата в Telegram выключена — нет токена или TG_ADMIN_CHAT_ID");
            return;
        }

        var sweeping = SweepLoopAsync(stoppingToken);

        try
        {
            await foreach (var messageId in queue.ReadAllAsync(stoppingToken))
                await SafeDeliverAsync(messageId, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }

        await sweeping;
    }

    /// <summary>
    /// Подбирает то, что не уехало: сообщения из упавшей очереди, из времени без сети
    /// и из периода, когда токен ещё не был настроен.
    /// </summary>
    private async Task SweepLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IChatRepository>();

                var pending = await repository.FindUndeliveredMessageIdsAsync(
                    DateTime.UtcNow - SweepDepth, SweepBatch, stoppingToken);

                if (pending.Count == 0)
                    continue;

                logger.LogInformation("Подметание outbox'а: {Count} недоставленных сообщений", pending.Count);

                foreach (var messageId in pending)
                    await SafeDeliverAsync(messageId, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SafeDeliverAsync(Guid messageId, CancellationToken cancellationToken)
    {
        try
        {
            await DeliverAsync(messageId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            // Текст сообщения в лог не пишем никогда — это персональные данные.
            // Не доехало — останется в outbox'е и уедет следующим подметанием.
            logger.LogWarning(e, "Не удалось доставить сообщение {MessageId} в группу гидов", messageId);
        }
    }

    private async Task DeliverAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var client = bot.Client!;
        var adminChatId = bot.AdminChatId!.Value;

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IChatRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var delivery = await repository.GetDeliveryAsync(messageId, cancellationToken);

        if (delivery is null)
            return;

        var (message, session) = delivery;

        // Уже доставлено (например, очередь и подметание взяли одно и то же) — второй раз не шлём.
        if (message.TgMessageId is not null || message.Direction != ChatDirection.Visitor)
            return;

        var chatId = session.AdminChatId ?? adminChatId;

        if (session.TopicMessageId is null)
        {
            var header = await client.SendMessage(
                chatId,
                BotTexts.SessionHeader(bot.AdminLanguage, ShortId(session.Id), session.Culture, session.Page),
                parseMode: ParseMode.Html,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                cancellationToken: cancellationToken);

            repository.SetSessionTopic(session, header.MessageId, chatId);

            // Коммитим сразу: если следующая отправка упадёт, шапка не потеряется и
            // повторная попытка не создаст в группе вторую.
            await unitOfWork.CommitAsync(cancellationToken);
        }

        var sent = await client.SendMessage(
            chatId,
            Escape(message.Text),
            parseMode: ParseMode.Html,
            replyParameters: new ReplyParameters
            {
                MessageId = (int)session.TopicMessageId!.Value,
                // Шапку могли удалить в группе руками — сообщение всё равно должно уйти.
                AllowSendingWithoutReply = true
            },
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            cancellationToken: cancellationToken);

        repository.MarkDelivered(message, sent.MessageId);

        await unitOfWork.CommitAsync(cancellationToken);

        logger.LogInformation("Сообщение #{Ordinal} диалога {SessionId} доставлено в группу гидов",
            message.Ordinal, session.Id);
    }

    /// <summary>Короткая метка диалога для глаз гида; сам токен в группу не уходит.</summary>
    internal static string ShortId(Guid sessionId) => sessionId.ToString("N")[..6].ToUpperInvariant();

    /// <summary>
    /// Текст посетителя идёт в parseMode Html, поэтому угловые скобки и амперсанд
    /// обязаны быть экранированы — иначе Telegram отвергнет сообщение или съест кусок текста.
    /// </summary>
    private static string Escape(string? text)
    {
        var value = text ?? string.Empty;

        if (value.Length > ChatLimits.MaxTextLength)
            value = value[..ChatLimits.MaxTextLength];

        return WebUtility.HtmlEncode(value);
    }
}
