using Domain;
using Grpc;
using Grpc.Core;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rpc.Extensions;
using Users.Contracts;

namespace Users.Infrastructure.DataAccess;

public class UserService(
    IMediator mediator,
    UserRpcMapper mapper,
    WriteApplicationDbContext dbContext,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<UserService> logger)
    : UserRpcService.UserRpcServiceBase
{
    public override async Task<UserRpcResponse> GetItems(UserRpcQuery query, ServerCallContext context) =>
        new()
        {
            Items = { mapper.ToRpcItems(await mediator.Send(mapper.ToQuery(query), context.CancellationToken)) }
        };

    public override async Task<UserGetOrCreateResponse> GetOrCreate(UserGetOrCreateRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        UserDto? user = null;

        if (!string.IsNullOrEmpty(request.TgId))
            user = await mediator.Send(new UserSingleQuery { TgId = request.TgId }, ct);

        if (user is null && !string.IsNullOrEmpty(request.Email))
            user = await mediator.Send(new UserSingleQuery { Email = request.Email }, ct);

        if (user is null)
        {
            var result = await mediator.Send(new UserCreateCommand
            {
                TgId = request.TgId.NullIfEmpty(),
                Email = request.Email.NullIfEmpty(),
                FirstName = request.FirstName.NullIfEmpty(),
                LastName = request.LastName.NullIfEmpty(),
            }, ct);

            user = new UserDto()
            {
                Id = result.Id.Value,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
            };
        }

        return new UserGetOrCreateResponse
        {
            Id = user.Id.ToString(),
            Email = user.Email.OrEmpty(),
            FirstName = user.FirstName.OrEmpty(),
            LastName = user.LastName.OrEmpty(),
            Role = user.Role,
        };
    }

    public override async Task<AdminLoginResponse> AdminLogin(AdminLoginRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;

        var user = await dbContext.Set<Users.Domain.UserEntity>()
            .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted, ct);

        if (user is null)
            return new AdminLoginResponse { Success = false, Error = "Пользователь не найден" };

        if (user.Role != UserRole.Admin && user.Role != UserRole.SuperAdmin)
            return new AdminLoginResponse { Success = false, Error = "Доступ запрещён" };

        // Dev-only: ADMIN_DEV_LOGIN skips the password check entirely.
        // Hard-blocked outside non-Production environments regardless of the flag value.
        var devLogin = !environment.IsProduction()
            && bool.TryParse(configuration["ADMIN_DEV_LOGIN"], out var dl) && dl;

        if (devLogin)
        {
            logger.LogWarning(
                "ADMIN_DEV_LOGIN active — password check skipped for admin login {Email}",
                request.Email);
        }
        else
        {
            if (string.IsNullOrEmpty(user.PasswordHash))
                return new AdminLoginResponse { Success = false, Error = "Пароль не задан. Обратитесь к администратору." };

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return new AdminLoginResponse { Success = false, Error = "Неверный пароль" };
        }

        return new AdminLoginResponse
        {
            Success = true,
            UserId = user.Id.ToString(),
            Email = user.Email ?? "",
            FirstName = user.FirstName ?? "",
            LastName = user.LastName ?? "",
            Role = (int)user.Role,
            TotpConfigured = !string.IsNullOrEmpty(user.TotpSecret),
            TotpSecret = user.TotpSecret ?? "",
        };
    }

    public override async Task<SetTotpSecretResponse> SetTotpSecret(SetTotpSecretRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = new Guid(request.UserId);

        var user = await dbContext.Set<Users.Domain.UserEntity>()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return new SetTotpSecretResponse { Success = false };

        user.TotpSecret = request.TotpSecret;
        await dbContext.SaveChangesAsync(ct);

        return new SetTotpSecretResponse { Success = true };
    }

    public override async Task<SetPasswordResponse> SetPassword(SetPasswordRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var userId = new Guid(request.UserId);

        var user = await dbContext.Set<Users.Domain.UserEntity>()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return new SetPasswordResponse { Success = false };

        user.PasswordHash = request.PasswordHash;
        await dbContext.SaveChangesAsync(ct);

        return new SetPasswordResponse { Success = true };
    }
}
