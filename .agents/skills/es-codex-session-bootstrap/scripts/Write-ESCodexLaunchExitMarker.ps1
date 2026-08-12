[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedRoot,

    [Parameter(Mandatory = $true)]
    [string]$LaunchToken,

    [Parameter(Mandatory = $true)]
    [int]$ExitCode
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
if ($LaunchToken -notmatch '^CodexLaunch:[A-Za-z0-9:-]+$') {
    throw 'Launch token is invalid for an exit marker.'
}

$root = [IO.Path]::GetFullPath($ExpectedRoot).TrimEnd('\')
$markerPath = [IO.Path]::GetFullPath($Path)
if (-not (Test-Path -LiteralPath $root -PathType Container) -or
    -not $markerPath.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Exit marker path escaped the managed command root.'
}

$marker = [ordered]@{
    schemaVersion = 1
    launchToken = $LaunchToken
    exitCode = $ExitCode
    createdUtc = [DateTime]::UtcNow.ToString('o')
} | ConvertTo-Json -Compress
$bytes = [Text.UTF8Encoding]::new($false).GetBytes($marker)
$stream = [IO.File]::Open($markerPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Flush($true)
}
finally {
    $stream.Dispose()
}
