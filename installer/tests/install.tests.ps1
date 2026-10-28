# Specs for install.ps1, run by test.sh under Windows PowerShell 5.1 (what friends have).
# Plain asserts rather than Pester: 5.1 ships an old Pester whose syntax newer ones reject.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\install.ps1')

$script:failures = 0
function Check($name, $condition) {
    if ($condition) { Write-Host "  ok   $name" } else { Write-Host "  FAIL $name"; $script:failures++ }
}

function New-TempDir {
    $dir = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid())
    New-Item -ItemType Directory $dir | Out-Null
    $dir
}

Write-Host 'Steam libraries'
$vdf = @'
"libraryfolders"
{
	"0"
	{
		"path"		"C:\\Program Files (x86)\\Steam"
		"apps" { "228980" "1" }
	}
	"1"
	{
		"path"		"D:\\Steam"
	}
}
'@
$libraries = Get-SteamLibraries $vdf
Check 'reads every library path' ($libraries.Count -eq 2)
Check 'unescapes backslashes' ($libraries[1] -eq 'D:\Steam')

Write-Host 'Finding Valheim'
$library = New-TempDir
$game = Join-Path $library 'steamapps\common\Valheim'
New-Item -ItemType Directory $game | Out-Null
New-Item -ItemType File (Join-Path $game 'valheim.exe') | Out-Null
Check 'finds the game in a library that has it' ((Find-Valheim @((New-TempDir), $library)) -eq $game)
Check 'nothing when no library has it' ($null -eq (Find-Valheim @((New-TempDir))))

Write-Host 'Installing'
$payload = New-TempDir
New-Item -ItemType Directory (Join-Path $payload 'BepInEx\core'), (Join-Path $payload 'BepInEx\plugins'), (Join-Path $payload 'BepInEx\config') | Out-Null
Set-Content (Join-Path $payload 'winhttp.dll') 'doorstop'
Set-Content (Join-Path $payload 'BepInEx\core\BepInEx.dll') 'bepinex'
Set-Content (Join-Path $payload 'BepInEx\plugins\Jotunn.dll') 'jotunn'
Set-Content (Join-Path $payload 'BepInEx\config\BepInEx.cfg') 'bundled config'
New-Item -ItemType Directory (Join-Path $game 'BepInEx\config') | Out-Null
Set-Content (Join-Path $game 'BepInEx\config\BepInEx.cfg') 'their config'
$dll = Join-Path (New-TempDir) 'Cartheim.dll'
Set-Content $dll 'cartheim v2'

Install-Cartheim -GameDir $game -Payload $payload -DllSource $dll
Check 'copies the mod loader' ((Get-Content (Join-Path $game 'winhttp.dll')) -eq 'doorstop')
Check 'copies BepInEx' (Test-Path (Join-Path $game 'BepInEx\core\BepInEx.dll'))
Check 'copies Jotunn' (Test-Path (Join-Path $game 'BepInEx\plugins\Jotunn.dll'))
Check 'keeps their own config' ((Get-Content (Join-Path $game 'BepInEx\config\BepInEx.cfg')) -eq 'their config')
Check 'puts Cartheim in plugins' ((Get-Content (Join-Path $game 'BepInEx\plugins\Cartheim\Cartheim.dll')) -eq 'cartheim v2')

Set-Content $dll 'cartheim v3'
Install-Cartheim -GameDir $game -Payload $payload -DllSource $dll
Check 'running again updates Cartheim' ((Get-Content (Join-Path $game 'BepInEx\plugins\Cartheim\Cartheim.dll')) -eq 'cartheim v3')

Write-Host 'Download address'
Check 'latest release asset' ((Get-DllUrl 'ncperry/Cartheim') -eq 'https://github.com/ncperry/Cartheim/releases/latest/download/Cartheim.dll')

if ($script:failures) { Write-Host "$script:failures installer spec(s) failed"; exit 1 }
Write-Host 'Installer specs passed'
