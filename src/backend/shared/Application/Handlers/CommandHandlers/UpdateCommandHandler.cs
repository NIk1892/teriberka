using Contracts;
using Domain;

namespace Application;

public abstract class UpdateCommandHandler<TRequest, TEntity, TRepository>(
    TRepository repository,
    IServiceProvider serviceProvider)
    : CommandHandler<TRequest, TRepository>(repository, serviceProvider)
    where TRequest : IUpdateCommand
    where TEntity : Entity
    where TRepository : ICommandRepository<TRequest, TEntity>
{
    protected override async Task<ExecuteRequestResult> ExecuteCommand(TRequest request,
        CancellationToken cancellationToken)
    {
        return new ExecuteRequestResult
        {
            Hash = await Repository.UpdateAsync(request.Id, request, cancellationToken)
        };
    }
}