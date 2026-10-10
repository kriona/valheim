<#
.SYNOPSIS
Builds a mod in Release and packages it as a zip ready for GitHub Releases and Nexus Mods

.DESCRIPTION
Reads the version from the mod's Plugin.cs, builds it and writes release\packages\<Mod>-<Version>.zip with the DLL at
BepInEx\plugins\<Mod>\<Mod>.dll, and runs build-bbcode.ps1 to write release\bbcode\<Mod>.bbcode for every mod. With
-Publish, also creates a GitHub release tagged <Mod>-v<Version> from the current commit with the zip and the DLL
attached, using that version's section of the mod's CHANGELOG.md as the release notes

.PARAMETER mod
The mod's folder name, e.g. ModName

.PARAMETER publish
Creates the GitHub release after packaging - the current commit must already be pushed

.EXAMPLE
.\build-release.ps1 ModName

.EXAMPLE
.\build-release.ps1 ModName -Publish
#>
param(
	[Parameter(Mandatory = $true)]
	[string]$mod,
	[switch]$publish
)

$ErrorActionPreference = 'Stop'

<#
.SYNOPSIS
Reads a string constant from the mod's Plugin.cs

.PARAMETER pluginFile
Path to Plugin.cs

.PARAMETER name
The constant's name, e.g. PluginVersion

.OUTPUTS
The constant's value
#>
function Get-PluginConstant([string]$pluginFile, [string]$name)
{
	$match = Select-String -Path $pluginFile -Pattern ($name + ' = "([^"]+)"')
	if (-not $match)
	{
		throw ($name + ' not found in ' + $pluginFile)
	}
	return($match.Matches[0].Groups[1].Value)
}

<#
.SYNOPSIS
Returns the lines under a version's "## <version>" heading in a changelog, up to the next heading

.PARAMETER changelogFile
Path to CHANGELOG.md

.PARAMETER version
The version whose section is returned

.OUTPUTS
The section's text, or an empty string when the file or section is missing
#>
function Get-ChangelogSection([string]$changelogFile, [string]$version)
{
	if (-not (Test-Path $changelogFile))
	{
		return('')
	}
	$lines = New-Object System.Collections.Generic.List[string]
	$inSection = $false
	foreach ($line in Get-Content $changelogFile)
	{
		if ($line -match '^## ')
		{
			if ($inSection)
			{
				break
			}
			$inSection = ($line.Trim() -eq ('## ' + $version))
			continue
		}
		if ($inSection)
		{
			$lines.Add($line)
		}
	}
	return(($lines -join "`r`n").Trim())
}

$root = $PSScriptRoot
$modDir = Join-Path $root $mod
$project = Join-Path $modDir ($mod + '.csproj')
if (-not (Test-Path $project))
{
	throw ('No project found at ' + $project)
}

$pluginFile = Join-Path $modDir 'Plugin.cs'
$version = Get-PluginConstant $pluginFile 'PluginVersion'
$displayName = Get-PluginConstant $pluginFile 'PluginName'

dotnet build $project -c Release
if ($LASTEXITCODE -ne 0)
{
	throw ('Build failed for ' + $mod)
}

$dll = Join-Path $modDir ('bin\Release\netstandard2.1\' + $mod + '.dll')
$packagesDir = Join-Path $root 'release\packages'
New-Item -ItemType Directory -Force $packagesDir | Out-Null
$zipPath = Join-Path $packagesDir ($mod + '-' + $version + '.zip')
if (Test-Path $zipPath)
{
	Remove-Item $zipPath
}

# Entries are added one at a time so their paths use forward slashes - Compress-Archive in Windows PowerShell
# writes backslashes, which some mod managers don't treat as folders
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, 'Create')
try
{
	[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $dll, ('BepInEx/plugins/' + $mod + '/' + $mod + '.dll')) | Out-Null
}
finally
{
	$zip.Dispose()
}
Write-Host ('Packaged ' + $zipPath)

& (Join-Path $root 'build-bbcode.ps1')

if (-not $publish)
{
	return
}

$tag = $mod + '-v' + $version
$notes = Get-ChangelogSection (Join-Path $modDir 'CHANGELOG.md') $version
if ($notes -eq '')
{
	throw ('No "## ' + $version + '" section in ' + $mod + '\CHANGELOG.md')
}
$notesFile = [System.IO.Path]::GetTempFileName()
try
{
	[System.IO.File]::WriteAllText($notesFile, $notes)
	$commit = (git -C $root rev-parse HEAD).Trim()
	gh release create $tag $zipPath $dll --target $commit --title ($displayName + ' ' + $version) --notes-file $notesFile
	if ($LASTEXITCODE -ne 0)
	{
		throw ('gh release create failed for ' + $tag)
	}
}
finally
{
	Remove-Item $notesFile
}
