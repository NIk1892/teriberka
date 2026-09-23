using Domain;

namespace Chat.Domain;

/// <summary>
/// Где пользователь в личке бота. Значения хранятся числом — переименование шага не
/// требует миграции, а порядок Route..Summary — это и порядок мастера заявки.
/// </summary>
public enum BotDialogStep
{
    /// <summary>Ничего не происходит: меню, инфо-экраны.</summary>
    Idle = 0,

    Route = 1,
    Date = 2,
    People = 3,
    Wishes = 4,
    Name = 5,
    Phone = 6,
    Summary = 7,

    /// <summary>Режим «написать менеджеру»: любой текст уходит в группу гидов.</summary>
    Chat = 8
}

/// <summary>
/// Состояние диалога с пользователем Telegram — одна строка на пользователя. Хранится в
/// БД, а не в памяти: сервис перезапускается на каждом деплое, а черновик заявки посреди
/// ввода телефона терять нельзя. Апдейты бот обрабатывает последовательно, поэтому две
/// правки одной строки одновременно не случаются.
///
/// Персональные данные (имя, телефон, дата, пожелания, отложенный вопрос) обнуляются
/// сразу после отправки заявки, по отмене и у брошенных черновиков через сутки; сама
/// строка живёт, пока пользователь активен (см. ChatRetentionService).
/// </summary>
public record BotDialogEntity : AuditableEntity
{
    /// <summary>Id пользователя Telegram; в личке совпадает с id чата.</summary>
    public long TgUserId { get; set; }

    public long TgChatId { get; set; }

    /// <summary>Язык клиента Telegram при последнем апдейте (ru/en/zh).</summary>
    public string? Lang { get; set; }

    public BotDialogStep Step { get; set; }

    /// <summary>Код направления из ApplicationRoutes.</summary>
    public string? Route { get; set; }

    /// <summary>Дата как написал пользователь; «?» — «пока не знаю».</summary>
    public string? DateText { get; set; }

    /// <summary>Число человек; 9 — «больше 8».</summary>
    public int? People { get; set; }

    /// <summary>Пожелания; пустая строка — шаг пропущен.</summary>
    public string? Wishes { get; set; }

    public string? Name { get; set; }

    public string? Phone { get; set; }

    /// <summary>Свободный текст, который ждёт подтверждения «передать менеджеру».</summary>
    public string? PendingText { get; set; }

    /// <summary>Последний экран бота с inline-кнопками — чтобы снять с него клавиатуру после текстового шага.</summary>
    public int? ScreenMessageId { get; set; }

    /// <summary>Последний апдейт от пользователя; по нему чистятся брошенные черновики и старые строки.</summary>
    public DateTime LastActivityAt { get; set; }

    /// <summary>Когда ушла последняя заявка — пауза между заявками.</summary>
    public DateTime? LastSubmittedAt { get; set; }

    /// <summary>Сутки (UTC) и число заявок за них — дневной потолок.</summary>
    public DateOnly? SubmitDayUtc { get; set; }

    public int SubmitDayCount { get; set; }

    /// <summary>Есть ли что-то заполненное в мастере (независимо от Step — режим чата черновик не стирает).</summary>
    public bool HasDraft =>
        Route is not null || DateText is not null || People is not null
        || Wishes is not null || Name is not null || Phone is not null;

    public bool IsBooking => Step is >= BotDialogStep.Route and <= BotDialogStep.Summary;

    /// <summary>Стирает всё персональное; Step не трогает — это решает вызывающий.</summary>
    public void ClearDraft()
    {
        Route = null;
        DateText = null;
        People = null;
        Wishes = null;
        Name = null;
        Phone = null;
        PendingText = null;
    }
}
