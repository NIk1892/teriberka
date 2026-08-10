using Domain;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Contracts;

namespace Application;

public abstract class CommandHandler<TRequest, TRepository>(
    TRepository repository,
    IServiceProvider serviceProvider)
    : IRequestHandler<TRequest, ExecuteRequestResult>
    where TRequest : Contracts.ICommand
    where TRepository : IUnitOfWorkRepository
{
    protected TRepository Repository { get; } = repository;

    protected readonly IUnitOfWork UnitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

    public virtual async ValueTask<ExecuteRequestResult> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var result = await ExecuteCommand(request, cancellationToken);

        await UnitOfWork.CommitAsync(cancellationToken);

        await AfterCommit(request, result);

        return result;
    }


    protected abstract Task<ExecuteRequestResult> ExecuteCommand(TRequest request, CancellationToken cancellationToken);
    protected virtual ValueTask AfterCommit(TRequest request, ExecuteRequestResult result) => ValueTask.CompletedTask;

    
}