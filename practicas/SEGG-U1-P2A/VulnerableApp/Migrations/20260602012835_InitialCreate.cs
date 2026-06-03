using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VulnerableApp.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Balance", "CreatedAt", "Email", "Password", "Username" },
                values: new object[,]
                {
                    { 1, 1000m, new DateTime(2026, 6, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), "admin@test.com", "admin", "admin" },
                    { 2, 500m, new DateTime(2026, 6, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), "user@test.com", "123456", "user1" },
                    { 3, 750m, new DateTime(2026, 6, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), "user2@test.com", "password", "user2" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
