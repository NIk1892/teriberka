using System.Net;
using Application;
using Chat.Application.Abstract;
using Chat.Contracts;
using Chat.Domain;
using Domain;
using Mediator;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Chat.Bot.Dm;

/// <summary>
/// Раннер лички: апдейт → диалог из БД → машина состояний → эффекты → отправка → сохранение.
/// Состояние сохраняется ДО отправки подтверждения: если бы commit шёл после и упал, заявка
/// уже была бы принята, а пользователь нажал бы «Отправить» ещё раз.
///
/// Апдейты обрабатываются последовательно (ReceiveAsync ждёт handler), поэтому две правки
/// одной строки диалога одновременно не случаются.
/// </summary>
public sealed class BotDmHandler(
    IServiceScopeFactory scopeFactory,
    BotOptions options,
    BotSender sender,
    ILogger<BotDmHandler> logger)
{
    /// <summary>
    /// Апдейты при старте не отбрасываются (иначе терялись бы ответы гидов), поэтому от
    /// пачки приветствий на вчерашние «/start» защищает возраст сообщения. Ответ на шаг
    /// активного диалога по возрасту не отбрасывается — телефон, набранный в момент
    /// деплоя, должен дойти.
    /// </summary>
    private static readonly TimeSpan StaleMessageAge = TimeSpan.FromMinutes(10);

    /// <summary>Режим «написать менеджеру» без сообщений дольше этого — снова меню.</summary>
    private static readonly TimeSpan ChatModeTimeout = TimeSpan.FromHours(24);

    private readonly BotDialogMachine _machine = new();

    public Task HandleMessageAsync(ITelegramBotClient client, Message message, CancellationToken cancellationToken)
    {
        if (message.From is not { IsBot: false } from)
            return Task.CompletedTask;

        var stale = DateTime.UtcNow - message.Date > StaleMessageAge;

        return ProcessAsync(client, message.Chat.Id, from, Classify(message, from), stale,
            userMessageId: message.MessageId, callback: null, cancellationToken);
    }

    public async Task HandleCallbackAsync(ITelegramBotClient client, CallbackQuery query, CancellationToken cancellationToken)
    {
        if (query.Message is not { } message)
        {
            // Сообщение недоступно (слишком старое) — остаётся только погасить «часики».
            await sender.AnswerCallbackAsync(client, query.Id, null, cancellationToken);
            return;
        }

        await ProcessAsync(client, message.Chat.Id, query.From, new CallbackInput(query.Data ?? string.Empty), stale: false,
            userMessageId: null, callback: query, cancellationToken);
    }

    private async Task ProcessAsync(ITelegramBotClient client, long chatId, User from, DialogInput input, bool stale,
        int? userMessageId, CallbackQuery? callback, CancellationToken cancellationToken)
    {
        string? toast = null;
        var lang = BotStrings.Lang(from.LanguageCode);
        var step = BotDialogStep.Idle;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var services = scope.ServiceProvider;
            var dialogs = services.GetRequiredService<IBotDialogRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();

            var dialog = await dialogs.FindByUserIdAsync(from.Id, cancellationToken);

            if (stale && (dialog is null || dialog.Step == BotDialogStep.Idle))
            {
                logger.LogDebug("Старое сообщение из чата {ChatId} без активного диалога пропущено", chatId);
                return;
            }

            var now = DateTime.UtcNow;
            dialog ??= dialogs.Create(from.Id, chatId, lang);

            if (dialog.Step == BotDialogStep.Chat && now - dialog.LastActivityAt > ChatModeTimeout)
                dialog.Step = BotDialogStep.Idle;

            dialog.Lang = lang;
            dialog.TgChatId = chatId;
            dialog.LastActivityAt = now;
            step = dialog.Step;

            // Нужно только для свободного текста в меню: уже писал менеджеру — пересылаем без вопросов.
            var hasChatSession = input is TextInput && dialog.Step == BotDialogStep.Idle
                && await services.GetRequiredService<IChatRepository>().FindSessionByTgChatIdAsync(chatId, cancellationToken) is not null;

            var ctx = new MachineContext(lang, from.Id, from.FirstName, from.LastName, from.Username, options, hasChatSession, now);

            var replies = await RunAsync(services, dialog, input, ctx, chatId, callback is not null, cancellationToken);

            await unitOfWork.CommitAsync(cancellationToken);

            var (outcome, toastText) = await DeliverAsync(client, chatId, userMessageId, callback, dialog, replies, cancellationToken);
            toast = toastText;

            if (outcome == SendOutcome.Unreachable)
            {
                // Заблокировал бота — черновик с телефоном держать незачем.
                dialog.ClearDraft();
                dialog.Step = BotDialogStep.Idle;
                dialog.ScreenMessageId = null;
            }

            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            // Упавший handler Telegram.Bot считает обработанным — повтора не будет, поэтому
            // пользователь должен хоть что-то увидеть. Текст сообщения в лог не пишем.
            logger.LogWarning(e, "Апдейт из чата {ChatId} не обработан (шаг {Step})", chatId, step);
            await sender.SendAsync(client, chatId, BotScreens.Error(lang), null, cancellationToken);
        }
        finally
        {
            if (callback is not null)
                await sender.AnswerCallbackAsync(client, callback.Id, toast, cancellationToken);
        }
    }

    /// <summary>Прогоняет вход через машину и исполняет эффекты, подставляя их результат вторым вызовом.</summary>
    private async Task<List<BotReply>> RunAsync(IServiceProvider services, BotDialogEntity dialog, DialogInput input,
        MachineContext ctx, long chatId, bool inPlace, CancellationToken cancellationToken)
    {
        var result = new List<BotReply>();

        foreach (var reply in _machine.Handle(dialog, input, ctx))
        {
            switch (reply)
            {
                case SubmitEffect submit:
                {
                    var api = services.GetRequiredService<BotApiClient>();
                    var submitResult = await api.CreateApplicationAsync(submit.Command, cancellationToken);
                    result.AddRange(_machine.Handle(dialog, new SubmitResultInput(submitResult, inPlace), ctx));
                    break;
                }

                case ForwardEffect forward:
                {
                    var outcome = await ForwardAsync(services, chatId, forward.Text, ctx, cancellationToken);
                    result.AddRange(_machine.Handle(dialog, new ForwardResultInput(outcome, forward.Announce, inPlace), ctx));
                    break;
                }

                default:
                    result.Add(reply);
                    break;
            }
        }

        return result;
    }

    private async Task<ForwardOutcome> ForwardAsync(IServiceProvider services, long chatId, string text, MachineContext ctx,
        CancellationToken cancellationToken)
    {
        var mediator = services.GetRequiredService<IMediator>();

        try
        {
            // Через Mediator, а не в репозиторий: так работают валидатор, общий CommitAsync и
            // AfterCommit, который относит сообщение в группу гидов.
            var result = await mediator.Send(new ChatTelegramSendCommand
            {
                TgChatId = chatId,
                Text = text,
                Lang = ctx.Lang,
                Username = string.IsNullOrWhiteSpace(ctx.Username) ? null : ctx.Username,
            }, cancellationToken);

            if (result.IsSuccess)
            {
                logger.LogInformation("Сообщение из лички {ChatId} передано в диалог гидов", chatId);
                return ForwardOutcome.Sent;
            }

            logger.LogWarning("Сообщение из лички {ChatId} не сохранено: {StatusCode}", chatId, result.StatusCode);
            return result.StatusCode == HttpStatusCode.TooManyRequests ? ForwardOutcome.TooMany : ForwardOutcome.Failed;
        }
        catch (ExcecuteCommandException e) when (e.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return ForwardOutcome.TooMany;
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "Сообщение из лички {ChatId} не сохранено", chatId);
            return ForwardOutcome.Failed;
        }
    }

    private async Task<(SendOutcome Outcome, string? Toast)> DeliverAsync(ITelegramBotClient client, long chatId, int? userMessageId,
        CallbackQuery? callback, BotDialogEntity dialog, List<BotReply> replies, CancellationToken cancellationToken)
    {
        string? toast = null;

        foreach (var reply in replies)
        {
            SendOutcome outcome;

            switch (reply)
            {
                case ScreenReply screen:
                {
                    int? messageId;

                    if (screen.EditInPlace && callback?.Message is { } tapped)
                    {
                        (outcome, messageId) = await sender.EditAsync(client, chatId, tapped.MessageId, screen.Html, screen.Keyboard, cancellationToken);
                    }
                    else
                    {
                        await ClearPreviousScreenAsync(client, chatId, dialog, cancellationToken);
                        (outcome, messageId) = await sender.SendAsync(client, chatId, screen.Html, screen.Keyboard, cancellationToken);
                    }

                    if (messageId is not null)
                        dialog.ScreenMessageId = screen.Keyboard is null ? null : messageId;

                    break;
                }

                case KeyboardReply keyboard:
                    await ClearPreviousScreenAsync(client, chatId, dialog, cancellationToken);
                    (outcome, _) = await sender.SendAsync(client, chatId, keyboard.Html, keyboard.Keyboard, cancellationToken);
                    break;

                case AckReply ack:
                    (outcome, _) = await sender.SendAsync(client, chatId, ack.Html, new ReplyKeyboardRemove(), cancellationToken);
                    break;

                case ToastReply t:
                    if (callback is not null)
                    {
                        toast = t.Text;
                        outcome = SendOutcome.Sent;
                    }
                    else
                    {
                        (outcome, _) = await sender.SendAsync(client, chatId, t.Text, null, cancellationToken);
                    }

                    break;

                case ReactReply react:
                    if (userMessageId is { } target)
                        await sender.ReactAsync(client, chatId, target, react.Emoji, cancellationToken);

                    outcome = SendOutcome.Sent;
                    break;

                default:
                    outcome = SendOutcome.Sent;
                    break;
            }

            if (outcome == SendOutcome.Unreachable)
                return (SendOutcome.Unreachable, toast);
        }

        return (SendOutcome.Sent, toast);
    }

    /// <summary>Перед новым экраном снимаем кнопки со старого, чтобы в переписке не висели два живых меню.</summary>
    private async Task ClearPreviousScreenAsync(ITelegramBotClient client, long chatId, BotDialogEntity dialog, CancellationToken cancellationToken)
    {
        if (dialog.ScreenMessageId is { } previous)
        {
            await sender.ClearKeyboardAsync(client, chatId, previous, cancellationToken);
            dialog.ScreenMessageId = null;
        }
    }

    private static DialogInput Classify(Message message, User from)
    {
        if (message.Contact is { } contact)
            return new ContactInput(contact.PhoneNumber, contact.UserId == from.Id);

        if (message.Text is not { Length: > 0 } text)
            return new UnsupportedInput();

        if (!text.StartsWith('/'))
            return new TextInput(text);

        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var name = parts[0][1..];

        // «/start@бот» — Telegram дописывает имя бота при выборе команды из подсказки.
        var at = name.IndexOf('@');
        if (at >= 0)
            name = name[..at];

        return new CommandInput(name.ToLowerInvariant(), parts.Length > 1 ? parts[1] : null);
    }
}
