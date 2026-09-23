using System.Globalization;
using System.Net;
using System.Text;
using Applications.Contracts;
using Chat.Domain;

namespace Chat.Bot.Dm;

/// <summary>
/// Тексты экранов лички — HTML для parseMode Html. Всё, что ввёл пользователь, проходит
/// через Escape; тексты из resx — нет, поэтому в них не должно быть «&lt;» и «&amp;».
/// </summary>
public static class BotScreens
{
    public const int FaqCount = 6;
    public const int StopCount = 8;
    public const int IncludedCount = 6;

    /// <summary>Значение People для «больше 8» — не число людей, а сентинел.</summary>
    public const int PeopleMoreThanMax = BotOptions.MaxPeopleForEstimate + 1;

    /// <summary>Значение DateText для «пока не знаю».</summary>
    public const string DateUnknown = "?";

    /// <summary>Сколько шагов видит пользователь: маршрут, дата, люди, пожелания, имя, телефон.</summary>
    public const int StepCount = 6;

    public static string Welcome(string lang)
        => $"{BotIcons.Welcome} <b>{T("BotWelcomeTitle", lang)}</b>\n\n{T("BotWelcome", lang)}\n\n{T("BotMenuTitle", lang)}";

    public static string Menu(string lang) => T("BotMenuTitle", lang);

    public static string UnknownCommand(string lang) => $"{T("BotUnknownCommand", lang)}\n\n{T("BotMenuTitle", lang)}";

    public static string Program(string lang)
    {
        var text = new StringBuilder()
            .Append(BotIcons.Program).Append(" <b>").Append(T("ItineraryTitle", lang)).Append("</b>\n")
            .Append(T("ItinerarySubtitle", lang)).Append("\n\n");

        for (var i = 1; i <= StopCount; i++)
            text.Append(BotIcons.Stops[i - 1]).Append(" <b>").Append(T($"Itinerary{i}Title", lang)).Append("</b>\n")
                .Append(T($"Itinerary{i}Text", lang)).Append("\n\n");

        text.Append("<i>").Append(T("ItineraryNote", lang)).Append("</i>");

        return text.ToString();
    }

    public static string Price(string lang)
    {
        var text = new StringBuilder()
            .Append(BotIcons.Price).Append(" <b>").Append(T("BotPriceTitle", lang)).Append("</b>\n")
            .Append("<b>").Append(T("HeroPrice", lang)).Append("</b> · ").Append(T("HeroNote", lang)).Append("\n\n")
            .Append("<b>").Append(T("IncludedTitle", lang)).Append("</b> — ").Append(T("IncludedSubtitle", lang)).Append('\n');

        for (var i = 1; i <= IncludedCount; i++)
            text.Append(BotIcons.Included).Append(' ').Append(T($"Included{i}", lang)).Append('\n');

        text.Append('\n').Append("<i>").Append(Capitalize(T("Why3Text", lang))).Append("</i>");

        return text.ToString();
    }

    public static string Routes(string lang)
        => $"{BotIcons.Routes} <b>{T("MapTitle", lang)}</b>\n{T("MapSubtitle", lang)}\n\n{T("BotRoutesPick", lang)}";

    public static string RouteInfo(string lang, string code) => code switch
    {
        ApplicationRoutes.Teriberka =>
            $"{BotIcons.RouteTeriberka} <b>{T("Dir1Title", lang)}</b> · {T("MapDist1", lang)}\n{T("Dir1Text", lang)}\n\n"
            + $"<b>{T("HeroPrice", lang)}</b> · {T("HeroNote", lang)}\n{T("HeroKicker", lang)}",
        ApplicationRoutes.Lovozero =>
            $"{BotIcons.RouteLovozero} <b>{T("Dir2Title", lang)}</b> · {T("MapDist2", lang)}\n{T("Dir2Text", lang)}\n\n"
            + $"{T("HeroKicker", lang)} · {T("BotEstimateManager", lang)}",
        ApplicationRoutes.Tersky =>
            $"{BotIcons.RouteTersky} <b>{T("Dir3Title", lang)}</b> · {T("MapDist3", lang)}\n{T("Dir3Text", lang)}\n\n"
            + $"{T("HeroKicker", lang)} · {T("BotEstimateManager", lang)}",
        _ =>
            $"{BotIcons.RouteCustom} <b>{T("RouteCustomTitle", lang)}</b>\n{T("RouteCustomText", lang)}\n\n"
            + $"{T("BotEstimateManager", lang)}",
    };

