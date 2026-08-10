using Domain;
using Mediator;

namespace Contracts;

public abstract class Query<T> : IRequest<T>, ISingleQuery
    where T : IDto
{
    public Guid? Id { get; init; }
    public string? Url { get; init; }

    protected Query()
    {

    }

    protected Query(Guid id) => Id = id;
}