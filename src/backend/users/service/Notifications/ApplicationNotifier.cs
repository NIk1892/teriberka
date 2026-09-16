using System.Net;
using System.Text;
using Api;
using Applications.Contracts;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Users.Domain;
using Users.Infrastructure.DataAccess;

namespace Users.Notifications;

/// <summary>
/// Отбивка о новой заявке в приватный Telegram-канал менеджеров.
///
/// Очереди в памяти нет: раз в PollInterval сервис сам забирает из БД заявки, которые
/// ещё не ушли в канал (TgMessageId IS NULL). Поэтому заявка не теряется ни при рестарте,
/// ни пока Telegram или туннель к нему недоступны, — уедет, как только связь вернётся.
/// Задержка отбивки до PollInterval, для обратного звонка это неважно.
///
/// Рассчитано на один экземпляр users: два процесса отправили бы одну заявку дважды.
/// Без TG_BOT_TOKEN или TG_APPLICATIONS_CHAT_ID отбивка выключена, заявки принимаются как обычно.
/// Имена и телефоны в лог не пишутся — только id заявки.
/// </summary>
public sealed class ApplicationNotifier(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<ApplicationNotifier> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    /// <summary>Потолок паузы, пока Telegram отвечает ошибкой, — чтобы не забивать лог.</summary>
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Заявки старше суток в канал не отправляем: при первом включении отбивки (или после
    /// долгого простоя) старые заявки не должны вывалиться в канал пачкой.
    /// </summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    private const int Batch = 20;

    /// <summary>В Москве с 2014 года нет перехода на летнее время — фиксированного сдвига достаточно.</summary>
    private static readonly TimeSpan MoscowOffset = TimeSpan.FromHours(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = configuration["TG_BOT_TOKEN"];
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogInformation("TG_BOT_TOKEN не задан — отбивка заявок в Telegram выключена");
            return;
        }

        if (!long.TryParse(configuration["TG_APPLICATIONS_CHAT_ID"], out var chatId))
        {
            logger.LogWarning(
                "TG_APPLICATIONS_CHAT_ID не задан — заявки в Telegram не уходят. Добавьте бота админом " +
                "в приватный канал и возьмите его id из лога chat-сервиса («Бота добавили в чат …»)");
            return;
        }

        using var http = ProxyHttpClient.Create(configuration["TG_PROXY_URL"], logger);
        var client = new TelegramBotClient(token, http);

        var delay = PollInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendPendingAsync(client, chatId, stoppingToken);
                delay = PollInterval;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                delay = delay * 2 > MaxBackoff ? MaxBackoff : delay * 2;
                logger.LogWarning(e, "Не удалось отправить заявку в Telegram-канал, повтор через {Delay}", delay);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SendPendingAsync(TelegramBotClient client, long chatId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WriteApplicationDbContext>();

        var since = DateTime.UtcNow - MaxAge;

        var pending = await db.Set<ApplicationEntity>()
            .Where(a => a.TgMessageId == null && !a.IsDeleted && a.Audit!.CreatedAt > since)
            .OrderBy(a => a.Audit!.CreatedAt)
            .Take(Batch)
            .ToListAsync(cancellationToken);

        foreach (var application in pending)
        {
            var sent = await client.SendMessage(
                chatId,
                Format(application),
                parseMode: ParseMode.Html,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                cancellationToken: cancellationToken);

            application.TgMessageId = sent.MessageId;

            // Сохраняем после каждой: если следующая отправка упадёт, уже ушедшая
            // не уйдёт в канал второй раз.
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Заявка {ApplicationId} отправлена в Telegram-канал", application.Id);
        }
    }

    internal static string Format(ApplicationEntity application)
    {
        var text = new StringBuilder()
            .Append("<b>Новая заявка</b> · ").Append(Escape(RouteTitle(application.Route)));

        if (!string.IsNullOrWhiteSpace(application.Title))
            text.Append("\nИмя: ").Append(Escape(application.Title));

        // Номер обычным текстом, без <code>: так Telegram делает его нажимаемым для звонка.
        text.Append("\nТелефон: ").Append(Escape(application.Phone));

        if (application.Audit?.CreatedAt is { } createdAt)
            text.Append('\n').Append((createdAt + MoscowOffset).ToString("dd.MM.yyyy HH:mm")).Append(" МСК");

        return text.ToString();
    }

    /// <summary>
    /// Те же названия, что в форме (resx сайта: MapSpot1..3, RouteCustomTitle). Ресурсов
    /// сайта у users нет, а канал одноязычный — поэтому здесь, по-русски.
    /// </summary>
    private static string RouteTitle(string? code) => code switch
    {
        ApplicationRoutes.Teriberka => "Териберка",
        ApplicationRoutes.Lovozero => "Ловозерские тундры",
        ApplicationRoutes.Tersky => "Терский берег",
        ApplicationRoutes.Custom => "Индивидуальный маршрут",
        null or "" => "маршрут не выбран",
        _ => code,
    };

    /// <summary>Сообщение уходит в parseMode Html — всё, что ввёл посетитель, обязано быть экранировано.</summary>
    private static string Escape(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
