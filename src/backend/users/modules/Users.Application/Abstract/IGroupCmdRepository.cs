using Application;
using Users.Contracts;
using Users.Domain;

namespace Users.Application.Abstract;

public interface IGroupCmdRepository : ICommandRepository<GroupCreateCommand, GroupEntity>
{
      Task AddMemberAsync(Guid groupId, Guid userId);
      Task RemoveMemberAsync(Guid groupId, Guid userId);   
}