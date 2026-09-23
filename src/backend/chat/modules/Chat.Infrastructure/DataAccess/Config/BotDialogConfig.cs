using Chat.Domain;
using Domain;
using Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat.Infrastructure.DataAccess;

public class BotDialogConfig : AuditableEntityConfig<BotDialogEntity>
{
    protected override string TableName => "BotDialogs";

    protected override void ConfigureEntity(EntityTypeBuilder<BotDialogEntity> builder)
    {
        base.ConfigureEntity(builder);

        builder.Property(e => e.TgUserId).IsRequired();
        builder.Property(e => e.TgChatId).IsRequired();
        builder.Property(e => e.Lang).HasMaxLength(8);
        builder.Property(e => e.Step).HasConversion<int>().IsRequired();
        builder.Property(e => e.Route).HasMaxLength(Constatnts.FieldLength.Text32);
        builder.Property(e => e.DateText).HasMaxLength(Constatnts.FieldLength.Text64);
        builder.Property(e => e.Wishes).HasMaxLength(Constatnts.FieldLength.Text512);
        builder.Property(e => e.Name).HasMaxLength(Constatnts.FieldLength.Text255);
        builder.Property(e => e.Phone).HasMaxLength(Constatnts.FieldLength.Text32);
        builder.Property(e => e.PendingText).HasMaxLength(Constatnts.FieldLength.Text1024);
        builder.Property(e => e.LastActivityAt).IsRequired();
    }

    // base не вызываем: он вешает уникальный citext-индекс на Title, которого у диалога нет
    // (как у ChatSessionConfig).
    protected override void ConfigureIndexes(EntityTypeBuilder<BotDialogEntity> builder)
    {
        // Одна строка на пользователя — по ней и ищем на каждом апдейте.
        builder.HasIndex(e => e.TgUserId).IsUnique();

        // Чистка брошенных черновиков и старых строк.
        builder.HasIndex(e => e.LastActivityAt);
    }
}
