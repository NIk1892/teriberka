namespace Applications.Contracts;

/// <summary>
/// Направления, между которыми выбирает посетитель в форме. Коды общие для сайта
/// и сервиса: в БД лежит именно код, а человекочитаемое название берётся из resx
/// (ключи MapSpot1..3) — так переименование маршрута не требует миграции.
/// </summary>
public static class ApplicationRoutes
{
    public const string Teriberka = "teriberka";
    public const string Lovozero = "lovozero";
    public const string Tersky = "tersky";

    /// <summary>
    /// Индивидуальный маршрут: программа собирается под запрос, готового описания
    /// направления у него нет — в форме подписан своими ключами RouteCustom*.
    /// </summary>
    public const string Custom = "custom";

    /// <summary>Порядок = порядок пунктов в форме; первый — значение по умолчанию.</summary>
    public static readonly string[] All = [Teriberka, Lovozero, Tersky, Custom];

    public static bool IsKnown(string? code) => code is not null && All.Contains(code);
}
