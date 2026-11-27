#!/bin/bash
set -e

if [ -z "$1" ]; then
    make prepare && make jenkins && make pack-dotnet
else
    case $1 in
        Prepare)
            make prepare
        ;;
        Build)
            make jenkins
        ;;
        Pack)
            make pack-dotnet
        ;;
        Everything)
            make prepare && make jenkins && make pack-dotnet
        ;;
    esac
fi