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
