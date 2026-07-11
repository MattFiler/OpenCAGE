@echo off
setlocal enabledelayedexpansion
set "BasePath=%~dp0"

if not exist "nuget.exe" (
    echo.
    echo Downloading NuGet.exe...
    powershell -Command "Invoke-WebRequest -Uri 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile 'nuget.exe'"
)

move OpenCAGE.exe OpenCAGE_orig.exe
nuget.exe restore "Source\OpenCAGE.sln"
msbuild "Source\OpenCAGE.sln" /restore /p:Configuration=Ship /t:Rebuild
move OpenCAGE_orig.exe OpenCAGE.exe

call "Source\OpenCAGE\Packager.exe"
pause

SteamCMD +login MattFiler +run_app_build "%BasePath%appbuild.vdf" +quit

pause
