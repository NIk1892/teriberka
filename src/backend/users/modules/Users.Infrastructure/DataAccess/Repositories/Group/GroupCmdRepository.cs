using Infrastructure;
using Infrastructure.DataAccess;
using Infrastructure.Mappers;
using Users.Application.Abstract;
using Users.Contracts;
using Users.Domain;

namespace Users.Infrastructure.DataAccess;

public class GroupCmdRepository(WriteApplicationDbContext dbContext, ICommandToEntityMapper<GroupEntity, GroupCreateCommand> mapper) :CommandRepository<GroupCreateCommand,GroupEntity>(dbContext, mapper),IGroupCmdRepository
{
    public Task AddMemberAsync(Guid groupId, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task RemoveMemberAsync(Guid groupId, Guid userId)
    {
        throw new NotImplementedException();
    }
}