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
using UI.Public.Web.Features.Chat;
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
// origin, встраивание в iframe запрещено. Скриптов на сайте минимум — сейчас это
// только water.js (WebGL-вода у футера).
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; " +
        "form-action 'self'; base-uri 'self'; frame-ancestors 'none'";
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

    await next();
});

// URL, не совпавший ни с одним @page (например, /qwerty), не доходит до Blazor-роутера
// и отдал бы голый 404 без тела — re-execute рендерит человеку страницу NotFoundView.
// Внутри неё guard по уже выставленному коду не даёт зациклить Navigation.NotFound().
app.UseStatusCodePagesWithReExecute("/not-found");

app.UseResponseCompression();

// Статика до rate limiter'а: css и картинки не должны сжигать лимит запросов.
// Кэш: css/js — час (fingerprinting нет, при деплое стили должны подтянуться
// быстро; ETag делает повторную проверку дешёвой — 304), картинки — 30 дней
// (webp меняются только вместе с новым файлом).
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

app.MapRazorComponents<App>();

app.MapHealthChecks("/health").DisableRateLimiting();

app.Run();
