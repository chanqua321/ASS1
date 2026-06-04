using Microsoft.EntityFrameworkCore.Migrations;
using System.Security.Cryptography;

#nullable disable

namespace Model.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdminUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed admin via migration so `dotnet ef database update` is enough.
            // Password hash format MUST match BusinessLogic.Helpers.PasswordHashHelper:
            // {iterations}.{saltBase64}.{hashBase64}
            var email = "admin@gmail.com";
            var role = "Admin";
            var passwordHash = HashPassword("123");

            migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM [dbo].[AppUsers] WHERE [Email] = N'{email}')
BEGIN
    INSERT INTO [dbo].[AppUsers] ([Email], [PasswordHash], [Role], [CreatedAt])
    VALUES (N'{email}', N'{passwordHash}', N'{role}', SYSUTCDATETIME());
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [dbo].[AppUsers]
WHERE [Email] = N'admin@gmail.com';
");
        }

        private static string HashPassword(string password, int iterations = 100_000, int saltSize = 16, int keySize = 32)
        {
            var salt = RandomNumberGenerator.GetBytes(saltSize);
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(keySize);
            return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }
    }
}
