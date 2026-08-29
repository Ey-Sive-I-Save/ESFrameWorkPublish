Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:IdPattern = '^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$'
$script:HashPattern = '^[a-f0-9]{64}$'

function ConvertTo-ESAITalkCanonical($Value) {
    if ($null -eq $Value) { return 'null' }
    if ($Value -is [string] -or $Value -is [char]) { return ([string]$Value | ConvertTo-Json -Compress) }
    if ($Value -is [bool]) { return $(if ($Value) { 'true' } else { 'false' }) }
    if ($Value -is [Collections.IDictionary]) {
        return '{' + ((@($Value.Keys | ForEach-Object { [string]$_ } | Sort-Object) | ForEach-Object { ('{0}:{1}' -f ($_ | ConvertTo-Json -Compress), (ConvertTo-ESAITalkCanonical $Value[$_])) }) -join ',') + '}'
    }
    if ($Value -is [pscustomobject]) {
        return '{' + ((@($Value.PSObject.Properties | Sort-Object Name) | ForEach-Object { ('{0}:{1}' -f ($_.Name | ConvertTo-Json -Compress), (ConvertTo-ESAITalkCanonical $_.Value)) }) -join ',') + '}'
    }
    if ($Value -is [Collections.IEnumerable] -and $Value -isnot [string]) { return '[' + ((@($Value) | ForEach-Object { ConvertTo-ESAITalkCanonical $_ }) -join ',') + ']' }
    return ([string]$Value | ConvertTo-Json -Compress)
}

function Get-ESAITalkMessageHash($Message) {
    $copy = [ordered]@{}
    foreach ($p in @($Message.PSObject.Properties | Where-Object { $_.Name -notin @('messageHash') })) { $copy[$p.Name] = $p.Value }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes((ConvertTo-ESAITalkCanonical $copy)))).Replace('-', '').ToLowerInvariant()) } finally { $sha.Dispose() }
}

function Assert-ESAITalkId([string]$Value, [string]$Name) { if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch $script:IdPattern) { throw "$Name is invalid." } }
function Assert-ESAITalkHash([string]$Value, [string]$Name) { if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch $script:HashPattern) { throw "$Name is invalid." } }

function Assert-ESAITalkNoReparsePoints([string]$Root) {
    $rootItem = Get-Item -LiteralPath $Root -Force -ErrorAction Stop
    if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "AITalk session root cannot be a reparse point: $Root" }
    foreach ($item in @(Get-ChildItem -LiteralPath $Root -Force -Recurse -ErrorAction Stop)) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "AITalk session contains a reparse point: $($item.FullName)"
        }
    }
}

