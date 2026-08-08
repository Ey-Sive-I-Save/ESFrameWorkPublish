param(
    [Parameter(Mandatory = $true)]
    [string]$SessionPath,

    [Parameter(Mandatory = $true)]
    [string]$HistoryPath,

    [Parameter(Mandatory = $true)]
    [string]$ArchiveId,

    [Parameter(Mandatory = $true)]
    [string]$Title,

    [Parameter(Mandatory = $true)]
    [string]$FileOutline
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()

function Convert-ToLocalTime([string]$timestamp) {
    if ([string]::IsNullOrWhiteSpace($timestamp)) {
        return '时间待核'
    }

    return [DateTimeOffset]::Parse($timestamp).ToLocalTime().ToString('yyyy-MM-dd HH:mm:ss')
}

function Protect-SensitiveText([string]$text) {
    if ([string]::IsNullOrEmpty($text)) {
        return ''
    }

    $value = $text -replace '(?i)((?:access[_ -]?key(?:id|secret)?|secret(?:access)?key|api[_ -]?key|authorization|password)\s*[:=]\s*)[^\s,;]+', '$1<REDACTED>'
    $value = $value -replace '(?i)(bearer\s+)[A-Za-z0-9._~+/=-]{12,}', '$1<REDACTED>'
    $value = $value.Replace([string][char]0xFFFD, '<U+FFFD>')
    return $value
}

function Normalize-Excerpt([string]$text, [int]$limit) {
    $value = Protect-SensitiveText $text
    $value = $value -replace '[\u0000-\u0008\u000B\u000C\u000E-\u001F]', ''
    $value = $value -replace '\r?\n', ' '
    $value = $value -replace '\s+', ' '
    $value = $value.Trim()
    if ($value.Length -gt $limit) {
        return $value.Substring(0, $limit) + '...'
    }

    return $value
}

function Get-TaskKind([string]$prompt) {
    $value = $prompt.Trim()
    if ($value -match '^(继续|快点|做|你做|你改|开始改|推进|立刻|补吧|写)$') {
        return '继续执行或催办'
    }
    if ($value -match '^(•|- )?已(完成|修复|处理|补|开始)|^(验收结论|结论：|不完全对|暂不能|新核对后)') {
        return '外部交付或审查结论复核'
    }
    if ($value -match '开始|实装|落地|推进|整改|修复|升级|删除|剔除|接入|写入|编写') {
        return '实现或修改要求'
    }
    if ($value -match '方案|设计|如何|怎么|咋办|是否|能否|为啥|为什么|评估|分析|觉得|推荐|确认|确保|检查|复核|验证|自测') {
        return '设计、解释或验证要求'
    }
    return '业务要求或补充说明'
}

function Get-TaskTitle([string]$prompt, [string]$kind) {
    $value = Normalize-Excerpt $prompt 46
    $value = $value.Trim('"', "'", '•', '-', ' ', '›')
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $kind
    }
    return $value
}

function New-Turn([string]$id, [string]$timestamp) {
    return [pscustomobject]@{
        Id = $id
        StartedAt = $timestamp
        Users = [Collections.Generic.List[object]]::new()
        ToolCalls = 0
        ToolOutputs = 0
        PatchEvents = 0
        Compactions = 0
        Final = ''
        CompletedAt = ''
        Status = 'incomplete'
    }
}

$sessionFile = Get-Item -LiteralPath $SessionPath
$lines = Get-Content -LiteralPath $SessionPath -Encoding UTF8
$parseErrors = 0
$unknownEvents = 0
$rows = [Collections.Generic.List[object]]::new()
foreach ($line in $lines) {
    try {
        $rows.Add(($line | ConvertFrom-Json))
    }
    catch {
        $parseErrors++
    }
}

$meta = $rows | Where-Object { $_.type -eq 'session_meta' } | Select-Object -First 1
$turns = [Collections.Generic.List[object]]::new()
$activeTurn = $null
$userOrdinal = 0
$compactionCount = 0

foreach ($row in $rows) {
    if ($row.type -eq 'event_msg') {
        switch ($row.payload.type) {
            'task_started' {
                $activeTurn = New-Turn $row.payload.turn_id $row.timestamp
                $turns.Add($activeTurn)
            }
            'user_message' {
                if ($null -eq $activeTurn) {
                    $activeTurn = New-Turn ('unbound-' + $userOrdinal) $row.timestamp
                    $turns.Add($activeTurn)
                }
                $userOrdinal++
                $activeTurn.Users.Add([pscustomobject]@{
                    Ordinal = $userOrdinal
                    Timestamp = $row.timestamp
                    Message = [string]$row.payload.message
                })
            }
            'patch_apply_end' {
                if ($null -ne $activeTurn) { $activeTurn.PatchEvents++ }
            }
            'context_compacted' {
                $compactionCount++
                if ($null -ne $activeTurn) { $activeTurn.Compactions++ }
            }
            'task_complete' {
                if ($null -ne $activeTurn) {
                    $activeTurn.Final = [string]$row.payload.last_agent_message
                    $activeTurn.CompletedAt = $row.timestamp
                    $activeTurn.Status = 'completed'
                    $activeTurn = $null
                }
            }
            'turn_aborted' {
                if ($null -ne $activeTurn) {
                    $activeTurn.CompletedAt = $row.timestamp
                    $activeTurn.Status = 'aborted'
                    $activeTurn = $null
                }
            }
            default {
                if ($row.payload.type -notin @('token_count', 'thread_settings_applied', 'agent_message')) {
                    $unknownEvents++
                }
            }
        }
    }
    elseif ($row.type -eq 'response_item' -and $null -ne $activeTurn) {
        if ($row.payload.type -in @('custom_tool_call', 'function_call')) {
            $activeTurn.ToolCalls++
        }
        elseif ($row.payload.type -in @('custom_tool_call_output', 'function_call_output')) {
            $activeTurn.ToolOutputs++
        }
    }
}

