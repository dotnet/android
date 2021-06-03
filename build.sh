#!/bin/bash
if [ -z $1 ]; then
    make prepare && make jenkins && make pack-dotnet
else
    case $1 in
        Prepare)
            make prepare
            break
        ;;
        PrepareExternal)
            make prepare-external-git-dependencies
            break
        ;;
        Build)
            make jenkins
            break
        ;;
        Pack)
            make pack-dotnet
            break
        ;;
        Everything)
            make prepare && make jenkins && make pack-dotnet
            break
        ;;
    esac
fi