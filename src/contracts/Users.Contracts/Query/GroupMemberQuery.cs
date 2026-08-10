using Contracts;
using Users.Contracts;

namespace Users.Contracts;

public class GroupMemberSingleQuery : Query<GroupMemberDto>;
public class GroupMemberListQuery : ListQuery<GroupMemberDto>;

public class GroupMemberPagedListQuery : PagedListQuery<GroupMemberDto, GroupMemberListQuery>;

