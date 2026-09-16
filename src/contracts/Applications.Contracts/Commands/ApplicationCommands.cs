using Contracts;

namespace Applications.Contracts;

/// <summary>
/// Заявка с сайта: имя (<see cref="Command.Title"/>, необязательно), телефон и
/// выбранное направление. Остальные детали — дата, состав группы, пожелания —
/// уточняются по телефону, поэтому в форме их сознательно нет (решение от 21.08.2026).
/// </summary>
public record ApplicationCreateCommand : Command
{
    public string? Phone { get; set; }

    /// <summary>Код направления из <see cref="ApplicationRoutes"/>; по умолчанию Териберка.</summary>
    public string? Route { get; set; } = ApplicationRoutes.Teriberka;
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
