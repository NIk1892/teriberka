using Api;
using Application;
using Chat.Application.Abstract;
using Chat.Application.Notifications;
using Chat.Bot;
using Chat.Contracts;
using Chat.Infrastructure.DataAccess;
using Domain;
using Mediator;

namespace Chat
{
    public class Configurator(WebApplicationBuilder appBuilder) : StartupConfigurator(appBuilder)
    {
        protected override void ConfigureDependencies()
        {
            Services.AddPersistence<ReadChatDbContext, WriteChatDbContext>(Configuration);

            Services.AddMediator(options =>
            {
                options.ServiceLifetime = ServiceLifetime.Scoped;
                options.PipelineBehaviors = [typeof(ValidatorBehavior<,>)];
            });

            Services.AddScoped<IIdentityService, IdentityService>();

            // Репозиторий чата не наследует generic CommandRepository, поэтому Scrutor
            // его не находит — регистрируем руками.
            Services.AddScoped<IChatRepository, ChatRepository>();

            // Очередь «сообщение сохранено → отнести в Telegram». Синглтон: канал живёт
            // столько же, сколько процесс, и потребитель у него один.
            Services.AddSingleton<IChatNotificationQueue, ChatNotificationQueue>();

            // Единственный на процесс клиент Telegram: им пользуются и polling, и доставка.
            Services.AddSingleton<TelegramBotAccessor>();

            // Telegram-бот. Без TG_BOT_TOKEN просто пишет в лог и не мешает сервису —
            // переписка всё равно сохраняется, недоставленное подхватится позже.
            Services.AddHostedService<BotService>();

            // Относит сообщения посетителей в группу гидов и подметает недоставленное.
            Services.AddHostedService<ChatNotificationDispatcher>();

            // Удаляет переписку по сроку хранения — в чате лежат персональные данные.
            Services.AddHostedService<ChatRetentionService>();
        }

        public override void ConfigureEndPoints(WebApplication app)
        {
            // Оба маршрута публичные: авторизации на сайте нет, доступ к переписке даёт
            // только секретный токен диалога из cookie.
            // ChatAdminReplyCommand эндпоинта не имеет сознательно — ответы гида приходят
            // не по HTTP, а из Telegram, изнутри этого же процесса.
            app.MediatePostCommand<ChatSendCommand>("chat", "send");

            app.MediateQueryList<ChatMessageListQuery, ChatMessageDto>("chat", "messages");
        }
    }
}
