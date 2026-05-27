@echo off
REM Publishes a self-contained single-file Windows executable.
REM Requires the .NET 8 SDK on PATH.
REM Output: publish\OperantEditor.exe

setlocal
dotnet publish src\Operant.Editor\Operant.Editor.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugType=embedded ^
    -o publish
if errorlevel 1 exit /b 1
echo.
echo Done. Run publish\OperantEditor.exe.
endlocal
