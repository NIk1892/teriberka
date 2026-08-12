
using Domain;
using Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Users.Domain;

namespace Users.Infrastructure.DataAccess
{
    public class UserConfig : AuditableEntityConfig<UserEntity>
    {
        protected override string TableName => "Users";

        protected override void ConfigureEntity(EntityTypeBuilder<UserEntity> builder)
        {
            base.ConfigureEntity(builder);

            builder.Property<string>(e => e.Email).HasMaxLength(Constatnts.FieldLength.Text64).HasColumnType("citext");
            builder.Property<string>(e => e.TgId).HasMaxLength(Constatnts.FieldLength.Text64).HasColumnType("citext");

            builder.Property<string>(e => e.FirstName).HasMaxLength(Constatnts.FieldLength.Text128)
                .HasColumnType("citext");
            builder.Property<string>(e => e.LastName).HasMaxLength(Constatnts.FieldLength.Text128)
                .HasColumnType("citext");
            builder.Property<string>(e => e.MiddleName).HasMaxLength(Constatnts.FieldLength.Text128)
                .HasColumnType("citext");

            builder.Property<string>(e => e.Title).HasMaxLength(Constatnts.FieldLength.Text255)
                .HasComputedColumnSql("""
                                      TRIM(COALESCE("FirstName", '') || ' ' || COALESCE("LastName", ''))
                                      """, stored: true);

        }


        protected override void ConfigureIndexes(EntityTypeBuilder<UserEntity> builder)
        {
            builder.HasIndex(e => e.Id)
                .IncludeProperties(
                    p => new { p.Title, p.FirstName, p.LastName, p.MiddleName });
            
            builder.HasIndex(e => e.Email).IsUnique()
                .HasFilter("\"IsDeleted\" = false AND \"Email\" IS NOT NULL");
            builder.HasIndex(e => e.TgId).IsUnique()
                .HasFilter("\"IsDeleted\" = false AND \"TgId\" IS NOT NULL");
        }
    }
}
