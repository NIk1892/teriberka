using Application;
using Users.Contracts;
using Users.Domain;

namespace Users.Application;

public class GroupMemberQuerySingleHandler(
    IQueryRepository<GroupMemberSingleQuery, GroupMemberDto, GroupMemberEntity> repository)
    : SingleQueryHandler<GroupMemberSingleQuery, GroupMemberDto,GroupMemberEntity,
        IQueryRepository<GroupMemberSingleQuery, GroupMemberDto,GroupMemberEntity>>(repository);

public class GroupMemberQueryListHandler(
    IListQueryRepository<GroupMemberListQuery, GroupMemberDto, GroupMemberEntity> repository)
    : ListQueryHandler<GroupMemberListQuery, GroupMemberDto,GroupMemberEntity,
        IListQueryRepository<GroupMemberListQuery, GroupMemberDto,GroupMemberEntity>>(repository);

public class GroupMemberPagedListQueryHandler(
    IListQueryRepository<GroupMemberListQuery, GroupMemberDto,GroupMemberEntity> repository)
    : PagedListQueryHandler<GroupMemberPagedListQuery,GroupMemberListQuery, GroupMemberDto,GroupMemberEntity,
        IListQueryRepository<GroupMemberListQuery, GroupMemberDto,GroupMemberEntity>>(repository);

