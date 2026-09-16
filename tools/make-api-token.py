"""Служебный JWT (API_TOKEN) для страницы заявок /manager.

Страница берёт заявки у шлюза, а admin-маршруты шлюза закрыты политикой Admin (роль 1/2).
Токен подписывается тем же ключом, что проверяет шлюз, — JWT_KEY как UTF-8 строка, HS256,
issuer — JWT_ISSUER шлюза. Роль — Admin (1), не SuperAdmin. Ключ и issuer брать из окружения
работающего шлюза, а не из compose-файлов: прод-оверлей их переопределяет (на бою issuer —
https://teriberka-kray.ru, локально — http://localhost:5000).

    python tools/make-api-token.py "$JWT_KEY" "$JWT_ISSUER" 3   # срок в годах

Токен даёт чтение и отметку заявок, но пользоваться им можно только изнутри сети compose:
шлюз наружу не опубликован. Сменили JWT_KEY — выпустить токен заново.
"""
import base64, hashlib, hmac, json, sys, time


def b64(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode()


key, issuer = sys.argv[1], sys.argv[2]
years = int(sys.argv[3]) if len(sys.argv) > 3 else 3
now = int(time.time())
header = {"alg": "HS256", "typ": "JWT"}
payload = {"iss": issuer, "sub": "ui-manager-page", "name": "UI manager page", "role": "1",
           "iat": now, "nbf": now, "exp": now + years * 365 * 24 * 3600}
signing_input = b64(json.dumps(header, separators=(",", ":")).encode()) + "." + b64(json.dumps(payload, separators=(",", ":")).encode())
signature = hmac.new(key.encode("utf-8"), signing_input.encode(), hashlib.sha256).digest()
print(signing_input + "." + b64(signature))
