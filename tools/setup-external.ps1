<#
.SYNOPSIS
Fills external\ with the game's decompiled code and a copy of the DLLs the mods build against

.DESCRIPTION
Decompiles assembly_valheim, assembly_utils and assembly_guiutils one class per file into external\decompiled\<Assembly>
and copies the game's valheim_Data\Managed and BepInEx's core folders into external\Valheim, which Directory.Build.props
builds against when the game isn't installed. Both folders are deleted and made again, so it also refreshes them after a
game or BepInEx update. Other files in external\ are left alone. Needs Valheim with BepInExPack installed and ilspycmd
(dotnet tool install -g ilspycmd)

.PARAMETER ValheimPath
The game folder - defaults to the ValheimPath environment variable, then the Steam folder

.EXAMPLE
.\tools\setup-external.ps1

.EXAMPLE
.\tools\setup-external.ps1 -ValheimPath 'D:\Games\Valheim'
#>

param(
	[string]$ValheimPath
)

$ErrorActionPreference = 'Stop'

$assemblies = @('assembly_valheim', 'assembly_utils', 'assembly_guiutils')

if (-not $ValheimPath)
{
	if ($env:ValheimPath)
	{
		$ValheimPath = $env:ValheimPath
	}
	else
	{
		$ValheimPath = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim'
	}
}

$managedPath = Join-Path $ValheimPath 'valheim_Data\Managed'
$bepInExCorePath = Join-Path $ValheimPath 'BepInEx\core'
if (-not (Test-Path (Join-Path $managedPath 'assembly_valheim.dll')))
{
	throw ('Valheim not found at ' + $ValheimPath + ' - pass -ValheimPath or set the ValheimPath environment variable')
}
if (-not (Test-Path (Join-Path $bepInExCorePath 'BepInEx.dll')))
{
	throw ('BepInEx not found in ' + $ValheimPath + ' - install BepInExPack for Valheim first')
}

$ilspy = Get-Command 'ilspycmd' -ErrorAction SilentlyContinue
if (-not $ilspy)
{
	throw 'ilspycmd not found - install it with: dotnet tool install -g ilspycmd'
}

$externalPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'external'
$copyPath = Join-Path $externalPath 'Valheim'
$decompiledPath = Join-Path $externalPath 'decompiled'

foreach ($path in @($copyPath, $decompiledPath))
{
	if (Test-Path $path)
	{
		Remove-Item -Recurse -Force $path
	}
}

Write-Host ('Copying DLLs from ' + $ValheimPath)
New-Item -ItemType Directory -Force (Join-Path $copyPath 'valheim_Data'), (Join-Path $copyPath 'BepInEx') | Out-Null
Copy-Item -Recurse $managedPath (Join-Path $copyPath 'valheim_Data')
Copy-Item -Recurse $bepInExCorePath (Join-Path $copyPath 'BepInEx')

foreach ($assembly in $assemblies)
{
	Write-Host ('Decompiling ' + $assembly)
	& $ilspy.Source -p -o (Join-Path $decompiledPath $assembly) -r $managedPath (Join-Path $managedPath ($assembly + '.dll'))
	if ($LASTEXITCODE -ne 0)
	{
		throw ('ilspycmd failed on ' + $assembly)
	}
}

$fileCount = (Get-ChildItem -Recurse -Filter '*.cs' $decompiledPath).Count
Write-Host ('Done - ' + $fileCount + ' decompiled files in ' + $decompiledPath + ', reference DLLs in ' + $copyPath)
