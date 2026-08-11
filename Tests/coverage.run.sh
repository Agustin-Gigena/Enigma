#!/usr/bin/env bash
set -euo pipefail

echo "==> Ejecutando tests con cobertura"

if ! dotnet tool list --global | grep -q coverlet.console; then
  echo "Instalando coverlet.console globalmente"
  dotnet tool install --global coverlet.console --version 4.0.0
fi

export PATH="$PATH:$HOME/.dotnet/tools"

# Ejecutar tests de todos los proyectos de test que existan
for proj in $(find . -type f -name "*Tests.csproj" -o -name "*.Tests.csproj" | sort); do
  echo "-> Ejecutando pruebas en $proj"
  dotnet test "$proj" --configuration Release /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=./TestResults/coverage/
  if [ $? -ne 0 ]; then
    echo "Error: los tests fallaron en $proj"
    exit 1
  fi

done

echo "==> Script coverage.run.sh completado"
