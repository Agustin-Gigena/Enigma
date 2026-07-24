# Container Ecosystem Design — Enigma

**Date:** 2026-07-24
**Status:** Draft (pending user review)
**Branch:** `development`
**Scope:** Build, packaging, and dev-environment containerization for the Enigma monorepo.

---

## 1. Goal

Define a container ecosystem for Enigma covering two concerns:

- **Production packaging** — ship `Server` and `Client` as two independent Docker images. `Shared` is not a runtime artifact; it is compiled into each image at build time.
- **Development environment** — the devcontainer runs the .NET SDK image and, on open, a single launcher script configures the dev environment (writes/exports the `.env` for `ASPNETCORE_ENVIRONMENT=Development` + MySQL connection vars), then brings up the full dev stack using Docker-in-Docker: a custom MySQL image from ghcr.io whose tag matches the nearest-ancestor branch, plus the **Server** (`dotnet watch`, port `8080`) and the **Client** (`dotnet watch`, ports `80`/`443`) — DB + API + Blazor client live and hot-reloading. No Compose is used.

Production deployment is **not orchestrated** by Compose. Each image is built and pushed to a registry; the operator decides where to run them (k8s, VM, etc.). Compose is not used anywhere in this design (dev or prod).

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
| Dev MySQL integration | Docker-in-Docker inside the devcontainer; pulls a **custom** MySQL image from ghcr.io whose tag matches the nearest-ancestor branch with an image | No Compose; MySQL runs as a sibling process inside the devcontainer. The custom image bundles MySQL 8.0 + seed/init scripts for the dev DB. Shared network namespace means `localhost:3306` works as before. |
| Dev MySQL image build | A single `.devcontainer/mysql/Dockerfile` extended from `mysql:8.0` with init/seed scripts; a GitHub Actions workflow builds and publishes it to ghcr.io. Published tag = current branch of the workflow (`development` or `production`). | One Dockerfile, one workflow; the published tag is derived from the branch the workflow runs on, so `enigma-dev-db:development` and `enigma-dev-db:production` always reflect each long-lived branch's latest seed. |
| Dev environment config | The launcher writes a workspace `.env` (gitignored via existing `**/.env`) with `ASPNETCORE_ENVIRONMENT=Development`, MySQL connection vars, and the resolved DB image repo/tag, then `export`s them so `dotnet watch` children inherit them | Single source of truth for dev env; no secret is committed; the `.env` is regenerated on each `postStartCommand`. |
| Dev Server/Client | `dotnet watch --project {Server,Client}` started by the launcher via `nohup`; ports resolved from each project's `launchSettings.json` (set to `8080` and `80`/`443` during implementation) | Full dev stack hot-reloading on devcontainer open; no separate TTY per service needed. |
| Production orchestration | None in this spec | Out of scope — the operator runs the two images where they choose. |

### Non-goals (YAGNI)

- No `Shared` Dockerfile (no runtime process).
- No Compose anywhere (neither dev nor prod). Development MySQL is launched inside the devcontainer via Docker-in-Docker, not Compose.
- No CI/CD pipeline, registry automation, or GitHub Actions **for the production Server/Client images**. (The dev MySQL image is published via its own workflow — see §5.2; that is the sole CI/CD artifact in scope.)
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

## 5. Development Environment (Devcontainer + Docker-in-Docker)

The devcontainer keeps `"image":` (no `dockerComposeFile`); it stays a single SDK image. On open, a single launcher script (`postStartCommand`) brings up the **full dev stack** inside the devcontainer: (1) MySQL as an additional container via Docker-in-Docker, pulling the custom package from ghcr.io whose tag matches the nearest-ancestor long-lived branch; (2) the **Server** in watch (`dotnet watch --project Server`) on port `8080`; and (3) the **Client** (Blazor WASM dev server) in watch (`dotnet watch --project Client`) on ports `80` (HTTP) and `443` (HTTPS). All three are hot-reloading. No Compose is used anywhere.

### 5.1 `.devcontainer/devcontainer.json`

