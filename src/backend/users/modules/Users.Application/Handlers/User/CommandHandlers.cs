using System.Net;
using Application;
using Domain;
using Users.Contracts;
using Users.Domain;

namespace Users.Application;

public class UserCreateHandler(
    ICommandRepository<UserCreateCommand,UserEntity> repository,
    IServiceProvider serviceProvider)
    : CreateCommandHandler<UserCreateCommand,UserEntity, ICommandRepository<UserCreateCommand,UserEntity>>(repository,
        serviceProvider);

public class UserUpdateHandler(
    ICommandRepository<UserUpdateCommand, UserEntity> repository,
    IQueryRepository<UserSingleQuery, UserDto, UserEntity> queryRepository,
    IIdentityService identityService,
    IServiceProvider serviceProvider)
    : UpdateCommandHandler<UserUpdateCommand, UserEntity, ICommandRepository<UserUpdateCommand, UserEntity>>(repository, serviceProvider)
{
    protected override async Task<ExecuteRequestResult> ExecuteCommand(UserUpdateCommand request, CancellationToken cancellationToken)
    {
        var existing = await queryRepository.SingleAsync(new UserSingleQuery { Id = request.Id }, cancellationToken);

        if (existing != null && existing.Role != request.Role)
        {
            if (identityService.Role != ((int)UserRole.SuperAdmin).ToString())
                throw new ExcecuteCommandException(HttpStatusCode.Forbidden, "Только суперковбоец может изменять роли");

            if (identityService.UserId == request.Id)
                throw new ExcecuteCommandException(HttpStatusCode.Forbidden, "Нельзя изменить роль своей учётной записи");
        }

        return await base.ExecuteCommand(request, cancellationToken);
    }
}