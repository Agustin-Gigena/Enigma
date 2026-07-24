# Container Ecosystem Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the container ecosystem: two production Dockerfiles (Server/Client), a custom dev MySQL image published via GitHub Actions, and a devcontainer that brings up the full dev stack (DB + Server + Client) on open via Docker-in-Docker.

**Architecture:** Multi-stage Docker builds with the repo root as context; `Shared/` is compiled into each image. Dev MySQL runs inside the devcontainer via Docker-in-Docker, pulled from a ghcr.io package built by a single GitHub Actions workflow. A `start-dev.sh` launcher writes the `.env`, starts MySQL, then runs `dotnet watch` for Server and Client with ports from `launchSettings.json`.

**Tech Stack:** .NET 10 SDK, ASP.NET Core, Blazor WebAssembly, Caddy, MySQL 8.0, Docker-in-Docker devcontainer feature, GitHub Actions, ghcr.io packages.

**Spec:** `docs/superpowers/specs/2026-07-24-container-ecosystem-design.md`

## Global Constraints

- Target framework: `net10.0` (all projects).
- Build context is always the repo root (`.`). Dockerfiles live in project folders and are invoked with `-f`.
- Spanish naming for entity properties (no English "fixing").
- `.devcontainer/devcontainer.json` currently uses `"image": "mcr.microsoft.com/dotnet/sdk:10.0"` — preserve this, add features, do not switch to `dockerComposeFile`.
- Existing features to preserve: `node:2.1.0`, `oh-my-pi:1`. Existing extension to preserve: `patcx.vscode-nuget-gallery`.
- MySQL dev credentials (dev only, never production): `enigma` / `enigma_dev_password`, DB `enigma_db`, root `root_password`.
- Server reads MySQL connection from env vars via `appsettings.json` interpolation: `MYSQL_HOST`, `MYSQL_PORT`, `MYSQL_DATABASE`, `MYSQL_USER`, `MYSQL_PASSWORD`.
- Ports: Server `8080`, Client `80` (HTTP) / `443` (HTTPS) — resolved from `launchSettings.json`, NOT from `--urls`.
- `.gitignore` already covers `**/.env` (root `.gitignore` line 17) and `*.log` (line 31).

## File Structure

### Files to modify

| File | Change |
|---|---|
| `Server/Properties/launchSettings.json` | Set `applicationUrl` to `http://localhost:8080` |
| `Client/Properties/launchSettings.json` | Set HTTP profile to `http://localhost:80`, HTTPS profile to `https://localhost:443;http://localhost:80` |
| `Server/Dockerfile` | Fill with multi-stage build (currently 0 bytes) |
| `Client/Dockerfile` | Fill with multi-stage build + Caddyfile (currently 0 bytes) |
| `.devcontainer/devcontainer.json` | Add `docker-in-docker` feature, `postStartCommand`, `remoteEnv` |
| `.devcontainer/docker-compose.yml` | Delete (absorbed into devcontainer DinD model) |

### Files to create

| File | Purpose |
|---|---|
| `.devcontainer/mysql/Dockerfile` | Custom MySQL 8.0 image with seed scripts |
| `.devcontainer/mysql/seed/00-schema.sql` | DB schema (placeholder until real migrations) |
| `.devcontainer/mysql/seed/10-seed-data.sql` | Dev seed data (placeholder) |
| `.devcontainer/start-dev.sh` | Launcher: writes `.env`, starts MySQL (DinD), starts Server + Client watch |
| `.github/workflows/dev-db-image.yml` | Builds and publishes MySQL image to ghcr.io per branch |

---

### Task 1: Update launchSettings.json for dev ports

**Files:**
- Modify: `Server/Properties/launchSettings.json`
- Modify: `Client/Properties/launchSettings.json`

**Interfaces:**
- Consumes: nothing (first task).
- Produces: Server binds `http://localhost:8080`; Client binds `http://localhost:80` (HTTP) and `https://localhost:443` (HTTPS). These are the ports the `start-dev.sh` script and production Dockerfiles expect.

- [ ] **Step 1: Update Server launchSettings.json**

