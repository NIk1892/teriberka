using Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.DataAccess;

public abstract class AuditableEntityConfig<T> : EntityConfig<T> where T : AuditableEntity
{
    protected override void ConfigureEntity(EntityTypeBuilder<T> builder)
    {
        base.ConfigureEntity(builder);

        builder
            .OwnsOne(
                e => e.Audit,
                q =>
                {
                    q
                        .Property(p => p.CreatedAt)
                        .HasDefaultValueSql("NOW()").ValueGeneratedOnAdd().IsRequired();
                    q
                        .Property(p => p.ModifiedAt)
                        .HasDefaultValueSql("NOW()").ValueGeneratedOnAdd().IsRequired();
                });
    }
}
