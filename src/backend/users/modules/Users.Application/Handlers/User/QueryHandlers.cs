using Application;
using Users.Contracts;
using Users.Domain;

namespace Users.Application;

public class UserSingleQueryHandler(
    IQueryRepository<UserSingleQuery, UserDto, UserEntity> repository)
    : SingleQueryHandler<UserSingleQuery, UserDto, UserEntity,IQueryRepository<UserSingleQuery, UserDto, UserEntity>>(repository);

public class UserListQueryHandler(
    IListQueryRepository<UserListQuery, UserDto, UserEntity> repository)
    : ListQueryHandler<UserListQuery, UserDto,UserEntity,
        IListQueryRepository<UserListQuery, UserDto,UserEntity>>(repository);

public class UserPagedListQueryHandler(
    IListQueryRepository<UserListQuery, UserDto,UserEntity> repository)
    : PagedListQueryHandler<UserPagedListQuery,UserListQuery, UserDto,UserEntity,
        IListQueryRepository<UserListQuery, UserDto,UserEntity>>(repository);

