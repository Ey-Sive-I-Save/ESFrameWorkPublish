[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SessionPath,

    [Parameter(Mandatory = $true)]
    [string]$ArchivePath,

    [string]$ProjectPath = '',

    [string]$ProjectIdentityFingerprint = '',

    [string]$TaskKey = '',

    [string]$ResponsibilityKey = '',

    [string]$TabTitle = '',

    [string]$TaskPrompt = '',

    [ValidateSet('Auto', 'CurrentWindow', 'ProjectWindow', 'NewWindow', 'PlainCmd')]
    [string]$TerminalMode = 'ProjectWindow',

    [string[]]$AdditionalHandoffPath = @(),

    [switch]$OpenNew,

    [switch]$CloseSource,

    [switch]$ForceNew,

    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

function Get-ResolvedFile([string]$Path, [string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Name 不存在：$Path"
    }
    return (Resolve-Path -LiteralPath $Path).Path
}

function Test-PathInside([string]$Path, [string]$Root) {
    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    return $fullPath.Equals($fullRoot, [StringComparison]::OrdinalIgnoreCase) -or $fullPath.StartsWith($fullRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}

function Get-ArchiveId([string]$Path) {
    foreach ($line in (Get-Content -LiteralPath $Path -TotalCount 24 -Encoding UTF8)) {
        $match = [Regex]::Match($line, '窗口档案ID：`([^`]+)`')
        if ($match.Success) { return $match.Groups[1].Value }
    }
    throw "交接档案缺少窗口档案ID：$Path"
}

function Get-SessionId([string]$Path) {
    foreach ($line in (Get-Content -LiteralPath $Path -Encoding UTF8)) {
        try {
            $row = $line | ConvertFrom-Json
            if ($row.type -eq 'session_meta' -and $row.payload -and -not [string]::IsNullOrWhiteSpace([string]$row.payload.id)) {
                return [string]$row.payload.id
            }
        }
        catch { throw "Session JSONL 无法解析：$Path；$($_.Exception.Message)" }
    }
    throw "Session JSONL 缺少 session_meta.id：$Path"
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-ProjectIdentityFingerprint([string]$Root) {
    $parts = foreach ($relative in @('AGENTS.md','ProjectSettings/ProjectVersion.txt')) {
        $full = [IO.Path]::GetFullPath((Join-Path $Root $relative))
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "Project identity file is missing: $relative" }
        $hash = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant()
        "$relative|$hash"
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes((($parts | Sort-Object) -join "`n")))).Replace('-','').ToLowerInvariant()) }
    finally { $sha.Dispose() }
}

function Write-CreateOnlyJson([string]$Path, [object]$Value) {
    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try {
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($Value | ConvertTo-Json -Depth 10))
        $stream.Write($bytes, 0, $bytes.Length)
    }
    finally { $stream.Dispose() }
}

if ($CloseSource -and -not $OpenNew) { throw '-CloseSource 必须与 -OpenNew 一起使用。' }
if ($ForceNew -and -not $OpenNew) { throw '-ForceNew 必须与 -OpenNew 一起使用；普通重试不得强制开新窗口。' }

$sessionFullPath = Get-ResolvedFile $SessionPath 'SessionPath'
$archiveFullPath = Get-ResolvedFile $ArchivePath 'ArchivePath'

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $historyDirectory = Split-Path -Parent $archiveFullPath
    $esDirectory = Split-Path -Parent $historyDirectory
    $ProjectPath = Split-Path -Parent $esDirectory
}
$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
if (-not (Test-PathInside $archiveFullPath $projectRoot)) { throw "ArchivePath 必须位于项目根内：$archiveFullPath" }
$actualProjectIdentityFingerprint = Get-ProjectIdentityFingerprint $projectRoot
if ([string]::IsNullOrWhiteSpace($ProjectIdentityFingerprint)) { $ProjectIdentityFingerprint = $actualProjectIdentityFingerprint }
if ($ProjectIdentityFingerprint -cne $actualProjectIdentityFingerprint) { throw 'Project identity fingerprint does not match the selected ProjectPath.' }

