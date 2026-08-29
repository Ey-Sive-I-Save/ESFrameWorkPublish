Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ESAITalkAggregation.psm1') -Force

function Get-ESAITalkProjectHash($Value) {
    $canonical = ConvertTo-ESAITalkCanonical $Value
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical))).Replace('-', '').ToLowerInvariant()) } finally { $sha.Dispose() }
}

function Get-ESAITalkProjectSessionIndex {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$SessionsRoot,[ValidateRange(1,256)][int]$MaxSessions = 256,[ValidateRange(1,8192)][int]$MaxMessagesPerSession = 512,[ValidateRange(1024,4194304)][int]$MaxMessageBytes = 262144)
    $root = (Resolve-Path -LiteralPath $SessionsRoot -ErrorAction Stop).Path
    $allDirs = @(Get-ChildItem -LiteralPath $root -Directory | Sort-Object Name)
    $dirs = @($allDirs | Select-Object -First $MaxSessions)
    $records = [Collections.Generic.List[object]]::new()
    foreach ($dir in $dirs) {
        $messageDir = Join-Path $dir.FullName 'Messages'
        if (-not (Test-Path -LiteralPath $messageDir -PathType Container)) {
            [void]$records.Add([pscustomobject][ordered]@{
                sessionId = $dir.Name
                sessionPath = (Join-Path (Split-Path $root -Leaf) $dir.Name).Replace('\','/')
                status = 'needs-review'
                messageCount = 0
                quarantineCount = 1
                latestMessageUtc = $null
                hasConsensusFile = Test-Path -LiteralPath (Join-Path $dir.FullName 'Consensus/当前共同意见.md') -PathType Leaf
                aggregationHash = Get-ESAITalkProjectHash ([ordered]@{ sessionId=$dir.Name; reason='MESSAGES_DIRECTORY_MISSING' })
            })
            continue
        }
        try {
            $agg = Invoke-ESAITalkSessionAggregation -SessionPath $dir.FullName -MaxMessages $MaxMessagesPerSession -MaxMessageBytes $MaxMessageBytes
            $files = @(Get-ChildItem -LiteralPath $messageDir -File -Filter '*.md' | Sort-Object Name)
            $latest = if ($files.Count) { ([DateTime]($files | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).LastWriteTimeUtc).ToUniversalTime().ToString('o') } else { $null }
            [void]$records.Add([pscustomobject][ordered]@{
                sessionId = $dir.Name
                sessionPath = (Join-Path (Split-Path $root -Leaf) $dir.Name).Replace('\','/')
                status = [string]$agg.status
                messageCount = @($agg.messages).Count
                quarantineCount = @($agg.quarantined).Count
                latestMessageUtc = $latest
                hasConsensusFile = Test-Path -LiteralPath (Join-Path $dir.FullName 'Consensus/当前共同意见.md') -PathType Leaf
                aggregationHash = [string]$agg.aggregationHash
            })
        } catch {
            [void]$records.Add([pscustomobject][ordered]@{
                sessionId = $dir.Name
                sessionPath = (Join-Path (Split-Path $root -Leaf) $dir.Name).Replace('\','/')
                status = 'needs-review'
                messageCount = 0
                quarantineCount = 1
                latestMessageUtc = $null
                hasConsensusFile = Test-Path -LiteralPath (Join-Path $dir.FullName 'Consensus/当前共同意见.md') -PathType Leaf
                aggregationHash = Get-ESAITalkProjectHash ([ordered]@{ sessionId=$dir.Name; error=$_.Exception.Message })
            })
        }
    }
    @($records)
}

function Invoke-ESAITalkProjectAggregation {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$SessionsRoot,[ValidateRange(1,256)][int]$MaxSessions = 256,[ValidateRange(1,8192)][int]$MaxMessagesPerSession = 512,[ValidateRange(1024,4194304)][int]$MaxMessageBytes = 262144)
    $root = (Resolve-Path -LiteralPath $SessionsRoot -ErrorAction Stop).Path
    $allDirs = @(Get-ChildItem -LiteralPath $root -Directory)
    $index = @(Get-ESAITalkProjectSessionIndex -SessionsRoot $root -MaxSessions $MaxSessions -MaxMessagesPerSession $MaxMessagesPerSession -MaxMessageBytes $MaxMessageBytes)
    $statuses = @($index | ForEach-Object { [string]$_.status })
    $status = if ($statuses -contains 'conflict') { 'conflict' } elseif ($statuses -contains 'needs-review') { 'needs-review' } elseif ($statuses -contains 'partial') { 'partial' } else { 'candidate' }
    $base = [ordered]@{
        schemaVersion = 1
        contractId = 'es://automation/contracts/task-collaboration/aitalk-project-aggregation/v1'
        recordType = 'AITalkProjectAggregation'
        projectAggregationId = $null
        sessionsRoot = $root
        status = $status
        sessions = @($index)
        sessionCount = $index.Count
        discoveredSessionCount = @($allDirs).Count
        sessionLimitReached = (@($allDirs).Count -gt $index.Count)
        resourceLimits = [ordered]@{ maxSessions = $MaxSessions; maxMessagesPerSession = $MaxMessagesPerSession; maxMessageBytes = $MaxMessageBytes }
        messageCount = [int](@($index | Measure-Object messageCount -Sum).Sum)
        completionDecisionRequired = $true
        nonClaims = @('Project aggregation is an index and candidate projection, not a task lifecycle.', 'AITalk never declares Accepted or Completed.', 'Runtime, mailbox delivery, and release behavior are not proven by this static aggregation.')
    }
    $base.projectAggregationId = 'aitalk-project-' + (Get-ESAITalkProjectHash $base).Substring(0,32)
    $base.aggregationHash = Get-ESAITalkProjectHash $base
    [pscustomobject]$base
}

function Invoke-ESAITalkRoundGate {
    [CmdletBinding()]
    param([Parameter(Mandatory)][object[]]$RoundObservations,[ValidateRange(1,5)][int]$MaxRounds = 5)
    $errors = [Collections.Generic.List[string]]::new()
    $seenRounds = [Collections.Generic.HashSet[int]]::new()
    foreach ($o in @($RoundObservations)) {
        foreach ($required in @('round','consensusReached','userDecisionRequired')) {
            if ($null -eq $o.PSObject.Properties[$required]) { [void]$errors.Add("Round observation missing $required.") }
        }
        if ($null -eq $o.PSObject.Properties['round']) { continue }
        foreach ($flag in @('consensusReached','userDecisionRequired')) {
            if ($null -ne $o.PSObject.Properties[$flag] -and $o.PSObject.Properties[$flag].Value -isnot [bool]) { [void]$errors.Add("$flag must be boolean.") }
        }
        if ([int]$o.round -lt 1 -or [int]$o.round -gt $MaxRounds) { [void]$errors.Add("Round outside 1..${MaxRounds}: $($o.round)") }
        elseif (-not $seenRounds.Add([int]$o.round)) { [void]$errors.Add("Duplicate round observation: $($o.round)") }
    }
    if ($errors.Count) { throw ($errors -join '; ') }
    $ordered = @($RoundObservations | Sort-Object @{Expression={ [int]$_.round }})
    $consensus = @($ordered | Where-Object { [bool]$_.consensusReached })
    $userDecision = @($ordered | Where-Object { [bool]$_.userDecisionRequired })
    $lastRound = if ($ordered.Count) { [int]$ordered[-1].round } else { 0 }
    $status = 'continue'; $stopReason = $null; $stopRound = $null
    if ($userDecision.Count) { $status = 'needs-user-decision'; $stopReason = 'USER_DECISION_REQUIRED'; $stopRound = [int]$userDecision[0].round }
    elseif ($consensus.Count) { $status = 'consensus-reached'; $stopReason = 'EXPLICIT_CONSENSUS'; $stopRound = [int]$consensus[0].round }
    elseif ($lastRound -ge $MaxRounds) { $status = 'interrupted'; $stopReason = 'MAX_ROUNDS_EXCEEDED'; $stopRound = $MaxRounds }
    [pscustomobject][ordered]@{ schemaVersion=1; contractId='es://automation/contracts/task-collaboration/aitalk-round-gate/v1'; status=$status; maxRounds=$MaxRounds; observedRounds=@($ordered); lastRound=$lastRound; stopRound=$stopRound; stopReason=$stopReason; completionDecisionRequired=$true; nonClaims=@('Round gate does not declare task completion.','No consensus after the maximum rounds is an interruption, not consent.'); gateHash=(Get-ESAITalkProjectHash ([ordered]@{status=$status;maxRounds=$MaxRounds;observedRounds=@($ordered);stopReason=$stopReason})) }
}

Export-ModuleMember -Function Get-ESAITalkProjectSessionIndex,Invoke-ESAITalkProjectAggregation,Invoke-ESAITalkRoundGate
