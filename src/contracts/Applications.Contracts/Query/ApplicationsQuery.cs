using Contracts;

namespace Applications.Contracts;

public class ApplicationListQuery : ListQuery<ApplicationDto>
{
    /// <summary>Фильтр страницы заявок: <see cref="ApplicationStatuses"/>; пусто — все.</summary>
    public string? Status { get; set; }
}

/// <summary>
/// Страница заявок (/manager) читает постранично: список общего конвейера отдаёт
/// не больше 30 записей за раз, а Total нужен для счётчиков вкладок.
/// </summary>
public class ApplicationPagedListQuery : PagedListQuery<ApplicationDto, ApplicationListQuery>;

public static class ApplicationStatuses
{
    /// <summary>Без отметки менеджера.</summary>
    public const string New = "new";

    /// <summary>Отмечена обработанной.</summary>
    public const string Done = "done";
}
