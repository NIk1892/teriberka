using Api;
using Application;
using Mediator;
using Domain;
using Applications.Contracts;
using Users.Contracts;
using Users.Infrastructure.DataAccess;
using Users.Notifications;
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

            // Отбивка новых заявок в Telegram-канал менеджеров. Без TG_BOT_TOKEN и
            // TG_APPLICATIONS_CHAT_ID просто пишет в лог и не мешает приёму заявок.
            Services.AddHostedService<ApplicationNotifier>();
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

            // Тот же обработчик под другим адресом — для Telegram-бота из chat: он ходит через
            // шлюз со служебным JWT (API_TOKEN), и на шлюзе этот маршрут закрыт политикой Admin
            // и не попадает под лимитер public-form (шлюз видит один IP контейнера chat на всех
            // пользователей бота). Сам сервис роль в адресе не проверяет.
            app.MediatePostCommand<ApplicationCreateCommand>("application", "create", Constants.UrlRestrictions.Private);

            // Admin-маршруты закрыты на шлюзе политикой Admin; читает и отмечает заявки
            // страница /manager сайта со служебным токеном API_TOKEN.
            app.MediateGroup("application", Constants.UrlRestrictions.Admin)
                .List<ApplicationListQuery, ApplicationDto>()
                .PagedList<ApplicationPagedListQuery, ApplicationListQuery, ApplicationDto>()
                .Update<ApplicationProcessCommand>();
        }
    }
}
