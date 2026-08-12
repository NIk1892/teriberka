using Api;
using Application;
using Mediator;
using Domain;
using Applications.Contracts;
using Users.Contracts;
using Users.Infrastructure.DataAccess;
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

            Services.AddScoped<IIdentityService, IdentityService>();
        }

        public override void ConfigureEndPoints(WebApplication app)
        {
            app.MediateGroup("user", Constants.UrlRestrictions.Admin)
                .Single<UserSingleQuery, UserDto>()
                .List<UserListQuery, UserDto>()
                .PagedList<UserPagedListQuery, UserListQuery, UserDto>()
                .Create<UserCreateCommand>()
                .Update<UserUpdateCommand>();

            app.MediatePostCommand<ApplicationCreateCommand>("application", "create");

            app.MediateGroup("application", Constants.UrlRestrictions.Admin)
                .List<ApplicationListQuery, ApplicationDto>();
        }
    }
}
