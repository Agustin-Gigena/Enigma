# Diseño: Sistema de menús basado en módulos/secciones (por namespace)

**Fecha:** 2026-08-27
**Rama:** `feat/menuSystem`
**Estado:** Aprobado en brainstorming, pendiente de revisión final

## Resumen

El menú de la aplicación se construye a partir de un **catálogo explícito de módulos y secciones** definido en `Shared` (ej: módulo *Administración* → sección *Personas*), inspirado en la convención de namespaces/carpetas por dominio que ya usa el Server. El acceso a cada sección es un **permiso por sección** (`Modulo.Seccion`), otorgado mediante **roles asignados por institución** (la membresía usuario↔institución define los roles). Los permisos viajan como **claims del JWT** (enfoque elegido por el usuario sobre endpoint de menú), con un **flujo de dos fases**: no existe sesión hasta elegir institución.

Decisiones clave tomadas en brainstorming:

| Decisión | Elección |
|---|---|
| Propósito del menú | Navegación **+ control de acceso** |
| Fuente de verdad del catálogo | Definición **explícita en Shared** (sin reflexión) |
| Granularidad de permisos | **Por sección** (`Modulo.Seccion`) |
| Alcance de permisos | **Por institución** (membresía usuario↔institución) |
| Asignación | **Roles por institución** agrupando secciones |
| UI de gestión de roles | **Incluida** en este spec |
| Contenido inicial | Administración → Usuarios, Instituciones |
| Layout | Dropdowns en barra superior con overflow **"Más +"** |
| Transporte de permisos | **Claims del JWT** (con espejo vía `/auth/me` por cookie HttpOnly) |
| Autorización repetitiva | **Ninguna**: guard central por catálogo (cliente) y convención de namespace (server) |
| Identity | Uso intensivo: `Rol : IdentityRole<int>`, `RoleManager`, role claims de Identity |

## 1. Catálogo de módulos y secciones (Shared)

Archivo único `Shared/Modules/CatalogoModulos.cs`:

```csharp
public sealed record ModuloDef(string Clave, string Etiqueta, int Orden);

public sealed record SeccionDef(
    string Clave,        // "Administracion.Usuarios" — TAMBIÉN es la clave de permiso
    string ModuloClave,  // "Administracion"
    string Etiqueta,     // "Usuarios"
    string Ruta,         // "/administracion/usuarios"
    int Orden);

public static class CatalogoModulos
{
    public static readonly IReadOnlyList<ModuloDef> Modulos =
    [
        new("Administracion", "Administración", 1),
    ];

    public static readonly IReadOnlyList<SeccionDef> Secciones =
    [
        new("Administracion.Usuarios",      "Administracion", "Usuarios",      "/administracion/usuarios",      1),
        new("Administracion.Instituciones", "Administracion", "Instituciones", "/administracion/instituciones", 2),
    ];

    /// <summary>Validación en runtime: claves/rutas únicas, módulo existente.</summary>
    public static bool ExisteSeccion(string clave);
    public static SeccionDef? SeccionPorRuta(string ruta);
}
```

Reglas:

- La clave de sección `Modulo.Seccion` **es** el permiso. No existe entidad "Permiso" separada.
- Agregar una sección = agregar la entrada al catálogo + crear la página (y el controller si tiene backend). El seed de roles Admin itera el catálogo, así que nunca queda desactualizado.
- `Server` debe agregar el `<ProjectReference>` a `Shared` (hoy no lo tiene).

### Reglas de diseño (obligatorias, documentadas aquí)

1. **Regla de catálogo (cliente):** toda página de sección se registra en `CatalogoModulos` con su ruta; el guard central la protege automáticamente. Una página sin registro no entra al menú y no es navegable.
2. **Regla de namespace (server):** todo controller bajo `Enigma.Server.Controllers.<Dominio>` obtiene su autorización automáticamente de la convención (sección 4). El namespace + nombre de controller determinan la sección.

## 2. Modelo de datos (Server)

### `Rol : IdentityRole<int>` (`Data/Entities/Auth/Rol.cs`)

Reemplaza el `IdentityRole<int>` del `AddIdentity` en `Program.cs`. Replica auditoría/soft-delete igual que `Usuario` (hereda de Identity, no puede heredar `GenericEntity`; sin navegaciones CreadoPor/BorradoPor por relación circular). Definición y normalización de nombres vía `RoleManager<Rol>`.

