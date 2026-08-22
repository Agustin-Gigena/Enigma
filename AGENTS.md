# Repository Guidelines

**Enigma** — Full-stack .NET 10 application with Blazor WebAssembly client, ASP.NET Core Web API server, and MySQL backend. Licensed under the Functional Source License 1.1 (FSL-1.1-ALv2): source-available, Competing Use prohibited, converts to Apache 2.0 after 2 years per version.

---

## Project Overview

Enigma is a modular, component-based monorepo with three projects: **Client** (Blazor WASM), **Server** (ASP.NET Core API), and **Shared** (common types), all versioned together in a single Git repository (no submodules). Built on .NET 10, it implements soft-delete audit tracking, dependency injection, and repository patterns.

---

## Architecture & Data Flow

```
┌─────────────────────┐     ┌──────────────────────────┐     ┌─────────────────┐
│  Client (Blazor)    │────▶│  Server (Web API)        │────▶│  MySQL 8.0      │
│  net10.0            │◀────│  net10.0 + EF Core 10    │◀────│  (Docker)       │
│                     │     │                          │     │                 │
│  - App.razor        │     │  - Program.cs (entry)    │     │  - enigma_db    │
│  - MainLayout       │     │  - Controllers           │     │  - Persistent   │
│  - Pages/*.razor    │     │  - Services              │     │    volume       │
│  - HttpClient       │     │  - Repositories          │     │                 │
│                     │     │  - DbContext             │     │                 │
└─────────────────────┘     └──────────────────────────┘     └─────────────────┘
```

**Key Modules:**

- **Client**: Blazor WebAssembly (`Client/Enigma.Client.csproj`)
  - Entry: `Program.cs`, `App.razor`
  - Layout: `MainLayout.razor`, `NavMenu.razor`
  - Pages: `Home.razor`, `Counter.razor`, `Weather.razor`, `NotFound.razor`
  - Services: Scoped `HttpClient` with base address injection

- **Server**: ASP.NET Core Web API (`Server/Enigma.Server.csproj`)
  - Entry: `Server/Program.cs`
  - DI: DbContext with MySQL retry (3 retries, 5s delay), controllers, OpenAPI
  - Auto-migrates DB in development

- **Shared**: Class library (`Shared/Enigma.Shared.csproj`)
  - Currently empty — intended for DTOs, shared models, constants

**Data Flow:**

```
Client Page (@inject HttpClient)
  → HTTP request (JSON)
    → Server Controller
      → Service Layer
        → GenericRepository<T>
          → EnigmaDbContext (EF Core)
            → MySQL
```

---

## Key Directories

| Path | Purpose | Notes |
|------|---------|-------|
| `Client/` | Blazor WebAssembly frontend | Pages, Layout, wwwroot |
| `Client/Pages/` | Razor components (routes) | `@page` directive routing |
| `Client/Layout/` | Layout + NavMenu | Scoped CSS files |
| `Server/` | ASP.NET Core API | Controllers, Services, Repositories |
| `Server/Data/` | EF Core DbContext, entities | `EnigmaDbContext.cs`, `GenericEntity.cs` |
| `Server/Data/Entities/` | Domain entities | Organized by domain (Auth, Administración) |
| `Server/Data/Repositories/` | Repository implementations | Generic pattern with soft-delete |
| `Server/Controllers/` | API endpoints | Domain folders (Auth/) |
| `Server/Services/` | Business logic, helpers | Domain folders (Auth/) |
| `Shared/` | Common types (empty) | Add DTOs, shared enums, constants |
| `.devcontainer/` | Dev container config | .NET 10 SDK, Oh My Pi, C# extensions |


**Domain Folder Structure:** la estructura de carpetas de `Data` (subcarpetas por
dominio de negocio) se replica en `Controllers` y `Services`:

```
Server/
├── Controllers/
│   └── Auth/            # Enigma.Server.Controllers.<Dominio>
├── Services/
│   └── Auth/            # Enigma.Server.Services.<Dominio>
└── Data/
    ├── Entities/
    │   ├── Auth/
    │   └── Administracion/
    └── Repositories/
        └── Auth/
```

- Cada dominio de negocio (Auth, Administración, …) tiene su carpeta en las
  tres capas; el namespace C# refleja la ruta (`Enigma.Server.Controllers.Auth`,
  `Enigma.Server.Services.Auth`, `Enigma.Server.Data.Entities.Auth`).
