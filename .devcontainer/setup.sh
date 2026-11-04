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
DEVCONTAINER_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# ═══ Fase CONTENEDOR: Android SDK + emulador (Task 4) ══════════════════════
# El mismo script atiende ambas caras del devcontainer:
#   - initializeCommand lo corre en el HOST (bind mounts, socket podman, .env).
#   - postCreateCommand lo corre DENTRO del contenedor como dev user:
#     workload maui-android, env del SDK para todas las shells, licencias,
#     emulador + imagen de sistema y AVD `enigma-dev` (idempotente).
# Requisito del plan: el devcontainer debe poder EJECUTAR MAUI en el emulador,
# no solo compilarla.
if [ -f /run/.containerenv ] || [ -f /.dockerenv ]; then
    step "Ensuring maui-android workload"
    if ! dotnet workload list 2>/dev/null | grep -q maui-android; then
        dotnet workload install maui-android
    fi
    info "OK: workload maui-android"

    # Localizar el SDK Android (el workload lo instala en ~/android-sdk por
    # defecto; si no, buscar sdkmanager y derivar la raíz del SDK).
    ANDROID_HOME="${ANDROID_HOME:-}"
    if [ -z "$ANDROID_HOME" ] || [ ! -d "$ANDROID_HOME" ]; then
        ANDROID_HOME="$HOME/android-sdk"
    fi
    if [ ! -d "$ANDROID_HOME" ]; then
        found="$(find "$HOME" -name sdkmanager -path '*cmdline-tools*' 2>/dev/null | head -1 || true)"
        if [ -z "$found" ]; then
            warn "No se encontró el SDK Android tras instalar el workload"
            exit 1
        fi
        ANDROID_HOME="$(cd "$found/../../.." && pwd)"
    fi
    JAVA_HOME="${JAVA_HOME:-}"
    if [ -z "$JAVA_HOME" ] || [ ! -d "$JAVA_HOME" ]; then
        java_bin="$(command -v java 2>/dev/null || true)"
        if [ -n "$java_bin" ]; then
            JAVA_HOME="$(dirname "$(dirname "$(readlink -f "$java_bin")")")"
        elif [ -d /usr/lib/jvm/java-17-openjdk-amd64 ]; then
            JAVA_HOME=/usr/lib/jvm/java-17-openjdk-amd64
        else
            warn "JAVA_HOME no localizado — sdkmanager/emulator lo necesitan"
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
EOF
    sudo chmod 0755 /etc/profile.d/android-sdk.sh
    if ! grep -q 'android-sdk.sh' /etc/bash.bashrc 2>/dev/null; then
        printf '\n# Android SDK env (Enigma devcontainer)\n[ -f /etc/profile.d/android-sdk.sh ] && . /etc/profile.d/android-sdk.sh\n' \
            | sudo tee -a /etc/bash.bashrc >/dev/null
    fi
    info "OK: /etc/profile.d/android-sdk.sh (ANDROID_HOME=$ANDROID_HOME, JAVA_HOME=$JAVA_HOME)"

    step "Accepting Android SDK licenses"
    SDKMANAGER="$ANDROID_HOME/cmdline-tools/latest/bin/sdkmanager"
    if [ ! -x "$SDKMANAGER" ]; then
        SDKMANAGER="$(find "$ANDROID_HOME" -name sdkmanager -path '*cmdline-tools*' 2>/dev/null | head -1)"
    fi
    if [ -z "$SDKMANAGER" ]; then
        warn "sdkmanager no encontrado bajo $ANDROID_HOME"
        exit 1
    fi
    yes | "$SDKMANAGER" --licenses >/dev/null 2>&1 || true
    info "OK: licencias aceptadas"

    step "Ensuring Android emulator + system image"
    if ! "$SDKMANAGER" --list_installed 2>/dev/null | grep -q 'system-images;android-34;google_apis;x86_64'; then
        yes | "$SDKMANAGER" "emulator" "system-images;android-34;google_apis;x86_64" "platform-tools" >/dev/null
    fi
    info "OK: emulator + system-images;android-34;google_apis;x86_64 + platform-tools"

    step "Ensuring AVD enigma-dev"
    AVDMANAGER="$(dirname "$SDKMANAGER")/avdmanager"
    if ! "$AVDMANAGER" list avd 2>/dev/null | grep -q enigma-dev; then
        echo no | "$AVDMANAGER" create avd -n enigma-dev \
            -k "system-images;android-34;google_apis;x86_64" -d pixel_6
    fi
    info "OK: AVD enigma-dev"

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
#    start-dev.sh) are preserved untouched.
#    También se detecta KVM para el passthrough al emulador Android: sin
#    /dev/kvm el GID queda en 0 (grupo root, inofensivo) y el emulador x86_64
#    no podrá arrancar (exige aceleración de CPU por hardware) — ver README.
step "Writing host identity to .devcontainer/.env"
kvm_gid="0"
if [ -e /dev/kvm ]; then
    kvm_gid="$(stat -c %g /dev/kvm)"
    info "KVM disponible: /dev/kvm (gid $kvm_gid) → emulador acelerado"
else
    warn "Host sin /dev/kvm — el emulador x86_64 NO arrancará (exige aceleración de CPU)"
fi
env_file="$DEVCONTAINER_DIR/.env"
env_tmp="$(mktemp)"
{
    grep -vE '^ENIGMA_(USER_NAME|USER_GROUP|USER_UID|USER_GID|PODMAN_SOCKET|KVM_GID)=' "$env_file" 2>/dev/null || true
    printf 'ENIGMA_USER_NAME=%s\n'     "$(id -un)"
    printf 'ENIGMA_USER_GROUP=%s\n'    "$(id -gn)"
    printf 'ENIGMA_USER_UID=%s\n'      "$(id -u)"
    printf 'ENIGMA_USER_GID=%s\n'      "$(id -g)"
    printf 'ENIGMA_PODMAN_SOCKET=%s\n' "$SOCKET"
    printf 'ENIGMA_KVM_GID=%s\n'       "$kvm_gid"
} > "$env_tmp"
mv "$env_tmp" "$env_file"
info "OK: host identity ($(id -un):$(id -u)), socket y KVM gid=$kvm_gid en $env_file"

info "setup complete"