function New-ESAITalkMessage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ConversationId,
        [Parameter(Mandatory)][string]$MessageId,
        [Parameter(Mandatory)][string]$AuthorId,
        [Parameter(Mandatory)][ValidateRange(1, 2147483647)][int]$Sequence,
        [Parameter(Mandatory)][ValidateSet('proposal','evidence','question','decision-request','acknowledgement')][string]$MessageKind,
        [Parameter(Mandatory)]$Payload,
        [object]$TaskBinding,
        [ValidateSet('public','private')][string]$Visibility = 'public',
        [string[]]$EvidenceRefs = @(),
        [string]$IdempotencyKey,
        [DateTime]$CreatedUtc = [DateTime]::UtcNow
    )
    Assert-ESAITalkId $ConversationId 'ConversationId'; Assert-ESAITalkId $MessageId 'MessageId'; Assert-ESAITalkId $AuthorId 'AuthorId'
    $binding = $null
    if ($null -ne $TaskBinding) {
        Assert-ESAITalkId ([string]$TaskBinding.taskId) 'TaskBinding.taskId'
        if ([int]$TaskBinding.taskRevision -lt 1 -or [int]$TaskBinding.contextVersion -lt 1) { throw 'TaskBinding revisions must be positive.' }
        foreach ($n in @('goalRevisionHash','routePlanHash','sourceScopeHash','collaborationPlanHash')) { if ($TaskBinding.PSObject.Properties.Name -contains $n -and $null -ne $TaskBinding.$n) { Assert-ESAITalkHash ([string]$TaskBinding.$n) "TaskBinding.$n" } }
        $binding = [ordered]@{ taskId=[string]$TaskBinding.taskId; taskRevision=[int]$TaskBinding.taskRevision; contextVersion=[int]$TaskBinding.contextVersion }
        foreach ($n in @('goalRevisionHash','routePlanHash','sourceScopeHash','parentTaskId','collaborationPlanHash')) { if ($TaskBinding.PSObject.Properties.Name -contains $n -and $null -ne $TaskBinding.$n) { $binding[$n] = $TaskBinding.$n.ToString().ToLowerInvariant() } }
        if ($binding.Contains('parentTaskId')) { Assert-ESAITalkId ([string]$binding.parentTaskId) 'TaskBinding.parentTaskId' }
    }
    if ($MessageKind -eq 'evidence' -and @($EvidenceRefs).Count -eq 0) { throw 'Evidence messages require at least one evidenceRef.' }
    $payloadHash = Get-ESAITalkMessageHash ([pscustomobject]@{ payload=$Payload })
    $base = [ordered]@{ schemaVersion=1; contractId='es://automation/contracts/task-collaboration/aitalk-message/v1'; conversationId=$ConversationId; messageId=$MessageId; authorId=$AuthorId; createdUtc=$CreatedUtc.ToUniversalTime().ToString('o'); sequence=$Sequence; messageKind=$MessageKind; taskBinding=$binding; visibility=$Visibility; payload=$Payload; payloadHash=$payloadHash; evidenceRefs=@($EvidenceRefs | Sort-Object -Unique); idempotencyKey=$null }
    if ([string]::IsNullOrWhiteSpace($IdempotencyKey)) { $base.idempotencyKey = 'aitalk-' + (Get-ESAITalkMessageHash ([pscustomobject]$base)).Substring(0,32) } else { $base.idempotencyKey = $IdempotencyKey }
    $base.messageHash = Get-ESAITalkMessageHash ([pscustomobject]$base)
    [pscustomobject]$base
}

function Test-ESAITalkMessage {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Message)
    $errors = [Collections.Generic.List[string]]::new()
    foreach ($required in @('schemaVersion','contractId','conversationId','messageId','authorId','createdUtc','sequence','messageKind','taskBinding','visibility','payload','payloadHash','evidenceRefs','idempotencyKey','messageHash')) {
        if ($null -eq $Message.PSObject.Properties[$required]) { [void]$errors.Add("Missing $required.") }
    }
    if ($errors.Count -gt 0) { return [pscustomobject][ordered]@{ valid=$false; errors=@($errors); messageId=$null; computedHash=$null } }
    if ([int]$Message.schemaVersion -ne 1) { [void]$errors.Add('schemaVersion must be 1.') }
    if ([string]$Message.contractId -cne 'es://automation/contracts/task-collaboration/aitalk-message/v1') { [void]$errors.Add('contractId is invalid.') }
    try { Assert-ESAITalkId ([string]$Message.conversationId) 'conversationId' } catch { [void]$errors.Add($_.Exception.Message) }
    try { Assert-ESAITalkId ([string]$Message.messageId) 'messageId' } catch { [void]$errors.Add($_.Exception.Message) }
    try { Assert-ESAITalkId ([string]$Message.authorId) 'authorId' } catch { [void]$errors.Add($_.Exception.Message) }
    if ([int]$Message.sequence -lt 1) { [void]$errors.Add('sequence must be positive.') }
    if ([string]$Message.messageKind -notin @('proposal','evidence','question','decision-request','acknowledgement')) { [void]$errors.Add('messageKind is invalid.') }
    if ([string]$Message.visibility -notin @('public','private')) { [void]$errors.Add('visibility is invalid.') }
    if ($null -ne $Message.taskBinding) {
        try { Assert-ESAITalkId ([string]$Message.taskBinding.taskId) 'taskBinding.taskId'; if ([int]$Message.taskBinding.taskRevision -lt 1 -or [int]$Message.taskBinding.contextVersion -lt 1) { throw 'taskBinding revisions must be positive.' } } catch { [void]$errors.Add($_.Exception.Message) }
    }
    if ([string]$Message.messageKind -ceq 'evidence' -and @($Message.evidenceRefs).Count -eq 0) { [void]$errors.Add('Evidence messages require evidenceRefs.') }
    try { Assert-ESAITalkHash ([string]$Message.payloadHash) 'payloadHash' } catch { [void]$errors.Add($_.Exception.Message) }
    $computed = Get-ESAITalkMessageHash $Message
    if ([string]$Message.messageHash -cne $computed) { [void]$errors.Add('messageHash does not match canonical message.') }
    [pscustomobject][ordered]@{ valid=($errors.Count -eq 0); errors=@($errors); messageId=[string]$Message.messageId; computedHash=$computed }
}

