# Развёртывание на сервере

Боевой сайт — `https://teriberka-kray.ru`. Стек тот же, что локально, но поднимается
с прод-оверлеем: `docker-compose.prod.yml` включает `Production` у всех .NET-сервисов
(это выключает `/dev/token` и Swagger), подставляет секреты из `.env`, отдаёт nginx
TLS-конфиг и ограничивает рост docker-логов.

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
```

Команда длинная и нужна каждый раз — на сервере удобно завести алиас:
`alias dc='docker compose -f /opt/teriberka/docker-compose.yml -f /opt/teriberka/docker-compose.prod.yml'`.

## Что где лежит

| Что | Где |
|---|---|
| Код и compose-файлы | `/opt/teriberka` |
| Секреты (`.env`) | `/opt/teriberka/.env` — **не в git**, при переустановке восстанавливается руками |
| Сертификат | `/opt/teriberka/certs/{fullchain.pem,privkey.pem}` — **не в git** |
| База | том `db_volume` |
| Фото | том `minio_volume` — **единственная копия**, бэкапов пока нет |

## Первая установка

1. Firewall: наружу только 22, 80, 443. Порты 5000/5012/5013/5432/9001 в compose
   привязаны к `127.0.0.1` и снаружи недоступны — так и должно быть: users и chat
   никого не авторизуют и доверяют вызывающему.
2. Swap. Сервер — 2 vCPU / 2 ГБ, и памяти не хватает не сайту, а **сборке**:
   `dotnet publish` держит около гигабайта на проект. Без swap первая же сборка
   упирается в OOM.

   ```bash
   fallocate -l 4G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
   echo '/swapfile none swap sw 0 0' >> /etc/fstab      # чтобы пережил перезагрузку
   ```

3. Код: `git archive --format=tar.gz -o teriberka.tar.gz HEAD` на машине разработчика,
   `scp` на сервер, распаковать в `/opt/teriberka`. Именно `git archive`, а не `zip`
   рабочего каталога: он берёт версионированное состояние и сохраняет LF в `.sh`
   (с CRLF `init-db.sh` не запустится — `\r` попадёт в shebang).
4. `.env` по образцу `.env.example`. Пароли — `openssl rand -hex 32`
   (base64 не годится: `=` в пароле ломает строку подключения Postgres).
   `POSTGRES_PASSWORD` задать **до первого старта** — на уже созданном томе
   он молча игнорируется.
5. Сертификат — см. ниже.
6. `docker compose -f docker-compose.yml -f docker-compose.prod.yml config` — сверить,
   что подставилось, и только потом `up -d --build`.

## Сертификат

Сертификат коммерческий (reg.ru), выпускается на год, автопродления нет.
**Приватный ключ reg.ru отдаёт один раз, в момент выпуска, и больше не показывает** —
потеряли ключ, значит нужно переиздание.

Из письма нужны два файла: сертификат домена и промежуточный. Корневой не нужен
(браузеры ему и так доверяют), CSR после выпуска не нужен вовсе.

```bash
cat certificate.crt intermediate.crt > fullchain.pem   # порядок важен: домен, потом промежуточный
cp private.key privkey.pem
chmod 600 privkey.pem
```

Перед установкой три проверки:

```bash
# ключ подходит к сертификату — хэши должны совпасть
openssl x509 -noout -modulus -in fullchain.pem | openssl md5
openssl rsa  -noout -modulus -in privkey.pem   | openssl md5

# ключ не под паролем: BEGIN ENCRYPTED PRIVATE KEY => nginx не стартует без ssl_password_file
head -1 privkey.pem