    public static string Faq(string lang)
        => $"{BotIcons.Faq} <b>{T("PracticalTitle", lang)}</b>\n{T("BotFaqPick", lang)}";

    public static string FaqAnswer(string lang, int number)
        => $"{BotIcons.FaqItems[number - 1]} <b>{T($"Faq{number}Question", lang)}</b>\n\n{T($"Faq{number}Answer", lang)}";

    public static string Contacts(string lang, BotOptions options)
    {
        var text = new StringBuilder()
            .Append(BotIcons.Contacts).Append(" <b>").Append(T("BotContactsTitle", lang)).Append("</b>\n")
            .Append(T("FooterDaily", lang)).Append('\n');

        // Номер обычным текстом: Telegram сам делает его нажимаемым.
        if (options.ContactPhone is not null)
            text.Append('\n').Append(BotStrings.Format("BotContactsPhone", lang, Escape(options.ContactPhone)));

        if (options.ContactEmail is not null)
            text.Append('\n').Append(BotStrings.Format("BotContactsEmail", lang, Escape(options.ContactEmail)));

        text.Append("\n\n<b>").Append(T("BotContactsHowTitle", lang)).Append("</b>\n")
            .Append(BotStrings.Format("BotContactsHow", lang, T("MapDist1", lang), T("MapDist2", lang), T("MapDist3", lang)));

        return text.ToString();
    }

    public static string Step(string lang, int number, string key)
        => $"<b>{BotStrings.Format("BotStepOf", lang, number, StepCount)}</b>\n{T(key, lang)}";

    /// <summary>Оценка на шаге пожеланий — сразу после числа людей, без лишнего нажатия.</summary>
    public static string Estimate(string lang, string? route, int? people)
    {
        if (route == ApplicationRoutes.Teriberka && people is > 0 and <= BotOptions.MaxPeopleForEstimate)
            return $"{BotIcons.Estimate} {BotStrings.Format("BotEstimateTeriberka", lang, Money(people.Value * BotOptions.PriceFromPerPerson), people.Value)}";

        return $"{BotIcons.Estimate} {T("BotEstimateManager", lang)}";
    }

    public static string Summary(string lang, BotDialogEntity dialog, string? prefix = null)
    {
        var text = new StringBuilder();

        if (prefix is not null)
            text.Append(prefix).Append("\n\n");

        text.Append(BotIcons.Summary).Append(" <b>").Append(T("BotSummaryTitle", lang)).Append("</b>\n")
            .Append(BotStrings.Format("BotSummaryRoute", lang, BotKeyboards.RouteLabel(dialog.Route ?? string.Empty, lang))).Append('\n')
            .Append(BotStrings.Format("BotSummaryDate", lang, DateLabel(lang, dialog.DateText))).Append('\n')
            .Append(BotStrings.Format("BotSummaryPeople", lang, PeopleLabel(lang, dialog.People))).Append('\n')
            .Append(BotStrings.Format("BotSummaryWishes", lang, TextOrDash(lang, dialog.Wishes))).Append('\n')
            .Append(BotStrings.Format("BotSummaryName", lang, TextOrDash(lang, dialog.Name))).Append('\n')
            .Append(BotStrings.Format("BotSummaryPhone", lang, Escape(dialog.Phone)));

        if (dialog.Route == ApplicationRoutes.Teriberka && dialog.People is > 0 and <= BotOptions.MaxPeopleForEstimate)
            text.Append("\n\n").Append(Estimate(lang, dialog.Route, dialog.People));

        text.Append("\n\n<i>").Append(T("BotSummaryConsent", lang)).Append("</i>");

        return text.ToString();
    }

    public static string Sent(string lang)
        => $"{BotIcons.Sent} <b>{T("SentTitle", lang)}</b>\n{T("SentText", lang)}\n\n{T("FormCallbackNote", lang)}\n{T("FooterDaily", lang)}";

    public static string SubmitInvalid(string lang, string? title)
        => $"{BotIcons.Error} {BotStrings.Format("BotSubmitInvalid", lang, Escape(title ?? "—"))}";

    public static string SubmitUnavailable(string lang, BotOptions options)
        => $"{BotIcons.Error} {BotStrings.Format("BotSubmitUnavailable", lang, PhoneSuffix(options))}";

    public static string SubmitRateLimited(string lang) => $"{BotIcons.Error} {T("BotSubmitRateLimited", lang)}";

