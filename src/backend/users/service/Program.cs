using Api;
using OTLP;
using Users;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureOpenTelemetry();

var configurator = new Configurator(builder);

configurator.Configure();

var app = builder.Build();

await app.EnsureDataBaseAsync();

configurator.ConfigureApplication(app);

configurator.ConfigureEndPoints(app);

app.Run();

public partial class Program;
