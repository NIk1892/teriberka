using Domain;
using Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Users.Domain;

namespace Users.Infrastructure.DataAccess;

public class ApplicationConfig : AuditableEntityConfig<ApplicationEntity>
{
    protected override string TableName => "Applications";

    protected override void ConfigureEntity(EntityTypeBuilder<ApplicationEntity> builder)
    {
        base.ConfigureEntity(builder);

        builder.Property(e => e.Phone).HasMaxLength(Constatnts.FieldLength.Text64);
        builder.Property(e => e.PeopleCount).IsRequired();
        builder.Property(e => e.ArrivalDate).HasColumnType("date").IsRequired();
        builder.Property(e => e.Comment).HasMaxLength(Constatnts.FieldLength.Text512);
    }

    protected override void ConfigureIndexes(EntityTypeBuilder<ApplicationEntity> builder)
    {
        builder.HasIndex(e => e.Id)
            .IncludeProperties(p => new { p.Title, p.Phone, p.PeopleCount, p.ArrivalDate });

        builder.HasIndex(e => e.ArrivalDate);
    }
}
