namespace UI.Public.Web.Features.Places;

/// <summary>
/// Каталог мест экскурсий. Это контент сайта, а не данные, поэтому БД не используется:
/// slug задаёт URL страницы места, Index — номер ключей в resx
/// (Place{Index}Title / Place{Index}Text / Place{Index}Detail1 / Place{Index}Detail2).
/// Новое место = строка здесь + ключи в трёх resx + фото wwwroot/img/places/{slug}.webp.
/// </summary>
public sealed record PlaceInfo(int Index, string Slug, PhotoCredit? Photo = null);

/// <summary>
/// Атрибуция фотографии места. Фото взяты с Wikimedia Commons под лицензиями CC,
/// которые ТРЕБУЮТ указания автора — подпись на странице места удалять нельзя.
/// </summary>
public sealed record PhotoCredit(string Author, string License, string SourceUrl);

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
        new(1, "dragon-eggs",
            new("PSerov", "CC BY 4.0", "https://commons.wikimedia.org/wiki/File:PS_A2825.jpg")),
        new(2, "ship-graveyard",
            new("Mihail Siergiejevicz", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Кладбище_кораблей_Териберки.jpg")),
        new(3, "batareysky-waterfall",
            new("Vsatinet", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Водопад_у_Малого_Батарейского_оз.jpg")),
        new(4, "barents-sea",
            new("Ted.ns", "CC BY 4.0", "https://commons.wikimedia.org/wiki/File:Баренцево_море_в_Териберке.jpg")),
        new(5, "northern-lights",
            new("Taksla", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Кандалакшский_залив,_северное_сияние.jpg")),
        new(6, "arctic-tundra",
            new("Artem Abdukakharov", "CC BY 4.0", "https://commons.wikimedia.org/wiki/File:Arctic_tundra_in_fall_colors_Kola_Peninsula_Murmansk_Russia.jpg")),
        new(7, "lovozero-tundras",
            new("Подоляк Елизавета", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Оз._Ловозеро,_вид_на_Ловозерские_тундры.JPG")),
        new(8, "geologists-pass",
            new("NortEastWestSouth", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Хибины.Перевал.jpg")),
        new(9, "tersky-coast",
            new("Vsatinet", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Терский_берег_между_Чаваньгой_и_Устьем_Варзуги.jpg")),
        new(10, "umba",
            new("Konstantin Malanchev", "CC BY 2.0", "https://commons.wikimedia.org/wiki/File:Old_Umba_from_New_Umba.jpg")),
        new(11, "varzuga",
            new("Krytsyn Vlad", "CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:Варзуга.jpg")),
        new(12, "stone-labyrinth",
            new("Serge Kolyzhev", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Каменный_лабиринт_%22Вавилон%22.jpg")),
        new(13, "sredny-rybachy",
            new("Vasily Iakovlev", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Мыс_Кекурский,_Полуостров_Рыбачий.jpg")),
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
