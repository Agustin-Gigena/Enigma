#!/usr/bin/env bash
set -euo pipefail

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if ! command -v dotnet-stryker >/dev/null 2>&1; then
  echo "-> Instalando dotnet-stryker 4.16.0"
  dotnet tool install --global dotnet-stryker --version 4.16.0
fi

export PATH="$PATH:$HOME/.dotnet/tools"
cd "$script_directory"

echo "==> Ejecutando mutation testing con Stryker"
dotnet stryker --config-file stryker-config.json --configuration Release --concurrency 4