- Archivos genéricos o transversales quedan en la raíz de su capa
  (`GenericEntity`, `GenericRepository`, `EnigmaDbContext`).
- Interfaz e implementación viven juntas: `IUsuarioService` + `UsuarioService`
  en `Services/Auth/UsuarioService.cs`, `ICurrentUserService` +
  `CurrentUserService` en `Services/Auth/CurrentUserService.cs`.
---

## Development Commands

**Build & Run:**

```bash
# Build all projects
dotnet build Enigma.slnx

# Run server (API)
dotnet run --project Server/Enigma.Server.csproj

# Run client (Blazor WASM dev server)
dotnet run --project Client/Enigma.Client.csproj

# Run both via solution
dotnet run --project Enigma.slnx
```

**Database:**

```bash
# Start MySQL dev database
docker-compose up -d

# Apply EF Core migrations
dotnet ef migrations add MigrationName --project Server/Enigma.Server.csproj
dotnet ef database update --project Server/Enigma.Server.csproj

# Auto-migration enabled in development (Program.cs auto-applies migrations)
```

**Test (none implemented):**

```bash
# Placeholders only
dotnet test
```

**Environment Setup:**

```bash
# 1. Start MySQL container
docker-compose up -d

# 2. Set environment variables (Program.cs assembles the connection string)
#    Server/Program.cs reads: MYSQL_HOST, MYSQL_PORT, MYSQL_DATABASE,
#    MYSQL_USER, MYSQL_PASSWORD (with dev defaults: localhost/3306/enigma_db/
#    enigma/enigma_dev_password). appsettings.json keeps ${MYSQL_*}
#    placeholders only as documentation — .NET IConfiguration does NOT
#    expand ${VAR}.

# 3. Apply migrations
dotnet ef database update --project Server

# 4. Run server + client separately or via solution
```

---

## Code Conventions & Common Patterns

**Language & Configuration:**

- Nullable reference types: **enabled** (`<Nullable>enable</Nullable>`)
- Implicit usings: **enabled** (`<ImplicitUsings>enable</ImplicitUsings>`)
- Target framework: **net10.0** (all projects)
- Naming: **Spanish** for entity properties (e.g., `CreadoEn`, `BorradoLogico`, `Usuario`)

**Dependency Injection:**

All DI configured in `Program.cs`:

```csharp
builder.Services.AddDbContext<EnigmaDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(ServerVersion.AutoDetect(connectionString)),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null
        )
    ));
builder.Services.AddControllers();
builder.Services.AddOpenApi();
```

**Service Registration Pattern:**

```csharp
// Singleton
builder.Services.AddSingleton<IService, Service>();

// Scoped
builder.Services.AddScoped<IService, Service>();

// Transient
builder.Services.AddTransient<IService, Service>();
```

**Repository Pattern:**

Generic repository with soft-delete support:

```csharp
public abstract class GenericRepository<T> where T : class
{
    private readonly EnigmaDbContext _context;

    public GenericRepository(EnigmaDbContext context) { /* DI */ }

    public virtual T? GetById(int id, bool borradoLogico = false) { /* ... */ }
    public bool SetBorradoLogico(int id, bool borradoLogico) { /* ... */ }
}
```

**Entity Base Class:**

All entities inherit from `GenericEntity` for audit tracking:

```csharp
public abstract class GenericEntity : IDisposable
{
    public int Id { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public Usuario CreadoPor { get; set; } = null!;
    public DateTime? ModificadoEn { get; set; }
    public Usuario? ModificadoPor { get; set; }
    public DateTime? BorradoEn { get; set; }
    public Usuario? BorradoPor { get; set; }
    public bool BorradoLogico { get; set; } = false;

    public virtual void SetCreadoPor(Usuario? usuario = null) { /* audit */ }
    public virtual void SetModificadoPor(Usuario? usuario = null) { /* audit */ }
    public virtual void SetBorradoLogico(bool borradoLogico, Usuario? usuario = null) { /* soft-delete */ }
}
```

**Soft-Delete Pattern:**

