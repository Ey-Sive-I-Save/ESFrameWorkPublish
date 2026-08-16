[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error $Message
    exit 1
}

$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$warningsRoot = Join-Path $root 'Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）'
$catalogPath = Join-Path $warningsRoot 'AIWarningsRouteCatalog.json'
$statusPath = Join-Path $warningsRoot '当前状态（CurrentStatus）.md'
$indexPath = Join-Path $warningsRoot '规则索引（RuleIndex）.md'
$collaborationPath = Join-Path $root 'Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/AgentSkills与AICommands协作边界_AI协作警告.md'

foreach ($path in @($catalogPath, $statusPath, $indexPath, $collaborationPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Fail "Required AIWarnings file is missing: $path"
    }
}

try {
    $catalog = Get-Content -LiteralPath $catalogPath -Raw -Encoding utf8 | ConvertFrom-Json
}
catch {
    Fail "Route catalog is not valid JSON: $($_.Exception.Message)"
}

if ($catalog.schemaVersion -ne 1 -or $catalog.catalogStatus -ne 'incremental') {
    Fail 'Route catalog schemaVersion/catalogStatus is invalid.'
}

$seenIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($route in @($catalog.routes)) {
    if ([string]::IsNullOrWhiteSpace($route.id) -or -not $seenIds.Add($route.id)) {
        Fail 'Each route needs one unique non-empty id.'
    }

    if ($route.state -notin @('current', 'reserved')) {
        Fail "Route '$($route.id)' has an invalid state."
    }

    if (@($route.mustRead).Count -eq 0) {
        Fail "Route '$($route.id)' has no mustRead paths."
    }

    foreach ($relativePath in @($route.mustRead)) {
        $fullPath = Join-Path $root $relativePath
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            Fail "Route '$($route.id)' references a missing mustRead path: $relativePath"
        }
    }

    if ($route.state -eq 'reserved' -and $route.implementationStatus -ne 'not-implemented') {
        Fail "Reserved route '$($route.id)' must explicitly state not-implemented."
    }
}

$statusText = Get-Content -LiteralPath $statusPath -Raw -Encoding utf8
if ($statusText.Length -gt 6000) {
    Fail 'CurrentStatus exceeds the short active-index budget.'
}

if ($statusText -notmatch '状态：现行导航 / 活跃索引') {
    Fail 'CurrentStatus is missing the active-index status marker.'
}

$transientBuildPatterns = @(
    '(?im)\bCS\d{4}\b',
    '(?im)\b\d+\s+warnings?\b',
    '(?im)\b\d+\s+errors?\b',
    '\d+\s*个[^\r\n]{0,20}警告'
)
foreach ($pattern in $transientBuildPatterns) {
    if ($statusText -match $pattern) {
        Fail "CurrentStatus contains transient build diagnostics matching '$pattern'."
    }
}

$indexText = Get-Content -LiteralPath $indexPath -Raw -Encoding utf8
if ($indexText -notmatch 'AIWarningsRouteCatalog\.json' -or $indexText -notmatch 'runtime-ui-window') {
    Fail 'RuleIndex is missing the route catalog or reserved runtime-ui-window route.'
}

$collaborationText = Get-Content -LiteralPath $collaborationPath -Raw -Encoding utf8
if ($collaborationText -notmatch 'NoMatchingCommand') {
    Fail 'AICommand collaboration rules are missing the NoMatchingCommand fallback.'
}

Write-Output "AIWarnings validation passed: $($catalog.routes.Count) catalog routes, CurrentStatus length $($statusText.Length)."
