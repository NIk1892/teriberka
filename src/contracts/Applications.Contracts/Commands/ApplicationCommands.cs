using Contracts;

namespace Applications.Contracts;

public record ApplicationCreateCommand : Command
{
    public string? Phone { get; set; }
    public int PeopleCount { get; set; }
    public DateOnly ArrivalDate { get; set; }
}
