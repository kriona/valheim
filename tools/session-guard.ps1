<#
.SYNOPSIS
Claude Code hook that keeps parallel Claude sessions in this repo from building while another one is editing code

.DESCRIPTION
Each session that edits a code file (.cs, .csproj, .props, .targets, .slnx) lists the files in
.claude\sessions\<session id>.txt until its turn ends. A build - or a dotnet test, which builds first - started while
another session has a file there is denied, and the files of the session whose build was skipped go into
.claude\sessions\build-pending.txt. The next build that runs covers them, and a session that ends its turn with nobody
else editing is told to build if one is still pending, so the last session to finish builds for both. A session file not
touched for 30 minutes is treated as left over from a session that was closed mid-turn

.PARAMETER Mode
edit (PostToolUse on Edit/Write), build (PreToolUse on Bash/PowerShell), stop (Stop) or end (SessionEnd)
#>

param(
	[Parameter(Mandatory = $true)]
	[ValidateSet('edit', 'build', 'stop', 'end')]
	[string]$Mode
)

$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$codeExtensions = @('.cs', '.csproj', '.props', '.targets', '.slnx')
$staleMinutes = 30
$buildPattern = '\bdotnet(\.exe)?\s+(build|msbuild|publish|rebuild|test)\b'

$repoPath = Split-Path $PSScriptRoot -Parent
$sessionsPath = Join-Path $repoPath '.claude\sessions'
$pendingPath = Join-Path $sessionsPath 'build-pending.txt'

$hookInput = [Console]::In.ReadToEnd() | ConvertFrom-Json
$sessionId = [string]$hookInput.session_id
if (-not $sessionId)
{
	exit 0
}
$ownPath = Join-Path $sessionsPath ($sessionId + '.txt')

<#
.SYNOPSIS
Reads the lines of a file, or none when it doesn't exist

.PARAMETER Path
The file to read

.OUTPUTS
The file's non-empty lines
#>
function Read-Lines
{
	param([string]$Path)

	if (-not (Test-Path $Path))
	{
		return(@())
	}
	$lines = @()
	foreach ($line in [System.IO.File]::ReadAllLines($Path))
	{
		if ($line.Trim())
		{
			$lines += $line.Trim()
		}
	}
	return($lines)
}

<#
.SYNOPSIS
Adds lines to a file, skipping any it already has

.PARAMETER Path
The file to add to, made along with its folder when missing

.PARAMETER Lines
The lines to add
#>
function Add-Lines
{
	param([string]$Path, [string[]]$Lines)

	$existing = @(Read-Lines $Path)
	$all = $existing
	foreach ($line in $Lines)
	{
		if ($all -notcontains $line)
		{
			$all += $line
		}
	}
	New-Item -ItemType Directory -Force $sessionsPath | Out-Null
	[System.IO.File]::WriteAllLines($Path, [string[]]$all)
}

<#
.SYNOPSIS
Finds the other sessions that are editing code right now, deleting session files left over from closed sessions

.OUTPUTS
One object per session with its Id and the Files it has edited
#>
function Get-OtherSessions
{
	$others = @()
	if (-not (Test-Path $sessionsPath))
	{
		return($others)
	}
	foreach ($file in Get-ChildItem $sessionsPath -Filter '*.txt')
	{
		if (($file.FullName -eq $ownPath) -or ($file.FullName -eq $pendingPath))
		{
			continue
		}
		if ($file.LastWriteTime -lt (Get-Date).AddMinutes(-$staleMinutes))
		{
			Remove-Item $file.FullName -Force -ErrorAction SilentlyContinue
			continue
		}
		$others += [pscustomobject]@{ Id = $file.BaseName; Files = @(Read-Lines $file.FullName) }
	}
	return($others)
}

<#
.SYNOPSIS
Turns an absolute path inside the repo into one relative to the repo root

.PARAMETER Path
The path to shorten

