#!/usr/bin/env bash

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo -e "${BLUE}Checking version consistency and changelog entry...${NC}"
echo "========================================================="

get_csproj_version() {
    grep -oE '<Version>[^<]+' src/Olira/Olira.csproj | head -1 | sed 's/<Version>//' || echo ""
}

get_version_info_version() {
    grep -E 'public const string Version = ' src/Olira/VersionInfo.cs \
        | sed -E 's/.*Version = "([^"]+)".*/\1/' || echo ""
}

get_changelog_version() {
    if [ -f CHANGELOG.md ]; then
        grep -E '^## \[?[0-9]+\.[0-9]+\.[0-9]+' CHANGELOG.md | head -1 \
            | sed -E 's/^## \[?([0-9]+\.[0-9]+\.[0-9]+(-[^]]+)?)\]?.*/\1/' || echo ""
    else
        echo ""
    fi
}

CSPROJ_VERSION=$(get_csproj_version)
VERSION_INFO_VERSION=$(get_version_info_version)
CHANGELOG_VERSION=$(get_changelog_version)

echo ""
echo -e "${BLUE}Found versions:${NC}"
echo "  - src/Olira/Olira.csproj: ${CSPROJ_VERSION:-<not found>}"
echo "  - src/Olira/VersionInfo.cs: ${VERSION_INFO_VERSION:-<not found>}"
echo "  - CHANGELOG.md: ${CHANGELOG_VERSION:-<not found>}"

if [ -z "$CSPROJ_VERSION" ]; then
    echo -e "${RED}ERROR: Could not find <Version> in src/Olira/Olira.csproj${NC}"
    exit 1
fi

if [ -z "$VERSION_INFO_VERSION" ]; then
    echo -e "${RED}ERROR: Could not find Version in src/Olira/VersionInfo.cs${NC}"
    exit 1
fi

if [ -z "$CHANGELOG_VERSION" ]; then
    echo -e "${RED}ERROR: Could not find version entry in CHANGELOG.md${NC}"
    echo -e "${RED}   Add a changelog entry with format: '## [version]' or '## version'${NC}"
    exit 1
fi

if [ "$CSPROJ_VERSION" != "$VERSION_INFO_VERSION" ]; then
    echo -e "${RED}ERROR: Version mismatch!${NC}"
    echo -e "${RED}   Olira.csproj: $CSPROJ_VERSION${NC}"
    echo -e "${RED}   VersionInfo.cs: $VERSION_INFO_VERSION${NC}"
    exit 1
fi

CSPROJ_BASE=$(echo "$CSPROJ_VERSION" | sed 's/-.*//')
CHANGELOG_BASE=$(echo "$CHANGELOG_VERSION" | sed 's/-.*//')

if [ "$CSPROJ_BASE" != "$CHANGELOG_BASE" ]; then
    echo -e "${RED}ERROR: Changelog version ($CHANGELOG_VERSION) doesn't match project version ($CSPROJ_VERSION)${NC}"
    exit 1
fi

if [ "${CI:-false}" = "true" ] && [ -n "${GITHUB_BASE_REF:-}" ]; then
    echo ""
    echo -e "${BLUE}Checking if version changed from base branch ($GITHUB_BASE_REF)...${NC}"
    BASE_VERSION=$(git show "origin/${GITHUB_BASE_REF}:src/Olira/Olira.csproj" 2>/dev/null \
        | grep -oE '<Version>[^<]+' | head -1 | sed 's/<Version>//' || echo "")
    if [ -z "$BASE_VERSION" ]; then
        echo -e "${YELLOW}Could not determine base branch version, skipping change check${NC}"
    elif [ "$CSPROJ_VERSION" = "$BASE_VERSION" ]; then
        echo -e "${RED}ERROR: Version has not been changed!${NC}"
        echo -e "${RED}   Current: $CSPROJ_VERSION, Base: $BASE_VERSION${NC}"
        echo -e "${RED}   Bump <Version> in Olira.csproj, VersionInfo.Version, and CHANGELOG.md${NC}"
        exit 1
    else
        echo -e "${GREEN}Version changed from $BASE_VERSION to $CSPROJ_VERSION${NC}"
    fi
elif command -v git &> /dev/null && git rev-parse --git-dir &> /dev/null; then
    BASE_BRANCH=$(git symbolic-ref refs/remotes/origin/HEAD 2>/dev/null | sed 's@^refs/remotes/origin/@@' || echo "main")
    BASE_VERSION=$(git show "origin/${BASE_BRANCH}:src/Olira/Olira.csproj" 2>/dev/null \
        | grep -oE '<Version>[^<]+' | head -1 | sed 's/<Version>//' || echo "")

    if [ -n "$BASE_VERSION" ] && [ "$CSPROJ_VERSION" = "$BASE_VERSION" ]; then
        echo ""
        echo -e "${YELLOW}WARNING: Version matches base branch ($BASE_BRANCH) version: $BASE_VERSION${NC}"
        echo -e "${YELLOW}   Consider updating the version if this is a new release${NC}"
    elif [ -n "$BASE_VERSION" ]; then
        echo ""
        echo -e "${GREEN}Version changed from $BASE_VERSION to $CSPROJ_VERSION${NC}"
    fi
fi

echo ""
echo -e "${GREEN}Version check passed: $CSPROJ_VERSION${NC}"
