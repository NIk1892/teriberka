using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace UI.Public.Web.Features.Manager;

/// <summary>
/// Вход на страницу заявок /manager: один общий пароль из MANAGER_PASSWORD (решение
/// владельца, 16.09.2026; Keycloak обсуждали и отложили — на 2 ГБ сервера он тяжёл,
/// а менеджеров двое-трое). Пустой пароль — раздела нет вовсе: вход отвечает 404.
///
/// В cookie лежит отпечаток пароля: сменили MANAGER_PASSWORD (например, ушёл менеджер) —
/// все выданные раньше сессии перестают действовать на следующем же запросе.
/// </summary>
public sealed class ManagerAuth
{
    public const string Scheme = "manager";
    public const string CookieName = "mgr_session";

    /// <summary>Cookie сессии живёт только под этим путём и на публичные страницы не ходит.</summary>
    public const string BasePath = "/manager";

    public const string LoginPath = "/manager/login";

    /// <summary>
    /// Куда уходит форма входа. Не LoginPath: страница Blazor сама принимает POST на свой
    /// адрес (так работают формы static SSR), и эндпоинт на том же пути давал
    /// AmbiguousMatchException.
    /// </summary>
    public const string SignInPath = "/manager/signin";

    private const string FingerprintClaim = "pwd";

    private readonly byte[]? _passwordHash;

    public ManagerAuth(IConfiguration configuration, ILogger<ManagerAuth> logger)
    {
        var password = configuration["MANAGER_PASSWORD"];
        if (string.IsNullOrEmpty(password))
        {
            logger.LogInformation("MANAGER_PASSWORD не задан — страница заявок /manager выключена");
            return;
        }

        // 8 случайных букв и цифр хватает (решение владельца, 16.09.2026): перебор сдерживает
        // лимит входа — 5 попыток за 15 минут с адреса. Опасен не короткий, а угадываемый пароль.
        if (password.Length < 8)
            logger.LogWarning("MANAGER_PASSWORD короче 8 символов — страница с телефонами клиентов открыта перебору");

        if (string.IsNullOrWhiteSpace(configuration["API_TOKEN"]))
            logger.LogWarning("API_TOKEN не задан — страница заявок не получит их у шлюза (он закрыт политикой Admin)");

        _passwordHash = Hash(password);
    }

    public bool Enabled => _passwordHash is not null;

    /// <summary>Сравнение за постоянное время: по скорости ответа пароль не подобрать.</summary>
    public bool Check(string? password)
        => _passwordHash is not null && password is not null
           && CryptographicOperations.FixedTimeEquals(Hash(password), _passwordHash);

    public ClaimsPrincipal CreatePrincipal()
        => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "manager"), new Claim(FingerprintClaim, Fingerprint)],
            Scheme));

    /// <summary>Сессия выдана под нынешний пароль (и раздел вообще включён).</summary>
    public bool IsCurrent(ClaimsPrincipal principal)
        => Enabled && principal.FindFirst(FingerprintClaim)?.Value == Fingerprint;

    private string Fingerprint => Convert.ToHexString(_passwordHash!)[..16];

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes("teriberka-manager:" + value));
}
