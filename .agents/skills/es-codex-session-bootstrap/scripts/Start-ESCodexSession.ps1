[CmdletBinding()]
param(
    [ValidateSet('Validate', 'New', 'Resume', 'Fork', 'Close', 'RestoreRecent', 'List', 'Status', 'Focus', 'Repair', 'Reconcile', 'Query', 'Resolve', 'BindResponsibility', 'BindAcceptance', 'SetPresence', 'Wait', 'SendMessage', 'QueueMessage', 'MessageStatus', 'UpdateMessageStatus', 'MessageRepair', 'BrokerStatus', 'Doctor', 'SmokeTest', 'RequestAcceptance', 'ReplyAcceptance', 'AcceptanceStatus', 'SelfTest', 'PrepareExternalClaim', 'SubmitExternalClaimInput', 'FinalizeExternalClaim', 'CancelExternalClaim')]
    [string]$Mode = 'New',

    [string]$SessionId = '',

    [string]$RecordId = '',

    [string]$LaunchToken = '',

    [string]$ProjectPath = '',

    [string]$ProjectIdentityFingerprint = '',

    [string]$TaskPrompt = '',

    [string]$TaskKey = '',

    [string]$ResponsibilityKey = '',

    [string]$TabTitle = '',

    [ValidateSet('Auto', 'CurrentWindow', 'ProjectWindow', 'NewWindow', 'PlainCmd')]
    [string]$TerminalMode = 'Auto',

    [string]$TerminalWindowName = 'ESFramework',

    [string[]]$HandoffPath = @(),

    # A bounded, read-only context packet created by New-ESCodexReadOnlyContext.ps1.
    # This is never a Resume/Fork source and is accepted only from the private
    # per-user read-only context store.
    [string]$ReadOnlyContextPath = '',

    # Only Complete-ESCodexHandoff.ps1 may set this for a handoff-intent New.
    # Direct callers must not bypass timeline coverage and handoff receipts.
    [switch]$HandoffMode,

    # When set, the launch carries only bootstrap instructions. The task
    # prompt must be delivered by the caller after ContextAccepted=true.
    [switch]$DeferTaskPrompt,

    # Internal capability set by Complete-ESCodexHandoff.ps1 for the duration
    # of its Validate/New calls. A public switch alone must never authorize a
    # handoff because it would make the orchestrator optional.
    [string]$HandoffAuthorization = '',

    [switch]$ForceNew,

    # Disable Codex hooks for this launch only. The project hook configuration
    # remains unchanged; this is intended for explicitly bounded initialization
    # or restoration tasks that must not invoke hook delivery/closeout.
    [switch]$SkipHooks,

    [switch]$AllSessions,

    [switch]$AllMatches,

    [ValidateRange(1, 720)]
    [int]$RecentHours = 24,

    [ValidateRange(1, 50)]
    [int]$MaxSessions = 12,

    [switch]$IncludeUnclassified,

    [switch]$IncludeTests,

    [switch]$Apply,

    [switch]$Current,

    [string]$BindResponsibilityKey = '',

    [ValidateSet('', 'Unknown', 'Busy', 'Idle', 'Waiting')]
    [string]$Availability = '',

    [string]$ActivityKey = '',

    [string]$ActivitySummary = '',

    [ValidateRange(30, 86400)]
    [int]$PresenceTtlSeconds = 900,

    [ValidateSet('Ready', 'Active', 'Idle', 'Waiting', 'NotBusy', 'Terminal')]
    [string]$WaitFor = 'Ready',

    [ValidateRange(0, 60)]
    [int]$WaitSeconds = 30,

    [ValidateRange(250, 10000)]
    [int]$PollMilliseconds = 1000,

    [ValidateRange(1, 180)]
    [int]$StartupWaitSeconds = 60,

    [string]$MessageId = '',

    [string]$MessageBody = '',

    [string]$IdempotencyKey = '',

    [ValidateSet('low', 'normal', 'high')]
    [string]$MessagePriority = 'normal',

    [ValidateRange(30, 86400)]
    [int]$MessageTtlSeconds = 900,

    [switch]$RequireReady,

    [ValidateSet('', 'accepted', 'turn_started', 'steered', 'completed', 'failed', 'expired')]
    [string]$MessageStatus = '',

    [string]$MessageNote = '',

    [string]$AcceptanceResponsibilityKey = 'engineering-acceptance',

    [switch]$DisableAcceptanceBinding,

    [string]$RequesterRecordId = '',

    [string]$RequesterSessionId = '',

    [string]$RequesterLaunchToken = '',

    [string]$ExternalClaimId = '',

    [string]$ExternalClaimBindingId = '',

    [int]$ExternalClaimExpectedCmdProcessId = 0,

    [string]$ExternalClaimExpectedCmdProcessStartedAtUtc = '',

    [ValidateRange(60, 600)]
    [int]$ExternalClaimTtlSeconds = 300,

    # Isolated test seam. The Agent workbench never supplies this value; its
    # external-CMD claims always use the authoritative local session root.
    [string]$ExternalClaimStateRoot = '',

    [string]$ResponderRecordId = '',

    [string]$ResponderSessionId = '',

    [switch]$NoWaitForReply,

    [int]$ExpectedRegistryRevision = -1,

    [int]$ExpectedMessageRevision = -1,

    [switch]$ProbeAppServer,

    [ValidateRange(1, 3650)]
    [int]$MessageRetentionDays = 30,

    [switch]$DeleteTerminalMessages,

    [switch]$RunSelfTests,

    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
. (Join-Path $PSScriptRoot 'ESCodexSessionState.ps1')
. (Join-Path $PSScriptRoot 'ESCodexLaunchReadiness.ps1')

function Assert-HandoffResponsibility([string]$ResponsibilityKeyValue, [string]$TabTitleValue,
    [object[]]$ResolvedHandoffFiles) {
    if ($ResponsibilityKeyValue -match '(?i)handoff|handover|\u4ea4\u63a5|\u63a5\u624b|\u79fb\u4ea4|resume|fork|\u6062\u590d|\u5206\u53c9|close|\u5173\u95ed|bootstrap|\u542f\u52a8') {
        throw 'Handoff ResponsibilityKey must identify the receiving content responsibility, not a handoff/resume/fork/bootstrap operation.'
    }
    if ($TabTitleValue -match '(?i)handoff|handover|\u4ea4\u63a5|\u63a5\u624b|\u79fb\u4ea4|resume|fork|\u6062\u590d|\u5206\u53c9|close|\u5173\u95ed|bootstrap|\u542f\u52a8') {
        throw 'Handoff TabTitle must identify the receiving content responsibility, not a handoff/resume/fork/bootstrap operation.'
    }
    $normalized = @($ResolvedHandoffFiles | ForEach-Object { [string]$_.relativePath })
    $archivePrefix = 'ES/' + (New-TextFromCodePoints @(0x0041, 0x0049, 0x534F, 0x4F5C, 0x5386, 0x7A0B, 0xFF08, 0x0043, 0x006F, 0x0064, 0x0065, 0x0078, 0xFF09)) + '/'
    if (-not ($normalized | Where-Object { [string]$_ -like ($archivePrefix + '*') })) {
        throw ('Handoff launch must carry an ES/AI collaboration archive; use Complete-ESCodexHandoff.ps1. Received: ' + ($normalized -join '; '))
    }
}

function Assert-HandoffAuthorization([string]$AuthorizationValue) {
    $expected = [string]$env:ES_CODEX_HANDOFF_AUTHORIZATION
    if ([string]::IsNullOrWhiteSpace($expected) -or
        [string]::IsNullOrWhiteSpace($AuthorizationValue) -or
        -not [String]::Equals($AuthorizationValue, $expected, [StringComparison]::Ordinal)) {
        throw 'HandoffMode is reserved for Complete-ESCodexHandoff.ps1; missing orchestrator authorization.'
    }
}

function Get-Sha256([string]$Value) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($Value)
        return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-FileSha256([string]$Path) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
        $stream.Dispose()
    }
}

function Write-CreateOnlyUtf8File([string]$Path, [string]$Content) {
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Content)
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally {
        $stream.Dispose()
    }
}

function New-TextFromCodePoints([int[]]$CodePoints) {
    return -join @($CodePoints | ForEach-Object { [char]$_ })
}

