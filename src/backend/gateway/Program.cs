
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Api;
using Domain;
using OTLP;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureOpenTelemetry();

// Версия сервера в заголовках ответа помогает только тому, кто ищет уязвимости под конкретный стек.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// За реверс-прокси (nginx, Cloudflare, ingress) настоящий IP клиента и схема приходят
// в X-Forwarded-*. Без этого rate limiting считал бы всех клиентов одним IP прокси.
// Включать только когда сервис реально стоит за доверенным прокси и недоступен напрямую.
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

builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuer = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JWT_ISSUER"],
        ValidateAudience = false,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT_KEY"]!))
    };
});

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("Admin", policy => policy
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .RequireClaim(ClaimTypes.Role,
            ((int)UserRole.Admin).ToString(),
            ((int)UserRole.SuperAdmin).ToString()));
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHttpClient("swagger");

builder.Services.AddHealthChecks();

// Публичный POST заявки открыт всем, поэтому его нужно ограничивать по IP: иначе базу
// засыпают спамом. Политика назначается маршруту через "RateLimiterPolicy" в appsettings.
const string publicFormPolicy = "public-form";

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(publicFormPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0
        }));

    // Страховка от простого флуда по остальным путям.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 300,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
});

// Браузер в API не ходит (страницы рендерит сервер UI), поэтому CORS здесь — только
// задел на будущий клиентский код: строгий белый список из UI_APP_URL, без credentials,
// чтобы cookie нельзя было переслать кросс-доменно. Список пуст — CORS не включается вовсе.
const string uiCorsPolicy = "ui-app";

var uiOrigins = builder.Configuration["UI_APP_URL"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

if (uiOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddPolicy(uiCorsPolicy, policy => policy
        .WithOrigins(uiOrigins)
        .WithMethods("GET", "POST", "PUT", "DELETE")
        .WithHeaders("Content-Type", "Authorization")));
}

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

if (useForwardedHeaders)
    app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseHsts();
}

app.UseRouting();

if (uiOrigins.Length > 0)
    app.UseCors(uiCorsPolicy);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapGatewaySwagger();

app.MapReverseProxy();

app.MapHealthChecks("/health").DisableRateLimiting();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/dev/token", (IConfiguration config) =>
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(config["JWT_KEY"]!);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("userId", Guid.Empty.ToString()),
            new(JwtRegisteredClaimNames.Name, "Dev Admin"),
            new(ClaimTypes.Role, ((int)UserRole.SuperAdmin).ToString()),
            new("email", "dev@localhost"),
            new("firstName", "Dev"),
            new("lastName", "Admin"),
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(30),
            Issuer = config["JWT_ISSUER"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
        };

        var token = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
        return Results.Ok(new { token });
    }).AllowAnonymous();
}

app.Run();

public partial class Program { }
