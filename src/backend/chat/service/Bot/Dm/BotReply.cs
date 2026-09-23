using Applications.Contracts;
using Telegram.Bot.Types.ReplyMarkups;

namespace Chat.Bot.Dm;

/// <summary>Что машина просит сделать в ответ. Раннер исполняет по порядку.</summary>
public abstract record BotReply;

/// <summary>
/// Экран: HTML и inline-кнопки. EditInPlace — править сообщение, на кнопке которого
/// нажали (экран по callback), иначе новое сообщение (после текста или команды).
/// </summary>
public sealed record ScreenReply(string Html, InlineKeyboardMarkup? Keyboard, bool EditInPlace) : BotReply;

/// <summary>Новое сообщение с reply-клавиатурой (шаг телефона): к EditMessageText её не приделать.</summary>
public sealed record KeyboardReply(string Html, ReplyKeyboardMarkup Keyboard) : BotReply;

/// <summary>Короткое сообщение, снимающее reply-клавиатуру.</summary>
public sealed record AckReply(string Html) : BotReply;

/// <summary>Всплывашка на нажатие кнопки (answerCallbackQuery); без callback уйдёт обычным сообщением.</summary>
public sealed record ToastReply(string Text) : BotReply;

/// <summary>Реакция на сообщение пользователя.</summary>
public sealed record ReactReply(string Emoji) : BotReply;

/// <summary>Эффект: отправить заявку через шлюз; результат вернётся машине как SubmitResultInput.</summary>
public sealed record SubmitEffect(ApplicationCreateCommand Command) : BotReply;

/// <summary>Эффект: передать текст менеджеру; Announce — ответить текстом (первое сообщение), а не только реакцией.</summary>
public sealed record ForwardEffect(string Text, bool Announce) : BotReply;