Replace the `applicationUrl` value in the `http` profile.

```jsonc
// Server/Properties/launchSettings.json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "launchUrl": "todos",
      "applicationUrl": "http://localhost:8080",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

- [ ] **Step 2: Update Client launchSettings.json**

Replace both profiles to use ports `80` (HTTP) and `443` (HTTPS).

```jsonc
// Client/Properties/launchSettings.json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "inspectUri": "{wsProtocol}://{url.hostname}:{url.port}/_framework/debug/ws-proxy?browser={browserInspectUri}",
      "applicationUrl": "http://localhost:80",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "inspectUri": "{wsProtocol}://{url.hostname}:{url.port}/_framework/debug/ws-proxy?browser={browserInspectUri}",
      "applicationUrl": "https://localhost:443;http://localhost:80",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

- [ ] **Step 3: Verify JSON is valid**

Run: `cat Server/Properties/launchSettings.json | python3 -m json.tool && cat Client/Properties/launchSettings.json | python3 -m json.tool`
Expected: both parse without error.

- [ ] **Step 4: Commit**

```bash
git add Server/Properties/launchSettings.json Client/Properties/launchSettings.json
git commit -m "Set dev ports: Server :8080, Client :80/:443 in launchSettings"
```

---

### Task 2: Fill Server/Dockerfile

**Files:**
- Modify: `Server/Dockerfile` (currently 0 bytes)

**Interfaces:**
- Consumes: `Shared/Enigma.Shared.csproj` and `Server/Enigma.Server.csproj` exist with `<ProjectReference>` to Shared.
- Produces: image `enigma/server` exposing port `8080`, entry point `dotnet Enigma.Server.dll`.

- [ ] **Step 1: Write the Server/Dockerfile**

```dockerfile
# Server/Dockerfile
# Build context: repo root (docker build -f Server/Dockerfile .)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer caching (restore is cached unless csproj changes)
COPY Shared/Enigma.Shared.csproj Shared/
COPY Server/Enigma.Server.csproj Server/
RUN dotnet restore Server/Enigma.Server.csproj

# Copy source and publish
COPY Shared/ Shared/
COPY Server/ Server/
RUN dotnet publish Server/Enigma.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Enigma.Server.dll"]
```

- [ ] **Step 2: Verify the Dockerfile builds**

Run: `docker build -f Server/Dockerfile -t enigma/server-test .`
Expected: exits 0, image created.

- [ ] **Step 3: Commit**

```bash
git add Server/Dockerfile
git commit -m "feat: add Server multi-stage Dockerfile (aspnet:10.0, port 8080)"
```

---

### Task 3: Fill Client/Dockerfile

**Files:**
- Modify: `Client/Dockerfile` (currently 0 bytes)

**Interfaces:**
- Consumes: `Shared/Enigma.Shared.csproj` and `Client/Enigma.Client.csproj` exist with `<ProjectReference>` to Shared.
- Produces: image `enigma/client` exposing port `80`, serving Blazor WASM statics via Caddy.

- [ ] **Step 1: Write the Client/Dockerfile**

```dockerfile
# Client/Dockerfile
# Build context: repo root (docker build -f Client/Dockerfile .)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer caching
COPY Shared/Enigma.Shared.csproj Shared/
COPY Client/Enigma.Client.csproj Client/
RUN dotnet restore Client/Enigma.Client.csproj

# Copy source and publish (Blazor WASM produces statics in /app/wwwroot/)
COPY Shared/ Shared/
COPY Client/ Client/
RUN dotnet publish Client/Enigma.Client.csproj -c Release -o /app --no-restore

FROM caddy:alpine
COPY --from=build /app/wwwroot /srv
COPY <<'CADDYFILE' /etc/caddy/Caddyfile
:80 {
	root * /srv
	try_files {path} /index.html
	file_server
}
CADDYFILE
EXPOSE 80
```

- [ ] **Step 2: Verify the Dockerfile builds**

Run: `docker build -f Client/Dockerfile -t enigma/client-test .`
Expected: exits 0, image created.

- [ ] **Step 3: Commit**

