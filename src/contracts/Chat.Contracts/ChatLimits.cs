namespace Chat.Contracts;

/// <summary>
/// Границы чата, общие для сервиса и сайта: сервер валидирует по ним команду,
/// UI по ним же ставит maxlength и тексты ошибок. Держать в одном месте, чтобы
/// браузерная и серверная проверки не разъезжались.
/// </summary>
public static class ChatLimits
{
    /// <summary>Длина одного сообщения. В БД поле шире (Text1024) — с запасом.</summary>
    public const int MaxTextLength = 1000;

    /// <summary>Сколько сообщений посетитель может написать в одном диалоге за всё время.</summary>
    public const int MaxMessagesPerSession = 100;

    /// <summary>Окно защиты от флуда и допустимое число сообщений в нём.</summary>
    public const int BurstWindowMinutes = 10;
    public const int MaxMessagesPerWindow = 20;
}