    public static string BookingDisabled(string lang, BotOptions options)
        => BotStrings.Format("BotBookingDisabled", lang, PhoneSuffix(options));

    public static string Cooldown(string lang, int minutesLeft) => BotStrings.Format("BotCooldown", lang, minutesLeft);

    public static string DailyCap(string lang) => T("BotDailyCap", lang);

    public static string DraftExists(string lang) => T("BotDraftExists", lang);

    public static string Cancelled(string lang) => $"{BotIcons.Cancelled} {T("BotCancelled", lang)}\n\n{T("BotMenuTitle", lang)}";

    public static string OnlyText(string lang) => T("BotOnlyText", lang);

    public static string ChatOnlyText(string lang) => T("BotChatOnlyText", lang);

    public static string UseButtons(string lang) => T("BotUseButtons", lang);

    public static string PeopleInvalid(string lang) => T("BotPeopleInvalid", lang);

    public static string PhoneAccepted(string lang) => $"{BotIcons.Submit} {T("BotPhoneAccepted", lang)}";

    public static string PhoneInvalid(string lang) => T("BotPhoneInvalid", lang);

    public static string PhoneNotOwn(string lang) => T("BotPhoneNotOwn", lang);

    public static string StepPassedToast(string lang) => T("BotStepPassed", lang);

    public static string StaleButtonToast(string lang) => T("BotStaleButton", lang);

    public static string FreeTextOffer(string lang) => T("BotFreeTextOffer", lang);

    public static string WriteIntro(string lang)
        => $"{BotIcons.Write} <b>{T("BotWriteTitle", lang)}</b>\n{T("BotWriteIntro", lang)}\n{T("FooterDaily", lang)}";

    public static string Forwarded(string lang) => $"{BotIcons.Forwarded} {T("BotForwarded", lang)}\n{T("FooterDaily", lang)}";

    public static string ForwardTooMany(string lang) => T("BotForwardTooMany", lang);

    public static string ForwardTooLong(string lang, int maxLength) => BotStrings.Format("BotForwardTooLong", lang, maxLength);

    public static string ForwardFailed(string lang) => T("BotForwardFailed", lang);

    public static string Error(string lang) => $"{BotIcons.Error} {T("BotError", lang)}";

    /// <summary>Детали для менеджера — по-русски: канал и /manager одноязычные.</summary>
    public static string Details(BotDialogEntity dialog)
    {
        var lines = new List<string>
        {
            $"Дата: {(dialog.DateText is null or DateUnknown ? "не определились" : dialog.DateText)}",
            $"Человек: {(dialog.People is PeopleMoreThanMax ? "больше 8" : dialog.People?.ToString(CultureInfo.InvariantCulture) ?? "—")}",
        };

        if (!string.IsNullOrWhiteSpace(dialog.Wishes))
            lines.Add($"Пожелания: {dialog.Wishes}");

        if (dialog.Route == ApplicationRoutes.Teriberka && dialog.People is > 0 and <= BotOptions.MaxPeopleForEstimate)
            lines.Add($"Оценка в боте: от {Money(dialog.People.Value * BotOptions.PriceFromPerPerson)} ₽");

        var details = string.Join('\n', lines);

        return details.Length <= ApplicationCreateCommand.MaxDetailsLength
            ? details
            : details[..ApplicationCreateCommand.MaxDetailsLength];
    }

    /// <summary>«96 000» — как HeroPrice на сайте, с обычным пробелом между разрядами.</summary>
    public static string Money(int amount)
        => amount.ToString("N0", CultureInfo.InvariantCulture).Replace(",", " ");

    public static string Escape(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string DateLabel(string lang, string? dateText)
        => dateText is null or DateUnknown ? T("BotSummaryDateUnknown", lang) : Escape(dateText);

    private static string PeopleLabel(string lang, int? people) => people switch
    {
        null => "—",
        PeopleMoreThanMax => T("BotSummaryPeopleMore", lang),
        _ => people.Value.ToString(CultureInfo.InvariantCulture),
    };

    private static string TextOrDash(string lang, string? value)
        => string.IsNullOrWhiteSpace(value) ? T("BotSummaryNone", lang) : Escape(value);

    private static string PhoneSuffix(BotOptions options)
        => options.ContactPhone is null ? string.Empty : $": {Escape(options.ContactPhone)}";

    private static string Capitalize(string value)
        => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static string T(string key, string lang) => BotStrings.Get(key, lang);
}
