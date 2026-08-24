# Раздел «Галерея»: MinIO + Caddy + страница `/gallery`

> **Статус: черновик, не реализовано.** План составлен 24.08.2026.
> Пока раздел не сделан, это руководство к работе. После реализации решения, которые
> останутся верными и дальше (почему тот же домен, почему `USE_FORWARDED_HEADERS`
> связан с закрытием порта, правила именования файлов), переезжают в
> [CLAUDE.md](../CLAUDE.md) — как это уже сделано для чата и бота, — а этот файл
> либо удаляется, либо получает пометку «выполнено».

## Зачем

У владельца накопились фотографии из туров, и нужен раздел «Галерея». Класть их в `wwwroot/img/` нельзя: сейчас там уже 50 файлов на ~11 МБ, а с галереей на 50–200 кадров каждое новое фото означало бы коммит, пересборку образа и деплой сайта.

Решение: поднять MinIO в docker compose и заливать фото через его веб-консоль, без ssh и без пересборки. Фото должны отдаваться **с того же домена** по пути `/media/...` — отдельный поддомен потребовал бы ослабить CSP (`img-src 'self' data:`) и завести второй сертификат, а выигрыша не даёт. Для этого в compose появляется reverse proxy (Caddy) — которого в проекте нет вообще: сейчас UI, шлюз, users, chat и Postgres опубликованы портами напрямую, TLS не настроен.

Сайт сам читает содержимое бакета и кэширует список: залил файл → через несколько минут он на странице, ничего править не надо. Миниатюры генерируются заранее, локально, скриптом — сервис-ресайзер не заводим.

Результат: `/gallery` со всеми фото, лайтбокс, полностью работающий вход по HTTPS на одном домене.

---

## Этап 0. Санитарные проверки (до написания кода)

