# Repository Guidelines

**Enigma** — Full-stack .NET 10 monorepo: hosts Web (Blazor WebAssembly) y MAUI (Blazor Hybrid, Android/Windows), ASP.NET Core Web API, MySQL 8.0 backend. Licensed under the Functional Source License 1.1 (FSL-1.1-ALv2): source-available, Competing Use prohibited, converts to Apache 2.0 after 2 years per version.

**Product & design canon:** `PRODUCT.md` (product vision, scope, open decisions) and `DESIGN.md` (design system "Jade Institucional" — colors, typography, layout, motion). Consult both before building UI or scoping features. Historical specs/plans live in `docs/superpowers/specs/` and `docs/superpowers/plans/`.

---

## Project Overview

Single Git monorepo (`src/` + `tests/`) con seis proyectos: **Api** (ASP.NET Core Web API, `src/Enigma.Api`), **Web** (host Blazor WASM, `src/Enigma.Web`), **Maui** (host Blazor Hybrid Android/Windows, `src/Enigma.Maui`), **SharedUI** (RCL con páginas, layout, servicios y assets, `src/Enigma.SharedUI`), **Shared** (DTOs/contracts, `src/Enigma.Shared`), **Tests** (NUnit unit + Playwright E2E, `tests/Enigma.Tests`). Auth is implemented end-to-end: ASP.NET Core Identity + JWT bearer, login → selección de institución → home. Domain folders (Auth, Administracion, Seed) are replicated across Api's Controllers/Services/Data.

---

## Architecture & Data Flow

```
Web (:8080, WASM) / Maui (Android/Windows)         Api (:8081)                        MySQL 8.0
┌────────────────────────────────┐  HTTP+JWT   ┌────────────────────────┐          ┌──────────────┐
│ SharedUI.App (RCL montada)     │────────────▶│ AuthController         │─────────▶│ enigma_db    │
│ - AuthService                  │◀────────────│   POST auth/login      │  EF Core │              │
│ - CookieHandler                │  JSON+Set-  │   POST auth/logout     │  (retry  │  Usuario     │
│ - AuthStateProvider            │  Cookie     │   GET  auth/me         │  3×5s)   │  Institucion │
│ - ThemeService                 │             │ UsuarioService         │          │  (m2m memb.) │
│ JWT en localStorage            │             │ TokenService (JWT)     │          └──────────────┘
└────────────────────────────────┘             │ Identity + EF stores   │
                                               │ CurrentUserMiddleware  │  (AsyncLocal ambient user)
                                               └────────────────────────┘
```

**Hosts:** `Enigma.Web` (WASM puro) y `Enigma.Maui` (Blazor Hybrid) montan la misma app: `builder.RootComponents.Add<Enigma.SharedUI.App>("#app")` + `builder.Services.AddEnigmaSharedUI()`. Toda página, layout, servicio y asset vive en `Enigma.SharedUI`; los hosts solo eligen runtime (browser vs WebView nativo). La `HttpClient` del host toma su base de `wwwroot/appsettings.json` (`ServerUri`; en Maui, MauiAsset). **Regla de dependencias: nadie referencia `Enigma.Api` salvo los tests.**

**Auth flow:** login con usuario+contraseña (Identity, lockout, password policy) → `TokenService` emite JWT → el host lo guarda en localStorage, `EnigmaAuthenticationStateProvider` lo decodifica para `<AuthorizeView>`; `CookieHandler` adjunta la cookie a cada request. Tras login, `GET auth/instituciones` alimenta `SeleccionInstitucion`. Requests autenticados llegan con JWT bearer; `CurrentUserMiddleware` siembra `CurrentUserService` (AsyncLocal, cache Lazy por request) — consumible desde cualquier capa sin inyección.

**Api pipeline (orden):** ForwardedHeaders (primero, detrás de proxy) → HSTS (no-dev) → SecurityHeaders → HTTPS redirect (prod) → OpenAPI + Scalar `/api/docs` (dev) → `/health` → CORS → RateLimiter → Authentication (JWT forzado sobre defaults de cookie de Identity) → Authorization → `CurrentUserMiddleware` → MapControllers. Kestrel escucha `0.0.0.0:8081`.

---

## Key Directories

