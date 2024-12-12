@echo off
REM Get the directory of the batch file
set "BasePath=%~dp0"

REM Delete the "Source/SteamContent" directory if it exists
if exist "%BasePath%Source\SteamContent" (
    rmdir /s /q "%BasePath%Source\SteamContent"
)

REM Create the "Source/SteamContent" directory
mkdir "%BasePath%Source\SteamContent"

REM Copy "steam_api64.dll" to "Source/SteamContent/steam_api64.dll"
copy "%BasePath%Source\steam_api64.dll" "%BasePath%Source\SteamContent\steam_api64.dll"

REM Copy the "Assets" folder to "Source/SteamContent/Assets"
mkdir "%BasePath%Source\SteamContent\Assets"
xcopy "%BasePath%Assets" "%BasePath%Source\SteamContent\Assets" /s /e /q /y

REM Copy "OpenCAGE.exe" to "Source/SteamContent/OpenCAGE.exe"
copy "%BasePath%OpenCAGE.exe" "%BasePath%Source\SteamContent\OpenCAGE.exe"

REM Run the SteamCMD command with the path to appbuild.vdf
SteamCMD +login MattFiler +run_app_build "%BasePath%Source\appbuild.vdf" +quit

REM Pause the script (optional, for debugging or confirmation)
pause
