using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Enigma.Server.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Configure OpenAPI

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// Configure DbContext
builder.Services.AddDbContext<EnigmaDbContext>(options =>
    
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(ServerVersion.AutoDetect(connectionString)),
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