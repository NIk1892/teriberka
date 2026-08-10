namespace Domain
{
    public record AuditableEntity : Entity
    {
        public Audit? Audit { get; set; }
    }
}