### Secciones de un rol = role claims de Identity

Las secciones que un rol habilita se guardan como **role claims** (`RoleManager.AddClaimAsync(rol, new Claim("seccion", clave))`) usando la tabla `IdentityRoleClaim<int>` de Identity. **No hay tabla custom `RolSeccion`.** Secciones visibles de una membresía = unión de claims `seccion` de sus roles. La validación de claves contra `CatalogoModulos` ocurre al sembrar/guardar (servicio), no por FK.

### `Membresia` (`Data/Entities/Auth/Membresia.cs`)

Join explícito Usuario↔Institución que reemplaza el many-to-many implícito `Usuario.Instituciones`:

- Hereda `GenericEntity` (trae su `Id int` propio) con **índice único** (`UsuarioId`, `InstitucionId`) — no PK compuesta, para conservar el patrón de auditoría del repo.
- `virtual List<MembresiaRol> Roles`.

### `MembresiaRol`

Join `MembresiaId` + `RolId` (PK compuesta). **Única pieza custom de roles** porque el `UserRoles` de Identity es global (userId+roleId) y no puede scopearse por institución — decisión documentada: no se fuerza `IdentityUserRole<int>` con atributos extra (rompería `UserManager.AddToRoleAsync`).

### Lazy loading (convención permanente del proyecto)

EF Core con lazy loading habilitado: paquete `Microsoft.EntityFrameworkCore.Proxies` en Server, `.UseLazyLoadingProxies()` en el `AddDbContext`, y **todas las propiedades de navegación `virtual`** (incluidas las de auditoría de `GenericEntity`). Preferir acceso directo a navegaciones por sobre `Include`/`LoadAsync` explícitos; las proyecciones LINQ a DTO siguen siendo la herramienta para queries de API (no disparan proxies).

### Services sin BD (convención permanente del proyecto)

Los services **nunca** acceden a `EnigmaDbContext` ni `DbSet<>`: todo acceso a datos vive en repositories dedicados (`Server/Data/Repositories/<Dominio>/`, patrón `UsuarioRepository`); los services orquestan repositories. Controllers → Services → Repositories → DbContext. La regla la vigila `ArchitectureTests.SoloLosRepositoriesAccedenAlContextoYDbSets` (exención temporal única: `SeedingService` hasta su migración a repositories). `UserManager`/`RoleManager` de Identity quedan fuera de la regla (managers, no DbContext).

### Repositories devuelven entidades (convención permanente del proyecto)

Los repositories **nunca** devuelven ni arman DTOs: devuelven entidades. El armado de DTOs vive en services o controllers según el caso (el mapping entidad→DTO de los contratos de service, y la composición de respuesta en el controller). Vigilado por `ArchitectureTests.RepositoriesNoDevuelvenDtos`.

### Migración

Una migración EF: convierte el many-to-many implícito existente en filas de `Membresia` (preserva datos), agrega columnas de auditoría a `AspNetRoles`, crea `MembresiaRol`. Navegaciones: `Usuario.Membresias`, `Institucion.Membresias`; `UsuarioRepository.ObtenerInstitucionesAsync` se adapta para excluir membresías/instituciones con `BorradoLogico`.

## 3. Tokens en dos fases (no hay sesión hasta elegir institución)

El JWT viaja en cookie HttpOnly `enigma_token` (sin cambios). El cliente nunca lee el token: `/auth/me` es su espejo.

### Fase 1 — Pre-autenticación (5 minutos)

`POST /auth/login` (anónimo, rate-limited, SignInManager + lockout como hoy): valida credenciales y emite cookie con token de **5 min**, claims: `sub`, `nameid`, `name`, `jti`, `tipo=pre-autenticacion`. La respuesta mantiene el body actual (usuario + instituciones). **Con este token no existe sesión**: solo autoriza

- `GET /auth/instituciones` — listar instituciones con membresía activa (para la pantalla de selección)
- `POST /auth/institucion` — elegir institución

`POST /auth/logout` sigue sin `[Authorize]` (limpia cookie en cualquier fase).

### Fase 2 — Sesión (8 horas)

`POST /auth/institucion` con body `{ institucionId }`: valida membresía activa del `sub`; emite cookie con token de **8 h**, claims: `sub`, `nameid`, `name`, `jti`, `tipo=sesion`, `institucion={institucionId}`, y **una claim `ClaimTypes.Role` por sección visible** (unión de claims `seccion` de los roles de esa membresía). Acepta tanto el token pre-autenticación (primera elección) como uno de sesión (cambio posterior de institución).

