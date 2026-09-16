# Уменьшенные копии готовых фото сайта — для srcset.
#
#   python tools/image-variants.py <папка> --widths 640,960 [--exclude=REGEX] [--quality 80]
#
# --exclude — только через «=»: regex вида -g\d+ начинается с минуса, и argparse
# принял бы его за флаг.
#
# Рядом с каждым umba.webp кладёт umba-w640.webp, umba-w960.webp (ширины не меньше
# самого кадра пропускает — растягивать незачем). Home.razor находит копии сам
# (File.Exists) и собирает из них srcset: карточка в 340px больше не качает кадр
# в 1280px. Нет копий — страница грузит кадр целиком, ничего не ломается.
#
# Повторный запуск дешёвый: копия пересобирается, только если её нет или исходник
# новее. Заменил фото под тем же именем — просто запусти скрипт ещё раз, иначе
# на части экранов останется старый кадр.
#
# Какие папки и ширины — см. CLAUDE.md, раздел «Фото мест»; ширины обязаны быть
# из списка SrcSetWidths в Home.razor, другие страница не найдёт.
#
# Зависимости: pip install pillow
import argparse
import re
import sys
from pathlib import Path

from PIL import Image

VARIANT = re.compile(r"-w\d+$")


def main() -> int:
    ap = argparse.ArgumentParser(description="Уменьшенные копии webp для srcset")
    ap.add_argument("dir", type=Path, help="папка с готовыми webp (например wwwroot/img/places)")
    ap.add_argument("--widths", required=True, help="ширины через запятую, например 640,960")
    ap.add_argument("--exclude", help="regex по имени файла — такие не трогать (например --exclude=-g\\d+\\.webp$)")
    ap.add_argument("--quality", type=int, default=80, help="качество webp 1..100 (80)")
    args = ap.parse_args()

    widths = sorted({int(w) for w in args.widths.split(",")})
    exclude = re.compile(args.exclude) if args.exclude else None

    sources = sorted(
        p for p in args.dir.glob("*.webp")
        if not VARIANT.search(p.stem) and not (exclude and exclude.search(p.name))
    )
    if not sources:
        print(f"в {args.dir} нет подходящих webp")
        return 1

    done = skipped = 0
    for src in sources:
        with Image.open(src) as im:
            for w in widths:
                if w >= im.width:
                    continue
                dst = src.with_name(f"{src.stem}-w{w}.webp")
                if dst.exists() and dst.stat().st_mtime >= src.stat().st_mtime:
                    skipped += 1
                    continue
                small = im.convert("RGB").resize((w, round(im.height * w / im.width)), Image.LANCZOS)
                small.save(dst, "WEBP", quality=args.quality, method=6)
                done += 1
                print(f"{src.name} -> {dst.name} ({small.width}x{small.height}, {dst.stat().st_size // 1024} KB)")

    print(f"\nготово: {done}, актуальны: {skipped}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