function Get-ESAITalkMarkdownSection([string]$Text, [string]$Heading) {
    $pattern = '(?ms)^##\s+' + [regex]::Escape($Heading) + '\s*\r?\n(?<body>.*?)(?=^##\s|\z)'
    $match = [regex]::Match($Text, $pattern)
    if (-not $match.Success) { return '' }
    return $match.Groups['body'].Value.Trim()
}

function ConvertTo-ESAITalkSafeAuthorId([string]$DisplayName) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($DisplayName))).Replace('-', '').ToLowerInvariant()).Substring(0,16) } finally { $sha.Dispose() }
    return 'author-' + $hash
}

function ConvertTo-ESAITalkSafeConversationId([string]$DisplayName) {
    if ($DisplayName -match $script:IdPattern) { return $DisplayName }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return 'conversation-' + ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($DisplayName))).Replace('-', '').ToLowerInvariant()).Substring(0,24) } finally { $sha.Dispose() }
}

function ConvertFrom-ESAITalkMarkdownMessage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [string]$ConversationId,
        [object]$TaskBinding,
        [ValidateRange(1024,4194304)][int]$MaxMessageBytes = 262144
    )
    $full = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
    $fileInfo = Get-Item -LiteralPath $full
    if ([int64]$fileInfo.Length -gt $MaxMessageBytes) { throw "MESSAGE_SIZE_LIMIT_EXCEEDED: $([IO.Path]::GetFileName($full)) bytes=$($fileInfo.Length) limit=$MaxMessageBytes" }
    $text = [IO.File]::ReadAllText($full, [Text.UTF8Encoding]::new($false, $true))
    $name = [IO.Path]::GetFileNameWithoutExtension($full)
    $fileMatch = [regex]::Match($name, '^(?<sequence>\d{1,8})_(?<author>.+)$')
    if (-not $fileMatch.Success) { throw "AITalk message filename is invalid: $name" }
    $sequence = [int]$fileMatch.Groups['sequence'].Value
    $displayName = $fileMatch.Groups['author'].Value
    $headingMatch = [regex]::Match($text, '^#\s*发言\s+\d+\s*-\s*(?<author>.+)$', [Text.RegularExpressions.RegexOptions]::Multiline)
    if ($headingMatch.Success) { $displayName = $headingMatch.Groups['author'].Value.Trim() }
    $conversation = $ConversationId
    if ([string]::IsNullOrWhiteSpace($conversation)) { $conversation = Split-Path (Split-Path (Split-Path $full -Parent) -Parent) -Leaf }
    $conversation = ConvertTo-ESAITalkSafeConversationId $conversation
    Assert-ESAITalkId $conversation 'ConversationId'
    $timeLine = [regex]::Match($text, '(?m)^时间：\s*(?<value>.+?)\s*$')
    $created = if ($timeLine.Success) { try { [DateTime]::Parse($timeLine.Groups['value'].Value).ToUniversalTime() } catch { (Get-Item -LiteralPath $full).LastWriteTimeUtc } } else { (Get-Item -LiteralPath $full).LastWriteTimeUtc }
    $evidenceText = Get-ESAITalkMarkdownSection $text '证据或代码位置'
    $evidenceLines = $evidenceText -split '\r?\n'
    $evidence = @($evidenceLines | ForEach-Object { $_.Trim() } | Where-Object { $_ -and $_ -notmatch '^\x60{3}' } | ForEach-Object { $_ -replace '^[-*]\s*','' } | Sort-Object -Unique)
    $kind = 'proposal'
    $kindText = Get-ESAITalkMarkdownSection $text '消息类型'
    if ($kindText -match '^(proposal|evidence|question|decision-request|acknowledgement)$') { $kind = $Matches[1] }
    $payload = [ordered]@{
        authorDisplayName = $displayName
        judgement = Get-ESAITalkMarkdownSection $text '我的判断'
        evidenceOrCode = $evidenceText
        agreedPoints = Get-ESAITalkMarkdownSection $text '我同意的点'
        disagreements = Get-ESAITalkMarkdownSection $text '我不同意的点'
        proposal = Get-ESAITalkMarkdownSection $text '我建议的方案'
        questions = Get-ESAITalkMarkdownSection $text '需要其他 AI 回答的问题'
        canEnd = Get-ESAITalkMarkdownSection $text '是否可以结束'
    }
    $messageId = 'msg-' + $sequence.ToString('D8') + '-' + (ConvertTo-ESAITalkSafeAuthorId $displayName).Substring(7)
    New-ESAITalkMessage -ConversationId $conversation -MessageId $messageId -AuthorId (ConvertTo-ESAITalkSafeAuthorId $displayName) -Sequence $sequence -MessageKind $kind -TaskBinding $TaskBinding -Payload $payload -EvidenceRefs $evidence -CreatedUtc $created
}

