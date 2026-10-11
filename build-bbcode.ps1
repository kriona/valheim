<#
.SYNOPSIS
Converts each mod's README.md to BBCode for its Nexus Mods description

.DESCRIPTION
Writes release\bbcode\<Mod>.bbcode for every mod folder with a README.md and a .csproj, ready to paste into the Nexus Mods
description editor. The README's "# Title" line is left out since the mod page shows its name, images point at the
copies on GitHub and tables become lists. An "Other Mods By Me" section links to every other mod in the root README's
"## Mods" table that has a Nexus Mods link, followed by a link to the mod's source on GitHub and the root README's
"## Other Mods I Use" section. Every mod is converted on each run because adding a mod changes the "Other Mods By Me"
section of all the others

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
	# Code spans are set aside first, so the link, bold and italic rules don't change the text inside them, like the *
	# in `[Sword*]`
	$codeSpans = New-Object System.Collections.Generic.List[string]
	$text = [regex]::Replace($text, '`([^`]+)`', {
		param($match)
		$codeSpans.Add($match.Groups[1].Value)
		return([char]0 + ($codeSpans.Count - 1) + [char]0)
	})
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
	$text = [regex]::Replace($text, ([char]0 + '(\d+)' + [char]0), {
		param($match)
		return('[font=Courier New]' + $codeSpans[[int]$match.Groups[1].Value] + '[/font]')
	})
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
Each item is the first cell in bold, the other cells as "Header: value" in brackets and a Description column after a
dash, e.g. "[b]Max Distance[/b] (Default: 50) - How far away...". Empty cells are left out

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
		for ($i = 0; $i -lt $cells.Count; $i++)
		{
			$cells[$i] = Convert-Inline $cells[$i] $baseUrl
		}
		$item = '[b]' + $cells[0] + '[/b]'
		$labeled = @()
		$description = ''
		for ($i = 1; $i -lt $cells.Count; $i++)
		{
			if ($cells[$i] -eq '')
			{
				continue
			}
			if ($i -eq $cells.Count - 1 -and $headers[$i] -eq 'Description')
			{
				$description = $cells[$i]
			}
			else
			{
				$labeled += ($headers[$i] + ': ' + $cells[$i])
			}
		}
		if ($labeled.Count -gt 0)
		{
			$item += ' (' + ($labeled -join ', ') + ')'
		}
		if ($description -ne '')
		{
			$item += ' - ' + $description
		}
		$output.Add('[*]' + $item)
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
Reads a section of a Markdown file, from its "## " heading up to the next one

.PARAMETER readmeFile
Path to the Markdown file

.PARAMETER heading
The section's heading line, e.g. "## Other Mods I Use"

.OUTPUTS
The section's lines including the heading, without trailing blank lines - empty when the file has no such section
#>
function Get-ReadmeSection([string]$readmeFile, [string]$heading)
{
	$section = New-Object System.Collections.Generic.List[string]
	$inSection = $false
	foreach ($line in Get-Content $readmeFile -Encoding UTF8)
	{
		if ($line -match '^## ')
		{
			if ($inSection)
			{
				break
			}
			$inSection = ($line.Trim() -eq $heading)
		}
		if ($inSection)
		{
			$section.Add($line)
		}
	}
	while ($section.Count -gt 0 -and $section[$section.Count - 1].Trim() -eq '')
	{
		$section.RemoveAt($section.Count - 1)
	}
	return(,$section.ToArray())
}

<#
.SYNOPSIS
Converts Markdown lines to BBCode, leaving out a "# " title and the blank line after it

.PARAMETER lines
The Markdown lines

.PARAMETER rawUrl
URL of the mod's folder for raw file downloads, used for images

.PARAMETER treeUrl
URL of the mod's folder on GitHub, used for relative links

