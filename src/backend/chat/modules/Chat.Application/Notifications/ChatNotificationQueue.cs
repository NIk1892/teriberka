using System.Threading.Channels;

namespace Chat.Application.Notifications;

/// <summary>
/// Мостик «сообщение сохранено → отнести его в Telegram». Абстракция намеренно
/// Telegram-free: пакет Telegram.Bot подключён только к проекту сервиса, доменный
/// слой про мессенджер знать не должен.
/// </summary>
public interface IChatNotificationQueue
{
    void Enqueue(Guid messageId);

    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Очередь в памяти процесса. Отправка в Telegram — сетевой вызов с ретраями, держать
/// на нём POST посетителя нельзя, а падение мессенджера не должно превращаться в ошибку
/// отправки сообщения.
///
/// Потеря очереди при рестарте не теряет сообщений: недоставленное видно в самой БД
/// (Visitor-сообщения с TgMessageId IS NULL), диспетчер подметает их отдельно.
/// </summary>
public sealed class ChatNotificationQueue : IChatNotificationQueue
{
    private const int Capacity = 200;

    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(Capacity)
        {
            // Переполнение очереди не должно блокировать запрос посетителя: отброшенное
            // сообщение всё равно лежит в БД и уедет подметанием outbox'а.
            FullMode = BoundedChannelFullMode.DropWrite
        });

    public void Enqueue(Guid messageId) => _channel.Writer.TryWrite(messageId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
