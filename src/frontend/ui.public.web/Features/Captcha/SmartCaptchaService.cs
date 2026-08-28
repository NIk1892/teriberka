using System.Text.Json;

namespace UI.Public.Web.Features.Captcha;

/// <summary>
/// Невидимая Яндекс SmartCaptcha на форме заявки. Включается ПАРОЙ ключей
/// (SMARTCAPTCHA_CLIENT_KEY + SMARTCAPTCHA_SERVER_KEY) — как TG_BOT_URL:
/// пустые ключи выключают капчу целиком, и виджет, и проверку. Половинная
/// конфигурация считается выключенной и подсвечивается warning'ом в логе,
/// чтобы «капча вроде есть, а проверки нет» не осталась незамеченной.
/// </summary>
public sealed class SmartCaptchaService
{
    // Хост валидации отличается от хоста виджета (smartcaptcha.yandexcloud.net) —
    // так в документации: docs/smartcaptcha/operations/validate-captcha.
    private const string ValidateUrl = "https://smartcaptcha.cloud.yandex.ru/validate";

    // Свой клиент, а не общий из DI: тот сконфигурирован на шлюз (BaseAddress,
    // авторизационный handler). Таймаут короткий — посетитель ждёт отправку формы.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };

    private readonly ILogger<SmartCaptchaService> _logger;

    public SmartCaptchaService(IConfiguration configuration, ILogger<SmartCaptchaService> logger)
    {
        _logger = logger;
        ClientKey = configuration["SMARTCAPTCHA_CLIENT_KEY"];
        ServerKey = configuration["SMARTCAPTCHA_SERVER_KEY"];

        var hasClient = !string.IsNullOrWhiteSpace(ClientKey);
        var hasServer = !string.IsNullOrWhiteSpace(ServerKey);
        Enabled = hasClient && hasServer;

        if (hasClient != hasServer)
            _logger.LogWarning(
                "SmartCaptcha настроена наполовину (client: {HasClient}, server: {HasServer}) — капча ВЫКЛЮЧЕНА; задайте оба ключа",
                hasClient, hasServer);
    }

    /// <summary>Публичный ключ для виджета (data-sitekey в разметке).</summary>
    public string? ClientKey { get; }

    private string? ServerKey { get; }

    public bool Enabled { get; }

    /// <summary>
    /// Проверка токена в API SmartCaptcha. Отсутствующий или пустой токен — отказ
    /// (без JavaScript токен не собрать — капча сознательно требует включённый JS).
    /// Недоступность самого сервиса капчи — пропуск с warning'ом (fail-open):
    /// сбой Яндекса не должен терять заявки, от спама остаётся rate limiting.
    /// </summary>
    public async Task<bool> ValidateAsync(string? token, string? ip)
    {
        if (!Enabled)
            return true;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var fields = new Dictionary<string, string>
            {
                ["secret"] = ServerKey!,
                ["token"] = token,
            };
            if (!string.IsNullOrWhiteSpace(ip))
                fields["ip"] = ip;

            using var content = new FormUrlEncodedContent(fields);
            using var response = await Http.PostAsync(ValidateUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                // 4xx/5xx от validate — считаем сбоем сервиса, не виной посетителя
                _logger.LogWarning("SmartCaptcha validate ответил {Status} — пропускаем без проверки", (int)response.StatusCode);
                return true;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);
            var status = json.RootElement.TryGetProperty("status", out var value) ? value.GetString() : null;
            return string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SmartCaptcha недоступна — пропускаем заявку без проверки");
            return true;
        }
    }
}
