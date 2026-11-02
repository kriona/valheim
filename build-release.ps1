<#
.SYNOPSIS
Builds a mod in Release and packages it for GitHub Releases, Nexus Mods and Thunderstore, optionally publishing it

.DESCRIPTION
Reads the version from the mod's Plugin.cs, builds it and writes two zips:
- release\packages\<Mod>-<Version>.zip with the DLL at BepInEx\plugins\<Mod>\<Mod>.dll, for GitHub Releases, Nexus Mods
  and installing by hand
- release\thunderstore\Kriona-<Mod>-<Version>.zip with the same DLL plus manifest.json, icon.png, README.md and
  CHANGELOG.md at its root. The README's images and links point at GitHub, and the manifest's description is the mod's
  description in the root README's table

It also runs build-bbcode.ps1 to write release\bbcode\<Mod>.bbcode for every mod. When a Thunderstore token is
available, the manifest and icon are checked with Thunderstore's validators, unless Thunderstore already has the version.

-Publish then publishes the version everywhere, skipping any site that already has it so a failed run can be repeated:
- GitHub - a release tagged <Mod>-v<Version> from the current commit with the zip and the DLL attached and that
  version's section of CHANGELOG.md as the notes, then the mod's older releases are removed, keeping their tags
- Thunderstore - the Thunderstore zip, with the categories in <Mod>\thunderstore-categories.txt plus the shared ones below
- Nexus Mods - the zip as a new version of the mod's main file, replacing the old one, with the changelog section added,
  then every mod's summary and description pushed when they've changed

Secrets come from THUNDERSTORE_TOKEN and NEXUS_API_KEY in the environment or the gitignored .env file. Everything that
can be checked - secrets, changelog, categories and Nexus Mods IDs - is checked before anything is published

.PARAMETER mod
The mod's folder name, e.g. ModName

.PARAMETER publish
Publishes to GitHub, Thunderstore and Nexus Mods after packaging - the current commit must already be pushed

.PARAMETER gitHubOnly
Publishes to GitHub only - needs neither secret

.PARAMETER thunderstoreOnly
Publishes to Thunderstore only, with no GitHub release

.PARAMETER nexusOnly
Publishes to Nexus Mods only, with no GitHub release

.EXAMPLE
.\build-release.ps1 ModName

.EXAMPLE
.\build-release.ps1 ModName -Publish

.EXAMPLE
.\build-release.ps1 ModName -ThunderstoreOnly
#>
param(
	[Parameter(Mandatory = $true)]
	[string]$mod,
	[switch]$publish,
	[switch]$gitHubOnly,
	[switch]$thunderstoreOnly,
	[switch]$nexusOnly
)

$ErrorActionPreference = 'Stop'

# The Thunderstore team every package belongs to - part of every package's URL and dependency string
$thunderstoreTeam = 'Kriona'
$thunderstoreCommunity = 'valheim'
# The BepInExPack the mods are built and tested against - change it after testing with a new version
$bepInExPack = 'denikson-BepInExPack_Valheim-5.4.2360'
# Added to every mod's own categories - when a new game update comes out, change the update's category here and on the
# packages already uploaded under "Manage Package"
$sharedCategories = @('deep-north-update', 'ai-generated')

. (Join-Path $PSScriptRoot 'release-common.ps1')
. (Join-Path $PSScriptRoot 'release-thunderstore.ps1')
. (Join-Path $PSScriptRoot 'release-nexus.ps1')

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
	foreach ($line in Get-Content $changelogFile -Encoding UTF8)
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

<#
.SYNOPSIS
Adds a text file to a zip, encoded as UTF-8 without a BOM

.PARAMETER zip
The open ZipArchive

.PARAMETER entryName
The entry's path in the zip

.PARAMETER text
The file's contents
#>
function Add-ZipText([System.IO.Compression.ZipArchive]$zip, [string]$entryName, [string]$text)
{
	$entry = $zip.CreateEntry($entryName)
	$writer = New-Object System.IO.StreamWriter($entry.Open(), (New-Object System.Text.UTF8Encoding($false)))
	try
	{
		$writer.Write($text)
	}
	finally
	{
		$writer.Dispose()
	}
}

<#
.SYNOPSIS
Checks whether a GitHub release exists for a tag

.PARAMETER tag
The release's tag

