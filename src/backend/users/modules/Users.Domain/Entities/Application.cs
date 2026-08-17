using Domain;

namespace Users.Domain;

public record ApplicationEntity : AuditableEntity
{
    public string? Phone { get; set; }
    public int PeopleCount { get; set; }
    public DateOnly ArrivalDate { get; set; }
    public string? Comment { get; set; }
}
