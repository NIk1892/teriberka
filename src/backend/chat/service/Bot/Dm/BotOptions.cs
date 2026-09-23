namespace Chat.Bot.Dm;

/// <summary>
/// Настройки лички бота из конфигурации. Singleton, читается один раз при старте —
/// тот же плоский стиль ключей, что у остальных сервисов.
/// </summary>
public sealed class BotOptions
{
    /// <summary>
    /// Та же цифра, что HeroPrice на сайте («от 12 000 ₽ / человек»): бот считает
    /// «ориентировочно от N × 12 000 ₽». Меняется вместе с resx сайта и бота.
    /// </summary>
    public const int PriceFromPerPerson = 12_000;

    /// <summary>Группам больше этого оценку не называем — «стоимость рассчитает менеджер».</summary>
    public const int MaxPeopleForEstimate = 8;

    public BotOptions(IConfiguration configuration, ILogger<BotOptions> logger)
    {
        SiteUrl = (configuration["SITE_URL"] ?? string.Empty).Trim().TrimEnd('/');
        ContactPhone = Clean(configuration["CONTACT_PHONE"]);
        ContactEmail = Clean(configuration["CONTACT_EMAIL"]);
        MaxUrl = Clean(configuration["MAX_URL"]);
        ApiUrl = Clean(configuration["API_URL"]);
        ApiToken = Clean(configuration["API_TOKEN"]);

        if (!BookingEnabled)
            logger.LogWarning(
                "API_URL или API_TOKEN не заданы — запись на тур через бота выключена: " +
                "бот предложит сайт и телефон, остальное работает");
    }

    public string SiteUrl { get; }

    public string? ContactPhone { get; }

    public string? ContactEmail { get; }

    public string? MaxUrl { get; }

    /// <summary>Адрес шлюза; заявка уходит POST /api/private/application/create.</summary>
    public string? ApiUrl { get; }

    /// <summary>Служебный JWT для шлюза — тот же, что у страницы /manager.</summary>
    public string? ApiToken { get; }

    public bool BookingEnabled => ApiUrl is not null && ApiToken is not null;

    public string PrivacyUrl => $"{SiteUrl}/privacy";

    public string ApplyUrl => $"{SiteUrl}/#apply";

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