function ConvertTo-ESAITalkConsensusMarkdown([Parameter(Mandatory)]$Aggregation) {
    $lines = [Collections.Generic.List[string]]::new()
    [void]$lines.Add('# 当前共同意见')
    [void]$lines.Add('')
    [void]$lines.Add(('聚合状态：{0}{1}{0}' -f [char]96,$Aggregation.status))
    [void]$lines.Add(('聚合 ID：{0}{1}{0}' -f [char]96,$Aggregation.aggregationId))
    [void]$lines.Add('')
    [void]$lines.Add('## 候选消息')
    [void]$lines.Add('')
    if (@($Aggregation.messages).Count -eq 0) {
        [void]$lines.Add('暂无可接受消息。')
    } else {
        foreach ($m in @($Aggregation.messages)) {
            [void]$lines.Add(('- 序号 {0} / 作者 {4}{1}{4} / 类型 {4}{2}{4} / messageId {4}{3}{4}' -f $m.sequence,$m.authorId,$m.messageKind,$m.messageId,[char]96))
            [void]$lines.Add(('  - payloadHash: {0}{1}{0}' -f [char]96,$m.payloadHash))
            if (@($m.evidenceRefs).Count) {
                $refs = ($m.evidenceRefs | ForEach-Object { [char]96 + $_ + [char]96 }) -join ', '
                [void]$lines.Add(('  - evidenceRefs: {0}' -f $refs))
            }
        }
    }
    [void]$lines.Add('')
    [void]$lines.Add('## 隔离项')
    [void]$lines.Add('')
    if (@($Aggregation.quarantined).Count -eq 0) {
        [void]$lines.Add('暂无。')
    } else {
        foreach ($q in @($Aggregation.quarantined)) {
            [void]$lines.Add(('- messageId {2}{0}{2}：{2}{1}{2}' -f $q.messageId,$q.reasonCode,[char]96))
        }
    }
    [void]$lines.Add('')
    [void]$lines.Add('## 权威边界')
    [void]$lines.Add('')
    [void]$lines.Add(('- AITalk 聚合只产生候选意见，不声明 {0}Accepted{0} 或 {0}Completed{0}。' -f [char]96))
    [void]$lines.Add(('- 最终任务状态由 TaskContextRuntime 的 {0}completionDecision{0} 决定。' -f [char]96))
    return ($lines -join [Environment]::NewLine) + [Environment]::NewLine
}

