using Contracts;
using Domain;

namespace Chat.Contracts;

public record ChatMessageDto : AuditableDto
{
    /// <summary>Порядковый номер внутри диалога; он же курсор поллинга.</summary>
    public int Ordinal { get; init; }

    public ChatDirection Direction { get; init; }

    public string? Text { get; init; }
}
