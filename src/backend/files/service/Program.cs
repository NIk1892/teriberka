using Api;
using OTLP;
using Files;
using Teriberka.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.ConfigureOpenTelemetry();

var configurator = new Configurator(builder);

configurator.Configure();

var app = builder.Build();

configurator.ConfigureApplication(app);

configurator.ConfigureEndPoints(app);

app.MapDefaultEndpoints();

app.Run();