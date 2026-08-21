using Chat.Domain;
using Domain;
using Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat.Infrastructure.DataAccess;

public class ChatSessionConfig : AuditableEntityConfig<ChatSessionEntity>
{
    protected override string TableName => "ChatSessions";

    protected override void ConfigureEntity(EntityTypeBuilder<ChatSessionEntity> builder)
    {
        base.ConfigureEntity(builder);

        builder.Property(e => e.Token).HasMaxLength(Constatnts.FieldLength.Text64).IsRequired();
        builder.Property(e => e.Culture).HasMaxLength(Constatnts.FieldLength.Text32);
        builder.Property(e => e.Page).HasMaxLength(Constatnts.FieldLength.Text255);
        builder.Property(e => e.LastMessageAt).IsRequired();
    }

    // base не вызываем: он вешает уникальный citext-индекс на Title, которого у чата нет
    // вовсе (как в ApplicationConfig). Вставки бы не падали — NULL'ы уникальность в Postgres
    // не нарушают, — но индекс на растущей таблице был бы мёртвым грузом.
    protected override void ConfigureIndexes(EntityTypeBuilder<ChatSessionEntity> builder)
    {
        builder.HasIndex(e => e.Token).UniqueIndex();

        // Запасной путь сопоставления «reply гида → диалог», когда отвечают на шапку сессии.
        builder.HasIndex(e => e.TopicMessageId);

        // Чистка по сроку хранения переписки.
        builder.HasIndex(e => e.LastMessageAt);
    }
}

public class ChatMessageConfig : AuditableEntityConfig<ChatMessageEntity>
{
    protected override string TableName => "ChatMessages";

    protected override void ConfigureEntity(EntityTypeBuilder<ChatMessageEntity> builder)
    {
        base.ConfigureEntity(builder);

        builder.Property(e => e.SessionId).IsRequired();
        builder.Property(e => e.Ordinal).IsRequired();
        builder.Property(e => e.Direction).HasConversion<int>().IsRequired();
        builder.Property(e => e.Text).HasMaxLength(Constatnts.FieldLength.Text1024).IsRequired();
    }

    protected override void ConfigureIndexes(EntityTypeBuilder<ChatMessageEntity> builder)
    {
        // Курсор поллинга: WHERE SessionId = @s AND Ordinal > @after ORDER BY Ordinal.
        // Уникальность заодно не даёт двум одновременным вставкам занять один номер.
        builder.HasIndex(e => new { e.SessionId, e.Ordinal }).IsUnique();

        // Сопоставление reply гида с сообщением; уникальность отсекает дубль при повторной
        // доставке апдейта Telegram.
        builder.HasIndex(e => e.TgMessageId).IsUnique().HasFilter("\"TgMessageId\" IS NOT NULL");

        // Outbox: что ещё не уехало в группу гидов.
        builder.HasIndex(e => e.Id).HasFilter("\"TgMessageId\" IS NULL AND \"Direction\" = 0");
    }
}
