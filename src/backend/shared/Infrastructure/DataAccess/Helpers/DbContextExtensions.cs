using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.DataAccess;

public static class DbContextExtensions
{
    public static void EnsureCollection<T>(this DbContext context, ICollection<T> collection, IEnumerable<Guid> ids)
        where T : Entity
    {
        var existingIds = collection.Select(x => x.Id).ToHashSet();
        var newIds = ids.ToHashSet();

        foreach (var item in collection.Where(x => !newIds.Contains(x.Id)).ToList())
            collection.Remove(item);

        foreach (var id in newIds.Where(id => !existingIds.Contains(id)))
        {
            var item = context.Set<T>().Find(id);
            if (item != null)
                collection.Add(item);
        }
    }
}
