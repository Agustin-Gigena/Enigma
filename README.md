# Enigma

Monorepo del proyecto Enigma: cliente Blazor WebAssembly, API ASP.NET Core y librería compartida, todo sobre .NET 10 con backend MySQL.

## Estructura

```
Enigma/
├── Client/   # Blazor WebAssembly (net10.0)
├── Server/   # ASP.NET Core Web API + EF Core (net10.0)
└── Shared/   # Tipos comunes, DTOs (net10.0)
```

`Server` y `Client` referencian `Shared` vía `ProjectReference`. La solution `Enigma.slnx` orquesta los tres proyectos.

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

# Server (API)
dotnet run --project Server/Enigma.Server.csproj

# Client (Blazor WASM dev server)
dotnet run --project Client/Enigma.Client.csproj
```

## Base de datos (MySQL 8.0)

```bash
docker-compose up -d
dotnet ef database update --project Server/Enigma.Server.csproj
```

## Devcontainer y emulador Android

El devcontainer incluye el workload `maui-android`, el emulador y un AVD `enigma-dev`
(`system-images;android-34;google_apis;x86_64`) para **ejecutar** la app MAUI, no solo
compilarla. El provisioning corre idempotentemente como dev user vía
`bash .devcontainer/setup.sh` (invocado desde `postCreateCommand`).

- **Con `/dev/kvm` en el host**: emulación acelerada. `compose.json` monta el device
  (`devices: ["/dev/kvm:/dev/kvm"]`) y agrega al contenedor el GID del device
  (`ENIGMA_KVM_GID`, detectado por `setup.sh` en el host).
- **Sin `/dev/kvm`** (VMs sin nested virtualization): el emulador **no puede arrancar** —
  las imágenes x86_64 de Android exigen aceleración de CPU por hardware (swiftshader solo
  reemplaza la GPU). Provisioning y AVD quedan listos para usar el devcontainer en un
  host con KVM; `setup.sh` detecta la ausencia y lo reporta en el log.


## Licencia

Funcional Source License 1.1 (FSL-1.1-ALv2). Ver [`LICENSE-FSL-1.1-ALv2`](./LICENSE-FSL-1.1-ALv2).

Source-available: el código es legible y modificable para **usos permitidos** (uso interno, educación e investigación no comercial, servicios profesionales a licenciatarios). Está **prohibido siempre** usar Enigma en un producto o servicio comercial que sustituya o compita con el Software o ofrezca funcionalidad similar (Competing Use).

A los 2 años de publicada cada versión, convierte automáticamente a Apache 2.0; la cláusula anti-competencia sobrevive al Change Date.
