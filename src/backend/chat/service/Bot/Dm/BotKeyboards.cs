using Applications.Contracts;
using Telegram.Bot.Types.ReplyMarkups;

namespace Chat.Bot.Dm;

/// <summary>Клавиатуры лички. Подписи — из BotStrings, иконки — из BotIcons.</summary>
public static class BotKeyboards
{
    public static string RouteLabel(string code, string lang) => code switch
    {
        ApplicationRoutes.Teriberka => $"{BotIcons.RouteTeriberka} {T("MapSpot1", lang)}",
        ApplicationRoutes.Lovozero => $"{BotIcons.RouteLovozero} {T("MapSpot2", lang)}",
        ApplicationRoutes.Tersky => $"{BotIcons.RouteTersky} {T("MapSpot3", lang)}",
        _ => $"{BotIcons.RouteCustom} {T("RouteCustomTitle", lang)}",
    };

    public static string BackLabel(string lang) => $"{BotIcons.Back} {T("BotBtnBack", lang)}";

    public static string CancelLabel(string lang) => $"{BotIcons.Cancel} {T("BotBtnCancel", lang)}";

    public static InlineKeyboardMarkup Menu(string lang, BotOptions options) => Rows(
        [Cb(BotIcons.Book, "BotMenuBook", lang, Callbacks.Book)],
        [Cb(BotIcons.Program, "BotMenuProgram", lang, Callbacks.Program), Cb(BotIcons.Price, "BotMenuPrice", lang, Callbacks.Price)],
        [Cb(BotIcons.Routes, "BotMenuRoutes", lang, Callbacks.Routes), Cb(BotIcons.Faq, "BotMenuFaq", lang, Callbacks.Faq)],
        [Cb(BotIcons.Contacts, "BotMenuContacts", lang, Callbacks.Contacts), Cb(BotIcons.Write, "BotMenuWrite", lang, Callbacks.Write)],
        [Url(BotIcons.Site, "BotMenuSite", lang, options.SiteUrl)]);

    public static InlineKeyboardMarkup MenuOnly(string lang) => Rows([MenuButton(lang)]);

    /// <summary>Хвост любого инфо-экрана: записаться или вернуться в меню.</summary>
    public static InlineKeyboardMarkup InfoFooter(string lang) => Rows(FooterRow(lang));

    public static InlineKeyboardMarkup RouteList(string lang) => Rows(
        [Cb(RouteLabel(ApplicationRoutes.Teriberka, lang), Callbacks.RouteInfo(ApplicationRoutes.Teriberka))],
        [Cb(RouteLabel(ApplicationRoutes.Lovozero, lang), Callbacks.RouteInfo(ApplicationRoutes.Lovozero))],
        [Cb(RouteLabel(ApplicationRoutes.Tersky, lang), Callbacks.RouteInfo(ApplicationRoutes.Tersky))],
        [Cb(RouteLabel(ApplicationRoutes.Custom, lang), Callbacks.RouteInfo(ApplicationRoutes.Custom))],
        [MenuButton(lang)]);

    public static InlineKeyboardMarkup RouteInfo(string lang, string code) => Rows(
        [Cb(BotIcons.Book, "BotBtnBookRoute", lang, Callbacks.BookRoute(code))],
        [Cb(BotIcons.Back, "BotBtnToRoutes", lang, Callbacks.Routes), MenuButton(lang)]);

