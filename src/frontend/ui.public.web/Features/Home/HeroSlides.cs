namespace UI.Public.Web.Features.Home;

/// <summary>
/// Слайды полосы «Что вас ждёт» на главной: файл в wwwroot/img и ключ alt-текста в resx
/// (видимых подписей нет — убраны владельцем 24.08.2026, alt остаётся для SEO и скринридеров).
/// Фото — собственные снимки владельца (оригиналы в src/frontend/temp, под .gitignore;
/// кроп 5:4 1500×1200 — tools/convert-photos.py с --aspect 5:4, CSS кропает их в ленту).
/// <see cref="Slide.Portrait"/> — вертикальный кадр (файл 4:5, 1200×1500): в широкой ленте
/// он показывается целиком (object-fit: contain), а поля по бокам заливает размытая копия
/// того же кадра — иначе от 9:16-оригинала в полосе 21:9 оставалась узкая середина.
/// Кадры лежат в хранилище (префикс hero/, имена — как здесь) и попадают на сайт
/// без пересборки; если хранилище выключено или файла в нём нет, Home.razor берёт
/// одноимённый файл из wwwroot/img, а когда нет и его — показывает placeholder.svg.
/// Порядок — решение владельца 24.08.2026: зима → «обед из моря» → лето.
/// </summary>
public static class HeroSlides
{
    public sealed record Slide(string ImagePath, string AltKey, bool Portrait = false)
    {
        /// <summary>Имя файла без каталога — под ним же кадр лежит в хранилище, в префиксе hero/.</summary>
        public string FileName => ImagePath[(ImagePath.LastIndexOf('/') + 1)..];
    }

    public static readonly Slide[] All =
    [
        // зима
        new("/img/hero-winter-sun.webp", "HeroSlide5Alt"),
        new("/img/hero-winter-ship.webp", "HeroSlide7Alt"),
        new("/img/hero-winter-bay.webp", "HeroSlide13Alt"),
        new("/img/hero-winter-surf.webp", "HeroSlide11Alt"),
        new("/img/hero-winter-blizzard.webp", "HeroSlide12Alt"),
        new("/img/hero-wind.webp", "HeroSlide6Alt"),
        // обед из моря — вертикальные кадры
        new("/img/hero-seafood.webp", "HeroSlide10Alt", Portrait: true),
        new("/img/hero-urchin.webp", "HeroSlide8Alt", Portrait: true),
        new("/img/hero-shell.webp", "HeroSlide9Alt", Portrait: true),
        // лето
        new("/img/hero-edge.webp", "HeroSlide1Alt"),
        new("/img/hero-bay.webp", "HeroSlide2Alt"),
        new("/img/hero-ships.webp", "HeroSlide3Alt"),
        new("/img/hero-gorge.webp", "HeroSlide4Alt"),
    ];
}
