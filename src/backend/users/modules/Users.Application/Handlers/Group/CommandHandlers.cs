using Application;
using Users.Contracts;
using Users.Domain;

namespace Users.Application;

public class GroupCreateHandler(
    ICommandRepository<GroupCreateCommand,GroupEntity> repository,
    IServiceProvider serviceProvider)
    : CreateCommandHandler<GroupCreateCommand,GroupEntity, ICommandRepository<GroupCreateCommand,GroupEntity>>(repository,
        serviceProvider);


public class GroupUpdateHandler(
    ICommandRepository<GroupUpdateCommand,GroupEntity> repository,
    IServiceProvider serviceProvider)
    : UpdateCommandHandler<GroupUpdateCommand, GroupEntity,ICommandRepository<GroupUpdateCommand,GroupEntity>>(repository,
        serviceProvider);