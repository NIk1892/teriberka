using System.Net.Http.Headers;
using Api;
using Application;
using Chat.Application.Abstract;
using Chat.Application.Notifications;
using Chat.Bot;
using Chat.Bot.Dm;
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

            // Репозитории чата и диалогов бота не наследуют generic CommandRepository,
            // поэтому Scrutor их не находит — регистрируем руками.
            Services.AddScoped<IChatRepository, ChatRepository>();
            Services.AddScoped<IBotDialogRepository, BotDialogRepository>();

            // Очередь «сообщение сохранено → отнести в Telegram». Синглтон: канал живёт
            // столько же, сколько процесс, и потребитель у него один.
            Services.AddSingleton<IChatNotificationQueue, ChatNotificationQueue>();

            // Единственный на процесс клиент Telegram: им пользуются и polling, и доставка.
            Services.AddSingleton<TelegramBotAccessor>();

            // Личка бота: настройки, отправка с разбором ошибок Bot API и раннер диалогов.
            Services.AddSingleton<BotOptions>();
            Services.AddSingleton<BotSender>();
            Services.AddSingleton<BotDmHandler>();

            // Заявка из бота уходит в users через шлюз со служебным JWT — обычный клиент,
            // без прокси: шлюз внутри compose-сети. Пустые API_URL/API_TOKEN просто выключают
            // запись (BotOptions.BookingEnabled), клиент тогда не вызывается.
            Services.AddHttpClient<BotApiClient>((provider, http) =>
            {
                var options = provider.GetRequiredService<BotOptions>();

                if (options.ApiUrl is not null)
                    http.BaseAddress = new Uri(options.ApiUrl.TrimEnd('/') + "/");

                if (options.ApiToken is not null)
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiToken);

                http.Timeout = TimeSpan.FromSeconds(15);
            });

            // Telegram-бот. Без TG_BOT_TOKEN просто пишет в лог и не мешает сервису —
            // переписка всё равно сохраняется, недоставленное подхватится позже.
            Services.AddHostedService<BotService>();

            // Относит сообщения посетителей в группу гидов и подметает недоставленное.
            Services.AddHostedService<ChatNotificationDispatcher>();

            // Удаляет переписку по сроку хранения и чистит диалоги бота — там персональные данные.
            Services.AddHostedService<ChatRetentionService>();
        }

        public override void ConfigureEndPoints(WebApplication app)
        {
            // Оба маршрута публичные: авторизации на сайте нет, доступ к переписке даёт
            // только секретный токен диалога из cookie.
            // ChatAdminReplyCommand и ChatTelegramSendCommand эндпоинтов не имеют сознательно —
            // ответы гида и сообщения из лички бота приходят из Telegram, изнутри этого же процесса.
            app.MediatePostCommand<ChatSendCommand>("chat", "send");

            app.MediateQueryList<ChatMessageListQuery, ChatMessageDto>("chat", "messages");
        }
    }
}
