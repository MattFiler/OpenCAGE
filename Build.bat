setlocal enabledelayedexpansion
set "BasePath=%~dp0"

git status

pause

if not exist "nuget.exe" (
    echo.
    echo Downloading NuGet.exe...
    powershell -Command "Invoke-WebRequest -Uri 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile 'nuget.exe'"
)

nuget.exe restore "Source\OpenCAGE.sln"
msbuild "Source\OpenCAGE.sln" /restore /p:Configuration=Ship /t:Rebuild
call "Source\OpenCAGE\Packager.exe"
popd
copy /Y "Build\OpenCAGE.exe" "OpenCAGE.exe"

pause

git add .
git commit -m "Automated build"
git push

if exist "%BasePath%Source\SteamContent" (
    rmdir /s /q "%BasePath%Source\SteamContent"
)
mkdir "%BasePath%Source\SteamContent"
copy "%BasePath%Source\steam_api64.dll" "%BasePath%Source\SteamContent\steam_api64.dll"
copy "%BasePath%Source\installscript.vdf" "%BasePath%Source\SteamContent\installscript.vdf"
mkdir "%BasePath%Source\SteamContent\Assets"
xcopy "%BasePath%Assets" "%BasePath%Source\SteamContent\Assets" /s /e /q /y
copy "%BasePath%Build\OpenCAGE.exe" "%BasePath%Source\SteamContent\OpenCAGE.exe"

SteamCMD +login MattFiler +run_app_build "%BasePath%Source\appbuild.vdf" +quit

pause
