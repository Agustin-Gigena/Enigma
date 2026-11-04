# Enigma

Monorepo del proyecto Enigma: hosts Web (Blazor WebAssembly) y MAUI (Android/Windows), API ASP.NET Core y librería de UI compartida, todo sobre .NET 10 con backend MySQL.

## Estructura

```
Enigma/
├── src/
│   ├── Enigma.Api/        # ASP.NET Core Web API + EF Core (net10.0)
│   ├── Enigma.Web/        # Host Blazor WebAssembly (net10.0)
│   ├── Enigma.SharedUI/   # RCL: páginas, layout, servicios, assets (net10.0)
│   ├── Enigma.Maui/       # Host Blazor Hybrid Android/Windows (net10.0)
│   └── Enigma.Shared/     # Tipos comunes, DTOs (net10.0)
├── tests/
│   └── Enigma.Tests/      # NUnit unit + Playwright E2E (net10.0)
└── deploy/                # docker-compose + Caddyfile para la API en VPS
```

`Web` y `Maui` montan `SharedUI.App` vía `AddEnigmaSharedUI()`; todos los proyectos consumen `Shared` (DTOs) y solo los hosts referencian `SharedUI`. Nadie referencia `Enigma.Api` salvo los tests. La solution `Enigma.slnx` orquesta los seis proyectos; `Enigma.Core.slnf` excluye Maui para builds de CI/pr-validation.

## Clonar

```bash
git clone https://github.com/Agustin-Gigena/Enigma.git
cd Enigma
```

No hay submódulos: es un clonado normal.

## Build

```bash
# Build completo
dotnet build Enigma.slnx

# API (:8081, auto-migra + siembra en Development)
dotnet watch --project src/Enigma.Api

# Web (Blazor WASM dev server, :8080)
dotnet watch --project src/Enigma.Web
```

## Base de datos (MySQL 8.0)

```bash
bash .devcontainer/start-dev.sh   # levanta MySQL (enigma-dev-db) + watchers
dotnet ef database update --project src/Enigma.Api
```

## Licencia

Funcional Source License 1.1 (FSL-1.1-ALv2). Ver [`LICENSE-FSL-1.1-ALv2`](./LICENSE-FSL-1.1-ALv2).

Source-available: el código es legible y modificable para **usos permitidos** (uso interno, educación e investigación no comercial, servicios profesionales a licenciatarios). Está **prohibido siempre** usar Enigma en un producto o servicio comercial que sustituya o compita con el Software o ofrezca funcionalidad similar (Competing Use).

A los 2 años de publicada cada versión, convierte automáticamente a Apache 2.0; la cláusula anti-competencia sobrevive al Change Date.
