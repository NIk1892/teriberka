
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Api;
using Domain;
using OTLP;
using Teriberka.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.ConfigureOpenTelemetry();

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

builder.Services.AddHttpClient("swagger")
    .AddServiceDiscovery();

builder.Services.AddHealthChecks();

const string uiAppPolicy = "LxpUIAppPolicy";

builder.Services.AddCors(options =>
{
    var origins = builder.Configuration["UI_APP_URL"]?.Split([',']);

    if (origins != null)
    {
        options.AddPolicy(uiAppPolicy, b => b.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
    }
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    app.UseCors(x => x
        .AllowAnyMethod()
        .AllowAnyHeader()
        .SetIsOriginAllowed(origin => true)
        .AllowCredentials());
}
else
{
    app.UseHsts();
    app.UseCors(uiAppPolicy);
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapGatewaySwagger();

app.MapReverseProxy();

app.MapDefaultEndpoints();

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
