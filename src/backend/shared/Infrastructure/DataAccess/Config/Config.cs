using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql.EntityFrameworkCore.PostgreSQL.ValueGeneration;

namespace Infrastructure.DataAccess;

public abstract class Config<T> : IEntityTypeConfiguration<T> where T : class, IEntity
{
    protected abstract string TableName { get; }
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.ToTable(TableName);
            
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Id).HasValueGenerator<NpgsqlSequentialGuidValueGenerator>();

        ConfigureEntity(builder);
            
        ConfigureIndexes(builder);
        
        ConfigureIgnoredProperties(builder);
    }

    protected abstract void ConfigureEntity(EntityTypeBuilder<T> builder);
    protected abstract void ConfigureIndexes(EntityTypeBuilder<T> builder);

    protected virtual void ConfigureIgnoredProperties(EntityTypeBuilder<T> builder)
    {
        
    }
}