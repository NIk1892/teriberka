# Пакетная конвертация фото в webp для сайта.
#
#   python tools/convert-photos.py <папка-с-оригиналами> <папка-вывода> [--width 1600] [--quality 80] [--aspect 5:4]
#
# Берёт JPG/PNG/HEIC (HEIC — через pillow-heif), учитывает EXIF-ориентацию (снимки
# с телефона иначе лягут боком), уменьшает до --width по ширине (оригиналы 4000+ px
# на сайте не нужны), при --aspect кропает по центру под нужный аспект (5:4 — для
# полосы «Что вас ждёт», см. HeroSlides) и пишет webp. Видео, PDF и прочее пропускает,
# уже сконвертированные файлы не трогает — можно запускать повторно на пополненной папке.
#
# Зависимости: pip install pillow pillow-heif
import argparse
import sys
from pathlib import Path

from PIL import Image, ImageOps

try:
    import pillow_heif
    pillow_heif.register_heif_opener()
except ImportError:  # без pillow-heif конвертируем всё, кроме HEIC
    pass

SUPPORTED = {".jpg", ".jpeg", ".png", ".heic", ".heif", ".webp", ".tif", ".tiff"}


def parse_aspect(value: str | None) -> float | None:
    if not value:
        return None
    w, h = value.split(":")
    return float(w) / float(h)


def convert(src: Path, dst: Path, width: int, quality: int, aspect: float | None) -> str:
    with Image.open(src) as im:
        im = ImageOps.exif_transpose(im).convert("RGB")
        if aspect:
            w, h = im.size
            if w / h > aspect:  # кадр шире нужного — режем бока
                cw = round(h * aspect)
                x = (w - cw) // 2
                im = im.crop((x, 0, x + cw, h))
            else:  # кадр выше нужного — режем верх/низ
                ch = round(w / aspect)
                y = (h - ch) // 2
                im = im.crop((0, y, w, y + ch))
        if im.width > width:
            im = im.resize((width, round(im.height * width / im.width)), Image.LANCZOS)
        dst.parent.mkdir(parents=True, exist_ok=True)
        im.save(dst, "WEBP", quality=quality, method=6)
        return f"{im.width}x{im.height}"


def main() -> int:
    ap = argparse.ArgumentParser(description="Конвертация фото в webp для сайта")
    ap.add_argument("src", type=Path, help="папка с оригиналами")
    ap.add_argument("out", type=Path, help="папка для webp")
    ap.add_argument("--width", type=int, default=1600, help="максимальная ширина, px (1600)")
    ap.add_argument("--quality", type=int, default=80, help="качество webp 1..100 (80)")
    ap.add_argument("--aspect", help="кроп под аспект, например 5:4 (по умолчанию без кропа)")
    ap.add_argument("--force", action="store_true", help="перезаписывать уже готовые файлы")
    args = ap.parse_args()

    aspect = parse_aspect(args.aspect)
    files = sorted(p for p in args.src.iterdir() if p.is_file() and p.suffix.lower() in SUPPORTED)
    if not files:
        print(f"в {args.src} нет подходящих файлов ({', '.join(sorted(SUPPORTED))})")
        return 1

    done = skipped = failed = 0
    for src in files:
        dst = args.out / (src.stem.lower().replace(" ", "-") + ".webp")
        if dst.exists() and not args.force:
            skipped += 1
            continue
        try:
            size = convert(src, dst, args.width, args.quality, aspect)
            done += 1
            print(f"{src.name} -> {dst.name} ({size}, {dst.stat().st_size // 1024} KB)")
        except Exception as e:  # один битый файл не должен ронять пакет
            failed += 1
            print(f"!! {src.name}: {e}", file=sys.stderr)

    print(f"\nготово: {done}, пропущено (уже были): {skipped}, ошибок: {failed}")
    return 0 if failed == 0 else 2


if __name__ == "__main__":
    sys.exit(main())
