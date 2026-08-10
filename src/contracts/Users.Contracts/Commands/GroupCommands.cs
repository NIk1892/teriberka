using Contracts;
using Users.Domain;

namespace Users.Contracts;

public record GroupCreateCommand : Command
{
    public GroupType Type { get; set; }
}

public record GroupUpdateCommand : GroupCreateCommand, IUpdateCommand
{
    public Guid Id { get; set; }
    public uint Xmin { get; set; }
}
public record GroupDeleteCommand : DeleteCommand;

public record GroupMemberAdd(Guid GroupId, Guid UserId) : Command;
public record GroupMemberRemove(Guid GroupId, Guid UserId) : Command;

