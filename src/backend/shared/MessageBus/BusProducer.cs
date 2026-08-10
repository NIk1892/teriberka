using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;

namespace MessageBus;

public sealed class BusProducer<TMessage>(IConfiguration configuration)
    : IBusProducer<TMessage>, IDisposable
    where TMessage : IBusEvent
{
    private readonly IProducer<string, string> _producer = new ProducerBuilder<string, string>(
        new ProducerConfig
        {
            BootstrapServers = configuration["BROKER_HOST"]
        }).Build();

    public async Task PublishAsync(TMessage message, string topic, CancellationToken cancellationToken = default)
    {
        var kafkaMessage = new Message<string, string>
        {
            Key = Guid.NewGuid().ToString(),
            Value = JsonSerializer.Serialize(message)
        };

        await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
    }

    public void Dispose() => _producer.Dispose();
}
