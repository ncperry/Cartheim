@echo off
rem Installs or updates Cartheim: copies the bundled BepInEx + Jotunn into Valheim, then the newest Cartheim and
rem ValheimInventory (its sorted crates, Fetch materials and Mods tab) from the latest GitHub release.
rem Double-click to run; run again to update. Plain batch rather than PowerShell, so Windows needs no script settings changed.
rem   Install.bat [Valheim or dedicated server folder] [local folder holding the release's DLLs instead of downloading]
rem   Install.bat --uninstall [folder]   what Uninstall.bat runs: takes the mods back out, leaving configs
setlocal
set "MODE=install"
if /i "%~1"=="--uninstall" set "MODE=uninstall" & shift
set "GAME=%~1"
set "RELEASE=%~2"
set "PAYLOAD=%~dp0payload"
set "URL=https://github.com/ncperry/Cartheim/releases/latest/download"

if not defined GAME call :find_valheim
:check_game
if defined GAME if exist "%GAME%\valheim.exe" goto found
if defined GAME if exist "%GAME%\valheim_server.exe" goto found
echo Couldn't find Valheim. In Steam, right-click Valheim ^> Manage ^> Browse local files,
echo then copy that folder's address from the top of the window and paste it here.
set "GAME="
set /p "GAME=Valheim folder: "
if not defined GAME goto nothing_installed
set "GAME=%GAME:"=%"
goto check_game

:found
tasklist /fi "imagename eq valheim.exe" 2>nul | findstr /i "valheim.exe" >nul && (
    echo Valheim is running. Close it first.
    pause
)
if "%MODE%"=="uninstall" goto uninstall
if not defined RELEASE (
    echo Downloading the latest Cartheim and ValheimInventory...
    if not exist "%TEMP%\Cartheim-release" mkdir "%TEMP%\Cartheim-release"
    curl.exe -fsSL -o "%TEMP%\Cartheim-release\Cartheim.dll" "%URL%/Cartheim.dll" || goto download_failed
    curl.exe -fsSL -o "%TEMP%\Cartheim-release\ValheimInventory.dll" "%URL%/ValheimInventory.dll" || goto download_failed
    set "RELEASE=%TEMP%\Cartheim-release"
)
rem Configs the player already has are kept: robocopy skips existing files when told to exclude changed, newer and older ones.
robocopy "%PAYLOAD%" "%GAME%" /E /XD "%PAYLOAD%\BepInEx\config" /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 goto copy_failed
if exist "%PAYLOAD%\BepInEx\config\" (
    robocopy "%PAYLOAD%\BepInEx\config" "%GAME%\BepInEx\config" /E /XC /XN /XO /NFL /NDL /NJH /NJS /NP >nul
    if errorlevel 8 goto copy_failed
)
call :install_plugin Cartheim || goto copy_failed
call :install_plugin ValheimInventory || goto copy_failed
echo Cartheim and ValheimInventory installed in %GAME%
call :done
exit /b 0

rem Without winhttp.dll nothing loads BepInEx, so the game starts unmodded. Configs and mods we didn't add stay.
:uninstall
del /f /q "%GAME%\winhttp.dll" "%GAME%\doorstop_config.ini" "%GAME%\.doorstop_version" 2>nul
if exist "%GAME%\BepInEx\core" rmdir /s /q "%GAME%\BepInEx\core"
if exist "%GAME%\BepInEx\plugins\Cartheim" rmdir /s /q "%GAME%\BepInEx\plugins\Cartheim"
if exist "%GAME%\BepInEx\plugins\ValheimInventory" rmdir /s /q "%GAME%\BepInEx\plugins\ValheimInventory"
del /f /q "%GAME%\BepInEx\plugins\Jotunn.dll" 2>nul
if exist "%GAME%\winhttp.dll" goto copy_failed
echo Mods removed from %GAME%. Valheim will start unmodded; run Install.bat to bring them back.
call :done
exit /b 0

rem Puts <name>.dll from the release in its own plugins folder, replacing an older build.
:install_plugin
if not exist "%GAME%\BepInEx\plugins\%1" mkdir "%GAME%\BepInEx\plugins\%1"
copy /y "%RELEASE%\%1.dll" "%GAME%\BepInEx\plugins\%1\%1.dll" >nul
exit /b

:nothing_installed
echo Nothing installed.
call :done
exit /b 1

:download_failed
echo Couldn't download Cartheim from GitHub. Check your internet connection and try again.
call :done
exit /b 1

:copy_failed
echo Couldn't change the files in %GAME%. Is Valheim still running?
call :done
exit /b 1

rem Sets GAME to Valheim's folder in the first Steam library that has it.
rem CARTHEIM_STEAM stands in for Steam's own folder (the specs set it).
:find_valheim
set "STEAM=%CARTHEIM_STEAM%"
if not defined STEAM for /f "tokens=2,*" %%a in ('reg query "HKCU\Software\Valve\Steam" /v SteamPath 2^>nul ^| findstr "SteamPath"') do set "STEAM=%%b"
if not defined STEAM exit /b
set "STEAM=%STEAM:/=\%"
call :try_library "%STEAM%"
rem Lines look like:  <tab><tab>"path"<tab><tab>"D:\\Steam"  so the folder is the fourth piece between quotes.
if exist "%STEAM%\steamapps\libraryfolders.vdf" for /f tokens^=4^ delims^=^" %%p in ('findstr /c:"\"path\"" "%STEAM%\steamapps\libraryfolders.vdf"') do (
    if not defined GAME call :try_library "%%p"
)
exit /b

:try_library
set "LIBRARY=%~1"
set "LIBRARY=%LIBRARY:\\=\%"
if exist "%LIBRARY%\steamapps\common\Valheim\valheim.exe" set "GAME=%LIBRARY%\steamapps\common\Valheim"
exit /b

rem Keeps the window open after a double-click so the player can read the result (the specs skip it).
:done
if not defined CARTHEIM_NOPAUSE pause
exit /b