```bash
git add Client/Dockerfile
git commit -m "feat: add Client multi-stage Dockerfile (caddy:alpine, port 80, SPA fallback)"
```

---

### Task 4: Create dev MySQL image (Dockerfile + seed scripts)

**Files:**
- Create: `.devcontainer/mysql/Dockerfile`
- Create: `.devcontainer/mysql/seed/00-schema.sql`
- Create: `.devcontainer/mysql/seed/10-seed-data.sql`

**Interfaces:**
- Consumes: nothing (standalone image).
- Produces: `.devcontainer/mysql/` directory with a buildable Dockerfile. The GitHub Actions workflow (Task 5) builds and pushes this. The `start-dev.sh` (Task 7) pulls and runs the resulting image.

- [ ] **Step 1: Create directory structure**

```bash
mkdir -p .devcontainer/mysql/seed
```

- [ ] **Step 2: Write .devcontainer/mysql/Dockerfile**

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

- [ ] **Step 3: Write seed placeholder .devcontainer/mysql/seed/00-schema.sql**

This file will grow as EF Core migrations define the schema. For now it creates a minimal marker table so the image build succeeds and the init scripts run without error.

```sql
-- Placeholder: replace with actual schema via EF Core migrations.
-- The Server's Program.cs runs auto-migration in development;
-- this file only ensures the DB is non-empty on first start.
CREATE TABLE IF NOT EXISTS _schema_version (
    id INT PRIMARY KEY AUTO_INCREMENT,
    description VARCHAR(255) NOT NULL,
    applied_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO _schema_version (description) VALUES ('initial placeholder');
```

- [ ] **Step 4: Write seed placeholder .devcontainer/mysql/seed/10-seed-data.sql**

```sql
-- Placeholder: add dev seed data here as domain entities are created.
-- Example: INSERT INTO usuarios (nombre, email) VALUES ('dev', 'dev@enigma.local');
SELECT 1;
```

- [ ] **Step 5: Verify the image builds**

Run: `docker build -f .devcontainer/mysql/Dockerfile -t enigma-dev-db-test .devcontainer/mysql`
Expected: exits 0.

- [ ] **Step 6: Commit**

```bash
git add .devcontainer/mysql/
git commit -m "feat: add custom dev MySQL image (mysql:8.0 + seed scripts)"
```

---

### Task 5: Create GitHub Actions workflow for dev DB image

**Files:**
- Create: `.github/workflows/dev-db-image.yml`

**Interfaces:**
- Consumes: `.devcontainer/mysql/Dockerfile` and `seed/` from Task 4.
- Produces: a GitHub Actions workflow that builds and pushes `ghcr.io/<owner>/enigma-dev-db:<branch>` on push to `development` or `production`.

- [ ] **Step 1: Create workflow directory**

```bash
mkdir -p .github/workflows
```

- [ ] **Step 2: Write .github/workflows/dev-db-image.yml**

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
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

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

