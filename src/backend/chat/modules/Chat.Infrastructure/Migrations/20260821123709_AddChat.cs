using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:CollationDefinition:case_insensitive", "ru_RU.UTF-8,ru_RU.UTF-8,icu,False")
                .Annotation("Npgsql:PostgresExtension:btree_gin", ",,")
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    TgMessageId = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "citext", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Audit_CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "NOW()"),
                    Audit_ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "NOW()"),
                    Audit_CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    Audit_ModifiedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Culture = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Page = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    MessageCount = table.Column<int>(type: "integer", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TopicMessageId = table.Column<long>(type: "bigint", nullable: true),
                    AdminChatId = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "citext", maxLength: 255, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Audit_CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "NOW()"),
                    Audit_ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "NOW()"),
                    Audit_CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    Audit_ModifiedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_Id",
                table: "ChatMessages",
                column: "Id",
                filter: "\"TgMessageId\" IS NULL AND \"Direction\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SessionId_Ordinal",
                table: "ChatMessages",
                columns: new[] { "SessionId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_TgMessageId",
                table: "ChatMessages",
                column: "TgMessageId",
                unique: true,
                filter: "\"TgMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_LastMessageAt",
                table: "ChatSessions",
                column: "LastMessageAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_Token",
                table: "ChatSessions",
                column: "Token",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_TopicMessageId",
                table: "ChatSessions",
                column: "TopicMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "ChatSessions");
        }
    }
}
