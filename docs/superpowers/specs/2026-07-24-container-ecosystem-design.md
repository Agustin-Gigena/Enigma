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
- `Server/appsettings.json` historically carried `${MYSQL_*}` placeholders in `ConnectionStrings:DefaultConnection` (docker-compose/EnvSubst syntax, NOT expanded by .NET `IConfiguration`). The Server now assembles the connection string from `MYSQL_*` env vars in `Program.cs`: `MYSQL_HOST`, `MYSQL_PORT`, `MYSQL_DATABASE`, `MYSQL_USER`, `MYSQL_PASSWORD` (with dev defaults). `appsettings.json` retains the placeholder only as a documentation trail; it is not read at runtime.
- Branching: `development` (default) and `production`. `main` removed.

## 3. Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Client runtime container | Separate image, static files served by **Caddy** | Decouple front/back scaling; Caddy gives minimal config for a SPA fallback. |
| Shared packaging | Compiled into each image (multi-stage build); no `Shared` image | `Shared` is a compile-time dependency, not a runtime process. |
| Docker build context | Repo root (`.`), one Dockerfile per project folder (`-f Server/Dockerfile .`) | Shared is reachable from the context without duplicating copy logic; Dockerfiles live beside their project. |
| Dev MySQL integration | Docker-in-Docker inside the devcontainer; pulls a **custom** MySQL image from ghcr.io whose tag matches the nearest-ancestor branch with an image | No Compose; MySQL runs as a sibling process inside the devcontainer. The custom image bundles MySQL 8.0 + the schema produced by EF Core migrations applied at build time. Shared network namespace means `localhost:3306` works as before. There is **no dev Dockerfile for MySQL** — the image is produced by the sync-db workflow (§5.3) which pulls `mysql:8` (or the prior `:latest`), applies `dotnet ef database update`, and `docker commit`s the result. |
| Dev MySQL image build | GitHub Actions `workflow_dispatch` (`.github/workflows/dev-db-image.yml`) invokes `.github/actions/sync-db` (composite action). It pulls `mysql:8` for bootstrap (or `<prefix>:latest` for incremental), applies EF Core migrations via `dotnet ef database update`, `docker commit`s the running container as `Version<timestamp>` and `:latest`, and pushes both tags to GHCR. One package per long-lived branch: `enigma-dev-db_<branch>` — the branch lives in the **package name**, not the tag. | No Dockerfile for the dev DB. The image IS the schema snapshot: it carries MySQL state on the custom datadir `/var/lib/mysql-baked` (bypassing the `VOLUME` so `docker commit` captures it), with a baked `.cnf` so derived containers default to that datadir. The operator (or the dev launcher) just `docker pull`s the matching branch package — no `docker build` anywhere in the dev path. |
| Dev environment config | The launcher writes a workspace `.env` (gitignored via existing `**/.env`) with `ASPNETCORE_ENVIRONMENT=Development`, MySQL connection vars, and the resolved DB image repo/tag, then `export`s them so `dotnet watch` children inherit them | Single source of truth for dev env; no secret is committed; the `.env` is regenerated on each `postStartCommand`. |
| Dev Server/Client | `dotnet watch --project {Server,Client}` started by the launcher via `nohup`; ports resolved from each project's `launchSettings.json` (set to `8080` and `80`/`443` during implementation) | Full dev stack hot-reloading on devcontainer open; no separate TTY per service needed. |
| Production orchestration | None in this spec | Out of scope — the operator runs the two images where they choose. |

### Non-goals (YAGNI)

- No `Shared` Dockerfile (no runtime process).
- No Compose anywhere (neither dev nor prod). Development MySQL is launched inside the devcontainer via Docker-in-Docker, not Compose.
- No CI/CD pipeline, registry automation, or GitHub Actions **for the production Server/Client images**. (The dev MySQL image is published via its own workflow — see §5.3; that is the sole CI/CD artifact in scope.)
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
- **Runtime config** — reads the MySQL connection from environment variables assembled in `Program.cs` (no secrets baked in). NOTE: `appsettings.json` previously carried `${MYSQL_*}` placeholders, but .NET `IConfiguration` does NOT expand `${VAR}` (that is docker-compose/EnvSubst syntax), so the connection string is now built in `Program.cs` from `Environment.GetEnvironmentVariable("MYSQL_HOST"|"MYSQL_PORT"|"MYSQL_DATABASE"|"MYSQL_USER"|"MYSQL_PASSWORD")` with dev defaults (`localhost`/`3306`/`enigma_db`/`enigma`/`enigma_dev_password`). `ServerVersion.AutoDetect` was replaced by `new MySqlServerVersion(new Version(8, 0, 42))` so design-time EF tooling (`dotnet ef migrations add`/`database update`) and server startup no longer open a TCP round-trip. Expected env at runtime: `MYSQL_HOST`, `MYSQL_PORT`, `MYSQL_DATABASE`, `MYSQL_USER`, `MYSQL_PASSWORD`. A `Server/Data/EnigmaDbContextFactory.cs` (IDesignTimeDbContextFactory) mirrors this connection assembly for design-time EF.
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

