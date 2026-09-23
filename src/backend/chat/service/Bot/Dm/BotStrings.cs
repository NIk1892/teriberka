using System.Globalization;
using System.Resources;

namespace Chat.Bot.Dm;

/// <summary>
/// Тексты лички бота — Resources/BotStrings.resx (русский, нейтральный) + .en + .zh.
/// Ключи контента совпадают с SharedResource*.resx сайта (Itinerary1Title, Faq3Answer,
/// Included2…): поменялся текст на сайте — скопировать значение по тому же ключу сюда.
/// Собственные ключи бота — с префиксом Bot. Неизвестный язык клиента → английский,
/// как в BotTexts; ключ, забытый в .en/.zh, откатывается на русский, а не падает.
/// </summary>
public static class BotStrings
{
    public static readonly string[] Languages = ["ru", "en", "zh"];

    private static readonly ResourceManager Resources =
        new("Chat.Bot.Resources.BotStrings", typeof(BotStrings).Assembly);

    public static string Lang(string? languageCode) => BotTexts.Lang(languageCode);

    public static string Get(string key, string lang)
    {
        var culture = lang == "ru" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(lang);

        return Resources.GetString(key, culture)
               ?? Resources.GetString(key, CultureInfo.InvariantCulture)
               ?? key;
    }

    public static string Format(string key, string lang, params object?[] args)
        => string.Format(CultureInfo.InvariantCulture, Get(key, lang), args);

    /// <summary>
    /// Текст совпадает с подписью кнопки на любом из языков: reply-кнопки («Назад»,
    /// «Отмена») приходят обычным текстом, и язык клиента мог смениться между экранами.
    /// </summary>
    public static bool IsLabel(string key, string icon, string text)
    {
        var value = text.Trim();

        return Languages.Any(lang =>
        {
            var label = Get(key, lang);
            return value.Equals(label, StringComparison.Ordinal)
                   || value.Equals($"{icon} {label}", StringComparison.Ordinal);
        });
    }
}
