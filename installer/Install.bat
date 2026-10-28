@echo off
rem Installs or updates Cartheim: copies the bundled BepInEx + Jotunn into Valheim, then the newest Cartheim.dll from GitHub.
rem Double-click to run; run again to update. Plain batch rather than PowerShell, so Windows needs no script settings changed.
rem   Install.bat [Valheim or dedicated server folder] [local Cartheim.dll instead of downloading]
rem   Install.bat --uninstall [folder]   what Uninstall.bat runs: takes the mods back out, leaving configs
setlocal
set "MODE=install"
if /i "%~1"=="--uninstall" set "MODE=uninstall" & shift
set "GAME=%~1"
set "DLL=%~2"
set "PAYLOAD=%~dp0payload"
set "URL=https://github.com/ncperry/Cartheim/releases/latest/download/Cartheim.dll"

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
if not defined DLL (
    echo Downloading the latest Cartheim...
    curl.exe -fsSL -o "%TEMP%\Cartheim.dll" "%URL%" || goto download_failed
    set "DLL=%TEMP%\Cartheim.dll"
)
rem Configs the player already has are kept: robocopy skips existing files when told to exclude changed, newer and older ones.
robocopy "%PAYLOAD%" "%GAME%" /E /XD "%PAYLOAD%\BepInEx\config" /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 goto copy_failed
if exist "%PAYLOAD%\BepInEx\config\" (
    robocopy "%PAYLOAD%\BepInEx\config" "%GAME%\BepInEx\config" /E /XC /XN /XO /NFL /NDL /NJH /NJS /NP >nul
    if errorlevel 8 goto copy_failed
)
if not exist "%GAME%\BepInEx\plugins\Cartheim" mkdir "%GAME%\BepInEx\plugins\Cartheim"
copy /y "%DLL%" "%GAME%\BepInEx\plugins\Cartheim\Cartheim.dll" >nul || goto copy_failed
echo Cartheim installed in %GAME%
call :done
exit /b 0

rem Without winhttp.dll nothing loads BepInEx, so the game starts unmodded. Configs and mods we didn't add stay.
:uninstall
del /f /q "%GAME%\winhttp.dll" "%GAME%\doorstop_config.ini" "%GAME%\.doorstop_version" 2>nul
if exist "%GAME%\BepInEx\core" rmdir /s /q "%GAME%\BepInEx\core"
if exist "%GAME%\BepInEx\plugins\Cartheim" rmdir /s /q "%GAME%\BepInEx\plugins\Cartheim"
del /f /q "%GAME%\BepInEx\plugins\Jotunn.dll" 2>nul
if exist "%GAME%\winhttp.dll" goto copy_failed
echo Mods removed from %GAME%. Valheim will start unmodded; run Install.bat to bring them back.
call :done
exit /b 0

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
