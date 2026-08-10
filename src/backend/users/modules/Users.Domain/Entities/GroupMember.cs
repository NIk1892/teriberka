using Domain;

namespace Users.Domain;

public record GroupMemberEntity : Entity
{
    public Guid UserId { get; init; }
    public UserEntity? User { get; init; }
    public Guid GroupId { get; init; }
    public GroupEntity? Group { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateOnly? DueDateAt { get; set; }
}