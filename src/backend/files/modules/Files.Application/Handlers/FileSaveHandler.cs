using Contracts;
using Domain;
using Files.Domain;
using Mediator;

namespace Files.Application.Handlers;

public class FileSaveHandler(
    IFileService fileService) : IRequestHandler<FileSaveCommand, ExecuteRequestResult>
{
    public async ValueTask<ExecuteRequestResult> Handle(FileSaveCommand request, CancellationToken cancellationToken)
    {
        return new (
            Value: await fileService.SaveAsync(request.FileName, request.File, cancellationToken));
    }
}
    