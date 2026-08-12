using System.Globalization;
using System.Threading.RateLimiting;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using UI.Public.Web.Components;
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

// Страницы отдаются без JavaScript, поэтому политика может быть предельно жёсткой:
// скрипты запрещены полностью, стили и картинки — только со своего origin,
// отправка форм — только на свой origin, встраивание в iframe запрещено.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'none'; style-src 'self'; img-src 'self' data:; " +
        "form-action 'self'; base-uri 'self'; frame-ancestors 'none'";
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

    await next();
});

// Статика до rate limiter'а: css и картинки не должны сжигать лимит запросов.
app.UseStaticFiles();

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

app.MapRazorComponents<App>();

app.MapHealthChecks("/health").DisableRateLimiting();

app.Run();