- [ ] **Step 3: Verify YAML is valid**

Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/dev-db-image.yml'))"`
Expected: exits 0.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/dev-db-image.yml
git commit -m "ci: add workflow to build & publish dev DB image to ghcr.io"
```

---

### Task 6: Update .devcontainer/devcontainer.json

**Files:**
- Modify: `.devcontainer/devcontainer.json`

**Interfaces:**
- Consumes: nothing (independent of other tasks).
- Produces: devcontainer.json with `docker-in-docker` feature, `postStartCommand` pointing to `start-dev.sh`, and `remoteEnv` with MySQL + DB image vars.

- [ ] **Step 1: Write updated .devcontainer/devcontainer.json**

Preserve existing features (`node`, `oh-my-pi`) and extensions (`ms-dotnettools.csharp`, `ms-dotnettools.csdevkit`, `patcx.vscode-nuget-gallery`). Add `docker-in-docker`, `postStartCommand`, and `remoteEnv`.

```json
{
  "name": ".NET 10 + MySQL (DinD)",
  "image": "mcr.microsoft.com/dotnet/sdk:10.0",
  "features": {
    "ghcr.io/devcontainers/features/node:2.1.0": {},
    "ghcr.io/iyaki/devcontainer-features/oh-my-pi:1": {},
    "ghcr.io/devcontainers/features/docker-in-docker:2": {
      "dockerDashCompose": false
    }
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
      "settings": {
        "terminal.integrated.defaultProfile.linux": "bash"
      },
      "extensions": [
        "ms-dotnettools.csharp",
        "ms-dotnettools.csdevkit",
        "patcx.vscode-nuget-gallery"
      ]
    }
  }
}
```

- [ ] **Step 2: Verify JSON is valid**

Run: `python3 -m json.tool .devcontainer/devcontainer.json`
Expected: exits 0.

- [ ] **Step 3: Commit**

```bash
git add .devcontainer/devcontainer.json
git commit -m "feat(devcontainer): add DinD feature, postStartCommand, remoteEnv for MySQL"
```

---

### Task 7: Create .devcontainer/start-dev.sh launcher

**Files:**
- Create: `.devcontainer/start-dev.sh` (make executable)

**Interfaces:**
- Consumes: `remoteEnv` vars from `.devcontainer/devcontainer.json` (Task 6): `DB_IMAGE_REPO`, `MYSQL_*`. MySQL image from Task 4 (pushed by Task 5 workflow). `launchSettings.json` ports from Task 1.
- Produces: a running `enigma-dev-db` container (DinD), running `Server` watch on `:8080`, running `Client` watch on `:80/:443`, and a `.env` file at repo root.

- [ ] **Step 1: Write .devcontainer/start-dev.sh**

```bash
#!/usr/bin/env bash
set -euo pipefail

REPO="${DB_IMAGE_REPO:-ghcr.io/agustin-gigena/enigma-dev-db}"
WORKSPACE="${WORKSPACE_FOLDER:-/workspaces/Enigma}"
LOG_DIR="$HOME/.devcontainer/dev-logs"
mkdir -p "$LOG_DIR"

cd "$WORKSPACE"

# --- Idempotency: tear down previous session's processes first. ---
docker rm -f enigma-dev-db >/dev/null 2>&1 || true
pkill -f 'dotnet watch --project Server' >/dev/null 2>&1 || true
pkill -f 'dotnet watch --project Client' >/dev/null 2>&1 || true

# --- 1. Resolve the nearest long-lived ancestor that has a published image tag. ---
LONG_BRANCH=""
for ref in $(git rev-list --first-parent --simplify-merges HEAD); do
  for cand in production development; do
    if git merge-base --is-ancestor "$ref" "origin/$cand" 2>/dev/null; then
      LONG_BRANCH="$cand"
      break
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
set -a
. "$ENV_FILE"
set +a
echo "dev: wrote $ENV_FILE and exported dev vars"

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

# Wait for MySQL to accept connections before starting Server.
for _ in $(seq 1 60); do
  docker exec enigma-dev-db mysqladmin ping -h localhost -u "$MYSQL_USER" -p"$MYSQL_PASSWORD" >/dev/null 2>&1 && break
  sleep 1
done
echo "dev: MySQL alive at localhost:3306 (tag $LONG_BRANCH)"

# --- 4. Start Server and Client in watch under nohup; ports via launchSettings.json. ---
nohup dotnet watch --project Server >"$LOG_DIR/server.log" 2>&1 &
echo "dev: Server watch started (see $LOG_DIR/server.log)"

# Small delay so the Server dev certificate / port reservation finishes first.
sleep 2

nohup dotnet watch --project Client >"$LOG_DIR/client.log" 2>&1 &
echo "dev: Client watch started (see $LOG_DIR/client.log)"

