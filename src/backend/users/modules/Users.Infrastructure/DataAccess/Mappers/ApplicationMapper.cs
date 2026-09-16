using Applications.Contracts;
using Infrastructure.Mappers;
using Riok.Mapperly.Abstractions;
using Users.Domain;

namespace Users.Infrastructure.DataAccess;

[Mapper]
public partial class ApplicationDtoMapper : IEntityToDtoMapper<ApplicationDto, ApplicationEntity>
{
    public partial IQueryable<ApplicationDto> ToDto(IQueryable<ApplicationEntity> query);
}

[Mapper]
public partial class ApplicationCreateEntityMapper : ICommandToEntityMapper<ApplicationEntity, ApplicationCreateCommand>
{
    [MapperIgnoreTarget(nameof(ApplicationEntity.Audit))]
    [MapperIgnoreTarget(nameof(ApplicationEntity.TgMessageId))]
    [MapperIgnoreTarget(nameof(ApplicationEntity.ProcessedAt))]
    [MapperIgnoreTarget(nameof(ApplicationEntity.ManagerComment))]
    public partial ApplicationEntity ToNewEntity(ApplicationCreateCommand source);
}

/// <summary>
/// Отметка менеджера. Руками, а не Mapperly: из команды берутся ровно два поля, а время
/// обработки ставится сервером — повторное «обработано» не сдвигает уже стоящую отметку.
/// Имя класса обязано кончаться на EntityMapper — по нему маппер регистрируется.
/// </summary>
public class ApplicationProcessEntityMapper : ICommandToEntityMapper<ApplicationEntity, ApplicationProcessCommand>
{
    public ApplicationEntity ToNewEntity(ApplicationProcessCommand source)
        => throw new NotSupportedException("Отметка менеджера не создаёт заявку");

    public void ToEntity(ApplicationProcessCommand source, ApplicationEntity target)
    {
        target.ProcessedAt = source.Processed ? target.ProcessedAt ?? DateTime.UtcNow : null;
        target.ManagerComment = string.IsNullOrWhiteSpace(source.ManagerComment) ? null : source.ManagerComment.Trim();
    }
}