```csharp
// Mark as deleted (logical delete)
entity.SetBorradoLogico(true, user);

// Query excludes soft-deleted unless explicitly included
var entity = repo.GetById(id, borradoLogico: false); // default
```

**Async/Await Usage:**

Current repositories: **SYNCHRONOUS** (refactor needed):

```csharp
public virtual T? GetById(int id, bool borradoLogico = false)
{
    return _context.Set<T>().Find(id); // Synchronous
}
```

Expect async pattern in new code:

```csharp
public virtual async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
{
    return await _context.Set<T>().FindAsync(id, ct);
}

**Error Handling:**

- Entity operations throw `InvalidOperationException` when user context missing
- Controller pattern: `[ApiController]` for automatic 400 responses
- DbContext retry on MySQL transient failures (3 retries, 5s delay)

**State Management:**

- **Client**: Blazor component state (`@currentCount`, `@loading`)
- **Server**: EF Core change tracking via DbContext
- **Audit fields**: Auto-populated via `CurrentUserService.GetCurrentUser()` (acceso ambiental, sin inyección)

---

## Important Files

| File | Purpose | Key Details |
|------|---------|-------------|
| `Enigma.slnx` | Solution file | 3 projects: Client, Server, Shared |
| `Server/Program.cs` | Server entry point | DI, migration auto-run, OpenAPI |
| `Server/Data/EnigmaDbContext.cs` | EF Core DbContext | Currently minimal, OnModelCreating empty |
| `Server/Data/GenericEntity.cs` | Base entity class | Audit fields, soft-delete; audit via static `CurrentUserService` |
| `Server/Data/Repositories/GenericRepository.cs` | Generic repo | Base sync methods + soft-delete support |
| `Server/Controllers/GenericController.cs` | Base controller | `[ApiController]`, `[Route("[controller]")]` |
| `Server/Controllers/Auth/AuthController.cs` | Auth endpoints | `POST auth/login`, `GET auth/me`, `GET auth/instituciones` |
| `Server/Services/Auth/CurrentUserService.cs` | Current-user ambient access | Static `IsAuthenticated`/`GetClaimsPrincipal`/`GetCurrentUser` + `ICurrentUserService`; `IHttpContextAccessor` + AsyncLocal scope seeded by `CurrentUserMiddleware` |
| `Server/Services/Auth/UsuarioService.cs` | Auth business logic | `IUsuarioService` + `UsuarioService`: login vía Identity (SignInManager/UserManager) + consultas al `UsuarioRepository` |
| `docker-compose.yml` | MySQL dev DB | Credentials: enigma/enigma_dev_password |
| `Server/appsettings.json` | Configuration | Environment variable interpolation |

---

## Runtime & Tooling Preferences

**Runtime Requirements:**

- **.NET 10 SDK** (as of 2026-07-07, bleeding edge)
- **MySQL 8.0** (via Docker Compose for development)
- **Docker Compose** for local database

**Package Manager:**

- NuGet (native to .NET SDK)

**Tooling:**

- EF Core Tools 10.0.0 (`dotnet ef`)
- Visual Studio Code extensions: `ms-dotnettools.csharp`, `ms-dotnettools.csdevkit`
- Devcontainer: `mcr.microsoft.com/dotnet/sdk:10.0` with Oh My Pi feature

**Constraints:**

- No production Dockerfile defined
- No CI/CD pipeline configured
- No test projects (xUnit/NUnit/MSTest) — only .gitignore placeholders
- Server does **not** explicitly reference Shared project (add `ProjectReference` if needed)

---

## Testing & QA

**Current State:**

- **NO test projects exist** in repository
- `.gitignore` contains placeholders for `TestResults/*.trx` and `*.coverage`
- No xUnit, NUnit, MSTest, Moq, or test host configuration
- No integration tests for API controllers or DbContext
- No E2E tests for Blazor client

**Expected Test Structure (when added):**

```
Enigma/
├── Server/
│   ├── Enigma.Server.csproj
│   └── ...
├── Client/
│   ├── Enigma.Client.csproj
│   └── ...
├── Shared/
├── Server.Tests/              # Unit tests for services, repositories
│   ├── Server.Tests.csproj
│   ├── Repositories/
│   └── Services/
├── Client.Tests/              # bUnit tests for Blazor components
│   ├── Client.Tests.csproj
│   └── Pages/
└── Integration.Tests/         # API integration, DbContext tests
    ├── Integration.Tests.csproj
    └── Api/
```

**Test Commands (when implemented):**

```bash
# Run all tests
dotnet test

# Run specific project
dotnet test Server.Tests/Enigma.Server.Tests.csproj

# With coverage (add coverlet.collector)
dotnet test --collect:"XPlat Code Coverage"
```

**QA Expectations:**

- Unit tests for repository methods ( GetById, SetBorradoLogico, soft-delete logic)
- Service/entity audit tests via the ambient `CurrentUserService` scope (`WebApplicationFactory` E2E, o `InternalsVisibleTo` para `BeginScope`)
- Controller tests with test server
- Blazor component tests using bUnit
- API integration tests using `WebApplicationFactory<Program>`
- Coverage target: undefined (start with ≥80% for new code)

**Code Quality (not enforced):**

- No analyzers configured (add Roslyn analyzers, StyleCop, or SonarLint)
- No formatting rules enforced (consider `.editorconfig` with format rules)
- No pre-commit hooks or CI validation

---

## Notes for AI Assistants

### Protected Configuration and Secrets

- Editing `.editorconfig`, `.gitignore`, `lefthook.yml`, or `.yamllint` requires explicit user confirmation before the edit. This includes overwriting or modifying them through any command or tool.
- Do not modify those protected files as a side effect of another task. If a requested change requires one of them, pause and ask for confirmation before proceeding.
- Do not read, write, edit, copy, move, delete, or inspect `*.env` or `*.env.*` files. Treat them as credential-bearing files; the user must handle any required changes directly.
- Do not expose secrets from environment files or command output in responses, logs, patches, or diagnostics.

1. **Soft-Delete Awareness**: All entities inherit `BorradoLogico` field. Queries must filter unless explicitly including soft-deleted records.

2. **Audit Field Dependencies**: `GenericEntity` resuelve el usuario vía `CurrentUserService.GetCurrentUser()` (estático, sin inyección). New entities **must** call `SetCreadoPor()` / `SetModificadoPor()` before `SaveChanges()`.

3. **Sync-to-Async Migration**: Current repositories are synchronous. New code should use async EF Core methods (`FindAsync`, `SaveChangesAsync`) with `CancellationToken`.

4. **Shared Project Gap**: Shared project is empty. If passing DTOs or enums between Client/Server, add them to `Shared` and update project references:

   ```xml
   <!-- Server/Enigma.Server.csproj -->
   <ItemGroup>
     <ProjectReference Include="..\Shared\Enigma.Shared.csproj" />
   </ItemGroup>
   ```

5. **Auth Implementation**: `CurrentUserService` expone acceso ambiental al usuario actual: `IsAuthenticated()`, `GetClaimsPrincipal()`, `GetCurrentUser()` (entidad `Usuario`, cacheada por request vía `Lazy`) — consumibles desde repositorios/controladores/entidades **sin inyectar nada**. El contexto se siembra con `CurrentUserMiddleware` (AsyncLocal) usando `IHttpContextAccessor`. JWT/cookies aún no configurados: todo request es anónimo hasta entonces. `ENIGMA_AUTH_REQUIRED=true` hace que `GetCurrentUser()` lance `UnauthorizedAccessException` cuando no hay usuario (ausente/false → `null`).

6. **Auto-Migration in Dev**: In development, `Program.cs` auto-applies migrations via `db.Database.Migrate()` (called when `ASPNETCORE_ENVIRONMENT=Development`). Production deployments must run `dotnet ef database update` explicitly.

7. **Database Credentials** (dev only):
   - Host: `localhost` or `${MYSQL_HOST}`
   - Port: 3306
   - Database: `enigma_db`
   - User: `enigma`
   - Password: `enigma_dev_password`
   - Root password: `root_password`

8. **Spanish Naming**: Entity properties use Spanish (e.g., `BorradoLogico`, `CreadoPor`). Do **not** "fix" to English — consistency matters.

9. **Bootstrap Version**: Bootstrap 5 (full dist) shipped in `wwwroot/lib/bootstrap/`. Comment in `index.html` notes deprecation plan for v6.

10. **No Production Container**: No Dockerfile for app containers — only MySQL dev database via docker-compose.yml. Production deployment undefined.