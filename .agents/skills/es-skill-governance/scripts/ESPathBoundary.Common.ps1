function Resolve-ESContainedRelativePath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Candidate,
        [Parameter(Mandatory = $true)][string]$ContainerRoot,
        [string]$Label = 'Path'
    )

    if ([string]::IsNullOrWhiteSpace($Candidate) -or $Candidate -ne $Candidate.Trim()) {
        throw "$Label must be a non-empty project-relative path without surrounding whitespace."
    }
    if ([IO.Path]::IsPathRooted($Candidate) -or $Candidate -match '^[a-zA-Z]:' -or $Candidate -match '^[\\/]{2}') {
        throw "$Label must be relative to its managed root."
    }
    if ($Candidate.Contains(':')) {
        throw "$Label must not use an alternate data stream."
    }

    $container = [IO.Path]::GetFullPath($ContainerRoot).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    $full = [IO.Path]::GetFullPath([IO.Path]::Combine($container, $Candidate.Replace('/', '\')))
    $prefix = $container + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label escapes ProjectRoot or its managed Skill root."
    }

    $relative = $full.Substring($prefix.Length)
    $cursor = $container
    foreach ($segment in @($relative -split '[\\/]')) {
        if ([string]::IsNullOrWhiteSpace($segment)) { continue }
        $cursor = Join-Path $cursor $segment
        if (-not (Test-Path -LiteralPath $cursor)) { break }
        $item = Get-Item -LiteralPath $cursor -Force
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Label crosses a reparse point."
        }
    }

    return [pscustomobject]@{
        FullPath = $full
        RelativePath = $relative.Replace('\', '/')
    }
}
