namespace UI.Public.Web.Features.Places;

/// <summary>
/// Каталог мест экскурсии. Это контент сайта, а не данные, поэтому БД не используется:
/// slug задаёт URL страницы места, Index — номер ключей в resx
/// (Place{Index}Title / Place{Index}Text / Place{Index}Detail1 / Place{Index}Detail2).
/// Новое место = строка здесь + ключи в трёх resx.
/// </summary>
public sealed record PlaceInfo(int Index, string Slug);

public static class PlaceCatalog
{
    public static readonly PlaceInfo[] All =
    [
        new(1, "dragon-eggs"),
        new(2, "ship-graveyard"),
        new(3, "batareysky-waterfall"),
        new(4, "barents-sea"),
        new(5, "northern-lights"),
        new(6, "arctic-tundra"),
    ];

    public static PlaceInfo? BySlug(string slug) =>
        All.FirstOrDefault(p => string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));
}
