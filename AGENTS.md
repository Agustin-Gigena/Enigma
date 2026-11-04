# Repository Guidelines

**Enigma** — Full-stack .NET 10 application: Blazor WebAssembly client, ASP.NET Core Web API server, MySQL 8.0 backend. Licensed under the Functional Source License 1.1 (FSL-1.1-ALv2): source-available, Competing Use prohibited, converts to Apache 2.0 after 2 years per version.

**Product & design canon:** `PRODUCT.md` (product vision, scope, open decisions) and `DESIGN.md` (design system "Jade Institucional" — colors, typography, layout, motion). Consult both before building UI or scoping features. Historical specs/plans live in `docs/superpowers/specs/` and `docs/superpowers/plans/`.

---

## Project Overview

Single Git monorepo with four projects: **Server** (ASP.NET Core API), **Client** (Blazor WASM), **Shared** (DTOs/contracts), **Tests** (NUnit unit + Playwright E2E + Stryker mutation). Auth is implemented end-to-end: ASP.NET Core Identity + JWT bearer, login → selección de institución → home. Domain folders (Auth, Administracion, Seed) are replicated across Controllers/Services/Data.

---

## Architecture & Data Flow

```
Browser (PWA, :8080)                    Server (:8081)                     MySQL 8.0
┌──────────────────────┐  HTTP+JWT   ┌────────────────────────┐          ┌──────────────┐
│ Client (Blazor WASM) │────────────▶│ AuthController         │─────────▶│ enigma_db    │
│ - AuthService        │◀────────────│   POST auth/login      │  EF Core │              │
│ - CookieHandler      │  JSON+Set-  │   POST auth/logout     │  (retry  │  Usuario     │
│ - AuthStateProvider  │  Cookie     │   GET  auth/me         │  3×5s)   │  Institucion │
│ - ThemeService       │             │ UsuarioService         │          │  (m2m memb.) │
│ JWT en localStorage  │             │ TokenService (JWT)     │          └──────────────┘
└──────────────────────┘             │ Identity + EF stores   │
                                     │ CurrentUserMiddleware  │  (AsyncLocal ambient user)
                                     └────────────────────────┘
```

**Auth flow:** login con usuario+contraseña (Identity, lockout, password policy) → `TokenService` emite JWT → el client lo guarda en localStorage, `EnigmaAuthenticationStateProvider` lo decodifica para `<AuthorizeView>`; `CookieHandler` adjunta la cookie a cada request. Tras login, `GET auth/instituciones` alimenta `SeleccionInstitucion`. Requests autenticados llegan con JWT bearer; `CurrentUserMiddleware` siembra `CurrentUserService` (AsyncLocal, cache Lazy por request) — consumible desde cualquier capa sin inyección.

**Server pipeline (orden):** HSTS (no-dev) → SecurityHeaders → HTTPS redirect (prod) → OpenAPI + Scalar `/api/docs` (dev) → `/health` → CORS → RateLimiter → Authentication (JWT forzado sobre defaults de cookie de Identity) → Authorization → `CurrentUserMiddleware` → MapControllers. Kestrel escucha `0.0.0.0:8081`.

---

## Key Directories

|Path|Purpose|Notes|
|---|---|---|
|`Client/`|Blazor WASM frontend|PWA (manifest + service workers), tema claro/oscuro|
|`Client/Pages/Auth/`|`Login.razor`, `SeleccionInstitucion.razor`|Flujo de acceso completo|
|`Client/Layout/`|`AuthLayout` (sin nav), `MainLayout` (navbar sticky)|Design system Jade Institucional|
|`Client/Services/`|`AuthService`, `CookieHandler`, `EnigmaAuthenticationStateProvider`, `ThemeService`|JWT en localStorage; tema en `localStorage` (`enigma_theme`)|
|`Client/Shared/PanelVivo.razor`|Vida ambiental compartida (login/selección/home)|Pausa con pestaña oculta; `prefers-reduced-motion` la apaga|
|`Server/Controllers/Auth/`|`AuthController`|Único controller de dominio hoy|
|`Server/Services/Auth/`|`CurrentUserService`(+`ICurrentUserService`), `CurrentUserMiddleware`, `TokenService`(+`ITokenService`), `UsuarioService`(+`IUsuarioService`), `SecurityHeadersMiddleware`|Interfaz+impl juntas, namespace por dominio|
|`Server/Services/Seed/`|`SeedingService`, `SeedLogin`|Seed dev idempotente: `admin`/`admin123` + 2 instituciones con membresía|
|`Server/Options/`|`JwtOptions`, `JwtSecretResolver`|Secret vía `ENIGMA_JWT_SECRET` (dev genera uno si falta)|
|`Server/Data/Entities/Auth/`|`Usuario`|Hereda `GenericEntity`; `LastLogin`, m2m `Instituciones`|
|`Server/Data/Entities/Administracion/`|`Institucion`|`Nombre`, `Tipo` (`TipoInstitucion`), m2m `Usuarios`|
|`Server/Data/Repositories/`|`GenericRepository<T>`, `Auth/UsuarioRepository`|Base sync; repos de dominio async|
|`Server/Migrations/`|`InitialCreate` (2026-08-10)|Auto-aplicada en Development|
|`Shared/Dtos/AuthDtos.cs`|`LoginRequest`, `LoginResult`, `LoginBody`, `UsuarioDto`, `InstitucionDto`|Contratos client↔server|
|`Tests/Unit/`|Auth, Data, Options, Security, Seed, Architecture|NUnit; architecture tests incluidos|
|`Tests/E2E/`|`E2EWebFixture` + `LoginFlowTest` (6 tests)|WebApplicationFactory + Playwright; **requiere MySQL corriendo**|
|`Tests/`|`stryker-config.json`, `mutation.run.sh`, `coverage.run.sh`|Mutation thresholds 100/90/80 (score actual 94.41)|
|`.github/workflows/`|CI (ver Testing & QA)||
|`scripts/`|`validate.sh`, `validate-yaml.sh`, `validate-commit-msg.sh`, `free-ports.sh`|Usados por lefthook/CI|

