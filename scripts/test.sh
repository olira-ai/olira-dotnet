#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

export PATH="${HOME}/.dotnet:${PATH}"

echo "Running Olira .NET tests..."
dotnet test Olira.sln --verbosity normal

echo "All tests passed"
