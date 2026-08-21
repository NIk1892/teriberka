using Application;
using Chat.Contracts;
using Chat.Domain;

namespace Chat.Application.Handlers;

public class ChatMessageQueryListHandler(
    IListQueryRepository<ChatMessageListQuery, ChatMessageDto, ChatMessageEntity> repository)
    : ListQueryHandler<ChatMessageListQuery, ChatMessageDto, ChatMessageEntity,
        IListQueryRepository<ChatMessageListQuery, ChatMessageDto, ChatMessageEntity>>(repository);