.OUTPUTS
The relative path, or the path unchanged when it's outside the repo
#>
function Get-RepoRelativePath
{
	param([string]$Path)

	$full = [System.IO.Path]::GetFullPath($Path)
	$root = $repoPath.TrimEnd('\') + '\'
	if ($full.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase))
	{
		return($full.Substring($root.Length))
	}
	return($full)
}

<#
.SYNOPSIS
Writes a hook result to stdout as JSON

.PARAMETER Result
The result object
#>
function Write-HookResult
{
	param([hashtable]$Result)

	[Console]::Out.Write(($Result | ConvertTo-Json -Compress -Depth 5))
}

if ($Mode -eq 'edit')
{
	$filePath = [string]$hookInput.tool_input.file_path
	if (-not $filePath)
	{
		$filePath = [string]$hookInput.tool_input.notebook_path
	}
	if ($filePath -and ($codeExtensions -contains [System.IO.Path]::GetExtension($filePath).ToLowerInvariant()))
	{
		Add-Lines $ownPath @(Get-RepoRelativePath $filePath)
	}
	exit 0
}

if ($Mode -eq 'build')
{
	$command = [string]$hookInput.tool_input.command
	if (-not ($command -match $buildPattern))
	{
		exit 0
	}
	if (Test-Path $ownPath)
	{
		(Get-Item $ownPath).LastWriteTime = Get-Date
	}

	$others = @(Get-OtherSessions)
	if ($others.Count -gt 0)
	{
		$otherFiles = @()
		foreach ($other in $others)
		{
			$otherFiles += $other.Files
		}
		$ownFiles = @(Read-Lines $ownPath)
		if ($ownFiles.Count -eq 0)
		{
			$ownFiles = @('(no code files - build requested by session ' + $sessionId + ')')
		}
		Add-Lines $pendingPath $ownFiles
		$reason = 'Build skipped: another Claude session is still editing code in this repo (' + (($otherFiles | Select-Object -Unique) -join ', ') + '). '
		$reason += 'It will build when it finishes and covers your changes too, so don''t build, wait or retry. '
		$reason += 'Finish the rest of your work and tell the user the build was left to the other session.'
		Write-HookResult @{
			hookSpecificOutput = @{
				hookEventName = 'PreToolUse'
				permissionDecision = 'deny'
				permissionDecisionReason = $reason
			}
		}
		exit 0
	}

	$pendingFiles = @(Read-Lines $pendingPath)
	if ($pendingFiles.Count -gt 0)
	{
		Remove-Item $pendingPath -Force -ErrorAction SilentlyContinue
		$context = 'This build also covers changes another Claude session made while you were editing, whose build was skipped: ' + ($pendingFiles -join ', ') + '. '
		$context += 'If it fails in those files, report the errors to the user instead of fixing them.'
		Write-HookResult @{
			hookSpecificOutput = @{
				hookEventName = 'PreToolUse'
				additionalContext = $context
			}
		}
	}
	exit 0
}

if ($Mode -eq 'stop')
{
	$hadEdits = Test-Path $ownPath
	$ownFiles = @(Read-Lines $ownPath)
	Remove-Item $ownPath -Force -ErrorAction SilentlyContinue
	if ((-not $hadEdits) -or ($hookInput.stop_hook_active -eq $true))
	{
		exit 0
	}

	$others = @(Get-OtherSessions)
	$pendingFiles = @(Read-Lines $pendingPath)
	if (($others.Count -eq 0) -and ($pendingFiles.Count -gt 0))
	{
		Add-Lines $ownPath $ownFiles
		$reason = 'Another Claude session''s build was skipped while you were editing (' + ($pendingFiles -join ', ') + ') and no other session is editing now. '
		$reason += 'Build the mod once more so its changes and yours are checked, and report any errors in its files to the user instead of fixing them.'
		Write-HookResult @{
			decision = 'block'
			reason = $reason
		}
	}
	exit 0
}

if ($Mode -eq 'end')
{
	Remove-Item $ownPath -Force -ErrorAction SilentlyContinue
	exit 0
}
