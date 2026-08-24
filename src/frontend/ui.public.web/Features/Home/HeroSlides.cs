namespace UI.Public.Web.Features.Home;

/// <summary>
/// Слайды полосы «Что вас ждёт» на главной: файл в wwwroot/img и ключ alt-текста в resx
/// (видимых подписей нет — убраны владельцем 24.08.2026, alt остаётся для SEO и скринридеров).
/// Фото — собственные снимки владельца (оригиналы в src/frontend/temp, под .gitignore;
/// кроп 5:4 1500×1200 — tools/convert-photos.py с --aspect 5:4, CSS кропает их в ленту).
/// Пока файла нет, Home.razor показывает заглушку placeholder.svg.
/// Порядок — решение владельца 24.08.2026: зима → «обед из моря» → лето.
/// </summary>
public static class HeroSlides
{
    public sealed record Slide(string ImagePath, string AltKey);

    public static readonly Slide[] All =
    [
        // зима
        new("/img/hero-winter-sun.webp", "HeroSlide5Alt"),
        new("/img/hero-winter-ship.webp", "HeroSlide7Alt"),
        new("/img/hero-winter-bay.webp", "HeroSlide13Alt"),
        new("/img/hero-winter-surf.webp", "HeroSlide11Alt"),
        new("/img/hero-winter-blizzard.webp", "HeroSlide12Alt"),
        new("/img/hero-wind.webp", "HeroSlide6Alt"),
        // обед из моря
        new("/img/hero-seafood.webp", "HeroSlide10Alt"),
        new("/img/hero-urchin.webp", "HeroSlide8Alt"),
        new("/img/hero-shell.webp", "HeroSlide9Alt"),
        // лето
        new("/img/hero-edge.webp", "HeroSlide1Alt"),
        new("/img/hero-bay.webp", "HeroSlide2Alt"),
        new("/img/hero-ships.webp", "HeroSlide3Alt"),
        new("/img/hero-gorge.webp", "HeroSlide4Alt"),
    ];
}
