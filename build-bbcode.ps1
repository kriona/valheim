<#
.SYNOPSIS
Converts each mod's README.md to BBCode for its Nexus Mods description

.DESCRIPTION
Writes release\<Mod>.bbcode for every mod folder with a README.md and a .csproj, ready to paste into the Nexus Mods
description editor. The README's "# Title" line is left out since the mod page shows its name, images point at the
copies on GitHub and tables become lists. An "Other Mods" section links to every other mod in the root README's
"## Mods" table that has a Nexus Mods link, followed by a link to the mod's source on GitHub. Every mod is converted
on each run because adding a mod changes the "Other Mods" section of all the others

.PARAMETER branch
The branch that images and source links point at

.EXAMPLE
.\build-bbcode.ps1
#>
param(
	[string]$branch = 'main'
)

$ErrorActionPreference = 'Stop'

<#
.SYNOPSIS
Converts inline Markdown - links, bold, italics and code - to BBCode

.PARAMETER text
The Markdown text

.PARAMETER baseUrl
URL that relative links resolve against

.OUTPUTS
The text with BBCode tags
#>
function Convert-Inline([string]$text, [string]$baseUrl)
{
	$text = [regex]::Replace($text, '\[([^\]]+)\]\(([^)]+)\)', {
		param($match)
		$url = $match.Groups[2].Value
		if ($url -notmatch '^[a-z]+://')
		{
			$url = $baseUrl + '/' + $url
		}
		return('[url=' + $url + ']' + $match.Groups[1].Value + '[/url]')
	})
	$text = [regex]::Replace($text, '\*\*([^*]+)\*\*', '[b]$1[/b]')
	$text = [regex]::Replace($text, '(?<![\w*])\*([^*]+)\*(?![\w*])', '[i]$1[/i]')
	$text = [regex]::Replace($text, '`([^`]+)`', '[font=Courier New]$1[/font]')
	return($text)
}

<#
.SYNOPSIS
Splits a Markdown table row into its trimmed cells

.PARAMETER line
The table row, e.g. "| a | b |"

.OUTPUTS
The cells' text
#>
function Split-TableRow([string]$line)
{
	$cells = $line.Trim().Trim('|').Split('|')
	return(,($cells | ForEach-Object { $_.Trim() }))
}

<#
.SYNOPSIS
Converts a Markdown table to a BBCode list, one item per row

.DESCRIPTION
Each item is the first cell in bold, the middle cells as "Header: value" in brackets and the last cell after a dash,
e.g. "[b]Max Distance[/b] (Default: 50) - How far away..."

.PARAMETER rows
The table's lines, including the header and separator rows

.PARAMETER baseUrl
URL that relative links resolve against

.OUTPUTS
The BBCode list's lines
#>
function Convert-Table([string[]]$rows, [string]$baseUrl)
{
	$headers = Split-TableRow $rows[0]
	$output = New-Object System.Collections.Generic.List[string]
	$output.Add('[list]')
	foreach ($row in ($rows | Select-Object -Skip 2))
	{
		$cells = Split-TableRow $row
		$item = '[*][b]' + $cells[0] + '[/b]'
		if ($cells.Count -gt 2)
		{
			$middle = @()
			for ($i = 1; $i -lt $cells.Count - 1; $i++)
			{
				$middle += ($headers[$i] + ': ' + $cells[$i])
			}
			$item += ' (' + ($middle -join ', ') + ')'
		}
		if ($cells.Count -gt 1)
		{
			$item += ' - ' + $cells[$cells.Count - 1]
		}
		$output.Add((Convert-Inline $item $baseUrl))
	}
	$output.Add('[/list]')
	return($output)
}

<#
.SYNOPSIS
Reads the table under the "## Mods" heading in the root README.md

.DESCRIPTION
Each row is "| [Name](Folder) | Description | Nexus Mods link |", where the Nexus Mods cell can be empty for a mod
that has no page yet

.PARAMETER readmeFile
Path to the root README.md

.OUTPUTS
An object per mod with Folder, Name, Description and NexusUrl, where NexusUrl is an empty string when the cell has
no link
#>
function Get-ModList([string]$readmeFile)
{
	$mods = @()
	$inSection = $false
	$rowIndex = 0
	foreach ($line in Get-Content $readmeFile -Encoding UTF8)
	{
		if ($line -match '^## ')
		{
			if ($inSection)
			{
				break
			}
			$inSection = ($line.Trim() -eq '## Mods')
			continue
		}
		if (-not $inSection -or $line -notmatch '^\s*\|')
		{
			continue
		}
		$rowIndex++
		# The first two rows are the header and the separator
		if ($rowIndex -le 2)
		{
			continue
		}
		$cells = Split-TableRow $line
		if ($cells[0] -notmatch '^\[([^\]]+)\]\(([^)]+)\)$')
		{
			throw ('Expected "[Name](Folder)" in the Mods table: ' + $cells[0])
		}
		$name = $matches[1]
		$folder = $matches[2].Trim('/')
		$nexusUrl = ''
		if ($cells.Count -gt 2 -and $cells[2] -match 'https?://[^)\s]+')
		{
			$nexusUrl = $matches[0]
		}
		$mods += [PSCustomObject]@{ Folder = $folder; Name = $name; Description = $cells[1]; NexusUrl = $nexusUrl }
	}
	return(,$mods)
}

