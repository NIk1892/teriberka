namespace Domain
{
    public interface IAuditable
    {
        public DateTime? CreatedAt { get; init; }
        public DateTime? ModifiedAt { get; set; }
        public Guid? CreatedById { get; init; }
        public Guid? ModifiedById { get; init; }
    }
}
