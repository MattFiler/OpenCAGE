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

call "Source\OpenCAGE\Packager.exe"
popd

move OpenCAGE_orig.exe OpenCAGE.exe

pause

scp -r "%BasePath%BuildFinal/"* root@opencage.mattfiler.co.uk:/var/www/websites/opencage.mattfiler.co.uk/download/staging/
ssh root@opencage.mattfiler.co.uk "chmod -R 755 /var/www/websites/opencage.mattfiler.co.uk/download/staging/"
SteamCMD +login MattFiler +run_app_build "%BasePath%Source\appbuild.vdf" +quit

pause
