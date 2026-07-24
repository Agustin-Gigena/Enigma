# Container Ecosystem Design — Enigma

**Date:** 2026-07-24
**Status:** Draft (pending user review)
**Branch:** `development`
**Scope:** Build, packaging, and dev-environment containerization for the Enigma monorepo.

---

## 1. Goal

Define a container ecosystem for Enigma covering two concerns:

- **Production packaging** — ship `Server` and `Client` as two independent Docker images. `Shared` is not a runtime artifact; it is compiled into each image at build time.
- **Development environment** — the devcontainer runs the SDK **and** MySQL as two services in a single Docker Compose file, replacing the current standalone `docker-compose.yml`.

Production deployment is **not orchestrated** by Compose. Each image is built and pushed to a registry; the operator decides where to run them (k8s, VM, etc.). Compose exists only for the devcontainer.

## 2. Context (current state)

- Monorepo: `Client/` (Blazor WASM), `Server/` (ASP.NET Core Web API), `Shared/` (class library).
  - `Server/Enigma.Server.csproj` and `Client/Enigma.Client.csproj` already `<ProjectReference>` `Shared/Enigma.Shared.csproj`.
- `.devcontainer/devcontainer.json` today uses `"image": "mcr.microsoft.com/dotnet/sdk:10.0"` (no Dockerfile, no MySQL).
- `docker-compose.yml` at repo root defines a standalone MySQL 8.0 service (dev DB, credentials `enigma`/`enigma_dev_password`).
- **No production Dockerfiles exist** for Server or Client (AGENTS.md note 10).
- `Server/appsettings.json` interpolates MySQL connection from env vars: `MYSQL_HOST`, `MYSQL_PORT`, `MYSQL_DATABASE`, `MYSQL_USER`, `MYSQL_PASSWORD`.
- Branching: `development` (default) and `production`. `main` removed.

## 3. Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Client runtime container | Separate image, static files served by **Caddy** | Decouple front/back scaling; Caddy gives minimal config for a SPA fallback. |
| Shared packaging | Compiled into each image (multi-stage build); no `Shared` image | `Shared` is a compile-time dependency, not a runtime process. |
| Docker build context | Repo root (`.`), one Dockerfile per project folder (`-f Server/Dockerfile .`) | Shared is reachable from the context without duplicating copy logic; Dockerfiles live beside their project. |
| Dev MySQL integration | MySQL as a service in the **devcontainer Compose** (alongside the SDK service) | Single Compose file the devcontainer manages; `localhost:3306` reachable from the SDK service via shared network. |
| Production orchestration | None in this spec | Out of scope — the operator runs the two images where they choose. |

### Non-goals (YAGNI)

- No `Shared` Dockerfile (no runtime process).
- No production `docker-compose.yml`.
- No CI/CD pipeline, registry automation, or GitHub Actions.
- No TLS inside containers (the operator terminates TLS in front; Caddy here serves plain HTTP).
- No Kubernetes manifests, Helm charts, or deployment configs.

## 4. Production Dockerfiles

Both Dockerfiles use multi-stage builds with the repo root as context (`docker build -f <path>/Dockerfile -t enigma/<name> .`). The build stage runs `dotnet publish`; the runtime stage is a minimal base image with only the published output.

### 4.1 `Server/Dockerfile`

- **Build stage** — `mcr.microsoft.com/dotnet/sdk:10.0`.
  - Copy `Shared/Enigma.Shared.csproj` and `Server/Enigma.Server.csproj`, `dotnet restore`.
  - Copy `Shared/` and `Server/` source, `dotnet publish -c Release -o /app`.
- **Runtime stage** — `mcr.microsoft.com/dotnet/aspnet:10.0`.
  - Copy `/app` from build stage, `EXPOSE 8080` (Kestrel default port in .NET 10 container images).
  - `ENTRYPOINT ["dotnet", "Enigma.Server.dll"]`.
- **Runtime config** — reads MySQL connection from env vars via `appsettings.json` interpolation (no secrets baked in). Expected env: `MYSQL_HOST`, `MYSQL_PORT`, `MYSQL_DATABASE`, `MYSQL_USER`, `MYSQL_PASSWORD`.
- **EF migrations in prod** — the operator runs `dotnet ef database update` against the target DB (or an equivalent migration job) before/independently of the server starting. The server's dev auto-migration (`Program.cs`) is not relied upon in production.

### 4.2 `Client/Dockerfile`

- **Build stage** — `mcr.microsoft.com/dotnet/sdk:10.0`.
  - Copy `Shared/Enigma.Shared.csproj` and `Client/Enigma.Client.csproj`, `dotnet restore`.
  - Copy `Shared/` and `Client/` source, `dotnet publish -c Release -o /app`.
  - The Blazor WASM publish produces `/app/wwwroot/` (the static assets).
- **Runtime stage** — `caddy:alpine`.
  - Copy `/app/wwwroot/` → `/srv/`.
  - Add a `Caddyfile` serving the SPA: `:80 { root * /srv try_files {path} /index.html file_server }`.
  - `EXPOSE 80`.
- **API URL in prod** — the client must reach `enigma/server` at its deployment URL. The base address is configured via `appsettings.Production.json` in the Client project (or an equivalent static-config mechanism) — not resolved in this spec beyond identifying it as a required runtime config. (See Open Questions §8.)
- `Microsoft.AspNetCore.Components.WebAssembly.DevServer` is a `PrivateAssets` dev dependency; `dotnet publish` already excludes it from release output.

