using System.Reflection;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Users.Infrastructure.DataAccess
{
    public class ReadApplicationDbContext(DbContextOptions<ReadApplicationDbContext> options) : ReadDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetAssembly(typeof(ReadApplicationDbContext))!);
        }
    }
}