Keeps `"image":`, adds the Docker-in-Docker feature, and wires `postStartCommand` to the launcher script. The existing features (`oh-my-pi`, `gh-cli`, `node`) and VS Code customizations are preserved verbatim.

```jsonc
{
  "name": ".NET 10 + MySQL (DinD)",
  "image": "mcr.microsoft.com/dotnet/sdk:10.0",
  "features": {
    "ghcr.io/devcontainers/features/node:2.1.0": {},
    "ghcr.io/iyaki/devcontainer-features/oh-my-pi:1": {},
    "ghcr.io/devcontainers-extra/features/gh-cli:1": {},
    "ghcr.io/devcontainers/features/docker-in-docker:2": { "dockerDashCompose": false }
  },
  "postStartCommand": "bash .devcontainer/start-dev.sh",
  "remoteEnv": {
    "DB_IMAGE_REPO": "ghcr.io/agustin-gigena/enigma-dev-db",
    "MYSQL_HOST": "localhost",
    "MYSQL_PORT": "3306",
    "MYSQL_DATABASE": "enigma_db",
    "MYSQL_USER": "enigma",
    "MYSQL_PASSWORD": "enigma_dev_password",
    "MYSQL_ROOT_PASSWORD": "root_password"
  },
  "customizations": {
    "vscode": {
      "settings": { "terminal.integrated.defaultProfile.linux": "bash" },
      "extensions": ["ms-dotnettools.csharp", "ms-dotnettools.csdevkit"]
    }
  }
}
```

The `docker-in-docker` feature spawns a nested Docker daemon inside the devcontainer (privileged by the feature) so the launcher script can `docker pull`/`docker run` the MySQL image. `localhost:3306` from the SDK points at that container. The same launcher session also starts `Server` and `Client` with `dotnet watch` (see §5.4) — ports for those are **not** hard-coded in the script but resolved from each project's `launchSettings.json` (the spec updates those files to `8080` for Server and `80`/`443` for Client during implementation; the script does not pass `--urls`).

### 5.2 `.devcontainer/mysql/Dockerfile` (dev DB image, single source)

A single Dockerfile extended from `mysql:8.0` with init/seed scripts. It is built and published by the workflow in §5.3, tagged per long-lived branch. Designed to be runnable as a plain `docker run` container (no compose, no orchestration) on port 3306.

```dockerfile
FROM mysql:8.0

ENV MYSQL_ROOT_PASSWORD=root_password \
    MYSQL_USER=enigma \
    MYSQL_PASSWORD=enigma_dev_password \
    MYSQL_DATABASE=enigma_db

# MySQL 8.0 auto-runs *.sh/*.sql under /docker-entrypoint-initdb.d/ on first start only.
COPY seed/ /docker-entrypoint-initdb.d/

EXPOSE 3306
```

Seed scripts live under `.devcontainer/mysql/seed/` (e.g. `00-schema.sql`, `10-seed-data.sql`); they define the dev DB schema and reference data the Server expects to find in development. (For `development`-tagged images these reflect in-progress schema; for `production`-tagged images they reflect production-shaped reference data. The Dockerfile is the same — only the seed content differs per branch.)

### 5.3 `.github/workflows/dev-db-image.yml` (publish the dev DB package)

