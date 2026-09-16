namespace Chat.Bot;

/// <summary>
/// Тексты Telegram-бота на трёх языках сайта. Язык выбирается по
/// <c>Message.From.LanguageCode</c> (IETF-тег из настроек клиента Telegram):
/// ru → русский, zh* → китайский, всё остальное (включая отсутствие тега) —
/// английский. Ключей мало, поэтому обычный switch вместо resx-инфраструктуры.
///
/// Служебные сообщения в группу гидов — исключение: там язык берётся из
/// <c>TG_ADMIN_LANG</c>, группа одноязычная и не должна менять язык от того,
/// кто из гидов написал последним.
/// </summary>
public static class BotTexts
{
    /// <summary>
    /// Приветствие в личке. Переписываться здесь бот пока не умеет (бронирование через бота —
    /// следующий этап), поэтому зовёт в чат на сайте: там отвечает менеджер.
    /// </summary>
    public static string Greeting(string? languageCode) => Lang(languageCode) switch
    {
        "ru" => "Здравствуйте! Я бот «ТериберкаКрай» — туры в Териберку, Ловозерские тундры и на Терский берег.\n\n"
                + "Задайте вопрос в чате на сайте — менеджер ответит там же. Скоро записаться на тур можно будет прямо здесь.",
        "zh" => "您好！我是「捷里别尔卡之境」的机器人——捷里别尔卡、洛沃泽罗苔原和捷尔斯基海岸之旅。\n\n"
                + "请在网站聊天中提问，客服经理会在那里回复。不久后即可直接在这里报名参加旅行。",
        _ => "Hello! I'm the TeriberkaKray bot — tours to Teriberka, the Lovozero tundras and the Tersky coast.\n\n"
             + "Ask your question in the chat on our website — a manager will answer right there. Soon you'll be able to book a tour right here.",
    };

    public static string OpenSiteButton(string? languageCode) => Lang(languageCode) switch
    {
        "ru" => "Открыть чат на сайте",
        "zh" => "打开网站聊天",
        _ => "Open the website chat",
    };

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

    private static string Lang(string? languageCode)
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
