using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Model.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDefaultSubjectSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Chapters",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Chapters",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Chapters",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Subjects",
                keyColumn: "Id",
                keyValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Subjects",
                columns: new[] { "Id", "Code", "Description", "Name", "TeacherUserId" },
                values: new object[,]
                {
                    { 1, "PRN222", "ASP.NET Core", "Building Cross-Platform Back-End", null },
                    { 2, "SWP391", "Capstone", "Software Project", null }
                });

            migrationBuilder.InsertData(
                table: "Chapters",
                columns: new[] { "Id", "OrderNumber", "SubjectId", "Title" },
                values: new object[,]
                {
                    { 1, 1, 1, "Chương 1: Giới thiệu" },
                    { 2, 2, 1, "Chương 2: MVC & EF Core" },
                    { 3, 1, 2, "Chương 1: Khởi động dự án" }
                });
        }
    }
}