### Policies (estándar, sin provider custom)

- `DefaultPolicy`: autenticado + claim `tipo=sesion` → todo `[Authorize]` existente/futuro rechaza el pre-auth con 403.
- Policy `PreAutenticacion`: solo autenticado (cualquier `tipo`) → los 2 endpoints de selección.
- `[Authorize(Roles = "Administracion.Usuarios")]` funciona nativo: las secciones viajan como claims `role` en el token.

### `/auth/me` extendido (solo sesión)

Devuelve `SesionDto` (reemplaza el `UsuarioDto` actual): usuario, `institucionActivaId`, `permisos[]` (las claims `role` del token). El `AuthenticationState` del cliente incluye una claim `permiso` por sección → el menú se arma con catálogo ∩ claims. Cache de 5 min como hoy.

### Trade-offs aceptados del enfoque claims-en-JWT

- Revocar/editar un rol no aplica hasta la próxima re-emisión (TTL 8 h). Mitigación incluida: al guardar roles que afectan al usuario actual en su institución activa, el cliente re-llama a `POST /auth/institucion`.
- El token crece con la cantidad de secciones (despreciable con el catálogo actual).

## 4. Enforcement — sin autorización repetitiva

### Server: convención de namespace

`IControllerModelConvention` (~20 líneas) registrada en `Program.cs`:

- Namespace `Enigma.Server.Controllers.Administracion` + `UsuariosController` → aplica automáticamente `Authorize(Roles = "Administracion.Usuarios")` (sección = namespace sin el prefijo `Enigma.Server.Controllers.` + nombre de controller sin sufijo `Controller`).
- Al arranque valida la convención contra `CatalogoModulos`: un controller de dominio sin entrada en el catálogo **rompe el arranque** (fail fast — imposible dejar un endpoint desprotegido por descuido).
- Los controllers de `Controllers/Auth/` quedan fuera de la convención (usan `[Authorize]`/policies explícitas).
- Los endpoints de dominio scopean sus consultas por el claim `institucion` del token — nunca por un id enviado por el cliente.

### Endpoints de dominio nuevos

- `Controllers/Administracion/InstitucionesController` — `GET administracion/instituciones`: todas las instituciones (módulo admin).
- `Controllers/Administracion/UsuariosController`:
  - `GET administracion/usuarios` — membresías de la institución del claim `institucion`, con usuario y roles (`UsuarioInstitucionDto`).
  - `PUT administracion/usuarios/{usuarioId}/roles` — body `{ roles: [nombres] }`; reemplaza los roles de esa membresía; valida membresía en institución activa y existencia de roles.

### Cliente: guard central por catálogo

**Ninguna página lleva `[Authorize]`.** En `App.razor`, `Router.OnNavigateAsync` consulta el destino contra `CatalogoModulos.SeccionPorRuta(ruta)`:

- ruta de sección + claim `permiso` presente → renderiza
- ruta de sección + sin claim → redirect a `/acceso-denegado`
- rutas de auth/login → anónimas; resto (Home, 404) → requiere sesión con institución (sin ella, redirect a selección de institución)

## 5. Cliente (Blazor WASM)

### `NavMenu.razor` (nuevo, en la barra de `MainLayout`)

Dropdowns por módulo entre el brand y las acciones. Fuente: `CatalogoModulos` filtrado por las claims `permiso` del `AuthenticationState` (módulos ordenados por `Orden`, secciones ídem; módulo sin secciones visibles no se dibuja).

**Overflow "Más +"**: los dropdowns que no entran en el ancho disponible colapsan a un dropdown "Más +" final. Implementación: `ResizeObserver` en JS interop (nuevo archivo en `wwwroot/js/`, junto a `EnigmaMotion`) que mide anchos de ítems contra el ancho del contenedor y mueve los excedentes al overflow. En pantallas angostas todo cae en "Más +" naturalmente.

### Cambio de institución

El nombre de institución en la barra pasa a ser clickable → dropdown con las instituciones del usuario → al elegir: `POST /auth/institucion` + `NotifyAuthStateChanged()` → el menú se recalcula. Con una sola institución se auto-selecciona tras el login.

### Menú de cuenta (reemplaza el botón "Salir")