|Path|Purpose|Notes|
|---|---|---|
|`src/Enigma.Api/`|ASP.NET Core Web API + EF Core|Identity + JWT bearer, migraciones, `Dockerfile` (imagen GHCR)|
|`src/Enigma.Api/Controllers/Auth/`|`AuthController`|Único controller de dominio hoy|
|`src/Enigma.Api/Services/Auth/`|`CurrentUserService`(+`ICurrentUserService`), `CurrentUserMiddleware`, `TokenService`(+`ITokenService`), `UsuarioService`(+`IUsuarioService`), `SecurityHeadersMiddleware`|Interfaz+impl juntas, namespace por dominio (`Enigma.Api.Services.Auth`)|
|`src/Enigma.Api/Services/Seed/`|`SeedingService`, `SeedLogin`|Seed dev idempotente: `admin`/`admin123` + 2 instituciones con membresía|
|`src/Enigma.Api/Options/`|`JwtOptions`, `JwtSecretResolver`|Secret vía `ENIGMA_JWT_SECRET` (dev genera uno si falta)|
|`src/Enigma.Api/Data/Entities/`|`Usuario` (Auth), `Institucion` (Administracion)|Heredan `GenericEntity`; m2m usuarios↔instituciones|
|`src/Enigma.Api/Data/Repositories/`|`GenericRepository<T>`, `Auth/UsuarioRepository`|Base sync; repos de dominio async|
|`src/Enigma.Api/Migrations/`|`InitialCreate` (2026-08-10)|Auto-aplicada en Development|
|`src/Enigma.SharedUI/`|RCL: `App.razor`, Pages, Layout, Services, `wwwroot/`|Montada por Web (`RootComponents.Add`) y Maui; tema claro/oscuro|
|`src/Enigma.SharedUI/Pages/Auth/`|`Login.razor`, `SeleccionInstitucion.razor`|Flujo de acceso completo|
|`src/Enigma.SharedUI/Layout/`|`AuthLayout` (sin nav), `MainLayout` (navbar sticky)|Design system Jade Institucional|
|`src/Enigma.SharedUI/Services/`|`AuthService`, `CookieHandler`, `EnigmaAuthenticationStateProvider`, `ThemeService`|JWT en localStorage; tema en `localStorage` (`enigma_theme`)|
|`src/Enigma.SharedUI/Shared/PanelVivo.razor`|Vida ambiental compartida (login/selección/home)|Pausa con pestaña oculta; `prefers-reduced-motion` la apaga|
|`src/Enigma.Web/`|Host Blazor WASM|PWA host mínimo; `ServerUri` en `wwwroot/appsettings.json`|
|`src/Enigma.Maui/`|Host Blazor Hybrid Android/Windows|`MauiProgram` registra `AddEnigmaSharedUI()`; `appsettings.json` como MauiAsset|
|`src/Enigma.Shared/Dtos/AuthDtos.cs`|`LoginRequest`, `LoginResult`, `LoginBody`, `UsuarioDto`, `InstitucionDto`|Contratos hosts↔api|
|`tests/Enigma.Tests/`|Unit (`Unit/`) + E2E (`E2E/`) en un solo proyecto|NUnit + Playwright; `stryker-config.json`, `mutation.run.sh`, `coverage.run.sh`|
|`deploy/`|`docker-compose.yml` + `Caddyfile`|Deploy manual de la API en VPS (Docker + Caddy)|
|`.github/workflows/`|CI (ver Testing & QA)|`pr-validation`, `deploy`, `deploy-api`, `deploy-web`, `deploy-maui`, `build-server-image`, `dev-db-image`, `apply-rulesets`|
|`.github/actions/`|Composite actions|`setup-dotnet`, `deploy-api`, `deploy-web`, `deploy-maui`, `sync-db`, `test`|
|`scripts/`|`validate.sh`, `validate-yaml.sh`, `validate-commit-msg.sh`, `free-ports.sh`|Usados por lefthook/CI|

---

## Development Commands

```bash
# Build
dotnet build Enigma.slnx                      # lefthook buildea Release con TreatWarningsAsErrors
dotnet build Enigma.Core.slnf                 # lo que valida pr-validation: sin Maui (sin workloads Android/Windows)

# Run (dev): MySQL debe estar arriba primero (ver Dev Environment)
dotnet watch --project src/Enigma.Api         # :8081, auto-migra + siembra en Development
dotnet watch --project src/Enigma.Web         # :8080 (devserver WASM)

# Tests (un solo proyecto: tests/Enigma.Tests)
dotnet test                                   # unit siempre; E2E requiere MySQL + browsers Playwright
dotnet test --collect:"XPlat Code Coverage"   # coverlet
bash tests/Enigma.Tests/mutation.run.sh       # Stryker (umbrales 100/90/80)
bash tests/Enigma.Tests/coverage.run.sh

# Migrations (usa versión fija MySQL 8.0.42; no abre TCP en design-time)
dotnet ef migrations add <Name> --project src/Enigma.Api
dotnet ef database update --project src/Enigma.Api
```

