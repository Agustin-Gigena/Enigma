# Endurecimiento de seguridad — Fase 1: hardening de config (Plan de implementación)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cerrar los hallazgos de seguridad independientes del token (secret JWT fail-fast, extracción del servicio de token, HSTS env-gateado, política de contraseña por entorno) sin cambiar el contrato observable del cliente en dev.

**Architecture:** Refactor puro del origen del secret (options pattern + servicio inyectado `ITokenService`), HSTS solo en no-dev, y política de contraseña movida a config con layering base=estricta/prod + override Development=laxa. **En dev no cambia ningún comportamiento observable** (la app sigue caída sobre el mismo fallback y la misma política laxa); el endurecimiento es env-gateado.

**Tech Stack:** .NET 10, ASP.NET Core Identity, EF Core, xUnit, `WebApplicationFactory<Program>`, `Microsoft.Extensions.Options`.

## Global Constraints

- .NET 10 (`net10.0`), nullable habilitado, implicit usings habilitados.
- Naming en **español** para entidades/propiedades (`Contrasena`, `BorradoLogico`); no "corregir" a inglés.
- Todo **DTO** (tipo con `Dto` en el nombre) debe vivir en `Shared/` — lo exige `Tests/Architecture/ArchitectureTests.cs`. Fase 1 **no crea DTOs nuevos**.
- Interfaz + implementación juntas en el mismo archivo (convención del repo: `IXxx`+`Xxx` en `Xxx.cs`).
- Servicios de auth en `Server/Services/Auth/`, namespace `Enigma.Server.Services.Auth`.
- JWT secret: `ENIGMA_JWT_SECRET`; mínimo 32 bytes; **fail-fast en Production** (throw en startup); dev permite el fallback `enigma_dev_jwt_secret_cambiar_en_produccion`.
- Build: `dotnet build Enigma.slnx`. Tests: `dotnet test` (usa el MySQL del devcontainer `enigma-dev-db`, seed `admin/admin123`).
- Commits por tarea, prefijo convencional (`feat`/`refactor`/`fix`/`test`).

**Spec de referencia:** `docs/superpowers/specs/2026-08-13-security-hardening-design.md` (Fase 1).

---

## File Structure

- **Create** `Server/Services/Auth/TokenService.cs` — `ITokenService` + `TokenService`: genera el access JWT desde `IOptions<JwtOptions>` (reemplaza al `GenerarToken` estático del controller).
- **Create** `Tests/Options/JwtOptionsTest.cs` — tests puros del resolver + EnsureValid.
- **Create** `Tests/Auth/TokenServiceTest.cs` — test puro de generación de token.
- **Create** `Tests/Config/PasswordPolicyTest.cs` — test del layering de config de password (dev laxa / prod estricta).
- **Create** `Tests/Security/HstsTest.cs` — test de que el gate excluye dev (sin header `Strict-Transport-Security`).
- **Modify** `Server/Program.cs` — registrar `JwtOptions`/`ITokenService`, `AddJwtBearer` consume la key resuelta, `UseHsts` solo en no-dev, política de contraseña vía `Bind` desde config.
- **Modify** `Server/Controllers/Auth/AuthController.cs` — inyectar `ITokenService`, eliminar el `GenerarToken` estático y la lectura duplicada del secret.
- **Modify** `Server/appsettings.json` — sección `Identity:Password` **estricta** (base = prod).
- **Modify** `Server/appsettings.Development.json` — sección `Identity:Password` **laxa** (override dev).

---

## Status de tasks completadas

### ~~Task 1: `JwtOptions` + `JwtSecretResolver`~~ ✅ COMPLETADA

- Commit: `96812a0` — `feat(security): JwtOptions fail-fast + resolver de secret por entorno`
- Archivos creados: `Server/Options/JwtOptions.cs`, `Server/Options/JwtSecretResolver.cs`, `Tests/Options/JwtOptionsTest.cs`
- **Pero**: el resolver no está conectado a `Program.cs` todavía (se hace en Task 3).

---

### Task 2: `ITokenService` (extraer `GenerarToken` del controller)

