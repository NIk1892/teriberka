namespace Domain;

public class Audit
{
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public Guid? ModifiedById { get; set; }
}