## 5. Devcontainer Compose

Replaces both the current `"image":` in `devcontainer.json` and the root `docker-compose.yml`. The root `docker-compose.yml` is removed; its MySQL definition moves here.

### 5.1 `.devcontainer/docker-compose.yml`

```yaml
services:
  dev:
    image: mcr.microsoft.com/dotnet/sdk:10.0
    volumes:
      - ..:/workspaces/Enigma:cached
    command: sleep infinity
    depends_on:
      mysql:
        condition: service_healthy
    network_mode: service:mysql

  mysql:
    image: mysql:8.0
    restart: unless-stopped
    environment:
      MYSQL_ROOT_PASSWORD: root_password
      MYSQL_USER: enigma
      MYSQL_PASSWORD: enigma_dev_password
      MYSQL_DATABASE: enigma_db
    ports:
      - "3306:3306"
    volumes:
      - mysql-data:/var/lib/mysql
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost", "-u", "enigma", "-penigma_dev_password"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  mysql-data:
```

- `network_mode: service:mysql` makes the `dev` service share MySQL's network namespace — `localhost:3306` from inside the devcontainer reaches MySQL, matching `appsettings.json`'s `MYSQL_HOST=localhost` default.
- `depends_on` with `service_healthy` ensures MySQL accepts connections before `dev` starts.
- The `mysql-data` named volume persists DB state across rebuilds.

### 5.2 `.devcontainer/devcontainer.json`

Switches from `"image":` to `"dockerComposeFile":` + `"service": "dev"`.

```jsonc
{
  "name": ".NET 10 + MySQL",
  "dockerComposeFile": "docker-compose.yml",
  "service": "dev",
  "workspaceFolder": "/workspaces/Enigma",
  "features": {
    "ghcr.io/devcontainers/features/node:2.1.0": {},
    "ghcr.io/iyaki/devcontainer-features/oh-my-pi:1": {},
    "ghcr.io/devcontainers-extra/features/gh-cli:1": {}
  },
  "customizations": {
    "vscode": {
      "settings": { "terminal.integrated.defaultProfile.linux": "bash" },
      "extensions": ["ms-dotnettools.csharp", "ms-dotnettools.csdevkit"]
    }
  }
}
```

The existing features (`oh-my-pi`, `gh-cli`, `node`) and VS Code customizations are preserved verbatim.

### 5.3 Root `docker-compose.yml`

Deleted. Its MySQL definition is absorbed by §5.1; running `docker-compose up -d` at the repo root is no longer the dev workflow (the devcontainer manages MySQL instead).

## 6. Build & Deploy Flow

```
Repo root (build context)
  ├── docker build -f Server/Dockerfile -t enigma/server .   →  enigma/server  (aspnet:10.0)
  └── docker build -f Client/Dockerfile -t enigma/client .    →  enigma/client (caddy:alpine)
```

- Both images are built with the repo root as context so `Shared/` is in-scope without extra copy logic.
- The operator tags and pushes both images to a registry of their choice.
- The operator runs each image where they choose (k8s, VM, etc.); no Compose orchestration is defined for production.
- The Server image reads its DB credentials from env vars at runtime — secrets are injected by the deployment environment, never into the image.

## 7. Isolation & Boundaries

| Unit | Responsibility | Depends on | Tested by |
|---|---|---|---|
| `Server/Dockerfile` | Produce a self-contained `enigma/server` runtime image with the API and its dependencies (incl. `Shared`) compiled in. | Repo root context, `Shared/`, `Server/`. | `docker build -f Server/Dockerfile .` succeeds; `docker run enigma/server` starts Kestrel on 8080. |
| `Client/Dockerfile` | Produce a self-contained `enigma/client` static-serving image with the Blazor WASM assets (incl. `Shared`) compiled in. | Repo root context, `Shared/`, `Client/`. | `docker build -f Client/Dockerfile .` succeeds; `docker run enigma/client` serves `index.html` on 80; deep-link routes return `index.html`. |
| `.devcontainer/docker-compose.yml` | Define the dev SDK service + MySQL service on a shared network for local development. | `sdk:10.0`, `mysql:8.0` images. | Devcontainer opens; `dotnet build Enigma.slnx` runs; `mysqladmin ping` from the `dev` service succeeds. |

Each Dockerfile can be reasoned about and built independently. The Devcontainer Compose is independent of the production Dockerfiles and is only exercised when opening the repo in a devcontainer.

## 8. Open Questions

1. **Client API base URL in production.** The Blazor WASM client needs the deployment URL of `enigma/server`. Options: bake `appsettings.Production.json` into the client at build via a build arg, or serve a runtime-resolvable config. **This spec identifies the need but does not prescribe the mechanism**; it must be resolved before the Client image is usable in production.

## 9. Verification (smoke tests post-implementation)

- `docker build -f Server/Dockerfile -t enigma/server .` exits 0.
- `docker build -f Client/Dockerfile -t enigma/client .` exits 0.
- `docker run --rm -p 8080:8080 enigma/server` (with MySQL env reachable) starts and `/swagger` or a known endpoint responds 200.
- `docker run --rm -p 8081:80 enigma/client` serves `index.html` at `/` and returns `index.html` for a SPA route like `/counter`.
- Opening the repo in the devcontainer brings up both services; `mysqladmin ping -h localhost -u enigma -penigma_dev_password` from the dev shell returns `mysqld is alive`.
