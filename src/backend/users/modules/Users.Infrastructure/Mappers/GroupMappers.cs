using Infrastructure.Mappers;
using Riok.Mapperly.Abstractions;
using Users.Contracts;
using Users.Domain;


namespace Users.Infrastructure.DataAccess;

[Mapper]
public partial class GroupDtoMapper: IEntityToDtoMapper<GroupDto, GroupEntity>
{
    public partial IQueryable<GroupDto> ToDto(IQueryable<GroupEntity> query);
}

[Mapper]
public partial class GroupCreateEntityMapper : ICommandToEntityMapper<GroupEntity, GroupCreateCommand>
{
    [MapperIgnoreTarget(nameof(GroupEntity.Audit))]
    public partial GroupEntity ToNewEntity(GroupCreateCommand source);
}

[Mapper]
public partial class GroupUpdateEntityMapper : ICommandToEntityMapper<GroupEntity, GroupUpdateCommand>
{
    public partial GroupEntity ToNewEntity(GroupUpdateCommand source);

    [MapperIgnoreTarget(nameof(GroupEntity.Audit))]
    public partial void ToEntity(GroupUpdateCommand source, GroupEntity target);
}


