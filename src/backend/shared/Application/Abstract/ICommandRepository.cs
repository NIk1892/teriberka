using Contracts;
using Domain;

namespace Application;

public interface ICommandRepository<in TCommand, TEntity> : IUnitOfWorkRepository
    where TCommand : ICommand
    where TEntity : Entity
{
    Task<Guid> CreateAsync(TCommand command, CancellationToken cancellationToken);
    Task<uint> UpdateAsync(Guid entityId, TCommand command, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id);
    Task SaveField<T>(Guid id, string field, T value);
}