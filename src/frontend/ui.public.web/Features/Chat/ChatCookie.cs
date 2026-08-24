using Microsoft.AspNetCore.Http;

namespace UI.Public.Web.Features.Chat;

/// <summary>
/// Cookie с токеном диалога. Единственный ключ к переписке: авторизации на сайте
/// нет, поэтому кто владеет токеном — тот и видит свои сообщения.
/// HttpOnly: скрипту она не нужна (запросы идут на свой origin и cookie уходит
/// сама), зато так её не достать через XSS.
/// </summary>
public static class ChatCookie
{
    public const string Name = "chat_sid";

    /// <summary>Совпадает со сроком хранения переписки в chat-сервисе (CHAT_RETENTION_DAYS).</summary>
    private const int Days = 90;

    public static CookieOptions Options(HttpContext context) => new()
    {
        Expires = DateTimeOffset.UtcNow.AddDays(Days),
        // строго необходимая: без неё чат не работает вовсе — как culture и theme
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        Path = "/"
    };
}
