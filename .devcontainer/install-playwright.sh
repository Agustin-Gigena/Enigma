#!/usr/bin/env bash
# Instala el navegador Chromium que usan los tests E2E (Microsoft.Playwright).
#
# Lo invoca postCreateCommand (devcontainer.json). Usa el driver node embebido
# en el paquete NuGet en lugar de pwsh (no está en la imagen), así la build del
# navegador siempre coincide con la versión de Microsoft.Playwright restaurada
# para Tests/Enigma.Test.csproj. Es idempotente: si ya está instalado, es un no-op.
set -euo pipefail

WORKSPACE="${WORKSPACE_FOLDER:-/workspaces/Enigma}"
NUGET_CACHE="${NUGET_PACKAGES:-$HOME/.nuget/packages}"

_ts()  { date -u +%H:%M:%S; }
info() { echo "[$(_ts)] playwright: $*"; }

# Restore para que el paquete (y su driver embebido) exista en la caché NuGet.
info "Restaurando proyecto de tests"
dotnet restore "$WORKSPACE/Tests/Enigma.Test.csproj" --nologo -v q

if [ ! -d "$NUGET_CACHE/microsoft.playwright" ]; then
  echo "playwright: ERROR — microsoft.playwright no está en $NUGET_CACHE tras el restore" >&2
  exit 1
fi

# Última versión del paquete (sort -V) = la que resolvieron los ProjectReference.
PKG_DIR="$(find "$NUGET_CACHE/microsoft.playwright" -mindepth 1 -maxdepth 1 -type d | sort -V | tail -1)"

# El driver node vive en .playwright/node/<rid>/node (binario por plataforma).
case "$(uname -m)" in
  x86_64)          RID=linux-x64 ;;
  aarch64 | arm64) RID=linux-arm64 ;;
  *)
    echo "playwright: ERROR — arquitectura no soportada: $(uname -m)" >&2
    exit 1
    ;;
esac
NODE="$PKG_DIR/.playwright/node/$RID/node"
CLI="$PKG_DIR/.playwright/package/cli.js"

if [ ! -x "$NODE" ] || [ ! -f "$CLI" ]; then
  echo "playwright: ERROR — driver no encontrado en $PKG_DIR/.playwright" >&2
  exit 1
fi

info "Instalando Chromium (Microsoft.Playwright $(basename "$PKG_DIR"))"
# --with-deps instala libs de sistema vía apt (el usuario dotnet tiene sudo NOPASSWD).
"$NODE" "$CLI" install --with-deps chromium

info "OK: Chromium listo en ~/.cache/ms-playwright"
