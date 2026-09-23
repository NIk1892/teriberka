using Applications.Contracts;
using Applications.Contracts.Validators;
using Chat.Contracts;
using Chat.Domain;
using Telegram.Bot.Types.ReplyMarkups;

namespace Chat.Bot.Dm;

/// <summary>
/// Машина состояний лички: (диалог, вход) → правки диалога + список ответов. Внутри нет
/// ни Telegram, ни HTTP, ни базы — только тексты и клавиатуры, поэтому её можно прогнать
/// без сети. Эффекты (отправить заявку, передать менеджеру) исполняет раннер и возвращает
/// результат вторым вызовом Handle.
///
/// Правило мастера: «следующий шаг — первый незаполненный» в порядке маршрут → дата →
/// люди → пожелания → имя → телефон → сводка. Поэтому «✏️ поправить поле» просто обнуляет
/// его, а после ввода машина сама возвращается в сводку; «назад» обнуляет предыдущее поле.
/// </summary>
public sealed class BotDialogMachine
{
    /// <summary>Пауза между заявками одного пользователя и потолок за сутки (UTC).</summary>
    private static readonly TimeSpan SubmitCooldown = TimeSpan.FromMinutes(5);
    private const int DailySubmitCap = 5;

    private const int DateMaxLength = 64;
    private const int WishesMaxLength = 500;
    private const int NameMaxLength = 255;

    private static readonly BotDialogStep[] Order =
    [
        BotDialogStep.Route, BotDialogStep.Date, BotDialogStep.People, BotDialogStep.Wishes,
        BotDialogStep.Name, BotDialogStep.Phone, BotDialogStep.Summary,
    ];

    /// <summary>Тот же валидатор, что у сайта и users: 422 от шлюза становится редкостью.</summary>
    private static readonly ApplicationCreateCommandValidator Validator = new();

    public IReadOnlyList<BotReply> Handle(BotDialogEntity dialog, DialogInput input, MachineContext ctx) => input switch
    {
        CommandInput command => OnCommand(dialog, command, ctx),
        CallbackInput callback => OnCallback(dialog, callback, ctx),
        TextInput text => OnText(dialog, text, ctx),
        ContactInput contact => OnContact(dialog, contact, ctx),
        SubmitResultInput submit => OnSubmitResult(dialog, submit, ctx),
        ForwardResultInput forward => OnForwardResult(dialog, forward, ctx),
        _ => OnUnsupported(dialog, ctx),
    };

    #region Команды и кнопки

    private IReadOnlyList<BotReply> OnCommand(BotDialogEntity d, CommandInput command, MachineContext ctx)
    {
        var lang = ctx.Lang;

        switch (command.Name)
        {
            case "start":
                if (string.Equals(command.Payload, "book", StringComparison.OrdinalIgnoreCase))
                    return StartBooking(d, ctx, inPlace: false, route: null);

                LeaveChat(d);
                return [Screen(BotScreens.Welcome(lang), BotKeyboards.Menu(lang, ctx.Options), false)];

            case "menu":
                LeaveChat(d);
                return [Screen(BotScreens.Menu(lang), BotKeyboards.Menu(lang, ctx.Options), false)];

            case "book":
                return StartBooking(d, ctx, inPlace: false, route: null);

            case "program":
                return [Screen(BotScreens.Program(lang), BotKeyboards.InfoFooter(lang), false)];

            case "price":
                return [Screen(BotScreens.Price(lang), BotKeyboards.InfoFooter(lang), false)];

            case "routes":
                return [Screen(BotScreens.Routes(lang), BotKeyboards.RouteList(lang), false)];

            case "faq":
                return [Screen(BotScreens.Faq(lang), BotKeyboards.FaqList(lang), false)];

            case "contacts":
                return [Screen(BotScreens.Contacts(lang, ctx.Options), BotKeyboards.Contacts(lang, ctx.Options), false)];

            case "write":
                return EnterChat(d, ctx, inPlace: false);

            case "cancel":
                return Cancel(d, ctx, inPlace: false);

            default:
                return [Screen(BotScreens.UnknownCommand(lang), BotKeyboards.Menu(lang, ctx.Options), false)];
        }
    }

