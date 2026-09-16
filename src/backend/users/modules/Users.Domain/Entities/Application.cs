using Domain;

namespace Users.Domain;

public record ApplicationEntity : AuditableEntity
{
    public string? Phone { get; set; }

    /// <summary>Код выбранного направления (teriberka / lovozero / tersky).
    /// Хранится кодом, а не названием: переименование маршрута не потребует миграции.</summary>
    public string? Route { get; set; }

    /// <summary>Id отбивки в Telegram-канале менеджеров; null — заявка туда ещё не ушла
    /// (по нему её и подбирает ApplicationNotifier).</summary>
    public long? TgMessageId { get; set; }
}
