using System.Resources;
using Domain;
using Microsoft.Extensions.Localization;

namespace Api;

public abstract class SharedLocalizer(IIdentityService identityService) : IStringLocalizer
{
    protected virtual ResourceManager ResourceManager => Resources.SharedStrings.ResourceManager;

    public LocalizedString this[string name] =>
        !identityService.IsAltLang
            ? new LocalizedString(GetString(name) ?? Resources.SharedStrings.ResourceManager.GetString(name)!, name)
            : new LocalizedString(name, name);

    public LocalizedString this[string name, params object[] arguments] => throw new NotImplementedException();


    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => throw new NotImplementedException();

    protected abstract string? GetString(string name);
}