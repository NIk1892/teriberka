using Api;
using Application;
using Mediator;
using Domain;
using Users.Application;
using Users.Contracts;
using Users.Infrastructure.DataAccess;
using Users.Domain;
using Constants = Api.Constants;


namespace Users
{
    public class Configurator(WebApplicationBuilder appBuilder) : StartupConfigurator(appBuilder)
    {
        protected override void ConfigureDependencies()
        {
            Services.AddPersistence<ReadApplicationDbContext, WriteApplicationDbContext>(Configuration);

            Services.AddMediator(options =>
            {
                options.ServiceLifetime = ServiceLifetime.Scoped;
                options.PipelineBehaviors = [typeof(ValidatorBehavior<,>)];
            });

            Services.AddGrpc();
            Services.AddScoped<IIdentityService, IdentityService>();
        }

        public override void ConfigureApplication(WebApplication app)
        {
            base.ConfigureApplication(app);
        }



        public override void ConfigureEndPoints(WebApplication app)
        {
            app.MapGrpcService<UserService>();

            app.MediateGroup("user", Constants.UrlRestrictions.Admin)
                .Single<UserSingleQuery, UserDto>()
                .List<UserListQuery, UserDto>()
                .PagedList<UserPagedListQuery, UserListQuery, UserDto>()
                .Create<UserCreateCommand>()
                .Update<UserUpdateCommand>();

            app.MediateGroup("group", Constants.UrlRestrictions.Admin)
                .Single<GroupSingleQuery, GroupDto>()
                .List<GroupListQuery, GroupDto>()
                .PagedList<GroupPagedListQuery, GroupListQuery, GroupDto>()
                .Create<GroupCreateCommand>()
                .Update<GroupUpdateCommand>();

            app.MediateGroup("group-member", Constants.UrlRestrictions.Admin)
                .Single<GroupMemberSingleQuery, GroupMemberDto>()
                .List<GroupMemberListQuery, GroupMemberDto>()
                .PagedList<GroupMemberPagedListQuery, GroupMemberListQuery, GroupMemberDto>();
            app.MediatePostCommand<GroupMemberAdd>("group-member", "add", Constants.UrlRestrictions.Admin);
            app.MediatePostCommand<GroupMemberRemove>("group-member", "remove", Constants.UrlRestrictions.Admin);
        }
    }
}
