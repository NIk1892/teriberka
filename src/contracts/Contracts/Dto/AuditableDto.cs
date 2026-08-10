
using Domain;

namespace Contracts;

public record AuditableDto : Dto
{
    public Audit? Audit { get; init; }
}