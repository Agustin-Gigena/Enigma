# Spec: Endurecimiento de seguridad del flujo de autenticación

Fecha: 2026-08-13
Estado: Borrador (pendiente de revisión)
Rama: `fix/security-hardening` (basada en `development` / `c24eb96`)

Cierra 7 hallazgos de seguridad del flujo de auth, estructurado como
**config-flip para producción** (seguro en dev ahora, endurecido para prod con
un cambio de variables de entorno).

---

## Contexto y problema

El flujo de autenticación actual rompe varias convenciones de seguridad. Tras
revisarlo de punta a punta (cliente Blazor + servidor ASP.NET Core + Identity +
EF Core), los hallazgos reales —distintos del diagnóstico inicial "la
contraseña se ve en el inspector", que **no** es una vulnerabilidad— son:

| # | Hallazgo | Dónde | Severidad |
|---|----------|-------|-----------|
| 1 | Secret JWT con **fallback hardcodeado silencioso** si `ENIGMA_JWT_SECRET` no está → cualquiera puede forjar tokens | `Server/Program.cs:78-79`, `Server/Controllers/Auth/AuthController.cs:80-81` (duplicado) | Crítica |
| 2 | **Sin HTTPS obligatorio / sin HSTS**: `UseHttpsRedirection` permite un primer request HTTP en claro (expone la contraseña) y luego redirige | `Server/Program.cs:156` | Alta |
| 3 | **CORS totalmente abierto** (`AllowAnyOrigin/Method/Header`) en todos los entornos | `Server/Program.cs:30-32` | Media |
| 4 | **JWT en `localStorage`** → cualquier XSS roba el token de sesión; sin CSP que lo acote | `Client/Services/AuthService.cs:48`, `Client/Services/EnigmaAuthenticationStateProvider.cs:23` | Media |
| 5 | **Sin refresh ni revocación**: token de 8 h, logout solo limpia el cliente (el servidor no puede invalidar) | `Server/Controllers/Auth/AuthController.cs:84` | Media |
| 6 | **Política de contraseña de dev corre en prod** (`length 6`, todo en `false`) + seed `admin/admin123` | `Server/Program.cs:64-71`, `:175-176` | Media |
| 7 | **Secrets/credenciales con fallbacks hardcodeados** commiteados en el código | `Server/Program.cs:42-43,79,176` | Baja-Media |

### Por qué NO se hashea la contraseña en el cliente