function Invoke-ESAITalkSessionAggregation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$SessionPath,
        [string]$ConversationId,
        [object]$ExpectedTaskBinding,
        [string[]]$AllowedPrivateAuthorIds = @(),
        [ValidateRange(1,8192)][int]$MaxMessages = 512,
        [ValidateRange(1024,4194304)][int]$MaxMessageBytes = 262144,
        [switch]$WriteConsensus,
        [string]$ConsensusPath
    )
    $session = (Resolve-Path -LiteralPath $SessionPath -ErrorAction Stop).Path
    Assert-ESAITalkNoReparsePoints $session
    $messagesPath = Join-Path $session 'Messages'
    if (-not (Test-Path -LiteralPath $messagesPath -PathType Container)) { throw "AITalk Messages directory not found: $messagesPath" }
    $files = @(Get-ChildItem -LiteralPath $messagesPath -File -Filter '*.md' | Sort-Object Name)
    if ($files.Count -gt $MaxMessages) { throw "MESSAGE_COUNT_LIMIT_EXCEEDED: count=$($files.Count) limit=$MaxMessages" }
    # ExpectedTaskBinding is an admission filter, not a value to copy into
    # messages. Historical Markdown without an explicit binding must remain
    # unbound and be isolated when a bound task view is requested.
    $parsed = @($files | ForEach-Object { ConvertFrom-ESAITalkMarkdownMessage -Path $_.FullName -ConversationId $ConversationId -MaxMessageBytes $MaxMessageBytes })
    $conversation = if ([string]::IsNullOrWhiteSpace($ConversationId)) { ConvertTo-ESAITalkSafeConversationId (Split-Path $session -Leaf) } else { $ConversationId }
    $aggregation = Invoke-ESAITalkAggregation -ConversationId $conversation -ExpectedTaskBinding $ExpectedTaskBinding -AllowedPrivateAuthorIds $AllowedPrivateAuthorIds -Messages $parsed
    $aggregation | Add-Member -NotePropertyName discoveredMessageCount -NotePropertyValue $files.Count
    $aggregation | Add-Member -NotePropertyName sourceMessageFiles -NotePropertyValue @($files.FullName)
    if ($WriteConsensus) {
        $target = if ([string]::IsNullOrWhiteSpace($ConsensusPath)) { Join-Path $session 'Consensus/当前共同意见.md' } else { $ConsensusPath }
        $parent = Split-Path -Parent $target; if (-not (Test-Path -LiteralPath $parent -PathType Container)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        [IO.File]::WriteAllText($target, (ConvertTo-ESAITalkConsensusMarkdown $aggregation), [Text.UTF8Encoding]::new($false))
        $aggregation | Add-Member -NotePropertyName consensusPath -NotePropertyValue $target
    }
    $aggregation
}

