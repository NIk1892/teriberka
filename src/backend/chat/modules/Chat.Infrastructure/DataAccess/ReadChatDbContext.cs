using System.Reflection;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Chat.Infrastructure.DataAccess
{
    public class ReadChatDbContext(DbContextOptions<ReadChatDbContext> options) : ReadDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetAssembly(typeof(ReadChatDbContext))!);
        }
    }
}
