# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## О проекте

Сайт записи на тур в Териберку: посетитель заполняет форму на главной и ждёт звонка. Полноценной авторизации нет и не планируется.

Просмотр поступивших заявок на сайте намеренно **не делается** — заявки будут читаться из Telegram-бота (решение от 11.08.2026, бот ещё не начат). Серверная часть для этого уже есть: `GET /api/admin/application/list` и репозиторий, сортирующий заявки свежими сверху.

Состав: два .NET 10 сервиса (`gateway` на YARP + `users` с БД) и Blazor-фронтенд на static SSR: лендинг (hero со статистикой → места → параллакс-полоса → «что включено» → гиды → параллакс-полоса → отзывы ×9 → форма → футер) плюс страницы мест `/place/{slug}`. Часть комментариев — на русском.

**Контент-плейсхолдеры, которые надо заменить перед запуском**: контакты в футере (телефон/Telegram/WhatsApp), цифры статистики в hero (6 лет / 2000+ / 4,9), имена гидов и все отзывы — вымышленные.

## Команды

```powershell
dotnet build Teriberka.sln                # сборка решения (~15 c, 0 ошибок; ~110 warning'ов — норма)
docker compose up -d postgres             # только БД для локальной разработки
docker compose up --build                 # весь стек в контейнерах (БД + gateway + users + UI)
```

Локальный запуск сервисов — **обязательно с явным окружением**: `dotnet run` печатает «Using launch settings…», но переменные из `environmentVariables` профиля не применяет, поэтому окружение остаётся Production и `appsettings.Development.json` не читается. Симптомы: у users — `The ConnectionString property has not been initialized`, у шлюза — 500 на любом запросе (`JWT_KEY` = null).

```powershell
dotnet run --project src/backend/users/service/Users.csproj -- --environment Development        # :5012
dotnet run --project src/backend/gateway/Gateway.csproj -- --environment Development            # :5000
dotnet run --project src/frontend/ui.public.web/UI.Public.Web.csproj -- --environment Development  # :7002
```

Каталог запуска не важен — ContentRoot всегда выставляется в каталог проекта. Альтернатива флагу — задать `ASPNETCORE_ENVIRONMENT=Development` в окружении или передать нужные ключи (`DB_*_CONNECTION_STRING`, `JWT_KEY`, …) переменными среды.

Миграции (только users, контекст `WriteApplicationDbContext`):

```powershell
dotnet ef migrations add <Name> `
  --project src/backend/users/modules/Users.Infrastructure `
  --startup-project src/backend/users/service `
  --context WriteApplicationDbContext
```

`database update` не нужен: `EnsureDataBaseAsync` в `Program.cs` применяет миграции при старте сервиса (при окружении `Testing` — `EnsureCreated`).

Тестов нет: тестовых проектов не создано, версии тестовых пакетов из `Directory.Packages.props` удалены. При добавлении тестов сначала верните нужные `PackageVersion` — включено central package management, поэтому в `.csproj` версии не указываются.

### Полезное для отладки

- Swagger-агрегатор шлюза: `http://localhost:5000/swagger` (только Development).
- Заявки посмотреть вручную: `GET http://localhost:5000/dev/token` (только Development) отдаёт JWT с ролью `SuperAdmin`, с ним `GET http://localhost:5000/api/admin/application/list` возвращает список. Тем же путём пойдёт Telegram-бот, только с постоянным токеном.
- Health: `/health/ready` и `/health/live` у сервисов, `/health` у шлюза и UI.

## Архитектура

### Путь запроса

```
браузер --POST form--> UI (Blazor SSR) --Mediator--> ApiCommandHandler --HTTP--> gateway --> users --> Postgres
```

Единственное, что делает сайт, — создаёт заявку через открытый маршрут `/api/public/application/create`. Чтение заявок наружу отдаётся только через `/api/admin/application/list`, закрытый на шлюзе политикой `Admin`.

### UI: тема, статика, локализация

