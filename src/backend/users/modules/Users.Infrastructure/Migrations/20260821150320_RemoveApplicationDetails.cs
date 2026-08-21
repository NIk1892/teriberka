using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Users.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveApplicationDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_ArrivalDate",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_Id",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ArrivalDate",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "PeopleCount",
                table: "Applications");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Applications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Id",
                table: "Applications",
                column: "Id")
                .Annotation("Npgsql:IndexInclude", new[] { "Title", "Phone" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_Id",
                table: "Applications");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Applications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ArrivalDate",
                table: "Applications",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "Applications",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeopleCount",
                table: "Applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ArrivalDate",
                table: "Applications",
                column: "ArrivalDate");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Id",
                table: "Applications",
                column: "Id")
                .Annotation("Npgsql:IndexInclude", new[] { "Title", "Phone", "PeopleCount", "ArrivalDate" });
        }
    }
}
