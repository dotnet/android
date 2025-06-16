#!/bin/bash -e
#
# This script should be kept in sync with build-tools/xa-prep-tasks/Xamarin.Android.BuildTools.PrepTasks/GitCommitInfo.cs
SUBMODULE_NAME="$1"

function die
{
    echo "$*"
    exit 1
}

if [ -n "$SUBMODULE_NAME" ]; then
    SUBMODULE="external/$SUBMODULE_NAME"

    if [ ! -d "$SUBMODULE" ]; then
        die Submodule $SUBMODULE not found
    fi

    pushd $SUBMODULE > /dev/null 2>&1
fi

BRANCH_FULL="$(git log -n 1 --pretty=%D HEAD)"

if [ -n "$SUBMODULE_NAME" ]; then
    popd > /dev/null
fi

if [ "$(echo $BRANCH_FULL | cut -c 1-6)" == "HEAD, " ]; then
    # Detached HEAD
    # Sample format:
    #
    #    HEAD, origin/main, origin/d16-0-p1, origin/HEAD, main
    #
    BRANCH="$(echo $BRANCH_FULL | cut -c 7-)"
elif [ "$(echo $BRANCH_FULL | cut -c 1-8)" == "HEAD -> " ]; then
    # Normal branch
    # Sample format:
    #
    #    HEAD -> bundle-ndk16-fix, origin/pr/1105
    #
    BRANCH="$(echo $BRANCH_FULL | cut -c 9-)"
elif [ "$(echo $BRANCH_FULL | cut -c 1-4)" == "HEAD" ]; then
    if [ -n "$SUBMODULE_NAME" ]; then
        BRANCH=$(git config -f .gitmodules --get "submodule.$SUBMODULE.branch")
    else
        BRANCH=unknown
    fi
elif [ "$(echo $BRANCH_FULL | cut -c 1-7)" == "grafted" ]; then
    echo "Warning: Grafted repository detected, unable to determine branch name from: $BRANCH_FULL" >&2
    BRANCH=unknown
else
    die Unable to parse branch name from: $BRANCH_FULL
fi

echo $BRANCH | cut -d ',' -f 1 | cut -d '/' -f 2
