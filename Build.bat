setlocal enabledelayedexpansion
set "BasePath=%~dp0"

git submodule update --init --recursive

msbuild "Source\OpenCAGE.sln" /restore /p:Configuration=Release /t:Rebuild
call "Source\OpenCAGE\Packager.exe"
popd
copy /Y "Source\Updater\Build\OpenCAGE Updater.exe" "Source\Dependencies\CommandsEditor\CathodeEditorGUI\Updater\OpenCAGE Updater.exe"

msbuild "Source\Dependencies\CommandsEditor\CathodeEditorGUI\CommandsEditor.sln" /restore /p:Configuration=Ship /t:Rebuild
copy /Y "Source\Dependencies\CommandsEditor\Build\CommandsEditor.exe" "OpenCAGE.exe"
copy /Y "Source\Dependencies\CommandsEditor\CathodeEditorGUI\Properties\AssemblyInfo.cs" "Source\OpenCAGE\Properties\AssemblyInfo.cs"

git add .
git commit -m "Automated build"

pause

if exist "%BasePath%Source\SteamContent" (
    rmdir /s /q "%BasePath%Source\SteamContent"
)
mkdir "%BasePath%Source\SteamContent"
copy "%BasePath%Source\steam_api64.dll" "%BasePath%Source\SteamContent\steam_api64.dll"
copy "%BasePath%Source\installscript.vdf" "%BasePath%Source\SteamContent\installscript.vdf"
mkdir "%BasePath%Source\SteamContent\Assets"
xcopy "%BasePath%Assets" "%BasePath%Source\SteamContent\Assets" /s /e /q /y
copy "%BasePath%OpenCAGE.exe" "%BasePath%Source\SteamContent\OpenCAGE.exe"

SteamCMD +login MattFiler +run_app_build "%BasePath%Source\appbuild.vdf" +quit

pause
