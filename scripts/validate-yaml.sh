#!/usr/bin/env bash
set -euo pipefail

echo "==> Validando archivos YAML"

if ! command -v yamllint >/dev/null 2>&1; then
  echo "-> yamllint no encontrado, instalando..."
  if command -v python >/dev/null 2>&1; then
    python -m pip install --user yamllint
    export PATH="$PATH:$(python -m site --user-base)/bin:$(python -m site --user-base)/Scripts"
  else
    echo "ERROR: no se encontró 'yamllint' ni 'python'. Instale yamllint manualmente." >&2
    exit 1
  fi
fi

yaml_files=()
while IFS= read -r -d '' file; do
  yaml_files+=("$file")
done < <(find . -type f \( -name '*.yml' -o -name '*.yaml' \) -not -path '*/.git/*' -print0)

if [ ${#yaml_files[@]} -eq 0 ]; then
  echo "No hay archivos YAML para validar."
  exit 0
fi

yamllint --strict "${yaml_files[@]}"