    private IReadOnlyList<BotReply> OnCallback(BotDialogEntity d, CallbackInput callback, MachineContext ctx)
    {
        var lang = ctx.Lang;

        if (!Callbacks.TryParse(callback.Data, out var ns, out var action, out var argument))
            return Stale(d, ctx);

        // Любое действие, кроме ответа на предложение «передать менеджеру», снимает отложенный вопрос.
        if (ns != "w")
            d.PendingText = null;

        switch (ns, action)
        {
            case ("m", "menu"):
                LeaveChat(d);
                return [Screen(BotScreens.Menu(lang), BotKeyboards.Menu(lang, ctx.Options), true)];

            case ("m", "book"):
                return StartBooking(d, ctx, inPlace: true, route: null);

            case ("m", "prog"):
                return [Screen(BotScreens.Program(lang), BotKeyboards.InfoFooter(lang), true)];

            case ("m", "price"):
                return [Screen(BotScreens.Price(lang), BotKeyboards.InfoFooter(lang), true)];

            case ("m", "routes"):
                return [Screen(BotScreens.Routes(lang), BotKeyboards.RouteList(lang), true)];

            case ("m", "faq"):
                return [Screen(BotScreens.Faq(lang), BotKeyboards.FaqList(lang), true)];

            case ("m", "contacts"):
                return [Screen(BotScreens.Contacts(lang, ctx.Options), BotKeyboards.Contacts(lang, ctx.Options), true)];

            case ("m", "write"):
                return EnterChat(d, ctx, inPlace: true);

            case ("i", "route") when ApplicationRoutes.IsKnown(argument):
                return [Screen(BotScreens.RouteInfo(lang, argument!), BotKeyboards.RouteInfo(lang, argument!), true)];

            case ("i", "faq") when int.TryParse(argument, out var number) && number is >= 1 and <= BotScreens.FaqCount:
                return [Screen(BotScreens.FaqAnswer(lang, number), BotKeyboards.FaqAnswer(lang), true)];

            case ("b", "route") when ApplicationRoutes.IsKnown(argument):
                if (d.Step == BotDialogStep.Route)
                {
                    d.Route = argument;
                    return Next(d, ctx, inPlace: true);
                }

                // Кнопка «записаться на этот маршрут» с инфо-экрана — старт мастера с выбранным маршрутом.
                return d.IsBooking ? StepPassed(d, ctx) : StartBooking(d, ctx, inPlace: true, route: argument);

            case ("b", "date") when argument == "skip":
                if (d.Step != BotDialogStep.Date)
                    return StepPassed(d, ctx);

                d.DateText = BotScreens.DateUnknown;
                return Next(d, ctx, inPlace: true);

            case ("b", "day"):
                if (d.Step != BotDialogStep.Date)
                    return StepPassed(d, ctx);

                // День из календаря; прошедший (кнопка пролежала с вчера) — показать месяц заново.
                if (!BotCalendar.TryParseDay(argument, BotCalendar.Today(ctx.UtcNow), out var day))
                    return DateScreen(ctx, month: null, inPlace: true);

                d.DateText = BotCalendar.DateArg(day);
                return Next(d, ctx, inPlace: true);

            case ("b", "cal"):
                return d.Step == BotDialogStep.Date
                    ? DateScreen(ctx, argument, inPlace: true)
                    : StepPassed(d, ctx);

            case ("b", "noop"):
                // Шапка календаря и пустые клетки: только погасить «часики» у кнопки.
                return [];

            case ("b", "people") when int.TryParse(argument, out var people) && people is >= 1 and <= BotScreens.PeopleMoreThanMax:
                if (d.Step != BotDialogStep.People)
                    return StepPassed(d, ctx);

                d.People = people;
                return Next(d, ctx, inPlace: true);

            case ("b", "wishes") when argument == "skip":
                if (d.Step != BotDialogStep.Wishes)
                    return StepPassed(d, ctx);

                d.Wishes = string.Empty;
                return Next(d, ctx, inPlace: true);

            case ("b", "name") when argument == "tg":
                if (d.Step != BotDialogStep.Name)
                    return StepPassed(d, ctx);

                if (ctx.TelegramName.Length == 0)
                    return ScreenFor(BotDialogStep.Name, d, ctx, inPlace: true);

                d.Name = Truncate(ctx.TelegramName, NameMaxLength);
                return Next(d, ctx, inPlace: true);

            case ("b", "submit") or ("b", "retry"):
                return d.Step == BotDialogStep.Summary ? Submit(d, ctx) : StepPassed(d, ctx);

            case ("b", "edit") when FieldStep(argument) is { } step:
                if (d.Step != BotDialogStep.Summary)
                    return StepPassed(d, ctx);

                ClearField(d, step);
                d.Step = step;
                return ScreenFor(step, d, ctx, inPlace: true);

            case ("b", "back"):
                return d.IsBooking ? Back(d, ctx, inPlace: true) : StepPassed(d, ctx);

            case ("b", "cancel"):
                return Cancel(d, ctx, inPlace: true);

            case ("b", "resume"):
                return d.HasDraft ? Next(d, ctx, inPlace: true) : StartBooking(d, ctx, inPlace: true, route: null);

            case ("b", "restart"):
                d.ClearDraft();
                d.Step = BotDialogStep.Idle;
                return StartBooking(d, ctx, inPlace: true, route: null);

            case ("w", "send"):
                if (d.PendingText is not { Length: > 0 } pending)
                    return Stale(d, ctx);

                d.PendingText = null;
                d.Step = BotDialogStep.Chat;
                return [new ForwardEffect(pending, Announce: true)];

            case ("w", "drop"):
                d.PendingText = null;
                return [Screen(BotScreens.Menu(lang), BotKeyboards.Menu(lang, ctx.Options), true)];

            default:
                return Stale(d, ctx);
        }
    }

