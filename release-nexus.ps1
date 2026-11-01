<#
.SYNOPSIS
Nexus Mods upload and description functions for build-release.ps1 and build-bbcode.ps1 - dot-source it after
release-common.ps1

.DESCRIPTION
Uses the Nexus Mods v3 API (https://api-docs.nexusmods.com/) with a personal API key. Most of it is marked
Experimental, so it may change without notice
#>

$nexusApi = 'https://api.nexusmods.com/v3/'

<#
.SYNOPSIS
Maps each of the user's Nexus Mods mods from the ID in its page URL to its API ID

.PARAMETER apiKey
The personal Nexus Mods API key

.OUTPUTS
A hashtable from the page URL's ID (e.g. "4343") to the API's mod ID
#>
function Get-NexusModIds([string]$apiKey)
{
	$headers = @{ apikey = $apiKey }
	$ids = @{}
	$page = 1
	while ($true)
	{
		$result = Invoke-JsonApi 'GET' ($nexusApi + 'team-mods?page_size=100&page=' + $page) $headers $null
		foreach ($teamMod in $result.data.mods)
		{
			$ids[[string]$teamMod.mod.game_scoped_id] = [string]$teamMod.mod.id
		}
		if (@($result.data.mods).Count -eq 0 -or ($page * $result.meta.page_size) -ge $result.meta.total_count)
		{
			break
		}
		$page++
	}
	return($ids)
}

<#
.SYNOPSIS
Looks up a mod's API ID from its Nexus Mods page URL

.PARAMETER modIds
The map from Get-NexusModIds

.PARAMETER nexusUrl
The mod's page URL, e.g. https://www.nexusmods.com/valheim/mods/4343

.OUTPUTS
The API's mod ID
#>
function Get-NexusModId([hashtable]$modIds, [string]$nexusUrl)
{
	if ($nexusUrl -notmatch '/mods/(\d+)')
	{
		throw ('Expected a Nexus Mods mod page URL: ' + $nexusUrl)
	}
	if (-not $modIds.ContainsKey($matches[1]))
	{
		throw ('Mod ' + $matches[1] + ' is not one of your Nexus Mods mods')
	}
	return($modIds[$matches[1]])
}

<#
.SYNOPSIS
Finds a mod's main file on Nexus Mods and its versions

.DESCRIPTION
Each release is a new version of the mod's one active file, so a mod with no active file or more than one fails

.PARAMETER apiKey
The personal Nexus Mods API key

.PARAMETER modId
The API's mod ID

.OUTPUTS
An object with FileId and Versions, the file's versions from the API
#>
function Get-NexusMainFile([string]$apiKey, [string]$modId)
{
	$headers = @{ apikey = $apiKey }
	$files = @((Invoke-JsonApi 'GET' ($nexusApi + 'mods/' + $modId + '/files') $headers $null).data.mod_files | Where-Object { $_.is_active })
	if ($files.Count -ne 1)
	{
		throw ('Expected one active file on Nexus Mods mod ' + $modId + ', found ' + $files.Count)
	}
	$versions = @((Invoke-JsonApi 'GET' ($nexusApi + 'mod-files/' + $files[0].id + '/versions') $headers $null).data.versions)
	return([PSCustomObject]@{ FileId = [string]$files[0].id; Versions = $versions })
}

<#
.SYNOPSIS
Uploads a zip as a new version of a mod's main file and adds its changelog

.DESCRIPTION
Uploads the zip to a presigned URL, waits for Nexus Mods to make it available, then adds it to the file's update chain
replacing the primary version, which moves to Old Files, and sets the mod page's version. The changelog is added after,
one entry per line with list markers removed

.PARAMETER apiKey
The personal Nexus Mods API key

.PARAMETER modId
The API's mod ID

.PARAMETER mainFile
The mod's main file from Get-NexusMainFile

.PARAMETER zipPath
Path to the zip

.PARAMETER name
The version's name, e.g. "QuickFill 1.5.0"

.PARAMETER version
The version

.PARAMETER changelog
The version's changelog section from CHANGELOG.md
#>
function Publish-NexusVersion([string]$apiKey, [string]$modId, [object]$mainFile, [string]$zipPath, [string]$name, [string]$version, [string]$changelog)
{
	$headers = @{ apikey = $apiKey }
	$fileName = [IO.Path]::GetFileName($zipPath)
	$bytes = [IO.File]::ReadAllBytes($zipPath)
	$md5 = Get-Md5 $bytes
	$md5Hex = -join ($md5 | ForEach-Object { $_.ToString('x2') })
	$upload = (Invoke-JsonApi 'POST' ($nexusApi + 'uploads') $headers ([ordered]@{ filename = $fileName; size_bytes = $bytes.Length; md5 = $md5Hex })).data

	# Both headers are part of the presigned URL's signature
	$contentHeaders = @{
		'Content-Disposition' = ('attachment; filename="' + $fileName + '"')
		'Content-MD5' = [Convert]::ToBase64String($md5)
	}
	$response = Invoke-Http 'PUT' $upload.presigned_url $null $bytes $null $contentHeaders
	if (-not $response.Success)
	{
		throw ('Uploading ' + $fileName + ' to Nexus Mods returned ' + $response.StatusCode + ': ' + $response.Text)
	}
	Invoke-JsonApi 'POST' ($nexusApi + 'uploads/' + $upload.id + '/finalise') $headers $null | Out-Null

	$state = ''
	for ($attempt = 0; $attempt -lt 90; $attempt++)
	{
		$state = (Invoke-JsonApi 'GET' ($nexusApi + 'uploads/' + $upload.id) $headers $null).data.state
		if ($state -eq 'available')
		{
			break
		}
		Start-Sleep -Seconds 2
	}
	if ($state -ne 'available')
	{
		throw ('Nexus Mods upload ' + $upload.id + ' is still "' + $state + '" after 3 minutes')
	}

	$primary = @($mainFile.Versions | Where-Object { $_.is_primary })
	$previousId = $null
	if ($primary.Count -gt 0)
	{
		$previousId = [string]$primary[0].id
	}
	$request = [ordered]@{
		upload_id = $upload.id
		name = $name
		version = $version
		file_category = 'main'
		update_mod_version = $true
		archive_existing_file = $true
		previous_version_id = $previousId
	}
	Invoke-JsonApi 'POST' ($nexusApi + 'mod-files/' + $mainFile.FileId + '/versions') $headers $request | Out-Null

	$entries = @($changelog -split "`r?`n" | ForEach-Object { ($_ -replace '^\s*([-*]|\d+\.)\s+', '').Trim() } | Where-Object { $_ -ne '' })
	Invoke-JsonApi 'POST' ($nexusApi + 'mods/' + $modId + '/changelogs') $headers ([ordered]@{ version = $version; changelog = ($entries -join "`n") }) | Out-Null
}

<#
.SYNOPSIS
Pushes each mod's summary and BBCode description to its Nexus Mods page when they've changed

.DESCRIPTION
The summary is the mod's description in the root README's table and the description is release\bbcode\<Mod>.bbcode
from build-bbcode.ps1. What was pushed is saved in release\bbcode\pushed, and a mod is only updated when its summary or
description differs from that copy. Mods with no Nexus Mods link are skipped

.PARAMETER apiKey
The personal Nexus Mods API key

.PARAMETER modList
The mods from Get-ModList

.PARAMETER bbcodeDir
The folder build-bbcode.ps1 writes to
#>
function Publish-NexusDescriptions([string]$apiKey, [object[]]$modList, [string]$bbcodeDir)
{
	$headers = @{ apikey = $apiKey }
	$pushedDir = Join-Path $bbcodeDir 'pushed'
	New-Item -ItemType Directory -Force $pushedDir | Out-Null
	$utf8 = New-Object System.Text.UTF8Encoding($false)
	$modIds = $null
	foreach ($mod in $modList)
	{
		if ($mod.NexusUrl -eq '')
		{
			continue
		}
		$bbcodeFile = Join-Path $bbcodeDir ($mod.Folder + '.bbcode')
		if (-not (Test-Path $bbcodeFile))
		{
			throw ('No BBCode for ' + $mod.Folder + ' at ' + $bbcodeFile + ' - run build-bbcode.ps1')
		}
		$description = [IO.File]::ReadAllText($bbcodeFile, $utf8)
		$pushedDescriptionFile = Join-Path $pushedDir ($mod.Folder + '.bbcode')
		$pushedSummaryFile = Join-Path $pushedDir ($mod.Folder + '.summary.txt')
		$changes = [ordered]@{}
		if (-not (Test-Path $pushedSummaryFile) -or [IO.File]::ReadAllText($pushedSummaryFile, $utf8) -cne $mod.Description)
		{
			$changes['summary'] = $mod.Description
		}
		if (-not (Test-Path $pushedDescriptionFile) -or [IO.File]::ReadAllText($pushedDescriptionFile, $utf8) -cne $description)
		{
			$changes['description'] = $description
		}
		if ($changes.Count -eq 0)
		{
			continue
		}
		if (-not $modIds)
		{
			$modIds = Get-NexusModIds $apiKey
		}
		$modId = Get-NexusModId $modIds $mod.NexusUrl
		Invoke-JsonApi 'PATCH' ($nexusApi + 'mods/' + $modId) $headers $changes | Out-Null
		if ($changes.Contains('summary'))
		{
			[IO.File]::WriteAllText($pushedSummaryFile, $mod.Description, $utf8)
		}
		if ($changes.Contains('description'))
		{
			[IO.File]::WriteAllText($pushedDescriptionFile, $description, $utf8)
		}
		Write-Host ('Updated the Nexus Mods ' + (@($changes.Keys) -join ' and ') + ' for ' + $mod.Name)
	}
}
