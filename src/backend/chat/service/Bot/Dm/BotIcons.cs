namespace Chat.Bot.Dm;

/// <summary>
/// Эмодзи кнопок и заголовков лички. Одно место на все три языка: подписи живут в
/// BotStrings.resx без иконок, а иконка приклеивается к подписи здесь. Набор подобран
/// 23.09.2026 (три варианта + судья): только эмодзи, которые одинаково рисуются в Telegram
/// на iOS, Android и Desktop — без ZWJ-последовательностей и тонов кожи, text-default
/// глифы (стрелки, карандаш, самолёт, часы) обязательно с селектором U+FE0F, иначе
/// Telegram Desktop на Windows покажет монохромный шрифтовой знак.
///
/// Правила семейств при добавлении кнопок: одно действие — одна иконка на всех экранах
/// (🚐 запись, 🌐 сайт, ⬅️ любой возврат на уровень выше, 🏠 меню, ❌ отмена); заголовок
/// экрана повторяет иконку кнопки, по которой в него попали; состояния отличны от действий
/// (🎉 принята, ❌ отменена, ⚠️ сбой); 🐋 — бренд и Териберка, 🐳 — морская прогулка;
/// 📞 — позвонить нам, 📱 — мой номер, 💬 — живой чат, 📨 — переслать текст.
/// </summary>
public static class BotIcons
{
    // главное меню
    public const string Book = "🚐";
    public const string Program = "🗺️";
    public const string Price = "💰";
    public const string Routes = "🧭";
    public const string Faq = "❓";
    public const string Contacts = "📞";
    public const string Write = "💬";
    public const string Site = "🌐";

    // навигация
    public const string Menu = "🏠";
    public const string Back = "⬅️";
    public const string Cancel = "❌";

    // действия
    public const string Policy = "🔒";
    public const string Submit = "✅";
    public const string Retry = "🔄";
    public const string Resume = "▶️";
    public const string Restart = "🔁";
    public const string DateSkip = "🤷";
    public const string PeopleMore = "👥";
    public const string WishesSkip = "➡️";
    public const string NameTg = "👤";
    public const string SharePhone = "📱";
    public const string SendToManager = "📨";
    public const string Edit = "✏️";
    public const string Max = "💬";

    // маршруты
    public const string RouteTeriberka = "🐋";
    public const string RouteLovozero = "🦌";
    public const string RouteTersky = "💎";
    public const string RouteCustom = "✨";

    // частые вопросы (порядок = Faq1..6: погода, одежда, дети, оплата, длительность, что после заявки)
    public static readonly string[] FaqItems = ["🌨️", "🧥", "👪", "💳", "⏱️", "📞"];

    // остановки программы дня (порядок = Itinerary1..8)
    public static readonly string[] Stops = ["✈️", "🛣️", "🦀", "🐳", "⚓", "🍽️", "🏞️", "🌆"];

    // заголовки экранов и состояния
    public const string Welcome = "🐋";
    public const string Estimate = "💰";
    public const string Summary = "📋";
    public const string Sent = "🎉";
    public const string Cancelled = "❌";
    public const string Error = "⚠️";
    public const string Forwarded = "📨";
    public const string Included = "✅";

    /// <summary>Реакция на сообщение, ушедшее менеджеру. Ровно U+1F44C без селектора: Bot API сверяет строку с разрешённым набором.</summary>
    public const string ReactionForwarded = "👌";
}
