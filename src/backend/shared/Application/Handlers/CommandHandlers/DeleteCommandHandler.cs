using Contracts;
using Domain;

namespace Application;

public abstract class DeleteCommandHandler<TRequest, TEntity, TCreateCommand, TRepository>(
    TRepository repository,
    IServiceProvider serviceProvider)
    : CommandHandler<TRequest, TRepository>(repository, serviceProvider)
    where TRequest : DeleteCommand
    where TEntity : Entity
    where TCreateCommand : ICommand
    where TRepository : ICommandRepository<TCreateCommand, TEntity>
{
    protected override async Task<ExecuteRequestResult> ExecuteCommand(TRequest request,
        CancellationToken cancellationToken)
    {
        await Repository.DeleteAsync(request.Id);
        return ExecuteRequestResult.Success;
    }
}
