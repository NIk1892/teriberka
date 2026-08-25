using System.Data;
using Application;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure;

public abstract class WriteDbContext : DbContext, IWriteDbContext
{
    protected WriteDbContext()
    {
    }

    protected WriteDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public async Task MigrateAsync()
    {
        // Pre-create shared database objects to avoid conflicts between services migrating concurrently.
        var connection = (NpgsqlConnection)Database.GetDbConnection();
        await connection.OpenAsync();

        // Schema routing is done by Postgres' search_path (read from the connection string). EF Core
        // emits unqualified CREATE TABLE so tables land in search_path's first schema — but Postgres
        // won't auto-create the schema itself, so we do it here.
        var schema = ResolveLeadingSchema(connection.ConnectionString);
        if (!string.IsNullOrEmpty(schema))
        {
            await using var schemaCmd = connection.CreateCommand();
            schemaCmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{schema}\";";
            await schemaCmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = connection.CreateCommand())
        {
            // WITH SCHEMA public: search_path у сервисов — `users,public` / `chat,public`,
            // и CREATE EXTENSION без схемы ставит citext в первую из них. Соседний сервис
            // потом видит «расширение уже есть», а типа в своём search_path нет.
            cmd.CommandText = """
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_collation WHERE collname = 'case_insensitive') THEN
                        CREATE COLLATION case_insensitive (provider = icu, locale = 'ru_RU.UTF-8', deterministic = false);
                    END IF;
                EXCEPTION WHEN duplicate_object THEN
                    NULL;
                END $$;
                CREATE EXTENSION IF NOT EXISTS citext WITH SCHEMA public;
                CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA public;
                CREATE EXTENSION IF NOT EXISTS btree_gin WITH SCHEMA public;
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM pg_extension e
                        JOIN pg_namespace n ON n.oid = e.extnamespace
                        WHERE e.extname = 'citext' AND n.nspname <> 'public') THEN
                        ALTER EXTENSION citext SET SCHEMA public;
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM pg_extension e
                        JOIN pg_namespace n ON n.oid = e.extnamespace
                        WHERE e.extname = 'pg_trgm' AND n.nspname <> 'public') THEN
                        ALTER EXTENSION pg_trgm SET SCHEMA public;
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM pg_extension e
                        JOIN pg_namespace n ON n.oid = e.extnamespace
                        WHERE e.extname = 'btree_gin' AND n.nspname <> 'public') THEN
                        ALTER EXTENSION btree_gin SET SCHEMA public;
                    END IF;
                END $$;
                """;
            await cmd.ExecuteNonQueryAsync();
        }
        await connection.CloseAsync();

        await Database.MigrateAsync();
    }

    public async Task EnsureCreatedAsync()
    {
        await Database.EnsureCreatedAsync();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.HasPostgresExtension("btree_gin");

        modelBuilder.HasCollation("case_insensitive", locale: "ru_RU.UTF-8", provider: "icu", deterministic: false);
    }

    private static string? ResolveLeadingSchema(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.SearchPath)) return null;
        var first = builder.SearchPath.Split(',', 2)[0].Trim();
        return string.Equals(first, "public", StringComparison.OrdinalIgnoreCase) ? null : first;
    }
}