    #endregion

    #region Текст и контакт

    private IReadOnlyList<BotReply> OnText(BotDialogEntity d, TextInput input, MachineContext ctx)
    {
        var lang = ctx.Lang;
        var text = input.Text.Trim();

        if (text.Length == 0)
            return OnUnsupported(d, ctx);

        // Reply-кнопки шага телефона приходят текстом — и могут прийти на любом шаге,
        // если клавиатура осталась открытой.
        if (BotStrings.IsLabel("BotBtnBack", BotIcons.Back, text))
            return d.IsBooking ? Back(d, ctx, inPlace: false) : [Screen(BotScreens.Menu(lang), BotKeyboards.Menu(lang, ctx.Options), false)];

        if (BotStrings.IsLabel("BotBtnCancel", BotIcons.Cancel, text))
            return Cancel(d, ctx, inPlace: false);

        switch (d.Step)
        {
            case BotDialogStep.Idle:
                if (text.Length > ChatLimits.MaxTextLength)
                    return [Screen(BotScreens.ForwardTooLong(lang, ChatLimits.MaxTextLength), BotKeyboards.MenuOnly(lang), false)];

                // Уже переписывался с менеджером — это продолжение разговора, без переспросов.
                if (ctx.HasChatSession)
                {
                    d.Step = BotDialogStep.Chat;
                    return [new ForwardEffect(text, Announce: true)];
                }

                d.PendingText = text;
                return [Screen(BotScreens.FreeTextOffer(lang), BotKeyboards.PendingQuestion(lang), false)];

            case BotDialogStep.Chat:
                if (text.Length > ChatLimits.MaxTextLength)
                    return [Screen(BotScreens.ForwardTooLong(lang, ChatLimits.MaxTextLength), BotKeyboards.MenuOnly(lang), false)];

                return [new ForwardEffect(text, Announce: false)];

            case BotDialogStep.Route:
                return ScreenFor(BotDialogStep.Route, d, ctx, inPlace: false, prefix: BotScreens.UseButtons(lang));

            case BotDialogStep.Date:
                d.DateText = Truncate(text, DateMaxLength);
                return Next(d, ctx, inPlace: false);

            case BotDialogStep.People:
                if (!TryParsePeople(text, out var people))
                    return ScreenFor(BotDialogStep.People, d, ctx, inPlace: false, prefix: BotScreens.PeopleInvalid(lang));

                d.People = people;
                return Next(d, ctx, inPlace: false);

            case BotDialogStep.Wishes:
                d.Wishes = Truncate(text, WishesMaxLength);
                return Next(d, ctx, inPlace: false);

            case BotDialogStep.Name:
                d.Name = Truncate(text, NameMaxLength);
                return Next(d, ctx, inPlace: false);

            case BotDialogStep.Phone:
                if (!BotPhone.TryNormalize(text, out var phone))
                    return [new KeyboardReply(BotScreens.PhoneInvalid(lang), BotKeyboards.StepPhone(lang))];

                d.Phone = phone;
                return [new AckReply(BotScreens.PhoneAccepted(lang)), .. Next(d, ctx, inPlace: false)];

            case BotDialogStep.Summary:
                return [Screen(BotScreens.Summary(lang, d, BotScreens.UseButtons(lang)), BotKeyboards.Summary(lang, ctx.Options), false)];

            default:
                return OnUnsupported(d, ctx);
        }
    }

