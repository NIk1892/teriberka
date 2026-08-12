using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Users.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Phone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PeopleCount = table.Column<int>(type: "integer", nullable: false),
                    ArrivalDate = table.Column<DateOnly>(type: "date", nullable: false),
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
                    table.PrimaryKey("PK_Applications", x => x.Id);
                });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Applications");
        }
    }
}