**Files:**
- Create: `Server/Services/Auth/TokenService.cs`
- Test: `Tests/Auth/TokenServiceTest.cs`

**Interfaces:**
- Consumes: `JwtOptions` (Task 1), `Usuario` (`Server/Data/Entities/Auth/Usuario.cs`).
- Produces: `ITokenService.GenerarAccessToken(Usuario usuario) -> (string Token, DateTime Expiracion)` con issuer `"Enigma"`, audience `"Enigma.Client"`, TTL **8 h** (sin cambio de contrato respecto al código actual; el TTL de 15 min es Fase 2).

- [ ] **Step 1: Write the failing test**

```csharp
using System.IdentityModel.Tokens.Jwt;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Options;
using Enigma.Server.Services.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace Enigma.Test.Auth;

public class TokenServiceTest
{
  private static TokenService NewSut(string secret) =>
      new(Options.Create(new JwtOptions { Secret = secret }));

  [Fact]
  public void GenerarAccessToken_DevuelveJwtDeTresPartes()
  {
    TokenService sut = NewSut(new string('k', 40));
    Usuario usuario = new() { Id = 7, UserName = "admin" };

    (string token, DateTime expiracion) = sut.GenerarAccessToken(usuario);

    Assert.Equal(3, token.Split('.').Length);
    Assert.True(expiracion > DateTime.UtcNow.AddHours(7));
    Assert.True(expiracion < DateTime.UtcNow.AddHours(9));
  }

  [Fact]
  public void GenerarAccessToken_EmiteIssuerYAudienceCorrectos()
  {
    TokenService sut = NewSut(new string('k', 40));
    Usuario usuario = new() { Id = 1, UserName = "admin" };

    JwtSecurityToken decoded = new JwtSecurityTokenHandler().ReadJwtToken(sut.GenerarAccessToken(usuario).Token);

    Assert.Equal("Enigma", decoded.Issuer);
    Assert.Contains("Enigma.Client", decoded.Audiences);
    Assert.Equal("1", decoded.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
  }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests/Enigma.Test.csproj --filter "FullyQualifiedName~TokenServiceTest"`
Expected: FAIL ("ITokenService does not exist").

- [ ] **Step 3: Write minimal implementation**

`Server/Services/Auth/TokenService.cs` (interfaz + impl juntas, convención repo):
```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Enigma.Server.Data.Entities.Auth;
using Enigma.Server.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Enigma.Server.Services.Auth;

public interface ITokenService
{
  /// <summary>Genera el access JWT (issuer Enigma, audience Enigma.Client, TTL 8 h).</summary>
  (string Token, DateTime Expiracion) GenerarAccessToken(Usuario usuario);
}

public sealed class TokenService : ITokenService
{
  private readonly JwtOptions _jwt;

  public TokenService(IOptions<JwtOptions> jwt) => _jwt = jwt.Value;

  public (string Token, DateTime Expiracion) GenerarAccessToken(Usuario usuario)
  {
    SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_jwt.Secret));
    SigningCredentials credenciales = new(key, SecurityAlgorithms.HmacSha256);
    DateTime expiracion = DateTime.UtcNow.AddHours(8);

    List<Claim> claims = new()
    {
      new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
      new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
      new(ClaimTypes.Name, usuario.UserName ?? ""),
      new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
    };

    JwtSecurityToken token = new(
        issuer: "Enigma",
        audience: "Enigma.Client",
        claims: claims,
        expires: expiracion,
        signingCredentials: credenciales);

    return (new JwtSecurityTokenHandler().WriteToken(token), expiracion);
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Tests/Enigma.Test.csproj --filter "FullyQualifiedName~TokenServiceTest"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Server/Services/Auth/TokenService.cs Tests/Auth/TokenServiceTest.cs
git commit -m "feat(security): extraer ITokenService del AuthController"
```

---

### Task 3: Wire `Program.cs` + `AuthController` (fuente única del secret)

