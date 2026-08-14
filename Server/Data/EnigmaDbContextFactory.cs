using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Enigma.Server.Data;

// Design-time factory used by `dotnet ef` (migrations add / database update / migrations list).
// EF Core looks for IDesignTimeDbContextFactory<DbContext> BEFORE attempting to run Program.cs,
// so design-time tooling works without starting the full host (and without a live MySQL).
// The connection string is assembled from the same env vars as Program.cs (with dev defaults).
public class EnigmaDbContextFactory : IDesignTimeDbContextFactory<EnigmaDbContext>
{
    public EnigmaDbContext CreateDbContext(string[] args)
    {
        string connectionString = $"Server={Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "localhost"};"
            + $"Port={Environment.GetEnvironmentVariable("MYSQL_PORT") ?? "3306"};"
            + $"Database={Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? "enigma_db"};"
            + $"User Id={Environment.GetEnvironmentVariable("MYSQL_USER") ?? "enigma"};"
            + $"Password={Environment.GetEnvironmentVariable("MYSQL_PASSWORD") ?? "enigma_dev_password"};";

        DbContextOptions<EnigmaDbContext> options = new DbContextOptionsBuilder<EnigmaDbContext>()
            .UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 42)),
                mySqlOptions => mySqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null
                )
            )
            .Options;

        return new EnigmaDbContext(options);
    }
}
