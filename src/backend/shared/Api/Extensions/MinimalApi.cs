using System.Text.Json;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Contracts;
using Domain;
using Microsoft.AspNetCore.Components.Forms;

namespace Api;

public static class MinimalApi
{

    extension(WebApplication app)
    {
        public WebApplication MediateQuerySingle<TRequest, TDto>(string group,string template, string role=Constants.UrlRestrictions.Public)
            where TRequest : Query<TDto>
            where TDto : IDto
        {
            app.MediateQuerySingle<TRequest, TDto>(group,$"/api/{role}/{group}/{template}");
            return app;
        }

        public WebApplication MediateQueryList<TRequest, TDto>(string group, string template, string role=Constants.UrlRestrictions.Public)
            where TRequest : IBaseRequest
            where TDto : IDto
        {
            app.MediateQueryList<TRequest, TDto>(group,$"/api/{role}/{group}/{template}");
            return app;
        }

        public WebApplication MediateQueryPagedList<TPagedRequest,TRequest, TDto>(string group, string template, string role = Constants.UrlRestrictions.Public)
            where TPagedRequest : PagedListQuery<TDto,TRequest>, new()
            where TRequest : ListQuery<TDto>
            where TDto : IDto
        {

            app.MediateQueryPagedList<TPagedRequest, TRequest, TDto>(group, $"/api/{role}/{group}/{template}");
            return app;
        }

        public WebApplication MediatePostCommand<TRequest>(string group, string template, string role=Constants.UrlRestrictions.Public)
            where TRequest : Contracts.ICommand
        {
            app.MediatePostCommand<TRequest>(group, $"/api/{role}/{group}/{template}");
            return app;
        }

        public WebApplication MediatePutCommand<TRequest>(string group, string template, string role)
            where TRequest : Contracts.ICommand
        {
            app.MediatePutCommand<TRequest>(group, $"/api/{role}/{group}/{template}");
            return app;
        }

        public WebApplication MediateDeleteCommand<TRequest>(string group, string template, string role)
            where TRequest : Contracts.DeleteCommand, new()
        {
            app.MapDelete($"/api/{role}/{group}/{template}/{{id:guid}}",
                    async (IMediator mediator, Guid id) =>
                        Results.Ok(await mediator.Send(new TRequest { Id = id })))
                .Produces<ExecuteRequestResult>().WithTags(group);
            return app;
        }

        public EndpointGroupBuilder MediateGroup(string group, string role = Constants.UrlRestrictions.Public)
        {
            return new EndpointGroupBuilder(app, group, role);
        }
    }


    #region Private
    extension(WebApplication app)
    {
        private WebApplication MediateQuerySingle<TRequest, TDto>(string group, string template)
            where TRequest : IBaseRequest
            where TDto : IDto
        {
            app.MapGet(template,
                async (IMediator mediator, [AsParameters] TRequest request) => await mediator.Send(request) is TDto dto
                    ? Results.Ok(dto)
                    : Results.NotFound()).Produces<TDto>().WithTags(group);

            return app;
        }

        private WebApplication MediateQueryList<TRequest, TDto>(string group,
            string template)
            where TRequest : IBaseRequest
            where TDto : IDto
        {
            app.MapGet(template,
                    async (IMediator mediator, [AsParameters] TRequest request) =>
                    Results.Ok(await mediator.Send(request)))
                .Produces<IList<TDto>>().WithTags(group);

            return app;
        }

        private WebApplication MediateQueryPagedList<TPagedRequest, TRequest, TDto>(string group,
            string template)
            where TPagedRequest : PagedListQuery<TDto, TRequest>, new()
            where TRequest : ListQuery<TDto>
            where TDto : IDto
        {
            app.MapGet(template,
                    async (IMediator mediator,int pageIndex, int pageSize, [AsParameters] TRequest request) =>
                    Results.Ok(await mediator.Send(new TPagedRequest()
                    {
                        Query = request,
                        PageIndex = pageIndex,
                        PageSize = pageSize
                    })))
                .Produces<PagedList<TDto>>().WithTags(group);

            return app;
        }

        private WebApplication MediatePostCommand<TRequest>(string group, string template)
            where TRequest : Contracts.ICommand
        {
            app.MapPost(template,
                    async (IMediator mediator, [FromBody] TRequest request) =>
                    {
                        var response = await mediator.Send(request);

                        return request is ISlugable urlable
                            ? Results.Created(urlable.Slug, response)
                            : Results.Created(response.Id.ToString(), response);
                    })
                .Produces<ExecuteRequestResult>().WithTags(group);

            return app;
        }

        private WebApplication MediatePutCommand<TRequest>(string group, string template)
            where TRequest : Contracts.ICommand
        {
            app.MapPut(template,
                    async (IMediator mediator, [FromBody] TRequest request) =>
                    Results.Ok(await mediator.Send(request)))
                .Produces<ExecuteRequestResult>().WithTags(group);

            return app;
        }
    }

    #endregion
}