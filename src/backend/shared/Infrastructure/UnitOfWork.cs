using Application;

namespace Infrastructure.DataAccess
{
    public class UnitOfWork(WriteDbContext context) : UnitOfWork<IWriteDbContext>(context), IUnitOfWork;
    public class UnitOfWork<TContext>(TContext context) : IUnitOfWork<IWriteDbContext>
        where TContext : IWriteDbContext
    {
        private readonly TContext _context = context;

        public void Commit() => _context.SaveChanges();


        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            _context.SaveChangesAsync(cancellationToken);
    }
}
