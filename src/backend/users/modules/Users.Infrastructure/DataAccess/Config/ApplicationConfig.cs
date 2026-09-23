using Applications.Contracts;
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
        builder.Property(e => e.ManagerComment).HasMaxLength(ApplicationProcessCommand.MaxCommentLength);

        // Источник обязателен: заявки до появления колонки — с сайта, дефолт колонки это и говорит.
        builder.Property(e => e.Source).HasMaxLength(Constatnts.FieldLength.Text32).IsRequired()
            .HasDefaultValue(ApplicationSources.Site);
        builder.Property(e => e.TgUsername).HasMaxLength(Constatnts.FieldLength.Text64);
        builder.Property(e => e.Details).HasMaxLength(ApplicationCreateCommand.MaxDetailsLength);
    }

    // Без уникального индекса на Title из базового конфига: имя необязательно,
    // а два тёзки должны иметь возможность записаться.
    protected override void ConfigureIndexes(EntityTypeBuilder<ApplicationEntity> builder)
    {
        builder.HasIndex(e => e.Id)
            .IncludeProperties(p => new { p.Title, p.Phone });
    }
}
