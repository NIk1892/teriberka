using Chat.Application.Abstract;

namespace Chat;

/// <summary>
/// Удаляет переписку, которую пора забыть. Срок — CHAT_RETENTION_DAYS (по умолчанию 90 дней
/// с последнего сообщения диалога).
///
/// Удаление именно жёсткое, вопреки общему для проекта мягкому: в чате лежат персональные
/// данные, которые посетитель писал свободным текстом, и IsDeleted = true оставил бы их
/// в базе навсегда. Копию в Telegram это не убирает — Bot API не даёт удалять сообщения
/// старше 48 часов, о чём должна честно говорить политика конфиденциальности.
/// </summary>
public sealed class ChatRetentionService(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<ChatRetentionService> logger) : BackgroundService
{
    private const int DefaultRetentionDays = 90;

    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var days = configuration.GetValue("CHAT_RETENTION_DAYS", DefaultRetentionDays);

        if (days <= 0)
        {
            logger.LogWarning("CHAT_RETENTION_DAYS = {Days} — чистка переписки выключена", days);
            return;
        }

        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IChatRepository>();

                var removed = await repository.DeleteExpiredAsync(DateTime.UtcNow.AddDays(-days), stoppingToken);

                if (removed > 0)
                    logger.LogInformation("Удалено диалогов старше {Days} дней: {Count}", days, removed);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Не удалось удалить устаревшую переписку, повторю через сутки");
            }
        } while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
