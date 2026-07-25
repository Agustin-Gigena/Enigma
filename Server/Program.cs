using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Enigma.Server.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Configure OpenAPI

builder.Services.AddOpenApi();

// Build the MySQL connection string from environment variables (with dev defaults).
// NOTE: ${VAR} placeholders in appsettings.json are NOT expanded by .NET
// IConfiguration (EnvSubst is not native). We assemble the string here.
var connectionString = $"Server={Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "localhost"};"
    + $"Port={Environment.GetEnvironmentVariable("MYSQL_PORT") ?? "3306"};"
    + $"Database={Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? "enigma_db"};"
    + $"User Id={Environment.GetEnvironmentVariable("MYSQL_USER") ?? "enigma"};"
    + $"Password={Environment.GetEnvironmentVariable("MYSQL_PASSWORD") ?? "enigma_dev_password"};";

// Configure DbContext
// Use a fixed MySQL 8.0 server version (no AutoDetect) so design-time EF
// (dotnet ef migrations add / database update) does not open a TCP connection.
builder.Services.AddDbContext<EnigmaDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 42)),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null
        )
    )
);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    
    // Auto-apply migrations in development
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<EnigmaDbContext>();
    db.Database.Migrate();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();