- **Вся страница — без JavaScript** (CSP `script-src 'none'`), поэтому и тема, и язык решаются на сервере.
- **Стили только в [wwwroot/css/app.css](src/frontend/ui.public.web/wwwroot/css/app.css)**: CSP `style-src 'self'` запрещает и `<style>`, и inline-атрибуты `style=""` — оформление добавляется классами. Дизайн-токены в `:root` (тёмная «полярная ночь», по умолчанию) и `[data-theme="light"]` («полярный день»). Тема переключается кнопкой в шапке: `/set-theme` ставит cookie `theme`, App.razor вешает `data-theme` на `<html>` и меняет `theme-color`. Параллакс-полосы намеренно остаются тёмными в обеих темах.
- **Кнопки прокрутки** (`.scrollers` в MainLayout) — якорные ссылки `#top`/`#page-bottom` + `scroll-behavior: smooth`; «наверх» проявляется после первого экрана через CSS scroll-driven animation (`animation-timeline: scroll()`, в браузерах без поддержки видна всегда).
- **Локализация**: ru (по умолчанию, нейтральный resx) / en / zh. Строки — в [Resources/SharedResource*.resx](src/frontend/ui.public.web/Resources/), в разметке — `IStringLocalizer<SharedResource>`. Культура хранится в cookie; переключатель — GET-ссылки на `/set-culture?culture=xx&redirect=...` (endpoint в Program.cs валидирует культуру по белому списку и делает LocalRedirect). Accept-Language сознательно не учитывается. Тексты ошибок FluentValidation в контрактах — только русские.
- **Страницы мест**: карточка на главной — ссылка на `/place/{slug}` ([PlaceView.razor](src/frontend/ui.public.web/Features/Places/Pages/PlaceView.razor)). Каталог мест — статический [PlaceCatalog](src/frontend/ui.public.web/Features/Places/PlaceCatalog.cs) (это контент, БД не нужна): новое место = запись в каталоге + ключи `Place{i}Title/Text/Detail1/Detail2` в трёх resx. Неизвестный slug отвечает честным 404 (StatusCode через каскадный `HttpContext`).
- **Фото мест** пока одна SVG-заглушка [img/placeholder.svg](src/frontend/ui.public.web/wwwroot/img/placeholder.svg) + бейдж «Фото скоро»; в галерее страницы места она разведена CSS-фильтрами по оттенку. Настоящие фотографии кладутся в `wwwroot/img` и подставляются в Home.razor и PlaceView.razor.
- **Скелет кита** ([img/leviathan.svg](src/frontend/ui.public.web/wwwroot/img/leviathan.svg)) — силуэт в декоративной полосе `.shore-band` (MainLayout, перед футером, на всех страницах); миниатюра лежит на берегу в parallax.svg. В hero его класть не стоит — уже пробовали: на широких экранах конфликтует с контентом.
- `<title>` задаётся только через `<PageTitle>` (HeadOutlet); статического title в App.razor нет — иначе в head оказываются два тега.
- Статика раздаётся `UseStaticFiles()` **до** rate limiter'а — css и картинки не сжигают лимит.
- Ловушка биндинга: `[SupplyParameterFromQuery] bool` понимает только `true`/`false` — редирект после формы обязан быть `/?sent=true`, с `?sent=1` страница «спасибо» молча не покажется.
- **Параллакс** — чистый CSS: у `.parallax-band` фон [img/parallax.svg](src/frontend/ui.public.web/wwwroot/img/parallax.svg) с `background-attachment: fixed` (iOS Safari игнорирует — полоса деградирует в обычный баннер). JavaScript-параллакс невозможен из-за CSP.
- **Карусель отзывов** — тоже без JavaScript: `scroll-snap` (свайп/колесо), на десктопе видны три карточки + край четвёртой как аффорданс листания. Стрелок нет и не будет, пока действует запрет скриптов.
- **Микроанимации** (мерцание звёзд и дрейф авроры в hero) анимируют только `opacity`/`transform` — исполняются GPU-композитором. При добавлении новых анимаций держаться этого правила (не анимировать background/box-shadow/filter) и не забывать ветку `prefers-reduced-motion`.
- Скриншоты для проверки вёрстки: headless Chrome на Windows не отдаёт окно уже ~500px, поэтому мобильную ширину снимать через CDP `Emulation.setDeviceMetricsOverride` (+`Page.captureScreenshot` с `captureBeyondViewport`), а не `--window-size`. На таких скриншотах fixed-фоны параллакс-полос выглядят пустыми — это артефакт съёмки, вживую фон на месте.

### Форма без JavaScript

