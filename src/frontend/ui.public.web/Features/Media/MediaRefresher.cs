namespace UI.Public.Web.Features.Media;

/// <summary>
/// Перечитывает содержимое хранилища фото в фоне: первый раз при старте, дальше — раз
/// в MEDIA_REFRESH_MINUTES. Это же значение и есть задержка «залил фото → видно на сайте».
/// Ходить в хранилище на рендере страницы нельзя: главная отрисовывается на каждый
/// запрос, и посетитель платил бы сетевым вызовом за каждый показ.
/// </summary>
public sealed class MediaRefresher(MediaCatalog catalog) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!catalog.IsEnabled)
            return;

        try
        {
            // RefreshAsync сам гасит ошибки, поэтому отдельной обработки они не требуют:
            // недоступное хранилище просто оставляет прошлый снимок до следующего круга.
            await catalog.RefreshAsync(stoppingToken);

            using var timer = new PeriodicTimer(catalog.RefreshInterval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
                await catalog.RefreshAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Штатная остановка приложения.
        }
    }
}
