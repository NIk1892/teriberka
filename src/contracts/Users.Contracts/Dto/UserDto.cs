using Contracts;

namespace Users.Contracts;

public record UserDto : AuditableDto
{
    public string? Email { get; init; }
    public string? TgId { get; set; }
    public string? FirstName { get; init; } 
    public string? LastName { get; init; }
    public string? MiddleName { get; init; }
    public int Role { get; init; }
}