# какие имена покрывает и до какого числа
openssl x509 -in fullchain.pem -noout -text | grep -A1 "Subject Alternative Name"
openssl x509 -in fullchain.pem -noout -dates
```

Если сертификат покрывает только голый домен — в `.env` оставить `SITE_DOMAIN`
с одним именем. Если покрывает и `www` — перечислить оба **через пробел**
(формат `server_name` у nginx), редирект `www` → apex конфиг сделает сам.

**Продление** (напоминание за месяц до даты из `-dates`): переиздать в панели reg.ru,
**сразу сохранить новый `private.key`**, заменить оба файла в `certs`, затем
`docker compose ... exec nginx nginx -s reload` — перезапуск контейнера не нужен.

HSTS в конфиге намеренно начинается с `max-age=300`. Через несколько дней стабильной
работы поднять до `31536000` в `infra/nginx/templates.tls/default.conf.template`
(два места: блок `server` и блок `location /media`) и перезагрузить nginx. Раньше
времени не поднимать: пока срок маленький, ошибку в сертификате ещё можно откатить.

## Капча

Форма заявки закрыта невидимой Яндекс SmartCaptcha. Она включается **парой** ключей;
половина пары = капча выключена целиком (с warning'ом в логе), и форма работает как
раньше — сайт не падает, но и не защищён.

- `SMARTCAPTCHA_CLIENT_KEY` — публичный, лежит дефолтом в compose, трогать не нужно.
- `SMARTCAPTCHA_SERVER_KEY` — **секрет, только в `.env` на сервере**. Без него защиты нет.

**Домен нужно разрешить в консоли Yandex Cloud** (SmartCaptcha → капча → список
доменов): добавить `teriberka-kray.ru` и `www.teriberka-kray.ru`. Пока там только
локальный адрес, виджет на проде будет отказывать — а поскольку без токена заявка
не отправляется, форма перестанет работать совсем. Это первое, что нужно проверить
после запуска, и проверять именно в браузере: в логах такой отказ не виден.

**Серверу нужен исходящий HTTPS** к `smartcaptcha.cloud.yandex.ru`: токен проверяется
с бэкенда. Если исходящие соединения закрыты, каждая заявка будет ждать таймаут 4 с
и проходить без проверки (fail-open — сознательное решение: сбой Яндекса не должен
терять заявки, но с наглухо закрытым фаерволом это тихо выключает капчу).

С включённой капчей **заявку нельзя отправить без JavaScript** — токен собирает
скрипт. Это осознанное изменение от 28.08.2026, а не регресс.

## Обновление сайта

```bash
# на машине разработчика
git archive --format=tar.gz -o teriberka.tar.gz HEAD
scp teriberka.tar.gz user@server:/opt/teriberka/

# на сервере
cd /opt/teriberka && tar xzf teriberka.tar.gz
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
```

`.env` и `certs/` распаковка не затрагивает — они не входят в архив.

**Сборка на этом сервере — по одному сервису.** `docker compose build` по умолчанию
собирает сервисы параллельно, а четыре одновременных `dotnet publish` на 2 ГБ кладут
машину даже со swap (лимиты памяти из `docker-compose.prod.yml` тут не работают:
сборка идёт в buildkit, мимо контейнеров):

```bash
for svc in api-users api-chat api-gateway ui-public; do
  docker compose ... build "$svc" || break
done
docker compose ... up -d
```

Именно цикл, а не `build` со списком сервисов: список compose раскладывает по
параллельным задачам, и ограничить их числом одной переменной окружения
надёжно не получается.

Диск (40 ГБ) со временем забивает кэш сборки — `docker builder prune -f` и
`docker image prune -f` раз в несколько деплоев. Проверять `docker system df`.

Альтернатива, если сборка на сервере надоест: собирать образы на своей машине,
`docker save` → `scp` → `docker load`. Дольше по сети, но сервер не занят.

Правка только конфига nginx (шаблон монтируется с диска, образ пересобирать не нужно):
`docker compose ... up -d --force-recreate nginx`.

Изменения в контрактах требуют пересборки того сервиса, который их использует,
причём именно `--force-recreate`: `--build` без него пересобирает образ, но
оставляет работать старый контейнер.

## Наблюдение

```bash
docker compose ... ps
docker compose ... logs -f ui-public          # сайт
docker compose ... logs -f api-chat           # чат и Telegram-бот
docker compose ... exec nginx nginx -t        # проверить конфиг перед reload
```

Заявки (Telegram-бот их пока не читает):

```bash
docker compose ... exec postgres psql -U postgres -d platform \
  -c 'select "Title", "Phone", "Route", "Audit_CreatedAt" from users."Applications" where not "IsDeleted" order by "Audit_CreatedAt" desc limit 20'
```

Консоль MinIO наружу не публикуется никогда — только через туннель:
`ssh -L 9001:127.0.0.1:9001 user@server`, дальше `http://localhost:9001`.

## Чего ещё нет

- **Бэкапов.** Ни базы, ни фото. Том `minio_volume` уже терялся при перестроении
  compose, а это единственная копия загруженных кадров.
- **Автопродления сертификата** — раз в год руками (см. выше).
- **Telegram-бота.** `TG_BOT_TOKEN` и `TG_ADMIN_CHAT_ID` пусты: чат на сайте работает
  и сохраняет переписку, но гидам ничего не приходит. Ссылку на бота прод-оверлей
  прячет (`TG_BOT_URL: ""`) — появится бот, вписать username туда же.
- **Метрики.** OTLP выключен, логи живут только в docker (10 МБ × 5 файлов на сервис).