**Files:**
- Modify: `Server/Program.cs` (secret resolver + registro de `JwtOptions`/`ITokenService`; `AddJwtBearer` usa la key resuelta).
- Modify: `Server/Controllers/Auth/AuthController.cs` (inyecta `ITokenService`, elimina `GenerarToken` estático).

**Interfaces:**
- Consumes: `JwtOptions`, `JwtSecretResolver` (Task 1), `ITokenService` (Task 2).
- Produces: `POST /auth/login` con el mismo contrato que hoy (access JWT en body). La regresión la cubre `Tests/Auth/LoginTest.cs` (debe seguir pasando sin cambios).

- [ ] **Step 1: Edits en `Server/Program.cs`**

Reemplazar las líneas 78-79 que leen el secret duplicado. El bloque completo a reemplazar (líneas 78-102):

**ANTES** (`Program.cs:78-102`):
```csharp
string jwtSecret = Environment.GetEnvironmentVariable("ENIGMA_JWT_SECRET")
    ?? "enigma_dev_jwt_secret_cambiar_en_produccion";
// ...
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
```

**DESPUÉS**:
```csharp
using Enigma.Server.Options;
string jwtSecret = JwtSecretResolver.Resolve(
    Environment.GetEnvironmentVariable("ENIGMA_JWT_SECRET"),
    builder.Environment.IsDevelopment());
JwtOptions jwtOptions = new() { Secret = jwtSecret };
jwtOptions.EnsureValid();
builder.Services.AddSingleton(Options.Create(jwtOptions));
builder.Services.AddSingleton<ITokenService, TokenService>();

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
        };
    });
```

También agregar los `using` que falten al top del archivo:
```csharp
using Enigma.Server.Options;
using Microsoft.Extensions.Options;
```

- [ ] **Step 2: Edits en `Server/Controllers/Auth/AuthController.cs`**

Agregar campo + inyección:
```csharp
private readonly IUsuarioService _usuarioService;
private readonly ITokenService _tokenService;

public AuthController(IUsuarioService usuarioService, ITokenService tokenService)
{
  _usuarioService = usuarioService;
  _tokenService = tokenService;
}
```

En `Login` (línea 38), reemplazar `GenerarToken(resultado.Usuario)` por `_tokenService.GenerarAccessToken(resultado.Usuario)`:
```csharp
(string token, DateTime expiracion) = _tokenService.GenerarAccessToken(resultado.Usuario);
```

**Eliminar** el método estático `private static (string Token, DateTime Expiracion) GenerarToken(Usuario usuario)` íntegramente (líneas 78-102) y los `using` que quedan sin uso (`System.IdentityModel.Tokens.Jwt`, `System.Security.Claims`, `System.Text`, `Microsoft.IdentityModel.Tokens`).

- [ ] **Step 3: Run regression — el E2E existente debe seguir pasando**

Run: `dotnet test Tests/Enigma.Test.csproj --filter "FullyQualifiedName~LoginTest"`
Expected: PASS (`Login_Admin_RetornaTokenYDosInstituciones`, `Login_CredencialesInvalidas_Retorna401`, `Instituciones_ConToken_RetornaLasMismasDos`). **NOTA**: requiere MySQL (enigma-dev-db). Si no hay DB disponible, al menos verificar que compila con `dotnet build Enigma.slnx`.

- [ ] **Step 4: Build completo**

Run: `dotnet build Enigma.slnx`
Expected: PASS, sin warnings nuevos.

- [ ] **Step 5: Commit**

```bash
git add Server/Program.cs Server/Controllers/Auth/AuthController.cs
git commit -m "refactor(security): fuente única del secret JWT vía ITokenService + JwtOptions"
```

---

### Task 4: HSTS env-gateado

**Files:**
- Modify: `Server/Program.cs`
- Test: `Tests/Security/HstsTest.cs`

**Interfaces:**
- Produces: en **Development** el response **no** lleva header `Strict-Transport-Security`; en no-dev sí (vía `UseHsts`).

- [ ] **Step 1: Write the failing test**

