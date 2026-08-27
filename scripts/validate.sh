#!/bin/bash
# validate.sh - Build del proyecto de tests y ejecución de los tests de arquitectura
set -euo pipefail

TEST_PROJECT="Tests/Enigma.Test.csproj"

dotnet build "$TEST_PROJECT"

dotnet test "$TEST_PROJECT" --no-build --filter "FullyQualifiedName~Enigma.Test.Architecture"
