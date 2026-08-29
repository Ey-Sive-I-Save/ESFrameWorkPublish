Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'

$script:ContractPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Contracts\es-commercial-evaluation-v1.schema.json'))
$script:RegistryPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Contracts\es-commercial-metric.registry.json'))
$script:RegistrySchemaPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Contracts\es-commercial-metric-registry-v1.schema.json'))
$script:SchemaModulePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Contracts\ESJsonSchemaLite.psm1'))
$script:TaskContextModulePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\TaskContextRuntime\ESTaskContextRuntime.psm1'))
$script:RoutePlanModulePath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\RoutePlan\ESRoutePlanContract.psm1'))
Import-Module $script:SchemaModulePath -ErrorAction Stop
Import-Module $script:TaskContextModulePath -ErrorAction Stop
Import-Module $script:RoutePlanModulePath -ErrorAction Stop

function Read-ESCommercialStrictJson([string]$Path){
    $raw=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path))
    return $raw|ConvertFrom-Json -ErrorAction Stop
}

function Get-ESCommercialHash($Value){return Get-ESRoutePlanCanonicalHash $Value}

function Get-ESCommercialMetricRegistrySnapshot {
    [CmdletBinding()]param()
    $registry=Read-ESCommercialStrictJson $script:RegistryPath
    $schemaErrors=@(Test-ESJsonSchemaValue -SchemaPath $script:RegistrySchemaPath -Value $registry)
    if($schemaErrors.Count){throw ('Commercial metric registry schema validation failed: '+($schemaErrors-join'; '))}
    $expected=@('successRate','stableSuccessRate','hardViolationRate','claimOverstatementRate','humanCorrectionRate','meanLatency','meanCost','recoveryRate','regressionPassRate')
    $actual=@($registry.metrics|ForEach-Object{[string]$_.metricId})
    if(@($actual|Sort-Object -Unique).Count-ne$actual.Count){throw 'Commercial metric registry contains duplicate metricId values.'}
    if(($actual -join '|')-cne($expected -join '|')){throw 'Commercial metric registry order or required metric closure drifted.'}
    foreach($definition in @($registry.metrics)){
        if([string]$definition.scope-cne'task-cohort'-or[string]$definition.missingEvidencePolicy-cne'evidence-pending-null'){throw 'Commercial metric registry scope or missing-evidence policy expanded.'}
        if([string]$definition.implementationState-ceq'evidence-pending'-and[string]$definition.sourceAuthority-cne'registered-external-verifier'){throw 'Evidence-pending commercial metric lacks a registered external verifier boundary.'}
    }
    [pscustomobject][ordered]@{registry=$registry;registryHash=(Get-FileHash -LiteralPath $script:RegistryPath -Algorithm SHA256).Hash.ToLowerInvariant()}
}

function New-ESCommercialMetricResult {
    param([string]$MetricId,[string]$State,$Value,$Numerator,$Denominator,[int]$CoverageCount,[string]$Unit,[string]$SourceAuthority,[string]$Reason)
    [pscustomobject][ordered]@{metricId=$MetricId;state=$State;value=$Value;numerator=$Numerator;denominator=$Denominator;coverageCount=$CoverageCount;unit=$Unit;sourceAuthority=$SourceAuthority;scope='task-cohort';reason=$Reason}
}

function Get-ESCommercialRateValue([int]$Numerator,[int]$Denominator){
    if($Denominator-le0){return $null}
    return [Math]::Round(([double]$Numerator/[double]$Denominator),6,[MidpointRounding]::AwayFromZero)
}

function New-ESHumanCorrectionMetric($Observations){
    $eligible=@($Observations|Where-Object{[bool]$_.correctionObservationClosed})
    if($eligible.Count-eq0){return New-ESCommercialMetricResult 'humanCorrectionRate' 'evidence-pending' $null $null $null 0 'ratio' 'TaskContextRuntime' 'No platform-verified task-bound correction observation is available.'}
    $corrected=@($eligible|Where-Object{[bool]$_.humanCorrectionObserved})
    return New-ESCommercialMetricResult 'humanCorrectionRate' 'closed' (Get-ESCommercialRateValue $corrected.Count $eligible.Count) $corrected.Count $eligible.Count $eligible.Count 'ratio' 'TaskContextRuntime' 'Tasks with platform-reverified task-bound Codex transcript correction observations.'
}