Фронтенд работает на **static SSR** — интерактивный render mode не зарегистрирован и `blazor.web.js` не подключён, поэтому `@onclick`/`@onsubmit` в браузере не выполняются. Форма серверная: `EditForm` + `[SupplyParameterFromForm]` + `FormName`, обработка на POST, после успеха — редирект на `/?sent=1` (post-redirect-get), чтобы F5 не отправлял заявку повторно. Antiforgery-токен `EditForm` добавляет сам, `app.UseAntiforgery()` включён. Валидация: HTML5-атрибуты в браузере + FluentValidation-валидатор из контрактов на сервере (в UI внедряется как `IEnumerable<IValidator<T>>`), плюс тот же валидатор отрабатывает в сервисе через `ValidatorBehavior`.

При переходе на интерактивный рендеринг (`AddInteractiveServerComponents` / `AddInteractiveServerRenderMode`) форму придётся переписать под обычные обработчики событий.

### Контракты общие для бэка и фронта

`src/contracts/*` ссылаются и сервисы, и Blazor-проект. Один и тот же тип (`ApplicationCreateCommand`) — это одновременно Mediator-запрос в UI, Mediator-запрос в сервисе и тело `[FromBody]` у HTTP-эндпоинта. Базовые типы в [src/contracts/Contracts/](src/contracts/Contracts/): `Command`, `DeleteCommand`, `Query<TDto>`, `ListQuery<TDto>`, `PagedListQuery<TDto,TQuery>`, `Dto`, `AuditableDto` и базовые валидаторы.

Следствие: **чтобы UI мог вызвать эндпоинт, в `ui.public.web/Handlers` нужен свой `ApiHandler`-наследник**, иначе Mediator не найдёт обработчик (генератор предупреждает `MSG0005`, а `Send` падает в рантайме).

### Обобщённый CQRS-конвейер: код фичи — почти только типовая обвязка

Всё реализовано в generic-виде в `src/backend/shared`, поэтому фича обычно = набор пустых наследников:

- Handler'ы: `CreateCommandHandler<>`, `UpdateCommandHandler<>`, `DeleteCommandHandler<>`, `SingleQueryHandler<>`, `ListQueryHandler<>`, `PagedListQueryHandler<>`.
- Репозитории `CommandRepository<,>`, `SingleQueryRepository<,,>`, `ListQueryRepository<,,>` зарегистрированы как open generic в `AddApiServices`. Свой репозиторий нужен только для нестандартной выборки — тогда наследуешься и переопределяешь `BuildDbQuery` / `ProcessDbQuery` / `ProcessTextQuery` / `ProcessSorting` / `ProjectSimple`; Scrutor подхватит наследника автоматически и он победит open-generic регистрацию (пример: [ApplicationListQueryRepository](src/backend/users/modules/Users.Infrastructure/DataAccess/Repositories/Application/ApplicationListQueryRepository.cs) сортирует заявки свежими сверху).
- Маппинг: Mapperly, регистрация по соглашению об имени — класс должен называться `*DtoMapper` / `*EntityMapper` / `*RpcMapper` и лежать в сборке, имя которой заканчивается на `Infrastructure.dll` (`ConfigureMappers`). Переименуешь — маппер молча не зарегистрируется.
- Валидация: `ValidatorBehavior<,>` подключается в `Configurator.ConfigureDependencies`, валидаторы находятся по сборкам автоматически.

Сквозной референс — сущность `Application` (заявка): [contracts](src/contracts/Applications.Contracts/), [entity](src/backend/users/modules/Users.Domain/Entities/Application.cs), [EF config](src/backend/users/modules/Users.Infrastructure/DataAccess/Config/ApplicationConfig.cs), [mappers](src/backend/users/modules/Users.Infrastructure/DataAccess/Mappers/ApplicationMapper.cs), [handlers](src/backend/users/modules/Users.Application/Handlers/Application/), [эндпоинты](src/backend/users/service/Configurator.cs), [UI handler](src/frontend/ui.public.web/Handlers/ApplicationHandlers.cs), [страница с формой](src/frontend/ui.public.web/Features/Home/Pages/Home.razor).

