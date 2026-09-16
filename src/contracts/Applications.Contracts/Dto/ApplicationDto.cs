using Contracts;

namespace Applications.Contracts;

public record ApplicationDto : AuditableDto
{
    public string? Phone { get; init; }

    /// <summary>Код направления из <see cref="ApplicationRoutes"/>.</summary>
    public string? Route { get; init; }

    /// <summary>Когда менеджер отметил заявку обработанной; null — новая.</summary>
    public DateTime? ProcessedAt { get; init; }

    public string? ManagerComment { get; init; }

    /// <summary>Id отбивки в Telegram-канале; null — заявка в канал не ушла.</summary>
    public long? TgMessageId { get; init; }
}
