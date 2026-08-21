using System.Reflection;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Chat.Infrastructure.DataAccess
{
    public class WriteChatDbContext : WriteDbContext
    {
        public WriteChatDbContext()
        {
        }

        public WriteChatDbContext(DbContextOptions<WriteChatDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetAssembly(typeof(WriteChatDbContext))!);
        }
    }
}