$archiveId = Get-ArchiveId $archiveFullPath
$sourceSessionId = Get-SessionId $sessionFullPath
if ($TaskKey -eq '') { $TaskKey = 'handoff-' + $archiveId }
if ($TaskKey -notmatch '^[A-Za-z0-9._:-]{1,128}$') { throw "TaskKey 不是安全稳定标识：$TaskKey" }
if (-not [string]::IsNullOrWhiteSpace($ResponsibilityKey) -and $ResponsibilityKey -notmatch '^[A-Za-z0-9._:-]{1,128}$') { throw "ResponsibilityKey 不是安全稳定标识：$ResponsibilityKey" }

$coverageTool = Join-Path $projectRoot 'ES\AI协作历程（Codex）\Tools\Test-ESCodexTimelineCoverage.ps1'
$responsibilityAssessmentTool = Join-Path $projectRoot '.agents\skills\es-codex-session-bootstrap\scripts\Get-ESCodexResponsibilityAssessment.ps1'
$launcher = Join-Path $projectRoot '.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1'
$bootstrapSkill = Join-Path $projectRoot '.agents\skills\es-codex-session-bootstrap\SKILL.md'
$currentStatus = Join-Path $projectRoot 'Assets\Plugins\ES\AIWarnings\00_开始阅读（Start）\当前状态（CurrentStatus）.md'
$ruleIndex = Join-Path $projectRoot 'Assets\Plugins\ES\AIWarnings\00_开始阅读（Start）\规则索引（RuleIndex）.md'
foreach ($required in @($coverageTool, $responsibilityAssessmentTool, $launcher, $bootstrapSkill, $currentStatus, $ruleIndex)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "交接链路缺少必需入口：$required" }
}

$coverageOutput = @(& $coverageTool -SessionPath $sessionFullPath -ArchivePath $archiveFullPath 2>&1)
$coverageText = ($coverageOutput | ForEach-Object { [string]$_ }) -join "`n"
try { $coverageResult = $coverageText | ConvertFrom-Json }
catch { throw "历程覆盖校验没有返回合法结果；拒绝启动新窗口。" }
if (-not [bool]$coverageResult.Passed) { throw "历程覆盖校验失败；拒绝启动新窗口。" }

$assessmentOutput = @(& $responsibilityAssessmentTool -ArchivePath $archiveFullPath -ResponsibilityKey $ResponsibilityKey 2>&1)
$assessmentText = ($assessmentOutput | ForEach-Object { [string]$_ }) -join "`n"
try { $responsibilityAssessment = $assessmentText | ConvertFrom-Json }
catch { throw '职责评估没有返回合法结果；拒绝启动新窗口。' }
if ([string]$responsibilityAssessment.status -ne 'assessed') {
    throw "交接档案的职责主题不明确（状态=$([string]$responsibilityAssessment.status)，置信度=$([string]$responsibilityAssessment.confidence)）；拒绝猜测，请缩小档案或明确职责范围。"
}
if ([string]::IsNullOrWhiteSpace($ResponsibilityKey)) {
    $ResponsibilityKey = [string]$responsibilityAssessment.recommendedResponsibilityKey
}
if (-not [bool]$responsibilityAssessment.requestedMatchesRecommendation) {
    throw "ResponsibilityKey '$ResponsibilityKey' 与完整历史主职责 '$([string]$responsibilityAssessment.recommendedResponsibilityKey)' 不匹配；请按职责评估结果重新交接。"
}

$handoffPaths = [Collections.Generic.List[string]]::new()
foreach ($path in @($archiveFullPath, $bootstrapSkill, $currentStatus, $ruleIndex) + @($AdditionalHandoffPath)) {
    if ([string]::IsNullOrWhiteSpace($path)) { continue }
    $resolved = Get-ResolvedFile $path 'HandoffPath'
    if (-not (Test-PathInside $resolved $projectRoot)) { throw "HandoffPath 必须位于项目根内：$resolved" }
    if (-not $handoffPaths.Contains($resolved)) { $handoffPaths.Add($resolved) }
}

