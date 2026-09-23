using Domain;

namespace Users.Domain;

public record ApplicationEntity : AuditableEntity
{
    public string? Phone { get; set; }

    /// <summary>Код выбранного направления (teriberka / lovozero / tersky / custom).
    /// Хранится кодом, а не названием: переименование маршрута не потребует миграции.</summary>
    public string? Route { get; set; }

    /// <summary>Откуда заявка: site / telegram (ApplicationSources). Старые строки — site по умолчанию колонки.</summary>
    public string? Source { get; set; }

    /// <summary>Telegram-пользователь, если заявка оставлена через бота.</summary>
    public long? TgUserId { get; set; }

    /// <summary>Username в Telegram без «@» — менеджер может ответить в мессенджере.</summary>
    public string? TgUsername { get; set; }

    /// <summary>Свободный текст от бота: дата, число человек, пожелания, названная оценка.</summary>
    public string? Details { get; set; }

    /// <summary>Id отбивки в Telegram-канале менеджеров; null — заявка туда ещё не ушла
    /// (по нему её и подбирает ApplicationNotifier).</summary>
    public long? TgMessageId { get; set; }

    /// <summary>Когда менеджер отметил заявку обработанной на странице /manager; null — новая.</summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>Комментарий менеджера: о чём договорились, когда перезвонить.</summary>
    public string? ManagerComment { get; set; }
}
