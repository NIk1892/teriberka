using System.Text;
using UI.Public.Web.Features.Places;

namespace UI.Public.Web.Features.Seo;

/// <summary>
/// Абсолютные URL для SEO (canonical, og:url, sitemap) и тексты robots.txt / sitemap.xml.
/// Базовый адрес сайта задаёт ключ SITE_URL; когда он пуст, абсолютные ссылки не строятся:
/// canonical и og:url/og:image не рендерятся, sitemap отдаёт 404, robots — без строки Sitemap.
/// </summary>
public sealed class SeoUrls
{
    private readonly string? _siteUrl; // без завершающего "/"
    private readonly Lazy<string> _robotsTxt;
    private readonly Lazy<string> _sitemapXml;

    public SeoUrls(IConfiguration configuration)
    {
        var raw = configuration["SITE_URL"];
        _siteUrl = string.IsNullOrWhiteSpace(raw) ? null : raw.TrimEnd('/');
        _robotsTxt = new(BuildRobots);
        _sitemapXml = new(BuildSitemap);
    }

    public bool HasSiteUrl => _siteUrl is not null;

    /// <summary>Абсолютный URL для пути вида "/place/umba"; null, если SITE_URL не задан.</summary>
    public string? Absolute(string path) => _siteUrl is null ? null : _siteUrl + path;

    public string RobotsTxt => _robotsTxt.Value;

    public string SitemapXml => _sitemapXml.Value;

    // Все индексируемые страницы сайта. Новое место в PlaceCatalog попадает
    // в sitemap автоматически — отдельно ничего дописывать не нужно.
    private static IEnumerable<string> PublicPaths()
    {
        yield return "/";

        foreach (var place in PlaceCatalog.All)
            yield return $"/place/{place.Slug}";

        yield return "/privacy";
        yield return "/photo-credits";
    }

    private string BuildRobots()
    {
        // Служебные endpoints и страницу 404 не сканируем; Sitemap — только абсолютным URL.
        var sb = new StringBuilder()
            .AppendLine("User-agent: *")
            .AppendLine("Disallow: /set-culture")
            .AppendLine("Disallow: /set-theme")
            .AppendLine("Disallow: /not-found")
            .AppendLine("Disallow: /chat");

        if (_siteUrl is not null)
            sb.AppendLine().AppendLine($"Sitemap: {_siteUrl}/sitemap.xml");

        return sb.ToString();
    }

    private string BuildSitemap()
    {
        var sb = new StringBuilder()
            .AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""")
            .AppendLine("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""");

        foreach (var path in PublicPaths())
            sb.AppendLine($"  <url><loc>{_siteUrl}{path}</loc></url>");

        return sb.AppendLine("</urlset>").ToString();
    }
}
