#!/usr/bin/env bash
set -euo pipefail

echo "==> Ejecutando validaciones de guardrail"

echo "-> Restaurar paquetes"
dotnet restore Enigma.slnx

if ! dotnet tool list --global | grep -q dotnet-format; then
  echo "-> Instalando dotnet-format globalmente"
  # Intentar instalar la versión fija; si no está disponible, instalar la última disponible
  if ! dotnet tool install --global dotnet-format --version 9.0.218; then
    echo "-> Versión 9.0.218 no disponible en fuentes NuGet, instalando la última versión disponible"
    if ! dotnet tool install --global dotnet-format; then
      echo "ERROR: no se pudo instalar 'dotnet-format'" >&2
      exit 1
    fi
  fi
fi

export PATH="$PATH:$HOME/.dotnet/tools"

echo "-> Validar YAML"
bash ./scripts/validate-yaml.sh

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
