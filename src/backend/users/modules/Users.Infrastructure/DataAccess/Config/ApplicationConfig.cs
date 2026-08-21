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

        builder.Property(e => e.Phone).HasMaxLength(Constatnts.FieldLength.Text64).IsRequired();
        builder.Property(e => e.Route).HasMaxLength(Constatnts.FieldLength.Text32);
    }

    // Без уникального индекса на Title из базового конфига: имя необязательно,
    // а два тёзки должны иметь возможность записаться.
    protected override void ConfigureIndexes(EntityTypeBuilder<ApplicationEntity> builder)
    {
        builder.HasIndex(e => e.Id)
            .IncludeProperties(p => new { p.Title, p.Phone });
    }
}