echo "dev: full stack up — MySQL :3306 | Server :8080 | Client :80/:443"
```

- [ ] **Step 2: Make executable**

```bash
chmod +x .devcontainer/start-dev.sh
```

- [ ] **Step 3: Verify syntax**

Run: `bash -n .devcontainer/start-dev.sh`
Expected: exits 0 (syntax check only, no execution).

- [ ] **Step 4: Commit**

```bash
git add .devcontainer/start-dev.sh
git commit -m "feat(devcontainer): add start-dev.sh launcher (MySQL DinD + Server/Client watch)"
```

---

### Task 8: Delete .devcontainer/docker-compose.yml

**Files:**
- Delete: `.devcontainer/docker-compose.yml`

**Interfaces:**
- Consumes: nothing (cleanup).
- Produces: standalone MySQL compose is removed; devcontainer now manages MySQL via DinD (Task 6 + Task 7).

- [ ] **Step 1: Delete the file**

```bash
git rm .devcontainer/docker-compose.yml
```

- [ ] **Step 2: Commit**

```bash
git commit -m "chore: remove .devcontainer/docker-compose.yml (replaced by DinD launcher)"
```

---

### Task 9: Smoke test production Docker builds

**Files:**
- Consumes: `Server/Dockerfile` (Task 2), `Client/Dockerfile` (Task 3).

**Interfaces:**
- Produces: confidence that both production images build successfully and serve expected content.

- [ ] **Step 1: Build Server image**

```bash
docker build -f Server/Dockerfile -t enigma/server-smoke .
```
Expected: exits 0.

- [ ] **Step 2: Build Client image**

```bash
docker build -f Client/Dockerfile -t enigma/client-smoke .
```
Expected: exits 0.

- [ ] **Step 3: Verify Server image starts**

```bash
docker run --rm -d --name smoke-server -e MYSQL_HOST=host.docker.internal -p 8080:8080 enigma/server-smoke
sleep 5
curl -sf http://localhost:8080/ -o /dev/null -w "%{http_code}"
```
Expected: HTTP status 200 or 404 (server starts and responds; 404 is acceptable if no root route is mapped). If `host.docker.internal` is unavailable, the server may fail to connect to MySQL — but it should still start (EF throws on first request, not at boot). Confirm the process is running: `docker logs smoke-server`.

- [ ] **Step 4: Verify Client image serves index.html**

```bash
docker run --rm -d --name smoke-client -p 8081:80 enigma/client-smoke
sleep 2
curl -sf http://localhost:8081/ | head -5
```
Expected: HTML content containing `<title>` or `<div id="app">` (Blazor WASM entry point). Then:

```bash
docker rm -f smoke-server smoke-client 2>/dev/null
```

- [ ] **Step 5: No commit** (smoke test only, no file changes).

---

### Task 10: Smoke test dev DB image build

**Files:**
- Consumes: `.devcontainer/mysql/Dockerfile` and `seed/` (Task 4).

**Interfaces:**
- Produces: confidence that the dev MySQL image builds, starts, accepts connections, and runs init scripts.

- [ ] **Step 1: Build dev DB image locally**

```bash
docker build -f .devcontainer/mysql/Dockerfile -t enigma-dev-db-smoke .devcontainer/mysql
```
Expected: exits 0.

- [ ] **Step 2: Run the image and verify MySQL starts**

```bash
docker run --rm -d --name smoke-db -p 3307:3306 \
  -e MYSQL_ROOT_PASSWORD=root_password \
  -e MYSQL_USER=enigma \
  -e MYSQL_PASSWORD=enigma_dev_password \
  -e MYSQL_DATABASE=enigma_db \
  enigma-dev-db-smoke
sleep 10
docker exec smoke-db mysqladmin ping -h localhost -u enigma -penigma_dev_password
```
Expected: `mysqld is alive`.

- [ ] **Step 3: Verify seed scripts ran**

```bash
docker exec smoke-db mysql -u enigma -penigma_dev_password enigma_db -e "SELECT * FROM _schema_version;"
```
Expected: one row with `description = 'initial placeholder'`.

- [ ] **Step 4: Clean up**

```bash
docker rm -f smoke-db
```

- [ ] **Step 5: No commit** (smoke test only, no file changes).

---

### Task 11: Commit and push all remaining work

**Files:**
- Consumes: all prior tasks.

- [ ] **Step 1: Verify git status is clean**

Run: `git status`
Expected: nothing to commit (all prior tasks committed individually).

- [ ] **Step 2: Push to development**

```bash
git push origin development
```
