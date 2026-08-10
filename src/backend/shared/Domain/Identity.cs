

namespace Domain;

public record Identity : IDto
{
    public string? Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
}