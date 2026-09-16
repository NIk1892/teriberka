using Application;
using Applications.Contracts;
using Users.Domain;

namespace Users.Application.Handlers;

public class ApplicationQueryListHandler(
    IListQueryRepository<ApplicationListQuery, ApplicationDto, ApplicationEntity> repository)
    : ListQueryHandler<ApplicationListQuery, ApplicationDto, ApplicationEntity,
        IListQueryRepository<ApplicationListQuery, ApplicationDto, ApplicationEntity>>(repository);

public class ApplicationPagedListQueryHandler(
    IListQueryRepository<ApplicationListQuery, ApplicationDto, ApplicationEntity> repository)
    : PagedListQueryHandler<ApplicationPagedListQuery, ApplicationListQuery, ApplicationDto, ApplicationEntity,
        IListQueryRepository<ApplicationListQuery, ApplicationDto, ApplicationEntity>>(repository);
