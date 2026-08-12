using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;


namespace OTLP;

public static class OtlpHelper
{
    public static WebApplicationBuilder ConfigureOpenTelemetry(this WebApplicationBuilder builder)
    {
        var minLevelStr = builder.Configuration["LOGGING_MIN_LEVEL"];

        if (int.TryParse(minLevelStr, out var minLevelInt) &&
            Enum.IsDefined(typeof(LogLevel), minLevelInt))
        {
            builder.Logging.SetMinimumLevel((LogLevel)minLevelInt);
        }

        builder.Services.AddSingleton(TracerProvider.Default.GetTracer(builder.Environment.ApplicationName));

        if (!bool.TryParse(builder.Configuration["OTLP_EXPORT_ENABLED"], out var enabled) ||
            !enabled)
        {
            Console.WriteLine("OpenTelemetry exporting is disabled");
            return builder;
        }

        var otlpHttpEndPoint = builder.Configuration["OTLP_RECEIVER_ENDPOINT_HTTP"];

        if (!string.IsNullOrEmpty(otlpHttpEndPoint))
        {
            builder.Logging.AddOpenTelemetry(x =>
            {
                x.IncludeScopes = true;
                x.IncludeFormattedMessage = true;
                x.AddOtlpExporter(options =>
                {
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    options.Endpoint = new Uri($"{otlpHttpEndPoint}/v1/logs");
                });
            });
        }
        else
        {
            builder.Logging.AddConsole();
        }

        var otlpGrpcEndPoint = builder.Configuration["OTLP_RECEIVER_ENDPOINT_GRPC"]!;

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName: builder.Environment.ApplicationName,
                serviceVersion: "1.0.0",
                serviceInstanceId: Environment.MachineName))
            .WithMetrics(metric => metric
                .AddAspNetCoreInstrumentation()
                .AddProcessInstrumentation()
                .AddRuntimeInstrumentation()
                .AddSqlClientInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsqlInstrumentation()
                .AddOtlpExporter(options => { options.Endpoint = new Uri(otlpGrpcEndPoint); }))
            .WithTracing(
                tracing =>
                    tracing.AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddEntityFrameworkCoreInstrumentation(x => x.SetDbStatementForText = true)
                        .AddOtlpExporter(options => { options.Endpoint = new Uri(otlpGrpcEndPoint); })
            );

        return builder;
    }
}