The `docker-in-docker` feature spawns a nested Docker daemon inside the devcontainer (privileged by the feature) so the launcher script can `docker pull`/`docker run` the MySQL image. `localhost:3306` from the SDK points at that container. The same launcher session also starts `Server` and `Client` with `dotnet watch` (see §5.4) — ports for those are **not** hard-coded in the script but resolved from each project's `launchSettings.json` (the spec updates those files to `8080` for Server and `80`/`443` for Client during implementation; the script does not pass `--urls`). Note that `DB_IMAGE_REPO` above is just the prefix; the launcher in §5.4 appends `_<branch>` to form one package per long-lived branch (e.g. `ghcr.io/agustin-gigena/enigma-dev-db_development`) — see §5.3 for the package/tag layout.

### 5.2 Dev DB image — `docker commit` snapshot, no Dockerfile

The dev MySQL image is **never built from a Dockerfile**. It is produced by the sync-db composite action (`§5.3`), which starts `mysql:8` (bootstrap) or the prior `<prefix>:latest` (incremental) as a plain container with `--datadir=/var/lib/mysql-baked` (bypassing the `mysql:8` `VOLUME /var/lib/mysql` declaration so `docker commit` can capture the data), applies EF Core migrations via `dotnet ef database update`, then `docker commit`s the stopped container and pushes it to GHCR.

There is **no `.devcontainer/mysql/` directory, no Dockerfile, no seed scripts**. The schema IS the image: the committed MySQL datadir lives on the image layer at `/var/lib/mysql-baked` (with a baked `/etc/mysql/conf.d/baked-datadir.cnf` redirecting `datadir=/var/lib/mysql-baked` so derived containers default to the baked state). The operator / dev launcher just `docker pull`s the matching branch package (§5.3) and `docker run`s it — no `docker build` anywhere in the dev path.

A bootstrap seed (the only path to materialise the first image of a branch package) is taken from the `seed-sql` workflow input (an S3 URI or a path in the repo), imported via `mysql < seed.sql` before the first migration run. After bootstrap, every subsequent run is incremental: pull `:latest`, migrate, commit a new `Version<timestamp>` tag over `:latest`.

### 5.3 `.github/workflows/dev-db-image.yml` + `.github/actions/sync-db` (publish the dev DB package)

A `workflow_dispatch` workflow with three inputs:

- `seed-sql` — S3 URI or local path. Empty = incremental mode (pull previous `:latest`, migrate, commit). Non-empty = bootstrap mode (pull `mysql:8`, import seed, migrate, commit) — used to materialise the first image of a branch package.
- `target-branch` — suffix of the GHCR package to write to (`development` or `production`). Empty = use the dispatch branch. Lets you run the workflow from a feature branch to bootstrap the `production` package (or vice-versa) without switching branches.
- `force` — boolean; commit & push even if no new migration was applied (otherwise the action skips when the latest migration timestamp equals the latest published `Version*` tag — see Skip logic below).

The workflow runs on `ubuntu-latest`, computes `GHCR_PREFIX="ghcr.io/${OWNER}/enigma-dev-db_${PACKAGE_BRANCH}"` (one package per long-lived branch — the branch lives in the **package name**, not the tag), and delegates all the work to `./.github/actions/sync-db` (a composite action that lives in the repo and can be reused by other workflows).

