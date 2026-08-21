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
    public static string Greeting(string? languageCode) => Lang(languageCode) switch
    {
        "ru" => "Привет! Я бот «Кольского Севера» — туры в Териберку, Ловозерские тундры и на Терский берег.\n\n"
                + "Скоро здесь можно будет оставить заявку прямо в чате, а пока вся информация и форма записи — на сайте.",
        "zh" => "你好！我是「科拉之北」的机器人——捷里别尔卡、洛沃泽罗苔原和捷尔斯基海岸之旅。\n\n"
                + "不久后即可直接在聊天中报名，目前请在网站上查看行程并填写申请表。",
        _ => "Hi! I'm the Kola North bot — tours to Teriberka, the Lovozero tundras and the Tersky coast.\n\n"
             + "Soon you'll be able to book right here in the chat; for now all the details and the booking form are on the website.",
    };

    public static string OpenSiteButton(string? languageCode) => Lang(languageCode) switch
    {
        "ru" => "Открыть сайт",
        "zh" => "打开网站",
        _ => "Open the website",
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
