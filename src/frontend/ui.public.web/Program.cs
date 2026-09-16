using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Chat.Contracts;
using Mediator;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using UI.Public.Web.Components;
using UI.Public.Web.Features.Analytics;
using UI.Public.Web.Features.Captcha;
using UI.Public.Web.Features.Chat;
using UI.Public.Web.Features.Manager;
using UI.Public.Web.Features.Media;
using UI.Public.Web.Features.Seo;
using UI.Shared;
using UI.Shared.Interceptors;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    // На сайте только текстовая форма заявки; загрузки файлов нет, поэтому 64 КБ хватает
    // с большим запасом. Появится загрузка файлов — лимит нужно поднять.
    options.Limits.MaxRequestBodySize = 64 * 1024;
});

// См. комментарий в gateway: нужно, чтобы за прокси видеть реальный IP клиента (для
// rate limiting) и схему запроса. Включать только когда сайт стоит за доверенным прокси.
var useForwardedHeaders = builder.Configuration.GetValue("USE_FORWARDED_HEADERS", false);
if (useForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddHealthChecks();
builder.Services.AddRazorComponents();

// Абсолютные URL для canonical/OG/sitemap строятся от SITE_URL (см. SeoUrls).
builder.Services.AddSingleton<SeoUrls>();

// Содержимое хранилища фото (полоса на главной и галерея). Список обновляется
// в фоне: рендер страницы читает готовый снимок и в хранилище не ходит.
builder.Services.AddSingleton<MediaCatalog>();
builder.Services.AddHostedService<MediaRefresher>();

// Часы работы чата: виджет честно говорит, ответят сейчас или утром.
builder.Services.AddSingleton<ChatSchedule>();

// Невидимая SmartCaptcha формы заявки: пустые ключи выключают её целиком.
builder.Services.AddSingleton<SmartCaptchaService>();

// Яндекс.Метрика: пустой YANDEX_METRIKA_ID выключает счётчик целиком.
builder.Services.AddSingleton<MetrikaService>();

// Сжатие только для статики: text/html сознательно не сжимаем — в страницах
// antiforgery-токен плюс отражённый query в ссылках set-culture/set-theme,
// их сжатие открывало бы BREACH. Картинки webp уже сжаты кодеком.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true; // безопасно: HTML исключён из списка MIME
    options.MimeTypes = ["text/css", "text/javascript", "image/svg+xml", "application/xml", "text/plain"];
});

// Три языка интерфейса. Нейтральный resx — русский, он же культура по умолчанию.
// Выбор языка хранится в culture-cookie, которую ставит endpoint /set-culture:
// страницы рендерятся без JavaScript, поэтому переключатель — обычные ссылки.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var supportedCultures = new[] { new CultureInfo("ru"), new CultureInfo("en"), new CultureInfo("zh") };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("ru");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    // Только cookie: Accept-Language не учитываем, чтобы язык не «прыгал» сам по себе.
    options.RequestCultureProviders = [new CookieRequestCultureProvider()];
});