```yaml
name: Sync DB to GHCR

on:
  workflow_dispatch:
    inputs:
      seed-sql:
        description: "Seed for bootstrap (s3://bucket/key.sql or local path). Empty = incremental."
        required: false
        default: ""
      target-branch:
        description: "GHCR package suffix (production/development). Empty = dispatch branch."
        required: false
        default: ""
      force:
        description: "Force rebuild even if no new migrations"
        required: false
        type: boolean
        default: false

permissions:
  contents: read
  packages: write

jobs:
  sync:
    runs-on: ubuntu-latest
    timeout-minutes: 20
    steps:
      - uses: actions/checkout@v4
      - name: Compute GHCR prefix
        id: ghcr
        shell: bash
        run: |
          OWNER=$(echo "${{ github.repository_owner }}" | tr '[:upper:]' '[:lower:]')
          DISPATCH_BRANCH="${{ github.ref_name }}"
          TARGET_BRANCH="${{ inputs.target-branch }}"
          PACKAGE_BRANCH="${TARGET_BRANCH:-${DISPATCH_BRANCH}}"
          PREFIX="ghcr.io/${OWNER}/enigma-dev-db_${PACKAGE_BRANCH}"
          echo "prefix=${PREFIX}" >> "$GITHUB_OUTPUT"
          echo "package-branch=${PACKAGE_BRANCH}" >> "$GITHUB_OUTPUT"
      - name: Sync DB to GHCR
        id: sync
        uses: ./.github/actions/sync-db
        env:
          GHCR_PREFIX: ${{ steps.ghcr.outputs.prefix }}
          SEED_SQL: ${{ inputs.seed-sql }}
          FORCE: ${{ inputs.force }}
          MYSQL_ROOT_PASSWORD: ${{ secrets.MYSQL_ROOT_PASSWORD || 'root_password' }}
          MYSQL_DATABASE: enigma_db
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          AWS_ACCESS_KEY_ID: ${{ secrets.AWS_ACCESS_KEY_ID }}
          AWS_SECRET_ACCESS_KEY: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
```

The **composite action** (`./.github/actions/sync-db/action.yml`):

1. `docker login ghcr.io` with `GITHUB_TOKEN`.
2. Calls `.github/actions/setup-dotnet` (.NET SDK 10 + `dotnet-ef 10.0.0` + `dotnet restore` + `dotnet build Server`).
3. Resolves the base image (`mysql:8` for bootstrap; `<prefix>:latest` for incremental — errors if the package has no `:latest` and `seed-sql` is empty, telling the operator to bootstrap first).
4. `docker run -d --name builder <BASE_IMAGE> --datadir=/var/lib/mysql-baked`, waits for `mysqladmin ping` (up to 60s).
5. Bootstrap path: `docker exec mysql < seed.sql` into a fresh `enigma_db` database.
6. `dotnet dotnet-ef database update --project Server --no-build` against the container IP, with `MYSQL_*` + `ASPNETCORE_ENVIRONMENT=Development` set so `Program.cs` + `EnigmaDbContextFactory` assemble the right connection string.
7. `V_NEW=Version<timestamp>` — derived NOT from `dotnet ef migrations list --json` (EF Core 10 emits warnings on stderr that break naive parses) but from the **filename** of the latest migration in `Server/Data/Migrations/<timestamp>_<name>.cs` (sort -r | head -1, exclude `.Designer.cs` and `Snapshot.cs`).
8. `V_BASE` — latest `Version*` tag currently published in the GHCR package, resolved via `gh api <ORG_OR_USER>/<owner>/packages/container/<package>/versions --jq`. Owner is user-vs-org aware (`/orgs/...` if `gh api /orgs/<owner>` resolves, else `/users/...`) so the API call works for repos like `agustin-gigena/Enigma` that live under a personal account.
9. **Skip logic** — if neither bootstrap nor `force` and `V_NEW == V_BASE`, no migration was added since the last commit; the action tears down the builder container and exits `pushed=false`, `version=$V_NEW` (no commit, no push). Otherwise:
10. `docker exec bash -c 'printf "[mysqld]\ndatadir=/var/lib/mysql-baked\n" > /etc/mysql/conf.d/baked-datadir.cnf'`, `docker stop -t 60 builder` (graceful flush), `docker commit builder <prefix>:$V_NEW`, `docker tag <prefix>:$V_NEW <prefix>:latest`, `docker rm builder`.
11. Push both tags to GHCR.
12. **Cleanup** — `gh api .../versions`, list `Version*`-tagged package versions (those without `latest`), sort descending, delete past the 10 newest (keep-latest-10 retention, `continue-on-error: true`).

