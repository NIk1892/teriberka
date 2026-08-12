using System.Threading.RateLimiting;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.HttpOverrides;
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
// скрипты запрещены полностью, отправка форм и загрузка ресурсов — только со своего
// origin, встраивание в iframe запрещено.
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

app.UseRateLimiter();

app.UseAntiforgery();

app.MapRazorComponents<App>();

app.MapHealthChecks("/health").DisableRateLimiting();

app.Run();
