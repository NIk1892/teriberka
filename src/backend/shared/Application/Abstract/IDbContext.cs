namespace Application;


public interface IWriteDbContext 
{
    Task MigrateAsync();
    Task EnsureCreatedAsync();
    int SaveChanges();
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}