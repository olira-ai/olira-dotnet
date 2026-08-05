#!/usr/bin/env bash

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m'

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

export PATH="${HOME}/.dotnet:${PATH}"

echo -e "${BLUE}Running pre-PR validation for Olira .NET SDK...${NC}"
echo "========================================================="

echo ""
echo -e "${BLUE}Step 1: Version consistency...${NC}"
bash scripts/check-version.sh

echo ""
echo -e "${BLUE}Step 2: Restore + build...${NC}"
dotnet restore Olira.sln
dotnet build Olira.sln --configuration Release --no-restore

echo ""
echo -e "${BLUE}Step 3: Tests...${NC}"
dotnet test Olira.sln --configuration Release --no-build --verbosity normal

echo ""
echo -e "${GREEN}All pre-PR checks passed.${NC}"
