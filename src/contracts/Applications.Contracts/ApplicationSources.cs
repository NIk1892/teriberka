namespace Applications.Contracts;

/// <summary>
/// Откуда пришла заявка. Код общий для сайта, users и бота: в БД лежит код, а как его
/// показать (бейдж на /manager, строка в отбивке в канал) решает потребитель.
/// Сайт ставит <see cref="Site"/> сам — сервером, а не из формы (подделанный POST не должен
/// помечать заявку «из Telegram»); <see cref="Telegram"/> ставит только бот из chat-сервиса.
/// </summary>
public static class ApplicationSources
{
    public const string Site = "site";
    public const string Telegram = "telegram";

    public static readonly string[] All = [Site, Telegram];

    public static bool IsKnown(string? code) => code is not null && All.Contains(code);
}