**0.1. Починить Dockerfile UI — сборка образа сейчас сломана.**
[UI.Public.Web.csproj:18](../src/frontend/ui.public.web/UI.Public.Web.csproj#L18) ссылается на `Chat.Contracts.csproj`, но [Dockerfile](../src/frontend/ui.public.web/Dockerfile#L7-L12) его не копирует перед `dotnet restore` — restore падает. Добавить строку рядом с остальными `COPY`:
```dockerfile
COPY ["src/contracts/Chat.Contracts/Chat.Contracts.csproj", "contracts/Chat.Contracts/"]
```
Проверка: `docker compose build ui-public`.

**0.2. Проверить веб-консоль MinIO — от этого зависит смысл всей затеи.**
В свежих community-релизах MinIO объектный браузер из консоли вырезан, остаются только логин и управление ключами. Заливка фото мышкой — единственная причина брать MinIO вместо папки на диске. Поднять образ руками, зайти на `:9001`, **загрузить один файл**:
```powershell
docker run --rm -p 9001:9001 -e MINIO_ROOT_USER=admin -e MINIO_ROOT_PASSWORD=admin12345 `
  minio/minio:RELEASE.2025-04-22T22-12-26Z server /data --console-address ":9001"
```
- Браузер объектов есть → идём по плану, тег образа пинуем и в CLAUDE.md пишем «не обновлять вслепую».
- Браузера нет → остановиться и обсудить: либо отдельный S3-браузер поверх того же бакета, либо запасной вариант «общий том + `filebrowser/filebrowser` + `file_server` в Caddy», который закрывает ту же потребность без S3 SDK, политик и ключей.

---

## Этап 1. Инфраструктура

### Новые файлы
```
infra/caddy/Caddyfile
infra/minio/init.sh
infra/minio/policy-public-read.json     # анонимам: только s3:GetObject на gallery/*
infra/minio/policy-ui-read.json         # сайту: ListBucket(prefix=gallery/) + GetObject
.env.example                            # .env уже в .gitignore
tools/gallery/Convert-GalleryPhotos.ps1
tools/gallery/Test-GalleryBucket.ps1
```

### [docker-compose.yml](../docker-compose.yml)

**`minio`** — образ пиновать (`minio/minio:RELEASE.2025-04-22T22-12-26Z`), `command: server /data --console-address ":9001"`, том `minio_volume:/data`, `healthcheck` через `mc ready local`. Порт 9000 (S3 API) **не публиковать вообще** — только `expose`. Консоль — `127.0.0.1:9001:9001`, наружу никогда; доступ снаружи через `ssh -L 9001:127.0.0.1:9001`. Креденшелы — через `${MINIO_ROOT_PASSWORD:?...}`, чтобы compose падал с внятной ошибкой, а не поднимался с `minioadmin/minioadmin`. `MINIO_DOMAIN` не задавать (virtual-host бакеты не нужны).

**`minio-init`** — одноразовый `minio/mc`, `depends_on: minio: condition: service_healthy` (без healthcheck `depends_on` ничего не гарантирует), `entrypoint` на `/init/init.sh`, скрипт идемпотентный. Последовательность:
```sh
mc alias set local http://minio:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD"
mc mb --ignore-existing "local/$MEDIA_BUCKET"
mc anonymous set-json /tmp/public-read.json "local/$MEDIA_BUCKET"   # НЕ `set download`
mc admin policy create local gallery-read /tmp/ui-read.json || mc admin policy update ...
mc admin user add local "$MEDIA_ACCESS_KEY" "$MEDIA_SECRET_KEY" || true
mc admin policy attach local gallery-read --user "$MEDIA_ACCESS_KEY" || true
```
`mc anonymous set download` в части версий добавляет `s3:ListBucket` — то есть открывает перечисление бакета. Только явный `set-json` c единственным `s3:GetObject` на `arn:aws:s3:::__BUCKET__/gallery/*`.

**`caddy`** — `caddy:2.10-alpine`, порты `80`, `443`, `443/udp`, конфиг read-only bind, **именованные тома `caddy_data` и `caddy_config`** (в `caddy_data` живут сертификаты; потеря = переиздание и лимит Let's Encrypt 5 неудач/час).

**Правки существующих сервисов:**
- `ui-public`: убрать `ports: 7002:8080`, оставить `expose`; добавить `USE_FORWARDED_HEADERS: "true"` и ключи `MEDIA_*` (см. этап 3).
- `postgres`, `api-gateway`, `api-users`, `api-chat`: префикс `127.0.0.1:` у портов. Postgres сейчас слушает `0.0.0.0:5432` с паролем `postgres` — до публичного домена это некрасиво, после — дыра.
- Всем: `restart: unless-stopped` (сейчас нет ни у кого — после перезагрузки сервера сайт не поднимется).

### `infra/caddy/Caddyfile`
```caddyfile
{
	email {$TLS_EMAIL}
	# первую настройку прода делать со staging-CA, чтобы не сжечь лимит неудач:
	# acme_ca https://acme-staging-v02.api.letsencrypt.org/directory
}

{$SITE_ADDRESS} {
	encode {
		zstd
		gzip
		match {                          # webp уже сжат кодеком — не тратим CPU
			header Content-Type text/*
			header Content-Type application/json*
			header Content-Type application/xml*
			header Content-Type image/svg+xml*
		}
	}

	# HSTS только по реальному TLS: безусловный заголовок на http://localhost
	# заставит браузер запомнить localhost как https-only и сломает все
	# локальные проекты на машине (лечится только через chrome://net-internals/#hsts)
	@secure protocol https
	header @secure Strict-Transport-Security "max-age=31536000; includeSubDomains"

	handle_path /media/* {
		@photo {
			method GET HEAD
			path_regexp \.webp$
		}
		handle @photo {
			rewrite * /{$MEDIA_BUCKET}{path}      # {path}, НЕ {uri}
			reverse_proxy minio:9000 {
				header_up Host {upstream_hostport}
			}
			header {
				Cache-Control "public, max-age={$MEDIA_MAX_AGE}"
				Content-Type "image/webp"
				X-Content-Type-Options "nosniff"
				Content-Security-Policy "default-src 'none'; sandbox"
				Referrer-Policy "no-referrer"
				-Server
				-x-amz-request-id
				-x-amz-id-2
				-x-minio-deployment-id
				-Vary
			}
		}
		handle { respond 404 }
	}

	handle { reverse_proxy ui-public:8080 }
}
```

Ключевые моменты:
- **`{path}`, а не `{uri}`** — `{uri}` тащит query, и открывается `GET /media/?list-type=2`, то есть весь S3 API наружу.
- Наружу пускается ровно один сценарий: `GET/HEAD` на `.webp`. PUT/DELETE, `.svg`, `.html`, presigned-параметры — 404.
- `Content-Type: image/webp` принудительно: при заливке некоторыми клиентами объект получает `application/octet-stream`, и браузер предлагает скачать файл вместо показа.
- `immutable` **не ставим**: заливка ручная, рано или поздно файл перезальют под тем же именем, и `immutable` спрятал бы правку на 30 дней даже по Ctrl+F5. Правило «новое фото = новое имя» — в документацию.
- `ETag`/`Last-Modified` от MinIO сохраняются → повторный заход даёт 304.

### `.env.example`
`SITE_ADDRESS` (dev `:80`, prod — домен), `SITE_URL`, `TLS_EMAIL`, `MEDIA_BUCKET=media`, `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD`, `MEDIA_ACCESS_KEY`, `MEDIA_SECRET_KEY`, `MEDIA_MAX_AGE=2592000`. Реальные значения — только в `.env`.

---

## Этап 2. Раскладка бакета и подготовка фото

```
media/gallery/full/010-teriberka-more.webp     ← лайтбокс, длинная сторона 1920
media/gallery/thumb/010-teriberka-more.webp    ← сетка, ровно 800×600
```

Имя: `^\d{3}-[a-z0-9-]+\.webp$`.
- **Трёхзначный номер с шагом 10** — `ListObjectsV2` отдаёт ключи лексикографически, без zero-padding `10-` встанет перед `9-`; шаг 10 позволяет вставлять фото без переименований.
- Только нижний регистр, `a-z0-9-`: ключи S3 регистрочувствительны, кириллица и пробелы дают percent-encoding и «файл есть, а не показывается».
- Только `.webp`, пара `full` + `thumb` обязательна.
- **Средняя часть имени = slug места** из [PlaceCatalog](../src/frontend/ui.public.web/Features/Places/PlaceCatalog.cs) (хвостовой `-2`/`-3` отбрасывается). Совпало → `alt` берётся из `L["Place{Index}Title"]`, то есть локализован на трёх языках бесплатно. Не совпало → общий `GalleryPhotoAlt`. Метаданные объекта (`x-amz-meta-*`) не используем — вручную их никто не заполнит.

### `tools/gallery/Convert-GalleryPhotos.ps1`
Инструмент — **ImageMagick 7 (`magick`), не `cwebp`**: `cwebp` игнорирует EXIF-ориентацию (все вертикальные кадры с телефона лягут на бок), не умеет cover-кроп, и не вырезает GPS-координаты из EXIF.
```powershell
magick $src -auto-orient -strip -resize "1920x1920>" -quality 82 -define webp:method=6 $full
magick $src -auto-orient -strip -resize "800x600^" -gravity $Gravity -extent 800x600 `
       -quality 78 -define webp:method=6 $thumb
```
Параметры: `-Source`, `-Out` (по умолчанию `./out`), `-StartIndex 10`, `-Step 10`, `-Slug`/`-SlugMap`, `-Gravity` (по умолчанию `center`; для портретных пейзажей часто лучше `north`), `-DryRun`. Раскладка вывода — `out/gallery/full` и `out/gallery/thumb`, чтобы в консоли MinIO сработал один **Upload Folder**. Валидация имён до записи + отчёт: обработано, суммарный вес, файлы тяжелее 400 КБ, неизвестные слуги. Ориентир по весу: full 150–300 КБ, thumb 25–45 КБ.

`tools/gallery/Test-GalleryBucket.ps1` — разбор `mc ls --recursive local/media/gallery`: непарные full/thumb, не-webp, имена не по регэкспу, дубли номеров. Те же проверки, что делает сайт, но с внятным сообщением до похода в логи.

**Инструкция владельцу** (в CLAUDE.md): прогнать скрипт → `ssh -L 9001:127.0.0.1:9001 user@server` → `http://localhost:9001` → бакет `media` → Upload Folder → выбрать `out/gallery` → через ≤5 минут фото на сайте.

---

## Этап 3. UI

### Пакет
`AWSSDK.S3` в [Directory.Packages.props](../Directory.Packages.props) + `PackageReference` без версии в [UI.Public.Web.csproj](../src/frontend/ui.public.web/UI.Public.Web.csproj) (central package management). Выбран вместо `Minio` ради переносимости: если MinIO не подойдёт, переезд на Yandex Object Storage / Selectel — это три строки конфига.

### Ключи конфигурации (плоские UPPER_SNAKE_CASE, как весь проект)
| Ключ | Значение |
|---|---|
| `MEDIA_ENDPOINT` | `http://minio:9000`; **пусто → раздел выключен целиком** (404, нет пунктов меню, нет в sitemap) — идиома `TG_BOT_URL`/`MAX_URL` |
| `MEDIA_BUCKET` / `MEDIA_PREFIX` | `media` / `gallery/` |
| `MEDIA_PUBLIC_PATH` | `/media` — из него строятся `src`; эндпоинт MinIO в разметку не попадает никогда |
| `MEDIA_ACCESS_KEY` / `MEDIA_SECRET_KEY` | учётка `gallery-read`. Root-креденшелы в UI класть нельзя |
| `MEDIA_REFRESH_MINUTES` | `5` — он же «через сколько фото появится на сайте» |

В [appsettings.json](../src/frontend/ui.public.web/appsettings.json) добавить пустыми, чтобы `dotnet run` без докера просто не показывал раздел.

### `Features/Gallery/`
- `GalleryPhoto.cs` — `record GalleryPhoto(string Name, string ThumbUrl, string FullUrl, string? PlaceSlug)`.
- `GalleryCatalog.cs` — singleton рядом с `SeoUrls` ([Program.cs:45](../src/frontend/ui.public.web/Program.cs#L45)). Снимок держится в `volatile` поле, а не в `IMemoryCache`: чтение на пути рендера синхронное и без блокировок (static SSR).
- `GalleryRefresher : BackgroundService` — обновляет раз в `MEDIA_REFRESH_MINUTES`, первый прогон при старте. Ни один запрос посетителя не платит за поход в MinIO.
- **Отказоустойчивость**: `RefreshAsync` целиком в try/catch, `LogWarning`, старый снимок остаётся жить. MinIO упал → страница показывает прошлый список; снимка ещё не было → пустой список и ветка-заглушка. 500 не бывает никогда.
- Клиент: `AmazonS3Client(new BasicAWSCredentials(...), new AmazonS3Config { ServiceURL, ForcePathStyle = true, AuthenticationRegion = "us-east-1", Timeout = 5s, MaxErrorRetry = 1 })`. **`ForcePathStyle = true` обязателен** — иначе SDK пойдёт на `http://media.minio:9000`, которого не существует. Явные `BasicAWSCredentials` — иначе цепочка резолва может уйти в IMDS-таймаут.
- Сборка списка: один проход по `gallery/`, фото попадает в результат только если ключ `.webp`, имя матчит регэксп, `Size > 0` (отсекает `.keep`) и **есть пара full+thumb**. Отброшенные — `LogInformation` со списком имён. Сортировка `StringComparer.Ordinal`.
- Цикл по `ContinuationToken` с потолком (~10 страниц) и `LogWarning` при упоре: `ListObjectsV2` отдаёт ≤1000 ключей, при 200 фото (~400 объектов) не проблема, но через год галерея молча обрежется.
- `IsEnabled` = `MEDIA_ENDPOINT` не пуст.

### `Features/Gallery/Pages/GalleryView.razor`
`@page "/gallery"`, `@layout MainLayout`, `<PageTitle>` + `<PageMeta Path="/gallery">` (контракт проекта — каждая индексируемая страница обязана его рендерить), `<JsonLd>` типа `ImageGallery` с `name`/`description`/`url` — **без массива `ImageObject`** на 200 элементов.

- Сетка плиток, каждая — `<a class="photo-tile lightbox-link" href="{FullUrl}" target="_blank" rel="noopener">` с `<img src="{ThumbUrl}" width="800" height="600">`. Лайтбокс подхватится сам: [lightbox.js](../src/frontend/ui.public.web/wwwroot/js/lightbox.js) цепляется к глобальному `a.lightbox-link`, править его не нужно; без JS клик открывает полноразмерный webp в новой вкладке.
- `width`/`height` честные (скрипт режет thumb ровно в 800×600) → CLS = 0. Первые 6 плиток `loading="eager"`, первые 3 — `fetchpriority="high"`: `lazy` на первом экране портит LCP.
- Пустой список → ветка-заглушка с `/img/placeholder.svg` и `PhotoSoon` (как у места без галереи).
- `if (!Gallery.IsEnabled) Navigation.NotFound();` в `OnInitialized` — именно `NotFound()`, ручной `Response.StatusCode = 404` в static SSR отдаёт 404 с пустым телом.
- **Пагинация не нужна.** 200 плиток = ~36 КБ HTML, `loading="lazy"` грузит только видимые ~12 (~0,4 МБ), запросы идут мимо Kestrel. Порог для возврата к вопросу — ~400 фото, тогда `?page=N` обычными ссылками (`@onclick` в static SSR мёртв).

### CSS — [app.css](../src/frontend/ui.public.web/wwwroot/css/app.css), новая секция около строки 1720
**Новые классы `.photo-grid` / `.photo-tile`, а не `.gallery-grid` / `.gallery-item`.** [app.css:1751-1753](../src/frontend/ui.public.web/wwwroot/css/app.css#L1751-L1753) безусловно красит `nth-child(2|3|4) img` фильтрами `hue-rotate` — переиспользование покрасит 2-е, 3-е и 4-е реальные фото. Сетка `repeat(auto-fill, minmax(220px, 1fr))`, `aspect-ratio: 4/3`, `object-fit: cover`, на ≤560px — 2 колонки. Ховер анимирует только `transform` (правило проекта) + ветка `prefers-reduced-motion`.

### resx (три файла, одинаковый порядок)
Новые: `NavGallery`, `GalleryPageTitle`, `GalleryPageSubtitle`, `GalleryPageDescription` (отдельный от subtitle), `GalleryPhotoAlt`. Готовых хватает: `PhotoAlt`, `PhotoSoon`, `Brand`, `GalleryTitle` (остаётся заголовком секции на странице места).

### Меню и SEO
- [Header.razor:66-71](../src/frontend/ui.public.web/Components/Header.razor#L66-L71) `.burger-panel` — добавить безусловно.
- [Header.razor:18-24](../src/frontend/ui.public.web/Components/Header.razor#L18-L24) `.main-nav` — **осторожно**: там уже 5 ссылок + кнопка «Записаться», и шапка на 900–1024px впритык. Добавить, снять скриншоты на 900/1024/1280, проверить `document.documentElement.scrollWidth`; если тесно — убрать из десктопной строки `NavPractical` (вопросы есть в футере).
- [Footer.razor:19-24](../src/frontend/ui.public.web/Components/Footer.razor#L19-L24) `.footer-col` — после `NavPlaces`.
- Все три ссылки — под `@if (Gallery.IsEnabled)`, иначе меню ведёт в 404 (паттерн `@if (!string.IsNullOrWhiteSpace(BotUrl))` уже есть в Header).
- [SeoUrls.PublicPaths()](../src/frontend/ui.public.web/Features/Seo/SeoUrls.cs#L36-L44) — `/gallery` после мест, с проверкой того же `MEDIA_ENDPOINT` (не тянуть `GalleryCatalog` — лишняя связность). **Image-sitemap не делать**: `SeoUrls` кэширует sitemap в `Lazy<string>`, список фото в нём протухнет навсегда.

---

## Этап 4. Безопасность

**CSP не трогаем.** `img-src 'self'` покрывает `/media/...`, потому что Caddy отдаёт и страницу, и фото с одного origin. Это и есть техническое обоснование решения «тот же домен»: поддомен потребовал бы `img-src 'self' data: https://media.…` — ослабление.

**Rate limiter UI не трогаем.** `/media/*` заканчивается в `reverse_proxy minio:9000` и до Kestrel не доходит — 200 картинок не съедают квоту `get:{ip}` 300/мин. Обратная сторона: `/media/*` публичен и не ограничен по частоте (в стандартной сборке Caddy нет rate limiting). Полный обход галереи ≈ 50 МБ — терпимо; при необходимости CDN или кастомная сборка с `mholt/caddy-ratelimit`.

**`USE_FORWARDED_HEADERS: "true"` и удаление `ports: 7002` — один коммит, не два.** Без флага [Program.cs:82](../src/frontend/ui.public.web/Program.cs#L82) видит IP контейнера Caddy, один для всех: партиция `post:{ip}` = 5 за 5 минут → шестая заявка за 5 минут получает 429 — форма ломается для всех в первый же день. Но код делает `KnownProxies.Clear()`, то есть доверяет `X-Forwarded-For` от кого угодно: пока `:7002` открыт, флаг позволяет подделать IP и обойти лимитер полностью, то есть делает хуже, чем без него.

**`MaxRequestBodySize = 64 КБ` не трогаем** — загрузка идёт в консоль MinIO через SSH-туннель, мимо Caddy и Kestrel.

**HSTS переезжает в Caddy** (покрывает и `/media`, не зависит от `ASPNETCORE_ENVIRONMENT`, который в compose стоит `Development` и HSTS фактически отключает).

**Бэкап.** После первой заливки `minio_volume` — единственная копия фото на сервере. Условия эксплуатации: оригиналы и папка `out/` остаются у владельца локально + регулярный `mc mirror --overwrite local/media /backup/media`. Записать в CLAUDE.md.

---

## Этап 5. CLAUDE.md

- **Команды**: 8 сервисов, сайт на `http://localhost` (не 7002 — порт закрыт), путь заливки фото, запуск скриптов.
- **Путь запроса**: новая первая ступень браузер → Caddy → (`/media/*` → MinIO | остальное → ui-public → gateway). Явно: медиа не проходят через Kestrel, не тратят rate limit и не участвуют в `UseStaticFiles`.
- **Новый раздел «Галерея и медиа-хранилище»**: зачем MinIO, пин версии образа, правила именования, slug в имени → локализованный alt, поведение при отказе MinIO, задержка = TTL, ловушка `.gallery-grid`, ограничение лайтбокса.
- **Новый раздел «Reverse proxy (Caddy)»**: единственная точка входа, staging-CA при первой настройке, `caddy_data` терять нельзя, правила `/media/*`, HSTS живёт здесь.
- **Безопасность**: убрать «нет TLS и реверс-прокси»; добавить связку `USE_FORWARDED_HEADERS` + закрытие 7002, три слоя защиты от исполняемого контента в бакете, `127.0.0.1` у служебных портов.
- **Конфигурация**: ключи `MEDIA_*`, строки для `minio` и `caddy`, `.env`/`.env.example`.
- **Локальная разработка**: при `dotnet run` на `:7002` галерея не работает (`/media` уйдёт в Kestrel → 404, а указать `localhost:9000` нельзя — `img-src 'self'`). Работать либо через `docker compose up`, либо с `MEDIA_ENDPOINT=""`.

---

## Проверка

**После этапа 1** (залив вручную `gallery/full/010-umba.webp` и `gallery/thumb/010-umba.webp`):
```powershell
curl.exe -sI http://localhost/media/gallery/thumb/010-umba.webp
#   200; Content-Type: image/webp; Cache-Control: public, max-age=2592000; ETag есть;
#   заголовков Server / x-amz-* НЕТ
curl.exe -s -o NUL -w "%{http_code}`n" -X PUT http://localhost/media/gallery/thumb/x.webp     # не 200
curl.exe -s -o NUL -w "%{http_code}`n" "http://localhost/media/?list-type=2"                  # 404 — листинг закрыт
curl.exe -s -o NUL -w "%{http_code}`n" "http://localhost/media/gallery/full/evil.svg"         # 404 — не .webp
curl.exe -s -o NUL -w "%{http_code}`n" http://localhost/                                      # 200 — сайт через Caddy
curl.exe -s -o NUL -w "%{http_code}`n" http://localhost:9000/                                 # отказ — S3 API закрыт
curl.exe -s -o NUL -w "%{http_code}`n" http://localhost:7002/                                 # отказ — UI не публикуется
```
Плюс 304 по `If-None-Match`. **Главная проверка «тот же домен»**: `http://localhost/` и `http://localhost/media/...` — один хост и порт.

**После этапа 2**: вертикальные кадры не лежат на боку; `magick identify -verbose out.webp | Select-String 'exif|GPS'` — пусто; thumb ровно 800×600.

**После этапа 3**:
```powershell
(curl.exe -s http://localhost/gallery) -match 'src="/media/gallery/thumb/'   # True
(curl.exe -s http://localhost/gallery) -match 'minio'                        # False — эндпоинт не утёк
(curl.exe -s http://localhost/sitemap.xml) -match '/gallery'                 # True
```
- DevTools → Console пуст (нет `Refused to load the image`); Network → `/media/*` = 200 + `max-age=2592000`; CLS ≈ 0.
- Клик по плитке → лайтбокс, Esc закрывает. С отключённым JS → открытие webp в новой вкладке.
- Языки en/zh → alt и заголовки локализованы.
- `docker compose stop minio` → `/gallery` не 500, показывает прошлый снимок, в логах `LogWarning`. `start` → норма через ≤5 минут.
- `MEDIA_ENDPOINT: ""` → `/gallery` = 404 с телом `NotFoundView`, ссылок в меню нет, в sitemap нет.
- Залить новый файл → появляется без перезапуска контейнера.
- Ширины 320/360/900/1024/1280 — `document.documentElement.scrollWidth` не превышает вьюпорт.

**После этапа 4**: 6 POST подряд → 429 на шестом, с другого IP → 200 (счётчик не общий). Прод: DNS + firewall, `SITE_ADDRESS`=домен, `SITE_URL=https://…`, сначала staging-CA, потом боевой; `docker compose logs caddy | Select-String 'certificate obtained'`; `curl.exe -sI https://домен/media/...` → есть `Strict-Transport-Security`; `curl.exe -sI http://домен/` → 308.

---

## Задел: видео (не реализуется сейчас)

Вопрос от 24.08.2026: как выкладывать несколько роликов по 3–5 минут. Ответ: **обычный MP4, потоковая передача (HLS/DASH) не нужна.**

### Почему хватает MP4

`<video>` шлёт `Range`-запросы, MinIO их поддерживает, Caddy пробрасывает — браузер играет с первых секунд и умеет перематывать, весь файл заранее не качается. Это и есть progressive streaming, просто без отдельного протокола.

HLS окупается при роликах длиннее ~10 минут, адаптивном битрейте под слабую мобильную сеть, прямом эфире или DRM. Цена — нарезка на сегменты, 2–3 рендишена вместо одного файла, плейлисты и **JS-плеер**: Safari играет HLS нативно, Chrome и Firefox — нет, то есть придётся вендорить hls.js в [js/vendor/](../src/frontend/ui.public.web/wwwroot/js/vendor/). Для нескольких коротких роликов несоразмерно.

### Три вещи, которые надо сделать правильно

**1. `faststart` — критично.** Если `moov`-атом лежит в конце файла (по умолчанию у многих кодировщиков), браузер обязан скачать ролик целиком, прежде чем показать первый кадр:
```bash
ffmpeg -i in.mov -c:v libx264 -crf 23 -preset slow -pix_fmt yuv420p \
       -c:a aac -b:a 128k -movflags +faststart out.mp4
```
Проверка: `ffprobe -v trace out.mp4 2>&1 | grep -m2 -E 'moov|mdat'` — `moov` первым.

**2. `preload="none"` + постер.** Иначе страница с тремя роликами начнёт тянуть десятки мегабайт до того, как посетитель нажмёт play:
```html
<video controls preload="none" poster="/media/video/poster/010-teriberka.webp"
       width="1280" height="720">
  <source src="/media/video/010-teriberka.mp4" type="video/mp4">
</video>
```

**3. Кодек — H.264 + AAC.** Играет везде без исключений. VP9/AV1 дают файл вдвое легче, но кодируются в разы дольше, а поддержка AV1 в Safari зависит от железа; при желании добавляются вторым `<source>`.

### Вес и трафик

Ориентиры для съёмки природы (море, снег, движение — картинка высокоэнтропийная):

| Разрешение | Битрейт | 3 мин | 5 мин |
|---|---|---|---|
| 720p | ~2,5 Мбит/с | ~55 МБ | ~90 МБ |
| 1080p | ~4 Мбит/с | ~90 МБ | ~150 МБ |

Делать **одну версию 1080p с CRF 23**, а не набор качеств. Уходит за ~150 МБ — резать длительность, а не переходить на HLS.

**Трафик — отдельная статья расходов.** `/media/*` публичен и не ограничен по частоте (в стандартной сборке Caddy нет rate limiting). 100 просмотров ролика на 90 МБ = 9 ГБ исходящего трафика. Для фото это несущественно, для видео — повод посмотреть на тариф VPS или поставить CDN.

### Правки конфигурации

Caddyfile из этапа 1 пускает наружу **только `.webp`** и принудительно ставит `Content-Type: image/webp`. Под видео — отдельный матчер рядом с `@photo`:

```caddyfile
@video {
    method GET HEAD
    path_regexp \.mp4$
}
handle @video {
    rewrite * /{$MEDIA_BUCKET}{path}
    reverse_proxy minio:9000 { header_up Host {upstream_hostport} }
    header {
        Cache-Control "public, max-age={$MEDIA_MAX_AGE}"
        Content-Type "video/mp4"
        X-Content-Type-Options "nosniff"
        -Server
        -x-amz-request-id
    }
}
```

- **Не пропускать видео через `encode`** — h264 уже сжат, а сжатие ломает `Content-Length` у диапазонов. Список MIME-типов в `encode` видео не включает, но при правке следить за этим.
- **Не трогать `Accept-Ranges`** от MinIO — на нём держится перемотка.
- `Content-Security-Policy: default-src 'none'; sandbox` на медиа-ответах видео не мешает.
- **CSP сайта менять не нужно**: `media-src` в политике не задан и падает на `default-src 'self'`, а видео с того же origin — ровно как фото.
- Префиксы в бакете: `video/*.mp4` и `video/poster/*.webp`. Постеры кладутся под `.webp` и проходят существующим матчером `@photo`.
- `GalleryCatalog` видео не собирает: ролики штучные, их проще перечислить в разметке страницы, чем городить второй листинг.

---

## Отложено (не входит в этот заход)

- **Отдельный сервис Media.** Обсуждался и сознательно отложен: при текущих решениях (заливка через консоль MinIO, UI читает бакет сам, админки нет) он был бы слоем без собственной логики, а отдача байтов через ASP.NET вместо Caddy — лишний хоп на каждое из 200 фото. Оправдается, когда появится загрузка через сайт, метаданные в БД (альбомы, подписи, порядок) или второй потребитель медиа. **Требование к коду сейчас**: вся работа с S3 живёт только в `Features/Gallery/` (`GalleryCatalog` + `GalleryPhoto` + `GalleryRefresher`), разметка знает исключительно относительные `/media/...`-пути. Тогда вынос в сервис = перенос двух файлов, наследник `ApiHandler` в UI и маршрут с кластером на шлюзе, без переписывания страницы.
- **Стрелки ←/→ в лайтбоксе.** На 200 фото текущий лайтбокс — стена: каждый снимок надо закрывать. Расширение [lightbox.js](../src/frontend/ui.public.web/wwwroot/js/lightbox.js) с сохранением прогрессивности; строки `CarouselPrev`/`CarouselNext` уже есть, передавать в JS через `data-*` (inline-скрипты запрещены CSP).
- Блок-тизер галереи на главной (3–4 плитки + ссылка «Все фотографии»).
- Фильтр галереи по местам — `PlaceSlug` для этого уже собирается.
