<#
.SYNOPSIS
Thunderstore packaging and upload functions for build-release.ps1 - dot-source it after release-common.ps1

.DESCRIPTION
Builds a package's manifest.json and README.md, checks them with Thunderstore's validators and uploads a package through
the API under https://thunderstore.io/api/experimental/, which is what thunderstore-cli uses
#>

$thunderstoreApi = 'https://thunderstore.io/api/experimental/'

<#
.SYNOPSIS
Encodes a string as a JSON string literal

.DESCRIPTION
ConvertTo-Json in Windows PowerShell escapes ', <, > and & as \u sequences - they're turned back into the characters
so manifest.json stays readable

.PARAMETER value
The string to encode

.OUTPUTS
The quoted, escaped string
#>
function ConvertTo-JsonString([string]$value)
{
	$json = ConvertTo-Json -InputObject $value
	return([regex]::Replace($json, '\\u(0027|003c|003e|0026)', {
		param($match)
		return([string][char][Convert]::ToInt32($match.Groups[1].Value, 16))
	}))
}

<#
.SYNOPSIS
Builds a package's manifest.json

.PARAMETER name
The package name - letters, digits and underscores only

.PARAMETER version
The version, in strict x.y.z form

.PARAMETER websiteUrl
The package's website

.PARAMETER description
The package's description - at most 250 characters

.PARAMETER dependencies
The dependency strings, each "Team-Package-x.y.z"

.OUTPUTS
The manifest's JSON text
#>
function New-ThunderstoreManifest([string]$name, [string]$version, [string]$websiteUrl, [string]$description, [string[]]$dependencies)
{
	if ($name -notmatch '^[a-zA-Z0-9_]{1,128}$')
	{
		throw ('Thunderstore package names can only use letters, digits and underscores: ' + $name)
	}
	if ($version -notmatch '^\d+\.\d+\.\d+$')
	{
		throw ('Thunderstore versions must be x.y.z: ' + $version)
	}
	if ($description.Length -gt 250)
	{
		throw ('The description for ' + $name + ' in the root README.md is ' + $description.Length + ' characters - Thunderstore allows 250')
	}
	$dependencyLines = @($dependencies | ForEach-Object { "`t`t" + (ConvertTo-JsonString $_) })
	$lines = @(
		'{',
		("`t" + '"name": ' + (ConvertTo-JsonString $name) + ','),
		("`t" + '"version_number": ' + (ConvertTo-JsonString $version) + ','),
		("`t" + '"website_url": ' + (ConvertTo-JsonString $websiteUrl) + ','),
		("`t" + '"description": ' + (ConvertTo-JsonString $description) + ','),
		("`t" + '"dependencies": [')
	)
	$lines += ($dependencyLines -join (',' + "`n"))
	$lines += ("`t" + ']')
	$lines += '}'
	return(($lines -join "`n") + "`n")
}

<#
.SYNOPSIS
Rewrites a mod's README.md for its Thunderstore package

.DESCRIPTION
Leaves out the "# Title" line and the blank line after it, since the package page shows its name, and points relative
images and links at GitHub, since Thunderstore doesn't render images inside the package. Links to headings (#...) and
the lines of fenced code blocks are left as they are. Ends with a link to every mod on Thunderstore, rather than a list
that would go stale since a version's README can't be changed after upload, and a link to the source

.PARAMETER readmeFile
Path to README.md

.PARAMETER rawUrl
URL of the mod's folder for raw file downloads, used for images

.PARAMETER treeUrl
URL of the mod's folder on GitHub, used for relative links and the source link

.PARAMETER teamUrl
URL of the team's package list on Thunderstore

