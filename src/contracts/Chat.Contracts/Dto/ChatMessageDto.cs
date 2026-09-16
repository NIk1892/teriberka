using Contracts;
using Domain;

namespace Chat.Contracts;

public record ChatMessageDto : AuditableDto
{
    /// <summary>Порядковый номер внутри диалога; он же курсор поллинга.</summary>
    public int Ordinal { get; init; }

    public ChatDirection Direction { get; init; }

    public string? Text { get; init; }

    /// <summary>
    /// Сообщение уже в Telegram-группе менеджеров — посетитель видит «✓ Доставлено менеджеру».
    /// У ответов менеджера всегда true: они из Telegram и пришли.
    /// </summary>
    public bool Delivered { get; init; }
}
