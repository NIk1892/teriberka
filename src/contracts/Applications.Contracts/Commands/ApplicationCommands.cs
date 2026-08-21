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
