param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Calculate It')
$ErrorActionPreference = 'Stop'
if (Get-Process CalculateIt -ErrorAction SilentlyContinue) { throw 'Close Calculate It before installing.' }
if (!(Test-Path -LiteralPath (Join-Path $GamePath 'CalculateIt.exe'))) { throw 'CalculateIt.exe not found.' }
if (!(Test-Path -LiteralPath (Join-Path $GamePath 'BepInEx\plugins\CalculateIt.ModLoader\CalculateIt.ModLoader.dll'))) { throw 'Install Calculate It Mod Loader first.' }
if ([Reflection.AssemblyName]::GetAssemblyName((Join-Path $GamePath 'BepInEx\plugins\CalculateIt.ModLoader\CalculateIt.ModLoader.dll')).Version -lt [Version]'1.3.0') { throw 'Update Calculate It Mod Loader to 1.3.0 or newer first.' }
$assembly = Join-Path $PSScriptRoot 'bin\Release\netstandard2.1\CalculateIt.FractionalNumbers.dll'
if (!(Test-Path -LiteralPath $assembly)) { throw 'Build FractionalNumbers.csproj first.' }
$destination = Join-Path $GamePath 'BepInEx\plugins\FractionalNumbers'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath $assembly -Destination $destination -Force
Write-Output 'Installed Fractional Numbers 0.7.1.'
