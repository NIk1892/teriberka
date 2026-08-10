using Domain;
using Mediator;

namespace Contracts;

public interface ICommand : IRequest<ExecuteRequestResult>;

public abstract record Command : ICommand
{
    public string? Title { get; set; }
}


public interface IUpdateCommand : ICommand
{
    public Guid Id { get; set; }
    public uint Xmin { get; set; }
}


public abstract record DeleteCommand : ICommand
{
    public Guid Id { get; set; }
}

public interface IHtmlContent
{
    string? Content { get; set; }
}