// Форма отправляется POST'ом на сам сайт, а не в API, поэтому лимит нужен и здесь —
// иначе ограничение на шлюзе обходится обычной отправкой формы в цикле.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Вход на страницу заявок: вместо общей страницы ошибки — обратно на форму входа
    // с понятным «подождите», иначе менеджер видел «Страница не найдена».
    options.OnRejected = (context, _) =>
    {
        if (context.HttpContext.Request.Path.StartsWithSegments(ManagerAuth.SignInPath))
            context.HttpContext.Response.Redirect(ManagerAuth.LoginPath + "?locked=true");

        return ValueTask.CompletedTask;
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var path = context.Request.Path;

        // У чата свои партиции. Иначе поллинг съедал бы общий лимит страницы, а
        // отправка сообщения — квоту формы заявки (5 за 5 минут), и шестая реплика
        // в диалоге упиралась бы в 429.
        if (path.StartsWithSegments("/chat/poll"))
            return RateLimitPartition.GetFixedWindowLimiter($"chatpoll:{client}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 40,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });

        // Вход на страницу заявок — отдельно и строже: пароль один на всех,
        // перебирать его не должно быть смысла. Отметки менеджера — своя щедрая
        // партиция, иначе шестая за пять минут упиралась бы в квоту формы заявки.
        if (path.StartsWithSegments(ManagerAuth.SignInPath))
            return RateLimitPartition.GetFixedWindowLimiter($"mgrlogin:{client}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            });

        if (path.StartsWithSegments("/manager/process"))
            return RateLimitPartition.GetFixedWindowLimiter($"mgrprocess:{client}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });

        if (path.StartsWithSegments("/chat/send"))
            return RateLimitPartition.GetFixedWindowLimiter($"chatsend:{client}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });


        return HttpMethods.IsPost(context.Request.Method)
            ? RateLimitPartition.GetFixedWindowLimiter($"post:{client}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            })
            : RateLimitPartition.GetFixedWindowLimiter($"get:{client}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

builder.Services.AddHttpContextAccessor();

// Страница заявок для менеджеров: cookie-вход по MANAGER_PASSWORD (Features/Manager).
builder.Services.AddManager();
builder.Services.AddTransient<AuthorizationHeaderHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthorizationHeaderHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler)
    {
        BaseAddress = new Uri(builder.Configuration["API_URL"]!)
    };
});

builder.Services.AddScoped(_ => new ConfigService(builder.Configuration["API_URL"]!));

var assemblies = AppDomain.CurrentDomain.GetAssemblies();
builder.Services.AddValidatorsFromAssemblies(assemblies);

builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
});

var app = builder.Build();

if (useForwardedHeaders)
    app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Жёсткая CSP: скрипты и стили — только собственные файлы (inline-скрипты и
// inline-стили запрещены), картинки — свои и data:, отправка форм — только на свой
// origin, встраивание в iframe запрещено.
// Сторонних хостов ровно два, и каждый появляется только вместе со своей фичей
// (без ключей CSP остаётся прежней): SmartCaptcha — её скрипт, iframe проверки и
// XHR виджета; Яндекс.Метрика — tag.js, хиты картинкой и XHR/beacon вебвизора
// (blob: в worker-src — вебвизор пишет сессию в Worker'е, созданном из blob).
// Хит уходит на mc.yandex.ru, но у зарубежных посетителей — на mc.yandex.com,
// поэтому в img/connect он тоже перечислен; скрипт грузится только с .ru.
// Заголовок собирается один раз — конфиг не меняется на лету.
// 'unsafe-inline' у стилей — вынужденная цена капчи (проверено 28.08.2026):
// виджет ставит инлайновые style-атрибуты в родительскую страницу (позиция
// бейджа и попапа с пазлом), под style-src 'self' их режет и пазл не показать;
// хеши не вариант — они меняются с каждым обновлением виджета. Скрипты при
// этом остаются под замком: script-src без 'unsafe-inline'.
var captchaOn = app.Services.GetRequiredService<SmartCaptchaService>().Enabled;
var metrikaOn = app.Services.GetRequiredService<MetrikaService>().Enabled;
const string captchaHost = "https://smartcaptcha.yandexcloud.net";
const string metrikaHost = "https://mc.yandex.ru";
const string metrikaHostCom = "https://mc.yandex.com";

var scriptSrc = "'self'"
    + (captchaOn ? " " + captchaHost : "")
    + (metrikaOn ? " " + metrikaHost : "");
var imgSrc = "'self' data:" + (metrikaOn ? $" {metrikaHost} {metrikaHostCom}" : "");
// wss:// перечисляется отдельно: источник со схемой https источники wss не
// покрывает, а вебвизор держит WebSocket на mc.yandex.ru/solid.ws (проверено
// в браузере 31.08.2026 — без него запись сессий блокируется CSP).
var connectSrc = "'self'"
    + (captchaOn ? " " + captchaHost : "")
    + (metrikaOn ? $" {metrikaHost} {metrikaHostCom} wss://mc.yandex.ru wss://mc.yandex.com" : "");
var frameSrc = "'self'"
    + (captchaOn ? " " + captchaHost : "")
    + (metrikaOn ? " " + metrikaHost : "");

var csp =
    "default-src 'self'; " +
    $"script-src {scriptSrc}; " +
    $"style-src 'self'{(captchaOn ? " 'unsafe-inline'" : "")}; img-src {imgSrc}; " +
    // frame-src/connect-src перечисляются, только когда есть кому их использовать:
    // без капчи и Метрики оба падают на default-src 'self'
    (captchaOn || metrikaOn ? $"frame-src {frameSrc}; connect-src {connectSrc}; " : "") +
    // шрифты бейджа капчи едут с yastatic.net (хост без схемы — матчит и http-локалку);
    // без них бейдж просто падает на системный шрифт, но чисто — лучше
    (captchaOn ? "font-src 'self' yastatic.net; " : "") +
    // вебвизор Метрики поднимает Worker из blob-URL; без этого запись сессий молчит
    (metrikaOn ? "worker-src 'self' blob:; " : "") +
    "form-action 'self'; base-uri 'self'; frame-ancestors 'none'";

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["Content-Security-Policy"] = csp;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

    // Страница заявок с телефонами клиентов не должна оседать ни в кеше браузера,
    // ни в промежуточных прокси.
    if (context.Request.Path.StartsWithSegments(ManagerAuth.BasePath))
    {
        headers.CacheControl = "no-store";
        headers["X-Robots-Tag"] = "noindex, nofollow";
    }

    await next();
});

// URL, не совпавший ни с одним @page (например, /qwerty), не доходит до Blazor-роутера
// и отдал бы голый 404 без тела — re-execute рендерит человеку страницу NotFoundView.
// Внутри неё guard по уже выставленному коду не даёт зациклить Navigation.NotFound().
app.UseStatusCodePagesWithReExecute("/not-found");

app.UseResponseCompression();

// Статику отдаёт MapStaticAssets (ниже, у эндпоинтов): при сборке каждый файл
// wwwroot получает адрес с отпечатком содержимого, разметка берёт его через
// Assets[...], и такой адрес кешируется на год (immutable). UseStaticFiles остался
// запасным вариантом для файлов, которых нет в манифесте сборки (например,
// подложенных в wwwroot уже после неё): маршрутизация идёт раньше middleware,
// поэтому известные сборке пути сюда не доходят. Стоит до rate limiter'а — css и
// картинки не должны сжигать лимит запросов.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var maxAge = ctx.File.Name.EndsWith(".css") || ctx.File.Name.EndsWith(".js")
            ? TimeSpan.FromHours(1)
            : TimeSpan.FromDays(30);
        ctx.Context.Response.Headers.CacheControl = $"public, max-age={(int)maxAge.TotalSeconds}";
    }
});

