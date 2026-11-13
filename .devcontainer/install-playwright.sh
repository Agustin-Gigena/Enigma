#!/usr/bin/env bash
# Instala el navegador Chromium que usan los tests E2E (Microsoft.Playwright).
#
# Lo invoca postCreateCommand (devcontainer.json). Usa el CLI oficial de npm
# (npx playwright@<versión>), con la versión tomada del PackageVersion de
# Directory.Packages.props (CPM): Microsoft.Playwright y el paquete npm
# playwright se versionan juntos y descargan exactamente los mismos builds a
# ~/.cache/ms-playwright. Es idempotente: si ya está instalado, es un no-op.
set -euo pipefail

WORKSPACE="${WORKSPACE_FOLDER:-/workspaces/Enigma}"
PKG_PROPS="$WORKSPACE/Directory.Packages.props"

_ts()  { date -u +%H:%M:%S; }
info() { echo "[$(_ts)] playwright: $*"; }

command -v npx >/dev/null 2>&1 || {
  echo "playwright: ERROR — npx no está disponible (falta la feature node del devcontainer)" >&2
  exit 1
}

# Única fuente de verdad de la versión: Directory.Packages.props (CPM).
VERSION="$(grep -oP '(?<=Include="Microsoft.Playwright" Version=")[^"]+' "$PKG_PROPS")"
if [ -z "$VERSION" ]; then
  echo "playwright: ERROR — no se encontró Microsoft.Playwright en $PKG_PROPS" >&2
  exit 1
fi

info "Instalando Chromium (npm playwright@$VERSION)"
# SIN --with-deps: las libs de sistema ya van en la imagen (.devcontainer/
# Dockerfile instala libnss3/libatk/etc. para Chrome headless). El paso apt
# de --with-deps se colgó 12+ min en este entorno (verificado 2026-11-13).
npx -y "playwright@$VERSION" install chromium

info "OK: Chromium listo en ~/.cache/ms-playwright"
