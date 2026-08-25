using System.Text.RegularExpressions;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace UI.Public.Web.Features.Media;

/// <summary>
/// Что лежит в хранилище фото. Сейчас используется один префикс:
/// <code>
/// hero/&lt;имя&gt;.webp — полоса «Что вас ждёт» на главной (имена — в HeroSlides)
/// </code>
/// Смысл хранилища в том, что владелец заливает кадры через веб-консоль, без коммита
/// и пересборки сайта. Список обновляет <see cref="MediaRefresher"/> в фоне, страницы
/// читают готовый снимок — ни один запрос посетителя не платит за поход в хранилище.
/// Хранилище недоступно — живёт прошлый снимок, страница не падает никогда.
///
/// В каждый URL подставляется версия <c>?v=</c> по времени изменения объекта: картинки
/// кэшируются на 30 суток, и без версии перезалитое под тем же именем фото посетитель
/// увидел бы только через месяц. Caddy проксирует в MinIO только путь, поэтому лишний
/// параметр до хранилища не доходит.
/// </summary>
public sealed class MediaCatalog
{
    private const string HeroPrefix = "hero/";
    private const int MaxListPages = 10;

    // Заливка ручная, поэтому имена проверяем: пробелы, кириллица и верхний регистр
    // дают percent-encoding и «файл есть, а на сайте его нет».
    private static readonly Regex NamePattern = new(@"^[a-z0-9][a-z0-9-]*\.webp$", RegexOptions.Compiled);

    private readonly ILogger<MediaCatalog> _logger;
    private readonly IAmazonS3? _client;
    private readonly string _bucket;
    private readonly string _publicPath;

    private volatile IReadOnlyDictionary<string, long> _hero = new Dictionary<string, long>();

    public MediaCatalog(IConfiguration configuration, ILogger<MediaCatalog> logger)
    {
        _logger = logger;

        var endpoint = configuration["MEDIA_ENDPOINT"];
        _bucket = configuration["MEDIA_BUCKET"] ?? "media";
        _publicPath = (configuration["MEDIA_PUBLIC_PATH"] ?? "/media").TrimEnd('/');
        RefreshInterval = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue("MEDIA_REFRESH_MINUTES", 5)));

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            // Ключа нет — хранилище выключено, фото берутся из wwwroot. Ровно та же
            // идиома, что у TG_BOT_URL и MAX_URL: пустое значение выключает функцию.
            _logger.LogInformation("MEDIA_ENDPOINT не задан — хранилище фото выключено");
            return;
        }

        var accessKey = configuration["MEDIA_ACCESS_KEY"];
        var secretKey = configuration["MEDIA_SECRET_KEY"];
        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            _logger.LogWarning("MEDIA_ENDPOINT задан, но MEDIA_ACCESS_KEY/MEDIA_SECRET_KEY пусты — хранилище выключено");
            return;
        }

        _client = new AmazonS3Client(
            new BasicAWSCredentials(accessKey, secretKey),
            new AmazonS3Config
            {
                ServiceURL = endpoint,
                // Без этого SDK построит виртуальный хост вида http://media.minio:9000,
                // который в compose-сети не резолвится.
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
                Timeout = TimeSpan.FromSeconds(5),
                MaxErrorRetry = 1
            });
    }

    /// <summary>Хранилище настроено. Иначе главная работает на локальных файлах из wwwroot.</summary>
    public bool IsEnabled => _client is not null;

    public TimeSpan RefreshInterval { get; }

    /// <summary>
    /// URL кадра полосы на главной по имени файла ("hero-edge.webp") или null,
    /// если такого объекта в хранилище нет — тогда страница берёт локальный файл.
    /// </summary>
    public string? HeroUrl(string fileName) =>
        _hero.TryGetValue(fileName, out var version)
            ? $"{_publicPath}/{HeroPrefix}{fileName}?v={version}"
            : null;

    /// <summary>Перечитать содержимое бакета. Ошибки гасятся: прошлый снимок остаётся жить.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (_client is null)
            return;

        try
        {
            _hero = Build(await ListAsync(cancellationToken));
            _logger.LogInformation("Хранилище фото: {Count} кадров на главной", _hero.Count);
        }
        catch (Exception exception)
        {
            // Сеть, политика, перезапуск MinIO — что угодно. Сайт продолжает показывать
            // прошлый список; если снимка ещё не было — локальные файлы и заглушки.
            _logger.LogWarning(exception, "Не удалось прочитать хранилище фото, оставляем прошлый список");
        }
    }

    private async Task<List<S3Object>> ListAsync(CancellationToken cancellationToken)
    {
        var result = new List<S3Object>();
        var request = new ListObjectsV2Request { BucketName = _bucket, Prefix = HeroPrefix, MaxKeys = 1000 };

        for (var page = 0; page < MaxListPages; page++)
        {
            var response = await _client!.ListObjectsV2Async(request, cancellationToken);

            if (response.S3Objects is { Count: > 0 })
                result.AddRange(response.S3Objects);

            if (response.IsTruncated is not true)
                return result;

            request.ContinuationToken = response.NextContinuationToken;
        }

        // Один ответ вмещает 1000 ключей, так что упереться можно только при десятках
        // тысяч файлов. Молча обрезать список нельзя — это выглядело бы как «часть фото пропала».
        _logger.LogWarning("Хранилище отдало больше {Limit} объектов, список обрезан", MaxListPages * 1000);
        return result;
    }

    private Dictionary<string, long> Build(List<S3Object> objects)
    {
        var hero = new Dictionary<string, long>(StringComparer.Ordinal);
        var rejected = new List<string>();

        foreach (var item in objects)
        {
            // Пустышки .keep, папки и всё, что не webp, отсекаем сразу: Caddy наружу
            // тоже пускает только .webp, показывать в разметке остальное бессмысленно.
            if (item.Size is null or 0 || !item.Key.EndsWith(".webp", StringComparison.Ordinal))
                continue;

            var name = item.Key[HeroPrefix.Length..];

            if (!NamePattern.IsMatch(name))
            {
                rejected.Add(item.Key);
                continue;
            }

            hero[name] = Version(item.LastModified);
        }

        // Заливка ручная, поэтому о непринятых файлах говорим вслух — иначе владелец
        // будет искать причину, почему фото залито, а на сайте его нет.
        if (rejected.Count > 0)
            _logger.LogInformation("Пропущены файлы с недопустимыми именами: {Files}", string.Join(", ", rejected));

        return hero;
    }

    /// <summary>Версия для query: секунды времени изменения объекта. Перезалили файл — URL стал другим.</summary>
    private static long Version(DateTime? lastModified) =>
        lastModified is { } value
            ? new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeSeconds()
            : 0;
}
