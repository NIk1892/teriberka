using Contracts;

namespace Files;

public record FileSaveRequest:ICommand
{
    public IFormFile File { get; init; }
}