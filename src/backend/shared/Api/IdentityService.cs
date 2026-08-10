using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using Contracts;
using Domain;
using Microsoft.AspNetCore.Http;

namespace Api;

public class IdentityService : IIdentityService
{
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly string? _bearer;

    public Guid UserId
    {
        get
        {
            var jwt = ReadJwt();

            return jwt != null && jwt.Payload.TryGetValue("userId", out var uid) ? new Guid(uid.ToString()!) : Guid.Empty;
        }
    }


    public string? Role
    {
        get
        {
            var jwt = ReadJwt();

            return jwt != null && jwt.Payload.ContainsKey("role") ? jwt.Payload["role"].ToString() : null;
        }
    }

    public bool IsAuthenticated => UserId != Guid.Empty;
    
    public bool IsAltLang
    {
        get
        {
            return false;
        }
    }

    public IdentityService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;

        if (AuthenticationHeaderValue.TryParse(_httpContextAccessor?.HttpContext?.Request?.Headers["Authorization"],
                out AuthenticationHeaderValue? value))
            _bearer = value.Parameter;
    }


    public Identity? GetIdentity()
    {
        var jwt = ReadJwt();

        if (jwt == null || !jwt.Payload.TryGetValue(JwtRegisteredClaimNames.Jti, out var plSessionId))
            return null;

        return new Identity()
        {
            Email = jwt.Payload.TryGetValue("email", out var email) ? email.ToString() : null,
            FirstName = jwt.Payload.TryGetValue("firstName", out var firstName) ? firstName.ToString() : null,
            LastName = jwt.Payload.TryGetValue("lastName", out var lastName) ? lastName.ToString() : null,
        };
    }



    private JwtSecurityToken? ReadJwt()
    {
        if (_bearer == null)
            return null;

        var handler = new JwtSecurityTokenHandler();

        if (!handler.CanReadToken(_bearer))
            return null;

        var jwt = handler.ReadJwtToken(_bearer);

        if (jwt?.ValidTo < DateTime.UtcNow)
            return null;

        return jwt;
    }
}
