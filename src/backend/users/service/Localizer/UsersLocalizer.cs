using System.Resources;
using Api;
using Auth.Localizer;
using Domain;

namespace Users;

public class UsersLocalizer(IIdentityService identityService) : SharedLocalizer(identityService)
{
    protected override ResourceManager ResourceManager => UsersStrings.ResourceManager;
    protected override string? GetString(string name)
        => ResourceManager.GetString(name);
}