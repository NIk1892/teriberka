using System.Net;
using Microsoft.Extensions.Logging;

namespace Api;

/// <summary>
/// HttpClient для исходящих вызовов через прокси из конфига (TG_PROXY_URL). Нужен Telegram'у:
/// с боевого сервера в России Bot API напрямую не отвечает, вызовы идут socks5-туннелем
/// на зарубежный сервер (docs/deploy.md, «Telegram через туннель»). Пустой адрес — прямое
/// соединение, как при локальной разработке.
/// </summary>
public static class ProxyHttpClient
{
    public static HttpClient Create(string? proxyUrl, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl))
            return new HttpClient();

        if (!Uri.TryCreate(proxyUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("socks5" or "socks4" or "socks4a" or "http" or "https"))
        {
            // Сервис не роняем: он нужен и без Telegram. Сам адрес в лог не пишем —
            // в нём может быть пароль прокси.
            logger.LogError("TG_PROXY_URL не распознан (ожидается socks5://host:port) — вызовы Telegram пойдут напрямую");
            return new HttpClient();
        }

        logger.LogInformation("Вызовы Telegram идут через прокси {ProxyHost}:{ProxyPort}", uri.Host, uri.Port);

        return new HttpClient(new SocketsHttpHandler { Proxy = new WebProxy(uri), UseProxy = true });
    }
}
