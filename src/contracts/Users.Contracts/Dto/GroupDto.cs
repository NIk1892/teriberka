using Contracts;
using Users.Domain;

namespace Users.Contracts;

public record GroupDto : AuditableDto
{
    public GroupType Type { get; set; }
}


