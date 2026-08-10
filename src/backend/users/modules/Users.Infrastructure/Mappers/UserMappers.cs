using Grpc;
using Infrastructure.Mappers;
using Riok.Mapperly.Abstractions;
using Users.Contracts;
using Users.Domain;

namespace Users.Infrastructure.DataAccess;


[Mapper]
public partial class UserDtoMapper: IEntityToDtoMapper<UserDto, UserEntity>
{
    public partial IQueryable<UserDto> ToDto(IQueryable<UserEntity> query);
}

[Mapper]
public partial class UserCreateEntityMapper : ICommandToEntityMapper<UserEntity, UserCreateCommand>
{
    [MapperIgnoreTarget(nameof(UserEntity.Audit))]
    [MapperIgnoreTarget(nameof(UserEntity.PasswordHash))]
    [MapperIgnoreTarget(nameof(UserEntity.TotpSecret))]
    [MapperIgnoreTarget(nameof(UserEntity.MiddleName))]
    public partial UserEntity ToNewEntity(UserCreateCommand source);
}

[Mapper]
public partial class UserUpdateEntityMapper : ICommandToEntityMapper<UserEntity, UserUpdateCommand>
{
    [MapperIgnoreTarget(nameof(UserEntity.PasswordHash))]
    [MapperIgnoreTarget(nameof(UserEntity.TotpSecret))]
    [MapperIgnoreTarget(nameof(UserEntity.MiddleName))]
    [MapperIgnoreTarget(nameof(UserEntity.Audit))]
    public partial UserEntity ToNewEntity(UserUpdateCommand source);

    [MapperIgnoreTarget(nameof(UserEntity.PasswordHash))]
    [MapperIgnoreTarget(nameof(UserEntity.TotpSecret))]
    [MapperIgnoreTarget(nameof(UserEntity.MiddleName))]
    [MapperIgnoreTarget(nameof(UserEntity.Audit))]
    public partial void ToEntity(UserUpdateCommand source, UserEntity target);
}

[Mapper]
public partial class UserRpcMapper: IDtoToRpcMapper
{
    public partial List<UserGrpcItem> ToRpcItems(IReadOnlyCollection<UserDto> source);
    public partial UserListQuery ToQuery(UserRpcQuery rpcQuery);
}


