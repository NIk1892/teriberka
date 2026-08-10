using Domain;
using Mediator;

namespace Contracts;

public record PagedList<T>(IReadOnlyCollection<T> Items, int Total, int PageIndex, int PageSize)
    where T : IDto;

public abstract class PagedListQuery<T, Tq> : IRequest<PagedList<T>>, IPagedListQuery
    where T : IDto
    where Tq : ListQuery<T>
{
    public Tq Query { get; init; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
}