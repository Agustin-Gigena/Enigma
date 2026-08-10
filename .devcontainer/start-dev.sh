#!/usr/bin/env bash
set -euo pipefail

REPO="${DB_IMAGE_REPO:-ghcr.io/agustin-gigena/enigma-dev-db}"
WORKSPACE="${WORKSPACE_FOLDER:-/workspaces/Enigma}"
LOG_DIR="$HOME/.devcontainer/dev-logs"
mkdir -p "$LOG_DIR"

cd "$WORKSPACE"

# --- Logging helpers ---
_ts() { date -u +%H:%M:%S; }
info()  { echo "[$(_ts)] dev: $*"; }
warn()  { echo "[$(_ts)] dev: WARN — $*" >&2; }
err()   { echo "[$(_ts)] dev: ERROR — $*" >&2; }
step()  { echo ""; echo "[$(_ts)] dev: ─── $* ───"; }

# --- Validate required env vars ---
step "Validating environment variables"
missing=()
for var in MYSQL_DATABASE MYSQL_USER MYSQL_PASSWORD MYSQL_ROOT_PASSWORD; do
  if [ -z "${!var:-}" ]; then
    missing+=("$var")
  fi
done
if [ ${#missing[@]} -gt 0 ]; then
  err "Missing required env vars: ${missing[*]}"
  exit 1
fi
info "All required env vars present"

# --- Podman client (remote, via host socket) ---
PODMAN_SOCKET="${PODMAN_SOCKET:-/run/user/1000/podman/podman.sock}"
podman() { command podman --remote --url "unix://${PODMAN_SOCKET}" "$@"; }

# --- PID tracking and cleanup trap ---
SERVER_PID=""
CLIENT_PID=""
cleanup() {
  step "Cleaning up"
  # Kill the whole process tree — dotnet watch only wraps the real app; the
  # child (Enigma.Server / Blazor DevServer) holds the port and survives a
  # watch-only kill, leaking orphans that block :8081/:80 on the next run.
  pkill -f 'Enigma\.Server' 2>/dev/null || true
  pkill -f 'Enigma\.Client' 2>/dev/null || true
  pkill -f 'webassembly\.devserver' 2>/dev/null || true
  pkill -f 'dotnet watch --project Server' 2>/dev/null || true
  pkill -f 'dotnet watch --project Client' 2>/dev/null || true
  sleep 1
  # Belt and suspenders: dotnet watch can swallow SIGTERM mid-restart.
  pkill -9 -f 'dotnet watch --project' 2>/dev/null || true
  info "Cleanup complete"
}
trap cleanup EXIT

# --- Tear down previous session ---
step "Tearing down previous session"
pkill -f 'Enigma\.Server' 2>/dev/null && info "Killed old Server app" || true
pkill -f 'Enigma\.Client' 2>/dev/null && info "Killed old Client app" || true
pkill -f 'webassembly\.devserver' 2>/dev/null && info "Killed old Client dev server" || true
pkill -f 'dotnet watch --project Server' 2>/dev/null && info "Killed old Server watch" || true
pkill -f 'dotnet watch --project Client' 2>/dev/null && info "Killed old Client watch" || true
sleep 1
  # Belt and suspenders: dotnet watch can swallow SIGTERM mid-restart.
  pkill -9 -f 'dotnet watch --project' 2>/dev/null && info "Force-killed remaining watches" || true

# --- 1. Resolve image tag from branch ancestry ---
step "Resolving image tag"
CURRENT_BRANCH="$(git rev-parse --abbrev-ref HEAD)"
info "Current branch: $CURRENT_BRANCH"

LONG_BRANCH=""
SEARCH_DEPTH=0
for ref in $(git rev-list --first-parent --simplify-merges HEAD); do
  SEARCH_DEPTH=$((SEARCH_DEPTH + 1))
  for cand in production development; do
    if git merge-base --is-ancestor "$ref" "origin/$cand" 2>/dev/null; then
      LONG_BRANCH="$cand"
      info "Found ancestor $ref (depth $SEARCH_DEPTH) is part of origin/$cand"
      break
    fi
  done
  [ -n "$LONG_BRANCH" ] && break
done

if [ -z "$LONG_BRANCH" ]; then
  warn "No production/development ancestor found after $SEARCH_DEPTH commits, defaulting to development"
  LONG_BRANCH=development
fi

IMAGE="${REPO}_${LONG_BRANCH}"
info "Image: $IMAGE"

# --- 2. Write .env files ---
step "Writing .env files"
# The devcontainer and the MySQL container share the podman network
# enigma-dev-net; the DB is reachable by its container name (aardvark DNS).
info "MySQL host: enigma-dev-db (network enigma-dev-net)"
cat > "$WORKSPACE/Server/.env" <<EOF
ASPNETCORE_ENVIRONMENT=Development
MYSQL_HOST=enigma-dev-db
MYSQL_PORT=3306
MYSQL_DATABASE=$MYSQL_DATABASE
MYSQL_USER=$MYSQL_USER
MYSQL_PASSWORD=$MYSQL_PASSWORD
MYSQL_ROOT_PASSWORD=$MYSQL_ROOT_PASSWORD
# VS Code injects HTTP_PORTS=8080 into the container env — neutralize it so
# Kestrel binds only what launchSettings pins (http://localhost:8081).
HTTP_PORTS=
HTTPS_PORTS=
EOF

set -a
. "$WORKSPACE/Server/.env"
set +a
info "Wrote Server/.env"

# --- 3. Verify Podman service is accessible ---
step "Checking Podman service"
if ! podman info --format '{{.Version.Version}}' >/dev/null 2>&1; then
  err "Podman service not accessible"
  podman info 2>&1 | head -10 | sed 's/^/  /' >&2
  err ""
  err "Ensure the host's podman socket is mounted at $PODMAN_SOCKET"
  err "Run on host: systemctl --user enable --now podman.socket"
  exit 1
fi
info "Podman: $(podman info --format '{{.Version.Version}}')"
# --- 4. Verify GHCR credentials (token present + read:packages scope) ---
step "Checking GHCR credentials"
GHCR_OWNER="$(printf '%s' "$IMAGE" | cut -d/ -f2)"
GHCR_PACKAGE="$(printf '%s' "$IMAGE" | cut -d/ -f3-)"

# 4a. Resolve a token: explicit env first, then the host's gh CLI login
#     (the host's ~/.config/gh is mounted into the container).
GH_TOKEN=""
if [ -n "${GITHUB_TOKEN:-}" ] || [ -n "${GHCR_TOKEN:-}" ]; then
  GH_TOKEN="${GHCR_TOKEN:-$GITHUB_TOKEN}"
  info "Using GHCR token from environment"
elif command -v gh >/dev/null 2>&1 && gh auth status >/dev/null 2>&1; then
  GH_TOKEN="$(gh auth token -h github.com)"
  info "Using GHCR token from host gh CLI login"
else
  err "No GitHub token available for GHCR pulls."
  err ""
  err "  Fix on the HOST (the container mounts ~/.config/gh from there):"
  err "    gh auth login -h github.com --web -s read:packages"
  err "  (or set GITHUB_TOKEN / GHCR_TOKEN in the devcontainer environment)."
  err ""
  exit 1
fi

# 4b. Validate the token against the GitHub API and confirm read:packages.
GH_API_HEADERS="$(gh api -i /user 2>/dev/null | tr -d '\r' || true)"
GH_API_CODE="$(printf '%s\n' "$GH_API_HEADERS" | awk 'NR==1 {match($0, /[0-9]{3}/); print substr($0, RSTART, RLENGTH)}')"
if [ "$GH_API_CODE" != "200" ]; then
  err "GitHub token rejected by the API (HTTP ${GH_API_CODE:-?}) — invalid or expired."
  err "  Fix on the HOST: gh auth login -h github.com --web -s read:packages"
  exit 1
fi
GH_SCOPES="$(printf '%s\n' "$GH_API_HEADERS" | awk -F': ' 'tolower($1)=="x-oauth-scopes" {print $2; exit}')"
if [ -n "$GH_SCOPES" ]; then
  case ",$(printf '%s' "$GH_SCOPES" | tr '[:upper:]' '[:lower:]' | tr -d ' ')," in
    *",read:packages,"*)
      info "Token OK — scopes: $GH_SCOPES"
      ;;
    *)
      err "Token lacks read:packages (scopes: ${GH_SCOPES:-none})."
      err "  Fix on the HOST: gh auth refresh -h github.com -s read:packages"
      err "  (or re-login: gh auth login -h github.com --web -s read:packages)"
      exit 1
      ;;
  esac
