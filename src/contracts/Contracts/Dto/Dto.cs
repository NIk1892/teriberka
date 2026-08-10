using Domain;

namespace Contracts
{
    public record Dto : IDto
    {
        public Guid Id { get; init; }
        public string? Title { get; init; }
        public string? ImagePath { get; set; }
        public uint Xmin { get; init; }
    }
}