if ([string]::IsNullOrWhiteSpace($TaskPrompt)) {
    $TaskPrompt = "完成窗口交接：先验证并读取 immutable launch envelope 中的私有 handoff snapshot；读取窗口档案 $archiveId、CurrentStatus、RuleIndex 与 Bootstrap Skill；报告 ContextAccepted、当前分支/HEAD 和工作树状态。额外核对窗口档案中最近至少 10 轮对话的概览（每轮至少包含用户要求、当时答复摘要和剩余工作；若总轮次不足 10 轮则全部核对），不得只依据最近一两轮。仅依据最新源码与运行证据继续，不能把旧档案当作新的修改、Git、Unity 或发布授权。"
}

$common = @{
    Mode = 'New'
    ProjectPath = $projectRoot
    TaskPrompt = $TaskPrompt
    TaskKey = $TaskKey
    ResponsibilityKey = $ResponsibilityKey
    TerminalMode = $TerminalMode
    HandoffPath = [string[]]$handoffPaths.ToArray()
    HandoffMode = $true
    ProjectIdentityFingerprint = $ProjectIdentityFingerprint
}
$handoffAuthorization = [Guid]::NewGuid().ToString('N')
$common.HandoffAuthorization = $handoffAuthorization

function Invoke-HandoffAuthorized([scriptblock]$Invocation, [string]$Authorization, [hashtable]$Arguments = $null) {
    $previous = [string]$env:ES_CODEX_HANDOFF_AUTHORIZATION
    $env:ES_CODEX_HANDOFF_AUTHORIZATION = $Authorization
    try {
        if ($null -eq $Arguments) { & $Invocation }
        else { & $Invocation $Arguments }
    }
    finally {
        if ([string]::IsNullOrWhiteSpace($previous)) {
            Remove-Item Env:ES_CODEX_HANDOFF_AUTHORIZATION -ErrorAction SilentlyContinue
        }
        else { $env:ES_CODEX_HANDOFF_AUTHORIZATION = $previous }
    }
}
if ($TabTitle -ne '') { $common.TabTitle = $TabTitle }
if ($ForceNew) { $common.ForceNew = $true }

$validationOutput = @(Invoke-HandoffAuthorized {
    & $launcher -Mode Validate -ProjectPath $projectRoot -TaskPrompt $TaskPrompt -TaskKey $TaskKey -ResponsibilityKey $ResponsibilityKey -TerminalMode $TerminalMode -HandoffPath ([string[]]$handoffPaths.ToArray()) -HandoffMode -HandoffAuthorization $handoffAuthorization -ProjectIdentityFingerprint $ProjectIdentityFingerprint -DryRun 2>&1
} $handoffAuthorization)
$validation = @($validationOutput | Where-Object { $_.PSObject.Properties.Name -contains 'requiredPathsValid' } | Select-Object -Last 1)[0]
if ($null -eq $validation -or -not [bool]$validation.requiredPathsValid) { throw "Session Bootstrap Validate 失败。" }

if (-not $OpenNew -or $DryRun) {
    [pscustomobject]@{
        status = 'Prepared'
        archiveId = $archiveId
        sourceSessionId = $sourceSessionId
        taskKey = $TaskKey
        responsibilityKey = $ResponsibilityKey
        projectIdentityFingerprint = $ProjectIdentityFingerprint
        responsibilityAssessment = [ordered]@{
            status = [string]$responsibilityAssessment.status
            recommendedResponsibilityKey = [string]$responsibilityAssessment.recommendedResponsibilityKey
            confidence = [double]$responsibilityAssessment.confidence
            nodeCount = [int]$responsibilityAssessment.nodeCount
        }
        handoffFiles = @($handoffPaths)
        openNewRequired = $true
        closeSourceSupported = $true
        launchValidation = @($validation)
        nextCommand = "Complete-ESCodexHandoff.ps1 -SessionPath '$sessionFullPath' -ArchivePath '$archiveFullPath' -OpenNew -CloseSource -TaskKey '$TaskKey' -ResponsibilityKey '$ResponsibilityKey'"
    }
    return
}

