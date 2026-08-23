[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string[]]$Scope,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()

# Static boundary contract: this is a read-only/report-only audit. All derived
# paths are constrained to the Git project root; no file mutation is performed.

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (& git rev-parse --show-toplevel 2>$null)
}
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    throw 'Cannot resolve the Git project root. Pass -ProjectRoot.'
}
$ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot.Trim())

foreach ($scopePath in @($Scope)) {
    if ([IO.Path]::IsPathRooted($scopePath)) { throw 'Scope must be project-relative; external expansion is denied.' }
    $scopeFull = [IO.Path]::GetFullPath((Join-Path $ProjectRoot $scopePath))
    $rootNormalized = $ProjectRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    if (-not ($scopeFull.Equals($rootNormalized, [StringComparison]::OrdinalIgnoreCase) -or $scopeFull.StartsWith($rootNormalized + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))) {
        throw 'Scope escapes ProjectRoot; external expansion is denied.'
    }
}

$branch = (& git -C $ProjectRoot branch --show-current).Trim()
$head = (& git -C $ProjectRoot rev-parse HEAD).Trim()
$statusLines = @(& git -C $ProjectRoot -c core.quotepath=false status --porcelain=v1 --untracked-files=all)
$items = New-Object Collections.Generic.List[object]

foreach ($line in $statusLines) {
    if ([string]::IsNullOrWhiteSpace($line) -or $line.Length -lt 4) { continue }
    $indexStatus = [string]$line[0]
    $worktreeStatus = [string]$line[1]
    $path = $line.Substring(3)
    $overlaps = $false
    foreach ($scopePath in @($Scope)) {
        $normalizedScope = $scopePath.Replace([IO.Path]::DirectorySeparatorChar, '/').TrimEnd('/')
        $normalizedPath = $path.Replace([IO.Path]::DirectorySeparatorChar, '/')
        if ($normalizedPath.Equals($normalizedScope, [StringComparison]::OrdinalIgnoreCase) -or $normalizedPath.StartsWith($normalizedScope + '/', [StringComparison]::OrdinalIgnoreCase)) {
            $overlaps = $true
            break
        }
    }

    $items.Add([pscustomobject]@{
        index = $indexStatus
        worktree = $worktreeStatus
        path = $path
        untracked = $indexStatus -eq '?' -and $worktreeStatus -eq '?'
        deleted = $indexStatus -eq 'D' -or $worktreeStatus -eq 'D'
        renamed = $indexStatus -eq 'R' -or $worktreeStatus -eq 'R'
        overlapsScope = $overlaps
    })
}

$report = [pscustomobject]@{
    projectRoot = $ProjectRoot
    branch = $branch
    head = $head
    total = $items.Count
    staged = @($items | Where-Object { $_.index -notin @(' ', '?') }).Count
    unstaged = @($items | Where-Object { $_.worktree -notin @(' ', '?') }).Count
    untracked = @($items | Where-Object { $_.untracked }).Count
    deleted = @($items | Where-Object { $_.deleted }).Count
    renamed = @($items | Where-Object { $_.renamed }).Count
    overlapping = @($items | Where-Object { $_.overlapsScope }).Count
    files = $items.ToArray()
}

if ($Json) {
    $report | ConvertTo-Json -Depth 6
}
else {
    $report | Select-Object projectRoot, branch, head, total, staged, unstaged, untracked, deleted, renamed, overlapping | Format-List
    $items | Format-Table index, worktree, overlapsScope, path -AutoSize
}