**Dev Environment:** el devcontainer (compose: `.devcontainer/compose.json` + `Dockerfile`) corre `start-dev.sh`, que levanta el contenedor MySQL (`enigma-dev-db`, imagen `ghcr.io/agustin-gigena/enigma-dev-db`) en la red podman `enigma-dev-net` y lanza Api+Web con `dotnet watch --project src/Enigma.Api` / `src/Enigma.Web`. `MYSQL_*` viene de `.devcontainer/.env` (**no leer/editar archivos `*.env`**). Sin `docker-compose.yml` en la raíz — la BD vive como contenedor aparte, alcanzable por nombre de contenedor. `Program.cs` de la Api arma el connection string desde `MYSQL_HOST/PORT/DATABASE/USER/PASSWORD` con defaults dev `localhost:3306/enigma_db/enigma/enigma_dev_password`; appsettings.json solo documenta los placeholders (IConfiguration NO expande `${VAR}`).

---

## Code Conventions & Patterns

- **net10.0**, nullable + implicit usings en todos los proyectos. `Enigma.slnx` (solution XML); `Enigma.Core.slnf` excluye Maui. Central Package Management: versiones solo en `Directory.Packages.props`.
- **Naming en español** (`BorradoLogico`, `CreadoPor`, `ObtenerInstitucionesAsync`) — NO "corregir" a inglés.
- **Voseo rioplatense imperativo** en UI ("Ingresá", "Elegí").
- **MySQL provider:** `Microting.EntityFrameworkCore.MySql` (no Pomelo). Api también referencia `Microsoft.AspNetCore.Authentication.JwtBearer`, `Identity.EntityFrameworkCore`, `Scalar.AspNetCore`.
- **Soft-delete:** `GenericEntity` base — `Id`, `CreadoEn/CreadoPor`, `ModificadoEn/ModificadoPor`, `BorradoEn/BorradoPor`, `BorradoLogico`, con `SetCreadoPor()`, `SetModificadoPor()`, `SetBorradoLogico()`. El usuario se resuelve vía `CurrentUserService.GetCurrentUser()` (estático, ambiental, sin inyección). Toda entidad nueva hereda y llama los setters antes de `SaveChanges()`. Queries filtran `BorradoLogico` salvo inclusión explícita (`GetById(id, borradoLogico: false)`).
- **Repository:** `GenericRepository<T>` sync (`GetById`, `SetBorradoLogico`); repos de dominio nuevos van async con `CancellationToken` (patrón `UsuarioRepository`).
- **Interfaz + implementación juntas** en el mismo archivo, namespace refleja dominio (`Enigma.Api.Services.Auth`).
- **DTOs** en `Shared` (records); Api, Web, Maui y SharedUI referencian `Shared`. UI (páginas, servicios de cliente, assets) vive en `SharedUI`; Web y Maui solo hospedan.
- **Ambient user:** `CurrentUserService.IsAuthenticated()/GetClaimsPrincipal()/GetCurrentUser()` — AsyncLocal sembrado por `CurrentUserMiddleware`, cacheada por request. `ENIGMA_AUTH_REQUIRED=true` → `GetCurrentUser()` lanza `UnauthorizedAccessException` sin usuario; ausente/false → `null`.
- **JWT:** issuer `Enigma`, audience `Enigma.Client` (literal intacto por compatibilidad de tokens); secret `ENIGMA_JWT_SECRET` (validado al startup vía `JwtOptions.EnsureValid()`). Identity registra cookies como default scheme — la Api fuerza los tres defaults a JWT para que `[Authorize]` desafíe con bearer.
- **Errores:** `[ApiController]` (400 automáticos); `InvalidOperationException` cuando falta contexto de usuario; transient MySQL retried por EF.

---

## Important Files

