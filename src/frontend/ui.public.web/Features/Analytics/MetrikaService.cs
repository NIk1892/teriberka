using System.Globalization;

namespace UI.Public.Web.Features.Analytics;

/// <summary>
/// Яндекс.Метрика. Включается одним ключом YANDEX_METRIKA_ID — как TG_BOT_URL и
/// MAX_URL: пустое значение выключает счётчик целиком, то есть ни скрипта на
/// странице, ни хостов Метрики в CSP. Номер счётчика публичный (он и так виден в
/// исходнике страницы), поэтому секретом не считается и живёт прямо в compose.
///
/// Значение проверяется на число: опечатка в ключе иначе ушла бы в разметку и
/// счётчик молча не работал бы — вместо этого warning в логе и выключенная
/// Метрика (тот же принцип, что у половинной конфигурации SmartCaptcha).
/// </summary>
public sealed class MetrikaService
{
    public MetrikaService(IConfiguration configuration, ILogger<MetrikaService> logger)
    {
        var raw = configuration["YANDEX_METRIKA_ID"]?.Trim();
        if (string.IsNullOrEmpty(raw))
            return;

        if (long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0)
            CounterId = id.ToString(CultureInfo.InvariantCulture);
        else
            logger.LogWarning(
                "YANDEX_METRIKA_ID = {Value} — это не номер счётчика; Метрика ВЫКЛЮЧЕНА",
                raw);
    }

    /// <summary>Номер счётчика для init и для noscript-пикселя; null — Метрика выключена.</summary>
    public string? CounterId { get; }

    public bool Enabled => CounterId is not null;
}