.OUTPUTS
True when the release exists
#>
function Test-GitHubRelease([string]$tag)
{
	# gh writes to stderr when there's no release, which Windows PowerShell turns into an error when it's redirected
	$previousPreference = $ErrorActionPreference
	$ErrorActionPreference = 'Continue'
	try
	{
		gh release view $tag --json tagName 2>$null | Out-Null
		return($LASTEXITCODE -eq 0)
	}
	finally
	{
		$ErrorActionPreference = $previousPreference
	}
}

$onlyCount = @($gitHubOnly, $thunderstoreOnly, $nexusOnly | Where-Object { $_ }).Count
if ($onlyCount -gt 1)
{
	throw '-GitHubOnly, -ThunderstoreOnly and -NexusOnly can''t be used together - use -Publish for every site'
}
$toGitHub = (($publish -and $onlyCount -eq 0) -or $gitHubOnly)
$toThunderstore = (($publish -and $onlyCount -eq 0) -or $thunderstoreOnly)
$toNexus = (($publish -and $onlyCount -eq 0) -or $nexusOnly)

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
$gitHub = Get-GitHubRepo $root
$modList = Get-ModList (Join-Path $root 'README.md')
$modEntry = @($modList | Where-Object { $_.Folder -eq $mod })
if ($modEntry.Count -eq 0)
{
	throw ($mod + ' is not in the Mods table in the root README.md')
}
$modEntry = $modEntry[0]
$changelogFile = Join-Path $modDir 'CHANGELOG.md'
$iconFile = Join-Path $modDir 'icon.png'
if (-not (Test-Path $iconFile))
{
	throw ('No Thunderstore icon at ' + $iconFile + ' - add a 256x256 PNG')
}

# Everything publishing needs is checked first, so a release never goes out to some sites and not others
$notes = ''
if ($toGitHub -or $toNexus)
{
	$notes = Get-ChangelogSection $changelogFile $version
	if ($notes -eq '')
	{
		throw ('No "## ' + $version + '" section in ' + $mod + '\CHANGELOG.md')
	}
}
$thunderstoreToken = Get-Secret $root 'THUNDERSTORE_TOKEN'
if ($toThunderstore -and $thunderstoreToken -eq '')
{
	throw 'THUNDERSTORE_TOKEN is not set in the environment or .env'
}
$nexusApiKey = ''
if ($toNexus)
{
	$nexusApiKey = Get-Secret $root 'NEXUS_API_KEY'
	if ($nexusApiKey -eq '')
	{
		throw 'NEXUS_API_KEY is not set in the environment or .env'
	}
	if ($modEntry.NexusUrl -eq '')
	{
		throw ($mod + ' has no Nexus Mods link in the root README.md - create its page on the website first')
	}
}
$categories = @()
if ($toThunderstore)
{
	$categories = Get-ThunderstoreCategories (Join-Path $modDir 'thunderstore-categories.txt') $sharedCategories $thunderstoreCommunity
}

dotnet build $project -c Release
if ($LASTEXITCODE -ne 0)
{
	throw ('Build failed for ' + $mod)
}

$dll = Join-Path $modDir ('bin\Release\netstandard2.1\' + $mod + '.dll')
$dllEntry = 'BepInEx/plugins/' + $mod + '/' + $mod + '.dll'
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
	[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $dll, $dllEntry) | Out-Null
}
finally
{
	$zip.Dispose()
}
Write-Host ('Packaged ' + $zipPath)

# The Thunderstore zip is kept apart from the other one, so extracting that one into the Valheim folder by hand doesn't
# leave a manifest, icon and README there
$rawUrl = 'https://raw.githubusercontent.com/' + $gitHub.Owner + '/' + $gitHub.Repo + '/main/' + $mod
$treeUrl = 'https://github.com/' + $gitHub.Owner + '/' + $gitHub.Repo + '/tree/main/' + $mod
$teamUrl = 'https://thunderstore.io/c/' + $thunderstoreCommunity + '/p/' + $thunderstoreTeam + '/'
$manifest = New-ThunderstoreManifest $mod $version $treeUrl $modEntry.Description @($bepInExPack)
$readme = Convert-ThunderstoreReadme (Join-Path $modDir 'README.md') $rawUrl $treeUrl $teamUrl
$thunderstoreDir = Join-Path $root 'release\thunderstore'
New-Item -ItemType Directory -Force $thunderstoreDir | Out-Null
$thunderstoreZipPath = Join-Path $thunderstoreDir ($thunderstoreTeam + '-' + $mod + '-' + $version + '.zip')
if (Test-Path $thunderstoreZipPath)
{
	Remove-Item $thunderstoreZipPath
}
$zip = [System.IO.Compression.ZipFile]::Open($thunderstoreZipPath, 'Create')
try
{
	Add-ZipText $zip 'manifest.json' $manifest
	Add-ZipText $zip 'README.md' $readme
	[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $iconFile, 'icon.png') | Out-Null
	if (Test-Path $changelogFile)
	{
		Add-ZipText $zip 'CHANGELOG.md' ([IO.File]::ReadAllText($changelogFile))
	}
	[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $dll, $dllEntry) | Out-Null
}
finally
{
	$zip.Dispose()
}
Write-Host ('Packaged ' + $thunderstoreZipPath)

