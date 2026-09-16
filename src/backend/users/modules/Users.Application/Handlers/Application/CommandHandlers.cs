using Application;
using Applications.Contracts;
using Users.Domain;

namespace Users.Application.Handlers;

public class ApplicationCreateHandler(
    ICommandRepository<ApplicationCreateCommand, ApplicationEntity> repository,
    IServiceProvider serviceProvider)
    : CreateCommandHandler<ApplicationCreateCommand, ApplicationEntity,
        ICommandRepository<ApplicationCreateCommand, ApplicationEntity>>(repository, serviceProvider);

public class ApplicationProcessHandler(
    ICommandRepository<ApplicationProcessCommand, ApplicationEntity> repository,
    IServiceProvider serviceProvider)
    : UpdateCommandHandler<ApplicationProcessCommand, ApplicationEntity,
        ICommandRepository<ApplicationProcessCommand, ApplicationEntity>>(repository, serviceProvider);
