using Domain;

namespace Users.Domain
{
    public record UserEntity : AuditableEntity
    {
        public string? Email { get; set; }
        public string? TgId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? MiddleName { get; set; }
        public UserRole Role { get; set; }
        public string? PasswordHash { get; set; }
        public string? TotpSecret { get; set; }
    }
}