function Get-ResponsibilityTitleLabel([string]$Key, [string]$ContextText) {
    $keyLower = ([string]$Key).ToLowerInvariant()
    $labels = @()
    $rules = @(
        @{ pattern = 'session'; label = (New-TextFromCodePoints @(0x4F1A,0x8BDD)) },
        @{ pattern = 'bootstrap'; label = (New-TextFromCodePoints @(0x5F15,0x5BFC)) },
        @{ pattern = 'maintenance'; label = (New-TextFromCodePoints @(0x7EF4,0x62A4)) },
        @{ pattern = 'editor'; label = (New-TextFromCodePoints @(0x7F16,0x8F91,0x5668)) },
        @{ pattern = 'foundation|infrastructure'; label = (New-TextFromCodePoints @(0x57FA,0x7840,0x8BBE,0x65BD)) },
        @{ pattern = 'architecture'; label = (New-TextFromCodePoints @(0x67B6,0x6784)) },
        @{ pattern = 'governance'; label = (New-TextFromCodePoints @(0x6CBB,0x7406)) },
        @{ pattern = 'protocol'; label = (New-TextFromCodePoints @(0x534F,0x8BAE)) },
        @{ pattern = 'audit'; label = (New-TextFromCodePoints @(0x5BA1,0x8BA1)) },
        @{ pattern = 'acceptance'; label = (New-TextFromCodePoints @(0x9A8C,0x6536)) },
        @{ pattern = 'release'; label = (New-TextFromCodePoints @(0x53D1,0x5E03)) },
        @{ pattern = 'compile'; label = (New-TextFromCodePoints @(0x7F16,0x8BD1)) },
        @{ pattern = 'build'; label = (New-TextFromCodePoints @(0x6784,0x5EFA)) },
        @{ pattern = 'runtime'; label = (New-TextFromCodePoints @(0x8FD0,0x884C,0x65F6)) },
        @{ pattern = 'resource'; label = (New-TextFromCodePoints @(0x8D44,0x6E90)) },
        @{ pattern = 'pipeline'; label = (New-TextFromCodePoints @(0x7BA1,0x7EBF)) },
        @{ pattern = 'knowledge'; label = (New-TextFromCodePoints @(0x77E5,0x8BC6)) },
        @{ pattern = 'interaction'; label = (New-TextFromCodePoints @(0x4EA4,0x4E92)) },
        @{ pattern = 'input'; label = (New-TextFromCodePoints @(0x8F93,0x5165)) },
        @{ pattern = 'ui'; label = 'UI' }, @{ pattern = 'prefab'; label = 'Prefab' },
        @{ pattern = 'entity'; label = (New-TextFromCodePoints @(0x5B9E,0x4F53)) },
        @{ pattern = 'weapon'; label = (New-TextFromCodePoints @(0x6B66,0x5668)) },
        @{ pattern = 'automation'; label = (New-TextFromCodePoints @(0x81EA,0x52A8,0x5316)) },
        @{ pattern = 'menu'; label = (New-TextFromCodePoints @(0x83DC,0x5355)) }
    )
    foreach ($rule in $rules) { if ($keyLower -match $rule.pattern -and $labels -notcontains $rule.label) { $labels += [string]$rule.label } }
    if ($labels.Count -eq 0 -and -not [string]::IsNullOrWhiteSpace($ContextText)) {
        $contextMatch = [regex]::Match([string]$ContextText, '[\p{IsCJKUnifiedIdeographs}]{2,16}')
        if ($contextMatch.Success -and $contextMatch.Value -match (New-TextFromCodePoints @(0x7A97,0x53E3)) + '|' + (New-TextFromCodePoints @(0x4F1A,0x8BDD)) + '|' + (New-TextFromCodePoints @(0x4EA4,0x4E92)) + '|' + (New-TextFromCodePoints @(0x8D44,0x6E90)) + '|' + (New-TextFromCodePoints @(0x7F16,0x8F91)) + '|' + (New-TextFromCodePoints @(0x9A8C,0x6536)) + '|' + (New-TextFromCodePoints @(0x6D4B,0x8BD5)) + '|' + (New-TextFromCodePoints @(0x7F16,0x8BD1)) + '|' + (New-TextFromCodePoints @(0x804C,0x8D23))) { $labels += $contextMatch.Value }
        if ($labels.Count -eq 0) {
            foreach ($hint in @((New-TextFromCodePoints @(0x97F3,0x9891)),(New-TextFromCodePoints @(0x76F8,0x673A)),(New-TextFromCodePoints @(0x6218,0x6597)),(New-TextFromCodePoints @(0x5B58,0x6863)),(New-TextFromCodePoints @(0x7F51,0x7EDC)),(New-TextFromCodePoints @(0x7A97,0x53E3)),(New-TextFromCodePoints @(0x4F1A,0x8BDD)),(New-TextFromCodePoints @(0x4EA4,0x63A5)),(New-TextFromCodePoints @(0x9A8C,0x6536)),(New-TextFromCodePoints @(0x8D44,0x6E90)),(New-TextFromCodePoints @(0x5B9E,0x4F53)),(New-TextFromCodePoints @(0x8F93,0x5165)),'UI')) { if ([string]$ContextText -like ('*' + $hint + '*')) { $labels += $hint } }
        }
    }
    if ($labels.Count -gt 0) { return ($labels -join '') }
    $fallback = (([string]$Key) -replace '^[eE][sS][-_.]?', '') -replace '[-_.:]+', ' '
    if ([string]::IsNullOrWhiteSpace($fallback)) { return (New-TextFromCodePoints @(0x804C,0x8D23)) }
    return (New-TextFromCodePoints @(0x804C,0x8D23,0x00B7)) + $fallback.Trim()
}