# Thunderstore's validator rejects a manifest for a version it already has, so a repeated run skips the check
$thunderstoreHasVersion = $false
if ($thunderstoreToken -ne '')
{
	$thunderstoreHasVersion = Test-ThunderstoreVersion $thunderstoreTeam $mod $version
	if ($thunderstoreHasVersion)
	{
		Write-Host ('Thunderstore already has ' + $thunderstoreTeam + '-' + $mod + '-' + $version + ', skipping the manifest and icon check')
	}
	else
	{
		Test-ThunderstorePackage $thunderstoreToken $thunderstoreTeam $manifest $iconFile
		Write-Host 'Thunderstore accepted manifest.json and icon.png'
	}
}

& (Join-Path $root 'build-bbcode.ps1')

if (-not ($toGitHub -or $toThunderstore -or $toNexus))
{
	return
}

# The Nexus Mods lookups happen before anything is published, so a missing mod or file fails early
$nexusModId = ''
$nexusMainFile = $null
$nexusHasVersion = $false
if ($toNexus)
{
	$nexusModId = Get-NexusModId (Get-NexusModIds $nexusApiKey) $modEntry.NexusUrl
	$nexusMainFile = Get-NexusMainFile $nexusApiKey $nexusModId
	$nexusHasVersion = (@($nexusMainFile.Versions | Where-Object { $_.version -eq $version }).Count -gt 0)
}

if ($toGitHub)
{
	$tag = $mod + '-v' + $version
	if (Test-GitHubRelease $tag)
	{
		Write-Host ('GitHub already has release ' + $tag + ', skipping it')
	}
	else
	{
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
	}

	# Only the newest release of each mod stays on the Releases page - the older ones' tags are kept, so every version
	# can still be found in the history
	$releaseTags = gh release list --limit 1000 --json tagName --jq '.[].tagName'
	if ($LASTEXITCODE -ne 0)
	{
		throw ('gh release list failed, so older ' + $mod + ' releases were not removed')
	}
	foreach ($oldTag in $releaseTags)
	{
		if ($oldTag.StartsWith($mod + '-v') -and $oldTag -ne $tag)
		{
			gh release delete $oldTag --yes
			if ($LASTEXITCODE -ne 0)
			{
				throw ('gh release delete failed for ' + $oldTag)
			}
			Write-Host ('Removed release ' + $oldTag + ', keeping its tag')
		}
	}
}

if ($toThunderstore)
{
	if ($thunderstoreHasVersion)
	{
		Write-Host ('Thunderstore already has ' + $thunderstoreTeam + '-' + $mod + '-' + $version + ', skipping it')
	}
	else
	{
		$downloadUrl = Publish-ThunderstorePackage $thunderstoreToken $thunderstoreZipPath $thunderstoreTeam $thunderstoreCommunity $categories
		Write-Host ('Published to Thunderstore: ' + $downloadUrl)
	}
}

if ($toNexus)
{
	if ($nexusHasVersion)
	{
		Write-Host ('Nexus Mods already has ' + $mod + ' ' + $version + ', skipping the upload and changelog')
	}
	else
	{
		Publish-NexusVersion $nexusApiKey $nexusModId $nexusMainFile $zipPath ($mod + ' ' + $version) $version $notes
		Write-Host ('Published ' + $mod + ' ' + $version + ' to Nexus Mods')
	}
	Publish-NexusDescriptions $nexusApiKey $modList (Join-Path $root 'release\bbcode')
}