.OUTPUTS
The README's text with LF line endings
#>
function Convert-ThunderstoreReadme([string]$readmeFile, [string]$rawUrl, [string]$treeUrl, [string]$teamUrl)
{
	$lines = @(Get-Content $readmeFile -Encoding UTF8)
	$output = New-Object System.Collections.Generic.List[string]
	$inCode = $false
	for ($i = 0; $i -lt $lines.Count; $i++)
	{
		$line = $lines[$i]
		if ($line -match '^\s*```')
		{
			$inCode = -not $inCode
			$output.Add($line)
			continue
		}
		if ($inCode)
		{
			$output.Add($line)
			continue
		}
		if ($line -match '^# ' -and $output.Count -eq 0)
		{
			if ($i + 1 -lt $lines.Count -and $lines[$i + 1].Trim() -eq '')
			{
				$i++
			}
			continue
		}
		$line = [regex]::Replace($line, '(<img\s[^>]*src=")(?![a-z]+://)([^"]+)"', {
			param($match)
			return($match.Groups[1].Value + $rawUrl + '/' + $match.Groups[2].Value + '"')
		})
		$line = [regex]::Replace($line, '(!?)\[([^\]]*)\]\((?![a-z]+://|#)([^)]+)\)', {
			param($match)
			$base = $treeUrl
			if ($match.Groups[1].Value -eq '!')
			{
				$base = $rawUrl
			}
			return($match.Groups[1].Value + '[' + $match.Groups[2].Value + '](' + $base + '/' + $match.Groups[3].Value + ')')
		})
		$output.Add($line)
	}
	while ($output.Count -gt 0 -and $output[$output.Count - 1].Trim() -eq '')
	{
		$output.RemoveAt($output.Count - 1)
	}
	$output.Add('')
	$output.Add('## Other Mods By Me')
	$output.Add('')
	$output.Add('See [all my mods on Thunderstore](' + $teamUrl + ').')
	$output.Add('')
	$output.Add('## Source')
	$output.Add('')
	$output.Add('The source code is on [GitHub](' + $treeUrl + ').')
	return(($output -join "`n") + "`n")
}

<#
.SYNOPSIS
Reads a mod's Thunderstore categories and checks them against the community's category list

.DESCRIPTION
The mod's own categories come from <Mod>\thunderstore-categories.txt, one slug per line with blank lines ignored, and
the shared ones are added to them. Every slug is checked against the community's category list, so a typo fails before
anything is published

.PARAMETER categoriesFile
Path to the mod's thunderstore-categories.txt

.PARAMETER sharedCategories
Categories added to every mod, e.g. the current game update's

.PARAMETER community
The Thunderstore community, e.g. valheim

.OUTPUTS
The category slugs, without duplicates
#>
function Get-ThunderstoreCategories([string]$categoriesFile, [string[]]$sharedCategories, [string]$community)
{
	if (-not (Test-Path $categoriesFile))
	{
		throw ('No Thunderstore categories at ' + $categoriesFile + ' - add one slug per line')
	}
	$categories = @(Get-Content $categoriesFile -Encoding UTF8 | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
	$categories = @(($categories + $sharedCategories) | Select-Object -Unique)

	$known = @{}
	$url = $thunderstoreApi + 'community/' + $community + '/category/'
	while ($url)
	{
		$page = Invoke-JsonApi 'GET' $url $null $null
		foreach ($category in $page.results)
		{
			$known[$category.slug] = $true
		}
		$url = $page.pagination.next_link
	}
	foreach ($category in $categories)
	{
		if (-not $known.ContainsKey($category))
		{
			throw ('"' + $category + '" is not a Thunderstore ' + $community + ' category - the valid slugs are ' + (($known.Keys | Sort-Object) -join ', '))
		}
	}
	return(,$categories)
}

<#
.SYNOPSIS
Checks a package's manifest.json and icon.png with Thunderstore's validators

.DESCRIPTION
README.md isn't checked, since the README validator returns a server error for every valid request

.PARAMETER token
The Thunderstore service account token

.PARAMETER team
The team the package belongs to

.PARAMETER manifest
The manifest's JSON text

.PARAMETER iconFile
Path to icon.png
#>
function Test-ThunderstorePackage([string]$token, [string]$team, [string]$manifest, [string]$iconFile)
{
	$headers = @{ Authorization = ('Bearer ' + $token) }
	$manifestData = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($manifest))
	$result = Invoke-JsonApi 'POST' ($thunderstoreApi + 'submission/validate/manifest-v1/') $headers ([ordered]@{ namespace = $team; manifest_data = $manifestData })
	if (-not $result.success)
	{
		throw ('Thunderstore rejected manifest.json: ' + (ConvertTo-Json -InputObject $result -Depth 5 -Compress))
	}
	$iconData = [Convert]::ToBase64String([IO.File]::ReadAllBytes($iconFile))
	$result = Invoke-JsonApi 'POST' ($thunderstoreApi + 'submission/validate/icon/') $headers ([ordered]@{ icon_data = $iconData })
	if (-not $result.success)
	{
		throw ('Thunderstore rejected ' + $iconFile + ': ' + (ConvertTo-Json -InputObject $result -Depth 5 -Compress))
	}
}

