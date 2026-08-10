using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MessageBus;

public static class BusTopicExtensions
{
    public static IServiceCollection AddBusTopics(this IServiceCollection services, params string[] baseTopics)
    {
        services.AddSingleton(new BusTopicOptions { BaseTopics = baseTopics });

        // Реестр заполняется при резолве из DI — до старта любого IHostedService
        services.AddSingleton<BusTopicRegistry>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var version = config["BUS_TOPIC_VERSION"];
            var registry = new BusTopicRegistry();
            foreach (var baseName in baseTopics)
            {
                var fullName = string.IsNullOrEmpty(version) ? baseName : $"{baseName}-{version}";
                registry.Set(baseName, fullName);
            }
            return registry;
        });

        services.AddHostedService<BusTopicInitializer>();
        return services;
    }
}

public sealed class BusTopicOptions
{
    public string[] BaseTopics { get; init; } = [];
}

public sealed class BusTopicRegistry
{
    private readonly Dictionary<string, string> _resolved = new();

    internal void Set(string baseName, string fullName) => _resolved[baseName] = fullName;

    public string Get(string baseName) => _resolved[baseName];
}

public sealed class BusTopicInitializer(
    IConfiguration configuration,
    BusTopicOptions options,
    BusTopicRegistry registry) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var version = configuration["BUS_TOPIC_VERSION"];

        using var adminClient = new AdminClientBuilder(
            new AdminClientConfig { BootstrapServers = configuration["BROKER_HOST"] })
            .Build();

        var toCreate = options.BaseTopics.Select(baseName =>
        {
            var fullName = string.IsNullOrEmpty(version) ? baseName : $"{baseName}-{version}";
            registry.Set(baseName, fullName);
            return new TopicSpecification { Name = fullName, NumPartitions = 1, ReplicationFactor = 1 };
        }).ToList();

        try
        {
            await adminClient.CreateTopicsAsync(toCreate);
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r =>
            r.Error.Code is ErrorCode.TopicAlreadyExists or ErrorCode.NoError))
        {
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
