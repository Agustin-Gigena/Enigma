#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "Usage: $0 <commit-message-file>" >&2
  exit 1
fi

commit_msg_file="$1"
commit_msg_subject=$(grep -vE '^#|^[[:space:]]*$' "$commit_msg_file" | head -n 1)

if [ -z "$commit_msg_subject" ]; then
  echo "Error: el mensaje de commit está vacío." >&2
  exit 1
fi

if ! [[ "$commit_msg_subject" =~ ^(build|chore|ci|docs|feat|fix|perf|refactor|revert|style|test)(\([a-z0-9_.-]+\))?:[[:space:]]+.+$ ]]; then
  cat <<'EOF' >&2
Error: El mensaje de commit no cumple con Conventional Commits.

Formato requerido:
  <type>(<scope>): <description>

Tipos permitidos:
  build, chore, ci, docs, feat, fix, perf, refactor, revert, style, test

Ejemplos válidos:
  feat(api): add new endpoint
  fix: correct formatting error
  docs(readme): update installation guide
EOF
  exit 1
fi
