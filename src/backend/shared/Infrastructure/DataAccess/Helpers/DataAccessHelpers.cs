using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DataAccess;

public static class DataAccessHelpers
{
    public static void UniqueIndex<T>(this IndexBuilder<T> builder) =>
        builder.IsUnique().HasFilter("\"IsDeleted\" = false");
}