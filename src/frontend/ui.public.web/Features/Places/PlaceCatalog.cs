namespace UI.Public.Web.Features.Places;

/// <summary>
/// Каталог мест экскурсий. Это контент сайта, а не данные, поэтому БД не используется:
/// slug задаёт URL страницы места, Index — номер ключей в resx
/// (Place{Index}Title / Place{Index}Text / Place{Index}Detail1 / Place{Index}Detail2).
/// Новое место = строка здесь + ключи в трёх resx.
/// </summary>
public sealed record PlaceInfo(int Index, string Slug);

/// <summary>
/// Направление объединяет места одного маршрута. Index — номер ключей
/// Dir{Index}Title / Dir{Index}Text в resx; на карте Кольского каждому
/// направлению соответствует кликабельный маркер.
/// </summary>
public sealed record Direction(int Index, string Key, int[] PlaceIndexes);

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
        new(7, "lovozero-tundras"),
        new(8, "geologists-pass"),
        new(9, "tersky-coast"),
        new(10, "umba"),
        new(11, "varzuga"),
        new(12, "stone-labyrinth"),
        new(13, "sredny-rybachy"),
    ];

    public static readonly Direction[] Directions =
    [
        new(1, "teriberka", [1, 2, 3, 4, 5, 6]),
        new(2, "tundras", [7, 8, 13]),
        new(3, "tersky", [9, 10, 11, 12]),
    ];

    public static PlaceInfo? BySlug(string slug) =>
        All.FirstOrDefault(p => string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));

    public static PlaceInfo ByIndex(int index) => All.First(p => p.Index == index);
}
