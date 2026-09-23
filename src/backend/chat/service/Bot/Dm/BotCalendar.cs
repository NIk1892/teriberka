using System.Globalization;
using Telegram.Bot.Types.ReplyMarkups;

namespace Chat.Bot.Dm;

/// <summary>
/// Календарь шага даты: сетка месяца inline-кнопками. Нативного выбора даты у ботов
/// Telegram нет, а Mini App ради одного поля — перебор (отдельная страница, CSP,
/// проверка initData). Листание ◀ ▶ правит то же сообщение, прошедшие дни и хвосты
/// соседних месяцев — пустые кнопки без действия. Выбранная дата хранится в DateText
/// как ISO (yyyy-MM-dd) и показывается в языке пользователя; текст «начало мая»
/// по-прежнему принимается как есть.
///
/// «Сегодня» — по Москве: туры стартуют из Мурманска, и в 01:00 МСК уже новые сутки,
/// хотя по UTC ещё вчера.
/// </summary>
public static class BotCalendar
{
    /// <summary>Насколько вперёд можно листать: дальше года туры не планируют.</summary>
    public const int MonthsAhead = 12;

    private const string IsoFormat = "yyyy-MM-dd";

    private static readonly TimeSpan MoscowOffset = TimeSpan.FromHours(3);

    public static DateOnly Today(DateTime utcNow) => DateOnly.FromDateTime(utcNow + MoscowOffset);

    public static string MonthArg(DateOnly month) => month.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public static string DateArg(DateOnly date) => date.ToString(IsoFormat, CultureInfo.InvariantCulture);

    /// <summary>Месяц из callback'а «yyyy-MM», зажатый в окно [текущий, текущий + 12].</summary>
    public static DateOnly ParseMonth(string? argument, DateOnly today)
    {
        var first = FirstOfMonth(today);

        if (argument is null
            || !DateOnly.TryParseExact(argument + "-01", IsoFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
            return first;

        var last = first.AddMonths(MonthsAhead);
        return month < first ? first : month > last ? last : month;
    }

    /// <summary>День из callback'а: только сегодня и позже, не дальше окна листания.</summary>
    public static bool TryParseDay(string? argument, DateOnly today, out DateOnly date)
    {
        if (argument is null
            || !DateOnly.TryParseExact(argument, IsoFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            date = default;
            return false;
        }

        return date >= today && date < FirstOfMonth(today).AddMonths(MonthsAhead + 1);
    }

    /// <summary>DateText — дата из календаря (ISO)? Иначе это текст пользователя или «?».</summary>
    public static bool TryParseStored(string? dateText, out DateOnly date)
        => DateOnly.TryParseExact(dateText, IsoFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>«12 октября 2026 (пн)» / «Mon 12 October 2026» / «2026年10月12日 星期一».</summary>
    public static string Format(DateOnly date, string lang)
    {
        var culture = Culture(lang);

        return lang switch
        {
            "ru" => $"{date.ToString("d MMMM yyyy", culture)} ({date.ToString("ddd", culture)})",
            "zh" => $"{date.ToString("yyyy年M月d日", culture)} {date.ToString("dddd", culture)}",
            _ => date.ToString("ddd d MMMM yyyy", culture),
        };
    }

    public static InlineKeyboardMarkup Keyboard(string lang, DateOnly month, DateOnly today)
    {
        var first = FirstOfMonth(month);
        var rows = new List<InlineKeyboardButton[]>();

        // Шапка: ◀ Месяц год ▶. На краях окна стрелка — пустая кнопка без действия.
        var canPrev = first > FirstOfMonth(today);
        var canNext = first < FirstOfMonth(today).AddMonths(MonthsAhead);

        rows.Add(
        [
            canPrev ? Cb("◀️", Callbacks.CalendarMonth(MonthArg(first.AddMonths(-1)))) : Noop(" "),
            Noop(MonthTitle(first, lang)),
            canNext ? Cb("▶️", Callbacks.CalendarMonth(MonthArg(first.AddMonths(1)))) : Noop(" "),
        ]);

        rows.Add(WeekdayNames(lang).Select(Noop).ToArray());

        // Неделя с понедельника: сдвиг первого числа от понедельника.
        var offset = ((int)first.DayOfWeek + 6) % 7;
        var days = DateTime.DaysInMonth(first.Year, first.Month);
        var week = new List<InlineKeyboardButton>();

        for (var i = 0; i < offset; i++)
            week.Add(Noop(" "));

        for (var day = 1; day <= days; day++)
        {
            var date = new DateOnly(first.Year, first.Month, day);
            var label = day.ToString(CultureInfo.InvariantCulture);

            week.Add(date < today
                ? Noop("·")
                : Cb(date == today ? $"[{label}]" : label, Callbacks.PickDate(DateArg(date))));

            if (week.Count == 7)
            {
                // Неделя, целиком ушедшая в прошлое, — только строка точек; её не показываем.
                if (date >= today)
                    rows.Add(week.ToArray());

                week.Clear();
            }
        }

        if (week.Count > 0)
        {
            while (week.Count < 7)
                week.Add(Noop(" "));

            rows.Add(week.ToArray());
        }

        rows.Add([Cb($"{BotIcons.DateSkip} {BotStrings.Get("BotBtnDateSkip", lang)}", Callbacks.DateSkip)]);
        rows.Add(
        [
            Cb($"{BotIcons.Back} {BotStrings.Get("BotBtnBack", lang)}", Callbacks.Back),
            Cb($"{BotIcons.Cancel} {BotStrings.Get("BotBtnCancel", lang)}", Callbacks.Cancel),
        ]);

        return new InlineKeyboardMarkup(rows);
    }

    private static string MonthTitle(DateOnly month, string lang)
    {
        if (lang == "zh")
            return $"{month.Year}年{month.Month}月";

        var name = Culture(lang).DateTimeFormat.MonthNames[month.Month - 1];
        return $"{char.ToUpper(name[0], Culture(lang))}{name[1..]} {month.Year}";
    }

    /// <summary>
    /// Пн..Вс строкой в два знака: ShortestDayNames у ru даёт одну букву («П В С Ч П С В» —
    /// не отличить вторник от среды), а длинные сокращения не влезают в семь колонок телефона.
    /// </summary>
    private static string[] WeekdayNames(string lang) => lang switch
    {
        "ru" => ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"],
        "zh" => ["一", "二", "三", "四", "五", "六", "日"],
        _ => ["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"],
    };

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private static CultureInfo Culture(string lang) => lang switch
    {
        "ru" => CultureInfo.GetCultureInfo("ru-RU"),
        "zh" => CultureInfo.GetCultureInfo("zh-CN"),
        _ => CultureInfo.GetCultureInfo("en-GB"),
    };

    private static InlineKeyboardButton Cb(string text, string data) => InlineKeyboardButton.WithCallbackData(text, data);

    private static InlineKeyboardButton Noop(string text) => InlineKeyboardButton.WithCallbackData(text, Callbacks.Noop);
}
