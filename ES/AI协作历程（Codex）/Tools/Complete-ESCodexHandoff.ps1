[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SessionPath,

    [Parameter(Mandatory = $true)]
    [string]$ArchivePath,

    [string]$ProjectPath = '',

    [string]$TaskKey = '',

    [string]$ResponsibilityKey = 'engineering-acceptance',

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

$archiveId = Get-ArchiveId $archiveFullPath
$sourceSessionId = Get-SessionId $sessionFullPath
if ($TaskKey -eq '') { $TaskKey = 'handoff-' + $archiveId }
if ($TaskKey -notmatch '^[A-Za-z0-9._:-]{1,128}$') { throw "TaskKey 不是安全稳定标识：$TaskKey" }
if ($ResponsibilityKey -notmatch '^[A-Za-z0-9._:-]{1,128}$') { throw "ResponsibilityKey 不是安全稳定标识：$ResponsibilityKey" }

$coverageTool = Join-Path $projectRoot 'ES\AI协作历程（Codex）\Tools\Test-ESCodexTimelineCoverage.ps1'
$launcher = Join-Path $projectRoot '.agents\skills\es-codex-session-bootstrap\scripts\Start-ESCodexSession.ps1'
$bootstrapSkill = Join-Path $projectRoot '.agents\skills\es-codex-session-bootstrap\SKILL.md'
$currentStatus = Join-Path $projectRoot 'Assets\Plugins\ES\AIWarnings\00_开始阅读（Start）\当前状态（CurrentStatus）.md'
$ruleIndex = Join-Path $projectRoot 'Assets\Plugins\ES\AIWarnings\00_开始阅读（Start）\规则索引（RuleIndex）.md'
foreach ($required in @($coverageTool, $launcher, $bootstrapSkill, $currentStatus, $ruleIndex)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "交接链路缺少必需入口：$required" }
}

$coverageOutput = @(& $coverageTool -SessionPath $sessionFullPath -ArchivePath $archiveFullPath 2>&1)
$coverageText = ($coverageOutput | ForEach-Object { [string]$_ }) -join "`n"
try { $coverageResult = $coverageText | ConvertFrom-Json }
catch { throw "历程覆盖校验没有返回合法结果；拒绝启动新窗口。" }
if (-not [bool]$coverageResult.Passed) { throw "历程覆盖校验失败；拒绝启动新窗口。" }

$handoffPaths = [Collections.Generic.List[string]]::new()
foreach ($path in @($archiveFullPath, $bootstrapSkill, $currentStatus, $ruleIndex) + @($AdditionalHandoffPath)) {
    if ([string]::IsNullOrWhiteSpace($path)) { continue }
    $resolved = Get-ResolvedFile $path 'HandoffPath'
    if (-not (Test-PathInside $resolved $projectRoot)) { throw "HandoffPath 必须位于项目根内：$resolved" }
    if (-not $handoffPaths.Contains($resolved)) { $handoffPaths.Add($resolved) }
}

if ([string]::IsNullOrWhiteSpace($TaskPrompt)) {
    $TaskPrompt = "完成窗口交接：先验证并读取 immutable launch envelope 中的私有 handoff snapshot；读取窗口档案 $archiveId、CurrentStatus、RuleIndex 与 Bootstrap Skill；报告 ContextAccepted、当前分支/HEAD 和工作树状态。仅依据最新源码与运行证据继续，不能把旧档案当作新的修改、Git、Unity 或发布授权。"
}

$common = @{
    Mode = 'New'
    ProjectPath = $projectRoot
    TaskPrompt = $TaskPrompt
    TaskKey = $TaskKey
    ResponsibilityKey = $ResponsibilityKey
    TerminalMode = $TerminalMode
    HandoffPath = [string[]]$handoffPaths.ToArray()
}
if ($TabTitle -ne '') { $common.TabTitle = $TabTitle }
if ($ForceNew) { $common.ForceNew = $true }

$validationOutput = @(& $launcher -Mode Validate -ProjectPath $projectRoot -TaskPrompt $TaskPrompt -TaskKey $TaskKey -ResponsibilityKey $ResponsibilityKey -TerminalMode $TerminalMode -HandoffPath ([string[]]$handoffPaths.ToArray()) -DryRun 2>&1)
$validation = @($validationOutput | Where-Object { $_.PSObject.Properties.Name -contains 'requiredPathsValid' } | Select-Object -Last 1)[0]
if ($null -eq $validation -or -not [bool]$validation.requiredPathsValid) { throw "Session Bootstrap Validate 失败。" }

if (-not $OpenNew -or $DryRun) {
    [pscustomobject]@{
        status = 'Prepared'
        archiveId = $archiveId
        sourceSessionId = $sourceSessionId
        taskKey = $TaskKey
        responsibilityKey = $ResponsibilityKey
        handoffFiles = @($handoffPaths)
        openNewRequired = $true
        closeSourceSupported = $true
        launchValidation = @($validation)
        nextCommand = "Complete-ESCodexHandoff.ps1 -SessionPath '$sessionFullPath' -ArchivePath '$archiveFullPath' -OpenNew -CloseSource -TaskKey '$TaskKey' -ResponsibilityKey '$ResponsibilityKey'"
    }
    return
}

$launchOutput = & $launcher @common
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
    targetSessionId = [string]$launchResult.sessionId
    launchToken = [string]$launchResult.launchToken
    envelopePath = [string]$launchResult.envelopePath
    contextAccepted = $contextAccepted
    closeSourceRequested = [bool]$CloseSource
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
