using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessageBus;

public abstract class BusConsumer<TMessage>(
    IConfiguration configuration,
    ILogger logger,
    BusTopicRegistry topicRegistry,
    string baseTopic,
    string groupId) : BackgroundService
    where TMessage : class, IBusEvent
{
    private readonly ConsumerConfig _consumerConfig = new()
    {
        BootstrapServers = configuration["BROKER_HOST"],
        GroupId = groupId,
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = true,
        EnableAutoOffsetStore = false
    };

    protected abstract Task HandleAsync(TMessage message, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Резолвим здесь, после того как BusTopicInitializer.StartAsync() заполнил реестр
        var topic = topicRegistry.Get(baseTopic);

        using var consumer = new ConsumerBuilder<string, string>(_consumerConfig).Build();
        consumer.Subscribe(topic);

        logger.LogInformation("Consumer started, listening to '{Topic}'", topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;
                try
                {
                    result = consumer.Consume(stoppingToken);
                    if (result?.Message?.Value is null) continue;

                    var message = JsonSerializer.Deserialize<TMessage>(result.Message.Value);
                    if (message is null)
                    {
                        logger.LogWarning("Skipping undeserializable message at {Offset}", result.TopicPartitionOffset);
                        consumer.StoreOffset(result);
                        continue;
                    }

                    await HandleAsync(message, stoppingToken);
                    consumer.StoreOffset(result);
                }
                catch (ConsumeException ex)
                {
                    logger.LogError(ex, "Consume error at {Offset}", ex.ConsumerRecord?.TopicPartitionOffset);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process message at {Offset}", result?.TopicPartitionOffset);
                    // poison pill: пропускаем сообщение, чтобы не зависнуть в бесконечном цикле
                    if (result is not null)
                        consumer.StoreOffset(result);
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }
}
