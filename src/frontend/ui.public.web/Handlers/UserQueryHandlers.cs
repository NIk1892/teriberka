using UI.Shared.Handlers;
using Users.Contracts;

namespace UI.Public.Web.Handlers;

public class UserListQueryHandler(HttpClient httpClient)
    : ApiListQueryHandler<UserListQuery, UserDto>(httpClient)
{
    protected override string ApiPath => "api/admin/user/list";
}

public class UserPagedListQueryHandler(HttpClient httpClient)
    : ApiPagedListQueryHandler<UserPagedListQuery, UserListQuery, UserDto>(httpClient)
{
    protected override string ApiPath => "api/admin/user/pagedList";
}

public class UserSingleQueryHandler(HttpClient httpClient)
    : ApiSingleQueryHandler<UserSingleQuery, UserDto>(httpClient)
{
    protected override string ApiPath => "api/admin/user/get";
}