function Invoke-ESAITalkAggregation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object[]]$Messages,
        [string]$ConversationId,
        [object]$ExpectedTaskBinding,
        [string[]]$AllowedPrivateAuthorIds = @()
    )
    # A direct aggregation may omit ConversationId, but it must never merge
    # messages from multiple conversations under a null identity.
    $effectiveConversationId = $ConversationId
    if ([string]::IsNullOrWhiteSpace($effectiveConversationId)) {
        $conversationIds = @($Messages |
            Where-Object { $null -ne $_ } |
            ForEach-Object {
                $property = $_.PSObject.Properties['conversationId']
                if ($null -ne $property -and -not [string]::IsNullOrWhiteSpace([string]$property.Value)) {
                    [string]$property.Value
                }
            } |
            Sort-Object -Unique)
        if ($conversationIds.Count -gt 1) {
            throw "CONVERSATION_ID_REQUIRED_FOR_MIXED_MESSAGES: conversations=$($conversationIds -join ',')"
        }
        if ($conversationIds.Count -eq 1) { $effectiveConversationId = $conversationIds[0] }
    }
    $accepted = [Collections.Generic.List[object]]::new(); $quarantine = [Collections.Generic.List[object]]::new(); $conflicts = [Collections.Generic.List[string]]::new(); $seenId = @{}; $seenKey = @{}
    foreach ($m in @($Messages)) {
        $check = Test-ESAITalkMessage $m
        if (-not $check.valid) { [void]$quarantine.Add([pscustomobject]@{ messageId=$m.messageId; reasonCode='INVALID_MESSAGE'; details=$check.errors }); continue }
        if (-not [string]::IsNullOrWhiteSpace($effectiveConversationId) -and [string]$m.conversationId -cne $effectiveConversationId) { [void]$quarantine.Add([pscustomobject]@{ messageId=$m.messageId; reasonCode='CONVERSATION_MISMATCH'; details=@() }); continue }
        if ($m.visibility -eq 'private' -and $AllowedPrivateAuthorIds -notcontains [string]$m.authorId) { [void]$quarantine.Add([pscustomobject]@{ messageId=$m.messageId; reasonCode='PRIVATE_NOT_AUTHORIZED'; details=@() }); continue }
        if ($seenId.ContainsKey([string]$m.messageId)) { if ($seenId[[string]$m.messageId] -ne [string]$m.messageHash) { [void]$conflicts.Add([string]$m.messageId) } else { [void]$quarantine.Add([pscustomobject]@{ messageId=$m.messageId; reasonCode='DUPLICATE_MESSAGE'; details=@() }) }; continue }
        if ($seenKey.ContainsKey([string]$m.idempotencyKey)) { if ($seenKey[[string]$m.idempotencyKey] -ne [string]$m.messageHash) { [void]$conflicts.Add([string]$m.idempotencyKey) } else { [void]$quarantine.Add([pscustomobject]@{ messageId=$m.messageId; reasonCode='DUPLICATE_IDEMPOTENCY'; details=@() }) }; continue }
        $seenId[[string]$m.messageId] = [string]$m.messageHash; $seenKey[[string]$m.idempotencyKey] = [string]$m.messageHash
        if ($null -ne $ExpectedTaskBinding) {
            $sameTask = $null -ne $m.taskBinding -and [string]$m.taskBinding.taskId -ceq [string]$ExpectedTaskBinding.taskId
            if (-not $sameTask) { [void]$quarantine.Add([pscustomobject]@{ messageId=$m.messageId; reasonCode='TASK_BINDING_MISMATCH'; details=@() }); continue }
            if ([int]$m.taskBinding.taskRevision -ne [int]$ExpectedTaskBinding.taskRevision -or [int]$m.taskBinding.contextVersion -ne [int]$ExpectedTaskBinding.contextVersion) { [void]$quarantine.Add([pscustomobject]@{ messageId=$m.messageId; reasonCode='STALE_TASK_CONTEXT'; details=@() }); continue }
            $bindingMismatch = $false
            foreach ($n in @('routePlanHash','sourceScopeHash','goalRevisionHash','collaborationPlanHash')) {
                $expectedHas = if ($ExpectedTaskBinding -is [Collections.IDictionary]) { $ExpectedTaskBinding.Contains($n) } else { $null -ne $ExpectedTaskBinding.PSObject.Properties[$n] }
                $messageHas = if ($m.taskBinding -is [Collections.IDictionary]) { $m.taskBinding.Contains($n) } else { $null -ne $m.taskBinding.PSObject.Properties[$n] }
                $expectedValue = if ($expectedHas) { if ($ExpectedTaskBinding -is [Collections.IDictionary]) { $ExpectedTaskBinding[$n] } else { $ExpectedTaskBinding.PSObject.Properties[$n].Value } } else { $null }
                $messageValue = if ($messageHas) { if ($m.taskBinding -is [Collections.IDictionary]) { $m.taskBinding[$n] } else { $m.taskBinding.PSObject.Properties[$n].Value } } else { $null }
                $different = if ($expectedHas -and $messageHas) { ([string]$messageValue) -cne ([string]$expectedValue) } else { $false }
                if ($different) {
                    [void]$quarantine.Add([pscustomobject]@{ messageId=$m.messageId; reasonCode=('BINDING_' + $n.ToUpperInvariant() + '_MISMATCH'); details=@() }); $bindingMismatch = $true; break
                }
            }
            if ($bindingMismatch) { continue }
        }
        [void]$accepted.Add($m)
    }
    $ordered = @($accepted | Sort-Object @{Expression={ [int]$_.sequence }}, @{Expression={ [DateTime]::Parse([string]$_.createdUtc).ToUniversalTime() }}, messageId)
    $status = if ($conflicts.Count) { 'conflict' } elseif ($ordered.Count -eq 0) { 'needs-review' } elseif ($quarantine.Count) { 'partial' } else { 'candidate' }
    $base = [ordered]@{ schemaVersion=1; contractId='es://automation/contracts/task-collaboration/aitalk-aggregation/v1'; recordType='AITalkAggregation'; aggregationId=$null; conversationId=$effectiveConversationId; status=$status; messages=$ordered; quarantined=@($quarantine); conflictKeys=@($conflicts | Sort-Object -Unique); completionDecisionRequired=$true; nonClaims=@('AITalk aggregation is advisory and never declares Accepted or Completed.','TaskContextRuntime completionDecision remains the only completion authority.','Chat payload is not evidence; evidenceRefs remain external references.') }
    $base.aggregationId = 'aitalk-agg-' + (Get-ESAITalkMessageHash ([pscustomobject]$base)).Substring(0,32); $base.aggregationHash = Get-ESAITalkMessageHash ([pscustomobject]$base); [pscustomobject]$base
}

