#!/usr/bin/env bash
# Publishes a self-contained single-file Windows executable. Requires .NET 8 SDK.
# Output: publish/OperantEditor.exe
set -euo pipefail
dotnet publish src/Operant.Editor/Operant.Editor.csproj \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=embedded \
    -o publish
echo "Done. publish/OperantEditor.exe"