    private IReadOnlyList<BotReply> OnContact(BotDialogEntity d, ContactInput contact, MachineContext ctx)
    {
        if (d.Step != BotDialogStep.Phone)
            return OnUnsupported(d, ctx);

        if (!contact.IsOwn)
            return [new KeyboardReply(BotScreens.PhoneNotOwn(ctx.Lang), BotKeyboards.StepPhone(ctx.Lang))];

        d.Phone = Truncate(BotPhone.FromContact(contact.PhoneNumber), BotPhone.MaxLength);
        return [new AckReply(BotScreens.PhoneAccepted(ctx.Lang)), .. Next(d, ctx, inPlace: false)];
    }

    private IReadOnlyList<BotReply> OnUnsupported(BotDialogEntity d, MachineContext ctx)
    {
        var lang = ctx.Lang;

        return d.Step switch
        {
            BotDialogStep.Idle => [Screen($"{BotScreens.OnlyText(lang)}\n\n{BotScreens.Menu(lang)}", BotKeyboards.Menu(lang, ctx.Options), false)],
            BotDialogStep.Chat => [Screen(BotScreens.ChatOnlyText(lang), BotKeyboards.MenuOnly(lang), false)],
            _ => ScreenFor(d.Step, d, ctx, inPlace: false, prefix: BotScreens.OnlyText(lang)),
        };
    }

    #endregion

    #region Результаты эффектов

    private IReadOnlyList<BotReply> OnSubmitResult(BotDialogEntity d, SubmitResultInput input, MachineContext ctx)
    {
        var lang = ctx.Lang;

        switch (input.Result.Outcome)
        {
            case SubmitOutcome.Created:
                d.ClearDraft();
                d.Step = BotDialogStep.Idle;
                d.LastSubmittedAt = ctx.UtcNow;

                var today = DateOnly.FromDateTime(ctx.UtcNow);
                if (d.SubmitDayUtc == today)
                {
                    d.SubmitDayCount += 1;
                }
                else
                {
                    d.SubmitDayUtc = today;
                    d.SubmitDayCount = 1;
                }

                return [Screen(BotScreens.Sent(lang), BotKeyboards.Sent(lang, ctx.Options), input.InPlace)];

            case SubmitOutcome.Invalid:
                return [Screen(BotScreens.SubmitInvalid(lang, input.Result.Title), BotKeyboards.SubmitInvalid(lang), input.InPlace)];

            case SubmitOutcome.RateLimited:
                return [Screen(BotScreens.SubmitRateLimited(lang), BotKeyboards.SubmitFailed(lang, ctx.Options), input.InPlace)];

            default:
                return [Screen(BotScreens.SubmitUnavailable(lang, ctx.Options), BotKeyboards.SubmitFailed(lang, ctx.Options), input.InPlace)];
        }
    }

    private IReadOnlyList<BotReply> OnForwardResult(BotDialogEntity d, ForwardResultInput input, MachineContext ctx)
    {
        var lang = ctx.Lang;

        return input.Outcome switch
        {
            ForwardOutcome.Sent when input.Announce =>
                [Screen(BotScreens.Forwarded(lang), BotKeyboards.MenuOnly(lang), input.InPlace)],
            ForwardOutcome.Sent => [new ReactReply(BotIcons.ReactionForwarded)],
            ForwardOutcome.TooMany => [Screen(BotScreens.ForwardTooMany(lang), BotKeyboards.MenuOnly(lang), input.InPlace)],
            _ => [Screen(BotScreens.ForwardFailed(lang), BotKeyboards.MenuOnly(lang), input.InPlace)],
        };
    }

    #endregion

    #region Мастер заявки

    private IReadOnlyList<BotReply> StartBooking(BotDialogEntity d, MachineContext ctx, bool inPlace, string? route)
    {
        var lang = ctx.Lang;

        LeaveChat(d);

        if (d.IsBooking || d.HasDraft)
            return [Screen(BotScreens.DraftExists(lang), BotKeyboards.DraftExists(lang), inPlace)];

        if (Gate(d, ctx) is { } blocked)
            return [Screen(blocked, BotKeyboards.Gate(lang, ctx.Options), inPlace)];

        d.ClearDraft();
        d.Route = route;

        return Next(d, ctx, inPlace);
    }

