
using Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Users.Domain;

namespace Users.Infrastructure.DataAccess
{
    public class GroupConfig : AuditableEntityConfig<GroupEntity>
    {
        protected override string TableName => "Groups";

        protected override void ConfigureEntity(EntityTypeBuilder<GroupEntity> builder)
        {
            base.ConfigureEntity(builder);
            
            builder
                .Property(o => o.Type)
                .HasConversion<int>();
            
            builder.HasOne(e => e.ParentGroup).WithMany().HasForeignKey(i => i.ParentGroupId).Metadata.DeleteBehavior =
                DeleteBehavior.Restrict;

        }
    }
}