else
  # Fine-grained PAT: no x-oauth-scopes header — probe read access to the actual package.
  GH_PROBE_CODE="$(gh api -i "/users/$GHCR_OWNER/packages/container/$GHCR_PACKAGE/versions" 2>/dev/null | awk 'NR==1 {match($0, /[0-9]{3}/); print substr($0, RSTART, RLENGTH)}' || true)"
  case "$GH_PROBE_CODE" in
    200)
      info "Token OK — can read container package $GHCR_PACKAGE (fine-grained)"
      ;;
    403)
      err "Token cannot read package $GHCR_PACKAGE (HTTP 403) — missing Packages:Read."
      err "  Fix on the HOST: gh auth refresh -h github.com -s read:packages"
      err "  (or make the package public: gh api --method PATCH /users/$GHCR_OWNER/packages/container/$GHCR_PACKAGE -f visibility=public)"
      exit 1
      ;;
    404)
      err "Package $GHCR_PACKAGE not found (HTTP 404) — run the 'Sync DB to GHCR' workflow first."
      exit 1
      ;;
    *)
      err "Could not verify read access to $GHCR_PACKAGE (HTTP ${GH_PROBE_CODE:-?})."
      exit 1
      ;;
  esac
fi

# 4c. Authenticate podman so the pull below is authorized.
echo "$GH_TOKEN" | podman login ghcr.io -u "$GHCR_OWNER" --password-stdin >/dev/null
info "Logged into ghcr.io as $GHCR_OWNER"