Чек-лист новой сущности: контракт → entity → EF config → мапперы → handler'ы → эндпоинт в `ConfigureEndPoints` → **маршрут в шлюзе** (если нужен внешний доступ) → миграция → handler + страница в UI. Забытый маршрут в шлюзе — самая частая причина «эндпоинт есть, но недоступен».

### URL-конвенция и авторизация

Формат `/api/{role}/{group}/{action}`, где `role` ∈ `public` | `private` | `admin` (`Api.Constants.UrlRestrictions`). Эндпоинты объявляются fluent-билдером:

```csharp
app.MediateGroup("application", Constants.UrlRestrictions.Admin)
   .List<ApplicationListQuery, ApplicationDto>();              // GET /api/admin/application/list
app.MediatePostCommand<ApplicationCreateCommand>("application", "create");  // POST /api/public/application/create
```

Действия билдера: `Single` → `get`, `List` → `list`, `PagedList` → `pagedList`, `Create` → POST `create`, `Update` → PUT `update`, `Delete` → DELETE `delete/{id}`.

**Роль в URL — только сегмент пути, сервис её не проверяет.** Аутентификация и авторизация целиком на шлюзе: JWT bearer + политика `Admin` (claim role = 1/2, см. `UserRole`), назначаемая маршруту через `AuthorizationPolicy` в [gateway/appsettings.json](src/backend/gateway/appsettings.json). Отсюда требование к развёртыванию: **users-сервис не должен быть доступен из внешней сети** — сам он никого не проверяет и доверяет вызывающему. В `docker-compose.yml` его порт 5012 опубликован только для локальной отладки.

### Данные

В БД две сущности: `Application` (заявки — то, ради чего сайт) и `User` (эндпоинты `/api/admin/user/*` есть, но потребителей у них пока нет).

- Раздельные контексты: `ReadApplicationDbContext` (глобально NoTracking) и `WriteApplicationDbContext`, строки подключения `DB_READ_CONNECTION_STRING` / `DB_WRITE_CONNECTION_STRING`.
- Схема выбирается через `search path` в строке подключения (users → `search path=users,public`), EF генерирует SQL без указания схемы. `WriteDbContext.MigrateAsync` перед миграцией создаёт схему, расширения `citext` / `pg_trgm` / `btree_gin` и коллацию `case_insensitive`. Переезд с общей БД на отдельные = правка строки подключения.
- Базовые контексты зарегистрированы factory-алиасами на конкретные (`AddScoped<ReadDbContext>(sp => sp.GetRequiredService<TR>())`), иначе `UnitOfWork` и репозиторий получили бы разные инстансы. Подробности — в комментарии внутри `AddPersistence`.
- Оптимистичная блокировка: `Entity.Xmin` замаплен на системный столбец `xmin` как concurrency token; update-команды несут `Xmin`, репозиторий кладёт его в `OriginalValue`.
- Удаление мягкое: `IsDeleted = true`, list-запросы фильтруют по нему (`GetBaseDbQuery`).
- `SaveChanges` в handler'ах не вызывается: `CommandHandler.Handle` делает `UnitOfWork.CommitAsync` после `ExecuteCommand`; для пост-коммит эффектов — `AfterCommit`.
- `EntityConfig` по умолчанию навешивает **уникальный** индекс на `Title` — где заголовки повторяются, переопределяй `ConfigureIndexes` (так сделано в `ApplicationConfig`: два тёзки должны иметь возможность записаться). `UserEntity.Title` — stored computed column из `FirstName`/`LastName`.

### Безопасность

Что уже настроено и почему именно так:

