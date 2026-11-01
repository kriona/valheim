<#
.SYNOPSIS
Functions shared by build-release.ps1 and build-bbcode.ps1 - dot-source it rather than running it

.DESCRIPTION
Reads the root README's "## Mods" table, the GitHub repository from the origin remote and secrets from .env, and sends
HTTP requests to the Thunderstore and Nexus Mods APIs
#>

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
Reads the table under the "## Mods" heading in the root README.md

.DESCRIPTION
Each row is "| [Name](Folder) | Description | Nexus Mods link | Thunderstore link |", where the link cells can be empty
for a mod that has no page yet

.PARAMETER readmeFile
Path to the root README.md

.OUTPUTS
An object per mod with Folder, Name, Description, NexusUrl and ThunderstoreUrl, where a URL is an empty string when its
cell has no link
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
		$urls = @('', '')
		for ($i = 0; $i -lt 2; $i++)
		{
			if ($cells.Count -gt ($i + 2) -and $cells[$i + 2] -match 'https?://[^)\s]+')
			{
				$urls[$i] = $matches[0]
			}
		}
		$mods += [PSCustomObject]@{ Folder = $folder; Name = $name; Description = $cells[1]; NexusUrl = $urls[0]; ThunderstoreUrl = $urls[1] }
	}
	return(,$mods)
}

<#
.SYNOPSIS
Reads the GitHub owner and repository from the origin remote

.PARAMETER root
The repository's root folder

.OUTPUTS
An object with Owner and Repo
#>
function Get-GitHubRepo([string]$root)
{
	$remote = (git -C $root remote get-url origin).Trim()
	if ($remote -notmatch 'github\.com[:/]([^/]+)/(.+?)(\.git)?$')
	{
		throw ('The origin remote is not a GitHub repository: ' + $remote)
	}
	return([PSCustomObject]@{ Owner = $matches[1]; Repo = $matches[2] })
}

<#
.SYNOPSIS
Reads a secret from an environment variable, or failing that from the .env file in the repository's root

.DESCRIPTION
The .env file holds "KEY=value" lines, with blank lines and lines starting with # ignored. An environment variable of the
same name takes precedence

.PARAMETER root
The repository's root folder

.PARAMETER name
The variable's name, e.g. THUNDERSTORE_TOKEN

.OUTPUTS
The value, or an empty string when it isn't set
#>
function Get-Secret([string]$root, [string]$name)
{
	$value = [Environment]::GetEnvironmentVariable($name)
	if ($value)
	{
		return($value.Trim())
	}
	$envFile = Join-Path $root '.env'
	if (-not (Test-Path $envFile))
	{
		return('')
	}
	foreach ($line in Get-Content $envFile -Encoding UTF8)
	{
		$line = $line.Trim()
		if ($line -eq '' -or $line.StartsWith('#'))
		{
			continue
		}
		$index = $line.IndexOf('=')
		if ($index -gt 0 -and $line.Substring(0, $index).Trim() -eq $name)
		{
			return($line.Substring($index + 1).Trim())
		}
	}
	return('')
}

<#
.SYNOPSIS
Sends an HTTP request

.DESCRIPTION
Uses HttpClient rather than Invoke-WebRequest, since presigned upload URLs need exact control over the content headers
and Invoke-WebRequest in Windows PowerShell adds a Content-Type of its own

.PARAMETER method
The HTTP method, e.g. GET

.PARAMETER url
The URL to send it to

.PARAMETER headers
Request headers, or $null for none

.PARAMETER body
A string, a byte array or $null for no body

.PARAMETER contentType
The Content-Type for a string body - a byte array body is sent without one

.PARAMETER contentHeaders
Content headers such as Content-MD5 or Content-Disposition, or $null for none

.OUTPUTS
An object with StatusCode, Success, Text (the response body) and ETag (empty when the response has none)
#>
function Invoke-Http([string]$method, [string]$url, [hashtable]$headers, [object]$body, [string]$contentType, [hashtable]$contentHeaders)
{
	if (-not $script:httpClient)
	{
		Add-Type -AssemblyName System.Net.Http
		[Net.ServicePointManager]::SecurityProtocol = ([Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12)
		$script:httpClient = New-Object System.Net.Http.HttpClient
		$script:httpClient.Timeout = [TimeSpan]::FromMinutes(10)
	}
	$request = New-Object System.Net.Http.HttpRequestMessage((New-Object System.Net.Http.HttpMethod($method)), $url)
	if ($headers)
	{
		foreach ($key in $headers.Keys)
		{
			$request.Headers.TryAddWithoutValidation($key, [string]$headers[$key]) | Out-Null
		}
	}
	if ($null -ne $body)
	{
		if ($body -is [byte[]])
		{
			$request.Content = New-Object System.Net.Http.ByteArrayContent -ArgumentList (,$body)
		}
		else
		{
			$request.Content = New-Object System.Net.Http.StringContent([string]$body, [Text.Encoding]::UTF8, $contentType)
		}
		if ($contentHeaders)
		{
			foreach ($key in $contentHeaders.Keys)
			{
				$request.Content.Headers.TryAddWithoutValidation($key, [string]$contentHeaders[$key]) | Out-Null
			}
		}
	}
	$response = $script:httpClient.SendAsync($request).GetAwaiter().GetResult()
	try
	{
		$text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
		$etag = ''
		if ($response.Headers.ETag)
		{
			$etag = $response.Headers.ETag.Tag
		}
		return([PSCustomObject]@{ StatusCode = [int]$response.StatusCode; Success = $response.IsSuccessStatusCode; Text = $text; ETag = $etag })
	}
	finally
	{
		$response.Dispose()
		$request.Dispose()
	}
}

<#
.SYNOPSIS
Sends a JSON request to an API and parses the JSON response, failing on any status other than 2xx

.PARAMETER method
The HTTP method, e.g. POST

.PARAMETER url
The URL to send it to

.PARAMETER headers
Request headers such as the API key, or $null for none

.PARAMETER data
An object to send as the JSON body, or $null for no body

.OUTPUTS
The parsed response, or $null when the response has no body
#>
function Invoke-JsonApi([string]$method, [string]$url, [hashtable]$headers, [object]$data)
{
	$body = $null
	if ($null -ne $data)
	{
		$body = ConvertTo-Json -InputObject $data -Depth 10 -Compress
	}
	$response = Invoke-Http $method $url $headers $body 'application/json' $null
	if (-not $response.Success)
	{
		throw ($method + ' ' + $url + ' returned ' + $response.StatusCode + ': ' + $response.Text)
	}
	if ($response.Text.Trim() -eq '')
	{
		return($null)
	}
	return($response.Text | ConvertFrom-Json)
}

<#
.SYNOPSIS
Returns the MD5 digest of some bytes

.PARAMETER bytes
The bytes to hash

.OUTPUTS
The 16-byte digest
#>
function Get-Md5([byte[]]$bytes)
{
	$md5 = [System.Security.Cryptography.MD5]::Create()
	try
	{
		return(,$md5.ComputeHash($bytes))
	}
	finally
	{
		$md5.Dispose()
	}
}