# --- 5. Pull image (raw output to terminal) ---
step "Pulling database image"
podman pull "$IMAGE"
info "Pull complete"

# --- 6. Ensure network + run MySQL container ---
step "Ensuring podman network"
if ! podman network exists enigma-dev-net; then
  podman network create enigma-dev-net >/dev/null
  info "Created network enigma-dev-net"
else
  info "Network enigma-dev-net already exists"
fi

step "Starting MySQL container"
info "Container: enigma-dev-db (network enigma-dev-net, volume enigma-db-data)"

# The DB survives devcontainer rebuilds: never removed on teardown, kept if the
# image tag is unchanged, and backed by a named volume at the baked datadir.
RUNNING_IMAGE="$(podman inspect enigma-dev-db --format '{{.ImageName}}' 2>/dev/null || true)"
if [ -n "$RUNNING_IMAGE" ] && [ "$RUNNING_IMAGE" = "$IMAGE" ]; then
  info "Container already running with $IMAGE — keeping it"
else
  if [ -n "$RUNNING_IMAGE" ]; then
    warn "Image changed ($RUNNING_IMAGE → $IMAGE) — recreating (data volume persists)"
    podman rm -f enigma-dev-db >/dev/null 2>&1 || true
  fi
  podman run -d \
    --name enigma-dev-db \
    --network enigma-dev-net \
    -v enigma-db-data:/var/lib/mysql-baked \
    -e MYSQL_ROOT_PASSWORD="$MYSQL_ROOT_PASSWORD" \
    -e MYSQL_USER="$MYSQL_USER" \
    -e MYSQL_PASSWORD="$MYSQL_PASSWORD" \
    -e MYSQL_DATABASE="$MYSQL_DATABASE" \
    "$IMAGE"
  info "Container started"
fi

# --- 7. Wait for MySQL to be ready ---
step "Waiting for MySQL"
MYSQL_READY=false
for attempt in $(seq 1 60); do
  if ! podman inspect enigma-dev-db --format '{{.State.Running}}' 2>/dev/null | grep -q true; then
    err "Container died during startup"
    podman logs enigma-dev-db 2>&1 | tail -20 >&2
    exit 1
  fi

  if podman exec enigma-dev-db sh -c 'echo > /dev/tcp/localhost/3306' 2>/dev/null; then
    MYSQL_READY=true
    info "MySQL ready after ${attempt}s"
    break
  fi

  [ $((attempt % 10)) -eq 0 ] && info "Waiting... (${attempt}/60s)"
  sleep 1
done

if [ "$MYSQL_READY" != "true" ]; then
  err "MySQL not ready after 60s"
  podman logs --tail 30 enigma-dev-db 2>&1 | sed 's/^/  /' >&2
  exit 1
fi

# --- 8. Start Server and Client ---
step "Starting .NET Server"
nohup dotnet watch --project Server >"$LOG_DIR/server.log" 2>&1 &
SERVER_PID=$!
info "Server started (PID $SERVER_PID, log: $LOG_DIR/server.log)"

sleep 2

step "Starting .NET Client"
nohup dotnet watch --project Client >"$LOG_DIR/client.log" 2>&1 &
CLIENT_PID=$!
info "Client started (PID $CLIENT_PID, log: $LOG_DIR/client.log)"

# --- Summary ---
step "Dev stack ready"
info "MySQL  : enigma-dev-db:3306 (net enigma-dev-net, vol enigma-db-data)"
info "Server : http://localhost:8081 (container :18081)"
info "Client : http://localhost:80 (container :18080)"
info "Logs   : $LOG_DIR/"

# Keep script alive so trap fires on Ctrl+C
wait