- **Rate limiting в двух местах.** Форма отправляется POST'ом на сам сайт, а API-вызов делает сервер UI, поэтому ограничение только на шлюзе обходится отправкой формы в цикле. В UI глобальный лимитер делит запросы по методу: POST — 5 за 5 минут на IP, остальное — 300 в минуту. На шлюзе маршрут `applications-public` помечен политикой `public-form` (те же 5 за 5 минут) плюс глобальные 300/мин; `/health` из лимитов исключён. Счётчики живут в памяти процесса — при нескольких инстансах фактический лимит умножается на их число.
- **CORS сознательно узкий и, по сути, задел на будущее.** Браузер в API не обращается (страницы рендерит сервер UI), поэтому политика собирается строго из `UI_APP_URL`, без `AllowCredentials`, и не регистрируется вовсе, если список пуст. Прежняя dev-ветка с `SetIsOriginAllowed(_ => true)` + `AllowCredentials()` убрана: такое сочетание разрешало любому сайту читать ответы API с cookie пользователя.
- **Заголовки ответа UI**: CSP (`script-src 'none'` — на страницах вообще нет JavaScript, проверено; `style-src 'self'` — поэтому никаких inline-стилей), `X-Content-Type-Options`, `X-Frame-Options: DENY`, `frame-ancestors 'none'`, `Referrer-Policy: no-referrer`, `Permissions-Policy`. Заголовок `Server` отключён у обоих сервисов. Если появится интерактивный Blazor — CSP придётся ослаблять под `blazor.web.js`.
- **Antiforgery** — включён (`UseAntiforgery`), токен в форму добавляет `EditForm`.
- **Валидация телефона** по формату, а не только по длине, и верхняя граница даты приезда: поле уходит оператору и в бота, без формата форма становится каналом для спам-ссылок.
- **Лимит тела запроса** в UI — 64 КБ (загрузок файлов нет). Появится загрузка — поднять.
- **`USE_FORWARDED_HEADERS`** (по умолчанию `false`) включает `X-Forwarded-For`/`Proto`. Включать только когда сервис реально стоит за доверенным прокси и недоступен напрямую: иначе клиент сам подставит себе IP и обойдёт rate limiting. Без этого флага за прокси все клиенты выглядят одним IP.
- **users-сервис не должен торчать в интернет** — он никого не проверяет и доверяет вызывающему; в `docker-compose.yml` порт открыт только для локальной отладки.

Чего ещё нет и о чём стоит помнить: капчи или honeypot (rate limiting не спасает от распределённого спама), согласия на обработку персональных данных и политики конфиденциальности (имя + телефон — персональные данные), TLS-сертификата и реверс-прокси, бэкапов БД. `EnableSensitiveDataLogging` в EF не включён — включать нельзя, иначе телефоны попадут в логи.

### Остатки, которые ещё ни к чему не подключены

- В `UI.Shared` из handler'ов используется только `ApiCommandHandler`; `ApiSingleQueryHandler`, `ApiListQueryHandler`, `ApiPagedListQueryHandler`, `QueryBuilder`, `ConfigService` и `AuthorizationHeaderHandler` (с ключом `API_TOKEN`) остались от страницы заявок и сейчас без потребителей.
- `SanitizationBehavior` + `HtmlSanitizer` написаны, но ни один сервис не добавляет behavior в `PipelineBehaviors`.
- Серверная локализация (`SharedStrings.resx` в Api, `UsersStrings.resx` в users, `SharedLocalizer`) заведена, но не используется — рабочая локализация есть только в UI (`SharedResource`).
- Абстракции `ISlugable` / `IPictureable` и поле `Dto.ImagePath` есть в базовых типах, но ни одна сущность их не реализует; файлового сервиса и хранилища в проекте нет.

## Конфигурация

Настройки — плоские ключи в стиле переменных окружения (не иерархические секции), читаются напрямую из `IConfiguration`:

| Сервис | Ключи |
|---|---|
| UI | `API_URL`, `API_TOKEN`, `USE_FORWARDED_HEADERS` |
| gateway | `JWT_ISSUER`, `JWT_KEY`, `UI_APP_URL` (белый список CORS), `USE_FORWARDED_HEADERS` |
| users | `DB_READ_CONNECTION_STRING`, `DB_WRITE_CONNECTION_STRING`, `HTTP_PORT` |
| все | `OTLP_EXPORT_ENABLED`, `OTLP_RECEIVER_ENDPOINT_HTTP`, `OTLP_RECEIVER_ENDPOINT_GRPC`, `LOGGING_MIN_LEVEL` |

Dev-значение `JWT_KEY` лежит в репозитории для локального запуска — в реальном развёртывании задаётся через окружение.

Сборка: `Directory.Build.props` задаёт `net10.0` / `LangVersion 14` / nullable / implicit usings; `src/backend/Directory.Build.props` добавляет `Mediator.SourceGenerator` и `EFCore.Design` во все Web-SDK проекты бэкенда. Версии пакетов — только в `Directory.Packages.props`, там намеренно нет неиспользуемых зависимостей.
