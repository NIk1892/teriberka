using System.Reflection;
using Application;
using Domain;
using Microsoft.EntityFrameworkCore;
using Contracts;
using Infrastructure.Mappers;

namespace Infrastructure.DataAccess;

public class CommandRepository<TCommand, TEntity>(
    WriteDbContext context,
    ICommandToEntityMapper<TEntity,TCommand> mapper)
    : Repository<TEntity>(context), ICommandRepository<TCommand,TEntity>
    where TCommand : ICommand
    where TEntity : Entity
{
    public async Task<Guid> CreateAsync(TCommand command, CancellationToken cancellationToken)
    {
        var entity = mapper.ToNewEntity(command);

        BeforeSave(command, entity);

        var result = await DbSet.AddAsync(entity, cancellationToken);
        return result.Entity.Id;
    }

    public virtual async Task<uint> UpdateAsync(Guid entityId, TCommand command, CancellationToken cancellationToken)
    {
        var entity = await GetExistedEntity(entityId, cancellationToken);
        if (entity == null) return 0;

        mapper.ToEntity(command, entity);

        BeforeSave(command, entity);

        if (entity is AuditableEntity auditableEntity)
            auditableEntity.Audit = new Audit
            {
                ModifiedAt = DateTime.UtcNow
            };

        if (command is IUpdateCommand updateCmd)
            Context.Entry(entity).Property(nameof(Entity.Xmin)).OriginalValue = updateCmd.Xmin;

        return entity.Xmin;
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        var entity = await DbSet.FindAsync(id);
        if (entity is null) return;
        Context.Entry(entity).Property("IsDeleted").CurrentValue = true;

        if (entity is AuditableEntity auditableEntity)
            auditableEntity.Audit = new Audit { ModifiedAt = DateTime.UtcNow };
    }

    public virtual async Task SaveField<T>(Guid id, string field, T value)
    {
        var entity = await DbSet.Where(x => x.Id == id).FirstOrDefaultAsync();
        if (entity == null)
            return;

        var propery = entity.GetType().GetProperty(field, BindingFlags.Public | BindingFlags.Instance);

        if (propery is null)
            return;

        propery.SetValue(entity, value, null);
    }
    
    
    protected virtual Task<TEntity?> GetExistedEntity(Guid entityId, CancellationToken cancellationToken)
        => DbSet.FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);

    protected virtual void BeforeSave(TCommand command, TEntity entity)
    {
    }
}