---

## Development Commands

```bash
# Build
dotnet build Enigma.slnx                      # lefthook buildea Release con TreatWarningsAsErrors

# Run (dev): MySQL debe estar arriba primero (ver Dev Environment)
dotnet watch --project Server                 # :8081, auto-migra + siembra en Development
dotnet watch --project Client                 # :8080 (devserver)

# Tests
dotnet test                                   # unit siempre; E2E requiere MySQL + browsers Playwright
dotnet test --collect:"XPlat Code Coverage"   # coverlet
bash Tests/mutation.run.sh                    # Stryker (umbrales 100/90/80)
bash Tests/coverage.run.sh

# Migrations (usa versión fija MySQL 8.0.42; no abre TCP en design-time)
dotnet ef migrations add <Name> --project Server
dotnet ef database update --project Server
```

**Dev Environment:** el devcontainer (compose: `.devcontainer/compose.json` + `Dockerfile`) corre `start-dev.sh`, que levanta el contenedor MySQL (`enigma-dev-db`, imagen `ghcr.io/agustin-gigena/enigma-dev-db`) en la red podman `enigma-dev-net` y lanza Server+Client con `dotnet watch`. `MYSQL_*` viene de `.devcontainer/.env` (**no leer/editar archivos `*.env`**). Sin `docker-compose.yml` en la raíz — la BD vive como contenedor aparte, alcanzable por nombre de contenedor. Program.cs arma el connection string desde `MYSQL_HOST/PORT/DATABASE/USER/PASSWORD` con defaults dev `localhost:3306/enigma_db/enigma/enigma_dev_password`; appsettings.json solo documenta los placeholders (IConfiguration NO expande `${VAR}`).

---

## Code Conventions & Patterns

