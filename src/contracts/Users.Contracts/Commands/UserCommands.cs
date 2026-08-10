using Contracts;

namespace Users.Contracts;

public record UserCreateCommand : Command
{
    public string? Email { get; set; }
    public string? TgId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int Role { get; set; }
}

public record UserUpdateCommand : UserCreateCommand, IUpdateCommand
{
    public Guid Id { get; set; }
    public uint Xmin { get; set; }
}
public record UserDeleteCommand : DeleteCommand;

