using Domain;

namespace Users.Domain
{
    public record GroupEntity : AuditableEntity
    {
        public GroupType Type { get; set; }
        public Guid? ParentGroupId { get; set; }
        public GroupEntity? ParentGroup { get; set; }
    }
}
