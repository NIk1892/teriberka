using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Users.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixTitleComputedColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Users",
                type: "citext",
                maxLength: 255,
                nullable: true,
                computedColumnSql: "TRIM(COALESCE(\"FirstName\", '') || ' ' || COALESCE(\"LastName\", ''))",
                stored: true,
                oldClrType: typeof(string),
                oldType: "citext",
                oldMaxLength: 255,
                oldNullable: true,
                oldComputedColumnSql: "\"FirstName\" || ' ' || \"LastName\"",
                oldStored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Users",
                type: "citext",
                maxLength: 255,
                nullable: true,
                computedColumnSql: "\"FirstName\" || ' ' || \"LastName\"",
                stored: true,
                oldClrType: typeof(string),
                oldType: "citext",
                oldMaxLength: 255,
                oldNullable: true,
                oldComputedColumnSql: "TRIM(COALESCE(\"FirstName\", '') || ' ' || COALESCE(\"LastName\", ''))",
                oldStored: true);
        }
    }
}