```csharp
using Enigma.Test.Auth; // EnigmaWebFactory (Development)
using Xunit;

namespace Enigma.Test.Security;

public class HstsTest : IClassFixture<EnigmaWebFactory>
{
  private readonly HttpClient _client;
  public HstsTest(EnigmaWebFactory factory) => _client = factory.CreateClient();

  [Fact]
  public async Task EnDesarrollo_NoSeEnviaHeaderHsts()
  {
    // Cualquier endpoint sirve; usamos /auth/me (401 sin token, pero los headers de pipeline igual).
    HttpResponseMessage response = await _client.GetAsync("/auth/me");

    Assert.False(response.Headers.Contains("Strict-Transport-Security"),
        "En Development NO debe aplicarse HSTS (UseHsts lanza si se llama en dev).");
  }
}
```

- [ ] **Step 2: Run test to verify it fails (o pasa trivialmente — verificación de la aserción)**

Run: `dotnet test Tests/Enigma.Test.csproj --filter "FullyQualifiedName~HstsTest"`
Expected: PASS ya (hoy no hay HSTS). Este test es de **regresión**: clava que, al agregar el gate en el Step 3, Development siga sin el header.

- [ ] **Step 3: Write minimal implementation**

En `Server/Program.cs`, antes de `app.UseHttpsRedirection();` (línea 156):
```csharp
if (!app.Environment.IsDevelopment())
{
  app.UseHsts();
}

app.UseHttpsRedirection();
```

- [ ] **Step 4: Run test to verify it still passes**

Run: `dotnet test Tests/Enigma.Test.csproj --filter "FullyQualifiedName~HstsTest"`
Expected: PASS (Development excluido del gate).

- [ ] **Step 5: Commit**

```bash
git add Server/Program.cs Tests/Security/HstsTest.cs
git commit -m "feat(security): HSTS solo en no-dev (env-gate)"
```

---

### Task 5: Política de contraseña por entorno (config layering)

**Files:**
- Modify: `Server/appsettings.json` (base = **estricta**, sirve de prod-safe).
- Modify: `Server/appsettings.Development.json` (override = **laxa**).
- Modify: `Server/Program.cs` (`AddIdentity` lee `Identity:Password` desde config).
- Test: `Tests/Config/PasswordPolicyTest.cs`

**Interfaces:**
- Produces: `builder.Configuration.GetSection("Identity:Password")` define la política efectiva. Dev = laxa (override), prod = estricta (base).

- [ ] **Step 1: Write the failing test**

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Enigma.Test.Config;

public class PasswordPolicyTest
{
  // Localiza Server/ desde la raíz del repo (mismo patrón que ArchitectureTests),
  // porque los tests corren desde su propio bin, no desde el directorio de Server.
  private static string ServerDir
  {
    get
    {
      DirectoryInfo? current = new(AppContext.BaseDirectory);
      while (current != null)
      {
        if (File.Exists(Path.Combine(current.FullName, "Enigma.slnx")))
        {
          return Path.Combine(current.FullName, "Server");
        }
        current = current.Parent;
      }
      throw new InvalidOperationException("No se encontró la raíz del repo (Enigma.slnx).");
    }
  }

  private static PasswordOptions Bind(bool addDevelopment)
  {
    IConfigurationBuilder cfg = new ConfigurationBuilder()
        .SetBasePath(ServerDir)
        .AddJsonFile("appsettings.json", optional: false);
    if (addDevelopment)
    {
      cfg.AddJsonFile("appsettings.Development.json", optional: false);
    }
    PasswordOptions opts = new();
    cfg.Build().GetSection("Identity:Password").Bind(opts);
    return opts;
  }

  [Fact]
  public void SoloBase_PoliticaEstrictaComoProd()
  {
    PasswordOptions opts = Bind(addDevelopment: false);
    Assert.Equal(8, opts.RequiredLength);
    Assert.True(opts.RequireDigit);
    Assert.True(opts.RequireUppercase);
    Assert.True(opts.RequireLowercase);
    Assert.True(opts.RequireNonAlphanumeric);
  }

