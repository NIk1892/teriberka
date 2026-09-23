using Chat.Application.Abstract;
using Chat.Domain;
using Microsoft.EntityFrameworkCore;

namespace Chat.Infrastructure.DataAccess;

/// <summary>
/// Состояние диалогов Telegram-бота. Регистрируется вручную в Configurator: Scrutor
/// сканирует только наследников CommandRepository/QueryRepository.
/// </summary>
public class BotDialogRepository(WriteChatDbContext context) : IBotDialogRepository
{
    private readonly DbSet<BotDialogEntity> _dialogs = context.Set<BotDialogEntity>();

    public Task<BotDialogEntity?> FindByUserIdAsync(long tgUserId, CancellationToken cancellationToken)
        => _dialogs.FirstOrDefaultAsync(d => d.TgUserId == tgUserId && !d.IsDeleted, cancellationToken);

    public BotDialogEntity Create(long tgUserId, long tgChatId, string? lang)
    {
        var dialog = new BotDialogEntity
        {
            TgUserId = tgUserId,
            TgChatId = tgChatId,
            Lang = lang,
            Step = BotDialogStep.Idle,
            LastActivityAt = DateTime.UtcNow
        };

        _dialogs.Add(dialog);

        return dialog;
    }

    public Task<int> ClearAbandonedAsync(DateTime edge, CancellationToken cancellationToken)
        => _dialogs
            .Where(d => d.Step != BotDialogStep.Idle && d.LastActivityAt < edge)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.Step, BotDialogStep.Idle)
                    .SetProperty(d => d.Route, (string?)null)
                    .SetProperty(d => d.DateText, (string?)null)
                    .SetProperty(d => d.People, (int?)null)
                    .SetProperty(d => d.Wishes, (string?)null)
                    .SetProperty(d => d.Name, (string?)null)
                    .SetProperty(d => d.Phone, (string?)null)
                    .SetProperty(d => d.PendingText, (string?)null),
                cancellationToken);

    public Task<int> DeleteInactiveAsync(DateTime edge, CancellationToken cancellationToken)
        => _dialogs
            .Where(d => d.LastActivityAt < edge)
            .ExecuteDeleteAsync(cancellationToken);
}
