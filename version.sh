#!/bin/bash

COMMIT_MESSAGE=$(git log -1 --pretty=%B)
echo "Commit message: $COMMIT_MESSAGE"

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
    echo "Current VERSION: $TAG"
else
    echo "Invalid tag: $TAG"
    exit 1
fi

MAJOR=$(cut -d'.' -f1 <<<$TAG)
MAJOR=${MAJOR:1}
MINOR=$(cut -d'.' -f2 <<<$TAG)
PATCH=$(cut -d'.' -f3 <<<$TAG)

if [[ $COMMIT_MESSAGE == release:* ]]
then
    MAJOR=$((MAJOR+1))
    MINOR=0
    PATCH=0
elif [[ $COMMIT_MESSAGE == feat:* ]]
then
    MINOR=$((MINOR+1))
    PATCH=0
else
    PATCH=$((PATCH+1))
fi

VERSION="v$MAJOR.$MINOR.$PATCH"
echo "NEW VERSION: $VERSION"

if [ $TAG != $VERSION ]
then
    git tag -a $VERSION -m "New verison: $VERSION"
    echo "Tagging new version: $VERSION"
    git push origin $VERSION
fi
