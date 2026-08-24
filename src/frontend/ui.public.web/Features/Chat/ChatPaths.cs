namespace UI.Public.Web.Features.Chat;

/// <summary>
/// Куда вернуть посетителя после отправки сообщения без JavaScript. Панель чата
/// раскрыта, когда в адресе есть ?chat=open — на static SSR это единственный
/// способ показать её после полной перезагрузки страницы.
/// </summary>
public static class ChatPaths
{
    public const string Parameter = "chat";
    public const string Open = "open";
    public const string Error = "error";

    public static string Clean(string? path) => WithState(path, state: null);

    public static string WithOpen(string? path) => WithState(path, Open);

    public static string WithError(string? path) => WithState(path, Error);

    private static string WithState(string? path, string? state)
    {
        // Голый "//" формально локален для LocalRedirect, но браузер читает его как
        // ссылку на чужой домен — отсекаем явно.
        var local = path is ['/', ..] && path.Length <= 256 && !path.StartsWith("//") ? path : "/";

        var split = local.Split('?', 2);
        var query = split.Length == 2
            ? string.Join('&', split[1]
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(pair => !pair.StartsWith($"{Parameter}=", StringComparison.OrdinalIgnoreCase)))
            : string.Empty;

        if (state is not null)
            query = string.IsNullOrEmpty(query) ? $"{Parameter}={state}" : $"{query}&{Parameter}={state}";

        return string.IsNullOrEmpty(query) ? split[0] : $"{split[0]}?{query}";
    }
}
