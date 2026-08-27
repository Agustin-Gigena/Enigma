using System.Text;
using System.Threading.RateLimiting;
using Enigma.Server.Data;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Data.Repositories.Auth;
using Enigma.Server.Options;
using Enigma.Server.Services.Auth;
using Enigma.Server.Services.Seed;
using Enigma.Shared.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8081);
});

// Add services to the container.
builder.Services.AddControllers(mvc =>
{
    // Regla de namespace: autorización por sección de catálogo (ver SeccionControllerConvention).
    mvc.Conventions.Add(new SeccionControllerConvention());
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UsuarioRepository>();
builder.Services.AddScoped<MembresiaRepository>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IMembresiaService, MembresiaService>();
// Configure OpenAPI

builder.Services.AddOpenApi();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("login", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
});

// CORS: el front (host :8080) y la API (host :8081) son orígenes distintos;
// sin estos headers el navegador bloquea las llamadas cruzadas (error CORS).
// Política permisiva para desarrollo; restringir orígenes para producción.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        string? corsOrigins = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
        string? corsHeaders = Environment.GetEnvironmentVariable("CORS_ALLOWED_HEADERS");
        string? corsMethods = Environment.GetEnvironmentVariable("CORS_ALLOWED_METHODS");

        if (corsOrigins is null)
        {
            // Dev: allow any localhost origin (any port, http or https)
            policy.SetIsOriginAllowed(origin =>
                origin.StartsWith("http://localhost:", StringComparison.Ordinal) ||
                origin.StartsWith("https://localhost:", StringComparison.Ordinal));
        }
        else
        {
            // Production: explicit origins from env var
            string[] origins = corsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries);
            policy.WithOrigins(origins);
        }

        if (corsHeaders is not null)
        {
            string[] headers = corsHeaders.Split(',', StringSplitOptions.RemoveEmptyEntries);
            policy.WithHeaders(headers);
        }
        else
        {
            policy.AllowAnyHeader();
        }

        if (corsMethods is not null)
        {
            string[] methods = corsMethods.Split(',', StringSplitOptions.RemoveEmptyEntries);
            policy.WithMethods(methods);
        }
        else
        {
            policy.AllowAnyMethod();
        }

        // REQUIRED: sends HttpOnly cookie cross-origin
        policy.AllowCredentials();
    });
});

// Build the MySQL connection string from environment variables (with dev defaults).
// NOTE: ${VAR} placeholders in appsettings.json are NOT expanded by .NET
// IConfiguration (EnvSubst is not native). We assemble the string here.
string connectionString = $"Server={Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "localhost"};"
    + $"Port={Environment.GetEnvironmentVariable("MYSQL_PORT") ?? "3306"};"
    + $"Database={Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? "enigma_db"};"
    + $"User Id={Environment.GetEnvironmentVariable("MYSQL_USER") ?? "enigma"};"
    + $"Password={Environment.GetEnvironmentVariable("MYSQL_PASSWORD") ?? "enigma_dev_password"};"
    + $"ConnectionTimeout=3;";

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
    // Lazy loading como convención del proyecto: proxies de Castle requieren
    // navegaciones virtual en las entidades.
    .UseLazyLoadingProxies());

// ASP.NET Core Identity: usuarios, contraseñas con hash y bloqueo por intentos.
builder.Services.AddIdentity<Usuario, Rol>(options =>
    {
        builder.Configuration.GetSection("Identity:Password").Bind(options.Password);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<EnigmaDbContext>()
    .AddDefaultTokenProviders();

// JWT bearer: el CurrentUserService y el CurrentUserMiddleware consumen los
// claims (NameIdentifier -> Usuario) emitidos por POST /auth/login.
string jwtSecret = JwtSecretResolver.Resolve(
    Environment.GetEnvironmentVariable("ENIGMA_JWT_SECRET"),
    builder.Environment.IsDevelopment());
JwtOptions jwtOptions = new() { Secret = jwtSecret };
jwtOptions.EnsureValid();
builder.Services.AddSingleton(Options.Create(jwtOptions));
builder.Services.AddSingleton<ITokenService, TokenService>();
// Identity registra cookies como default scheme; forzamos JWT para la API
// (los tres defaults, no solo DefaultScheme) para que [Authorize] desafíe
// con el bearer y no con /Account/Login.
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "Enigma",
            ValidateAudience = true,
            ValidAudience = "Enigma.Client",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Read JWT from HttpOnly cookie instead of Authorization header
                string? token = context.Request.Cookies["enigma_token"];
                if (!string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    // DefaultPolicy: [Authorize] exige sesión completa (el pre-auth de 5 min no alcanza).
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(EnigmaClaims.Tipo, EnigmaClaims.Sesion)
        .Build();
    // Para los endpoints de elección de institución (aceptan pre-auth y sesión).
    options.AddPolicy("PreAutenticacion", policy => policy.RequireAuthenticatedUser());
});

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
// Order matters: Exception -> CORS -> Auth -> Authorization.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseSecurityHeaders();

if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference("/api/docs", options =>
    {
        options.WithTitle("Enigma API")
               .WithOpenApiRoutePattern("/openapi/v1.json");
    });

    // Auto-apply migrations in development — retry briefly, but never take the
    // app down if the database is unreachable (dev DB may not be up yet).
    using IServiceScope scope = app.Services.CreateScope();
    EnigmaDbContext db = scope.ServiceProvider.GetRequiredService<EnigmaDbContext>();
    bool migrationsApplied = false;
    for (int attempt = 1; attempt <= 5 && !migrationsApplied; attempt++)
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

    if (migrationsApplied && app.Environment.IsDevelopment())
    {
        try
        {
            await SeedingService.SeedAsync(app.Services, app.Logger);
            app.Logger.LogInformation("Dev seed applied");
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Could not apply dev seed — continuing");
        }
    }
}

app.MapGet("/health", () => Results.Ok());

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CurrentUserMiddleware>();
app.MapControllers();

app.Run();


public partial class Program { }