    public static InlineKeyboardMarkup FaqList(string lang)
    {
        var rows = new List<InlineKeyboardButton[]>();

        for (var i = 1; i <= BotScreens.FaqCount; i++)
            rows.Add([Cb($"{BotIcons.FaqItems[i - 1]} {T($"Faq{i}Question", lang)}", Callbacks.FaqInfo(i))]);

        rows.Add(FooterRow(lang));

        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup FaqAnswer(string lang) => Rows(
        [Cb(BotIcons.Back, "BotBtnToFaq", lang, Callbacks.Faq)],
        FooterRow(lang));

    public static InlineKeyboardMarkup Contacts(string lang, BotOptions options)
    {
        var rows = new List<InlineKeyboardButton[]>();

        if (options.MaxUrl is not null)
            rows.Add([Url($"{BotIcons.Max} MAX", options.MaxUrl), Url(BotIcons.Site, "BotMenuSite", lang, options.SiteUrl)]);
        else
            rows.Add([Url(BotIcons.Site, "BotMenuSite", lang, options.SiteUrl)]);

        rows.Add(FooterRow(lang));

        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>Запись недоступна (нет API, пауза, дневной потолок): сайт, менеджер, меню.</summary>
    public static InlineKeyboardMarkup Gate(string lang, BotOptions options) => Rows(
        [Url(BotIcons.Site, "BotBtnSiteApply", lang, options.ApplyUrl)],
        [Cb(BotIcons.Write, "BotMenuWrite", lang, Callbacks.Write), MenuButton(lang)]);

    public static InlineKeyboardMarkup DraftExists(string lang) => Rows(
        [Cb(BotIcons.Resume, "BotBtnResume", lang, Callbacks.Resume), Cb(BotIcons.Restart, "BotBtnRestart", lang, Callbacks.Restart)],
        [CancelButton(lang)]);

    public static InlineKeyboardMarkup StepRoute(string lang) => Rows(
        [Cb(RouteLabel(ApplicationRoutes.Teriberka, lang), Callbacks.BookRoute(ApplicationRoutes.Teriberka))],
        [Cb(RouteLabel(ApplicationRoutes.Lovozero, lang), Callbacks.BookRoute(ApplicationRoutes.Lovozero))],
        [Cb(RouteLabel(ApplicationRoutes.Tersky, lang), Callbacks.BookRoute(ApplicationRoutes.Tersky))],
        [Cb(RouteLabel(ApplicationRoutes.Custom, lang), Callbacks.BookRoute(ApplicationRoutes.Custom))],
        [CancelButton(lang)]);

    public static InlineKeyboardMarkup StepDate(string lang) => Rows(
        [Cb(BotIcons.DateSkip, "BotBtnDateSkip", lang, Callbacks.DateSkip)],
        NavRow(lang));

    public static InlineKeyboardMarkup StepPeople(string lang) => Rows(
        [Cb("1", Callbacks.People(1)), Cb("2", Callbacks.People(2)), Cb("3", Callbacks.People(3)), Cb("4", Callbacks.People(4))],
        [Cb("5", Callbacks.People(5)), Cb("6", Callbacks.People(6)), Cb("7", Callbacks.People(7)), Cb("8", Callbacks.People(8))],
        [Cb(BotIcons.PeopleMore, "BotBtnPeopleMore", lang, Callbacks.People(BotScreens.PeopleMoreThanMax))],
        NavRow(lang));

    public static InlineKeyboardMarkup StepWishes(string lang) => Rows(
        [Cb(BotIcons.WishesSkip, "BotBtnWishesSkip", lang, Callbacks.WishesSkip)],
        NavRow(lang));

    public static InlineKeyboardMarkup StepName(string lang, string telegramName)
    {
        var rows = new List<InlineKeyboardButton[]>();

        if (telegramName.Length > 0)
            rows.Add([Cb($"{BotIcons.NameTg} {BotStrings.Format("BotBtnNameTg", lang, telegramName)}", Callbacks.NameFromTelegram)]);

        rows.Add(NavRow(lang));

        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>Шаг телефона — reply-клавиатура: только она умеет запросить контакт.</summary>
    public static ReplyKeyboardMarkup StepPhone(string lang) => new(new[]
    {
        new[] { KeyboardButton.WithRequestContact($"{BotIcons.SharePhone} {T("BotBtnSharePhone", lang)}") },
        new[] { new KeyboardButton(BackLabel(lang)), new KeyboardButton(CancelLabel(lang)) },
    })
    {
        ResizeKeyboard = true,
        OneTimeKeyboard = true,
    };

    public static InlineKeyboardMarkup Summary(string lang, BotOptions options) => Rows(
        [Cb(BotIcons.Submit, "BotBtnSubmit", lang, Callbacks.Submit)],
        [Url(BotIcons.Policy, "BotBtnPolicy", lang, options.PrivacyUrl)],
        [Edit("BotBtnEditRoute", lang, Callbacks.Fields.Route), Edit("BotBtnEditDate", lang, Callbacks.Fields.Date)],
        [Edit("BotBtnEditPeople", lang, Callbacks.Fields.People), Edit("BotBtnEditWishes", lang, Callbacks.Fields.Wishes)],
        [Edit("BotBtnEditName", lang, Callbacks.Fields.Name), Edit("BotBtnEditPhone", lang, Callbacks.Fields.Phone)],
        [CancelButton(lang)]);

    public static InlineKeyboardMarkup SubmitInvalid(string lang) => Rows(
        [Edit("BotBtnEditPhone", lang, Callbacks.Fields.Phone), Edit("BotBtnEditName", lang, Callbacks.Fields.Name)],
        [CancelButton(lang)]);

    public static InlineKeyboardMarkup SubmitFailed(string lang, BotOptions options) => Rows(
        [Cb(BotIcons.Retry, "BotBtnRetry", lang, Callbacks.Retry)],
        [Url(BotIcons.Site, "BotBtnSiteApply", lang, options.ApplyUrl)],
        [CancelButton(lang)]);

    public static InlineKeyboardMarkup Sent(string lang, BotOptions options) => Rows(
        [MenuButton(lang)],
        [Url(BotIcons.Site, "BotMenuSite", lang, options.SiteUrl)]);

    public static InlineKeyboardMarkup PendingQuestion(string lang) => Rows(
        [Cb(BotIcons.SendToManager, "BotBtnSendToManager", lang, Callbacks.WriteSend)],
        [Cb(BotIcons.Menu, "BotBtnMenu", lang, Callbacks.WriteDrop)]);

    private static InlineKeyboardButton[] FooterRow(string lang) =>
        [Cb(BotIcons.Book, "BotBtnBook", lang, Callbacks.Book), MenuButton(lang)];

    private static InlineKeyboardButton[] NavRow(string lang) =>
        [Cb(BotIcons.Back, "BotBtnBack", lang, Callbacks.Back), CancelButton(lang)];

    private static InlineKeyboardButton MenuButton(string lang) => Cb(BotIcons.Menu, "BotBtnMenu", lang, Callbacks.Menu);

    private static InlineKeyboardButton CancelButton(string lang) => Cb(BotIcons.Cancel, "BotBtnCancel", lang, Callbacks.Cancel);

    private static InlineKeyboardButton Edit(string key, string lang, string field)
        => Cb(BotIcons.Edit, key, lang, Callbacks.Edit(field));

    private static InlineKeyboardMarkup Rows(params InlineKeyboardButton[][] rows) => new(rows);

    private static InlineKeyboardButton Cb(string icon, string key, string lang, string data)
        => InlineKeyboardButton.WithCallbackData($"{icon} {T(key, lang)}", data);

    private static InlineKeyboardButton Cb(string text, string data) => InlineKeyboardButton.WithCallbackData(text, data);

    private static InlineKeyboardButton Url(string icon, string key, string lang, string url)
        => InlineKeyboardButton.WithUrl($"{icon} {T(key, lang)}", url);

    private static InlineKeyboardButton Url(string text, string url) => InlineKeyboardButton.WithUrl(text, url);

    private static string T(string key, string lang) => BotStrings.Get(key, lang);
}