.OUTPUTS
The BBCode lines
#>
function Convert-Markdown([string[]]$lines, [string]$rawUrl, [string]$treeUrl)
{
	$output = New-Object System.Collections.Generic.List[string]
	# The open lists, outermost first, each with its items' indent (a tab counting as 4 spaces) and its tag
	$listStack = New-Object System.Collections.Generic.List[object]
	$i = 0
	while ($i -lt $lines.Count)
	{
		$line = $lines[$i]

		$listType = ''
		$indent = 0
		if ($line -match '^(\s*)(?:([-*])|\d+\.) ')
		{
			$indent = ($matches[1] -replace "`t", '    ').Length
			$listType = 'list=1'
			if ($matches[2])
			{
				$listType = 'list'
			}
		}
		# Lists indented deeper than this item, or at its indent but of the other kind, are closed, and a line that
		# isn't a list item closes them all
		while ($listStack.Count -gt 0)
		{
			$top = $listStack[$listStack.Count - 1]
			if ($listType -ne '' -and ($top.Indent -lt $indent -or ($top.Indent -eq $indent -and $top.Tag -eq $listType)))
			{
				break
			}
			$output.Add('[/list]')
			$listStack.RemoveAt($listStack.Count - 1)
		}

		if ($line -match '^\s*```')
		{
			# A fenced code block's lines are kept as they are, with the language after the fence dropped
			$code = @()
			$i++
			while ($i -lt $lines.Count -and $lines[$i] -notmatch '^\s*```')
			{
				$code += $lines[$i]
				$i++
			}
			$output.Add('[code]' + ($code -join "`r`n") + '[/code]')
		}
		elseif ($line -match '^# ')
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
			if ($listStack.Count -eq 0 -or $listStack[$listStack.Count - 1].Indent -lt $indent)
			{
				$output.Add('[' + $listType + ']')
				$listStack.Add([PSCustomObject]@{ Indent = $indent; Tag = $listType })
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
	foreach ($list in $listStack)
	{
		$output.Add('[/list]')
	}
	return(,$output)
}

<#
.SYNOPSIS
Converts a mod's README.md to BBCode, followed by its CHANGELOG.md when it has one

.PARAMETER readmeFile
Path to README.md

.PARAMETER rawUrl
URL of the mod's folder for raw file downloads, used for images

.PARAMETER treeUrl
URL of the mod's folder on GitHub, used for relative links and the source link

.PARAMETER otherMods
The other mods to link to from an "Other Mods By Me" section, from Get-ModList - the section is left out when empty

.PARAMETER usedMods
BBCode lines added after the source link, from the root README's "## Other Mods I Use" section - nothing is added
when empty

.OUTPUTS
The BBCode text
#>
function Convert-Readme([string]$readmeFile, [string]$rawUrl, [string]$treeUrl, [object[]]$otherMods, [string[]]$usedMods)
{
	$output = Convert-Markdown @(Get-Content $readmeFile -Encoding UTF8) $rawUrl $treeUrl

	$changelogFile = Join-Path (Split-Path $readmeFile) 'CHANGELOG.md'
	if (Test-Path $changelogFile)
	{
		# Each heading moves down a level, so the "# Changelog" title becomes a section like the README's and each
		# version a heading within it
		$changelog = @(Get-Content $changelogFile -Encoding UTF8 | ForEach-Object {
			if ($_ -match '^#')
			{
				return('#' + $_)
			}
			return($_)
		})
		$output.Add('')
		$output.AddRange([string[]](Convert-Markdown $changelog $rawUrl $treeUrl))
	}

	if ($otherMods.Count -gt 0)
	{
		$output.Add('')
		$output.Add('[size=5][b]Other Mods By Me[/b][/size]')
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

	if ($usedMods.Count -gt 0)
	{
		$output.Add('')
		$output.AddRange($usedMods)
	}
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

$bbcodeDir = Join-Path $root 'release\bbcode'
New-Item -ItemType Directory -Force $bbcodeDir | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)
$rootReadme = Join-Path $root 'README.md'
$modList = Get-ModList $rootReadme
$rootTreeUrl = 'https://github.com/' + $owner + '/' + $repo + '/tree/' + $branch
$rootRawUrl = 'https://raw.githubusercontent.com/' + $owner + '/' + $repo + '/' + $branch
$usedMods = [string[]](Convert-Markdown (Get-ReadmeSection $rootReadme '## Other Mods I Use') $rootRawUrl $rootTreeUrl)

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
	$bbcode = Convert-Readme $readmeFile $rawUrl $treeUrl $otherMods $usedMods
	$outFile = Join-Path $bbcodeDir ($dir.Name + '.bbcode')
	[System.IO.File]::WriteAllText($outFile, $bbcode, $utf8)
	Write-Host ('Wrote ' + $outFile)
}