function Convert-ESAITalkMessagesToResultEnvelopes {
    [CmdletBinding()]
    param([Parameter(Mandatory)][object[]]$Messages,[Parameter(Mandatory)][hashtable]$LeaseClaims,[Parameter(Mandatory)][string]$ParentTaskId,[Parameter(Mandatory)][string]$CollaborationPlanHash)
    $modulePath = Join-Path $PSScriptRoot 'ESTaskCollaborationContracts.psm1'; Import-Module $modulePath -Force
    foreach ($m in @($Messages | Where-Object { $_.messageKind -in @('proposal','evidence') -and $null -ne $_.taskBinding })) {
        $lease = $LeaseClaims[[string]$m.taskBinding.taskId]
        if ($null -eq $lease) { continue }
        New-ESResultEnvelope -ParentTaskId $ParentTaskId -ChildTaskId ([string]$m.taskBinding.taskId) -CollaborationPlanHash $CollaborationPlanHash -TaskRevision ([int]$m.taskBinding.taskRevision) -ContextVersion ([int]$m.taskBinding.contextVersion) -Attempt 1 -LeaseClaim $lease -ResultStatus candidate -OutputHash ([string]$m.payloadHash) -EvidenceRefs @($m.evidenceRefs) -IdempotencyKey ('aitalk:' + [string]$m.idempotencyKey) -CapturedUtc ([DateTime]::Parse([string]$m.createdUtc).ToUniversalTime())
    }
}

Export-ModuleMember -Function ConvertTo-ESAITalkCanonical,Get-ESAITalkMessageHash,New-ESAITalkMessage,Test-ESAITalkMessage,ConvertFrom-ESAITalkMarkdownMessage,Invoke-ESAITalkAggregation,Invoke-ESAITalkSessionAggregation,ConvertTo-ESAITalkConsensusMarkdown,Convert-ESAITalkMessagesToResultEnvelopes
