[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string[]]$Path,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (& git rev-parse --show-toplevel 2>$null)
}
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    throw 'Cannot resolve the Git project root. Pass -ProjectRoot.'
}
$ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot.Trim())
$explicitPathsProvided = $Path -and $Path.Count -gt 0

if (-not $Path -or $Path.Count -eq 0) {
    $changed = @()
    $changed += @(& git -C $ProjectRoot -c core.quotepath=false diff --name-only)
    $changed += @(& git -C $ProjectRoot -c core.quotepath=false diff --cached --name-only)
    $changed += @(& git -C $ProjectRoot -c core.quotepath=false ls-files --others --exclude-standard)
    $Path = @($changed | Where-Object { $_ } | Sort-Object -Unique)
}

$textExtensions = @('.cs','.md','.txt','.json','.yaml','.yml','.xml','.uxml','.uss','.shader','.hlsl','.cginc','.asmdef','.asmref','.csv','.tsv','.ps1','.py','.js','.ts','.toml')
$projectRootNormalized = $ProjectRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
function Test-InProjectRoot([string]$candidate) {
    $fullCandidate = [IO.Path]::GetFullPath($candidate).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    return $fullCandidate.Equals($projectRootNormalized, [StringComparison]::OrdinalIgnoreCase) -or $fullCandidate.StartsWith($projectRootNormalized + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}
$strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
$markers = @(
    ([string]([char]0x00E2) + [char]0x20AC),
    ([string]([char]0x00EF) + [char]0x00BF + [char]0x00BD),
    ([string]([char]0x951F) + [char]0x65A4 + [char]0x62F7)
)
$results = New-Object Collections.Generic.List[object]
$checkedRelativePaths = New-Object Collections.Generic.List[string]
$hasHardFailure = $false
$hasReview = $false

foreach ($item in ($Path | Sort-Object -Unique)) {
    $fullPath = if ([IO.Path]::IsPathRooted($item)) { [IO.Path]::GetFullPath($item) } else { [IO.Path]::GetFullPath((Join-Path $ProjectRoot $item)) }
    if (-not (Test-InProjectRoot $fullPath)) {
        $hasHardFailure = $true
        $results.Add([pscustomobject]@{ path = $item; valid = $false; issues = @('Path escapes ProjectRoot.'); review = @() })
        continue
    }
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        $hasHardFailure = $true
        $results.Add([pscustomobject]@{ path = $item; valid = $false; issues = @('Target file does not exist.'); review = @() })
        continue
    }
    if ($textExtensions -notcontains [IO.Path]::GetExtension($fullPath).ToLowerInvariant()) { continue }

    $issues = New-Object Collections.Generic.List[string]
    $review = New-Object Collections.Generic.List[string]
    try {
        $text = $strictUtf8.GetString([IO.File]::ReadAllBytes($fullPath))
        if ($text.Contains([char]0xFFFD)) { $issues.Add('Contains Unicode replacement character U+FFFD.') }
        foreach ($marker in $markers) {
            if ($text.Contains($marker)) {
                $codes = ($marker.ToCharArray() | ForEach-Object { 'U+{0:X4}' -f [int]$_ }) -join ' '
                $review.Add("Possible mojibake marker: $codes")
            }
        }
    }
    catch {
        $issues.Add("Strict UTF-8 decoding failed: $($_.Exception.Message)")
    }

    if ($issues.Count -gt 0) { $hasHardFailure = $true }
    if ($review.Count -gt 0) { $hasReview = $true }
    $relativePath = $fullPath.Substring($ProjectRoot.Length).TrimStart([IO.Path]::DirectorySeparatorChar).Replace([IO.Path]::DirectorySeparatorChar, '/')
    $checkedRelativePaths.Add($relativePath)
    $results.Add([pscustomobject]@{
        path = $relativePath
        valid = $issues.Count -eq 0
        issues = $issues.ToArray()
        review = $review.ToArray()
    })
}

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
if ($explicitPathsProvided -and $checkedRelativePaths.Count -gt 0) {
    $diffArguments = @('-C', $ProjectRoot, 'diff', '--check', '--') + $checkedRelativePaths.ToArray()
    $diffOutput = @(& git @diffArguments 2>&1)
}
else {
    $diffOutput = @(& git -C $ProjectRoot diff --check 2>&1)
}
$diffExitCode = $LASTEXITCODE
$ErrorActionPreference = $previousErrorActionPreference
if ($diffExitCode -ne 0) { $hasHardFailure = $true }

$report = [pscustomobject]@{
    projectRoot = $ProjectRoot
    checkedFileCount = $results.Count
    valid = -not $hasHardFailure -and -not $hasReview
    requiresReview = $hasReview
    diffCheckExitCode = $diffExitCode
    diffCheck = ($diffOutput -join [Environment]::NewLine)
    files = $results.ToArray()
}

if ($Json) {
    $report | ConvertTo-Json -Depth 8
}
else {
    "UTF-8 checked: $($report.checkedFileCount), hardFailure: $hasHardFailure, review: $hasReview"
    foreach ($item in $results | Where-Object { -not $_.valid -or $_.review.Count -gt 0 }) {
        "[$($item.path)]"
        foreach ($message in $item.issues) { "  - $message" }
        foreach ($message in $item.review) { "  - $message" }
    }
    if ($diffExitCode -ne 0) { $report.diffCheck }
}

if ($hasHardFailure) { exit 1 }
if ($hasReview) { exit 2 }