$completedTurns = @($turns | Where-Object { $_.Status -eq 'completed' }).Count
$abortedTurns = @($turns | Where-Object { $_.Status -eq 'aborted' }).Count
$incompleteTurns = @($turns | Where-Object { $_.Status -eq 'incomplete' }).Count
$toolCalls = ($turns | Measure-Object -Property ToolCalls -Sum).Sum
$toolOutputs = ($turns | Measure-Object -Property ToolOutputs -Sum).Sum
$patchEvents = ($turns | Measure-Object -Property PatchEvents -Sum).Sum

$oldText = if (Test-Path -LiteralPath $HistoryPath) { Get-Content -LiteralPath $HistoryPath -Encoding UTF8 -Raw } else { '' }
$preservedHeader = '## 原阶段总结（保留，不替代时间线）'
$preservedStart = $oldText.IndexOf($preservedHeader)
if ($preservedStart -ge 0) {
    $oldSummary = $oldText.Substring($preservedStart + $preservedHeader.Length).Trim()
}
else {
    $summaryStart = $oldText.IndexOf('## 一、')
    $oldSummary = if ($summaryStart -ge 0) { $oldText.Substring($summaryStart).Trim() } else { $oldText.Trim() }
}

# 历史摘要中的旧 T 标题只能作为导航，不能再次被覆盖校验器识别为正式时间线节点。
$oldSummary = $oldSummary -replace '(?m)^### T(\d{3})', '#### 旧节点 T$1'

