using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Enigma.Server.Data;
using Enigma.Server.Data.Repositories.Auth;
using Enigma.Server.Services;
using Enigma.Server.Services.Interfaces;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UsuarioRepository>();
builder.Services.AddSingleton<ICurrentUserService, CurrentUserService>();
// Configure OpenAPI

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
    options.AddPolicy("DevFrontend", policy =>
        policy.WithOrigins("http://localhost:80", "http://127.0.0.1:80")
              .AllowAnyHeader()
              .AllowAnyMethod()));

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
    
    app.MapScalarApiReference("/api/docs", options =>
    {
        options.WithTitle("Enigma API")
               .WithOpenApiRoutePattern("/openapi/v1.json");
    }   );
    // Auto-apply migrations in development — retry briefly, but never take the
    // app down if the database is unreachable (dev DB may not be up yet).
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<EnigmaDbContext>();
    var migrationsApplied = false;
    for (var attempt = 1; attempt <= 5 && !migrationsApplied; attempt++)
    {
        try
        {
            db.Database.Migrate();
            migrationsApplied = true;
            app.Logger.LogInformation("Database migrations applied");
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Could not apply database migrations (attempt {Attempt}/5) — continuing without DB", attempt);
            if (attempt < 5)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }
}

app.UseHttpsRedirection();
app.UseCors("DevFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CurrentUserMiddleware>();
app.MapControllers();

app.Run();

public partial class Program { }