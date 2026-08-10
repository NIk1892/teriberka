namespace Domain;

public interface IIdentityService
{
    public Guid UserId { get; }
    public string Role { get; }
    public bool IsAuthenticated { get; }
    public bool IsAltLang { get; }
    public Identity? GetIdentity();
}