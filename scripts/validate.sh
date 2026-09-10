#!/bin/bash
# validate.sh - Build de la solución. Los analizadores EGX (Analyzers/Enigma.Analyzers)
# validan arquitectura y convenciones dentro del compilador en cada build.
set -euo pipefail

dotnet build Enigma.slnx --nologo
