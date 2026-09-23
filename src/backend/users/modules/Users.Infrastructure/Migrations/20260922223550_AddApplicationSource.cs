using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Users.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "Applications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Applications",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "site");

            migrationBuilder.AddColumn<long>(
                name: "TgUserId",
                table: "Applications",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TgUsername",
                table: "Applications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Details",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "TgUserId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "TgUsername",
                table: "Applications");
        }
    }
}
