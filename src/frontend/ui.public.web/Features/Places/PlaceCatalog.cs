namespace UI.Public.Web.Features.Places;

/// <summary>
/// Каталог мест экскурсий. Это контент сайта, а не данные, поэтому БД не используется:
/// slug задаёт URL страницы места, Index — номер ключей в resx
/// (Place{Index}Title / Place{Index}Text / Place{Index}Detail1 / Place{Index}Detail2).
/// Новое место = строка здесь + ключи в трёх resx + фото wwwroot/img/places/{slug}.webp
/// (галерея — файлы {slug}-g1..gN.webp, порядок совпадает с массивом Gallery).
/// </summary>
public sealed record PlaceInfo(int Index, string Slug, PhotoCredit? Photo = null, PhotoCredit[]? Gallery = null);

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
            new("PSerov", "CC BY 4.0", "https://commons.wikimedia.org/wiki/File:PS_A2825.jpg"),
            [
                new("Konstant", "CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:Murmansk_Oblast,_Russia,_184630_-_panoramio_(6).jpg"),
                new("Konstant", "CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:Murmansk_Oblast,_Russia,_184630_-_panoramio_(7).jpg"),
                new("Vsatinet", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Stone_beach_on_the_way_to_Batareyskiy_waterfall.jpg"),
            ]),
        new(2, "ship-graveyard",
            new("Mihail Siergiejevicz", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Кладбище_кораблей_Териберки.jpg"),
            [
                new("Юрочкин Роман", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Пос.Териберка_кладбище_кораблей2.JPG"),
                new("Юрочкин Роман", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Пос.Териберка_кладбище_кораблей1.JPG"),
                new("Юрочкин Роман", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Пос.Териберка_кладбище_кораблей4.JPG"),
            ]),
        new(3, "batareysky-waterfall",
            new("Vsatinet", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Водопад_у_Малого_Батарейского_оз.jpg"),
            [
                new("Vsatinet", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Водопад_у_Малого_Батарейского.jpg"),
                new("Savvdm", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Батарейский_водопад,_Териберка.jpg"),
                new("Insider", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Батарейский_водопад.jpg"),
            ]),
        new(4, "barents-sea",
            new("Ted.ns", "CC BY 4.0", "https://commons.wikimedia.org/wiki/File:Баренцево_море_в_Териберке.jpg"),
            [
                new("Дмитрий Алтунин", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Баренцево_море_в_районе_Териберки.jpg"),
                new("Vsatinet", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Stone_beach_near_Teriberka.jpg"),
            ]),
        new(5, "northern-lights",
            new("Taksla", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Кандалакшский_залив,_северное_сияние.jpg"),
            [
                new("Taksla", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Кандалакшский_залив,_северное_сияние,_отлив.jpg"),
                new("Taksla", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Кандалакшский_залив_северное_сияние.jpg"),
                new("Ted.ns", "CC BY 4.0", "https://commons.wikimedia.org/wiki/File:Полярное_сияние_над_заброшенным_зданием_метеостанцией.jpg"),
            ]),
        new(6, "arctic-tundra",
            new("Artem Abdukakharov", "CC BY 4.0", "https://commons.wikimedia.org/wiki/File:Arctic_tundra_in_fall_colors_Kola_Peninsula_Murmansk_Russia.jpg"),
            [
                new("Artem Abdukakharov", "CC BY 4.0", "https://commons.wikimedia.org/wiki/File:Autumn_tundra_mosaic_Kola_Peninsula_Murmansk_Russia.jpg"),
                new("Ninara", "CC BY 2.0", "https://commons.wikimedia.org/wiki/File:Kola_Peninsula_(21681590148).jpg"),
                new("Ninaras", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Tundra_on_the_Kola_Peninsula,_Russia.jpg"),
            ]),
        new(7, "lovozero-tundras",
            new("Подоляк Елизавета", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Оз._Ловозеро,_вид_на_Ловозерские_тундры.JPG"),
            [
                new("Ivtorov", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Lovozero_Massif1.jpg"),
                new("Ivtorov", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:LovozeroMassif.jpg"),
                new("Daria Lobacheva", "CC BY 4.0", "https://commons.wikimedia.org/wiki/File:Ловозерские_тундры.jpg"),
            ]),
        new(8, "geologists-pass",
            new("NortEastWestSouth", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Хибины.Перевал.jpg"),
            [
                new("Александр Байдуков", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Khibiny_mountain_range._The_Lake_Bolshoy_Vudyavr.jpg"),
                new("Anak", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Перевал_Умбозерский.jpg"),
                new("Александр Байдуков", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:An_Array_Of_Khibiny.jpg"),
            ]),
        new(9, "tersky-coast",
            new("Vsatinet", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Терский_берег_между_Чаваньгой_и_Устьем_Варзуги.jpg"),
            [
                new("ArtemGolubev80", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Магия_воды.jpg"),
                new("Vsatinet", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Tersky_Coast.jpg"),
                new("Aleksandr Chazov", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Терский_берег._Скала.jpg"),
            ]),
        new(10, "umba",
            new("Konstantin Malanchev", "CC BY 2.0", "https://commons.wikimedia.org/wiki/File:Old_Umba_from_New_Umba.jpg"),
            [
                new("Вячеслав Лобанов", "CC BY 3.0", "https://commons.wikimedia.org/wiki/File:Умба._2011_г.jpg"),
                new("Ted.ns", "CC BY 4.0", "https://commons.wikimedia.org/wiki/File:Пороги_на_Умбе.jpg"),
                new("Konstantin Malanchev", "CC BY 2.0", "https://commons.wikimedia.org/wiki/File:Street_in_Umba.jpg"),
            ]),
        new(11, "varzuga",
            new("Krytsyn Vlad", "CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:Варзуга.jpg"),
            [
                new("Vsatinet", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Переправа_в_Устье_Варзуги.jpg"),
                new("Vsatinet", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:05_Большая_Варзуга.jpg"),
                new("Vsatinet", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:08_Большая_Варзуга,_Падун.jpg"),
            ]),
        new(12, "stone-labyrinth",
            new("Serge Kolyzhev", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Каменный_лабиринт_%22Вавилон%22.jpg"),
            [
                new("Lena.criukova", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Лабиринт_каменный.jpg"),
                new("Digr", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:RU_Kandalaksha_Vavilon.jpg"),
            ]),
        new(13, "sredny-rybachy",
            new("Vasily Iakovlev", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Мыс_Кекурский,_Полуостров_Рыбачий.jpg"),
            [
                new("RostislavMashin", "CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Kekurskiy_MashinRostislav.jpg"),
                new("Sergey Gruzdev", "CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:Бухта_Озерко,_губа_Бол._Мотка.jpg"),
                new("Вячеслав Ребров", "CC BY 3.0", "https://commons.wikimedia.org/wiki/File:Полуостров_Рыбачий_-_panoramio.jpg"),
            ]),
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