    /// <summary>Почему записаться сейчас нельзя; null — можно.</summary>
    private static string? Gate(BotDialogEntity d, MachineContext ctx)
    {
        if (!ctx.Options.BookingEnabled)
            return BotScreens.BookingDisabled(ctx.Lang, ctx.Options);

        if (d.LastSubmittedAt is { } last && last + SubmitCooldown > ctx.UtcNow)
            return BotScreens.Cooldown(ctx.Lang, (int)Math.Ceiling((last + SubmitCooldown - ctx.UtcNow).TotalMinutes));

        if (d.SubmitDayUtc == DateOnly.FromDateTime(ctx.UtcNow) && d.SubmitDayCount >= DailySubmitCap)
            return BotScreens.DailyCap(ctx.Lang);

        return null;
    }

    private IReadOnlyList<BotReply> Submit(BotDialogEntity d, MachineContext ctx)
    {
        var lang = ctx.Lang;

        // Проверяем ещё раз: между сводкой и нажатием могла уйти другая заявка.
        if (Gate(d, ctx) is { } blocked)
            return [Screen(blocked, BotKeyboards.Gate(lang, ctx.Options), true)];

        var command = new ApplicationCreateCommand
        {
            Title = NullIfBlank(d.Name),
            Phone = d.Phone,
            Route = d.Route,
            Source = ApplicationSources.Telegram,
            TgUserId = ctx.UserId,
            TgUsername = NullIfBlank(ctx.Username),
            Details = BotScreens.Details(d),
        };

        var validation = Validator.Validate(command);
        if (!validation.IsValid)
        {
            var title = string.Join(", ", validation.Errors.Select(e => e.ErrorMessage).Distinct());
            return [Screen(BotScreens.SubmitInvalid(lang, title), BotKeyboards.SubmitInvalid(lang), true)];
        }

        return [new SubmitEffect(command)];
    }

    private IReadOnlyList<BotReply> Next(BotDialogEntity d, MachineContext ctx, bool inPlace, string? prefix = null)
    {
        d.Step = FirstUnfilled(d);
        return ScreenFor(d.Step, d, ctx, inPlace, prefix);
    }

    private static BotDialogStep FirstUnfilled(BotDialogEntity d)
    {
        if (d.Route is null) return BotDialogStep.Route;
        if (d.DateText is null) return BotDialogStep.Date;
        if (d.People is null) return BotDialogStep.People;
        if (d.Wishes is null) return BotDialogStep.Wishes;
        if (d.Name is null) return BotDialogStep.Name;
        if (d.Phone is null) return BotDialogStep.Phone;
        return BotDialogStep.Summary;
    }

    private static IReadOnlyList<BotReply> ScreenFor(BotDialogStep step, BotDialogEntity d, MachineContext ctx, bool inPlace, string? prefix = null)
    {
        var lang = ctx.Lang;

        return step switch
        {
            BotDialogStep.Route => [Screen(P(BotScreens.Step(lang, 1, "BotStepRoute")), BotKeyboards.StepRoute(lang), inPlace)],
            BotDialogStep.Date => DateScreen(ctx, month: null, inPlace, prefix),
            BotDialogStep.People => [Screen(P(BotScreens.Step(lang, 3, "BotStepPeople")), BotKeyboards.StepPeople(lang), inPlace)],
            BotDialogStep.Wishes => [Screen(P($"{BotScreens.Estimate(lang, d.Route, d.People)}\n\n{BotScreens.Step(lang, 4, "BotStepWishes")}"), BotKeyboards.StepWishes(lang), inPlace)],
            BotDialogStep.Name => [Screen(P(BotScreens.Step(lang, 5, "BotStepName")), BotKeyboards.StepName(lang, ctx.TelegramName), inPlace)],
            BotDialogStep.Phone => [new KeyboardReply(P(BotScreens.Step(lang, 6, "BotStepPhone")), BotKeyboards.StepPhone(lang))],
            BotDialogStep.Summary => [Screen(BotScreens.Summary(lang, d, prefix), BotKeyboards.Summary(lang, ctx.Options), inPlace)],
            _ => [Screen(P(BotScreens.Menu(lang)), BotKeyboards.Menu(lang, ctx.Options), inPlace)],
        };

        string P(string html) => prefix is null ? html : $"{prefix}\n\n{html}";
    }

