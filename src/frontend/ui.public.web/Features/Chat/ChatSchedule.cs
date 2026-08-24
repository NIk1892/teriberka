using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace UI.Public.Web.Features.Chat;

/// <summary>
/// Часы, когда на сообщения в чате отвечают живые люди. Виджет показывает это
/// честно: обещать мгновенный ответ ночью — быстрый способ разочаровать.
/// Ключи конфигурации: CHAT_HOURS (например «09:00-21:00») и CHAT_TZ.
/// </summary>
public sealed class ChatSchedule
{
    private readonly TimeOnly _from;
    private readonly TimeOnly _to;
    private readonly TimeZoneInfo _timeZone;

    public ChatSchedule(IConfiguration configuration, ILogger<ChatSchedule> logger)
    {
        var hours = configuration["CHAT_HOURS"] ?? "09:00-21:00";
        var parts = hours.Split('-', StringSplitOptions.TrimEntries);

        if (parts.Length != 2
            || !TimeOnly.TryParse(parts[0], out _from)
            || !TimeOnly.TryParse(parts[1], out _to))
        {
            // Опечатка в конфиге не должна прятать виджет: считаем, что мы всегда на связи.
            logger.LogWarning("CHAT_HOURS = «{Hours}» не разобрать — считаем, что чат отвечает круглосуточно", hours);
            _from = new TimeOnly(0, 0);
            _to = new TimeOnly(23, 59);
        }

        var timeZoneId = configuration["CHAT_TZ"] ?? "Europe/Moscow";

        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            logger.LogWarning(e, "Часовой пояс {TimeZone} не найден — считаем время по UTC", timeZoneId);
            _timeZone = TimeZoneInfo.Utc;
        }

        FromText = $"{_from.Hour:D2}:{_from.Minute:D2}";
        ToText = $"{_to.Hour:D2}:{_to.Minute:D2}";
    }

    public string FromText { get; }

    public string ToText { get; }

    public bool IsOnline(DateTimeOffset? now = null)
    {
        var local = TimeZoneInfo.ConvertTime(now ?? DateTimeOffset.UtcNow, _timeZone);
        var time = TimeOnly.FromDateTime(local.DateTime);

        // окно через полночь (22:00-02:00) — тоже валидное расписание
        return _from <= _to
            ? time >= _from && time < _to
            : time >= _from || time < _to;
    }
}
