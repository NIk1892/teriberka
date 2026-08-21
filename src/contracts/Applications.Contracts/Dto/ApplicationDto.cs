using Contracts;

namespace Applications.Contracts;

public record ApplicationDto : AuditableDto
{
    public string? Phone { get; init; }

    /// <summary>Код направления из <see cref="ApplicationRoutes"/>.</summary>
    public string? Route { get; init; }
}
