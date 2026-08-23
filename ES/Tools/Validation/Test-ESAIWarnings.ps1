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
$hotContainerRulePath = Join-Path $root 'Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/运行时性能（RuntimePerformance）/项目最高警告_P0_热路径容器预热与稳态GC边界_AI协作警告.md'
$deliveryRulePath = Join-Path $root 'Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md'

foreach ($path in @($catalogPath, $statusPath, $indexPath, $collaborationPath, $hotContainerRulePath, $deliveryRulePath)) {
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

$hotContainerRoutes = @($catalog.routes | Where-Object { $_.id -eq 'runtime-hot-container' })
if ($hotContainerRoutes.Count -ne 1 -or $hotContainerRoutes[0].state -ne 'current') {
    Fail 'Route catalog needs exactly one current runtime-hot-container route.'
}

$hotContainerRouteText = ($hotContainerRoutes[0].match -join ' ')
if ($hotContainerRouteText -notmatch '排序' -or $hotContainerRouteText -notmatch '工作区' -or $hotContainerRouteText -notmatch '0GC') {
    Fail 'runtime-hot-container route is missing Chinese sorting, workspace, or zero-GC match coverage.'
}

$statusText = Get-Content -LiteralPath $statusPath -Raw -Encoding utf8
if ($statusText.Length -gt 6000) {
    Fail 'CurrentStatus exceeds the short active-index budget.'
}

if ($statusText -notmatch '状态：现行导航 / 活跃索引') {
    Fail 'CurrentStatus is missing the active-index status marker.'
}

if ($statusText -notmatch 'runtime-hot-container') {
    Fail 'CurrentStatus is missing the current runtime-hot-container route.'
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
if ($indexText -notmatch 'AIWarningsRouteCatalog\.json' -or $indexText -notmatch 'runtime-ui-window' -or $indexText -notmatch '辅助工作区') {
    Fail 'RuleIndex is missing the route catalog, runtime-ui-window, or hot-container workspace routing.'
}

$hotContainerRuleText = Get-Content -LiteralPath $hotContainerRulePath -Raw -Encoding utf8
$requiredHotContainerContracts = @(
    'es.p0.runtime.hot-container-steady-gc',
    '实现前的结果与工作区合同',
    '工作区必须按真实生命周期选择所有者',
    '首次调用、预热后连续调用、容量未变化、容量突破'
)
foreach ($contract in $requiredHotContainerContracts) {
    if ($hotContainerRuleText -notmatch [regex]::Escape($contract)) {
        Fail "Hot-container P0 is missing required contract: $contract"
    }
}

$deliveryRuleText = Get-Content -LiteralPath $deliveryRulePath -Raw -Encoding utf8
$requiredPerformanceDeliveryContracts = @(
    '性能与分配任务附加合同',
    '结果身份：',
    '工作区所有权：',
    '只完成编译或只在源码中未发现显式'
)
foreach ($contract in $requiredPerformanceDeliveryContracts) {
    if ($deliveryRuleText -notmatch [regex]::Escape($contract)) {
        Fail "Delivery P0 is missing required performance contract: $contract"
    }
}

$collaborationText = Get-Content -LiteralPath $collaborationPath -Raw -Encoding utf8
if ($collaborationText -notmatch 'NoMatchingCommand') {
    Fail 'AICommand collaboration rules are missing the NoMatchingCommand fallback.'
}

Write-Output "AIWarnings validation passed: $($catalog.routes.Count) catalog routes, CurrentStatus length $($statusText.Length)."