app.UseRequestLocalization();

app.UseRateLimiter();

// Аутентификация — только для страницы заявок (cookie с путём /manager); до
// antiforgery, как требует ASP.NET Core.
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Переключение языка без JavaScript: GET-ссылка ставит culture-cookie и возвращает
// на страницу. LocalRedirect не пускает редирект на чужие домены.
app.MapGet("/set-culture", (string culture, string? redirect, HttpContext context) =>
{
    if (culture is not ("ru" or "en" or "zh"))
        return Results.BadRequest();

    context.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            HttpOnly = true,
            Secure = context.Request.IsHttps
        });

    return Results.LocalRedirect(redirect is ['/', ..] ? redirect : "/");
});

// Переключение темы — тем же приёмом: cookie читает App.razor и вешает
// data-theme на <html>; палитры лежат в app.css.
app.MapGet("/set-theme", (string theme, string? redirect, HttpContext context) =>
{
    if (theme is not ("dark" or "light"))
        return Results.BadRequest();

    context.Response.Cookies.Append("theme", theme, new CookieOptions
    {
        Expires = DateTimeOffset.UtcNow.AddYears(1),
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        HttpOnly = true,
        Secure = context.Request.IsHttps
    });

    return Results.LocalRedirect(redirect is ['/', ..] ? redirect : "/");
});

// Согласие с уведомлением о cookie: та же схема, что у темы и языка — cookie
// ставит сервер, ссылка возвращает на ту же страницу. Год жизни: дольше держать
// технический флажок незачем, а чаще раза в год спрашивать — навязчиво.
app.MapGet("/accept-cookies", (string? redirect, HttpContext context) =>
{
    context.Response.Cookies.Append("cookie_notice", "1", new CookieOptions
    {
        Expires = DateTimeOffset.UtcNow.AddYears(1),
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        HttpOnly = true,
        Secure = context.Request.IsHttps
    });

    return Results.LocalRedirect(redirect is ['/', ..] ? redirect : "/");
});

// robots.txt и sitemap.xml — endpoints, а не файлы в wwwroot: robots нужна
// динамическая строка Sitemap (только при заданном SITE_URL), а sitemap
// собирается из PlaceCatalog. Должны быть объявлены до MapRazorComponents,
// иначе запрос уйдёт в Razor-роутер и вернёт страницу 404.
app.MapGet("/robots.txt", (SeoUrls seo, HttpContext context) =>
{
    context.Response.Headers.CacheControl = "public, max-age=3600";
    return Results.Text(seo.RobotsTxt, "text/plain", Encoding.UTF8);
});

app.MapGet("/sitemap.xml", (SeoUrls seo, HttpContext context) =>
{
    // Sitemap с относительными URL невалиден — без SITE_URL его просто нет.
    if (!seo.HasSiteUrl)
        return Results.NotFound();

    context.Response.Headers.CacheControl = "public, max-age=3600";
    return Results.Text(seo.SitemapXml, "application/xml", Encoding.UTF8);
});

// ---- чат с посетителем -------------------------------------------------------
// Оба эндпоинта живут на самом сайте, а не на шлюзе: в chat-сервис ходит сервер UI,
// он же владеет cookie с токеном диалога. Объявлены до MapRazorComponents — иначе
// запрос уйдёт в Razor-роутер и вернётся страница 404.

