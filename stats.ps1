<#
.SYNOPSIS
Shows each mod's downloads and endorsements on Nexus Mods and downloads and likes on Thunderstore

.DESCRIPTION
Reads the mods and their links from the root README's "## Mods" table and prints a table with a row per mod and a
total row. Nexus Mods needs NEXUS_API_KEY from the environment or .env - without it the Nexus Mods columns are left
empty. Thunderstore needs no key. A mod with no link for a site has empty columns for it

.EXAMPLE
.\stats.ps1
#>

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'release-common.ps1')

<#
.SYNOPSIS
Reads a mod's download and endorsement counts from the Nexus Mods v1 API

.PARAMETER apiKey
The personal Nexus Mods API key

.PARAMETER nexusUrl
The mod's page URL, e.g. https://www.nexusmods.com/valheim/mods/4343

.OUTPUTS
An object with Downloads, UniqueDownloads and Endorsements
#>
function Get-NexusStats([string]$apiKey, [string]$nexusUrl)
{
	if ($nexusUrl -notmatch 'nexusmods\.com/([^/]+)/mods/(\d+)')
	{
		throw ('Expected a Nexus Mods mod page URL: ' + $nexusUrl)
	}
	$url = 'https://api.nexusmods.com/v1/games/' + $matches[1] + '/mods/' + $matches[2] + '.json'
	$mod = Invoke-JsonApi 'GET' $url @{ apikey = $apiKey } $null
	return([PSCustomObject]@{ Downloads = [int]$mod.mod_downloads; UniqueDownloads = [int]$mod.mod_unique_downloads; Endorsements = [int]$mod.endorsement_count })
}

<#
.SYNOPSIS
Reads a package's download and like counts from the Thunderstore package metrics API

.PARAMETER thunderstoreUrl
The package's page URL, e.g. https://thunderstore.io/c/valheim/p/Kriona/QuickFill/

.OUTPUTS
An object with Downloads (across every version) and Likes
#>
function Get-ThunderstoreStats([string]$thunderstoreUrl)
{
	if ($thunderstoreUrl -notmatch '/p/([^/]+)/([^/]+)')
	{
		throw ('Expected a Thunderstore package page URL: ' + $thunderstoreUrl)
	}
	$url = 'https://thunderstore.io/api/v1/package-metrics/' + $matches[1] + '/' + $matches[2] + '/'
	$package = Invoke-JsonApi 'GET' $url $null $null
	return([PSCustomObject]@{ Downloads = [int]$package.downloads; Likes = [int]$package.rating_score })
}

<#
.SYNOPSIS
Adds up a column of the stats rows, skipping empty cells

.PARAMETER rows
The stats rows

.PARAMETER column
The column's name

.OUTPUTS
The total, or an empty string when every cell is empty
#>
function Get-ColumnTotal([object[]]$rows, [string]$column)
{
	$values = @($rows | ForEach-Object { $_.$column } | Where-Object { $_ -isnot [string] })
	if ($values.Count -eq 0)
	{
		return('')
	}
	return(($values | Measure-Object -Sum).Sum)
}

$root = $PSScriptRoot
$nexusApiKey = Get-Secret $root 'NEXUS_API_KEY'
if ($nexusApiKey -eq '')
{
	Write-Warning 'NEXUS_API_KEY is not set in the environment or .env - leaving out Nexus Mods'
}

$columns = @('Mod', 'Nexus Downloads', 'Nexus Unique', 'Endorsements', 'TS Downloads', 'TS Likes')
$modList = Get-ModList (Join-Path $root 'README.md')
$rows = @()
foreach ($mod in $modList)
{
	$row = [ordered]@{}
	foreach ($column in $columns)
	{
		$row[$column] = ''
	}
	$row['Mod'] = $mod.Name
	if ($mod.NexusUrl -ne '' -and $nexusApiKey -ne '')
	{
		$nexus = Get-NexusStats $nexusApiKey $mod.NexusUrl
		$row['Nexus Downloads'] = $nexus.Downloads
		$row['Nexus Unique'] = $nexus.UniqueDownloads
		$row['Endorsements'] = $nexus.Endorsements
	}
	if ($mod.ThunderstoreUrl -ne '')
	{
		$thunderstore = Get-ThunderstoreStats $mod.ThunderstoreUrl
		$row['TS Downloads'] = $thunderstore.Downloads
		$row['TS Likes'] = $thunderstore.Likes
	}
	$rows += [PSCustomObject]$row
}

$total = [ordered]@{ Mod = 'Total' }
foreach ($column in ($columns | Select-Object -Skip 1))
{
	$total[$column] = Get-ColumnTotal $rows $column
}

# A row of dashes
$separator = [ordered]@{}
foreach ($column in $columns)
{
	$cells = @($column, $total[$column]) + @($rows | ForEach-Object { $_.$column })
	$width = ($cells | ForEach-Object { "$_".Length } | Measure-Object -Maximum).Maximum
	$separator[$column] = '-' * $width
}
$separator['Mod'] = '---'
$rows += [PSCustomObject]$separator
$rows += [PSCustomObject]$total

$rows | Format-Table -AutoSize
