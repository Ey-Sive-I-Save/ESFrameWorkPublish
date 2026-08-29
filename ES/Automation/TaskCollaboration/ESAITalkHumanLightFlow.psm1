Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ESAITalkProjectAggregation.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'ESAITalkAggregation.psm1') -Force

function Get-ESAITalkHumanPlanHash($Value) {
    $canonical = ConvertTo-ESAITalkCanonical $Value
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical))).Replace('-', '').ToLowerInvariant()) } finally { $sha.Dispose() }
}

function Invoke-ESAITalkHumanLightFlow {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$SessionsRoot,
        [ValidateRange(1,256)][int]$MaxSessions = 256,
        [ValidateRange(1,8192)][int]$MaxMessagesPerSession = 512,
        [ValidateRange(1024,4194304)][int]$MaxMessageBytes = 262144,
        [object[]]$RoundObservations
    )
    $project = Invoke-ESAITalkProjectAggregation -SessionsRoot $SessionsRoot -MaxSessions $MaxSessions -MaxMessagesPerSession $MaxMessagesPerSession -MaxMessageBytes $MaxMessageBytes
    $roundGate = $null
    if ($null -ne $RoundObservations) { $roundGate = Invoke-ESAITalkRoundGate -RoundObservations $RoundObservations -MaxRounds 5 }
    $actions = [Collections.Generic.List[object]]::new()
    $autoActions = [Collections.Generic.List[string]]::new()
    [void]$autoActions.Add('自动扫描工程 Sessions 并读取全部消息')
    [void]$autoActions.Add('自动按消息身份和幂等键去重并保留聚合哈希')
    [void]$autoActions.Add('自动生成候选结果，不改写 TaskContext 完成状态')
    if ([string]$project.status -eq 'conflict') { [void]$actions.Add([pscustomobject][ordered]@{code='PROJECT_CONFLICT';severity='decision';summary='工程聚合存在冲突；请只选择冲突处理策略，不必逐条搬运消息。'}) }
    elseif ([string]$project.status -eq 'needs-review') { [void]$actions.Add([pscustomobject][ordered]@{code='PROJECT_REVIEW';severity='review';summary='工程中存在格式、权限、过时绑定或资源超限项；请按隔离原因处理。'}) }
    elseif ([string]$project.status -eq 'partial') { [void]$actions.Add([pscustomobject][ordered]@{code='PROJECT_PARTIAL_INFO';severity='info';summary='工程聚合有部分隔离项；候选结果仍可自动查看，人工介入不是必需。'}) }
    if ($null -ne $roundGate) {
        if ([string]$roundGate.status -eq 'needs-user-decision') { [void]$actions.Add([pscustomobject][ordered]@{code='ROUND_USER_DECISION';severity='decision';summary='讨论已到达需要用户拍板的节点；请只回答该节点的决策问题。'}) }
        elseif ([string]$roundGate.status -eq 'interrupted') { [void]$actions.Add([pscustomobject][ordered]@{code='ROUND_LIMIT_INTERRUPTED';severity='decision';summary='五轮内未达成共识，已中断；请决定继续讨论还是采用候选方案。'}) }
    }
    $requires = @($actions | Where-Object { [string]$_.severity -eq 'decision' -or [string]$_.severity -eq 'review' }).Count -gt 0
    $status = if ($roundGate -and [string]$roundGate.status -eq 'interrupted') { 'interrupted' } elseif ($requires) { 'needs-human-decision' } else { 'auto-ready' }
    $base = [ordered]@{ schemaVersion=1; contractId='es://automation/contracts/task-collaboration/aitalk-human-action-plan/v1'; recordType='AITalkHumanActionPlan'; status=$status; humanActionRequired=$requires; projectAggregationHash=[string]$project.aggregationHash; roundGateStatus=if($roundGate){[string]$roundGate.status}else{$null}; actionItems=@($actions); autoActions=@($autoActions); completionDecisionRequired=$true; nonClaims=@('Human-light flow reduces interaction steps but never makes business decisions.','AITalk, TaskContextRuntime, Unity Runtime, and release acceptance remain separate authorities.'); planHash=$null }
    $base.planHash = Get-ESAITalkHumanPlanHash $base
    [pscustomobject]$base
}

Export-ModuleMember -Function Invoke-ESAITalkHumanLightFlow