- **Package / tag layout:** one package per long-lived branch, named `enigma-dev-db_<branch>` (e.g. `ghcr.io/agustin-gigena/enigma-dev-db_development`). Inside each package, there is one movable `:latest` plus one immutable `Version<timestamp>` per commit; the dev launcher pulls `:latest`.
- **Trigger:** `workflow_dispatch` only — no push triggers. The operator (or the dev launcher's manual re-sync) reruns the workflow when a new migration is committed. Incidental edits elsewhere don't burn CI.
- **Bootstrap:** the FIRST image of a branch package requires `seed-sql`. After that, the workflow is incremental. Migrations committed after the last `Version*` become a new commit; the action skips silently when no migration was added.
- **Auth to read GHCR:** publicly-readable by default for owner-scoped ghcr.io packages; pull from the devcontainer does not require auth in practice because GitHub Codespaces / devcontainer tokens are scoped to the owner. If private, the devcontainer needs a `GITHUB_TOKEN` with `packages: read` and `docker login ghcr.io` in `postStartCommand` before `docker pull` (not the default path — flagged in Open Questions §8).


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

# MySQL connection (matches the docker run below and Program.cs env-var construction).
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
  -v "enigma-dev-db-${LONG_BRANCH}-data:/var/lib/mysql-baked" \
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
| `.github/actions/sync-db` + `.github/workflows/dev-db-image.yml` | Produce the dev DB image as a `docker commit` snapshot (`mysql:8` + EF migrations applied, datadir `/var/lib/mysql-baked`), published to one GHCR package per long-lived branch with `Version<timestamp>` + `:latest` tags. | `mysql:8` base (bootstrap) or prior `<prefix>:latest` (incremental), `Server/Data/Migrations/`, `.github/actions/setup-dotnet`, `GITHUB_TOKEN` (GHCR), `MYSQL_ROOT_PASSWORD`. | `gh workflow run "Sync DB to GHCR" -f seed-sql=...` ends green; a new `Version*` tag appears in `ghcr.io/<owner>/enigma-dev-db_<branch>`; derived container `mysqladmin ping` returns alive. |
| `.devcontainer/start-dev.sh` | On devcontainer open: write `.env`, pull + run the matching MySQL tag under DinD, then start `Server` and `Client` under `dotnet watch`. | `DB_IMAGE_REPO`, `MYSQL_*` env (from `devcontainer.json` `remoteEnv`), `launchSettings.json` for Server/Client ports. | `postStartCommand` runs; `localhost:3306` pings alive; `localhost:8080` serves the API; `localhost:80` serves the Blazor app; `.env` exists at repo root. |

Each unit can be reasoned about and built run independently. The production Dockerfiles (`Server/`, `Client/`) are independent of the dev-artifacts (`.github/actions/sync-db`, `.github/actions/setup-dotnet`, `.github/workflows/dev-db-image.yml`, `.devcontainer/start-dev.sh`); the latter are exercised on devcontainer open and on workflow_dispatch, not during `docker build` of the production images.

## 8. Open Questions

1. **Client API base URL in production.** The Blazor WASM client needs the deployment URL of `enigma/server`. Options: bake `appsettings.Production.json` into the client at build via a build arg, or serve a runtime-resolvable config. **This spec identifies the need but does not prescribe the mechanism**; it must be resolved before the Client image is usable in production.

## 9. Verification (smoke tests post-implementation)

- `docker build -f Server/Dockerfile -t enigma/server .` exits 0.
- `docker build -f Client/Dockerfile -t enigma/client .` exits 0.
- `docker run --rm -p 8080:8080 enigma/server` (with MySQL env reachable) starts and `/swagger` or a known endpoint responds 200.
- `docker run --rm -p 8081:80 enigma/client` serves `index.html` at `/` and returns `index.html` for a SPA route like `/counter`.
- A `workflow_dispatch` run of `Sync DB to GHCR` (with `seed-sql` set for the first run) ends green and posts a new `Version<timestamp>` tag to `ghcr.io/<owner>/enigma-dev-db_<branch>`; the `:latest` tag moves to it.
- Opening the repo in the devcontainer triggers `start-dev.sh` once (via `postStartCommand`): it writes `.env` at the repo root (with `ASPNETCORE_ENVIRONMENT=Development` + MySQL connection vars), pulls and runs the matching `enigma-dev-db_<branch>:latest` image under DinD (with a named volume at `/var/lib/mysql-baked` so per-branch dev state persists), then starts `dotnet watch` for both `Server` and `Client`.
- In that dev session: `mysqladmin ping -h localhost -u enigma -penigma_dev_password` returns `mysqld is alive`; `curl -s http://localhost:8080` reaches the Server's default endpoint (e.g. OpenAPI/Swagger) with 200; `curl -s http://localhost:80` serves the Blazor `index.html`; `cat /workspaces/Enigma/.env` shows the expected dev vars and is gitignored (`git check-ignore .env` matches).
