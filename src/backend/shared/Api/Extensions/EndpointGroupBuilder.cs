using Contracts;
using Domain;
using Mediator;
using Microsoft.AspNetCore.Builder;

namespace Api;

public class EndpointGroupBuilder(WebApplication app, string group, string role)
{
    public EndpointGroupBuilder Single<TRequest, TDto>()
        where TRequest : Query<TDto>
        where TDto : IDto
    {
        app.MediateQuerySingle<TRequest, TDto>(group, "get", role);
        return this;
    }

    public EndpointGroupBuilder List<TRequest, TDto>()
        where TRequest : IBaseRequest
        where TDto : IDto
    {
        app.MediateQueryList<TRequest, TDto>(group, "list", role);
        return this;
    }

    public EndpointGroupBuilder PagedList<TPagedRequest, TRequest, TDto>()
        where TPagedRequest : PagedListQuery<TDto, TRequest>, new()
        where TRequest : ListQuery<TDto>
        where TDto : IDto
    {
        app.MediateQueryPagedList<TPagedRequest, TRequest, TDto>(group, "pagedList", role);
        return this;
    }

    public EndpointGroupBuilder Create<TRequest>()
        where TRequest : Contracts.ICommand
    {
        app.MediatePostCommand<TRequest>(group, "create", role);
        return this;
    }

    public EndpointGroupBuilder Update<TRequest>()
        where TRequest : Contracts.ICommand
    {
        app.MediatePutCommand<TRequest>(group, "update", role);
        return this;
    }

    public EndpointGroupBuilder Delete<TRequest>()
        where TRequest : DeleteCommand, new()
    {
        app.MediateDeleteCommand<TRequest>(group, "delete", role);
        return this;
    }
}