  [Fact]
  public void ConOverrideDevelopment_PoliticaLaxaComoDev()
  {
    PasswordOptions opts = Bind(addDevelopment: true);
    Assert.Equal(6, opts.RequiredLength);
    Assert.False(opts.RequireDigit);
    Assert.False(opts.RequireUppercase);
    Assert.False(opts.RequireLowercase);
    Assert.False(opts.RequireNonAlphanumeric);
  }
}
```
Nota: los dos tests fijan el **contrato de las dos capas** — base (prod-safe, estricta) y override Development (dev, laxa). Reflejan el layering real de ASP.NET Core (`appsettings.json` base + `appsettings.Development.json` cuando el entorno es Development).

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests/Enigma.Test.csproj --filter "FullyQualifiedName~PasswordPolicyTest"`
Expected: FAIL en los dos — la sección `Identity:Password` aún no existe, así que `Bind` devuelve los defaults de `PasswordOptions` (`RequiredLength` 6, `Require*` todos `true`), que no matchean el contrato del test.

- [ ] **Step 3: Write minimal implementation**

`Server/appsettings.json` — agregar la sección (base = prod-safe, estricta):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=${MYSQL_HOST};Port=${MYSQL_PORT};Database=${MYSQL_DATABASE};User Id=${MYSQL_USER};Password=${MYSQL_PASSWORD};"
  },
  "Identity": {
    "Password": {
      "RequiredLength": 8,
      "RequireDigit": true,
      "RequireUppercase": true,
      "RequireLowercase": true,
      "RequireNonAlphanumeric": true
    }
  }
}
```

`Server/appsettings.Development.json` — override laxa:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Identity": {
    "Password": {
      "RequiredLength": 6,
      "RequireDigit": false,
      "RequireUppercase": false,
      "RequireLowercase": false,
      "RequireNonAlphanumeric": false
    }
  }
}
```

`Server/Program.cs` — reemplazar el bloque hardcodeado de `AddIdentity` (líneas 61-74):
```csharp
// ANTES (Program.cs ~61-72): options.Password.RequiredLength = 6; ... = false; etc.

// DESPUÉS:
builder.Services.AddIdentity<Usuario, IdentityRole<int>>(options =>
    {
      builder.Configuration.GetSection("Identity:Password").Bind(options.Password);
      options.Lockout.MaxFailedAccessAttempts = 5;
      options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
      options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<EnigmaDbContext>()
    .AddDefaultTokenProviders();
```

- [ ] **Step 4: Run test to verify it passes + regresión de dev**

Run: `dotnet test Tests/Enigma.Test.csproj --filter "FullyQualifiedName~PasswordPolicyTest|FullyQualifiedName~LoginTest"`
Expected: PASS. `LoginTest` (que entra con `admin/admin123` en **Development**) debe seguir pasando — prueba que el override laxo conserva la política de dev.

- [ ] **Step 5: Commit**

```bash
git add Server/appsettings.json Server/appsettings.Development.json Server/Program.cs Tests/Config/PasswordPolicyTest.cs
git commit -m "feat(security): política de contraseña por entorno (config layering base=estricta)"
```

---

## Verificación final de la Fase 1

- [ ] **Build completo + suite entera**

Run: `dotnet build Enigma.slnx && dotnet test`
Expected: PASS. En particular `Tests/Auth/LoginTest` (regresión de contrato) y `Tests/Architecture/ArchitectureTests` (no se crearon DTOs fuera de Shared).

- [ ] **Checkeo manual del fail-fast en prod (sin DB)**

```bash
# Simula Production sin ENIGMA_JWT_SECRET → la app debe rehusar a arrancar.
ASPNETCORE_ENVIRONMENT=Production ENIGMA_JWT_SECRET="" \
  dotnet run --project Server/Enigma.Server.csproj --no-build
# Esperado: InvalidOperationException del resolver/EnsureValid y proceso termina.
```

- [ ] **Commit de cierre (opcional, si quedó algo sin commitear)**

```bash
git status   # working tree limpio
```

## Fuera de esta Fase

- Rewrite del token (access en memoria + refresh en cookie HttpOnly + rotación + revocación + CORS con credentials + CSRF + reescritura del cliente Blazor) → **Fase 2**, spec/plan separado.
