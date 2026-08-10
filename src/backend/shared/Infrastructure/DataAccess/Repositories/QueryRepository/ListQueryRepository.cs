using Application;
using Domain;
using Microsoft.EntityFrameworkCore;
using Contracts;
using Infrastructure.Mappers;

namespace Infrastructure.DataAccess
{
    public class ListQueryRepository<TQuery, TDto, TEntity>(
        ReadDbContext context,
        IEntityToDtoMapper<TDto,TEntity>  mapper)
        : Repository<TEntity>(context), IListQueryRepository<TQuery, TDto, TEntity>
        where TDto : Dto, new()
        where TEntity : Entity
        where TQuery : ListQuery<TDto>
    {
        public virtual async Task<IReadOnlyCollection<TDto>> ListAsync(TQuery query, CancellationToken cancellationToken)
        {
            var dbQuery = GetDbQuery(query);

            if (query.Offset.HasValue)
                dbQuery = dbQuery.Skip(query.Offset.Value);

            dbQuery = dbQuery.Take(query.Limit ?? Constants.LimitSelectRecords);

            if (query.SimpleQuery == true)
                return await ProjectSimple(dbQuery).ToListAsync(cancellationToken);


            return await mapper.ToDto(dbQuery).ToListAsync(cancellationToken);

        }

        // Lightweight projection for autocompletes/selects (SimpleQuery=true): только поля,
        // нужные пикеру, без полного маппера/джойнов. Переопределяй в репе, чтобы добавить
        // отображаемые поля (картинку и т.п.).
        protected virtual IQueryable<TDto> ProjectSimple(IQueryable<TEntity> dbQuery)
            => dbQuery.Select(t => new TDto
            {
                Id = t.Id,
                Title = t.Title,
            });


        public async Task<PagedList<TDto>> PagedListAsync(int pageIndex, int pageSize, TQuery query,
            CancellationToken cancellationToken = default)
        {
            var dbQuery = GetDbQuery(query);
            var count = await dbQuery.CountAsync(cancellationToken);

            return new PagedList<TDto>(await mapper
                    .ToDto(dbQuery.Skip((pageIndex - 1) * pageSize).Take(pageSize)).ToListAsync(cancellationToken), count,
                pageIndex,
                pageSize);
        }


        protected virtual IQueryable<TEntity> GetBaseDbQuery() => DbSet.AsNoTracking().Where(x=>x.IsDeleted == false);
        protected virtual IQueryable<TEntity> ProcessDbQuery(TQuery query, IQueryable<TEntity> dbQuery) => dbQuery;

        protected virtual IQueryable<TEntity> ProcessTextQuery(TQuery query, IQueryable<TEntity> dbQuery)
        {
            if (!string.IsNullOrEmpty(query.Text))
                dbQuery = dbQuery.Where(x => x.Title.Contains(query.Text));

            return dbQuery;
        }

        protected virtual IQueryable<TEntity> ProcessSorting(string sorting, IQueryable<TEntity> dbQuery)
        {
            if (string.IsNullOrWhiteSpace(sorting))
                return dbQuery.OrderBy(x=>x.Id);

            var sortingParams = sorting.Split('_');
            if (sortingParams.Length > 0)
            {
                switch (sortingParams[0])
                {
                    // case "Created": return sortingParams.Length == 2 && sortingParams[1] == "desc" ? dbQuery.OrderByDescending(x => x.Created) : dbQuery.OrderBy(x => x.Created);
                    // case "Modified": return sortingParams.Length == 2 && sortingParams[1] == "desc" ? dbQuery.OrderByDescending(x => x.Modified) : dbQuery.OrderBy(x => x.Modified);
                    case "Title":
                        return sortingParams is [_, "desc"]
                            ? dbQuery.OrderByDescending(x => x.Title)
                            : dbQuery.OrderBy(x => x.Title);
                }
            }
            else
                dbQuery = dbQuery.OrderBy(t => t.Id);

            return dbQuery;
        }
        
        private IQueryable<TEntity> GetDbQuery(TQuery query)
        {
            var dbQuery = GetBaseDbQuery();

            dbQuery = ProcessTextQuery(query, dbQuery);

            dbQuery = ProcessDbQuery(query, dbQuery);
            
            dbQuery = ProcessSorting(query.Sorting, dbQuery);
            
            return dbQuery;
        }
    }
}
