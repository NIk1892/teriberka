using Contracts;

namespace Applications.Contracts;

public record ApplicationDto : AuditableDto
{
    public string? Phone { get; init; }

    /// <summary>Код направления из <see cref="ApplicationRoutes"/>.</summary>
    public string? Route { get; init; }

    /// <summary>Откуда заявка — код из <see cref="ApplicationSources"/>.</summary>
    public string? Source { get; init; }

    /// <summary>Telegram-пользователь, если заявка из бота.</summary>
    public long? TgUserId { get; init; }

    /// <summary>Username в Telegram без «@», если он есть у пользователя бота.</summary>
    public string? TgUsername { get; init; }

    /// <summary>Что бот узнал по шагам: дата, число человек, пожелания.</summary>
    public string? Details { get; init; }

    /// <summary>Когда менеджер отметил заявку обработанной; null — новая.</summary>
    public DateTime? ProcessedAt { get; init; }

    public string? ManagerComment { get; init; }

    /// <summary>Id отбивки в Telegram-канале; null — заявка в канал не ушла.</summary>
    public long? TgMessageId { get; init; }
}
