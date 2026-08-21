using Domain;

namespace Users.Domain;

public record ApplicationEntity : AuditableEntity
{
    public string? Phone { get; set; }

    /// <summary>Код выбранного направления (teriberka / lovozero / tersky).
    /// Хранится кодом, а не названием: переименование маршрута не потребует миграции.</summary>
    public string? Route { get; set; }
}
