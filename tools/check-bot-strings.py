"""Сверка resx Telegram-бота (src/backend/chat/service/Bot/Resources/BotStrings*.resx) с сайтом.

Ключи без префикса Bot скопированы в бота с сайта под теми же именами (Itinerary1Title,
Faq3Answer, Included2…) — поменялся текст в SharedResource*.resx, он должен поменяться и
здесь. Скрипт проверяет: набор ключей .en/.zh совпадает с русским; сайтовые ключи равны
сайту символ в символ; плейсхолдеры {0}/{1} в Bot*-ключах те же, что в русском; в значениях
нет < > & (тексты уходят в Telegram как HTML); лимиты длины описания бота.

    python tools/check-bot-strings.py            # 0 — всё сходится, 1 — есть расхождения
"""
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BOT = ROOT / "src/backend/chat/service/Bot/Resources"
SITE = ROOT / "src/frontend/ui.public.web/Resources"


def load(path: Path) -> dict[str, str]:
    values = {}
    for data in ET.parse(path).getroot().findall("data"):
        value = data.find("value")
        values[data.get("name")] = value.text if value is not None and value.text is not None else ""
    return values


def main() -> int:
    problems = 0
    ru = load(BOT / "BotStrings.resx")
    site_ru = load(SITE / "SharedResource.resx")

    for lang, bot_file, site_file in [
        ("ru", "BotStrings.resx", "SharedResource.resx"),
        ("en", "BotStrings.en.resx", "SharedResource.en.resx"),
        ("zh", "BotStrings.zh.resx", "SharedResource.zh.resx"),
    ]:
        bot = load(BOT / bot_file)
        site = load(SITE / site_file)

        missing = sorted(set(ru) - set(bot))
        extra = sorted(set(bot) - set(ru))
        if missing:
            print(f"[{lang}] нет ключей: {', '.join(missing)}")
            problems += 1
        if extra:
            print(f"[{lang}] лишние ключи: {', '.join(extra)}")
            problems += 1

        for key, value in bot.items():
            if not key.startswith("Bot"):
                site_value = site.get(key)
                if site_value is None:
                    print(f"[{lang}] {key}: ключа нет на сайте")
                    problems += 1
                elif site_value != value:
                    print(f"[{lang}] {key}: отличается от сайта\n    бот : {value!r}\n    сайт: {site_value!r}")
                    problems += 1
            elif key in ru:
                if sorted(re.findall(r"\{\d\}", ru[key])) != sorted(re.findall(r"\{\d\}", value)):
                    print(f"[{lang}] {key}: плейсхолдеры не совпадают с русским")
                    problems += 1
                if re.search(r"[{}]", re.sub(r"\{\d\}", "", value)):
                    print(f"[{lang}] {key}: лишняя фигурная скобка")
                    problems += 1
            if re.search(r"[<>&]", value):
                print(f"[{lang}] {key}: символы < > & недопустимы (уходит как HTML)")
                problems += 1

        for key, limit in [("BotShortDescription", 120), ("BotDescription", 512)]:
            if len(bot.get(key, "")) > limit:
                print(f"[{lang}] {key}: длиннее {limit} символов")
                problems += 1
        for key, value in bot.items():
            if key.startswith("BotCmd") and len(value) > 256:
                print(f"[{lang}] {key}: длиннее 256 символов")
                problems += 1

        print(f"[{lang}] {len(bot)} ключей проверено")

    print("OK" if problems == 0 else f"Расхождений: {problems}")
    return 0 if problems == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