$previousLaunchAuthorization = [string]$env:ES_CODEX_HANDOFF_AUTHORIZATION
$env:ES_CODEX_HANDOFF_AUTHORIZATION = $handoffAuthorization
try {
    # Keep the argument splat in this script scope so every handoff snapshot
    # reaches Start-ESCodexSession.ps1 on the real launch path.
    $launchOutput = & $launcher @common
}
finally {
    if ([string]::IsNullOrWhiteSpace($previousLaunchAuthorization)) {
        Remove-Item Env:ES_CODEX_HANDOFF_AUTHORIZATION -ErrorAction SilentlyContinue
    }
    else { $env:ES_CODEX_HANDOFF_AUTHORIZATION = $previousLaunchAuthorization }
}
$launchResult = @($launchOutput | Where-Object { $_.PSObject.Properties.Name -contains 'launchPhase' } | Select-Object -Last 1)[0]
if ($null -eq $launchResult) { throw 'Session Bootstrap 未返回结构化启动结果。' }

# 某些终端/Windows Terminal 情况下，启动器底层 cmd 会返回非零退出码，
# 但结构化结果已经明确报告目标窗口 ContextAccepted。此时不能把已接收的
# 交接误判为失败；只有没有结构化结果，或结构化结果明确失败，才拒绝继续。
$bootstrapExitCode = $LASTEXITCODE
if ($bootstrapExitCode -ne 0 -and -not [bool]$launchResult.contextAccepted) {
    throw "Session Bootstrap New 失败。退出码=$bootstrapExitCode；阶段=$([string]$launchResult.launchPhase)；原因=$([string]$launchResult.startupFailureReason)"
}

$contextAccepted = [bool]$launchResult.contextAccepted
$deliveryStatus = if ($contextAccepted) { 'ContextAccepted' } elseif ([bool]$launchResult.startupTimedOut) { 'PendingAcceptance' } elseif ([bool]$launchResult.startupFailed) { 'Failed' } else { [string]$launchResult.launchPhase }
$receiptRoot = Join-Path $env:LOCALAPPDATA 'ESFramework\CodexSessions\handoff-receipts'
$receiptName = $archiveId + '-' + ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')) + '.json'
$receiptPath = Join-Path $receiptRoot $receiptName
$receipt = [ordered]@{
    schemaVersion = 1
    immutable = $true
    createdUtc = [DateTime]::UtcNow.ToString('o')
    deliveryStatus = $deliveryStatus
    sourceSessionId = $sourceSessionId
    archiveId = $archiveId
    archivePath = $archiveFullPath
    archiveSha256 = Get-Sha256 $archiveFullPath
    taskKey = $TaskKey
    responsibilityKey = $ResponsibilityKey
    responsibilityAssessmentStatus = [string]$responsibilityAssessment.status
    responsibilityAssessmentKey = [string]$responsibilityAssessment.recommendedResponsibilityKey
    responsibilityAssessmentConfidence = [double]$responsibilityAssessment.confidence
    responsibilityAssessmentNodeCount = [int]$responsibilityAssessment.nodeCount
    targetSessionId = [string]$launchResult.sessionId
    launchToken = [string]$launchResult.launchToken
    envelopePath = [string]$launchResult.envelopePath
    contextAccepted = $contextAccepted
    closeSourceRequested = [bool]$CloseSource
    projectIdentityFingerprint = $ProjectIdentityFingerprint
}
Write-CreateOnlyJson $receiptPath $receipt

$closeResult = $null
if ($CloseSource) {
    if (-not $contextAccepted) { throw "新窗口尚未 ContextAccepted，拒绝关闭源窗口。Receipt：$receiptPath" }
    $closeResult = & $launcher -Mode Close -ProjectPath $projectRoot -SessionId $sourceSessionId
    if ($LASTEXITCODE -ne 0) { throw "新窗口已接收，但源窗口关闭失败；请使用 Close-ESCodexSession.ps1 按精确 SessionId 处理。Receipt：$receiptPath" }
}

[pscustomobject]@{
    status = if ($contextAccepted) { 'Delivered' } else { $deliveryStatus }
    archiveId = $archiveId
    sourceSessionId = $sourceSessionId
    targetSessionId = [string]$launchResult.sessionId
    contextAccepted = $contextAccepted
    launchPhase = [string]$launchResult.launchPhase
    envelopePath = [string]$launchResult.envelopePath
    acceptanceReceiptPath = [string]$launchResult.acceptanceReceiptPath
    handoffReceiptPath = $receiptPath
    closeSourceRequested = [bool]$CloseSource
    closeResult = $closeResult
}
