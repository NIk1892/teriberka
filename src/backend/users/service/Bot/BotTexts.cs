namespace Users.Bot;

/// <summary>
/// Тексты Telegram-бота на трёх языках сайта. Язык выбирается по
/// <c>Message.From.LanguageCode</c> (IETF-тег из настроек клиента Telegram):
/// ru → русский, zh* → китайский, всё остальное (включая отсутствие тега) —
/// английский. Ключей мало, поэтому обычный switch вместо resx-инфраструктуры.
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
