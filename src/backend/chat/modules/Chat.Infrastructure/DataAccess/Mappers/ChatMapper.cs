using Chat.Contracts;
using Chat.Domain;
using Infrastructure.Mappers;
using Riok.Mapperly.Abstractions;

namespace Chat.Infrastructure.DataAccess;

// Имя обязано заканчиваться на DtoMapper и лежать в сборке *Infrastructure.dll —
// иначе ConfigureMappers молча не зарегистрирует маппер, и DI упадёт при первом запросе.
[Mapper]
public partial class ChatMessageDtoMapper : IEntityToDtoMapper<ChatMessageDto, ChatMessageEntity>
{
    public partial IQueryable<ChatMessageDto> ToDto(IQueryable<ChatMessageEntity> query);

    // Настройки на IQueryable-проекции Mapperly игнорирует (RMG065) — они живут на
    // поэлементном маппинге, который проекция встраивает в SQL.
    [MapPropertyFromSource(nameof(ChatMessageDto.Delivered), Use = nameof(IsDelivered))]
    private partial ChatMessageDto ToDto(ChatMessageEntity message);

    // Доставка = у сообщения есть id в Telegram (outbox пуст). Тело — одно выражение:
    // только такое Mapperly встраивает в проекцию, а не вызывает на клиенте.
    private static bool IsDelivered(ChatMessageEntity message) => message.TgMessageId != null;
}
