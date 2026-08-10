using Api;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using OTLP;
using Teriberka.ServiceDefaults;
using Users;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.ConfigureOpenTelemetry();

var configurator = new Configurator(builder);

configurator.Configure();

var httpPort = builder.Configuration.GetValue("HTTP_PORT", 8080);
var grpcPort = builder.Configuration.GetValue("GRPC_PORT", 8081);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(httpPort, lo => lo.Protocols = HttpProtocols.Http1);
    options.ListenAnyIP(grpcPort, lo => lo.Protocols = HttpProtocols.Http2);
});

var app = builder.Build();

await app.EnsureDataBaseAsync();

configurator.ConfigureApplication(app);

configurator.ConfigureEndPoints(app);

app.MapDefaultEndpoints();

app.Run();

public partial class Program;