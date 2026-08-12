using Contracts;

namespace Applications.Contracts;

public record ApplicationDto : AuditableDto
{
    public string? Phone { get; init; }
    public int PeopleCount { get; init; }
    public DateOnly ArrivalDate { get; init; }
}
