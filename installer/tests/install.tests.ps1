# Specs for Install.bat, run by test.sh: each runs the real installer against a made-up Steam and Valheim.
# Plain asserts rather than Pester: Windows ships an old Pester whose syntax newer ones reject.
$ErrorActionPreference = 'Stop'
$bat = Join-Path $PSScriptRoot '..\Install.bat'
$payload = Join-Path $PSScriptRoot '..\payload'
$env:CARTHEIM_NOPAUSE = '1'

$script:failures = 0
function Check($name, $condition) {
    if ($condition) { Write-Host "  ok   $name" } else { Write-Host "  FAIL $name"; $script:failures++ }
}

function New-TempDir([string]$name = [Guid]::NewGuid()) {
    $dir = Join-Path ([IO.Path]::GetTempPath()) "cartheim-specs\$([Guid]::NewGuid())\$name"
    New-Item -ItemType Directory $dir | Out-Null
    $dir
}

function New-Game([string]$library) {
    $game = Join-Path $library 'steamapps\common\Valheim'
    New-Item -ItemType Directory $game | Out-Null
    New-Item -ItemType File (Join-Path $game 'valheim.exe') | Out-Null
    $game
}

function Read([string]$path) { if (Test-Path $path) { Get-Content $path } }

# The bundled loader the installer copies: a stand-in, made fresh for each run of the specs.
if (Test-Path $payload) { Remove-Item $payload -Recurse -Force }
New-Item -ItemType Directory "$payload\BepInEx\core", "$payload\BepInEx\plugins", "$payload\BepInEx\config" | Out-Null
Set-Content "$payload\winhttp.dll" 'doorstop'
Set-Content "$payload\doorstop_config.ini" 'doorstop settings'
Set-Content "$payload\BepInEx\core\BepInEx.dll" 'bepinex'
Set-Content "$payload\BepInEx\plugins\Jotunn.dll" 'jotunn'
Set-Content "$payload\BepInEx\config\BepInEx.cfg" 'bundled config'
Set-Content "$payload\BepInEx\config\Jotunn.cfg" 'bundled jotunn config'
$dll = Join-Path (New-TempDir) 'Cartheim.dll'

try {
    Write-Host 'Installing into a given folder'
    $game = New-Game (New-TempDir 'Steam Library')
    New-Item -ItemType Directory "$game\BepInEx\config" | Out-Null
    Set-Content "$game\BepInEx\config\BepInEx.cfg" 'their config'
    Set-Content $dll 'cartheim v2'
    & $bat $game $dll | Out-Null
    Check 'succeeds' ($LASTEXITCODE -eq 0)
    Check 'copies the mod loader' ((Read "$game\winhttp.dll") -eq 'doorstop')
    Check 'copies BepInEx' ((Read "$game\BepInEx\core\BepInEx.dll") -eq 'bepinex')
    Check 'copies Jotunn' ((Read "$game\BepInEx\plugins\Jotunn.dll") -eq 'jotunn')
    Check 'keeps a config they already have' ((Read "$game\BepInEx\config\BepInEx.cfg") -eq 'their config')
    Check 'adds a config they lack' ((Read "$game\BepInEx\config\Jotunn.cfg") -eq 'bundled jotunn config')
    Check 'puts Cartheim in its plugins folder' ((Read "$game\BepInEx\plugins\Cartheim\Cartheim.dll") -eq 'cartheim v2')

    Set-Content $dll 'cartheim v3'
    & $bat $game $dll | Out-Null
    Check 'running again updates Cartheim' ((Read "$game\BepInEx\plugins\Cartheim\Cartheim.dll") -eq 'cartheim v3')

    Write-Host 'Finding Valheim through Steam'
    $steam = New-TempDir 'Steam'
    $library = New-TempDir 'Steam Library'
    $found = New-Game $library
    New-Item -ItemType Directory "$steam\steamapps" | Out-Null
    # Steam writes the file like this: tab-separated, backslashes doubled.
    Set-Content "$steam\steamapps\libraryfolders.vdf" @"
"libraryfolders"
{
	"0"
	{
		"path"		"$($steam -replace '\\', '\\')"
	}
	"1"
	{
		"path"		"$($library -replace '\\', '\\')"
		"apps" { "892970" "1" }
	}
}
"@
    $env:CARTHEIM_STEAM = $steam
    & $bat '""' $dll | Out-Null
    Check 'installs into the library that has the game' ((Read "$found\BepInEx\plugins\Cartheim\Cartheim.dll") -eq 'cartheim v3')

    Write-Host 'Bundle without configs'
    # The real bundle has none: BepInEx and Jotunn write theirs on first launch.
    Remove-Item "$payload\BepInEx\config" -Recurse -Force
    $fresh = New-Game (New-TempDir)
    & $bat $fresh $dll | Out-Null
    Check 'still installs' ((Read "$fresh\BepInEx\plugins\Cartheim\Cartheim.dll") -eq 'cartheim v3')

    Write-Host 'Uninstalling'
    $uninstall = Join-Path $PSScriptRoot '..\Uninstall.bat'
    Set-Content "$game\BepInEx\plugins\TheirMod.dll" 'theirs'
    & $uninstall $game | Out-Null
    Check 'succeeds' ($LASTEXITCODE -eq 0)
    Check 'removes the mod loader, so the game starts unmodded' (!(Test-Path "$game\winhttp.dll"))
    Check 'removes the loader settings' (!(Test-Path "$game\doorstop_config.ini"))
    Check 'removes BepInEx' (!(Test-Path "$game\BepInEx\core"))
    Check 'removes Jotunn' (!(Test-Path "$game\BepInEx\plugins\Jotunn.dll"))
    Check 'removes Cartheim' (!(Test-Path "$game\BepInEx\plugins\Cartheim"))
    Check 'keeps their configs for a reinstall' ((Read "$game\BepInEx\config\BepInEx.cfg") -eq 'their config')
    Check "keeps mods it didn't install" ((Read "$game\BepInEx\plugins\TheirMod.dll") -eq 'theirs')
    Check 'leaves the game alone' (Test-Path "$game\valheim.exe")
    & $uninstall $game | Out-Null
    Check 'running again is harmless' ($LASTEXITCODE -eq 0)
    & $bat $game $dll | Out-Null
    Check 'installing again brings it back' ((Read "$game\BepInEx\plugins\Cartheim\Cartheim.dll") -eq 'cartheim v3')

    Write-Host 'No Valheim anywhere'
    $env:CARTHEIM_STEAM = New-TempDir
    '' | & $bat '""' $dll | Out-Null
    Check 'gives up when no folder is given' ($LASTEXITCODE -ne 0)
}
finally {
    Remove-Item $payload -Recurse -Force
    Remove-Item (Join-Path ([IO.Path]::GetTempPath()) 'cartheim-specs') -Recurse -Force -ErrorAction SilentlyContinue
}

if ($script:failures) { Write-Host "$script:failures installer spec(s) failed"; exit 1 }
Write-Host 'Installer specs passed'
