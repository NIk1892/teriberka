namespace Application;

public interface IUnitOfWork : IUnitOfWork<IWriteDbContext>;
public interface IUnitOfWorkRepository : IUnitOfWorkRepository<IWriteDbContext>;
public interface IUnitOfWorkRepository<TContext> where TContext : IWriteDbContext;

public interface IUnitOfWork<TContext>
{
    void Commit();
    Task CommitAsync(CancellationToken cancellationToken = default);
}


