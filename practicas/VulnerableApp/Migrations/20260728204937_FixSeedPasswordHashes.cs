using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VulnerableApp.Migrations
{
    /// <inheritdoc />
    public partial class FixSeedPasswordHashes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$nMD.vQH858TzSJiSyNKexucoSuTx3U58hzaN09P40bDzluK1XP6M2");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                column: "PasswordHash",
                value: "$2a$11$kzcuxEZN/oNktHiSoAUYaOWEhWs7MxgTShk.o9MBIi.z7BwtL6tIm");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3,
                column: "PasswordHash",
                value: "$2a$11$Yr9Dm.2oDsD5h8.uXXBkHecPNENuJnowA/2CUcSk2TOvMz4mBiGSi");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2b$11$.hn63SQVzdNRGjD8s7z2ku0TgnAOUcaKphgsmENA9UFGbrqcEXpaS");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                column: "PasswordHash",
                value: "$2b$11$YhMOm3Kw9pWhdrBTGtQqfO6Y6DpGSkIsrP3g3yWqwO9rDWFrgjJsC");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3,
                column: "PasswordHash",
                value: "$2b$11$9EwQ4bIoyc5GOxKi6MZSueGbDRZFqtXBlfi9Y7gS1TOPB/xB5dV2u");
        }
    }
}
