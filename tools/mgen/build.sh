#!/bin/bash -e
DOTNET="$(type -fP dotnet-preview || true)"
DOTNET="${DOTNET:-dotnet}"

exec "${DOTNET}" build "$@"
