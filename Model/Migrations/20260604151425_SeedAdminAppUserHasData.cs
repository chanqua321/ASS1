using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Model.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdminAppUserHasData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AppUsers",
                columns: new[] { "Id", "CreatedAt", "Email", "PasswordHash", "Role" },
                values: new object[] { 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin@gmail.com", "100000.RWR1QUlfVXNlclNlZWQhIQ==.Bexafofd0/xrgmR+AgoioivdJ1P/cIauSK07DRkGD3U=", "Admin" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AppUsers",
                keyColumn: "Id",
                keyValue: 1);
        }
    }
}
