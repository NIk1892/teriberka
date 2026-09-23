namespace Chat.Bot.Dm;

/// <summary>
/// Данные inline-кнопок: «пространство:действие[:аргумент]», все короче 64 байт (лимит
/// Telegram). Пространства: m — меню, i — инфо-экраны, b — мастер заявки, w — вопрос
/// менеджеру. Данные приходят от клиента, поэтому разбираются, а не доверяются.
/// </summary>
public static class Callbacks
{
    public const string Menu = "m:menu";
    public const string Book = "m:book";
    public const string Program = "m:prog";
    public const string Price = "m:price";
    public const string Routes = "m:routes";
    public const string Faq = "m:faq";
    public const string Contacts = "m:contacts";
    public const string Write = "m:write";

    public static string RouteInfo(string code) => $"i:route:{code}";
    public static string FaqInfo(int number) => $"i:faq:{number}";

    public static string BookRoute(string code) => $"b:route:{code}";
    public const string DateSkip = "b:date:skip";

    /// <summary>Выбор дня в календаре: b:day:yyyy-MM-dd.</summary>
    public static string PickDate(string isoDate) => $"b:day:{isoDate}";

    /// <summary>Листание календаря: b:cal:yyyy-MM.</summary>
    public static string CalendarMonth(string yearMonth) => $"b:cal:{yearMonth}";

    /// <summary>Кнопка без действия: шапка календаря, дни недели, пустые клетки.</summary>
    public const string Noop = "b:noop";
    public static string People(int count) => $"b:people:{count}";
    public const string WishesSkip = "b:wishes:skip";
    public const string NameFromTelegram = "b:name:tg";
    public const string Submit = "b:submit";
    public const string Retry = "b:retry";
    public const string Resume = "b:resume";
    public const string Restart = "b:restart";
    public const string Back = "b:back";
    public const string Cancel = "b:cancel";
    public static string Edit(string field) => $"b:edit:{field}";

    public const string WriteSend = "w:send";
    public const string WriteDrop = "w:drop";

    /// <summary>Поля сводки, которые можно поправить кнопкой «✏️».</summary>
    public static class Fields
    {
        public const string Route = "route";
        public const string Date = "date";
        public const string People = "people";
        public const string Wishes = "wishes";
        public const string Name = "name";
        public const string Phone = "phone";
    }

    public static bool TryParse(string? data, out string ns, out string action, out string? argument)
    {
        ns = action = string.Empty;
        argument = null;

        if (string.IsNullOrEmpty(data) || data.Length > 64)
            return false;

        var parts = data.Split(':', 3);
        if (parts.Length < 2 || parts[0].Length == 0 || parts[1].Length == 0)
            return false;

        ns = parts[0];
        action = parts[1];
        argument = parts.Length == 3 ? parts[2] : null;
        return true;
    }
}
