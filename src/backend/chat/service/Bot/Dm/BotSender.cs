using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Chat.Bot.Dm;

public enum SendOutcome
{
    Sent,

    /// <summary>Пользователь заблокировал бота или удалил аккаунт — писать ему больше нельзя.</summary>
    Unreachable,

    Failed
}

/// <summary>
/// Отправка и правка сообщений лички с разбором ошибок Bot API. Всё — HTML, без превью
/// ссылок. Правка на месте деградирует в новое сообщение, если старое уже нельзя править.
/// </summary>
public sealed class BotSender(ILogger<BotSender> logger)
{
    public async Task<(SendOutcome Outcome, int? MessageId)> SendAsync(ITelegramBotClient client, long chatId, string html,
        ReplyMarkup? markup, CancellationToken cancellationToken)
    {
        try
        {
            var sent = await client.SendMessage(chatId, html,
                parseMode: ParseMode.Html,
                replyMarkup: markup,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                cancellationToken: cancellationToken);

            return (SendOutcome.Sent, sent.MessageId);
        }
        catch (ApiRequestException e) when (IsUnreachable(e))
        {
            logger.LogInformation("Чат {ChatId} недоступен ({ErrorCode}: {Message})", chatId, e.ErrorCode, e.Message);
            return (SendOutcome.Unreachable, null);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "Не удалось отправить сообщение в чат {ChatId}", chatId);
            return (SendOutcome.Failed, null);
        }
    }

    public async Task<(SendOutcome Outcome, int? MessageId)> EditAsync(ITelegramBotClient client, long chatId, int messageId,
        string html, InlineKeyboardMarkup? markup, CancellationToken cancellationToken)
    {
        try
        {
            await client.EditMessageText(chatId, messageId, html,
                parseMode: ParseMode.Html,
                replyMarkup: markup,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                cancellationToken: cancellationToken);

            return (SendOutcome.Sent, messageId);
        }
        catch (ApiRequestException e) when (e.ErrorCode == 400 && e.Message.Contains("not modified", StringComparison.OrdinalIgnoreCase))
        {
            // Нажали ту же кнопку ещё раз — экран и так такой.
            return (SendOutcome.Sent, messageId);
        }
        catch (ApiRequestException e) when (IsUnreachable(e))
        {
            logger.LogInformation("Чат {ChatId} недоступен ({ErrorCode}: {Message})", chatId, e.ErrorCode, e.Message);
            return (SendOutcome.Unreachable, null);
        }
        catch (ApiRequestException e) when (e.ErrorCode == 400 && !cancellationToken.IsCancellationRequested)
        {
            // Сообщение старше 48 часов, удалено или было с reply-клавиатурой — шлём новое.
            logger.LogDebug("Сообщение {MessageId} в чате {ChatId} не правится ({Message}) — отправляю новое", messageId, chatId, e.Message);
            return await SendAsync(client, chatId, html, markup, cancellationToken);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "Не удалось изменить сообщение {MessageId} в чате {ChatId}", messageId, chatId);
            return (SendOutcome.Failed, null);
        }
    }

    /// <summary>Снимает inline-кнопки со старого экрана, чтобы в переписке не висели два живых меню. Best effort.</summary>
    public async Task ClearKeyboardAsync(ITelegramBotClient client, long chatId, int messageId, CancellationToken cancellationToken)
    {
        try
        {
            await client.EditMessageReplyMarkup(chatId, messageId, replyMarkup: null, cancellationToken: cancellationToken);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Клавиатуру сообщения {MessageId} в чате {ChatId} снять не удалось: {Message}", messageId, chatId, e.Message);
        }
    }

    public async Task ReactAsync(ITelegramBotClient client, long chatId, int messageId, string emoji, CancellationToken cancellationToken)
    {
        try
        {
            await client.SetMessageReaction(chatId, messageId, [new ReactionTypeEmoji { Emoji = emoji }],
                cancellationToken: cancellationToken);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            // Реакции могут быть выключены — это не повод считать пересылку неудачной.
            logger.LogDebug("Реакцию в чате {ChatId} поставить не удалось: {Message}", chatId, e.Message);
        }
    }

    public async Task AnswerCallbackAsync(ITelegramBotClient client, string callbackQueryId, string? text, CancellationToken cancellationToken)
    {
        try
        {
            await client.AnswerCallbackQuery(callbackQueryId, text, cancellationToken: cancellationToken);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            // Просроченный callback (после рестарта) Telegram отвергает — нажатие уже обработано.
            logger.LogDebug("AnswerCallbackQuery не удался: {Message}", e.Message);
        }
    }

    private static bool IsUnreachable(ApiRequestException e)
        => e.ErrorCode == 403
           || (e.ErrorCode == 400 && e.Message.Contains("chat not found", StringComparison.OrdinalIgnoreCase));
}
