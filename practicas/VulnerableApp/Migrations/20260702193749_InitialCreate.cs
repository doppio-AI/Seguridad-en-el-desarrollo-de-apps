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
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                columns: new[] { "Id", "Balance", "CreatedAt", "Email", "Password", "PasswordHash", "Username" },
                values: new object[,]
                {
                    { 1, 1000m, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "admin@test.com", "admin", "$2b$11$.hn63SQVzdNRGjD8s7z2ku0TgnAOUcaKphgsmENA9UFGbrqcEXpaS", "admin" },
                    { 2, 500m, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "user@test.com", "123456", "$2b$11$YhMOm3Kw9pWhdrBTGtQqfO6Y6DpGSkIsrP3g3yWqwO9rDWFrgjJsC", "user1" },
                    { 3, 750m, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "user2@test.com", "password", "$2b$11$9EwQ4bIoyc5GOxKi6MZSueGbDRZFqtXBlfi9Y7gS1TOPB/xB5dV2u", "user2" }
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