// Форма чата и JS шлют одно и то же тело (application/x-www-form-urlencoded) на один
// адрес: так antiforgery работает штатно и не нужно двух путей кода. Ответ разный —
// JSON для скрипта, редирект для страницы без JavaScript.
app.MapPost("/chat/send", async (HttpContext context, IMediator mediator, IAntiforgery antiforgery) =>
{
    // UseAntiforgery проверяет только эндпоинты с form-binding, а форму мы читаем
    // руками — значит и токен проверяем руками.
    try
    {
        await antiforgery.ValidateRequestAsync(context);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest();
    }

    var form = await context.Request.ReadFormAsync();
    var wantsJson = context.Request.Headers.Accept.ToString().Contains("application/json");
    var back = ChatPaths.WithOpen(form["redirect"].ToString());

    // honeypot: поле спрятано классом (inline style запрещён CSP). Ботам отвечаем
    // «успехом», чтобы не подсказывать обход, но ничего не сохраняем.
    if (!string.IsNullOrEmpty(form["hp"]))
        return wantsJson ? Results.Json(new { ordinal = 0 }) : Results.LocalRedirect(back);

    var text = form["text"].ToString().Trim();

    if (text.Length is 0 or > ChatLimits.MaxTextLength)
        return wantsJson
            ? Results.Json(new { error = "text" }, statusCode: StatusCodes.Status400BadRequest)
            : Results.LocalRedirect(ChatPaths.WithError(form["redirect"].ToString()));

    var result = await mediator.Send(new ChatSendCommand
    {
        SessionToken = context.Request.Cookies[ChatCookie.Name],
        Text = text,
        Culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
        Page = ChatPaths.Clean(form["redirect"].ToString())
    });

    if (!result.IsSuccess)
    {
        var error = result.StatusCode == HttpStatusCode.TooManyRequests ? "toomany" : "failed";

        return wantsJson
            ? Results.Json(new { error }, statusCode: (int)result.StatusCode)
            : Results.LocalRedirect(ChatPaths.WithError(form["redirect"].ToString()));
    }

    // Токен диалога возвращает сервис (он же его и создал) — кладём в cookie ровно
    // так же, как /set-culture и /set-theme: из Razor-компонента cookie не поставить.
    if (result.Value is { Length: > 0 } token && token != context.Request.Cookies[ChatCookie.Name])
        context.Response.Cookies.Append(ChatCookie.Name, token, ChatCookie.Options(context));

    return wantsJson
        ? Results.Json(new { ordinal = (int)(result.Hash ?? 0) })
        : Results.LocalRedirect(back);
});

// Опрос новых сообщений. Ответ зависит от cookie, поэтому no-store и Vary: Cookie —
// иначе ответ одного посетителя мог бы осесть в промежуточном кэше для другого.
app.MapGet("/chat/poll", async (HttpContext context, IMediator mediator, ChatSchedule schedule, int? after) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.Vary = "Cookie";

    var token = context.Request.Cookies[ChatCookie.Name];
    var online = schedule.IsOnline();

    if (string.IsNullOrEmpty(token))
        return Results.Json(new { session = false, online, messages = Array.Empty<object>() });

    IReadOnlyCollection<ChatMessageDto> messages;

    try
    {
        messages = await mediator.Send(new ChatMessageListQuery
        {
            Token = token,
            After = Math.Max(0, after ?? 0),
            Limit = ChatLimits.PageSize
        });
    }
    catch (Exception)
    {
        // chat недоступен — виджет просто повторит опрос позже
        return Results.Json(new { session = true, online, messages = Array.Empty<object>() },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Json(new
    {
        session = true,
        online,
        messages = messages.Select(message => new
        {
            o = message.Ordinal,
            d = (int)message.Direction,
            t = message.Text
        })
    });
});

// Статика с отпечатками содержимого (см. комментарий у UseStaticFiles). Лимитер
// снят явно: эти эндпоинты идут через маршрутизацию, а значит и через глобальный
// лимитер — одна загрузка главной это ~70 запросов, и несколько переходов в минуту
// упирались бы в лимит GET. Без .WithStaticAssets() у MapRazorComponents Assets[...]
// в компонентах не знает отпечатков и возвращает путь как есть.
app.MapStaticAssets().DisableRateLimiting();

// Вход, выход и отметки страницы заявок — до MapRazorComponents, как и чат.
app.MapManager();

app.MapRazorComponents<App>()
    .WithStaticAssets();

app.MapHealthChecks("/health").DisableRateLimiting();

app.Run();
