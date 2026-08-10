using Domain;
using Mediator;

namespace Contracts;



public abstract class ListQuery<T> : IRequest<IReadOnlyCollection<T>> ,IRequestQuery
    where T : IDto
{
    public int? Limit { get; init; }
    public int? Offset { get; init; }
    public bool? SimpleQuery { get; init; }
    public string? Text { get; set; }
    public string? Sorting { get; set; }
}


