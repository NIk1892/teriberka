namespace UI.Public.Web.Features.Places;

/// <summary>
/// Ссылка на текст лицензии по её короткому имени из PhotoCredit («CC BY-SA 4.0», «CC0»).
/// Creative Commons требует указывать лицензию со ссылкой — подпись без неё неполная.
/// </summary>
public static class PhotoLicenses
{
    public static string Url(string license)
    {
        var parts = license.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1 && parts[0].Equals("CC0", StringComparison.OrdinalIgnoreCase))
            return "https://creativecommons.org/publicdomain/zero/1.0/";
        if (parts.Length == 3 && parts[0].Equals("CC", StringComparison.OrdinalIgnoreCase))
            return $"https://creativecommons.org/licenses/{parts[1].ToLowerInvariant()}/{parts[2]}/";
        return "https://creativecommons.org/licenses/";
    }
}
