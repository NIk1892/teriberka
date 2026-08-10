using Contracts;

namespace News.Contracts;

public record TagDto : AuditableDto
{
    public string? Url { get; init; }
}


