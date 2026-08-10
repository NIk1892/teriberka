using System.Reflection;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Users.Infrastructure.DataAccess
{
    public class WriteApplicationDbContext : WriteDbContext
    {
        public WriteApplicationDbContext()
        {
        }

        public WriteApplicationDbContext(DbContextOptions<WriteApplicationDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetAssembly(typeof(WriteApplicationDbContext))!);
        }
    }
}
