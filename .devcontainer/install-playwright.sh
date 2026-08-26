#!/usr/bin/env bash
# Instala el navegador Chromium que usan los tests E2E (Microsoft.Playwright).
#
# Lo invoca postCreateCommand (devcontainer.json). Usa el CLI oficial de npm
# (npx playwright@<versión>), con la versión tomada del PackageReference de
# Tests/Enigma.Test.csproj: Microsoft.Playwright y el paquete npm playwright se
# versionan juntos y descargan exactamente los mismos builds a
# ~/.cache/ms-playwright. Es idempotente: si ya está instalado, es un no-op.
set -euo pipefail

WORKSPACE="${WORKSPACE_FOLDER:-/workspaces/Enigma}"
CSPROJ="$WORKSPACE/Tests/Enigma.Test.csproj"

_ts()  { date -u +%H:%M:%S; }
info() { echo "[$(_ts)] playwright: $*"; }

command -v npx >/dev/null 2>&1 || {
  echo "playwright: ERROR — npx no está disponible (falta la feature node del devcontainer)" >&2
  exit 1
}

# Única fuente de verdad de la versión: el csproj.
VERSION="$(grep -oP '(?<=Include="Microsoft.Playwright" Version=")[^"]+' "$CSPROJ")"
if [ -z "$VERSION" ]; then
  echo "playwright: ERROR — no se encontró Microsoft.Playwright en $CSPROJ" >&2
  exit 1
fi

info "Instalando Chromium (npm playwright@$VERSION)"
# --with-deps instala libs de sistema vía apt (el usuario vscode tiene sudo NOPASSWD).
npx -y "playwright@$VERSION" install --with-deps chromium

info "OK: Chromium listo en ~/.cache/ms-playwright"
