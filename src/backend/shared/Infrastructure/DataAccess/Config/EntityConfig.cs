using Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.DataAccess;

public abstract class EntityConfig<T> : Config<T> where T : Entity
{
    protected override void ConfigureEntity(EntityTypeBuilder<T> builder)
    {
        builder.Property<string>(e => e.Title).HasMaxLength(Constatnts.FieldLength.Text255).HasColumnType("citext");

        if (typeof(ISlugable).IsAssignableFrom(typeof(T)))
            builder.Property<string>(e => ((ISlugable)e).Slug).HasMaxLength(Constatnts.FieldLength.Text64)
                .HasColumnType("citext");

        if (typeof(IPictureable).IsAssignableFrom(typeof(T)))
            builder.Property<string>(e => ((IPictureable)e).ImagePath).HasMaxLength(Constatnts.FieldLength.Text64)
                .HasColumnType("citext");

        builder.Property(x => x.Xmin).HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }

    protected override void ConfigureIndexes(EntityTypeBuilder<T> builder)
    {
        builder.HasIndex(e => e.Id)
            .IncludeProperties(
                p => new { p.Title });

        builder.HasIndex(e => e.Title).UniqueIndex();
        if (typeof(ISlugable).IsAssignableFrom(typeof(T)))
            builder.HasIndex(e => ((ISlugable)e).Slug).UniqueIndex();
    }
}

   
