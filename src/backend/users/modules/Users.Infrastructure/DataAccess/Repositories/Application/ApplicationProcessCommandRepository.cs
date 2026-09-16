using Applications.Contracts;
using Infrastructure.DataAccess;
using Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;
using Users.Domain;

namespace Users.Infrastructure.DataAccess;

/// <summary>
/// Отметка менеджера пишется «последний прав»: без проверки Xmin. Базовый репозиторий
/// сверяет версию строки, а она меняется не только от людей — отбивка в Telegram
/// (ApplicationNotifier) тоже сохраняет заявку. Карточка, открытая до отправки отбивки,
/// ловила бы конфликт на ровном месте, а повторная отметка без перезагрузки страницы —
/// всегда. Scrutor подхватывает наследника сам, регистрировать не нужно.
/// </summary>
public class ApplicationProcessCommandRepository(
    WriteApplicationDbContext context,
    ICommandToEntityMapper<ApplicationEntity, ApplicationProcessCommand> mapper)
    : CommandRepository<ApplicationProcessCommand, ApplicationEntity>(context, mapper)
{
    public override async Task<uint> UpdateAsync(Guid entityId, ApplicationProcessCommand command,
        CancellationToken cancellationToken)
    {
        var entity = await GetExistedEntity(entityId, cancellationToken);
        if (entity is null)
            return 0;

        mapper.ToEntity(command, entity);

        entity.Audit ??= new global::Domain.Audit();
        entity.Audit.ModifiedAt = DateTime.UtcNow;

        return entity.Xmin;
    }

    // Удалённую заявку отметить нельзя.
    protected override Task<ApplicationEntity?> GetExistedEntity(Guid entityId, CancellationToken cancellationToken)
        => DbSet.FirstOrDefaultAsync(x => x.Id == entityId && !x.IsDeleted, cancellationToken);
}