$builder = [Text.StringBuilder]::new()
$null = $builder.AppendLine('# ' + $Title)
$null = $builder.AppendLine()
$null = $builder.AppendLine('文件名大纲：' + $FileOutline)
$null = $builder.AppendLine()
$null = $builder.AppendLine('窗口档案ID：`' + $ArchiveId + '`')
$null = $builder.AppendLine()
$null = $builder.AppendLine('Codex Session ID：`' + $meta.payload.id + '`')
$null = $builder.AppendLine()
$null = $builder.AppendLine('会话开始：' + (Convert-ToLocalTime $meta.timestamp))
$null = $builder.AppendLine()
$null = $builder.AppendLine('工作目录：`' + $meta.payload.cwd + '`')
$null = $builder.AppendLine()
$null = $builder.AppendLine('恢复时间：' + [DateTimeOffset]::Now.ToString('yyyy-MM-dd HH:mm:ss zzz'))
$null = $builder.AppendLine()
$null = $builder.AppendLine('## 恢复来源与归属依据')
$null = $builder.AppendLine()
$null = $builder.AppendLine('- 权威来源：`' + $SessionPath + '`。')
$null = $builder.AppendLine('- 快照：' + $sessionFile.Length.ToString('N0') + ' 字节、' + $lines.Count + ' 行，最后修改于 ' + $sessionFile.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff zzz') + '。')
$null = $builder.AppendLine('- 结构统计：' + $userOrdinal + ' 条用户消息、' + $turns.Count + ' 次任务开始、' + $completedTurns + ' 次完成、' + $abortedTurns + ' 次中止、' + $incompleteTurns + ' 次未闭合、' + $compactionCount + ' 次上下文压缩、' + $toolCalls + ' 次工具调用、' + $toolOutputs + ' 次工具输出、' + $patchEvents + ' 次补丁结束事件。')
$null = $builder.AppendLine('- 解析结果：' + $parseErrors + ' 条 JSON 错误，' + $unknownEvents + ' 条未知业务事件。内部 JSONL 仅按本机观察结构解析。')
$null = $builder.AppendLine('- 归属依据：调用方已在运行恢复器前核对 session ID、时间、CWD、首尾提示与档案尾部连续性；恢复器只重建显式传入的档案路径，不自行授予或推断写入归属。')
$null = $builder.AppendLine('- 脱敏边界：不写入系统/开发者提示、world state、reasoning 原文及完整工具输出；凭据字段自动替换为 `<REDACTED>`。')
$null = $builder.AppendLine()
$null = $builder.AppendLine('## 完整任务时间线')
$null = $builder.AppendLine()

$stageOrdinal = 0
foreach ($turn in $turns) {
    $stageOrdinal++
    $stageStatus = $turn.Status
    $null = $builder.AppendLine('### Stage S' + $stageOrdinal.ToString('000') + ': 执行轮 `' + $turn.Id + '` (' + $stageStatus + ')')
    $null = $builder.AppendLine()
    $null = $builder.AppendLine('- **阶段边界**：本阶段只表示同一执行轮的容器，不得替代其下独立任务节点。')
    $null = $builder.AppendLine('- **阶段内用户消息数**：' + $turn.Users.Count + '。每条消息必须保留为独立 T 节点。')
    $null = $builder.AppendLine()
    $messageCount = $turn.Users.Count
    for ($i = 0; $i -lt $messageCount; $i++) {
        $user = $turn.Users[$i]
        $kind = Get-TaskKind $user.Message
        $title = Get-TaskTitle $user.Message $kind
        $number = 'T' + $user.Ordinal.ToString('000')
        $null = $builder.AppendLine('### ' + $number + '（' + (Convert-ToLocalTime $user.Timestamp) + '）：' + $title)
        $null = $builder.AppendLine()
        $null = $builder.AppendLine('- **用户要求（原文节选）**：' + (Normalize-Excerpt $user.Message 520))
        $null = $builder.AppendLine('- **任务性质**：' + $kind + '。')
        $shared = if ($messageCount -gt 1) { '；本 turn 共 ' + $messageCount + ' 条用户消息，本节点为第 ' + ($i + 1) + ' 条' } else { '' }
        $null = $builder.AppendLine('- **执行轮**：`' + $turn.Id + '`，状态为 `' + $turn.Status + '`' + $shared + '。')
        $null = $builder.AppendLine('- **过程证据**：该轮记录 ' + $turn.ToolCalls + ' 次工具调用、' + $turn.ToolOutputs + ' 次工具输出、' + $turn.PatchEvents + ' 次补丁结束事件、' + $turn.Compactions + ' 次上下文压缩。')
        if ($turn.Status -eq 'completed') {
            $null = $builder.AppendLine('- **当时答复摘要**：' + (Normalize-Excerpt $turn.Final 760))
            $null = $builder.AppendLine('- **结论边界**：记录为当时已答复；是否构成源码实现、编译通过或 Unity 运行验收，只能按上述答复中的证据判断，不因 `task_complete` 自动升级。')
        }
        elseif ($turn.Status -eq 'aborted') {
            $null = $builder.AppendLine('- **当时结果**：该轮被 `turn_aborted` 中止，没有完成答复；不得推断为已实现或已验证。')
        }
        else {
            $null = $builder.AppendLine('- **当时结果**：未找到完成或中止事件，状态保持未闭合。')
        }
        $null = $builder.AppendLine('- **剩余项**：恢复器不替代当时的技术判断；请依据该节点答复、工具证据和当前源码重新核对未完成项。')
        $null = $builder.AppendLine()
    }
}

$null = $builder.AppendLine('## 覆盖审计')
$null = $builder.AppendLine()
$null = $builder.AppendLine('- 可见用户消息：' + $userOrdinal + '。')
$null = $builder.AppendLine('- 独立或补充/纠正节点：' + $userOrdinal + '；排除用户消息：0。')
$null = $builder.AppendLine('- 阶段数：' + $stageOrdinal + '；实际时间线节点：' + $userOrdinal + '，编号 T001-T' + $userOrdinal.ToString('000') + ' 连续。')
$null = $builder.AppendLine('- task start 数与节点数差异：' + $turns.Count + ' 个 turn 承载 ' + $userOrdinal + ' 条用户消息，多出的 ' + ($userOrdinal - $turns.Count) + ' 条是同一执行轮中的补充或纠正，仍独立成节点。')
$null = $builder.AppendLine('- 完成/中止/未闭合：' + $completedTurns + '/' + $abortedTurns + '/' + $incompleteTurns + '，合计等于 task start 数。')
$null = $builder.AppendLine()
$null = $builder.AppendLine('## 原阶段总结（保留，不替代时间线）')
$null = $builder.AppendLine()
$null = $builder.AppendLine('以下内容来自该窗口此前建立的旧格式档案。它用于主题检索和当时状态汇总；与逐轮证据冲突时，以时间线、源码和最新验收为准。')
$null = $builder.AppendLine()
$null = $builder.AppendLine($oldSummary)

[IO.File]::WriteAllText($HistoryPath, $builder.ToString(), [Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    HistoryPath = $HistoryPath
    UserMessages = $userOrdinal
    Turns = $turns.Count
    Completed = $completedTurns
    Aborted = $abortedTurns
    Incomplete = $incompleteTurns
    Compactions = $compactionCount
    ToolCalls = $toolCalls
    PatchEvents = $patchEvents
    ParseErrors = $parseErrors
    OutputBytes = (Get-Item -LiteralPath $HistoryPath).Length
} | ConvertTo-Json
