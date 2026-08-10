using Infrastructure.Mappers;
using Riok.Mapperly.Abstractions;
using Users.Contracts;
using Users.Domain;


namespace Users.Infrastructure.DataAccess;

[Mapper]
public partial class GroupMemberDtoMapper: IEntityToDtoMapper<GroupMemberDto, GroupMemberEntity>
{
    public partial IQueryable<GroupMemberDto> ToDto(IQueryable<GroupMemberEntity> query);
}

[Mapper]
public partial class GroupMemberAddEntityMapper : ICommandToEntityMapper<GroupMemberEntity, GroupMemberAdd>
{
    public partial GroupMemberEntity ToNewEntity(GroupMemberAdd source);
}
//
// [Mapper]
// public partial class GroupMemberUpdateEntityMapper : ICommandToEntityMapper<GroupMemberEntity, GroupMemberUpdateCommand>
// {
//     public partial GroupMemberEntity ToEntity(GroupMemberUpdateCommand source);
// }


