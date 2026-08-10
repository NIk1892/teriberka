using System.Net;
using Contracts;
using Domain;
using Microsoft.Extensions.Logging;

namespace Application;

public abstract class CreateCommandHandler<TRequest, TEntity, TRepository>(
    TRepository repository,
    IServiceProvider serviceProvider)
    : CommandHandler<TRequest, TRepository>(repository, serviceProvider) 
    where TRequest : Command
    where TEntity : Entity
    where TRepository : ICommandRepository<TRequest,TEntity>
{
    protected override async Task<ExecuteRequestResult> ExecuteCommand(TRequest request,
        CancellationToken cancellationToken)
        => new(HttpStatusCode.Created, await Repository.CreateAsync(request, cancellationToken));
}