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
}