El botón `Salir` de la barra desaparece. En su lugar, un **icono de cuenta** (avatar/icono de usuario, junto al nombre del usuario) despliega un dropdown con la acción **"Cerrar sesión"**. Estructura pensada para crecer: el dropdown es una lista de ítems donde luego se agregan perfil, preferencias, etc., sin rediseñar la barra. El nombre de usuario visible hoy (`app__usuario`) se mantiene junto al icono.

### Páginas nuevas

- `Pages/AccesoDenegado.razor` — `/acceso-denegado`, mensaje + link a Home.
- `Pages/Administracion/Usuarios.razor` — tabla de usuarios de la institución activa con roles (chips/checkboxes por rol); guardar → `PUT`; si el usuario editado es el actual, re-llamar `POST /auth/institucion` para refrescar sus propios permisos.
- `Pages/Administracion/Instituciones.razor` — listado (nombre, tipo).

### DTOs nuevos en `Shared/Dtos`

`SesionDto(UsuarioDto Usuario, int? InstitucionActivaId, List<string> Permisos)`, `RolDto(int Id, string Nombre)`, `UsuarioInstitucionDto(UsuarioDto Usuario, List<string> Roles)`, `SeleccionInstitucionRequest(int InstitucionId)`, `ActualizarRolesRequest(List<string> Roles)`.

### AuthService (cliente)

Nuevo `SeleccionarInstitucionAsync(institucionId)` → `POST /auth/institucion`; `GetUsuarioAsync` pasa a devolver `SesionDto` (el provider de autenticación arma las claims `permiso` desde ahí; en el token JWT esas secciones viajan como `ClaimTypes.Role`, pero el cliente las consume desde el espejo `SesionDto`).

## 6. Seed

`SeedingService` extendido (idempotente): crea el rol **`Admin`** vía `RoleManager.CreateAsync` con claims `seccion` para **todas** las secciones de `CatalogoModulos` (itera el catálogo), y lo asigna a las membresías del usuario admin sembrado. El CRUD de definiciones de roles queda fuera del scope; este spec solo siembra y **asigna**.

## 7. Manejo de errores

| Caso | Comportamiento |
|---|---|
| Pre-auth vencido (5 min) antes de elegir | 401 → cliente redirige a login |
| Endpoint de sesión llamado con pre-auth | 403 (DefaultPolicy) |
| `POST /auth/institucion` sin membresía activa | 403 con mensaje |
| Usuario sin ningún rol en la institución | Menú vacío (solo Home); rutas de sección → `/acceso-denegado` |
| Rol editado deja de incluir una sección | Desaparece del menú a la re-emisión; hasta entonces el server ya la protege |
| Controller de dominio sin entrada en catálogo | Arranque del server falla (fail fast) |
| Error de red al guardar roles | Mensaje inline; sin cambio optimista |

## 8. Testing (sigue la estructura de `Tests/`)

**Unit** (`Tests/Unit/`):

- Catálogo: claves únicas, rutas únicas, módulo de cada sección existe, `SeccionPorRuta`.
- `TokenService`: token pre-auth (claims, TTL 5 min) vs sesión (claims `tipo`/`institucion`/`role`, TTL 8 h).
- Resolución de secciones de una membresía (unión de role claims; roles vacíos → vacío).
- Convención de namespace: controller de dominio → sección correcta; sin entrada en catálogo → error de arranque.

**E2E** (`Tests/E2E/`, `WebApplicationFactory` como `LoginFlowTest`):

- Login → token pre-auth → elegir institución → `/auth/me` con permisos correctos.
- Endpoint de sección con pre-auth → 403; con sesión y rol → 200; sin rol → 403.
- `POST /auth/institucion` con institución sin membresía → 403.
- Asignar rol a usuario → re-emitir → nuevo token lo refleja.
- Seed idempotente con rol Admin (claims = catálogo completo).

**Architecture** (`ArchitectureTests.cs`): extender — controllers bajo carpeta de dominio protegidos por la convención; páginas de sección registradas en catálogo.

## Fuera de scope

- CRUD de definiciones de roles (crear/editar rol y sus secciones desde UI).
- Permisos por acción (Ver/Crear/Editar/Borrar) — cuando existan páginas CRUD.
- Módulos/secciones adicionales (Personas, etc.) — se agregan al catálogo cuando existan sus páginas.
