using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;

namespace Infrastructure.DataAccess;

/// <summary>
/// Overrides Npgsql migration SQL generation so that CREATE COLLATION is always
/// wrapped in a DO $$ IF NOT EXISTS $$ block. This prevents "collation already exists"
/// errors when multiple services migrate against the same shared database.
/// </summary>
public class SafeNpgsqlMigrationsSqlGenerator(
    MigrationsSqlGeneratorDependencies dependencies,
    INpgsqlSingletonOptions npgsqlSingletonOptions)
    : NpgsqlMigrationsSqlGenerator(dependencies, npgsqlSingletonOptions)
{
    private const string CollationAnnotationPrefix = "Npgsql:CollationDefinition:";

    protected override void Generate(
        AlterDatabaseOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        foreach (var annotation in operation.GetAnnotations()
                     .Where(a => a.Name.StartsWith(CollationAnnotationPrefix))
                     .ToList())
        {
            var collationName = annotation.Name[CollationAnnotationPrefix.Length..];
            var parts = ((string)annotation.Value!).Split(',');
            var locale = parts[0];
            var provider = parts[2];
            var deterministic = parts[3].Equals("True", StringComparison.OrdinalIgnoreCase);

            builder.AppendLine($"""
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_collation WHERE collname = '{collationName}') THEN
                        CREATE COLLATION "{collationName}" (provider = {provider}, locale = '{locale}', deterministic = {(deterministic ? "true" : "false")});
                    END IF;
                EXCEPTION WHEN duplicate_object THEN
                    NULL;
                END $$;
                """);
            builder.EndCommand(suppressTransaction: true);

            operation.RemoveAnnotation(annotation.Name);
        }

        base.Generate(operation, model, builder);
    }
}
