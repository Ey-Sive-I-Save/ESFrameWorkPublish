[CmdletBinding()]
param(
    [string]$ProjectionPath = '',
    [string]$AggregatePath = (Join-Path $PSScriptRoot 'fixtures/aggregate.valid.json')
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '..\TaskCollaboration\ESTaskCollaborationContracts.psm1') -Force
$projection = if ($ProjectionPath) {
    if (-not (Test-Path -LiteralPath $ProjectionPath -PathType Leaf)) { throw 'PROJECTION_REQUIRED' }
    Get-Content -Raw -Encoding UTF8 -LiteralPath $ProjectionPath | ConvertFrom-Json
} else {
    & (Join-Path $PSScriptRoot 'Invoke-ESWebUiSubAgentProjection.ps1') -AggregatePath $AggregatePath | ConvertFrom-Json
}
$plan = $projection.executionPlan
$children = @($plan.parallelStage.childTaskIds | ForEach-Object { [string]$_ })
$verification = @($projection.verificationResults)
$verificationHash = Get-ESCollaborationHash $verification
$candidateEnvelopes = @($projection.resultEnvelopes | Where-Object { [string]$_.resultStatus -eq 'candidate' })
$notRunLayers = @($verification | Where-Object { [string]$_.runtimeStatus -ne 'runtime-passed' } | ForEach-Object { 'web-ui.' + [string]$_.layer })
$candidateChildIds = @($candidateEnvelopes | ForEach-Object { [string]$_.childTaskId })
$receiptIdentityValid = $true
foreach ($v in $verification) {
    $path = [string]$v.receiptPath
    if ([string]$v.runtimeStatus -eq 'runtime-passed' -and [string]::IsNullOrWhiteSpace($path)) { $receiptIdentityValid = $false; continue }
    if (-not [string]::IsNullOrWhiteSpace($path)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { $receiptIdentityValid = $false; continue }
        $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -cne ([string]$v.receiptSha256).ToLowerInvariant()) { $receiptIdentityValid = $false; continue }
        try {
            $receipt = Get-Content -Raw -Encoding UTF8 -LiteralPath $path | ConvertFrom-Json
            if ([string]$receipt.receiptId -notmatch '.+' -or [string]$receipt.recordType -notmatch 'Receipt') { $receiptIdentityValid = $false }
            if ($receipt.PSObject.Properties['layer'] -and [string]$receipt.layer -cne [string]$v.layer) { $receiptIdentityValid = $false }
            if ($receipt.PSObject.Properties['receiptHash']) {
                $receiptInput = [ordered]@{}; foreach ($property in $receipt.PSObject.Properties) { if ($property.Name -ne 'receiptHash') { $receiptInput[$property.Name] = $property.Value } }
                if ([string]$receipt.receiptHash -notmatch '^[a-f0-9]{64}$' -or (Get-ESCollaborationHash $receiptInput) -cne [string]$receipt.receiptHash) { $receiptIdentityValid = $false }
            } else { $receiptIdentityValid = $false }
        } catch { $receiptIdentityValid = $false }
    }
}
$dependencyEdges = @($plan.dependencies | ForEach-Object { [string]$_.stage + '>' + [string]$_.dependsOn })
$expectedEdges = @('layer-evidence>static-preparation','layer-validation>layer-evidence','evidence-aggregation>layer-validation')
$stageNodes = @('static-preparation','layer-evidence','layer-validation','evidence-aggregation')
$indegree = @{}; $adjacency = @{}; foreach ($node in $stageNodes) { $indegree[$node] = 0; $adjacency[$node] = [System.Collections.Generic.List[string]]::new() }
$dependencyShapeValid = $true
foreach ($edge in @($plan.dependencies)) {
    $stage = [string]$edge.stage; $dependsOn = [string]$edge.dependsOn
    if ($stageNodes -notcontains $stage -or $stageNodes -notcontains $dependsOn -or $stage -eq $dependsOn) { $dependencyShapeValid = $false; continue }
    $adjacency[$dependsOn].Add($stage); $indegree[$stage]++
}
$queue = [System.Collections.Generic.Queue[string]]::new(); foreach ($node in $stageNodes) { if ($indegree[$node] -eq 0) { $queue.Enqueue($node) } }
$visited = 0; while ($queue.Count -gt 0) { $current = $queue.Dequeue(); $visited++; foreach ($next in $adjacency[$current]) { $indegree[$next]--; if ($indegree[$next] -eq 0) { $queue.Enqueue($next) } } }
$dependencyGraphValid = $dependencyShapeValid -and (@($dependencyEdges | Select-Object -Unique).Count -eq $dependencyEdges.Count) -and $dependencyEdges.Count -eq $expectedEdges.Count -and @($expectedEdges | Where-Object { $dependencyEdges -notcontains $_ }).Count -eq 0 -and $visited -eq $stageNodes.Count
$candidateMembershipValid = @($candidateChildIds | Where-Object { $children -notcontains $_ }).Count -eq 0
$envelopeIdentityValid = $true
if ([int]$projection.schemaVersion -ne 1 -or [string]$projection.recordType -cne 'WebPageStudioSubAgentProjection' -or [string]$projection.collaborationPlan.planHash -notmatch '^[a-f0-9]{64}$') { $envelopeIdentityValid = $false }
if ([string]$projection.childTaskRegistry.collaborationPlanHash -cne [string]$projection.collaborationPlan.planHash) { $envelopeIdentityValid = $false }
foreach ($envelope in @($projection.resultEnvelopes)) {
    if ([string]$envelope.parentTaskId -cne [string]$projection.parentTaskId -or [string]$envelope.collaborationPlanHash -cne [string]$projection.collaborationPlan.planHash -or [string]$envelope.resultHash -notmatch '^[a-f0-9]{64}$') { $envelopeIdentityValid = $false; continue }
    $envelopeInput = [ordered]@{}; foreach ($property in $envelope.PSObject.Properties) { if ($property.Name -ne 'resultHash') { $envelopeInput[$property.Name] = $property.Value } }
    if ((Get-ESCollaborationHash $envelopeInput) -cne [string]$envelope.resultHash) { $envelopeIdentityValid = $false }
}
foreach ($candidate in $candidateEnvelopes) {
    $layerName = ([string]$candidate.childTaskId).Replace('web-ui.','')
    $match = @($verification | Where-Object { [string]$_.layer -ceq $layerName })
    if ($match.Count -ne 1 -or [string]$match[0].runtimeStatus -cne 'runtime-passed' -or [string]$match[0].validatorStatus -notin @('passed','accepted','runtime-passed')) { $candidateMembershipValid = $false }
}
$checks = @(
    [pscustomobject]@{ check = 'unique-child-tasks'; passed = (@($children | Select-Object -Unique).Count -eq $children.Count) }
    [pscustomobject]@{ check = 'budget-admits-candidates'; passed = ([int]$plan.parallelStage.concurrencyBudget -ge 1 -and [int]$plan.parallelStage.concurrencyBudget -le [Math]::Max(1,$children.Count) -and $candidateEnvelopes.Count -le [int]$plan.parallelStage.concurrencyBudget -and @($candidateChildIds | Select-Object -Unique).Count -eq $candidateChildIds.Count -and $candidateMembershipValid) }
    [pscustomobject]@{ check = 'verification-hash-bound'; passed = ([string]$projection.verificationHash -ceq $verificationHash -and [string]$projection.aggregationInput.consumesVerificationHash -ceq $verificationHash) }
    [pscustomobject]@{ check = 'aggregation-after-validation'; passed = (@($plan.dependencies | Where-Object { $_.stage -eq 'evidence-aggregation' -and $_.dependsOn -eq 'layer-validation' }).Count -eq 1) }
    [pscustomobject]@{ check = 'not-run-not-admitted'; passed = (@($projection.resultEnvelopes | Where-Object { [string]$_.resultStatus -eq 'candidate' -and ($notRunLayers -contains [string]$_.childTaskId) }).Count -eq 0) }
    [pscustomobject]@{ check = 'receipt-identity-rechecked'; passed = $receiptIdentityValid }
    [pscustomobject]@{ check = 'dependency-graph-shape'; passed = $dependencyGraphValid }
    [pscustomobject]@{ check = 'envelope-identity-chain'; passed = $envelopeIdentityValid }
)
$failed = @($checks | Where-Object { -not $_.passed })
$report = [ordered]@{
    schemaVersion = 1
    validator = 'web-ui-sub-agent-admission'
    status = if ($failed.Count) { 'failed' } else { 'passed' }
    checks = $checks
    admittedCandidateCount = $candidateEnvelopes.Count
    concurrencyBudget = [int]$plan.parallelStage.concurrencyBudget
    verificationHash = $verificationHash
    runtimeStatus = 'runtime-not-run'
    nonClaims = @('static-admission-only','does-not-start-workers','does-not-prove-cross-process-speedup')
}
$report | ConvertTo-Json -Depth 12
if ($failed.Count) { exit 1 }
