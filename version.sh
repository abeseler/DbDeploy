#!/bin/bash
set -euo pipefail

# Source of truth is <Version> in src/Ratchet/Ratchet.csproj.
# This script tags that number when it is newer than the latest git tag.
# It does not bump the project. A commit that does not change Version is not a release.

SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd)
CSPROJ="$SCRIPT_DIR/src/Ratchet/Ratchet.csproj"

if [[ ! -f "$CSPROJ" ]]
then
    echo "Project file not found: $CSPROJ"
    exit 1
fi

PROJECT_VERSION=$(sed -n 's/.*<Version>\([0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*\)<\/Version>.*/\1/p' "$CSPROJ" | head -n 1)
if [[ ! $PROJECT_VERSION =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]
then
    echo "Could not read a semver <Version> from $CSPROJ"
    exit 1
fi

if [ -z "$(git tag -l)" ]
then
    TAG="v0.0.0"
else
    TAG=$(git describe --abbrev=0 --tags)
fi

if git describe --tags --exact-match HEAD >/dev/null 2>&1
then
    echo "Commit has already been tagged: $TAG"
    exit 0
fi

if [[ $TAG =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]
then
    echo "Latest tag: $TAG"
else
    echo "Invalid tag: $TAG"
    exit 1
fi

TAG_VERSION=${TAG#v}
echo "Project VERSION: $PROJECT_VERSION"

if [ "$PROJECT_VERSION" = "$TAG_VERSION" ]
then
    echo "Project version matches latest tag. Nothing to tag."
    exit 0
fi

LOWEST=$(printf '%s\n%s\n' "$PROJECT_VERSION" "$TAG_VERSION" | sort -V | head -n 1)
if [ "$LOWEST" = "$PROJECT_VERSION" ]
then
    echo "Project version $PROJECT_VERSION is behind latest tag $TAG"
    exit 1
fi

VERSION="v$PROJECT_VERSION"
echo "NEW VERSION: $VERSION"

git tag -a "$VERSION" -m "New version: $VERSION"
echo "Tagging new version: $VERSION"
git push origin "$VERSION"