|File|Purpose|
|---|---|
|`src/Enigma.Api/Program.cs`|DI completa (Identity, JWT, CORS, RateLimiter, DbContext), pipeline, `public partial class Program` (para `WebApplicationFactory`)|
|`src/Enigma.Api/Data/EnigmaDbContext.cs`|`IdentityDbContext<Usuario, IdentityRole<int>, int>`; DbSets `Usuarios`, `Instituciones`|
|`src/Enigma.Api/Controllers/Auth/AuthController.cs`|`POST auth/login`, `POST auth/logout`, `GET auth/me`, `GET auth/instituciones`|
|`src/Enigma.Api/Services/Auth/CurrentUserService.cs`|Acceso ambiental al usuario actual|
|`src/Enigma.SharedUI/EnigmaUIServiceCollectionExtensions.cs`|`AddEnigmaSharedUI()`: registra servicios de UI; consumida por Web y Maui|
|`src/Enigma.Web/Program.cs` + `src/Enigma.Maui/MauiProgram.cs`|Hosts: montan `SharedUI.App` y registran `AddEnigmaSharedUI()`; `HttpClient` base desde `wwwroot/appsettings.json` (`ServerUri`; Maui: MauiAsset)|
|`tests/Enigma.Tests/E2E/E2EWebFixture.cs`|Fixture E2E: levanta Api (:8081) y Web (:8080) con `dotnet run`, espera `/health` y `/`; requiere MySQL y browsers Playwright|
|`Directory.Packages.props`|Central Package Management: versiones de todos los paquetes|
|`deploy/docker-compose.yml` + `deploy/Caddyfile`|Stack de producción de la API en VPS|
|`lefthook.yml`|pre-commit: build Release (warnings=errors) + yamllint strict; commit-msg: conventional; pre-push: build|
|`.github/workflows/pr-validation.yml`|PR: job `security` (paquetes vulnerables = fail) + job `validate` (actionlint + `dotnet build Enigma.Core.slnf`) + job `test` (suite completa con service MySQL para E2E) + job `format` (aplica formato y commit si hay cambios)|
|`.github/workflows/deploy.yml`|`workflow_dispatch` con toggles `run_api`/`run_web`/`run_maui` → orquesta los 4 jobs (api/web/maui-android/maui-windows) via composite actions|
|`.github/workflows/deploy-api.yml`|`workflow_dispatch` → imagen GHCR `enigma-server` (`latest` solo se pisa desde `production`)|
|`.github/workflows/deploy-web.yml`|`workflow_dispatch` → `dotnet publish src/Enigma.Web` → Cloudflare Pages|
|`.github/workflows/deploy-maui.yml`|`workflow_dispatch` → APK Android + instalador Windows (Inno Setup) como artifacts|
|`.github/workflows/build-server-image.yml`|Push a `production` → imagen GHCR `enigma-server` desde `src/Enigma.Api/Dockerfile` (automation; convive con Deploy API manual)|
|`.github/actions/{setup-dotnet,deploy-api,deploy-web,deploy-maui,sync-db,test}`|Composite actions reutilizables (CI y deploy; los secrets pasan como env desde el workflow)|
|`.github/workflows/dev-db-image.yml`|`workflow_dispatch` → imagen dev de MySQL|
|`.github/workflows/apply-rulesets.yml`|Aplica rulesets desde `main-branches.json`|

---

## Testing & QA

- **Un solo proyecto:** `tests/Enigma.Tests` — Unit + E2E juntos (53 unit + 6 E2E).
- **Unit:** NUnit + `Mvc.Testing`; cubre Auth (login, tokens, usuario borrado, CurrentUserService), Data (GenericEntity/GenericRepository), Options (JwtOptions), Security (HSTS/headers), Config (password policy), Seed (idempotencia) y **Architecture tests** (reglas de dependencia: DTOs solo en `Enigma.Shared`).
- **E2E:** `LoginFlowTest` — 6 tests Playwright del flujo login/logout/selección/token expirado/ruta protegida. `E2EWebFixture` levanta Api (:8081) y Web (:8080) con `dotnet run --no-build -c <config>` y espera `/health`. Requieren MySQL disponible (`MYSQL_*` en el entorno) y browsers Playwright instalados (`pwsh tests/Enigma.Tests/bin/<config>/net10.0/playwright.ps1 install`; si fallan con `Executable doesn't exist`, es el browser, no el código).
- **Mutation:** Stryker con umbrales 100/90/80 (`tests/Enigma.Tests/stryker-config.json`) — los tests nuevos deben ser fuertes; evitar assertions débiles.
- **Quality gates:** pre-commit builda Release con `TreatWarningsAsErrors`; pr-validation falla con paquetes vulnerables (`dotnet list Enigma.slnx package --vulnerable`) y valida `dotnet build Enigma.Core.slnf`.

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
6. **Tests:** E2E nuevo usa el patrón `E2EWebFixture`; correr `dotnet test` local (con MySQL arriba y `MYSQL_*` en el entorno) antes de pushear — lefthook y CI buildan con warnings como errores.
7. **Branches & deploy:** `development` es la rama de integración principal; `production` dispara la imagen GHCR de la API. Targets de deploy: **API** Docker + Caddy en VPS (manual, `deploy/`; refresco sin SSH = workflow `Deploy API`); **Web** Cloudflare Pages, estático (`deploy-web.yml`, `workflow_dispatch`); **MAUI** APK Android + instalador Windows (Inno Setup) como artifacts de `deploy-maui.yml` (`workflow_dispatch`). Los tres dominios también se despliegan juntos con toggles desde `deploy.yml`. Features salen de `development` (ej. `feat/menusystem`).
8. **Dependencias de proyecto:** nadie referencia `Enigma.Api` salvo los tests; `Enigma.SharedUI` es la única dueña de UI/assets — los hosts (Web, Maui) no agregan páginas ni estilos propios; versiones de NuGet solo en `Directory.Packages.props`.
