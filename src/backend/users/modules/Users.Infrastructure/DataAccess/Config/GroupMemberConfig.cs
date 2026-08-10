
using Domain;
using Infrastructure;
using Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Users.Domain;

namespace Users.Infrastructure.DataAccess
{
    public class GroupMemberConfig : Config<GroupMemberEntity>
    {
        protected override string TableName => "GroupMembers";

        protected override void ConfigureEntity(EntityTypeBuilder<GroupMemberEntity> builder)
        {
            builder.HasOne(e => e.User).WithMany().HasForeignKey(i => i.UserId).Metadata.DeleteBehavior =
                DeleteBehavior.Restrict;
            builder.HasOne(e => e.Group).WithMany().HasForeignKey(i => i.GroupId).Metadata.DeleteBehavior =
                DeleteBehavior.Restrict;

            builder.Property(quotas => quotas.CreatedAt)
                .HasDefaultValueSql("NOW()").ValueGeneratedOnAdd().IsRequired();

            builder.Ignore(x => x.Title);
        }

        protected override void ConfigureIndexes(EntityTypeBuilder<GroupMemberEntity> builder)
        {
        }
    }
}
