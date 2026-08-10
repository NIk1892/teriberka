using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.DataAccess
{
    public abstract class Repository<TEntity> 
        where TEntity : Entity
    {
        protected readonly DbContext Context;
        protected readonly DbSet<TEntity> DbSet;

        protected Repository(DbContext dbContext)
        {
            Context = dbContext;
            DbSet = Context.Set<TEntity>();
        }
    }
}
