using Ganss.Xss;

namespace Application;

public class HtmlSanitizerService
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitizerService()
    {
        _sanitizer = new HtmlSanitizer();

        _sanitizer.AllowedTags.Clear();
        foreach (var tag in new[]
        {
            "p", "br", "h1", "h2", "h3", "h4", "h5",
            "strong", "b", "em", "i", "u", "s", "del", "strike",
            "span", "a", "img", "blockquote", "ul", "ol", "li",
            "div", "pre", "code", "sub", "sup"
        })
        {
            _sanitizer.AllowedTags.Add(tag);
        }

        _sanitizer.AllowedAttributes.Clear();
        foreach (var attr in new[] { "class", "style", "href", "target", "rel", "src", "alt", "width", "height" })
        {
            _sanitizer.AllowedAttributes.Add(attr);
        }

        _sanitizer.AllowedCssProperties.Clear();
        foreach (var css in new[]
        {
            "color", "background-color", "text-align", "padding-left",
            "font-size", "font-weight", "font-style", "text-decoration"
        })
        {
            _sanitizer.AllowedCssProperties.Add(css);
        }

        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("mailto");
    }

    public string Sanitize(string html) => _sanitizer.Sanitize(html);
}
