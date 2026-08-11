# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

- **Personal de instituciones educativas** — docentes, administradores y directivos de universidades, secundarias, primarias y cursos/escuelas. Operan el sistema desde un backoffice: gestionan datos y procesos de la institución. **Un mismo usuario puede acceder a varias instituciones.**
- **Estudiantes** — usuarios finales de la plataforma. Su alcance exacto (qué tareas pueden hacer) está **sin definir**.

## Product Purpose

Plataforma SaaS para instituciones educativas — universidades, secundarias, primarias y cursos/escuelas — que sirve tanto al personal interno (gestión y administración) como a los estudiantes. Un solo despliegue sirve a **muchas instituciones**; el login (usuario+contraseña y/o OAuth) va seguido de la selección de la institución a la que se ingresa. El propósito de negocio concreto (qué módulos resuelven qué tareas) está **sin definir**: el repo hoy es un scaffold con el dominio de Auth implementado y frontend de plantilla.

## Positioning

**Sin declarar** el mecanismo de producto frente a otras plataformas educativas. Multi-tenancy por subdominio descartado. Diferenciadores técnicos confirmados del repo (no comerciales): auditoría integral por entidad (soft-delete + sellos CreadoPor/ModificadoPor/BorradoPor) y acceso ambiental al usuario actual desde cualquier capa, sin inyección.

## Operating Context

- Aplicación web: cliente Blazor WebAssembly, API ASP.NET Core, backend MySQL 8.0.
- Entorno dev: devcontainer con .NET 10 SDK; MySQL vía Docker Compose (credenciales dev `enigma`/`enigma_dev_password`); migraciones auto-aplicadas en Development.
- Puertos dev: front en `:80`, API en `:8081`, docs de API (Scalar) en `/api/docs`.
- Idioma de operación: español (naming de entidades, UI y documentación).
- Autenticación: ASP.NET Core Identity (usuarios con hash y lockout) + JWT bearer (issuer `Enigma`, audience `Enigma.Client`, secret vía `ENIGMA_JWT_SECRET`); seed dev `admin`/`admin123` sin registro público. Docs de API (Scalar) en `/api/docs`.

## Capabilities and Constraints

**Confirmado:**

- **Auth (dominio actual):** entidad `Usuario` (NombreUsuario, CorreoElectronico, Contrasena, LastLogin), `UsuarioRepository`, `CurrentUserService` ambiental (AsyncLocal + middleware) invocable desde repositorios, controladores y entidades sin DI; política `ENIGMA_AUTH_REQUIRED` (`true` → lanza `UnauthorizedAccessException` si no hay usuario; ausente/false → `null`).
- **Auditoría:** toda entidad hereda `GenericEntity` — CreadoEn/CreadoPor, ModificadoEn/ModificadoPor, BorradoEn/BorradoPor, BorradoLogico (soft-delete).
- **Stack:** .NET 10 (`net10.0`), EF Core 10 + MySQL 8.0 (retry 3×5s), nullable + implicit usings, naming en español.
- **Patrones:** `GenericRepository` síncrono (refactor async pendiente), `GenericController` base, DI central en `Program.cs`, migración `InitialCreate` aplicada.
- **Muchas instituciones, un solo despliegue:** sin tenancy por subdominio (`{tenant}.enigma.com` descartado) y **sin branding por institución** (una sola identidad visual para todas). **Un mismo usuario puede acceder a varias instituciones** (membresías usuario↔institución); por eso la selección post-login y un contexto activo de institución por sesión.
- **Flujo de acceso:** login con usuario+contraseña y/o OAuth; tras autenticarse, el usuario selecciona la institución a la que ingresa.

**Pendiente / sin decidir:**

- Providers OAuth concretos (Google/Microsoft/otros) **sin definir** — el login es usuario+contraseña vía Identity; el JWT ya está conectado de punta a punta.
- Persistencia del contexto de institución elegido (sesión, token, cookie) — **sin definir**.
- Módulos funcionales (qué gestiona cada institución) — **sin definir**.
- Alcance de los estudiantes dentro del sistema — **sin definir**.
- Tests: no existe ningún proyecto de test; cobertura E2E planeada para `CurrentUserService`.

## Brand Commitments

- Nombre: **Enigma** (repo público de Agustin-Gigena).
- Idioma: español en UI, entidades y documentación — no "corregir" el naming a inglés.
- Licencia: FSL-1.1-ALv2 (source-available; Competing Use prohibido; convierte a Apache 2.0 a los 2 años de cada versión).
- Bootstrap 5 (full dist) actualmente en `wwwroot/lib/bootstrap/` — deprecación a v6 comentada en `index.html`.

## Evidence on Hand

- Código actual: dominio Auth funcional (`CurrentUserService` + `CurrentUserMiddleware` + `Usuario` + `UsuarioRepository`), migración `InitialCreate` aplicada.
- Specs de diseño aprobadas: `docs/superpowers/specs/2026-08-10-current-user-service-design.md`, `docs/superpowers/specs/2026-07-24-container-ecosystem-design.md`.
- **Ausencias:** sin contenido real de instituciones o estudiantes, sin testimonios, sin pricing, sin datos de demostración. No fabricar contenido educativo en futuras superficies sin material real.

## Product Principles

1. **Auditabilidad total** — toda mutación de datos queda sellada (quién, cuándo) y el borrado es lógico; ninguna entidad escapa del patrón.
2. **Acceso ambiental al usuario** — repositorios, controladores y entidades resuelven el usuario actual sin inyección, con una sola resolución por request.
3. **Español primero** — entidades, UI y docs en español; consistencia por encima de convenciones inglesas.
4. **Multi-institución, un despliegue** — el SaaS sirve a muchas instituciones en un solo despliegue; el contexto de institución se establece tras el login y las decisiones de arquitectura (aislamiento de datos entre instituciones) deben contemplarlo desde ya.
5. **Auth progresiva** — el andamiaje existe y no debe bloquear features; conectar JWT/cookies cuando exista un módulo que lo requiera.
