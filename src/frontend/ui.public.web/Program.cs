using FluentValidation;
using Mediator;
using Teriberka.ServiceDefaults;
using UI.Public.Web.Components;
using UI.Shared;
using UI.Shared.Interceptors;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents();

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

builder.Services.AddScoped(_ => new FileService(builder.Configuration["S3_URL"]!));
builder.Services.AddScoped(_ => new ConfigService(builder.Configuration["API_URL"]!));

var assemblies = AppDomain.CurrentDomain.GetAssemblies();
builder.Services.AddValidatorsFromAssemblies(assemblies);

builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseAntiforgery();

app.MapRazorComponents<App>();

app.MapDefaultEndpoints();

app.Run();
