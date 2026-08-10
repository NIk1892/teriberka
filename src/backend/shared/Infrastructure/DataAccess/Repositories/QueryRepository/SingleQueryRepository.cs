using Application;
using Domain;
using Microsoft.EntityFrameworkCore;
using Contracts;
using Infrastructure.Mappers;

namespace Infrastructure.DataAccess
{
    public class SingleQueryRepository<TQuery, TDto, TEntity>(
        ReadDbContext context,
        IEntityToDtoMapper<TDto,TEntity>  mapper)
        : Repository<TEntity>(context), IQueryRepository<TQuery, TDto, TEntity>
        where TDto : IDto, new()
        where TEntity : Entity
        where TQuery : Query<TDto>
    {

        public Task<TDto?> SingleAsync(TQuery query, CancellationToken cancellationToken = default)
            => mapper.ToDto(BuildDbQuery(DbSet, query))
                .FirstOrDefaultAsync(cancellationToken);

        public Task<bool> AnyAsync(TQuery query, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        protected virtual IQueryable<TEntity> BuildDbQuery(IQueryable<TEntity> dbQuery, TQuery query)
        {
            if (query.Url is not null && typeof(ISlugable).IsAssignableFrom(typeof(TEntity)))
                return dbQuery.Where(x => EF.Property<string>(x, nameof(ISlugable.Slug)) == query.Url);
            
            return dbQuery.Where(x => x.Id == query.Id);
        }
    }
}
