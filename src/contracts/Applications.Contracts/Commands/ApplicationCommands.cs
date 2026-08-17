using Contracts;

namespace Applications.Contracts;

public record ApplicationCreateCommand : Command
{
    public string? Phone { get; set; }
    public int PeopleCount { get; set; }
    public DateOnly ArrivalDate { get; set; }

    /// <summary>Пожелания к поездке; заполняется в том числе заявками на индивидуальный тур.</summary>
    public string? Comment { get; set; }
}
