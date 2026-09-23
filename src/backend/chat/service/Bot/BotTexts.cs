namespace Chat.Bot;

/// <summary>
/// Служебные тексты бота для группы гидов. Язык — из <c>TG_ADMIN_LANG</c>: группа
/// одноязычная и не должна менять язык от того, кто из гидов написал последним.
/// Тексты лички живут в resx (см. Dm/BotStrings) — их много и они повторяют сайт.
/// </summary>
public static class BotTexts
{
    /// <summary>
    /// «Шапка» диалога в группе: публикуется один раз, все сообщения посетителя вешаются
    /// на неё как reply. Гид отвечает reply на любое из них — так бот понимает, куда писать.
    /// </summary>
    public static string SessionHeader(string? adminLang, string shortId, string? culture, string? page) =>
        Lang(adminLang) switch
        {
            "zh" => $"💬 网站新对话 · #{shortId}\n"
                    + $"语言：{culture ?? "—"} · 页面：{page ?? "—"}\n"
                    + "请回复（reply）消息，访客将在网站上看到您的回答。",
            "en" => $"💬 New chat from the website · #{shortId}\n"
                    + $"Language: {culture ?? "—"} · page: {page ?? "—"}\n"
                    + "Reply to a message and the visitor will see your answer on the site.",
            _ => $"💬 Новый чат с сайта · #{shortId}\n"
                 + $"Язык: {culture ?? "—"} · страница: {page ?? "—"}\n"
                 + "Отвечайте reply на сообщение — ответ увидит посетитель.",
        };

    /// <summary>Шапка диалога из лички бота: ответ гида уйдёт посетителю в Telegram, а не на сайт.</summary>
    public static string SessionHeaderTelegram(string? adminLang, string shortId, string? lang, string? username)
    {
        var who = username is { Length: > 0 } ? $"@{username}" : "—";

        return Lang(adminLang) switch
        {
            "zh" => $"✈️ Telegram 新对话 · #{shortId}\n"
                    + $"语言：{lang ?? "—"} · 用户：{who}\n"
                    + "请回复（reply）消息，访客将在 Telegram 私聊中收到您的回答。",
            "en" => $"✈️ New chat from Telegram · #{shortId}\n"
                    + $"Language: {lang ?? "—"} · user: {who}\n"
                    + "Reply to a message and the visitor will get your answer in their Telegram chat.",
            _ => $"✈️ Новый чат из Telegram · #{shortId}\n"
                 + $"Язык: {lang ?? "—"} · пользователь: {who}\n"
                 + "Отвечайте reply на сообщение — ответ придёт посетителю в личку Telegram.",
        };
    }

    /// <summary>Гид написал в группу, но не ответом на сообщение — бот такое сопоставить не может.</summary>
    public static string ReplyHint(string? adminLang) => Lang(adminLang) switch
    {
        "zh" => "若要回复访客，请对该对话中的消息使用「回复」功能。",
        "en" => "To answer a visitor, use Reply on a message from their conversation.",
        _ => "Чтобы ответить посетителю, ответьте (reply) на сообщение из его диалога.",
    };

    public static string SessionNotFound(string? adminLang) => Lang(adminLang) switch
    {
        "zh" => "未找到该消息对应的对话——记录可能已按保存期限删除。",
        "en" => "Couldn't find a conversation for that message — it may already be deleted by the retention rule.",
        _ => "Не нашёл диалог для этого сообщения — возможно, переписка уже удалена по сроку хранения.",
    };

    public static string UnsupportedContent(string? adminLang) => Lang(adminLang) switch
    {
        "zh" => "目前只能向访客转发文本消息。",
        "en" => "For now I can only pass plain text on to the visitor.",
        _ => "Пока умею передавать посетителю только текст.",
    };

    /// <summary>Ответ длиннее лимита чата: не отправлен, иначе менеджер думал бы, что посетитель его видит.</summary>
    public static string ReplyTooLong(string? adminLang, int maxLength) => Lang(adminLang) switch
    {
        "zh" => $"未发送：每条回复最多 {maxLength} 个字符。请拆分为几条消息，每条都使用「回复」。",
        "en" => $"Not sent: a reply to the visitor can be at most {maxLength} characters. Split it into several messages, each as a reply.",
        _ => $"Не отправлено: за раз посетителю можно передать не больше {maxLength} символов. Разбейте ответ на несколько сообщений, каждое — reply.",
    };

    /// <summary>Ответ не сохранился (сбой базы или валидации) — повторить может только сам менеджер.</summary>
    public static string ReplyNotSaved(string? adminLang) => Lang(adminLang) switch
    {
        "zh" => "此回复未能送达访客——请再次使用「回复」发送。",
        "en" => "This reply didn't reach the visitor — please send it again as a reply.",
        _ => "Ответ не дошёл до посетителя — отправьте его ещё раз (reply).",
    };

    /// <summary>Диалог из лички: посетитель заблокировал бота — писать ему больше нельзя.</summary>
    public static string VisitorBlockedBot(string? adminLang) => Lang(adminLang) switch
    {
        "zh" => "回复已保存，但无法送达：访客已屏蔽机器人。",
        "en" => "Saved, but not delivered: the visitor has blocked the bot.",
        _ => "Ответ сохранён, но не доставлен: посетитель заблокировал бота.",
    };

    /// <summary>Диалог из лички: Telegram не принял отправку — повторить может только менеджер.</summary>
    public static string ReplyNotDeliveredToTelegram(string? adminLang) => Lang(adminLang) switch
    {
        "zh" => "回复已保存，但未能送达 Telegram 私聊——请稍后再次使用「回复」发送。",
        "en" => "Saved, but not delivered to the visitor's Telegram chat — please send it again as a reply in a minute.",
        _ => "Ответ сохранён, но в личку Telegram не доставлен — отправьте его ещё раз (reply) через минуту.",
    };

    /// <summary>ru / zh / иначе en — тот же выбор, что у сайта: неизвестный язык клиента получает английский.</summary>
    internal static string Lang(string? languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
        {
            return "en";
        }

        if (languageCode.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
        {
            return "ru";
        }

        return languageCode.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh" : "en";
    }
}
