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
err()   { echo "[$(_ts)] setup: ERROR — $*" >&2; }
step()  { echo ""; echo "[$(_ts)] setup: ─── $* ───"; }

RUNTIME_DIR="${XDG_RUNTIME_DIR:-/run/user/$(id -u)}"
SOCKET="${PODMAN_SOCKET:-$RUNTIME_DIR/podman/podman.sock}"
DEVCONTAINER_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# ═══ Fase CONTENEDOR: workload maui-android (Task 4) ═══════════════════════
# El mismo script atiende ambas caras del devcontainer:
#   - initializeCommand lo corre en el HOST (bind mounts, socket podman, .env).
#   - postCreateCommand lo corre DENTRO del contenedor como dev user:
#     workload maui-android y env del SDK para todas las shells (idempotente).
# El build APK (gradle/jarsigner) corre en CI y local; sin provisioning de
# emulador (scope rechazado por el controller — YAGNI).
if [ -f /run/.containerenv ] || [ -f /.dockerenv ]; then
    # Verificación, no instalación: el workload maui-android se HORNEA en la
    # imagen (root, build time — ver .devcontainer/Dockerfile). El SDK vive
    # root-owned en /usr/share/dotnet; instalarlo en runtime como dev user
    # muere con EACCES sugiriendo sudo (que contamina $HOME con archivos
    # root). Si falta acá, la imagen es vieja: Rebuild Container.
    step "Verifying maui-android workload (horneado en la imagen)"
    if ! dotnet workload list 2>/dev/null | grep -q maui-android; then
        err "Workload maui-android ausente — la imagen está vieja. Corré 'Dev Containers: Rebuild Container' (instalarlo en runtime requiere sudo y contamina \$HOME)"
        exit 1
    fi
    info "OK: workload maui-android"

    # Localizar el SDK Android: se provisiona en el PRIMER build net-android
    # como dev user (default $HOME/android-sdk — no en install-time del
    # workload). Si aún no existe, NO es un error: avisar y seguir (el env
    # exportado abajo cubre el resto).
    ANDROID_HOME="${ANDROID_HOME:-}"
    if [ -z "$ANDROID_HOME" ] || [ ! -d "$ANDROID_HOME" ]; then
        ANDROID_HOME="$HOME/android-sdk"
    fi
    if [ ! -d "$ANDROID_HOME" ]; then
        info "SDK Android aún no provisionado — se descargará en el primer build MAUI ($ANDROID_HOME)"
    fi
    JAVA_HOME="${JAVA_HOME:-}"
    if [ -z "$JAVA_HOME" ] || [ ! -d "$JAVA_HOME" ]; then
        java_bin="$(command -v java 2>/dev/null || true)"
        if [ -n "$java_bin" ]; then
            JAVA_HOME="$(dirname "$(dirname "$(readlink -f "$java_bin")")")"
        elif [ -d /usr/lib/jvm/java-17-openjdk-amd64 ]; then
            JAVA_HOME=/usr/lib/jvm/java-17-openjdk-amd64
        else
            warn "JAVA_HOME no localizado — el build MAUI (gradle/jarsigner) lo necesita"
            exit 1
        fi
    fi
    export ANDROID_HOME JAVA_HOME

    # Exportar el env del SDK Android para TODAS las shells (incluido lefthook
    # validate, que buildea el slnx con el proyecto MAUI): /etc/profile.d cubre
    # login shells; el hook en /etc/bash.bashrc cubre interactivas no-login
    # (terminales VS Code). Ambas escrituras idempotentes.
    step "Exporting Android SDK env for all shells"
    sudo tee /etc/profile.d/android-sdk.sh >/dev/null <<EOF
export ANDROID_HOME="$ANDROID_HOME"
export JAVA_HOME="$JAVA_HOME"
# jarsigner reserva ~25% del heap visible y aborta (exit 134) dentro del
# cgroup de 3G del devcontainer — cap explícito (evidencia: task-3-report).
export JAVA_TOOL_OPTIONS="-Xmx800m"
EOF
    sudo chmod 0755 /etc/profile.d/android-sdk.sh
    if ! grep -q 'android-sdk.sh' /etc/bash.bashrc 2>/dev/null; then
        printf '\n# Android SDK env (Enigma devcontainer)\n[ -f /etc/profile.d/android-sdk.sh ] && . /etc/profile.d/android-sdk.sh\n' \
            | sudo tee -a /etc/bash.bashrc >/dev/null
    fi
    info "OK: /etc/profile.d/android-sdk.sh (ANDROID_HOME=$ANDROID_HOME, JAVA_HOME=$JAVA_HOME)"

    info "setup complete (container)"
    exit 0
fi


# 1. Bind-mount sources must exist on the host or podman refuses to start the
#    container. Create them empty if missing.
step "Ensuring bind-mount sources exist"
mkdir -p "$HOME/.omp"
mkdir -p "$HOME/.config/gh"
mkdir -p "$HOME/.config/opencode"
info "OK: $HOME/.omp, $HOME/.config/gh, $HOME/.config/opencode"

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

# 4. Host identity for compose + Dockerfile: the container runs as the SAME
#    user (name, group, uid, gid) as the host. compose.json interpolates these
#    from .devcontainer/.env (build args, `user`, volume targets). Only the
#    generated keys are refreshed; any pre-existing lines (e.g. secrets used by
#    start-dev.sh) are preserved untouched. KVM_GID purga entradas viejas del
#    passthrough de dispositivos (scope rechazado).
step "Writing host identity to .devcontainer/.env"
env_file="$DEVCONTAINER_DIR/.env"
env_tmp="$(mktemp)"
{
    grep -vE '^ENIGMA_(USER_NAME|USER_GROUP|USER_UID|USER_GID|PODMAN_SOCKET|KVM_GID)=' "$env_file" 2>/dev/null || true
    printf 'ENIGMA_USER_NAME=%s\n'     "$(id -un)"
    printf 'ENIGMA_USER_GROUP=%s\n'    "$(id -gn)"
    printf 'ENIGMA_USER_UID=%s\n'      "$(id -u)"
    printf 'ENIGMA_USER_GID=%s\n'      "$(id -g)"
    printf 'ENIGMA_PODMAN_SOCKET=%s\n' "$SOCKET"
} > "$env_tmp"
mv "$env_tmp" "$env_file"
info "OK: host identity ($(id -un):$(id -u)) y socket en $env_file"

info "setup complete"
