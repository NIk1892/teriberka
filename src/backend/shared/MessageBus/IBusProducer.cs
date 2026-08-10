namespace MessageBus;

public interface IBusProducer<in TMessage> where TMessage : IBusEvent
{
    Task PublishAsync(TMessage message, string topic, CancellationToken cancellationToken = default);
}
