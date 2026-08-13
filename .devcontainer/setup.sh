#!/usr/bin/env bash
# Host-side preparation, runs via `initializeCommand` BEFORE the container starts.
# Portable across native Linux and Windows-via-WSL: both execute initializeCommand
# in bash. Nothing here is container-specific.
#
# It guarantees the bind-mount sources exist (even if empty) so `podman run` does
# not fail with `statfs: no such file or directory`, and it prepares the rootless
# podman socket + shared network the devcontainer and the DB container rely on.
set -euo pipefail

_ts()   { date -u +%H:%M:%S; }
info()  { echo "[$(_ts)] setup: $*"; }
warn()  { echo "[$(_ts)] setup: WARN — $*" >&2; }
step()  { echo ""; echo "[$(_ts)] setup: ─── $* ───"; }

RUNTIME_DIR="${XDG_RUNTIME_DIR:-/run/user/$(id -u)}"
SOCKET="${PODMAN_SOCKET:-$RUNTIME_DIR/podman/podman.sock}"

# 1. Bind-mount sources must exist on the host or podman refuses to start the
#    container. Create them empty if missing.
step "Ensuring bind-mount sources exist"
mkdir -p "$HOME/.omp"
mkdir -p "$HOME/.config/gh"
info "OK: $HOME/.omp, $HOME/.config/gh"

# 2. Rootless podman socket: bring it up if down, widen its perms so the
#    container (different uid under the userns mapping) can reach it.
step "Ensuring podman rootless socket"
if command -v systemctl >/dev/null 2>&1; then
    systemctl --user start podman.socket 2>/dev/null || warn "systemctl --user start podman.socket failed (ignored)"
fi
if [ -S "$SOCKET" ]; then
    chmod 666 "$SOCKET" 2>/dev/null || warn "chmod $SOCKET failed"
    info "OK: socket $SOCKET"
else
    warn "Podman socket not found at $SOCKET."
    warn "  Start it on the host: systemctl --user enable --now podman.socket"
    warn "  The container will not be able to drive the host daemon until it exists."
fi

# 3. Shared podman network used by both the devcontainer and the MySQL container.
step "Ensuring podman network enigma-dev-net"
if ! podman network exists enigma-dev-net 2>/dev/null; then
    podman network create --dns 1.1.1.1 --dns 9.9.9.9 enigma-dev-net >/dev/null
    info "Created network enigma-dev-net"
else
    # Older instances may predate the custom DNS — add the servers if absent.
    if ! podman network inspect enigma-dev-net --format '{{.NetworkDNSServers}}' 2>/dev/null | grep -q 1.1.1.1; then
        podman network update enigma-dev-net --dns-add 1.1.1.1 --dns-add 9.9.9.9 2>/dev/null || true
    fi
    info "OK: network enigma-dev-net"
fi

info "setup complete"