function Get-ContextDomainTitle([string]$ContextText) {
    if ([string]::IsNullOrWhiteSpace($ContextText)) { return '' }
    $middleDot = [string][char]0x00B7
    $rules = @(
        @{ token = 'Manifest|Provider|AssetBook|catalog|' + (New-TextFromCodePoints @(0x8D44,0x6E90)) + '|' + (New-TextFromCodePoints @(0x6E05,0x5355)); label = (New-TextFromCodePoints @(0x8D44,0x6E90,0x7BA1,0x7EBF)) },
        @{ token = (New-TextFromCodePoints @(0x8BED,0x4E49)) + '|' + (New-TextFromCodePoints @(0x8DEF,0x7531)) + '|P0'; label = (New-TextFromCodePoints @(0x8BED,0x4E49,0x8DEF,0x7531)) },
        @{ token = 'UI|' + (New-TextFromCodePoints @(0x754C,0x9762)); label = 'UI' },
        @{ token = (New-TextFromCodePoints @(0x4F1A,0x8BDD)) + '|' + (New-TextFromCodePoints @(0x4EA4,0x63A5)) + '|' + (New-TextFromCodePoints @(0x7A97,0x53E3)); label = (New-TextFromCodePoints @(0x4F1A,0x8BDD,0x4EA4,0x63A5)) },
        @{ token = (New-TextFromCodePoints @(0x7F16,0x8BD1)) + '|C#|compile|architecture'; label = (New-TextFromCodePoints @(0x7F16,0x8BD1)) },
        @{ token = 'Knowledge|KnowledgeIndex'; label = (New-TextFromCodePoints @(0x77E5,0x8BC6)) },
        @{ token = 'Weapon'; label = (New-TextFromCodePoints @(0x6B66,0x5668)) },
        @{ token = 'Entity|Prefab|prefab'; label = (New-TextFromCodePoints @(0x5B9E,0x4F53)) },
        @{ token = 'GameCore'; label = 'GameCore' },
        @{ token = 'Graph'; label = 'Graph' },
        @{ token = 'Performance|性能'; label = (New-TextFromCodePoints @(0x6027,0x80FD)) },
        @{ token = 'Security|安全'; label = (New-TextFromCodePoints @(0x5B89,0x5168)) }
    )
    $scored = @($rules | ForEach-Object {
        $score = @([regex]::Matches($ContextText, $_.token, [Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
        if ($score -gt 0) { [pscustomobject]@{ label = [string]$_.label; score = $score } }
    } | Sort-Object score -Descending)
    if ($scored.Count -eq 0) { return '' }
    if ($scored.Count -gt 1 -and $scored[0].score -eq $scored[1].score -and $scored[0].label -ne $scored[1].label) { return (New-TextFromCodePoints @(0x591A,0x9886,0x57DF,0x5F85,0x786E,0x8BA4)) }
    return [string]$scored[0].label
}

function Get-DefaultTabTitle([string]$Key, [string]$LaunchMode, [string]$ContextText = '') {
    $middleDot = [string][char]0x00B7
    # Context is the highest-authority naming input. Responsibility keys are
    # only fallback metadata and may never override a clear task context.
    $contextLabel = Get-ContextDomainTitle -ContextText $ContextText
    if (-not [string]::IsNullOrWhiteSpace($contextLabel)) { return 'ES' + $middleDot + $contextLabel }
    $knownTitles = @{
        'semantic-routing-governance' = 'ES' + $middleDot + (New-TextFromCodePoints @(0x8BED,0x4E49,0x8DEF,0x7531,0x6CBB,0x7406))
        'prompt-engineering' = 'ES' + $middleDot + (New-TextFromCodePoints @(0x63D0,0x793A,0x8BCD,0x5305,0x88C5))
        'prompt-wrapper' = 'ES' + $middleDot + (New-TextFromCodePoints @(0x63D0,0x793A,0x8BCD,0x5305,0x88C5))
        'es-codex-multilaunch' = 'ES' + $middleDot + (New-TextFromCodePoints @(0x591A,0x7A97,0x53E3,0x7F16,0x6392))
        'session-bootstrap-maintenance' = 'ES' + $middleDot + (New-TextFromCodePoints @(0x4F1A,0x8BDD,0x5F15,0x5BFC,0x7EF4,0x62A4))
        'aibrain-architecture' = 'ES' + $middleDot + 'AIBrain' + (New-TextFromCodePoints @(0x67B6,0x6784))
        'session-context-review' = 'ES' + $middleDot + (New-TextFromCodePoints @(0x4E0A,0x4E0B,0x6587,0x672C,0x5BA1,0x67E5))
        'context-recycle' = 'ES' + $middleDot + (New-TextFromCodePoints @(0x4F1A,0x8BDD,0x518D,0x751F))
    }
    if ($knownTitles.ContainsKey($Key.ToLowerInvariant())) { return [string]$knownTitles[$Key.ToLowerInvariant()] }
    switch ($Key.ToLowerInvariant()) {
        'release-acceptance' { return 'ES' + $middleDot + (New-TextFromCodePoints @(0x5DE5, 0x7A0B, 0x9A8C, 0x6536)) }
        'engineering-acceptance' { return 'ES' + $middleDot + (New-TextFromCodePoints @(0x5DE5, 0x7A0B, 0x9A8C, 0x6536)) }
        'aitest' { return 'ES' + $middleDot + 'AITest' }
        'aitest-implementation' { return 'ES' + $middleDot + 'AITest' }
        'resource-pipeline' { return 'ES' + $middleDot + (New-TextFromCodePoints @(0x8D44, 0x6E90, 0x7BA1, 0x7EBF)) }
        'graph-audit' { return 'ES' + $middleDot + 'Graph' + (New-TextFromCodePoints @(0x5BA1, 0x8BA1)) }
    }
    if ($Key -and $Key -ne 'default' -and (Test-ActionResponsibilityKey $Key)) {
        return 'ES' + $middleDot + (New-TextFromCodePoints @(0x5F85,0x5206,0x914D,0x804C,0x8D23))
    }
    if ($Key -and $Key -ne 'default') {
        return 'ES' + $middleDot + (Get-ResponsibilityTitleLabel -Key $Key -ContextText $ContextText)
    }
    if ($Key -and $Key -ne 'default') {
        switch ($LaunchMode) {
            'Resume' { return 'ES' + $middleDot + (New-TextFromCodePoints @(0x5F85,0x8BC6,0x522B,0x804C,0x8D23)) }
            'Fork' { return 'ES' + $middleDot + (New-TextFromCodePoints @(0x5F85,0x8BC6,0x522B,0x804C,0x8D23)) }
        }
    }
    if ($Key -eq 'default') {
        if (-not [string]::IsNullOrWhiteSpace($ContextText)) {
            $contextRules = @(
                @{ token = 'Manifest|Provider|' + (New-TextFromCodePoints @(0x8D44,0x6E90)) + '|' + (New-TextFromCodePoints @(0x6E05,0x5355)); label = (New-TextFromCodePoints @(0x8D44,0x6E90,0x7BA1,0x7EBF)) },
                @{ token = (New-TextFromCodePoints @(0x8BED,0x4E49)) + '|' + (New-TextFromCodePoints @(0x8DEF,0x7531)) + '|P0'; label = (New-TextFromCodePoints @(0x8BED,0x4E49,0x8DEF,0x7531)) },
                @{ token = 'UI|' + (New-TextFromCodePoints @(0x754C,0x9762)); label = 'UI' },
                @{ token = (New-TextFromCodePoints @(0x4F1A,0x8BDD)) + '|' + (New-TextFromCodePoints @(0x4EA4,0x63A5)) + '|' + (New-TextFromCodePoints @(0x7A97,0x53E3)); label = (New-TextFromCodePoints @(0x4F1A,0x8BDD,0x4EA4,0x63A5)) },
                @{ token = (New-TextFromCodePoints @(0x7F16,0x8BD1)) + '|C#'; label = (New-TextFromCodePoints @(0x7F16,0x8BD1)) }
            )
            $scored = @(
                foreach ($contextRule in $contextRules) {
                    $score = @([regex]::Matches($ContextText, $contextRule.token, [Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
                    if ($score -gt 0) { [pscustomobject]@{ label = [string]$contextRule.label; score = $score } }
                }
            ) | Sort-Object score -Descending
            $scored = @($scored)
            if ($scored.Count -gt 0) {
                $top = $scored[0]
                $second = if ($scored.Count -gt 1) { $scored[1] } else { $null }
                if ($second -and [int]$top.score -eq [int]$second.score -and [string]$top.label -ne [string]$second.label) {
                    return 'ES' + $middleDot + (New-TextFromCodePoints @(0x591A,0x9886,0x57DF,0x5F85,0x786E,0x8BA4))
                }
                return 'ES' + $middleDot + [string]$top.label
            }
            $contextLabel = Get-ResponsibilityTitleLabel -Key '' -ContextText $ContextText
            if (-not [string]::IsNullOrWhiteSpace($contextLabel) -and $contextLabel -ne (New-TextFromCodePoints @(0x804C,0x8D23))) {
                return 'ES' + $middleDot + $contextLabel
            }
        }
        return 'ES' + $middleDot + (New-TextFromCodePoints @(0x5F85,0x5206,0x914D,0x804C,0x8D23))
    }
    return 'ES' + $middleDot + (New-TextFromCodePoints @(0x5F85,0x8BC6,0x522B,0x804C,0x8D23))
}

function Test-AbstractTabTitle([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $true }
    $middleDot = [string][char]0x00B7
    $bullet = [string][char]0x2022
    $normalized = ($Value.Trim() -replace '\s+', '').Replace('.', '-').Replace($middleDot, '-').Replace($bullet, '-')
    # Operation words describe the launch action, not the receiving duty. They
    # must not survive as a tab title, even when explicitly supplied.
    if ($normalized -match '^(?i:ES-?Codex|ES-?Framework|Codex|ES|Resume|Fork|Handoff|Handover|Close|Bootstrap)$') { return $true }
    $operationTitles = @(
        (New-TextFromCodePoints @(0x542F,0x52A8)),
        (New-TextFromCodePoints @(0x6062,0x590D)),
        (New-TextFromCodePoints @(0x5206,0x53C9)),
        (New-TextFromCodePoints @(0x4EA4,0x63A5)),
        (New-TextFromCodePoints @(0x5173,0x95ED))
    )
    return $operationTitles -contains $normalized
}

function Test-ActionResponsibilityKey([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    $normalized = ($Value.Trim() -replace '[\s_.-]+', '').ToLowerInvariant()
    if ($normalized -match '^(handoff|handover|resume|fork|close|bootstrap|launch|start|restore|recycle|focus)$') { return $true }
    return $normalized -in @(
        (New-TextFromCodePoints @(0x542F,0x52A8)),
        (New-TextFromCodePoints @(0x6062,0x590D)),
        (New-TextFromCodePoints @(0x5206,0x53C9)),
        (New-TextFromCodePoints @(0x4EA4,0x63A5)),
        (New-TextFromCodePoints @(0x5173,0x95ED))
    )
}

function Get-SafeTabTitle([string]$Value) {
    $safe = ($Value -replace '[\x00-\x1F\x7F&|<>^"]', ' ' -replace '\s+', ' ').Trim()
    if ([string]::IsNullOrWhiteSpace($safe)) { $safe = 'ES-Codex' }
    if ($safe.Length -gt 24) { $safe = $safe.Substring(0, 24).Trim() }
    return $safe
}

function Get-SafeWindowName([string]$Value) {
    $safe = ($Value -replace '[^\p{L}\p{Nd}_.-]', '-').Trim('-')
    if ([string]::IsNullOrWhiteSpace($safe)) { $safe = 'ESFramework' }
    if ($safe.Length -gt 32) { $safe = $safe.Substring(0, 32).Trim('-') }
    return $safe
}

function ConvertTo-WindowsArgument([string]$Value) {
    if ($null -eq $Value -or $Value.Length -eq 0) { return '""' }
    if ($Value -notmatch '[\s"]') { return $Value }
    $builder = [Text.StringBuilder]::new()
    [void]$builder.Append('"')
    $backslashes = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq '\') {
            $backslashes++
            continue
        }
        if ($character -eq '"') {
            [void]$builder.Append(('\' * (($backslashes * 2) + 1)))
            [void]$builder.Append('"')
            $backslashes = 0
            continue
        }
        if ($backslashes -gt 0) {
            [void]$builder.Append(('\' * $backslashes))
            $backslashes = 0
        }
        [void]$builder.Append($character)
    }
    if ($backslashes -gt 0) { [void]$builder.Append(('\' * ($backslashes * 2))) }
    [void]$builder.Append('"')
    return $builder.ToString()
}

function Test-ProcessAlive([int]$ProcessId) {
    if ($ProcessId -le 0) { return $false }
    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    return $null -ne $process -and -not $process.HasExited
}

function Find-LaunchedShellProcessId([string]$Token) {
    try {
        $process = Get-CimInstance Win32_Process -ErrorAction Stop |
            Where-Object { $_.Name -eq 'cmd.exe' -and [string]$_.CommandLine -like ('*' + $Token + '*') } |
            Sort-Object CreationDate -Descending |
            Select-Object -First 1
        if ($null -ne $process) { return [int]$process.ProcessId }
    }
    catch {
        Write-Verbose ("Unable to inspect launched Codex shell process: " + $_.Exception.Message)
    }
    return 0
}

function Find-SessionId([string]$HistoryPath, [string]$Token, [long]$StartedAtUnix) {
    if (-not (Test-Path -LiteralPath $HistoryPath -PathType Leaf)) { return '' }
    foreach ($line in (Get-Content -LiteralPath $HistoryPath -Tail 3000 -Encoding UTF8)) {
        try {
            $row = $line | ConvertFrom-Json
            if ([string]$row.text -like ('*' + $Token + '*') -and [long]$row.ts -ge $StartedAtUnix) {
                return [string]$row.session_id
            }
        }
        catch {
            Write-Verbose ("Ignoring malformed Codex history line while resolving session: " + $_.Exception.Message)
        }
    }
    return ''
}

function Read-SessionRegistry([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [pscustomobject]@{ version = 1; sessions = @() }
    }
    try {
        $registry = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($null -eq $registry.sessions) { $registry | Add-Member -NotePropertyName sessions -NotePropertyValue @() }
        return $registry
    }
    catch {
        throw "Codex session registry is invalid: $Path"
    }
}

function Find-SessionRegistryEntry([object]$Registry, [string]$Id) {
    if ([string]::IsNullOrWhiteSpace($Id)) { return $null }
    return @($Registry.sessions | Where-Object { [string]$_.sessionId -eq $Id } | Select-Object -First 1)[0]
}

function Save-SessionRegistry([string]$Path, [object]$Registry, [object]$Entry) {
    $items = @($Registry.sessions | Where-Object { [string]$_.sessionId -ne [string]$Entry.sessionId })
    $items += $Entry
    $payload = [ordered]@{ version = 1; sessions = $items }
    $temporary = $Path + '.tmp-' + [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($temporary, ($payload | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        $backup = $Path + '.bak-' + [Guid]::NewGuid().ToString('N')
        [IO.File]::Replace($temporary, $Path, $backup)
        if (Test-Path -LiteralPath $backup -PathType Leaf) { Remove-Item -LiteralPath $backup -Force }
    }
    else {
        [IO.File]::Move($temporary, $Path)
    }
}

function Resolve-HandoffFiles([string]$Root, [string[]]$Paths) {
    $resolved = @()
    foreach ($item in @($Paths)) {
        if ([string]::IsNullOrWhiteSpace($item)) { continue }
        $candidate = if ([IO.Path]::IsPathRooted($item)) { $item } else { Join-Path $Root $item }
        $fullPath = [IO.Path]::GetFullPath($candidate)
        if (-not ($fullPath.Equals($Root, [StringComparison]::OrdinalIgnoreCase) -or
                $fullPath.StartsWith($Root + '\', [StringComparison]::OrdinalIgnoreCase))) {
            throw "HandoffPath must stay inside the fixed project root: $item"
        }
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "Handoff file was not found: $fullPath"
        }
        $resolved += [pscustomobject]@{
            relativePath = $fullPath.Substring($Root.Length).TrimStart('\').Replace('\', '/')
            absolutePath = $fullPath
            sha256 = Get-FileSha256 $fullPath
            length = (Get-Item -LiteralPath $fullPath).Length
        }
    }
    return $resolved
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

function New-HandoffSnapshots([object[]]$Sources, [string]$SnapshotDirectory) {
    if (Test-Path -LiteralPath $SnapshotDirectory) {
        throw "Handoff snapshot directory already exists: $SnapshotDirectory"
    }
    [void][IO.Directory]::CreateDirectory($SnapshotDirectory)
    $snapshotOwnerPath = Join-Path $SnapshotDirectory '.snapshot-owner.json'
    $snapshotOwner = [ordered]@{
        schemaVersion = 1
        createdUtc = [DateTime]::UtcNow.ToString('o')
        processId = $PID
    } | ConvertTo-Json -Compress
    Write-CreateOnlyUtf8File $snapshotOwnerPath $snapshotOwner
    $snapshotRoot = [IO.Path]::GetFullPath($SnapshotDirectory).TrimEnd('\')
    $snapshots = @()
    foreach ($source in @($Sources)) {
        $relativePath = ([string]$source.relativePath).Replace('/', '\')
        $snapshotPath = [IO.Path]::GetFullPath((Join-Path $snapshotRoot $relativePath))
        if (-not $snapshotPath.StartsWith($snapshotRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Handoff snapshot path escaped its launch directory: $relativePath"
        }
        [void][IO.Directory]::CreateDirectory((Split-Path -Parent $snapshotPath))
        $sourceStream = [IO.File]::Open([string]$source.absolutePath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try {
            $snapshotStream = [IO.File]::Open($snapshotPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
            try {
                $sourceStream.CopyTo($snapshotStream)
                $snapshotStream.Flush($true)
            }
            finally {
                $snapshotStream.Dispose()
            }
            $snapshotHash = Get-FileSha256 $snapshotPath
            $sourceStream.Position = 0
            $sha = [Security.Cryptography.SHA256]::Create()
            try {
                $sourceHash = ([BitConverter]::ToString($sha.ComputeHash($sourceStream))).Replace('-', '').ToLowerInvariant()
            }
            finally {
                $sha.Dispose()
            }
            if ($sourceHash -ne $snapshotHash) {
                throw "Handoff source changed while its per-launch snapshot was being created: $($source.absolutePath)"
            }
            $snapshots += [pscustomobject]@{
                relativePath = ([string]$source.relativePath).Replace('\', '/')
                absolutePath = $snapshotPath
                sha256 = $snapshotHash
                length = (Get-Item -LiteralPath $snapshotPath).Length
                snapshot = $true
                sourceAbsolutePath = [string]$source.absolutePath
                sourceSha256AtSnapshot = $sourceHash
            }
        }
        finally {
            $sourceStream.Dispose()
        }
    }
    return $snapshots
}

function Get-GitSnapshot([string]$Root) {
    $branch = (& git -C $Root branch --show-current 2>$null | Select-Object -First 1)
    $head = (& git -C $Root rev-parse HEAD 2>$null | Select-Object -First 1)
    return [pscustomobject]@{ branch = [string]$branch; head = [string]$head }
}

$skillDirectory = Split-Path -Parent $PSScriptRoot
$skillsDirectory = Split-Path -Parent $skillDirectory
$agentsDirectory = Split-Path -Parent $skillsDirectory
$derivedProjectRoot = Split-Path -Parent $agentsDirectory
$fixedProjectRoot = 'F:\aaProject\ESFrameWorkPublish'
$installedProjectRoot = [IO.Path]::GetFullPath($derivedProjectRoot).TrimEnd('\')
if (-not $installedProjectRoot.Equals($fixedProjectRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Skill location does not match the fixed ESFramework root: $fixedProjectRoot"
}

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $resolvedProjectRoot = $fixedProjectRoot
}
else {
    $resolvedProjectRoot = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $ProjectPath).Path).TrimEnd('\')
    if (-not $resolvedProjectRoot.Equals($fixedProjectRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "ProjectPath must resolve to the fixed ESFramework root: $fixedProjectRoot"
    }
}

if ($HandoffMode -and $Mode -notin @('Validate', 'New')) {
    throw 'HandoffMode is valid only for Validate or New.'
}
if ($Mode -eq 'New' -and $HandoffPath.Count -gt 0 -and -not $HandoffMode) {
    throw 'HandoffPath on a New session requires Complete-ESCodexHandoff.ps1; direct handoff delivery is prohibited.'
}
if ($HandoffMode) { Assert-HandoffAuthorization $HandoffAuthorization }

$actualProjectIdentityFingerprint = Get-ProjectIdentityFingerprint $resolvedProjectRoot
if (($HandoffMode -or $Mode -eq 'New') -and -not [string]::IsNullOrWhiteSpace($ProjectIdentityFingerprint) -and $ProjectIdentityFingerprint -cne $actualProjectIdentityFingerprint) {
    throw 'Project identity fingerprint does not match the selected ProjectPath.'
}
if ($HandoffMode -and [string]::IsNullOrWhiteSpace($ProjectIdentityFingerprint)) {
    throw 'Project identity fingerprint is required for handoff validation/launch.'
}

if ($Mode -eq 'Close') {
    $closeScriptPath = Join-Path $PSScriptRoot 'Close-ESCodexSession.ps1'
    if (-not (Test-Path -LiteralPath $closeScriptPath -PathType Leaf)) {
        throw "Codex session closer was not found: $closeScriptPath"
    }
    & $closeScriptPath `
        -SessionId $SessionId `
        -TaskKey $TaskKey `
        -ResponsibilityKey $ResponsibilityKey `
        -TabTitle $TabTitle `
        -AllMatches:$AllMatches `
        -DryRun:$DryRun
    return
}

if ($Mode -eq 'RestoreRecent') {
    $restoreScriptPath = Join-Path $PSScriptRoot 'Restore-ESRecentCodexSessions.ps1'
    if (-not (Test-Path -LiteralPath $restoreScriptPath -PathType Leaf)) {
        throw "Recent Codex session restorer was not found: $restoreScriptPath"
    }
    & $restoreScriptPath `
        -RecentHours $RecentHours `
        -MaxSessions $MaxSessions `
        -IncludeUnclassified:$IncludeUnclassified `
        -IncludeTests:$IncludeTests `
        -DryRun:$DryRun
    return
}

if ($Mode -in @('List', 'Status')) {
    $statusScriptPath = Join-Path $PSScriptRoot 'Get-ESCodexSessionStatus.ps1'
    if (-not (Test-Path -LiteralPath $statusScriptPath -PathType Leaf)) {
        throw "Codex session status reader was not found: $statusScriptPath"
    }
    & $statusScriptPath `
        -SessionId $SessionId `
        -TaskKey $TaskKey `
        -ResponsibilityKey $ResponsibilityKey `
        -TabTitle $TabTitle
    return
}

if ($Mode -eq 'Focus') {
    & (Join-Path $PSScriptRoot 'Focus-ESCodexSessionTerminal.ps1') `
        -SessionId $SessionId `
        -RecordId $RecordId `
        -LaunchToken $LaunchToken
    return
}

if ($Mode -in @('Query', 'Resolve')) {
    $queryScriptPath = Join-Path $PSScriptRoot 'Resolve-ESCodexSessionRoute.ps1'
    if (-not (Test-Path -LiteralPath $queryScriptPath -PathType Leaf)) { throw "Codex session route resolver was not found: $queryScriptPath" }
    & $queryScriptPath `
        -SessionId $SessionId `
        -RecordId $RecordId `
        -TaskKey $TaskKey `
        -ResponsibilityKey $ResponsibilityKey `
        -LaunchToken $LaunchToken `
        -Current:$Current `
        -RequireUnique:($Mode -eq 'Resolve')
    return
}

if ($Mode -eq 'BindResponsibility') {
    if ([string]::IsNullOrWhiteSpace($BindResponsibilityKey)) { throw 'BindResponsibility requires -BindResponsibilityKey.' }
    & (Join-Path $PSScriptRoot 'Set-ESCodexSessionResponsibility.ps1') `
        -SessionId $SessionId `
        -RecordId $RecordId `
        -LaunchToken $LaunchToken `
        -Current:$Current `
        -NewResponsibilityKey $BindResponsibilityKey `
        -ExpectedRegistryRevision $ExpectedRegistryRevision
    return
}

if ($Mode -eq 'SetPresence') {
    if ([string]::IsNullOrWhiteSpace($Availability)) { throw 'SetPresence requires -Availability.' }
    & (Join-Path $PSScriptRoot 'Set-ESCodexSessionPresence.ps1') `
        -SessionId $SessionId `
        -RecordId $RecordId `
        -LaunchToken $LaunchToken `
        -Current:$Current `
        -Availability $Availability `
        -ActivityKey $ActivityKey `
        -ActivitySummary $ActivitySummary `
        -TtlSeconds $PresenceTtlSeconds `
        -ExpectedRegistryRevision $ExpectedRegistryRevision
    return
}

if ($Mode -eq 'BindAcceptance') {
    & (Join-Path $PSScriptRoot 'Set-ESCodexAcceptanceBinding.ps1') `
        -RecordId $RecordId `
        -SessionId $SessionId `
        -LaunchToken $LaunchToken `
        -Current:$Current `
        -AcceptanceResponsibilityKey $AcceptanceResponsibilityKey `
        -Disable:$DisableAcceptanceBinding `
        -ExpectedRegistryRevision $ExpectedRegistryRevision
    return
}

if ($Mode -eq 'Wait') {
    & (Join-Path $PSScriptRoot 'Wait-ESCodexSessionRoute.ps1') `
        -SessionId $SessionId `
        -RecordId $RecordId `
        -ResponsibilityKey $ResponsibilityKey `
        -LaunchToken $LaunchToken `
        -Current:$Current `
        -WaitFor $WaitFor `
        -TimeoutSeconds $WaitSeconds `
        -PollMilliseconds $PollMilliseconds
    return
}

if ($Mode -eq 'QueueMessage') {
    if ([string]::IsNullOrWhiteSpace($MessageBody)) { throw 'QueueMessage requires -MessageBody.' }
    & (Join-Path $PSScriptRoot 'Publish-ESCodexSessionMessage.ps1') `
        -SessionId $SessionId `
        -RecordId $RecordId `
        -ResponsibilityKey $ResponsibilityKey `
        -Body $MessageBody `
        -IdempotencyKey $IdempotencyKey `
        -Priority $MessagePriority `
        -TtlSeconds $MessageTtlSeconds `
        -RequireReady:$RequireReady `
        -ExpectedRegistryRevision $ExpectedRegistryRevision
    return
}

if ($Mode -eq 'SendMessage') {
    if ([string]::IsNullOrWhiteSpace($MessageBody)) { throw 'SendMessage requires -MessageBody.' }
    & (Join-Path $PSScriptRoot 'Send-ESCodexSessionMessage.ps1') `
        -SessionId $SessionId `
        -RecordId $RecordId `
        -ResponsibilityKey $ResponsibilityKey `
        -Body $MessageBody `
        -IdempotencyKey $IdempotencyKey `
        -Priority $MessagePriority `
        -TtlSeconds $MessageTtlSeconds `
        -ExpectedRegistryRevision $ExpectedRegistryRevision
    return
}

if ($Mode -eq 'MessageStatus') {
    & (Join-Path $PSScriptRoot 'Get-ESCodexSessionMessage.ps1') `
        -MessageId $MessageId `
        -IdempotencyKey $IdempotencyKey `
        -TargetRecordId $RecordId
    return
}

if ($Mode -eq 'RequestAcceptance') {
    & (Join-Path $PSScriptRoot 'Request-ESCodexAcceptance.ps1') `
        -AcceptanceResponsibilityKey $AcceptanceResponsibilityKey `
        -RequesterRecordId $RequesterRecordId `
        -RequesterSessionId $RequesterSessionId `
        -RequesterLaunchToken $RequesterLaunchToken `
        -Current:$Current `
        -Body $MessageBody `
        -IdempotencyKey $IdempotencyKey `
        -Priority $MessagePriority `
        -TtlSeconds $MessageTtlSeconds `
        -WaitSeconds $WaitSeconds `
        -PollMilliseconds $PollMilliseconds `
        -NoWaitForReply:$NoWaitForReply
    return
}

if ($Mode -eq 'ReplyAcceptance') {
    if ([string]::IsNullOrWhiteSpace($MessageId) -or [string]::IsNullOrWhiteSpace($MessageBody)) { throw 'ReplyAcceptance requires -MessageId and -MessageBody.' }
    & (Join-Path $PSScriptRoot 'Reply-ESCodexAcceptance.ps1') `
        -RequestMessageId $MessageId `
        -Body $MessageBody `
        -ResponderRecordId $ResponderRecordId `
        -ResponderSessionId $ResponderSessionId `
        -Current:$Current `
        -Priority $MessagePriority `
        -TtlSeconds $MessageTtlSeconds
    return
}

if ($Mode -eq 'AcceptanceStatus') {
    if ([string]::IsNullOrWhiteSpace($MessageId)) { throw 'AcceptanceStatus requires -MessageId.' }
    & (Join-Path $PSScriptRoot 'Get-ESCodexAcceptanceStatus.ps1') -RequestMessageId $MessageId -RequesterRecordId $RequesterRecordId
    return
}

if ($Mode -eq 'UpdateMessageStatus') {
    if ([string]::IsNullOrWhiteSpace($MessageId) -or [string]::IsNullOrWhiteSpace($MessageStatus)) { throw 'UpdateMessageStatus requires -MessageId and -MessageStatus.' }
    & (Join-Path $PSScriptRoot 'Set-ESCodexSessionMessageStatus.ps1') `
        -MessageId $MessageId `
        -Status $MessageStatus `
        -AcceptedByRecordId $RecordId `
        -Note $MessageNote `
        -ExpectedStateRevision $ExpectedMessageRevision
    return
}

if ($Mode -eq 'MessageRepair') {
    & (Join-Path $PSScriptRoot 'Repair-ESCodexSessionMessages.ps1') `
        -Apply:$Apply `
        -RetentionDays $MessageRetentionDays `
        -DeleteTerminalMessages:$DeleteTerminalMessages
    return
}

if ($Mode -eq 'BrokerStatus') {
    & (Join-Path $PSScriptRoot 'Get-ESCodexSessionBrokerStatus.ps1') -ProbeAppServer:$ProbeAppServer
    return
}

if ($Mode -eq 'Doctor') {
    & (Join-Path $PSScriptRoot 'Get-ESCodexSessionDoctor.ps1') -ProbeAppServer:$ProbeAppServer
    return
}

if ($Mode -eq 'SmokeTest') {
    & (Join-Path $PSScriptRoot 'Test-ESCodexSessionOperationalFlow.ps1')
    return
}

if ($Mode -eq 'SelfTest') {
    & (Join-Path $PSScriptRoot 'Test-ESCodexSessionReadiness.ps1') -RunPester:$RunSelfTests -ProbeAppServer:$ProbeAppServer
    return
}

if ($Mode -in @('Repair', 'Reconcile')) {
    $repairScriptPath = Join-Path $PSScriptRoot 'Repair-ESCodexSessionState.ps1'
    if (-not (Test-Path -LiteralPath $repairScriptPath -PathType Leaf)) {
        throw "Codex session repair tool was not found: $repairScriptPath"
    }
    if ($Apply -and $DryRun) { throw 'Repair cannot combine -Apply with -DryRun.' }
    & $repairScriptPath `
        -SessionId $SessionId `
        -ResponsibilityKey $ResponsibilityKey `
        -Apply:$Apply
    return
}

if ($Mode -in @('PrepareExternalClaim', 'SubmitExternalClaimInput', 'FinalizeExternalClaim', 'CancelExternalClaim')) {
    $externalClaimScriptPath = Join-Path $PSScriptRoot 'Invoke-ESCodexExternalClaim.ps1'
    if (-not (Test-Path -LiteralPath $externalClaimScriptPath -PathType Leaf)) {
        throw "External CMD claim script was not found: $externalClaimScriptPath"
    }
    $externalClaimStateParameters = @{}
    if (-not [string]::IsNullOrWhiteSpace($ExternalClaimStateRoot)) {
        $externalClaimStateParameters.StateRoot = $ExternalClaimStateRoot
    }
    if ($Mode -eq 'PrepareExternalClaim') {
        if ([string]::IsNullOrWhiteSpace($SessionId) -and [string]::IsNullOrWhiteSpace($ExternalClaimBindingId)) {
            throw 'PrepareExternalClaim requires an exact SessionId or an external CMD binding identity.'
        }
        & $externalClaimScriptPath `
            -Action Prepare `
            -SessionId $SessionId `
            -ExternalBindingId $ExternalClaimBindingId `
            -ExpectedCmdProcessId $ExternalClaimExpectedCmdProcessId `
            -ExpectedCmdProcessStartedAtUtc $ExternalClaimExpectedCmdProcessStartedAtUtc `
            -TaskKey $TaskKey `
            -ResponsibilityKey $ResponsibilityKey `
            -TabTitle $TabTitle `
            -ClaimId $ExternalClaimId `
            -TtlSeconds $ExternalClaimTtlSeconds `
            @externalClaimStateParameters
    }
    elseif ($Mode -eq 'SubmitExternalClaimInput') {
        if ([string]::IsNullOrWhiteSpace($ExternalClaimId)) { throw 'SubmitExternalClaimInput requires -ExternalClaimId.' }
        $externalInputScriptPath = Join-Path $PSScriptRoot 'Invoke-ESCodexExternalConsoleInput.ps1'
        if (-not (Test-Path -LiteralPath $externalInputScriptPath -PathType Leaf)) {
            throw "External CMD console input script was not found: $externalInputScriptPath"
        }
        if ($ExternalClaimExpectedCmdProcessId -le 0 -or
            [string]::IsNullOrWhiteSpace($ExternalClaimExpectedCmdProcessStartedAtUtc)) {
            throw 'SubmitExternalClaimInput requires the exact selected CMD PID and process start time.'
        }
        $submissionOutput = & $externalInputScriptPath `
            -ClaimId $ExternalClaimId `
            -ExpectedCmdProcessId $ExternalClaimExpectedCmdProcessId `
            -ExpectedCmdProcessStartedAtUtc $ExternalClaimExpectedCmdProcessStartedAtUtc `
            @externalClaimStateParameters

        # A PowerShell child script may return a native PSCustomObject or a JSON
        # string depending on the host/pipeline. Do not stringify objects and
        # feed their display representation back into ConvertFrom-Json.
        $submission = $submissionOutput
        if ($submissionOutput -is [string]) {
            $submissionText = $submissionOutput.Trim()
            if ([string]::IsNullOrWhiteSpace($submissionText)) {
                throw 'External CMD console input script returned an empty submission receipt.'
            }
            try { $submission = $submissionText | ConvertFrom-Json }
            catch { throw "External CMD console input script returned invalid JSON: $($_.Exception.Message)" }
        }
        if ($submission -is [System.Array]) {
            if ($submission.Count -ne 1) { throw 'External CMD console input script returned an ambiguous submission receipt.' }
            $submission = $submission[0]
        }
        if ($null -eq $submission -or -not [bool]$submission.success -or
            [string]::IsNullOrWhiteSpace([string]$submission.claimId) -or
            -not ([string]$submission.claimId).Equals($ExternalClaimId, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'External CMD console input script did not return a successful submission receipt.'
        }
        $submission
    }
    elseif ($Mode -eq 'FinalizeExternalClaim') {
        if ([string]::IsNullOrWhiteSpace($ExternalClaimId)) { throw 'FinalizeExternalClaim requires -ExternalClaimId.' }
        & $externalClaimScriptPath -Action Finalize -ClaimId $ExternalClaimId @externalClaimStateParameters
    }
    else {
        if ([string]::IsNullOrWhiteSpace($ExternalClaimId)) { throw 'CancelExternalClaim requires -ExternalClaimId.' }
        & $externalClaimScriptPath -Action Cancel -ClaimId $ExternalClaimId @externalClaimStateParameters
    }
    return
}

$requiredPatterns = @(
    'Assets/Plugins/ES/AIWarnings/00_*/README.md',
    'Assets/Plugins/ES/AIWarnings/00_*/*CurrentStatus*.md',
    'Assets/Plugins/ES/AIWarnings/00_*/*RuleIndex*.md',
    'ES/*Codex*/Tools/Find-CodexSession.ps1'
)
$missingPaths = @($requiredPatterns | Where-Object {
        @((Get-ChildItem -Path (Join-Path $resolvedProjectRoot $_) -File -ErrorAction SilentlyContinue)).Count -ne 1
    })

$codexCommand = Get-Command codex -ErrorAction SilentlyContinue
if ($null -eq $codexCommand) { throw 'Codex CLI was not found on PATH.' }
$codexCmdPath = Join-Path (Split-Path -Parent $codexCommand.Source) 'codex.cmd'
if (-not (Test-Path -LiteralPath $codexCmdPath -PathType Leaf)) {
    throw "Required CMD launcher was not found: $codexCmdPath"
}
if ($missingPaths.Count -gt 0) { throw "Required bootstrap paths are missing:`n$($missingPaths -join "`n")" }

$wtCommand = Get-Command wt.exe -ErrorAction SilentlyContinue
$effectiveTerminalMode = $TerminalMode
if ($TerminalMode -eq 'Auto') {
    $effectiveTerminalMode = if ($null -ne $wtCommand) { 'ProjectWindow' } else { 'PlainCmd' }
}
elseif ($TerminalMode -in @('CurrentWindow', 'ProjectWindow', 'NewWindow') -and $null -eq $wtCommand) {
    throw "Windows Terminal is required for TerminalMode $TerminalMode."
}
if ($effectiveTerminalMode -eq 'CurrentWindow' -and [string]::IsNullOrWhiteSpace($env:WT_SESSION)) {
    throw 'TerminalMode CurrentWindow requires launching from an existing Windows Terminal session.'
}

$localStateBase = if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
    Join-Path $env:LOCALAPPDATA 'ESFramework\CodexSessions'
}
else {
    Join-Path ([IO.Path]::GetTempPath()) 'ESFramework-CodexSessions'
}
$launchStateRoot = Join-Path $localStateBase 'launch-state'
$envelopeRoot = Join-Path $localStateBase 'envelopes'
$handoffSnapshotRoot = Join-Path $localStateBase 'handoff-snapshots'
$commandRoot = Join-Path $localStateBase 'commands'
$registryPath = Join-Path $localStateBase 'sessions.json'
$historyPath = Join-Path $env:USERPROFILE '.codex\history.jsonl'
$projectSkillsRoot = Join-Path $resolvedProjectRoot '.agents\skills'
$sessionBootstrapSkillPath = Join-Path $projectSkillsRoot 'es-codex-session-bootstrap\SKILL.md'
$envelopeValidatorPath = Join-Path $PSScriptRoot 'Test-ESCodexLaunchEnvelope.ps1'
if (-not (Test-Path -LiteralPath $envelopeValidatorPath -PathType Leaf)) {
    throw "Launch envelope validator was not found: $envelopeValidatorPath"
}
$exitMarkerWriterPath = Join-Path $PSScriptRoot 'Write-ESCodexLaunchExitMarker.ps1'
if (-not (Test-Path -LiteralPath $exitMarkerWriterPath -PathType Leaf)) {
    throw "Launch exit-marker writer was not found: $exitMarkerWriterPath"
}
if (-not (Test-Path -LiteralPath $sessionBootstrapSkillPath -PathType Leaf)) {
    throw "Project session bootstrap Skill was not found: $sessionBootstrapSkillPath"
}
$registry = Read-ESCodexSessionRegistry $registryPath
$selectedByResponsibility = $false
if ($Mode -in @('Resume', 'Fork') -and [string]::IsNullOrWhiteSpace($SessionId) -and -not [string]::IsNullOrWhiteSpace($ResponsibilityKey)) {
    $responsibilityMatches = @($registry.sessions | Where-Object {
            [string]$_.projectRoot -eq $resolvedProjectRoot -and
            [string]$_.responsibilityKey -eq $ResponsibilityKey.Trim() -and
            -not [string]::IsNullOrWhiteSpace([string]$_.sessionId) -and
            [string]$_.lifecycleStatus -ne 'Closed'
        })
    if ($responsibilityMatches.Count -eq 1) {
        $SessionId = [string]$responsibilityMatches[0].sessionId
        $selectedByResponsibility = $true
    }
    elseif ($responsibilityMatches.Count -gt 1) {
        $candidateText = @($responsibilityMatches | ForEach-Object {
                ([string]$_.sessionId) + ' | ' + ([string]$_.tabTitle) + ' | ' + ([string]$_.lastSeenUtc)
            }) -join "`n"
        throw "ResponsibilityKey matched multiple registered sessions. Pass an exact SessionId:`n$candidateText"
    }
}
if (-not [string]::IsNullOrWhiteSpace($SessionId)) {
    $parsedSessionId = [Guid]::Empty
    if (-not [Guid]::TryParse($SessionId.Trim(), [ref]$parsedSessionId)) {
        throw 'SessionId must be an exact Codex session UUID.'
    }
    $SessionId = $parsedSessionId.ToString()
}
if ($Mode -in @('Resume', 'Fork') -and [string]::IsNullOrWhiteSpace($SessionId)) {
    throw "Managed $Mode requires an exact SessionId because the official picker cannot append the mandatory launch-envelope prompt. Use Status/Query/Resolve to identify one exact session, then retry."
}
$restoredEntry = Find-SessionRegistryEntry $registry $SessionId

$effectiveResponsibilityKey = $ResponsibilityKey.Trim()
if ([string]::IsNullOrWhiteSpace($effectiveResponsibilityKey) -and $null -ne $restoredEntry) {
    $effectiveResponsibilityKey = [string]$restoredEntry.responsibilityKey
}
if ([string]::IsNullOrWhiteSpace($effectiveResponsibilityKey)) { $effectiveResponsibilityKey = 'default' }

$effectiveTaskKey = $TaskKey.Trim()
if ([string]::IsNullOrWhiteSpace($effectiveTaskKey) -and $null -ne $restoredEntry) {
    $effectiveTaskKey = [string]$restoredEntry.taskKey
}
if ([string]::IsNullOrWhiteSpace($effectiveTaskKey) -and -not [string]::IsNullOrWhiteSpace($TaskPrompt)) {
    $effectiveTaskKey = $TaskPrompt.Trim()
}
if ([string]::IsNullOrWhiteSpace($effectiveTaskKey) -and -not [string]::IsNullOrWhiteSpace($SessionId)) {
    $effectiveTaskKey = $SessionId
}
if ([string]::IsNullOrWhiteSpace($effectiveTaskKey)) { $effectiveTaskKey = 'default' }

$effectiveTabTitle = $TabTitle.Trim()
if ([string]::IsNullOrWhiteSpace($effectiveTabTitle) -and $null -ne $restoredEntry) {
    $effectiveTabTitle = [string]$restoredEntry.tabTitle
}
$normalizedTitle = (($effectiveTabTitle.Trim() -replace '\.', '-') -replace '\s+', '').Replace([string][char]0x00B7, '-')
$abstractTitle = Test-AbstractTabTitle $effectiveTabTitle
if ($abstractTitle) {
    $handoffTitleContext = @($HandoffPath | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | ForEach-Object { Get-Content -LiteralPath $_ -Encoding UTF8 -TotalCount 40 -ErrorAction SilentlyContinue }) -join ' '
    $effectiveTabTitle = Get-DefaultTabTitle -Key $effectiveResponsibilityKey -LaunchMode $Mode -ContextText ($TaskPrompt + ' ' + $effectiveTaskKey + ' ' + (@($HandoffPath) -join ' ') + ' ' + $handoffTitleContext)
}
$effectiveTabTitle = Get-SafeTabTitle $effectiveTabTitle
$effectiveWindowName = Get-SafeWindowName $TerminalWindowName

$handoffFiles = @(Resolve-HandoffFiles $resolvedProjectRoot $HandoffPath)
$readOnlyContext = $null
if (-not [string]::IsNullOrWhiteSpace($ReadOnlyContextPath)) {
    $readOnlyRoot = Join-Path $localStateBase 'read-only-contexts'
    $readOnlyFull = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $ReadOnlyContextPath -ErrorAction Stop).Path)
    if (-not $readOnlyFull.StartsWith(([IO.Path]::GetFullPath($readOnlyRoot)).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'ReadOnlyContextPath must stay inside the private read-only context store.'
    }
    if (-not (Test-Path -LiteralPath $readOnlyFull -PathType Leaf)) { throw "Read-only context packet was not found: $readOnlyFull" }
    $readOnlyContext = [pscustomobject]@{
        relativePath = 'ES/AIReadOnlyContext/' + [IO.Path]::GetFileName($readOnlyFull)
        absolutePath = $readOnlyFull
        sha256 = Get-FileSha256 $readOnlyFull
        length = (Get-Item -LiteralPath $readOnlyFull).Length
    }
}
if ($null -ne $readOnlyContext) { $handoffFiles += $readOnlyContext }
if ($HandoffMode -and $Mode -eq 'New') {
    Assert-HandoffResponsibility $effectiveResponsibilityKey $effectiveTabTitle $handoffFiles
}
$gitSnapshot = Get-GitSnapshot $resolvedProjectRoot
$taskFingerprint = Get-Sha256 ($Mode + '|' + $resolvedProjectRoot + '|' + $effectiveTaskKey + '|' + $SessionId + '|' + $effectiveResponsibilityKey)
$launchToken = 'CodexLaunch:' + $taskFingerprint.Substring(0, 16) + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$statePath = Join-Path $launchStateRoot ($taskFingerprint + '.json')
$launchWtSession = if ($effectiveTerminalMode -eq 'CurrentWindow') { [string]$env:WT_SESSION } else { '' }
$windowKey = switch ($effectiveTerminalMode) {
    'CurrentWindow' { 'wt-session:' + $launchWtSession }
    'ProjectWindow' { 'named-window:' + $effectiveWindowName }
    'NewWindow' { 'launch-window:' + $launchToken }
    'PlainCmd' { 'plain-cmd:' + $launchToken }
    default { 'unknown:' + $launchToken }
}
$hookConfigPath = Join-Path $resolvedProjectRoot '.codex\hooks.json'
$hookConfigPresent = Test-Path -LiteralPath $hookConfigPath -PathType Leaf

$result = [ordered]@{
    mode = $Mode
    projectRoot = $resolvedProjectRoot
    projectIdentityFingerprint = $actualProjectIdentityFingerprint
    codexCli = $codexCmdPath
    windowsTerminal = if ($null -eq $wtCommand) { '' } else { $wtCommand.Source }
    terminalMode = $effectiveTerminalMode
    terminalWindowName = $effectiveWindowName
    tabTitle = $effectiveTabTitle
    responsibilityKey = $effectiveResponsibilityKey
    exactSession = -not [string]::IsNullOrWhiteSpace($SessionId)
    selectedByResponsibility = $selectedByResponsibility
    usesOfficialPicker = $Mode -in @('Resume', 'Fork') -and [string]::IsNullOrWhiteSpace($SessionId)
    restoredSessionMetadata = $null -ne $restoredEntry
    requiredPathsValid = $true
    taskKey = $effectiveTaskKey
    taskFingerprint = $taskFingerprint
    launchToken = $launchToken
    contextAuthority = 'highest'
    contextInjectionRequired = $true
    handoffFiles = $handoffFiles
    readOnlyContext = $null -ne $readOnlyContext
    resumeUsed = $Mode -in @('Resume', 'Fork')
    crossAiResume = $false
    gitBranch = $gitSnapshot.branch
    gitHead = $gitSnapshot.head
    envelopePath = ''
    handoffSnapshotDirectory = ''
    alreadyRunning = $false
    processId = 0
    terminalLauncherProcessId = 0
    terminalWindowProcessId = 0
    sessionId = if ($Mode -eq 'Resume' -and -not [string]::IsNullOrWhiteSpace($SessionId)) { $SessionId } else { '' }
    recordId = ''
    launched = $false
    terminalStarted = $false
    promptObserved = $false
    contextAccepted = $false
    startupFailed = $false
    startupTimedOut = $false
    launchPhase = 'Prepared'
    acceptanceReceiptPath = ''
    startupDiagnosticPath = ''
    startupFailureReason = ''
    dryRun = [bool]$DryRun
    codexArguments = @()
    skipHooks = [bool]$SkipHooks
    hookConfigPath = if ($hookConfigPresent) { $hookConfigPath } else { '' }
    hookConfigPresent = $hookConfigPresent
    hookTrustVerified = $false
    hookActivationNote = if ($hookConfigPresent) { 'Review the exact project hook definition with /hooks. Existing trust/load state is not inferred.' } else { '' }
}

if ($Mode -ne 'Validate') {
    $previewPrompt = 'Run the ES launch-envelope validator at ' + $envelopeValidatorPath + ' against <created-on-launch-envelope> with LaunchToken ' + $launchToken + ' before using any handoff. This is a one-time acceptance gate, not a continuous runtime lease. After successful acceptance, later envelope loss does not stop the current conversation; continue only from already accepted transcript/context and never substitute another handoff source. Consume only envelope.handoffFiles absolutePath values, which are private per-launch snapshots; never substitute their mutable sourceAbsolutePath values. Project Skills are rooted at ' + $projectSkillsRoot + '; for any handoff or Codex session operation, read ' + $sessionBootstrapSkillPath + ' before claiming no matching Skill exists. If project hooks require review, report that state and use /hooks for explicit trust; never bypass hook trust automatically. Then execute envelope.taskPrompt under the ES initialization and authorization rules. Launch token ' + $launchToken + '.'
    $previewArguments = [Collections.Generic.List[string]]::new()
    switch ($Mode) {
        'New' {
            $previewArguments.Add('-C')
            $previewArguments.Add($resolvedProjectRoot)
            $previewArguments.Add($previewPrompt)
        }
        'Resume' {
            $previewArguments.Add('resume')
            $previewArguments.Add('-C')
            $previewArguments.Add($resolvedProjectRoot)
            if ($AllSessions) { $previewArguments.Add('--all') }
            if (-not [string]::IsNullOrWhiteSpace($SessionId)) {
                $previewArguments.Add($SessionId)
                $previewArguments.Add($previewPrompt)
            }
        }
        'Fork' {
            $previewArguments.Add('fork')
            $previewArguments.Add('-C')
            $previewArguments.Add($resolvedProjectRoot)
            if ($AllSessions) { $previewArguments.Add('--all') }
            if (-not [string]::IsNullOrWhiteSpace($SessionId)) {
                $previewArguments.Add($SessionId)
                $previewArguments.Add($previewPrompt)
            }
        }
    }
    if ($SkipHooks) {
        $previewArguments.Insert(0, 'hooks')
        $previewArguments.Insert(0, '--disable')
    }
    $result.codexArguments = @($previewArguments)
}

if ($Mode -eq 'Validate' -or $DryRun) {
    [pscustomobject]$result
    return
}

New-Item -ItemType Directory -Path $launchStateRoot -Force | Out-Null
New-Item -ItemType Directory -Path $envelopeRoot -Force | Out-Null
New-Item -ItemType Directory -Path $handoffSnapshotRoot -Force | Out-Null
New-Item -ItemType Directory -Path $commandRoot -Force | Out-Null
$mutex = [Threading.Mutex]::new($false, 'ESFrameworkCodexLaunch_' + $taskFingerprint)
$mutexAcquired = $false
try {
    $mutexAcquired = $mutex.WaitOne(0)
    if (-not $mutexAcquired) {
        $result.alreadyRunning = $true
        [pscustomobject]$result
        return
    }

    if (-not $ForceNew -and (Test-Path -LiteralPath $statePath -PathType Leaf)) {
        try {
            $existing = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
            $existingProcessId = [int]$existing.processId
            if (-not (Test-ProcessAlive $existingProcessId) -and -not [string]::IsNullOrWhiteSpace([string]$existing.launchToken)) {
                $existingProcessId = Find-LaunchedShellProcessId ([string]$existing.launchToken)
            }
            if (Test-ProcessAlive $existingProcessId) {
                if ([bool]$existing.requiresV2Resume) {
                    throw "A live schema v1 session still owns this task. Close and Resume its exact SessionId to create a schema v2 snapshot: $([string]$existing.sessionId)"
                }
                $result.alreadyRunning = $true
                $result.processId = $existingProcessId
                $result.terminalLauncherProcessId = [int]$existing.terminalLauncherProcessId
                $result.sessionId = [string]$existing.sessionId
                $existingLaunchToken = [string]$existing.launchToken
                $existingSessionId = [string]$existing.sessionId
                $existingRecord = @($registry.sessions | Where-Object {
                        if ((-not [string]::IsNullOrWhiteSpace($existingLaunchToken)) -and ([string]$_.launchToken -eq $existingLaunchToken)) {
                            return $true
                        }
                        if ((-not [string]::IsNullOrWhiteSpace($existingSessionId)) -and ([string]$_.sessionId -eq $existingSessionId)) {
                            return $true
                        }
                        return $false
                    } | Select-Object -First 1)[0]
                if ($null -ne $existingRecord) {
                    $result.recordId = [string]$existingRecord.recordId
                }
                $result.envelopePath = [string]$existing.envelopePath
                $result.launchToken = [string]$existing.launchToken
                $result.terminalStarted = $true
                $result.launchPhase = [string](Get-ESCodexPropertyValue $existing 'launchPhase' 'TerminalStarted')
                $result.promptObserved = [bool](Get-ESCodexPropertyValue $existing 'promptObserved' $false)
                $result.contextAccepted = [bool](Get-ESCodexPropertyValue $existing 'contextAccepted' $false)
                $result.startupFailed = [bool](Get-ESCodexPropertyValue $existing 'startupFailed' $false)
                $result.startupTimedOut = [bool](Get-ESCodexPropertyValue $existing 'startupTimedOut' $false)
                $result.startupFailureReason = [string](Get-ESCodexPropertyValue $existing 'startupFailureReason' '')
                $result.acceptanceReceiptPath = [string](Get-ESCodexPropertyValue $existing 'acceptanceReceiptPath' '')
                $result.startupDiagnosticPath = [string](Get-ESCodexPropertyValue $existing 'startupDiagnosticPath' '')
                if (-not [string]::IsNullOrWhiteSpace($result.envelopePath) -and (Test-Path -LiteralPath $result.envelopePath -PathType Leaf)) {
                    $existingReadiness = Get-ESCodexLaunchReadiness `
                        -LaunchToken $result.launchToken `
                        -EnvelopePath $result.envelopePath `
                        -ProjectRoot $resolvedProjectRoot `
                        -ReceiptRoot (Join-Path $localStateBase 'acceptance-receipts') `
                        -HistoryPath $historyPath `
                        -StartedAtUnix ([long](Get-ESCodexPropertyValue $existing 'startedAtUnix' 0)) `
                        -ExitMarkerPath $result.startupDiagnosticPath `
                        -KnownSessionId $result.sessionId
                    $result.launchPhase = [string]$existingReadiness.launchPhase
                    $result.promptObserved = [bool]$existingReadiness.promptObserved
                    $result.contextAccepted = [bool]$existingReadiness.contextAccepted
                    $result.startupFailed = [bool]$existingReadiness.startupFailed
                    $result.startupFailureReason = [string]$existingReadiness.failureReason
                    $result.acceptanceReceiptPath = [string]$existingReadiness.acceptanceReceiptPath
                }
                [pscustomobject]$result
                return
            }
        }
        catch {
            # A concurrent/partially-written registry entry is not a valid resume proof.
            # Continue with a fresh envelope, but keep the condition observable.
            Write-Verbose ("Ignoring unreadable existing Codex session record: " + $_.Exception.Message)
        }
    }

    $envelopeName = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssfffZ') + '-' + $launchToken.Substring($launchToken.Length - 8) + '.json'
    $envelopePath = Join-Path $envelopeRoot $envelopeName
    $snapshotDirectoryName = [IO.Path]::GetFileNameWithoutExtension($envelopeName)
    $handoffSnapshotDirectory = Join-Path $handoffSnapshotRoot $snapshotDirectoryName
    $handoffFiles = @(New-HandoffSnapshots $handoffFiles $handoffSnapshotDirectory)
    if ($null -ne $readOnlyContext) {
        # A read-only restore must not expose its mutable source locator to the
        # receiving AI. The private snapshot absolutePath is the sole consumable
        # context source; sourceAbsolutePath is intentionally dropped.
        $handoffFiles = @($handoffFiles | ForEach-Object {
                [pscustomobject][ordered]@{
                    relativePath = [string]$_.relativePath
                    absolutePath = [string]$_.absolutePath
                    sha256 = [string]$_.sha256
                    length = [long]$_.length
                    snapshot = $true
                }
            })
    }
    $result.handoffFiles = $handoffFiles
    $result.handoffSnapshotDirectory = $handoffSnapshotDirectory
    $envelope = [ordered]@{
        schemaVersion = 2
        immutable = $true
        launchToken = $launchToken
        createdUtc = [DateTime]::UtcNow.ToString('o')
        mode = $Mode
        projectRoot = $resolvedProjectRoot
        projectKey = 'ESFramework'
        taskKey = $effectiveTaskKey
        taskFingerprint = $taskFingerprint
        responsibilityKey = $effectiveResponsibilityKey
        tabTitle = $effectiveTabTitle
        requestedSessionId = $SessionId
        taskPrompt = $TaskPrompt.Trim()
        contextAuthority = 'highest'
        contextInjectionRequired = $true
        contextSources = @('taskPrompt','taskKey','responsibilityContext','handoffFiles.absolutePath')
        taskDeliveryMode = if ($DeferTaskPrompt) { 'post-acceptance' } else { 'bootstrap-inline' }
        handoffMode = 'PerLaunchSnapshot'
        handoffSnapshotDirectory = $handoffSnapshotDirectory
        handoffFiles = $handoffFiles
        readOnlyContext = $null -ne $readOnlyContext
        resumeUsed = $Mode -in @('Resume', 'Fork')
        crossAiResume = $false
        git = $gitSnapshot
        authorizationBoundary = 'Read initialization context first. Do not write history, audit state, Git, release, or delete without current explicit authorization.'
    }
    $envelopeJson = $envelope | ConvertTo-Json -Depth 8
    $envelopeStream = [IO.File]::Open($envelopePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $envelopeBytes = [Text.UTF8Encoding]::new($false).GetBytes($envelopeJson)
        $envelopeStream.Write($envelopeBytes, 0, $envelopeBytes.Length)
        $envelopeStream.Flush($true)
    }
    finally {
        $envelopeStream.Dispose()
    }
    $result.envelopePath = $envelopePath

    $taskInstruction = if ($DeferTaskPrompt) { 'Do not execute envelope.taskPrompt during bootstrap. Wait for a post-acceptance task message from the orchestrator.' } else { 'Report initialization and execute envelope.taskPrompt in Chinese.' }
    $initialPrompt = 'Run the ES launch-envelope validator at ' + $envelopeValidatorPath + ' against ' + $envelopePath + ' with LaunchToken ' + $launchToken + ' before using any handoff. A first-acceptance non-zero result is a hard context-drift failure; report it instead of silently switching context. This is a one-time acceptance gate, not a continuous runtime lease. After successful acceptance, later envelope loss does not stop the current conversation; continue only from already accepted transcript/context and never substitute another handoff source or claim fresh artifact verification. Consume only envelope.handoffFiles absolutePath values, which are private per-launch snapshots; never substitute their mutable sourceAbsolutePath values. Project Skills are rooted at ' + $projectSkillsRoot + '; for any handoff or Codex session operation, read ' + $sessionBootstrapSkillPath + ' before claiming no matching Skill exists, and do not treat global skill directories as authoritative for project Skill absence. Then read the project AGENTS.md first, followed by ES/AISpace/README.md for AI-content placement, the immutable envelope, AIWarnings README, CurrentStatus, RuleIndex, and matched task rules; do not claim a project concept is absent before checking AGENTS.md and its referenced authoritative README. Inspect branch, HEAD, and worktree read-only; ' + $taskInstruction + ' Before concluding that Unity is unavailable, inspect currently running Unity/UnityHub processes (including their executable paths, command lines, and window titles) and match the project path; an installed-path/PATH lookup alone is insufficient evidence of absence. Do not write history, audit state, Git, release, or delete without current explicit authorization. Launch token ' + $launchToken + '.'

    $codexArguments = [Collections.Generic.List[string]]::new()
    switch ($Mode) {
        'New' {
            $codexArguments.Add('-C')
            $codexArguments.Add($resolvedProjectRoot)
            $codexArguments.Add($initialPrompt)
        }
        'Resume' {
            $codexArguments.Add('resume')
            $codexArguments.Add('-C')
            $codexArguments.Add($resolvedProjectRoot)
            if ($AllSessions) { $codexArguments.Add('--all') }
            if (-not [string]::IsNullOrWhiteSpace($SessionId)) {
                $codexArguments.Add($SessionId)
                $codexArguments.Add($initialPrompt)
            }
        }
        'Fork' {
            $codexArguments.Add('fork')
            $codexArguments.Add('-C')
            $codexArguments.Add($resolvedProjectRoot)
            if ($AllSessions) { $codexArguments.Add('--all') }
            if (-not [string]::IsNullOrWhiteSpace($SessionId)) {
                $codexArguments.Add($SessionId)
                $codexArguments.Add($initialPrompt)
            }
        }
    }
    if ($SkipHooks) {
        $codexArguments.Insert(0, 'hooks')
        $codexArguments.Insert(0, '--disable')
    }
    $result.codexArguments = @($codexArguments)

    $quote = [char]34
    $doubleQuote = [string]$quote + [string]$quote
    $cmdParts = @($quote + $codexCmdPath + $quote)
    $cmdParts += @($codexArguments | ForEach-Object {
            $quote + ([string]$_).Replace([string]$quote, $doubleQuote) + $quote
        })
    $commandBaseName = $taskFingerprint + '-' + $launchToken.Substring($launchToken.Length - 8)
    $commandWrapperPath = Join-Path $commandRoot ($commandBaseName + '.cmd')
    $exitMarkerPath = Join-Path $commandRoot ($commandBaseName + '.exit.json')
    $exitMarkerWriterArguments = @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'RemoteSigned',
        '-File', $exitMarkerWriterPath,
        '-Path', $exitMarkerPath,
        '-ExpectedRoot', $commandRoot,
        '-LaunchToken', $launchToken,
        '-ExitCode', '%ES_CODEX_EXIT_CODE%'
    )
    $exitMarkerWriterCommand = 'powershell.exe ' + (@($exitMarkerWriterArguments |
            ForEach-Object { ConvertTo-WindowsArgument ([string]$_) }) -join ' ')
    $commandWrapperContent = "@echo off`r`nchcp 65001 >nul`r`nset `"ES_CODEX_LAUNCH_TOKEN=$launchToken`"`r`ncall " + ($cmdParts -join ' ') + "`r`nset `"ES_CODEX_EXIT_CODE=%ERRORLEVEL%`"`r`n" + $exitMarkerWriterCommand + "`r`n"
    Write-CreateOnlyUtf8File $commandWrapperPath $commandWrapperContent
    $startedAtUnix = [DateTimeOffset]::Now.ToUnixTimeSeconds()
    $terminalLauncherProcess = $null
    $shellProcessId = 0

    if ($effectiveTerminalMode -eq 'PlainCmd') {
        $plainCmdLine = 'title ' + $effectiveTabTitle + ' & call ' + (ConvertTo-WindowsArgument $commandWrapperPath) + ' ' + $launchToken
        $terminalLauncherProcess = Start-Process -FilePath 'cmd.exe' -ArgumentList @('/K', $plainCmdLine) -WorkingDirectory $resolvedProjectRoot -PassThru
        $shellProcessId = $terminalLauncherProcess.Id
    }
    else {
        $windowTarget = switch ($effectiveTerminalMode) {
            'NewWindow' { '-1' }
            'CurrentWindow' { '0' }
            default { $effectiveWindowName }
        }
        $wtArguments = @(
            '-w', $windowTarget,
            'new-tab',
            '--title', $effectiveTabTitle,
            '--suppressApplicationTitle',
            '-d', $resolvedProjectRoot,
            'cmd.exe', '/K', $commandWrapperPath, $launchToken
        )
        $wtArgumentLine = (@($wtArguments | ForEach-Object { ConvertTo-WindowsArgument ([string]$_) }) -join ' ')
        $terminalLauncherProcess = Start-Process -FilePath $wtCommand.Source -ArgumentList $wtArgumentLine -WorkingDirectory $resolvedProjectRoot -PassThru
        for ($attempt = 0; $attempt -lt 24; $attempt++) {
            Start-Sleep -Milliseconds 250
            $shellProcessId = Find-LaunchedShellProcessId $launchToken
            if ($shellProcessId -gt 0) { break }
        }
        if ($shellProcessId -le 0 -and -not $terminalLauncherProcess.HasExited) {
            $shellProcessId = $terminalLauncherProcess.Id
        }
    }

    $result.terminalStarted = $shellProcessId -gt 0
    $result.launched = $result.terminalStarted
    $result.startupFailed = -not $result.terminalStarted
    $result.launchPhase = if ($result.terminalStarted) { 'TerminalStarted' } else { 'Failed' }
    $result.startupFailureReason = if ($result.terminalStarted) { '' } else { 'The terminal launcher returned without an observable shell process for this launch token.' }
    $result.processId = $shellProcessId
    $result.terminalLauncherProcessId = $terminalLauncherProcess.Id
    $terminalWindowProcessId = if ($result.terminalStarted -and $effectiveTerminalMode -ne 'PlainCmd') {
        Get-ESCodexTerminalHostProcessId $shellProcessId
    }
    else { 0 }
    $result.terminalWindowProcessId = $terminalWindowProcessId
    $result.startupDiagnosticPath = if ($result.terminalStarted) { $exitMarkerPath } else { '' }
    $state = [ordered]@{
        taskKey = $effectiveTaskKey
        taskFingerprint = $taskFingerprint
        responsibilityKey = $effectiveResponsibilityKey
        tabTitle = $effectiveTabTitle
        terminalMode = $effectiveTerminalMode
        terminalWindowName = $effectiveWindowName
        windowKey = $windowKey
        wtSession = $launchWtSession
        processId = $shellProcessId
        terminalLauncherProcessId = $terminalLauncherProcess.Id
        terminalWindowProcessId = $terminalWindowProcessId
        sessionId = $result.sessionId
        recordId = ''
        startedAtUnix = $startedAtUnix
        launchToken = $launchToken
        envelopePath = $envelopePath
        handoffSnapshotDirectory = $handoffSnapshotDirectory
        commandWrapperPath = $commandWrapperPath
        startupDiagnosticPath = $exitMarkerPath
        launchPhase = [string]$result.launchPhase
        promptObserved = $false
        contextAccepted = $false
        startupFailed = [bool]$result.startupFailed
        startupTimedOut = $false
        startupFailureReason = [string]$result.startupFailureReason
        acceptanceReceiptPath = ''
        lifecycleStatus = if ($result.startupFailed) { 'LaunchFailed' } else { 'PendingPrompt' }
    }
    [IO.File]::WriteAllText($statePath, ($state | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
    $registryEntry = [ordered]@{
        sessionId = [string]$result.sessionId
        requestedSessionId = $SessionId
        projectKey = 'ESFramework'
        projectRoot = $resolvedProjectRoot
        responsibilityKey = $effectiveResponsibilityKey
        tabTitle = $effectiveTabTitle
        taskKey = $effectiveTaskKey
        taskFingerprint = $taskFingerprint
        terminalMode = $effectiveTerminalMode
        terminalWindowName = $effectiveWindowName
        windowKey = $windowKey
        wtSession = $launchWtSession
        processId = $shellProcessId
        terminalLauncherProcessId = $terminalLauncherProcess.Id
        terminalWindowProcessId = $terminalWindowProcessId
        launchToken = $launchToken
        handoffFiles = $handoffFiles
        envelopePath = $envelopePath
        handoffSnapshotDirectory = $handoffSnapshotDirectory
        commandWrapperPath = $commandWrapperPath
        launchPhase = [string]$result.launchPhase
        promptObserved = $false
        contextAccepted = $false
        startupFailed = [bool]$result.startupFailed
        startupTimedOut = $false
        startupFailureReason = [string]$result.startupFailureReason
        acceptanceReceiptPath = ''
        startupDiagnosticPath = $exitMarkerPath
        lastKnownHead = $gitSnapshot.head
        lifecycleStatus = if ($result.startupFailed) { 'LaunchFailed' } else { 'PendingPrompt' }
        registeredAtUtc = [DateTime]::UtcNow.ToString('o')
        lastSeenUtc = [DateTime]::UtcNow.ToString('o')
    }
    $registryUpdateContext = [pscustomobject]@{ entry = [pscustomobject]$registryEntry }
    $registeredEntry = Invoke-ESCodexRegistryUpdate -Path $registryPath -Update {
        param($currentRegistry, $context)
        AddOrUpdate-ESCodexSessionRecord $currentRegistry $context.entry
    } -Argument $registryUpdateContext
    $result.recordId = [string]$registeredEntry.recordId
    $state.recordId = $result.recordId

    $receiptRoot = Join-Path $localStateBase 'acceptance-receipts'
    $startupDeadline = [DateTime]::UtcNow.AddSeconds($StartupWaitSeconds)
    if (-not $result.startupFailed) {
        do {
            $readiness = Get-ESCodexLaunchReadiness `
                -LaunchToken $launchToken `
                -EnvelopePath $envelopePath `
                -ProjectRoot $resolvedProjectRoot `
                -ReceiptRoot $receiptRoot `
                -HistoryPath $historyPath `
                -StartedAtUnix $startedAtUnix `
                -ExitMarkerPath $exitMarkerPath `
                -KnownSessionId ([string]$result.sessionId)
            if (-not [string]::IsNullOrWhiteSpace([string]$readiness.sessionId)) { $result.sessionId = [string]$readiness.sessionId }
            $result.launchPhase = [string]$readiness.launchPhase
            $result.promptObserved = [bool]$readiness.promptObserved
            $result.contextAccepted = [bool]$readiness.contextAccepted
            $result.startupFailed = [bool]$readiness.startupFailed
            $result.acceptanceReceiptPath = [string]$readiness.acceptanceReceiptPath
            $result.startupFailureReason = [string]$readiness.failureReason
            if ($result.contextAccepted -or $result.startupFailed) { break }
            if ([DateTime]::UtcNow -lt $startupDeadline) { Start-Sleep -Milliseconds 1000 }
        } while ([DateTime]::UtcNow -lt $startupDeadline)
    }

    if ($result.terminalStarted -and -not $result.contextAccepted -and -not $result.startupFailed) {
        $result.startupTimedOut = $true
    }
    $lifecycleStatus = if ($result.contextAccepted) { 'Registered' } elseif ($result.startupFailed) { 'LaunchFailed' } elseif ($result.promptObserved) { 'PendingAcceptance' } else { 'PendingPrompt' }
    $state.sessionId = [string]$result.sessionId
    $state.launchPhase = [string]$result.launchPhase
    $state.promptObserved = [bool]$result.promptObserved
    $state.contextAccepted = [bool]$result.contextAccepted
    $state.startupFailed = [bool]$result.startupFailed
    $state.startupTimedOut = [bool]$result.startupTimedOut
    $state.startupFailureReason = [string]$result.startupFailureReason
    $state.acceptanceReceiptPath = [string]$result.acceptanceReceiptPath
    $state.lifecycleStatus = $lifecycleStatus
    [IO.File]::WriteAllText($statePath, ($state | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))

    $registryEntry.sessionId = [string]$result.sessionId
    $registryEntry.launchPhase = [string]$result.launchPhase
    $registryEntry.promptObserved = [bool]$result.promptObserved
    $registryEntry.contextAccepted = [bool]$result.contextAccepted
    $registryEntry.startupFailed = [bool]$result.startupFailed
    $registryEntry.startupTimedOut = [bool]$result.startupTimedOut
    $registryEntry.startupFailureReason = [string]$result.startupFailureReason
    $registryEntry.acceptanceReceiptPath = [string]$result.acceptanceReceiptPath
    $registryEntry.lifecycleStatus = $lifecycleStatus
    $registryEntry.lastSeenUtc = [DateTime]::UtcNow.ToString('o')
    $registryUpdateContext = [pscustomobject]@{ entry = [pscustomobject]$registryEntry }
    $registeredEntry = Invoke-ESCodexRegistryUpdate -Path $registryPath -Update {
        param($currentRegistry, $context)
        AddOrUpdate-ESCodexSessionRecord $currentRegistry $context.entry
    } -Argument $registryUpdateContext
    $result.recordId = [string]$registeredEntry.recordId
    $state.recordId = $result.recordId
    [IO.File]::WriteAllText($statePath, ($state | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))

    [pscustomobject]$result
}
finally {
    if ($mutexAcquired) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