- **net10.0**, nullable + implicit usings en los 4 proyectos. `Enigma.slnx` (solution XML).
- **Naming en español** (`BorradoLogico`, `CreadoPor`, `ObtenerInstitucionesAsync`) — NO "corregir" a inglés.
- **Voseo rioplatense imperativo** en UI ("Ingresá", "Elegí").
- **MySQL provider:** `Microting.EntityFrameworkCore.MySql` (no Pomelo). Server también referencia `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.10, `Identity.EntityFrameworkCore` 10.0.10, `Scalar.AspNetCore`.
- **Soft-delete:** `GenericEntity` base — `Id`, `CreadoEn/CreadoPor`, `ModificadoEn/ModificadoPor`, `BorradoEn/BorradoPor`, `BorradoLogico`, con `SetCreadoPor()`, `SetModificadoPor()`, `SetBorradoLogico()`. El usuario se resuelve vía `CurrentUserService.GetCurrentUser()` (estático, ambiental, sin inyección). Toda entidad nueva hereda y llama los setters antes de `SaveChanges()`. Queries filtran `BorradoLogico` salvo inclusión explícita (`GetById(id, borradoLogico: false)`).
- **Repository:** `GenericRepository<T>` sync (`GetById`, `SetBorradoLogico`); repos de dominio nuevos van async con `CancellationToken` (patrón `UsuarioRepository`).
- **Interfaz + implementación juntas** en el mismo archivo, namespace refleja dominio (`Enigma.Server.Services.Auth`).
- **DTOs** en `Shared` (records); tanto Client como Server referencian `Shared`.
- **Ambient user:** `CurrentUserService.IsAuthenticated()/GetClaimsPrincipal()/GetCurrentUser()` — AsyncLocal sembrado por `CurrentUserMiddleware`, cacheada por request. `ENIGMA_AUTH_REQUIRED=true` → `GetCurrentUser()` lanza `UnauthorizedAccessException` sin usuario; ausente/false → `null`.
- **JWT:** issuer `Enigma`, audience `Enigma.Client`; secret `ENIGMA_JWT_SECRET` (validado al startup vía `JwtOptions.EnsureValid()`). Identity registra cookies como default scheme — el server fuerza los tres defaults a JWT para que `[Authorize]` desafíe con bearer.
- **Errores:** `[ApiController]` (400 automáticos); `InvalidOperationException` cuando falta contexto de usuario; transient MySQL retried por EF.

---

## Important Files

|File|Purpose|
|---|---|
|`Server/Program.cs`|DI completa (Identity, JWT, CORS, RateLimiter, DbContext), pipeline, `public partial class Program` (para `WebApplicationFactory`)|
|`Server/Data/EnigmaDbContext.cs`|`IdentityDbContext<Usuario, IdentityRole<int>, int>`; DbSets `Usuarios`, `Instituciones`|
|`Server/Controllers/Auth/AuthController.cs`|`POST auth/login`, `POST auth/logout`, `GET auth/me`, `GET auth/instituciones`|
|`Server/Services/Auth/CurrentUserService.cs`|Acceso ambiental al usuario actual|
|`Client/Program.cs`|Registra AuthService/ThemeService/CookieHandler/AuthStateProvider; `HttpClient` base desde `wwwroot/appsettings.json` (`ServerUri`)|
|`Tests/E2E/E2EWebFixture.cs`|Fábrica E2E (WebApplicationFactory + Playwright); requiere MySQL|
|`lefthook.yml`|pre-commit: build Release (warnings=errors) + yamllint strict; commit-msg: conventional; pre-push: build|
|`.github/workflows/pr-validation.yml`|PR: job `security` (paquetes vulnerables = fail) + job `validate` (actionlint + build)|
|`.github/workflows/build-{client,server}-image.yml`|Push a `production` → imágenes GHCR `enigma-client`/`enigma-server`|
|`.github/workflows/dev-db-image.yml`|`workflow_dispatch` → imagen dev de MySQL|
|`.github/workflows/apply-rulesets.yml`|Aplica rulesets desde `main-branches.json`|

---

## Testing & QA

- **Unit:** NUnit 4.3.2 + `Mvc.Testing`; cubre Auth (login, tokens, usuario borrado, CurrentUserService), Data (GenericEntity/GenericRepository), Options (JwtOptions), Security (HSTS/headers), Config (password policy), Seed (idempotencia) y **Architecture tests** (reglas de dependencia entre proyectos).
- **E2E:** `LoginFlowTest` — 6 tests Playwright del flujo login/logout/selección/token expirado/ruta protegida. Requieren MySQL disponible y browsers Playwright instalados (`agent-browser install --with-deps` en devcontainer; si fallan con `Executable doesn't exist`, es el browser, no el código).
- **Mutation:** Stryker con umbrales 100/90/80 — los tests nuevos deben ser fuertes; evitar assertions débiles.
- **Quality gates:** pre-commit builda Release con `TreatWarningsAsErrors`; pr-validation falla con paquetes vulnerables (`dotnet list package --vulnerable`).

---

## Notes for AI Assistants

### Protected Configuration and Secrets

- Editing `.editorconfig`, `.gitignore`, `lefthook.yml`, or `.yamllint` requires explicit user confirmation before the edit — including overwriting them via any command or tool.
- Do not modify those protected files as a side effect of another task.
- Do not read, write, edit, copy, move, delete, or inspect `*.env` or `*.env.*` files (incluye `.devcontainer/.env`). Treat them as credential-bearing; the user handles any required changes directly.
- Do not expose secrets from environment files or command output in responses, logs, patches, or diagnostics.

### Domain Rules

1. **Soft-delete + auditoría:** ver convenciones arriba — ninguna entidad escapa del patrón; queries filtran `BorradoLogico` por defecto.
2. **Async para código nuevo:** repos nuevos async (`FindAsync`, `SaveChangesAsync`, `CancellationToken`); `GenericRepository` base queda sync por ahora (refactor pendiente, no hacerlo de rebote).
3. **Multi-institución:** un usuario tiene membresías a varias instituciones (m2m); el flujo login → selección es parte del diseño. El contexto de institución activa aún no tiene persistencia definida — consultar `PRODUCT.md` antes de inventar una.
4. **Seed dev:** `admin`/`admin123` + "Universidad Nacional del Plata" (Universidad) + "Colegio San Martín" (Secundaria), idempotente, solo Development.
5. **Design system:** toda UI nueva sigue `DESIGN.md` (pares claro/oscuro, radio 10px, Manrope solo display, acento jade solo interacción, voseo).
6. **Tests:** E2E nuevo usa el patrón `E2EWebFixture`; correr `dotnet test` local (con MySQL) antes de pushear — lefthook y CI buildan con warnings como errores.
7. **Branches:** `development` es la rama de integración principal; `production` dispara builds de imágenes GHCR. Features salen de `development` (ej. `feat/menusystem`).
