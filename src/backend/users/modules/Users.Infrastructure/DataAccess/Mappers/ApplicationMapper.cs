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
    public partial ApplicationEntity ToNewEntity(ApplicationCreateCommand source);
}