<#
.SYNOPSIS
Checks whether a package version is already on Thunderstore

.PARAMETER team
The team the package belongs to

.PARAMETER name
The package name

.PARAMETER version
The version

.OUTPUTS
True when the version exists
#>
function Test-ThunderstoreVersion([string]$team, [string]$name, [string]$version)
{
	$response = Invoke-Http 'GET' ($thunderstoreApi + 'package/' + $team + '/' + $name + '/' + $version + '/') $null $null $null $null
	if ($response.StatusCode -eq 404)
	{
		return($false)
	}
	if (-not $response.Success)
	{
		throw ('Checking for ' + $team + '-' + $name + '-' + $version + ' on Thunderstore returned ' + $response.StatusCode + ': ' + $response.Text)
	}
	return($true)
}

<#
.SYNOPSIS
Uploads a package zip to Thunderstore and submits it

.DESCRIPTION
Starts an upload, PUTs each part the server asks for to its presigned URL with its MD5, finishes the upload and submits
it to the community with the given categories. The upload is aborted when a part or the finish fails

.PARAMETER token
The Thunderstore service account token

.PARAMETER zipPath
Path to the package zip

.PARAMETER team
The team the package belongs to

.PARAMETER community
The Thunderstore community, e.g. valheim

.PARAMETER categories
The community category slugs

.OUTPUTS
The new version's download URL
#>
function Publish-ThunderstorePackage([string]$token, [string]$zipPath, [string]$team, [string]$community, [string[]]$categories)
{
	$headers = @{ Authorization = ('Bearer ' + $token) }
	$bytes = [IO.File]::ReadAllBytes($zipPath)
	$initiate = Invoke-JsonApi 'POST' ($thunderstoreApi + 'usermedia/initiate-upload/') $headers ([ordered]@{ filename = [IO.Path]::GetFileName($zipPath); file_size_bytes = $bytes.Length })
	$uuid = $initiate.user_media.uuid
	try
	{
		$parts = New-Object System.Collections.Generic.List[object]
		foreach ($part in $initiate.upload_urls)
		{
			$chunk = New-Object byte[] ([int]$part.length)
			[Array]::Copy($bytes, [long]$part.offset, $chunk, 0, [int]$part.length)
			$contentHeaders = @{ 'Content-MD5' = [Convert]::ToBase64String((Get-Md5 $chunk)) }
			$response = Invoke-Http 'PUT' $part.url $null $chunk $null $contentHeaders
			if (-not $response.Success)
			{
				throw ('Uploading part ' + $part.part_number + ' to Thunderstore returned ' + $response.StatusCode + ': ' + $response.Text)
			}
			if ($response.ETag -eq '')
			{
				throw ('Thunderstore returned no ETag for part ' + $part.part_number)
			}
			$parts.Add([ordered]@{ ETag = $response.ETag; PartNumber = [int]$part.part_number })
		}
		Invoke-JsonApi 'POST' ($thunderstoreApi + 'usermedia/' + $uuid + '/finish-upload/') $headers ([ordered]@{ parts = @($parts.ToArray()) }) | Out-Null
	}
	catch
	{
		Invoke-Http 'POST' ($thunderstoreApi + 'usermedia/' + $uuid + '/abort-upload/') $headers $null $null $null | Out-Null
		throw
	}

	$submission = [ordered]@{
		author_name = $team
		communities = @($community)
		community_categories = @{ $community = @($categories) }
		has_nsfw_content = $false
		upload_uuid = $uuid
	}
	$result = Invoke-JsonApi 'POST' ($thunderstoreApi + 'submission/submit/') $headers $submission
	return($result.package_version.download_url)
}
