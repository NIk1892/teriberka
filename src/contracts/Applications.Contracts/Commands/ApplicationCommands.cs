using Contracts;

namespace Applications.Contracts;

/// <summary>
/// Заявка: имя (<see cref="Command.Title"/>, необязательно), телефон и выбранное направление.
/// С сайта приходят только они — дата, состав группы и пожелания уточняются по телефону,
/// поэтому в форме их сознательно нет (решение от 21.08.2026). Telegram-бот ведёт по шагам
/// и то, что узнал (дата, число человек, пожелания, оценка), кладёт одной строкой в
/// <see cref="Details"/> — менеджеру перед звонком, а не отдельными колонками.
/// </summary>
public record ApplicationCreateCommand : Command
{
    public const int MaxDetailsLength = 1000;

    public string? Phone { get; set; }

    /// <summary>Код направления из <see cref="ApplicationRoutes"/>; по умолчанию Териберка.</summary>
    public string? Route { get; set; } = ApplicationRoutes.Teriberka;

    /// <summary>Откуда заявка — код из <see cref="ApplicationSources"/>. Сайт ставит сам, сервером.</summary>
    public string? Source { get; set; } = ApplicationSources.Site;

    /// <summary>Telegram-пользователь, оставивший заявку через бота; с сайта — всегда null.</summary>
    public long? TgUserId { get; set; }

    /// <summary>Username в Telegram без «@» — менеджер сможет написать в ответ; null, если его нет.</summary>
    public string? TgUsername { get; set; }

    /// <summary>Свободный текст для менеджера: дата, число человек, пожелания. Заполняет только бот.</summary>
    public string? Details { get; set; }
}

/// <summary>
/// Отметка менеджера на странице заявок (/manager): обработана ли заявка и комментарий.
/// Правит только эти два поля — имя, телефон и маршрут посетителя менеджер не меняет.
/// Xmin не проверяется (ApplicationProcessCommandRepository): отметку ставит один
/// менеджер за раз, а побеждает последняя правка.
/// </summary>
public record ApplicationProcessCommand : Command, IUpdateCommand
{
    public const int MaxCommentLength = 1000;

    public Guid Id { get; set; }
    public uint Xmin { get; set; }

    public bool Processed { get; set; }

    public string? ManagerComment { get; set; }
}
