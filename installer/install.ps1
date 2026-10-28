# Installs Cartheim for a friend: the bundled BepInEx + Jotunn, then the newest Cartheim.dll from GitHub.
# Run again to update. Written for Windows PowerShell 5.1, which every Windows 10/11 machine has.
param(
    # Valheim's folder (or a dedicated server's); found through Steam when left out.
    [string]$GameDir,
    # A local Cartheim.dll to install instead of downloading the latest release.
    [string]$DllSource
)

$Repo = 'ncperry/Cartheim'

# Every Steam library folder named in steamapps\libraryfolders.vdf.
function Get-SteamLibraries([string]$vdf) {
    @([regex]::Matches($vdf, '"path"\s+"([^"]+)"') | ForEach-Object { $_.Groups[1].Value -replace '\\\\', '\' })
}

# The Valheim folder in the first library that has the game, or $null.
function Find-Valheim([string[]]$libraries) {
    foreach ($library in $libraries) {
        $game = Join-Path $library 'steamapps\common\Valheim'
        if (Test-Path (Join-Path $game 'valheim.exe')) { return $game }
    }
    $null
}

function Get-DllUrl([string]$repo) { "https://github.com/$repo/releases/latest/download/Cartheim.dll" }

# Copies the bundled mod loader into the game without touching configs the player already has,
# then puts Cartheim in its own plugins folder (replacing any older build).
function Install-Cartheim([string]$GameDir, [string]$Payload, [string]$DllSource) {
    Get-ChildItem $Payload -Recurse -File -Force | ForEach-Object {
        $relative = $_.FullName.Substring($Payload.TrimEnd('\').Length + 1)
        $target = Join-Path $GameDir $relative
        if ($relative -like 'BepInEx\config\*' -and (Test-Path $target)) { return }
        New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
        Copy-Item $_.FullName $target -Force
    }
    $plugin = Join-Path $GameDir 'BepInEx\plugins\Cartheim'
    New-Item -ItemType Directory -Force $plugin | Out-Null
    Copy-Item $DllSource (Join-Path $plugin 'Cartheim.dll') -Force
}

function Get-SteamGameDir {
    $steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    if (!$steam) { return $null }
    $libraries = @($steam)
    $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
    if (Test-Path $vdf) { $libraries += Get-SteamLibraries (Get-Content $vdf -Raw) }
    Find-Valheim $libraries
}

function Main {
    $ErrorActionPreference = 'Stop'
    if (!$GameDir) { $GameDir = Get-SteamGameDir }
    while (!$GameDir -or !(Test-Path $GameDir)) {
        $GameDir = (Read-Host "Couldn't find Valheim. Paste its folder (Steam > Valheim > Manage > Browse local files)").Trim('"')
    }
    if (Get-Process valheim -ErrorAction SilentlyContinue) {
        Read-Host 'Valheim is running. Close it, then press Enter' | Out-Null
    }
    if (!$DllSource) {
        $DllSource = Join-Path ([IO.Path]::GetTempPath()) 'Cartheim.dll'
        Write-Host 'Downloading the latest Cartheim...'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest (Get-DllUrl $Repo) -OutFile $DllSource -UseBasicParsing
    }
    Install-Cartheim -GameDir $GameDir -Payload (Join-Path $PSScriptRoot 'payload') -DllSource $DllSource
    $version = (Get-Item $DllSource).VersionInfo.ProductVersion
    Write-Host "Cartheim $version installed in $GameDir"
}

# Dot-sourced by the specs: define the functions only.
if ($MyInvocation.InvocationName -ne '.') { Main }