function New-ESCommercialEvaluationReport {
    [CmdletBinding()]
    param(
        [string]$ProjectRoot='.',
        [string]$StoreRoot='ES/Output/TaskContextRuntime',
        [Parameter(Mandatory=$true)][string[]]$TaskId,
        [ValidateRange(2,100)][int]$MinimumStableRuns=2
    )
    if($TaskId.Count-eq0){throw 'Commercial evaluation requires at least one TaskId.'}
    $normalized=@($TaskId|ForEach-Object{if([string]::IsNullOrWhiteSpace($_)){throw 'Commercial evaluation TaskId cannot be empty.'};[string]$_}|Sort-Object -CaseSensitive)
    if(@($normalized|Sort-Object -Unique).Count-ne$normalized.Count){throw 'Commercial evaluation rejects duplicate TaskId values.'}
    $registrySnapshot=Get-ESCommercialMetricRegistrySnapshot
    $observations=@($normalized|ForEach-Object{Get-ESTaskCommercialObservation -ProjectRoot $ProjectRoot -StoreRoot $StoreRoot -TaskId $_})
    $evaluated=@($observations|Where-Object{$null-ne$_.latestDecision})
    $accepted=@($evaluated|Where-Object{[string]$_.latestDecision-ceq'accepted'})
    $stableGroups=@($observations|Group-Object goalRevisionHash|Where-Object{$_.Count-ge$MinimumStableRuns-and@($_.Group|Where-Object{$null-eq$_.latestDecision}).Count-eq0})
    $stableAccepted=@($stableGroups|Where-Object{@($_.Group|Where-Object{[string]$_.latestDecision-cne'accepted'}).Count-eq0})
    $hardViolations=@($evaluated|Where-Object{$_.hardViolationObserved})
    $latencies=@($observations|Where-Object{$null-ne$_.evaluationLatencyMs}|ForEach-Object{[long]$_.evaluationLatencyMs})
    $recoveryEligible=@($observations|Where-Object{$_.recoveryEligible})
    $recovered=@($recoveryEligible|Where-Object{$_.recoveryObserved})
    $regressionObserved=@($observations|Where-Object{$_.regressionObserved})
    $regressionPassed=@($regressionObserved|Where-Object{$_.regressionPassed})
    $metricsById=@{}
    $metricsById.successRate=if($evaluated.Count){New-ESCommercialMetricResult 'successRate' 'closed' (Get-ESCommercialRateValue $accepted.Count $evaluated.Count) $accepted.Count $evaluated.Count $evaluated.Count 'ratio' 'TaskContextRuntime' 'Latest verified task evaluations.'}else{New-ESCommercialMetricResult 'successRate' 'evidence-pending' $null $null $null 0 'ratio' 'TaskContextRuntime' 'No verified task EvaluationRecord is available.'}
    $metricsById.stableSuccessRate=if($stableGroups.Count){New-ESCommercialMetricResult 'stableSuccessRate' 'closed' (Get-ESCommercialRateValue $stableAccepted.Count $stableGroups.Count) $stableAccepted.Count $stableGroups.Count $stableGroups.Count 'ratio' 'TaskContextRuntime' "GoalRevision cohorts with at least $MinimumStableRuns verified task evaluations."}else{New-ESCommercialMetricResult 'stableSuccessRate' 'evidence-pending' $null $null $null 0 'ratio' 'TaskContextRuntime' "No GoalRevision cohort has at least $MinimumStableRuns verified task evaluations."}
    $metricsById.hardViolationRate=if($evaluated.Count){New-ESCommercialMetricResult 'hardViolationRate' 'closed' (Get-ESCommercialRateValue $hardViolations.Count $evaluated.Count) $hardViolations.Count $evaluated.Count $evaluated.Count 'ratio' 'TaskContextRuntime' 'Task-scoped completion-blocking FailureRecords; never project-global P0.'}else{New-ESCommercialMetricResult 'hardViolationRate' 'evidence-pending' $null $null $null 0 'ratio' 'TaskContextRuntime' 'No verified task EvaluationRecord is available.'}
    $metricsById.claimOverstatementRate=New-ESCommercialMetricResult 'claimOverstatementRate' 'evidence-pending' $null $null $null 0 'ratio' 'registered-external-verifier' 'No registered claim-audit observation source is integrated.'
    $metricsById.humanCorrectionRate=New-ESHumanCorrectionMetric $observations
    $metricsById.meanLatency=if($latencies.Count){$sum=[double](($latencies|Measure-Object -Sum).Sum);New-ESCommercialMetricResult 'meanLatency' 'closed' ([Math]::Round($sum/$latencies.Count,3,[MidpointRounding]::AwayFromZero)) $null $latencies.Count $latencies.Count 'milliseconds' 'TaskContextRuntime' 'First TaskContext event to latest verified EvaluationRecord.'}else{New-ESCommercialMetricResult 'meanLatency' 'evidence-pending' $null $null $null 0 'milliseconds' 'TaskContextRuntime' 'No verified EvaluationRecord latency observation is available.'}
    $metricsById.meanCost=New-ESCommercialMetricResult 'meanCost' 'evidence-pending' $null $null $null 0 'cost-units' 'registered-external-verifier' 'No registered verified run-cost observation source is integrated.'
    $metricsById.recoveryRate=if($recoveryEligible.Count){New-ESCommercialMetricResult 'recoveryRate' 'closed' (Get-ESCommercialRateValue $recovered.Count $recoveryEligible.Count) $recovered.Count $recoveryEligible.Count $recoveryEligible.Count 'ratio' 'TaskContextRuntime' 'Tasks with a prior non-accepted evaluation followed by the latest verified evaluation.'}else{New-ESCommercialMetricResult 'recoveryRate' 'evidence-pending' $null $null $null 0 'ratio' 'TaskContextRuntime' 'No task has a prior non-accepted evaluation in the selected cohort.'}
    $metricsById.regressionPassRate=if($regressionObserved.Count){New-ESCommercialMetricResult 'regressionPassRate' 'closed' (Get-ESCommercialRateValue $regressionPassed.Count $regressionObserved.Count) $regressionPassed.Count $regressionObserved.Count $regressionObserved.Count 'ratio' 'TaskContextRuntime' 'Task-scoped OutcomeAssertions derived by the registered platform.static-replay-v1 verifier.'}else{New-ESCommercialMetricResult 'regressionPassRate' 'evidence-pending' $null $null $null 0 'ratio' 'TaskContextRuntime' 'No closed task-scoped StaticReplay OutcomeAssertion is available.'}
    $metrics=@($registrySnapshot.registry.metrics|ForEach-Object{$metricsById[[string]$_.metricId]})
    foreach($metric in $metrics){if([string]$metric.state-ceq'evidence-pending'-and$null-ne$metric.value){throw 'Evidence-pending commercial metric cannot contain a numeric value.'}}
    $sourceSnapshotHash=Get-ESCommercialHash ([ordered]@{taskIds=$normalized;observationHashes=@($observations|ForEach-Object{[string]$_.observationHash});registryHash=[string]$registrySnapshot.registryHash;minimumStableRuns=$MinimumStableRuns})
    $contractHash=(Get-FileHash -LiteralPath $script:ContractPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $generatedUtc=[DateTime]::UtcNow.ToString('o')
    $reportId='commercial-'+(Get-ESCommercialHash ([ordered]@{sourceSnapshotHash=$sourceSnapshotHash;contractHash=$contractHash;registryHash=[string]$registrySnapshot.registryHash})).Substring(0,32)
    $base=[ordered]@{
        schemaVersion=1;contractId='es://automation/contracts/commercial-evaluation/v1';contractHash=$contractHash;recordType='CommercialEvaluationReport';reportId=$reportId
        registryId='es.ai-collaboration.commercial-metrics.v1';registryHash=[string]$registrySnapshot.registryHash;scope='task-cohort';generatedUtc=$generatedUtc;minimumStableRuns=$MinimumStableRuns
        taskCount=$observations.Count;evaluatedTaskCount=$evaluated.Count;sourceSnapshotHash=$sourceSnapshotHash;taskObservations=$observations;metrics=$metrics
        overallStatus=if(@($metrics|Where-Object{[string]$_.state-cne'closed'}).Count){'partial'}else{'closed'}
        nonClaims=@('Task-cohort metrics are not project-global P0','Missing telemetry is not zero','Static aggregation does not prove Runtime or Release','Domain scores are not averaged into one ES quality score')
    }
    $report=[ordered]@{};foreach($key in $base.Keys){$report[$key]=$base[$key]};$report.reportHash=Get-ESCommercialHash $base
    $schemaErrors=@(Test-ESJsonSchemaValue -SchemaPath $script:ContractPath -Value ([pscustomobject]$report))
    if($schemaErrors.Count){throw ('Commercial evaluation report schema validation failed: '+($schemaErrors-join'; '))}
    return [pscustomobject]$report
}

Export-ModuleMember -Function Get-ESCommercialMetricRegistrySnapshot,New-ESCommercialEvaluationReport