El diagnóstico original ("la contraseña viaja sin hashear, se ve en el
inspector") parte de un malentendido que conviene dejar explícito, porque
implementarlo literal **introduciría** una vulnerabilidad:

1. **DevTools muestra el cuerpo desencriptado.** El panel Network muestra el
   request *después* de que TLS lo desencripta — es una vista local de
   depuración; solo quien está en la máquina lo ve. Todo login del internet
   (GitHub, bancos) muestra ahí la contraseña en claro. No es el modelo de
   amenaza.
2. **La amenaza en tránsito se resuelve con HTTPS, no con hasheo en cliente.**
   Sobre TLS la contraseña va cifrada; un atacante de red (MITM) no la lee. El
   arreglo para "contraseña expuesta en la red" es **forzar HTTPS + HSTS**
   (hallazgo #2), no hashear en el browser.
3. **Hashear en el cliente es contraproducente.** El hash *se vuelve* la
   credencial: quien lo intercepta lo repite directamente (pass-the-hash). Se
   cambia un secreto que solo vive en la cabeza del usuario por uno que viaja
   por la red. Es estrictamente peor.

La arquitectura correcta **ya está**: contraseña en claro → TLS → servidor →
**Identity la hashea** (PBKDF2-HMAC-SHA256, 256k iteraciones, salt por usuario)
→ compara con el hash almacenado. El almacenamiento es correcto; el tránsito
solo necesita HTTPS. Esa parte **no se toca**.

---

## Decisiones (tomadas en la sesión de brainstorming)

- **Objetivo de despliegue:** dev ahora, prod pronto → todo env-gateado,
  fail-fast en prod, config-flip para endurecer.
- **Estrategia de token:** access token JWT de corta vida en **memoria** del
  Blazor + refresh token de larga vida en **cookie HttpOnly** con **rotación +
  detección de reúso + revocación** en DB.
- **Enfoque:** un solo spec con **dos fases internas** (config primero, rewrite
  del token después) en la misma rama. La cookie de refresh obliga a tocar
  CORS-con-credentials + CSRF + origen fijado, así que ese trabajo es
  inseparable del rewrite del token — hacerlo en un solo spec evita rehacerlo.
- **La fase de config pura va primero** y es de bajo riesgo.

---

## Fase 1 — Hardening de config (independiente del token)

Aterriza y se prueba primero, **sin tocar el cliente**.

### Cambios exactos

**1. Secret JWT fail-fast + fuente única.** Centralizar el secret en el
**options pattern**: `JwtOptions { string Secret }` ligado a la variable
`ENIGMA_JWT_SECRET`, con un método `Validate()` invocado en startup que aplica
el fail-fast. Tanto `AddJwtBearer` como la generación del token consumen
`IOptions<JwtOptions>` (un solo origen de verdad, sin lectura duplicada). El
`AuthController.GenerarToken` (hoy `static` y duplica la lectura del secret en
`AuthController.cs:80-81`) se elimina: la generación del access JWT pasa a un
**servicio inyectado** `ITokenService` (en `Server/Services/Auth/`, convención
`IXxx`+`Xxx` juntas) que consume `IOptions<JwtOptions>`.

| Entorno | Comportamiento |
|---|---|
| **Production** | Si `ENIGMA_JWT_SECRET` falta **o** mide < 32 bytes → `throw` en startup (la app no arranca). |
| **Development** | Se permite el fallback `"enigma_dev_jwt_secret_cambiar_en_produccion"`. |

**2. HSTS env-gate.**

| Archivo | Cambio |
|---|---|
| `Server/Program.cs` | Agregar `if (!app.Environment.IsDevelopment()) app.UseHsts();` antes de `UseHttpsRedirection()`. (`UseHsts` lanza si se llama en dev.) |

Nota: en dev el devcontainer corre **HTTP en localhost** (loopback, no es
amenaza real), pero la cookie de refresh de la Fase 2 requiere HTTPS para el
flag `Secure`; se resuelve con `Secure = !dev`.

**3. Política de contraseña env-gate.** Hoy `Program.cs:64-68` fija `length 6`
+ todo `false` en todos los entornos. Mover la política a **config** con dos
perfiles y seleccionar por entorno:

| Parámetro | Dev | Prod |
|---|---|---|
| `RequiredLength` | 6 | 8 |
| `RequireDigit` / `RequireUppercase` / `RequireLowercase` / `RequireNonAlphanumeric` | `false` | `true` |

Vía `appsettings.json` (`Identity:Password:*`) + `appsettings.Production.json`,
leído al armar `AddIdentity`. El seed `admin/admin123` **ya** vive dentro del
bloque `IsDevelopment()` (`Program.cs:108-153`), confirmado: solo corre en dev.

---

## Fase 2 — Rewrite del token (acoplado a la cookie)

### 2.1 Modelo de datos

Nueva entidad + migración EF. Vive en `Server/Data/Entities/Auth/`:

```
RefreshToken : GenericEntity
  int    Id
  int    UsuarioId               // FK → Usuario
  string TokenHash               // SHA-256 del token; nunca el token en claro
  DateTime ExpiresAt             // creación + 7 días
  DateTime? RevokedAt            // null = vigente
  string? ReplacedByTokenHash    // cadena de rotación (familia)
  string? CreatedByIp
```

Registrar `DbSet<RefreshToken>` en `EnigmaDbContext`. Migración
`dotnet ef migrations add RefreshTokens`.

- **Rotación:** cada `POST /auth/refresh` **revoca** el token presentado
  (`RevokedAt = now`) y emite uno nuevo encadenando `ReplacedByTokenHash`.
- **Detección de reúso:** si llega un token **ya revocado pero no expirado** →
  señal de robo → se **revoca toda la familia** (siguiendo la cadena
  `ReplacedByTokenHash`) y se devuelve 401 forzando re-login. Es la defensa
  estándar contra robo de refresh.

### 2.2 Endpoints (`AuthController`)

| Endpoint | Comportamiento |
|---|---|
| `POST /auth/login` | Valida credenciales vía Identity (sin cambios), emite **access token** (15 min) en el **cuerpo JSON** + setea **cookie de refresh** (primer token de la familia). |
| `POST /auth/refresh` | Lee la cookie, valida `TokenHash` + no revocado + no expirado, **rota** (revoca viejo + emite nuevo + nueva cookie), devuelve nuevo access token. Reúso → revoca familia + 401. |
| `POST /auth/logout` | Revoca el refresh en DB + borra la cookie. |
| `GET /auth/me`, `GET /auth/instituciones` | Sin cambios conceptuales (siguen validando el access token vía `[Authorize]`). |

Nueva clase de servicio `RefreshTokenService` (interface + impl en
`Server/Services/Auth/`, siguiendo la convención `IXxx`+`Xxx` juntas) que
encapsula: emisión, rotación, validación y detección de reúso. El
`AuthController` se queda fino y delega.

### 2.3 Cookie, CSRF y CORS

**Cookie de refresh:**

| Flag | Valor |
|---|---|
| `HttpOnly` | siempre `true` |
| `Secure` | `!IsDevelopment()` (dev corre HTTP en localhost; prod HTTPS) |
| `SameSite` | `Lax` |
| `Path` | `/auth` (scope reducido) |

Requiere que cliente y API compartan **dominio registrable** (caso típico:
`app.enigma.com`/`api.enigma.com`, o `localhost` en dev). Si algún día
estuvieran en dominios distintos → `SameSite=None;Secure`.

**CSRF:** los endpoints protegidos usan el **access token en el header
`Authorization`**, que un sitio atacante no puede setear cross-origin → **no
hay CSRF** ahí. El riesgo CSRF queda acotado a `/auth/refresh` y `/auth/logout`
(los únicos que autentican por cookie). Defensa:

- `SameSite=Lax` en la cookie (bloquea cross-site POST), **más**
- header custom `X-Requested-With` en esos dos endpoints, que dispara preflight
  CORS que el atacante no puede satisfacer.

Evita la maquinaria pesada de anti-forgery tokens.

**CORS:**

| Entorno | Origen |
|---|---|
| Dev | `http://localhost:8080` |
| Prod | `ENIGMA_ALLOWED_ORIGENS` (CSV de orígenes) |

`AllowCredentials` + **orígenes explícitos** (sin wildcard — incompatible con
credentials). Reemplaza `AllowAnyOrigin/Method/Header` de `Program.cs:30-32`.

### 2.4 Cliente Blazor

- **`TokenStore`** (servicio scoped): guarda el access token **en memoria**
  (campo). Cero `localStorage` para tokens.
- **`DelegatingHandler`** en el `HttpClient`: adjunta
  `Authorization: Bearer <access>` desde `TokenStore`; ante **401**, dispara un
  refresh (single-flight, sin recursión), actualiza el store y reintenta **una
  vez**.
- **`AuthService`:** `LoginAsync` guarda el access en `TokenStore` (el refresh
  viene como cookie automática); `LogoutAsync` llama `POST /auth/logout`;
  **elimina todo uso de `localStorage`** (`enigma_token`, `enigma_usuario`,
  `enigma_instituciones`). Instituciones: se obtienen del response de login (en
  memoria) y, tras reload, vía `GET /auth/instituciones`.
- **Silent refresh al cargar:** estado inicial anónimo → en el arranque se
  intenta `POST /auth/refresh` (cookie persistida) → si ok, se carga el access
  token y se notifica el auth-state. La sesión sobrevive al F5 sin re-login.
- **`EnigmaAuthenticationStateProvider`:** reescrito para leer claims del
  access token **en memoria** (decode del payload solo para UI), sin tocar
  `localStorage`.

### 2.5 Lifetimes y políticas (defaults)

| Parámetro | Dev | Prod |
|---|---|---|
| Access token TTL | 15 min | 15 min |
| Refresh token TTL | 7 días (sliding) | 7 días (sliding) |
| Password min length | 6 | 8 |
| Password complejidad | off | digit/upper/lower/non-alnum |
| HSTS | off | on |
| Cookie `Secure` | false (HTTP localhost) | true |
| CORS origins | localhost:8080 | `ENIGMA_ALLOWED_ORIGENS` |
| JWT secret | fallback ok | fail-fast si falta / < 32 B |

---

## Manejo de errores y edge cases

- Refresh **expirado / revocado / reúso** → 401 → el cliente limpia la sesión y
  va a `/auth/login`.
- `POST /auth/logout` sin cookie → no-op (200).
- **Refresh concurrente (pestañas múltiples):** el primer refresh rota el token;
  la segunda pestaña presenta el token recién revocado → **detona reúso**.
  Mitigación: single-flight en el cliente + **ventana de gracia corta** en el
  server (aceptar el token recién reemplazado por N segundos, ej. 10 s).
  Documentado como tradeoff conocido.
- **Reloj del cliente desincronizado:** el access token parece válido pero el
  server lo rechaza → el `DelegatingHandler` reintenta con refresh.
- **Reloj del servidor:** `ClockSkew` actual es 1 min (`Program.cs:100`); se
  mantiene.

---

## Testing

- **Unit:**
  - `JwtSecretLoader`: fail-fast en prod (falta / < 32 B), fallback en dev.
  - `RefreshTokenService`: rotación, detección de reúso, revocación de familia,
    expiración.
  - Política de contraseña por perfil (dev laxo / prod estricto).
- **Integration (`WebApplicationFactory<Program>`):**
  - Login setea cookie + access.
  - Refresh rota y revoca el anterior.
  - Logout revoca.
  - Reúso → 401 + familia revocada.
  - CORS rechaza origen no listado.
  - `/auth/me` con access expirado → refresh automático del handler.
- **Existente:** `Tests/Auth/LoginTest.cs` y `Tests/Architecture/ArchitectureTests.cs`
  deben seguir pasando. Ajustar los que asuman token en `localStorage` o en el
  body (si los hay).

---

## Fuera de alcance (YAGNI)

- 2FA / MFA.
- Verificación de email / flujo de recuperación de contraseña.
- Rate limiter propio (el lockout de Identity — 5 intentos / 5 min — ya cubre
  fuerza bruta). Recortable/agregable luego.
- Cambio del modelo de dominio `Institucion`/`Usuario`.
