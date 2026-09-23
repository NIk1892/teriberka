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
///
/// Здесь же чистятся диалоги Telegram-бота (BotDialogs): брошенный черновик заявки
/// теряет имя, телефон, дату и пожелания через сутки, а строка пользователя, который
/// давно не писал, удаляется целиком. Эта чистка не зависит от CHAT_RETENTION_DAYS —
/// выключить хранение переписки можно, а держать телефоны из недописанных заявок нельзя.
/// </summary>
public sealed class ChatRetentionService(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<ChatRetentionService> logger) : BackgroundService
{
    private const int DefaultRetentionDays = 90;

    /// <summary>Черновик заявки без ответа пользователя дольше этого — обнулить персональные данные.</summary>
    private static readonly TimeSpan AbandonedDraftAge = TimeSpan.FromDays(1);

    /// <summary>Пользователь бота не писал столько — его строка не нужна (счётчики антиспама тоже).</summary>
    private static readonly TimeSpan InactiveDialogAge = TimeSpan.FromDays(30);

    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var days = configuration.GetValue("CHAT_RETENTION_DAYS", DefaultRetentionDays);

        if (days <= 0)
            logger.LogWarning("CHAT_RETENTION_DAYS = {Days} — чистка переписки выключена", days);

        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();

                if (days > 0)
                {
                    var repository = scope.ServiceProvider.GetRequiredService<IChatRepository>();

                    var removed = await repository.DeleteExpiredAsync(DateTime.UtcNow.AddDays(-days), stoppingToken);

                    if (removed > 0)
                        logger.LogInformation("Удалено диалогов старше {Days} дней: {Count}", days, removed);
                }

                var dialogs = scope.ServiceProvider.GetRequiredService<IBotDialogRepository>();

                var cleared = await dialogs.ClearAbandonedAsync(DateTime.UtcNow - AbandonedDraftAge, stoppingToken);
                var deleted = await dialogs.DeleteInactiveAsync(DateTime.UtcNow - InactiveDialogAge, stoppingToken);

                if (cleared > 0 || deleted > 0)
                    logger.LogInformation("Диалоги бота: обнулено брошенных черновиков {Cleared}, удалено неактивных строк {Deleted}",
                        cleared, deleted);
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