    /// <summary>Шаг даты: календарь месяца (по умолчанию текущего по Москве).</summary>
    private static IReadOnlyList<BotReply> DateScreen(MachineContext ctx, string? month, bool inPlace, string? prefix = null)
    {
        var today = BotCalendar.Today(ctx.UtcNow);
        var html = BotScreens.Step(ctx.Lang, 2, "BotStepDate");

        return
        [
            Screen(prefix is null ? html : $"{prefix}\n\n{html}",
                BotCalendar.Keyboard(ctx.Lang, BotCalendar.ParseMonth(month, today), today), inPlace),
        ];
    }

    private IReadOnlyList<BotReply> Back(BotDialogEntity d, MachineContext ctx, bool inPlace)
    {
        var index = Array.IndexOf(Order, d.Step);

        // На первом шаге назад некуда — просто показать его ещё раз.
        if (index <= 0)
            return ScreenFor(BotDialogStep.Route, d, ctx, inPlace);

        var previous = Order[index - 1];
        ClearField(d, previous);
        d.Step = previous;

        return ScreenFor(previous, d, ctx, inPlace);
    }

    private IReadOnlyList<BotReply> Cancel(BotDialogEntity d, MachineContext ctx, bool inPlace)
    {
        var lang = ctx.Lang;
        var hadDraft = d.IsBooking || d.HasDraft;

        d.ClearDraft();
        d.Step = BotDialogStep.Idle;

        return [Screen(hadDraft ? BotScreens.Cancelled(lang) : BotScreens.Menu(lang), BotKeyboards.Menu(lang, ctx.Options), inPlace)];
    }

    private IReadOnlyList<BotReply> StepPassed(BotDialogEntity d, MachineContext ctx)
        => d.IsBooking
            ? [new ToastReply(BotScreens.StepPassedToast(ctx.Lang)), .. ScreenFor(d.Step, d, ctx, inPlace: true)]
            : Stale(d, ctx);

    private static IReadOnlyList<BotReply> Stale(BotDialogEntity d, MachineContext ctx)
        =>
        [
            new ToastReply(BotScreens.StaleButtonToast(ctx.Lang)),
            Screen(BotScreens.Menu(ctx.Lang), BotKeyboards.Menu(ctx.Lang, ctx.Options), true),
        ];

    private static IReadOnlyList<BotReply> EnterChat(BotDialogEntity d, MachineContext ctx, bool inPlace)
    {
        // Черновик заявки не трогаем: вернуться к нему можно через /book → «Продолжить».
        d.Step = BotDialogStep.Chat;
        return [Screen(BotScreens.WriteIntro(ctx.Lang), BotKeyboards.MenuOnly(ctx.Lang), inPlace)];
    }

    private static void LeaveChat(BotDialogEntity d)
    {
        if (d.Step == BotDialogStep.Chat)
            d.Step = BotDialogStep.Idle;
    }

    private static BotDialogStep? FieldStep(string? field) => field switch
    {
        Callbacks.Fields.Route => BotDialogStep.Route,
        Callbacks.Fields.Date => BotDialogStep.Date,
        Callbacks.Fields.People => BotDialogStep.People,
        Callbacks.Fields.Wishes => BotDialogStep.Wishes,
        Callbacks.Fields.Name => BotDialogStep.Name,
        Callbacks.Fields.Phone => BotDialogStep.Phone,
        _ => null,
    };

    private static void ClearField(BotDialogEntity d, BotDialogStep step)
    {
        switch (step)
        {
            case BotDialogStep.Route: d.Route = null; break;
            case BotDialogStep.Date: d.DateText = null; break;
            case BotDialogStep.People: d.People = null; break;
            case BotDialogStep.Wishes: d.Wishes = null; break;
            case BotDialogStep.Name: d.Name = null; break;
            case BotDialogStep.Phone: d.Phone = null; break;
        }
    }

    private static bool TryParsePeople(string text, out int people)
    {
        people = 0;

        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is 0 or > 2 || !int.TryParse(digits, out var value) || value < 1)
            return false;

        people = value > BotOptions.MaxPeopleForEstimate ? BotScreens.PeopleMoreThanMax : value;
        return true;
    }

    #endregion

    private static ScreenReply Screen(string html, InlineKeyboardMarkup? keyboard, bool inPlace) => new(html, keyboard, inPlace);

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
