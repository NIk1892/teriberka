using Infrastructure;
using Infrastructure.DataAccess;
using Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;
using Users.Contracts;
using Users.Domain;

namespace Users.Infrastructure.DataAccess;

public class UserSingleQueryRepository(ReadApplicationDbContext dbContext, IEntityToDtoMapper<UserDto,UserEntity> mapper) :SingleQueryRepository<UserSingleQuery,UserDto,UserEntity>(dbContext, mapper)
{
    protected override IQueryable<UserEntity> BuildDbQuery(IQueryable<UserEntity> dbQuery, UserSingleQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.TgId))
            return dbQuery.Where(x => x.TgId == query.TgId);

        if (!string.IsNullOrWhiteSpace(query.Email))
            return dbQuery.Where(x => x.Email == query.Email);

        return base.BuildDbQuery(dbQuery, query);
    }
}