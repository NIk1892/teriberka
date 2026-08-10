using Application;
using Users.Contracts;
using Users.Domain;

namespace Users.Application;

public class GroupQuerySingleHandler(
    IQueryRepository<GroupSingleQuery, GroupDto, GroupEntity> repository)
    : SingleQueryHandler<GroupSingleQuery, GroupDto,GroupEntity,
        IQueryRepository<GroupSingleQuery, GroupDto,GroupEntity>>(repository);

public class GroupQueryListHandler(
    IListQueryRepository<GroupListQuery, GroupDto, GroupEntity> repository)
    : ListQueryHandler<GroupListQuery, GroupDto,GroupEntity,
        IListQueryRepository<GroupListQuery, GroupDto,GroupEntity>>(repository);

public class GroupPagedListQueryHandler(
    IListQueryRepository<GroupListQuery, GroupDto,GroupEntity> repository)
    : PagedListQueryHandler<GroupPagedListQuery,GroupListQuery, GroupDto,GroupEntity,
        IListQueryRepository<GroupListQuery, GroupDto,GroupEntity>>(repository);