<#
.SYNOPSIS
Converts a mod's README.md to BBCode

.PARAMETER readmeFile
Path to README.md

.PARAMETER rawUrl
URL of the mod's folder for raw file downloads, used for images

.PARAMETER treeUrl
URL of the mod's folder on GitHub, used for relative links and the source link

.PARAMETER otherMods
The other mods to link to from an "Other Mods" section, from Get-ModList - the section is left out when empty

.OUTPUTS
The BBCode text
#>
function Convert-Readme([string]$readmeFile, [string]$rawUrl, [string]$treeUrl, [object[]]$otherMods)
{
	$lines = @(Get-Content $readmeFile -Encoding UTF8)
	$output = New-Object System.Collections.Generic.List[string]
	$listTag = ''
	$i = 0
	while ($i -lt $lines.Count)
	{
		$line = $lines[$i]

		$listType = ''
		if ($line -match '^\s*[-*] ')
		{
			$listType = 'list'
		}
		elseif ($line -match '^\s*\d+\. ')
		{
			$listType = 'list=1'
		}
		if ($listTag -ne '' -and $listType -ne $listTag)
		{
			$output.Add('[/list]')
			$listTag = ''
		}

		if ($line -match '^# ')
		{
			# The title is skipped along with the blank line after it
			if ($i + 1 -lt $lines.Count -and $lines[$i + 1].Trim() -eq '')
			{
				$i++
			}
		}
		elseif ($line -match '^(#{2,6}) (.+)$')
		{
			$size = 4
			if ($matches[1].Length -eq 2)
			{
				$size = 5
			}
			$output.Add('[size=' + $size + '][b]' + (Convert-Inline $matches[2] $treeUrl) + '[/b][/size]')
		}
		elseif ($listType -ne '')
		{
			if ($listTag -eq '')
			{
				$output.Add('[' + $listType + ']')
				$listTag = $listType
			}
			$output.Add('[*]' + (Convert-Inline ($line -replace '^\s*([-*]|\d+\.) ', '') $treeUrl))
		}
		elseif ($line -match '^\s*\|')
		{
			$rows = @()
			while ($i -lt $lines.Count -and $lines[$i] -match '^\s*\|')
			{
				$rows += $lines[$i]
				$i++
			}
			$output.AddRange([string[]](Convert-Table $rows $treeUrl))
			continue
		}
		elseif ($line -match '<img\s[^>]*src="([^"]+)"')
		{
			$src = $matches[1]
			if ($src -notmatch '^[a-z]+://')
			{
				$src = $rawUrl + '/' + $src
			}
			$output.Add('[img]' + $src + '[/img]')
		}
		else
		{
			$output.Add((Convert-Inline $line $treeUrl))
		}
		$i++
	}
	if ($listTag -ne '')
	{
		$output.Add('[/list]')
	}

	if ($otherMods.Count -gt 0)
	{
		$output.Add('')
		$output.Add('[size=5][b]Other Mods[/b][/size]')
		$output.Add('')
		$output.Add('[list]')
		foreach ($otherMod in $otherMods)
		{
			$output.Add('[*][url=' + $otherMod.NexusUrl + ']' + $otherMod.Name + '[/url] - ' + (Convert-Inline $otherMod.Description $treeUrl))
		}
		$output.Add('[/list]')
	}

	$output.Add('')
	$output.Add('[size=5][b]Source[/b][/size]')
	$output.Add('')
	$output.Add('The source code is on [url=' + $treeUrl + ']GitHub[/url].')
	return(($output -join "`r`n").Trim() + "`r`n")
}

$root = $PSScriptRoot
$remote = (git -C $root remote get-url origin).Trim()
if ($remote -notmatch 'github\.com[:/]([^/]+)/(.+?)(\.git)?$')
{
	throw ('The origin remote is not a GitHub repository: ' + $remote)
}
$owner = $matches[1]
$repo = $matches[2]

$releaseDir = Join-Path $root 'release'
New-Item -ItemType Directory -Force $releaseDir | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)
$modList = Get-ModList (Join-Path $root 'README.md')

foreach ($dir in Get-ChildItem $root -Directory)
{
	$readmeFile = Join-Path $dir.FullName 'README.md'
	$project = Join-Path $dir.FullName ($dir.Name + '.csproj')
	if (-not (Test-Path $readmeFile) -or -not (Test-Path $project))
	{
		continue
	}
	$rawUrl = 'https://raw.githubusercontent.com/' + $owner + '/' + $repo + '/' + $branch + '/' + $dir.Name
	$treeUrl = 'https://github.com/' + $owner + '/' + $repo + '/tree/' + $branch + '/' + $dir.Name
	$otherMods = @($modList | Where-Object { $_.Folder -ne $dir.Name -and $_.NexusUrl -ne '' })
	$bbcode = Convert-Readme $readmeFile $rawUrl $treeUrl $otherMods
	$outFile = Join-Path $releaseDir ($dir.Name + '.bbcode')
	[System.IO.File]::WriteAllText($outFile, $bbcode, $utf8)
	Write-Host ('Wrote ' + $outFile)
}
