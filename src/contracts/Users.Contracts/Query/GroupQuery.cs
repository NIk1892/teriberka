using Contracts;
using Users.Contracts;

namespace Users.Contracts;

public class GroupSingleQuery : Query<GroupDto>;
public class GroupListQuery : ListQuery<GroupDto>;

public class GroupPagedListQuery : PagedListQuery<GroupDto, GroupListQuery>;

