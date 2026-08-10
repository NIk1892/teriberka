using Contracts;

namespace Users.Contracts;

public class UserSingleQuery : Query<UserDto>
{
    public string? Email { get; init; }
    public string? TgId { get; set; } = default!;
}

public class UserListQuery : ListQuery<UserDto>
{
    public string? Login { get; init; }
}

public class UserPagedListQuery : PagedListQuery<UserDto, UserListQuery>;
