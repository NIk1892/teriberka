using Contracts;

namespace Chat.Contracts;

/// <summary>
/// Переписка одного диалога. Токен обязателен: без него выборка пустая — чужую
/// переписку нельзя получить, не зная секрета из cookie.
/// </summary>
public class ChatMessageListQuery : ListQuery<ChatMessageDto>
{
    public string? Token { get; set; }

    /// <summary>Курсор: вернуть сообщения с Ordinal строго больше этого значения.</summary>
    public int After { get; set; }
}