A single workflow that, on push to `development` or `production`, builds the `.devcontainer/mysql/Dockerfile` and pushes it to ghcr.io tagged with the branch name. `GITHUB_TOKEN` with `packages: write` (default for the repo's own token) is sufficient; no extra credentials.

```yaml
name: Build & publish dev-db image

on:
  push:
    branches: [ development, production ]
    paths:
      - '.devcontainer/mysql/**'

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
    steps:
      - uses: actions/checkout@v4
      - uses: docker/setup-buildx-action@v3
      - uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: $\{\{ github.actor \}\}
          password: $\{\{ secrets.GITHUB_TOKEN \}\}
      - uses: docker/build-push-action@v5
        with:
          context: .devcontainer/mysql
          file: .devcontainer/mysql/Dockerfile
          push: true
          tags: |
            ghcr.io/${{ github.repository_owner }}/enigma-dev-db:${{ github.ref_name }}
            ghcr.io/${{ github.repository_owner }}/enigma-dev-db:${{ github.ref_name }}-${{ github.sha }}
          cache-from: type=gha
          cache-to: type=gha,mode=max
```

- **Tag layout:** `enigma-dev-db:<branch>` (rolling, latest) and `enigma-dev-db:<branch>-<sha>` (immutable, for traceability).
- **Trigger:** only changes under `.devcontainer/mysql/` build a new image — incidental edits elsewhere don't burn CI.
- The image is **publicly readable** by default for owner-scoped ghcr.io packages, but pull from the devcontainer does not require auth in practice because GitHub Codespaces / devcontainer tokens are scoped to the owner; if private, the devcontainer needs `remoteEnv` with a `GITHUB_TOKEN` having `packages: read` and `docker login ghcr.io` in `postStartCommand` before `docker pull` (not the default path — flagged in Open Questions §8).

### 5.4 `.devcontainer/start-dev.sh` (branch-aware launcher for DB + Server + Client)

Runs as `postStartCommand`. Brings up the full dev stack:

1. **Configure dev environment** — write a workspace `.env` (gitignored via `**/.env`) with the dev vars (`ASPNETCORE_ENVIRONMENT=Development`, MySQL connection vars, DB image repo/tag), and `export` them into the shell so the `dotnet watch` children inherit them. External tooling (the C# extension, manual `dotnet ef` runs, `docker build --env-file`) can also read that `.env`. No secret is committed; the file is regenerated on each `postStartCommand`.
2. **MySQL** — detect the active branch, walk first-parent ancestry to the nearest long-lived branch with an image (`development` or `production`), `docker pull` the matching custom package, `docker run` it on `localhost:3306` with a named volume per tag.
3. **Server** — `dotnet watch --project Server` once MySQL accepts connections. Binding (`http://localhost:8080`) comes from `Server/Properties/launchSettings.json`, not from `--urls`.
4. **Client** — `dotnet watch --project Client` in parallel. Binding (`http://localhost:80` and `https://localhost:443`) comes from `Client/Properties/launchSettings.json`. NOTE: because `443` requires privileged port binding, the devcontainer runs as root (default for the SDK image and the DinD feature); if the container were non-root, an `init`/`setcap` step would be needed — flagged in Open Questions §8.

All three run as background processes; logs go to `~/.devcontainer/dev-logs/` (one file per service) for `tail -f` debugging during a session.

```bash
#!/usr/bin/env bash
set -euo pipefail

REPO="${DB_IMAGE_REPO:-ghcr.io/agustin-gigena/enigma-dev-db}"
WORKSPACE="${WORKSPACE_FOLDER:-/workspaces/Enigma}"
LOG_DIR="$HOME/.devcontainer/dev-logs"
mkdir -p "$LOG_DIR"

cd "$WORKSPACE"

# --- Idempotency: tear down a previous session's processes first. ---
docker rm -f enigma-dev-db >/dev/null 2>&1 || true
pkill -f 'dotnet watch --project Server'  >/dev/null 2>&1 || true
pkill -f 'dotnet watch --project Client'  >/dev/null 2>&1 || true

# --- 1. Resolve the nearest long-lived ancestor that has a published image tag. ---
LONG_BRANCH=""
for ref in $(git rev-list --first-parent --simplify-merges HEAD); do
  # rev-list gives SHAs; check which long-lived branch contains it (production first).
  for cand in production development; do
    if git merge-base --is-ancestor "$ref" "origin/$cand" 2>/dev/null; then
      LONG_BRANCH="$cand"; break
    fi
  done
  [ -n "$LONG_BRANCH" ] && break
done
if [ -z "$LONG_BRANCH" ]; then
  echo "dev: no production/development ancestor for $(git rev-parse --abbrev-ref HEAD); defaulting to 'development'." >&2
  LONG_BRANCH=development
fi
IMAGE="$REPO:$LONG_BRANCH"
echo "dev: using $IMAGE for MySQL"

# --- 1b. Configure the dev environment: write a workspace .env and export its vars. ---
# The .env file lives at the repo root (already covered by **/.env in .gitignore — no secret is committed).
# `dotnet watch` processes below inherit these via the shell; external tooling (C# extension, `dotnet ef`)
# can also read the file via `--env-file` or a manual `source .env`.
ENV_FILE="$WORKSPACE/.env"
cat >"$ENV_FILE" <<EOF
# Generated by .devcontainer/start-dev.sh on $(date -u +%Y-%m-%dT%H:%M:%SZ). Do not commit.
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://localhost:8080

# Blazor WASM dev server (Client) listens via launchSettings.json; this var just hints the env.
ASPNETCORE_CLIENT_URLS=http://localhost:80;https://localhost:443

# MySQL connection (matches the docker run below and appsettings.json interpolation).
MYSQL_HOST=localhost
MYSQL_PORT=3306
MYSQL_DATABASE=$MYSQL_DATABASE
MYSQL_USER=$MYSQL_USER
MYSQL_PASSWORD=$MYSQL_PASSWORD
MYSQL_ROOT_PASSWORD=$MYSQL_ROOT_PASSWORD

# Dev DB image selection (for manual re-runs of the launcher).
DB_IMAGE_REPO=$REPO
DB_IMAGE_TAG=$LONG_BRANCH
EOF
set -a; . "$ENV_FILE"; set +a   # export vars into this shell so the dotnet watch children inherit them
echo "dev: wrote $ENV_FILE and exported $(set +u; env | grep -cE '^(ASPNETCORE|MYSQL|DB_IMAGE)' ) dev vars"
# --- 2. Wait for the DinD daemon (started by the docker-in-docker feature). ---
for _ in $(seq 1 30); do docker info >/dev/null 2>&1 && break; sleep 1; done

# --- 3. Pull + run MySQL; volume keyed by tag (per-branch DB state). ---
docker pull "$IMAGE"
docker run -d --rm \
  --name enigma-dev-db \
  -p 3306:3306 \
  -e MYSQL_ROOT_PASSWORD="$MYSQL_ROOT_PASSWORD" \
  -e MYSQL_USER="$MYSQL_USER" \
  -e MYSQL_PASSWORD="$MYSQL_PASSWORD" \
  -e MYSQL_DATABASE="$MYSQL_DATABASE" \
  -v "enigma-dev-db-${LONG_BRANCH}-data:/var/lib/mysql" \
  "$IMAGE"

# Wait for MySQL to accept connections before starting Server (it auto-migrates on boot).
for _ in $(seq 1 60); do
  docker exec enigma-dev-db mysqladmin ping -h localhost -u "$MYSQL_USER" -p"$MYSQL_PASSWORD" >/dev/null 2>&1 && break
  sleep 1
done
echo "dev: MySQL alive at localhost:3306 (tag $LONG_BRANCH)"

# --- 4. Start Server and Client in watch under nohup; ports via launchSettings.json. ---
nohup dotnet watch --project Server >"$LOG_DIR/server.log" 2>&1 &
echo "dev: Server watch started (see $LOG_DIR/server.log)"

# Small delay so the Server dev certificate / port reservation finishes first for the Client's HTTPS proxy.
sleep 2

nohup dotnet watch --project Client >"$LOG_DIR/client.log" 2>&1 &
echo "dev: Client watch started (see $LOG_DIR/client.log)"

echo "dev: full stack up — MySQL :3306 | Server :8080 (config via launchSettings.json) | Client :80/:443 (config via launchSettings.json)"
```

- **Fallback:** if neither ancestor is found (or local `origin/production`/`origin/development` refs are absent — e.g. a fresh shallow clone), defaults to `development` with a clear stderr message.
- **State per tag:** the named volume is `enigma-dev-db-<branch>-data`, so switching branches uses each branch's own DB state — no cross-contamination.
- **Ancestry algorithm:** walks back along first-parent commits from `HEAD`; for each commit, checks `git merge-base --is-ancestor <sha> origin/<long-branch>` against `production` first, then `development`. The first long-lived branch containing the commit wins. `feature/auth` (branched from `development`) → `development`; a hotfix branched from `production` → `production`.
- **Ports via `launchSettings.json`, not `--urls`:** the script does not pass `--urls` to `dotnet watch`; the bind addresses come from each project's `launchSettings.json`. The spec sets those to `8080` (Server) and `80`/`443` (Client) during implementation (see `Server/Properties/launchSettings.json` and `Client/Properties/launchSettings.json`). Editing those files later changes the dev bindings — single source of truth.
- **Env config:** the script writes the workspace `.env` and exports its vars before launching any child process, so `dotnet watch` (Server, Client) and external tools that `source .env` see the same dev config.
- **Hot reload:** `dotnet watch` recompiles on save for both Server and Client; the Blazor WASM dev server pushes updates via its WebSocket to the browser.
- **Idempotency:** `postStartCommand` runs on every (re)attach; the script kills any prior `dotnet watch` processes and removes any prior `enigma-dev-db` container before starting fresh.

### 5.5 Root `docker-compose.yml`

Deleted. Its standalone MySQL workflow (`docker-compose up -d` at the repo root) is replaced by §5.1–5.4 — MySQL now lives inside the devcontainer under DinD with the custom image.

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
| `.devcontainer/mysql/Dockerfile` | Produce the custom dev DB image (`mysql:8.0` + seed scripts) published to ghcr.io per branch. | `mysql:8.0` base, seed scripts under `.devcontainer/mysql/seed/`. | `docker build -f .devcontainer/mysql/Dockerfile .devcontainer/mysql` succeeds; container starts and `mysqladmin ping` returns alive. |
| `.devcontainer/start-dev.sh` | On devcontainer open: write `.env`, pull + run the matching MySQL tag under DinD, then start `Server` and `Client` under `dotnet watch`. | `DB_IMAGE_REPO`, `MYSQL_*` env (from `devcontainer.json` `remoteEnv`), `launchSettings.json` for Server/Client ports. | `postStartCommand` runs; `localhost:3306` pings alive; `localhost:8080` serves the API; `localhost:80` serves the Blazor app; `.env` exists at repo root. |

Each unit can be reasoned about and built run independently. The production Dockerfiles (`Server/`, `Client/`) are independent of the dev-artifacts (`mysql/Dockerfile`, `start-dev.sh`); the latter are only exercised when opening the repo in a devcontainer.

## 8. Open Questions

1. **Client API base URL in production.** The Blazor WASM client needs the deployment URL of `enigma/server`. Options: bake `appsettings.Production.json` into the client at build via a build arg, or serve a runtime-resolvable config. **This spec identifies the need but does not prescribe the mechanism**; it must be resolved before the Client image is usable in production.

## 9. Verification (smoke tests post-implementation)

- `docker build -f Server/Dockerfile -t enigma/server .` exits 0.
- `docker build -f Client/Dockerfile -t enigma/client .` exits 0.
- `docker run --rm -p 8080:8080 enigma/server` (with MySQL env reachable) starts and `/swagger` or a known endpoint responds 200.
- `docker run --rm -p 8081:80 enigma/client` serves `index.html` at `/` and returns `index.html` for a SPA route like `/counter`.
- `docker build -f .devcontainer/mysql/Dockerfile .devcontainer/mysql` exits 0 and produces a runnable MySQL 8.0 image with the seed applied.
- Opening the repo in the devcontainer triggers `start-dev.sh` once (via `postStartCommand`): it writes `.env` at the repo root (with `ASPNETCORE_ENVIRONMENT=Development` + MySQL connection vars), pulls and runs the matching `enigma-dev-db:<branch>` image under DinD, then starts `dotnet watch` for both `Server` and `Client`.
- In that dev session: `mysqladmin ping -h localhost -u enigma -penigma_dev_password` returns `mysqld is alive`; `curl -s http://localhost:8080` reaches the Server's default endpoint (e.g. OpenAPI/Swagger) with 200; `curl -s http://localhost:80` serves the Blazor `index.html`; `cat /workspaces/Enigma/.env` shows the expected dev vars and is gitignored (`git check-ignore .env` matches).
