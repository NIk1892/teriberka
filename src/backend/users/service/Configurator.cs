using Api;
using Application;
using Mediator;
using Domain;
using Applications.Contracts;
using Users.Contracts;
using Users.Bot;
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

            // Telegram-бот. Без TG_BOT_TOKEN просто пишет в лог и не мешает
            // сервису — API заявок работает независимо от бота.
            Services.AddHostedService<BotService>();
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
