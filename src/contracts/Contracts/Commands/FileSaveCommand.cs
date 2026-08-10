using Domain;
using Mediator;

namespace Contracts;

public record FileSaveCommand() : IRequest<ExecuteRequestResult>
{
    public required string FileName { get; init; }
    public required byte[] File { get; init; }
    public required string ContentType { get; init; }
}