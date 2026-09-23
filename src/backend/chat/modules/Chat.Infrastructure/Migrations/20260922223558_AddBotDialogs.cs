using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBotDialogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TgChatId",
                table: "ChatSessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TgUsername",
                table: "ChatSessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BotDialogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TgUserId = table.Column<long>(type: "bigint", nullable: false),
                    TgChatId = table.Column<long>(type: "bigint", nullable: false),
                    Lang = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Step = table.Column<int>(type: "integer", nullable: false),
                    Route = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DateText = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    People = table.Column<int>(type: "integer", nullable: true),
                    Wishes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PendingText = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ScreenMessageId = table.Column<int>(type: "integer", nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmitDayUtc = table.Column<DateOnly>(type: "date", nullable: true),
                    SubmitDayCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_BotDialogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_TgChatId",
                table: "ChatSessions",
                column: "TgChatId");

            migrationBuilder.CreateIndex(
                name: "IX_BotDialogs_LastActivityAt",
                table: "BotDialogs",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_BotDialogs_TgUserId",
                table: "BotDialogs",
                column: "TgUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BotDialogs");

            migrationBuilder.DropIndex(
                name: "IX_ChatSessions_TgChatId",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "TgChatId",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "TgUsername",
                table: "ChatSessions");
        }
    }
}
