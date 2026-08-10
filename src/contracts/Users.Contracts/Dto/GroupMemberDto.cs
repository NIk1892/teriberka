using Contracts;

namespace Users.Contracts;

public record GroupMemberDto : Dto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public UserDto? User { get; init; }
    public Guid GroupId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateOnly? DueDateAt { get; set; }
}