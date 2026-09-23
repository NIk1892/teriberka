using Application;
using Chat.Domain;

namespace Chat.Application.Abstract;

/// <summary>
/// Состояние диалогов Telegram-бота. Свой репозиторий, а не generic: строка одна на
/// пользователя, создаётся и правится по ходу диалога, а чистка — массовыми запросами.
/// </summary>
public interface IBotDialogRepository : IUnitOfWorkRepository
{
    Task<BotDialogEntity?> FindByUserIdAsync(long tgUserId, CancellationToken cancellationToken);

    /// <summary>Заводит строку для нового пользователя; сохраняется общим CommitAsync.</summary>
    BotDialogEntity Create(long tgUserId, long tgChatId, string? lang);

    /// <summary>
    /// Брошенные диалоги (не Idle, без активности с <paramref name="edge"/>): обнулить
    /// персональные данные и вернуть в Idle. Строка остаётся — в ней счётчики антиспама.
    /// </summary>
    Task<int> ClearAbandonedAsync(DateTime edge, CancellationToken cancellationToken);

    /// <summary>Удаляет строки пользователей, не писавших с <paramref name="edge"/>. Жёстко — там были ПД.</summary>
    Task<int> DeleteInactiveAsync(DateTime edge, CancellationToken cancellationToken);
}
