using Application;
using Applications.Contracts;
using Users.Domain;

namespace Users.Application.Handlers;

public class ApplicationCreateHandler(
    ICommandRepository<ApplicationCreateCommand, ApplicationEntity> repository,
    IServiceProvider serviceProvider)
    : CreateCommandHandler<ApplicationCreateCommand, ApplicationEntity,
        ICommandRepository<ApplicationCreateCommand, ApplicationEntity>>(repository, serviceProvider);
