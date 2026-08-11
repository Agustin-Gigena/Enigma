#!/usr/bin/env bash
set -euo pipefail

echo "==> Ejecutando validaciones de guardrail"

echo "-> Restaurar paquetes"
dotnet restore Enigma.slnx

if ! dotnet tool list --global | grep -q dotnet-format; then
  echo "-> Instalando dotnet-format globalmente"
  dotnet tool install --global dotnet-format --version 9.0.218
fi

export PATH="$PATH:$HOME/.dotnet/tools"

echo "-> Formatear código"
dotnet format Enigma.slnx --verify-no-changes

echo "-> Compilar solución"
dotnet build Enigma.slnx -c Release /p:TreatWarningsAsErrors=true

echo "-> Ejecutar tests y cobertura"
if [ -f ./tests/coverage.run.sh ]; then
  bash ./tests/coverage.run.sh
else
  echo "No existe un script de cobertura. Omite temporalmente si no hay tests."
  exit 1
fi

echo "==> Todas las validaciones pasaron"
