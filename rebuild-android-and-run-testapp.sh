#!/bin/bash
set -x

make CONFIGURATION=Debug prepare all
rm -rf ~/.nuget/packages/*
./dotnet-local.sh build Xamarin.Android.sln -c Debug -t:InstallMaui -p:MauiVersion=10.0.0-preview.5.25306.5 -p:MauiVersionBand=10.0.100-preview.5
./dotnet-local.sh build -t:Run -c Debug -f net10.0-android TestApp -p:UseMonoRuntime=false
