namespace Domain
{
    public record Entity : IEntity, IDeletable
    {
        public Guid Id { get; init; }
        public string? Title { get; set; }
        public uint Xmin { get; init; }
        public bool IsDeleted { get; init; }
    }
}
