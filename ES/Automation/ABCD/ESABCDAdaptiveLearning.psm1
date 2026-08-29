Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:HashPattern = '^[a-f0-9]{64}$'
$script:IdPattern = '^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$'
$script:HoldoutConsumptionRegistry = @{}

function ConvertTo-ESABCDAdaptiveCanonical($Value) {
    if ($null -eq $Value) { return 'null' }
    if ($Value -is [string] -or $Value -is [char]) { return ([string]$Value | ConvertTo-Json -Compress) }
    if ($Value -is [bool]) { return $(if ($Value) { 'true' } else { 'false' }) }
    if ($Value -is [Collections.IDictionary]) { return '{' + ((@($Value.Keys | ForEach-Object { [string]$_ } | Sort-Object) | ForEach-Object { ('{0}:{1}' -f ($_ | ConvertTo-Json -Compress), (ConvertTo-ESABCDAdaptiveCanonical $Value[$_])) }) -join ',') + '}' }
    if ($Value -is [pscustomobject]) { return '{' + ((@($Value.PSObject.Properties | Sort-Object Name) | ForEach-Object { ('{0}:{1}' -f ($_.Name | ConvertTo-Json -Compress), (ConvertTo-ESABCDAdaptiveCanonical $_.Value)) }) -join ',') + '}' }
    if ($Value -is [Collections.IEnumerable] -and $Value -isnot [string]) { return '[' + ((@($Value) | ForEach-Object { ConvertTo-ESABCDAdaptiveCanonical $_ }) -join ',') + ']' }
    return ([string]$Value | ConvertTo-Json -Compress)
}

function Get-ESABCDAdaptiveHash($Value) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes((ConvertTo-ESABCDAdaptiveCanonical $Value)))).Replace('-', '').ToLowerInvariant()) }
    finally { $sha.Dispose() }
}

function Assert-ESABCDAdaptiveId([string]$Value, [string]$Name) { if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch $script:IdPattern) { throw "$Name is invalid." } }
function Assert-ESABCDAdaptiveHash([string]$Value, [string]$Name) { if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch $script:HashPattern) { throw "$Name must be a lowercase SHA-256 hash." } }
function Get-ESABCDAdaptiveHashInput($Value, [string]$HashProperty) { $copy = [ordered]@{}; foreach ($property in $Value.PSObject.Properties) { if ($property.Name -ne $HashProperty) { $copy[$property.Name] = $property.Value } }; return $copy }

function ConvertTo-ESABCDSampleSet([string]$PartitionName, $Refs) {
    $items = @($Refs)
    if ($items.Count -lt 1 -or $items.Count -gt 100000) { throw "DATASET_$($PartitionName.ToUpperInvariant())_COUNT_INVALID" }
    $normalized = [Collections.Generic.List[object]]::new()
    foreach ($item in @($items | Sort-Object @{ Expression = { [string]$_.caseId } })) {
        Assert-ESABCDAdaptiveId ([string]$item.caseId) "$PartitionName.caseId"
        Assert-ESABCDAdaptiveId ([string]$item.sourceGroupId) "$PartitionName.sourceGroupId"
        Assert-ESABCDAdaptiveHash ([string]$item.snapshotHash) "$PartitionName.snapshotHash"
        [void]$normalized.Add([pscustomobject][ordered]@{ caseId = [string]$item.caseId; sourceGroupId = [string]$item.sourceGroupId; snapshotHash = ([string]$item.snapshotHash).ToLowerInvariant() })
    }
    return @($normalized)
}

function New-ESABCDDatasetPartitionManifest {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$DatasetId, [Parameter(Mandatory)]$TrainRefs, [Parameter(Mandatory)]$ValidationRefs, [Parameter(Mandatory)]$HoldoutRefs)
    Assert-ESABCDAdaptiveId $DatasetId 'DatasetId'
    $train = @(ConvertTo-ESABCDSampleSet 'train' $TrainRefs); $validation = @(ConvertTo-ESABCDSampleSet 'validation' $ValidationRefs); $holdout = @(ConvertTo-ESABCDSampleSet 'holdout' $HoldoutRefs)
    $all = @($train) + @($validation) + @($holdout)
    foreach ($property in @('caseId', 'snapshotHash', 'sourceGroupId')) {
        $values = @($all | ForEach-Object { [string]$_.$property })
        if ($values.Count -ne @($values | Sort-Object -Unique).Count) { throw "DATASET_PARTITION_LEAKAGE_$($property.ToUpperInvariant())" }
    }
    $partitionHashes = [ordered]@{ train = Get-ESABCDAdaptiveHash $train; validation = Get-ESABCDAdaptiveHash $validation; holdout = Get-ESABCDAdaptiveHash $holdout }
    $manifest = [ordered]@{
        schemaVersion = 1; contractId = 'es://automation/contracts/abcd/adaptive-learning/v1'; recordType = 'ABCDDatasetPartitionManifest'; datasetId = $DatasetId
        partitions = [ordered]@{ train = $train; validation = $validation; holdout = $holdout }; partitionHashes = $partitionHashes; isolationPolicy = 'case-snapshot-source-group-disjoint'; manifestHash = $null
    }
    $result = [pscustomobject]$manifest; $result.manifestHash = Get-ESABCDAdaptiveHash (Get-ESABCDAdaptiveHashInput $result 'manifestHash'); return $result
}

function Test-ESABCDDatasetPartitionManifest {
    [CmdletBinding()] param([Parameter(Mandatory)]$Manifest)
    $issues = [Collections.Generic.List[string]]::new()
    try { Assert-ESABCDAdaptiveHash ([string]$Manifest.manifestHash) 'ManifestHash'; if ((Get-ESABCDAdaptiveHash (Get-ESABCDAdaptiveHashInput $Manifest 'manifestHash')) -cne [string]$Manifest.manifestHash) { [void]$issues.Add('DATASET_MANIFEST_HASH_MISMATCH') } } catch { [void]$issues.Add($_.Exception.Message) }
    $all = @($Manifest.partitions.train) + @($Manifest.partitions.validation) + @($Manifest.partitions.holdout)
    foreach ($name in @('train', 'validation', 'holdout')) { if (@($Manifest.partitions.$name).Count -lt 1 -or (Get-ESABCDAdaptiveHash @($Manifest.partitions.$name)) -cne [string]$Manifest.partitionHashes.$name) { [void]$issues.Add("DATASET_PARTITION_HASH_MISMATCH:$name") } }
    foreach ($property in @('caseId', 'snapshotHash', 'sourceGroupId')) { $values = @($all | ForEach-Object { [string]$_.$property }); if ($values.Count -ne @($values | Sort-Object -Unique).Count) { [void]$issues.Add("DATASET_PARTITION_LEAKAGE:$property") } }
    [pscustomobject][ordered]@{ status = if ($issues.Count) { 'failed' } else { 'passed' }; issues = @($issues); isolationVerified = ($issues.Count -eq 0) }
}

function New-ESABCDAdaptiveLearningPlan {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$DatasetManifest, [Parameter(Mandatory)][string]$GeneratorId, [Parameter(Mandatory)][string]$FixedSeed, [Parameter(Mandatory)]$MetricDefinitions, [ValidateRange(1, 256)][int]$MaxRounds = 8, [ValidateRange(1, 1024)][int]$MaxCandidates = 32, [ValidateRange(1, 4096)][int]$MaxEvaluations = 128, [ValidateRange(1, 10000000)][int]$MaxMetricCalls = 10000, [ValidateRange(1, 10000000)][int]$MaxCases = 10000, [ValidateRange(2, 32)][int]$ConvergenceWindow = 3)
    Assert-ESABCDAdaptiveId $GeneratorId 'GeneratorId'; if ([string]::IsNullOrWhiteSpace($FixedSeed) -or $FixedSeed.Length -gt 256) { throw 'ADAPTIVE_FIXED_SEED_REQUIRED' }; $datasetCheck = Test-ESABCDDatasetPartitionManifest $DatasetManifest; if ($datasetCheck.status -ne 'passed') { throw 'ADAPTIVE_DATASET_INVALID' }
    $metricDefs = [Collections.Generic.List[object]]::new(); $seenMetrics = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($metric in @($MetricDefinitions | Sort-Object name)) { if ([string]::IsNullOrWhiteSpace([string]$metric.name) -or -not $seenMetrics.Add([string]$metric.name) -or [string]$metric.direction -notin @('maximize','minimize') -or $null -eq $metric.minDelta -or $null -eq $metric.noRegressionTolerance -or [double]$metric.minDelta -lt 0 -or [double]$metric.noRegressionTolerance -lt 0) { throw 'ADAPTIVE_METRIC_DEFINITION_INVALID' }; [void]$metricDefs.Add([pscustomobject][ordered]@{ name=[string]$metric.name; direction=[string]$metric.direction; minDelta=[double]$metric.minDelta; noRegressionTolerance=[double]$metric.noRegressionTolerance }) }
    if ($metricDefs.Count -lt 2) { throw 'ADAPTIVE_MULTI_OBJECTIVE_REQUIRES_TWO_METRICS' }
    $plan = [ordered]@{ schemaVersion = 1; contractId = 'es://automation/contracts/abcd/adaptive-learning/v1'; recordType = 'ABCDAdaptiveLearningPlan'; planId = $null; datasetRef = [ordered]@{ datasetId = [string]$DatasetManifest.datasetId; manifestHash = [string]$DatasetManifest.manifestHash; trainHash = [string]$DatasetManifest.partitionHashes.train; validationHash = [string]$DatasetManifest.partitionHashes.validation; holdoutHash = [string]$DatasetManifest.partitionHashes.holdout }; generatorId = $GeneratorId; fixedSeed = $FixedSeed; metricDefinitions = @($metricDefs); budgets = [ordered]@{ maxRounds = $MaxRounds; maxCandidates = $MaxCandidates; maxEvaluations = $MaxEvaluations; maxMetricCalls = $MaxMetricCalls; maxCases = $MaxCases; convergenceWindow = $ConvergenceWindow }; planHash = $null; promotionAllowed = $false }
    $seed = [ordered]@{ datasetRef = $plan.datasetRef; generatorId = $GeneratorId; fixedSeed = $FixedSeed; metricDefinitions = @($metricDefs); budgets = $plan.budgets }; $plan.planId = 'abcd-alp-' + (Get-ESABCDAdaptiveHash $seed).Substring(0, 32)
    $result = [pscustomobject]$plan; $result.planHash = Get-ESABCDAdaptiveHash (Get-ESABCDAdaptiveHashInput $result 'planHash'); return $result
}

function New-ESABCDPolicyCandidate {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Plan, [Parameter(Mandatory)][ValidateRange(1, 256)][int]$RoundNo, [Parameter(Mandatory)][ValidateRange(1, 1024)][int]$CandidateNo, [Parameter(Mandatory)][string]$BasePolicyId, [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$BasePolicyHash, $ParentCandidateRef, [Parameter(Mandatory)][ValidateSet('weight-adjustment', 'route-policy', 'threshold', 'selection-rule')][string]$MutationKind, [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$MutationHash)
    if ($RoundNo -gt [int]$Plan.budgets.maxRounds -or $CandidateNo -gt [int]$Plan.budgets.maxCandidates) { throw 'ADAPTIVE_CANDIDATE_BUDGET_EXHAUSTED' }
    Assert-ESABCDAdaptiveId $BasePolicyId 'BasePolicyId'
    if ($RoundNo -eq 1 -and $null -ne $ParentCandidateRef) { throw 'POLICY_LINEAGE_ROOT_PARENT_FORBIDDEN' }
    if ($RoundNo -gt 1) {
        if ($null -eq $ParentCandidateRef) { throw 'POLICY_LINEAGE_PARENT_REQUIRED' }
        Assert-ESABCDAdaptiveId ([string]$ParentCandidateRef.candidateId) 'ParentCandidateId'; Assert-ESABCDAdaptiveHash ([string]$ParentCandidateRef.candidateHash) 'ParentCandidateHash'
        if ([int]$ParentCandidateRef.roundNo -ge $RoundNo) { throw 'POLICY_LINEAGE_PARENT_ROUND_INVALID' }
    }
    $seed = [ordered]@{ planHash = [string]$Plan.planHash; fixedSeed = [string]$Plan.fixedSeed; roundNo = $RoundNo; candidateNo = $CandidateNo; basePolicyHash = $BasePolicyHash; parentCandidateRef = $ParentCandidateRef; mutationKind = $MutationKind; mutationHash = $MutationHash }
    $candidate = [ordered]@{
        schemaVersion = 1; contractId = 'es://automation/contracts/abcd/adaptive-learning/v1'; recordType = 'ABCDPolicyCandidate'; candidateId = 'abcd-pc-' + (Get-ESABCDAdaptiveHash $seed).Substring(0, 32); status = 'candidate'
        planRef = [ordered]@{ planId = [string]$Plan.planId; planHash = [string]$Plan.planHash }; roundNo = $RoundNo; candidateNo = $CandidateNo; basePolicyRef = [ordered]@{ policyId = $BasePolicyId; policyHash = $BasePolicyHash.ToLowerInvariant() }
        parentCandidateRef = if ($null -eq $ParentCandidateRef) { $null } else { [ordered]@{ candidateId = [string]$ParentCandidateRef.candidateId; candidateHash = [string]$ParentCandidateRef.candidateHash; roundNo = [int]$ParentCandidateRef.roundNo } }
        generatedBy = [string]$Plan.generatorId; fixedSeedHash = Get-ESABCDAdaptiveHash ([string]$Plan.fixedSeed); trainingDataRef = [ordered]@{ partition = 'train'; partitionHash = [string]$Plan.datasetRef.trainHash }; mutation = [ordered]@{ kind = $MutationKind; mutationHash = $MutationHash.ToLowerInvariant() }
        candidateHash = $null; promotion = [ordered]@{ promotionAllowed = $false; requiresExplicitApply = $true; decision = 'await-independent-evaluation' }; nonClaims = @('candidate-only', 'no-automatic-promotion', 'validation-and-holdout-not-training-inputs', 'runtime-not-proven')
    }
    $result = [pscustomobject]$candidate; $result.candidateHash = Get-ESABCDAdaptiveHash (Get-ESABCDAdaptiveHashInput $result 'candidateHash'); return $result
}

function Test-ESABCDPolicyCandidate {
    [CmdletBinding()] param([Parameter(Mandatory)]$Plan, [Parameter(Mandatory)]$Candidate, $KnownCandidates)
    $issues = [Collections.Generic.List[string]]::new()
    try { Assert-ESABCDAdaptiveHash ([string]$Candidate.candidateHash) 'CandidateHash'; if ((Get-ESABCDAdaptiveHash (Get-ESABCDAdaptiveHashInput $Candidate 'candidateHash')) -cne [string]$Candidate.candidateHash) { [void]$issues.Add('POLICY_CANDIDATE_HASH_MISMATCH') } } catch { [void]$issues.Add($_.Exception.Message) }
    if ([string]$Candidate.planRef.planId -cne [string]$Plan.planId -or [string]$Candidate.planRef.planHash -cne [string]$Plan.planHash) { [void]$issues.Add('POLICY_PLAN_MISMATCH') }
    if ([string]$Candidate.generatedBy -cne [string]$Plan.generatorId -or [string]$Candidate.fixedSeedHash -cne (Get-ESABCDAdaptiveHash ([string]$Plan.fixedSeed))) { [void]$issues.Add('POLICY_GENERATOR_OR_SEED_MISMATCH') }
    if ([string]$Candidate.trainingDataRef.partition -cne 'train' -or [string]$Candidate.trainingDataRef.partitionHash -cne [string]$Plan.datasetRef.trainHash) { [void]$issues.Add('POLICY_TRAINING_PARTITION_VIOLATION') }
    if ($Candidate.promotion.promotionAllowed -ne $false -or $Candidate.promotion.requiresExplicitApply -ne $true) { [void]$issues.Add('POLICY_AUTOMATIC_PROMOTION_FORBIDDEN') }
    if ([int]$Candidate.roundNo -gt 1) { $matches = @($KnownCandidates | Where-Object { [string]$_.candidateId -ceq [string]$Candidate.parentCandidateRef.candidateId -and [string]$_.candidateHash -ceq [string]$Candidate.parentCandidateRef.candidateHash -and [int]$_.roundNo -lt [int]$Candidate.roundNo }); if ($matches.Count -ne 1) { [void]$issues.Add('POLICY_LINEAGE_PARENT_UNRESOLVED') } }
    [pscustomobject][ordered]@{ status = if ($issues.Count) { 'failed' } else { 'passed' }; issues = @($issues); promotionAllowed = $false }
}

function ConvertTo-ESABCDMetricValueMap($Plan, $Metrics, [string]$Name) {
    $map = @{}; foreach ($metric in @($Metrics)) { if ([string]::IsNullOrWhiteSpace([string]$metric.name) -or $null -eq $metric.value -or $map.ContainsKey([string]$metric.name)) { throw "ADAPTIVE_METRIC_INVALID:$Name" }; $numeric = [double]$metric.value; if ([double]::IsNaN($numeric) -or [double]::IsInfinity($numeric)) { throw "ADAPTIVE_METRIC_NONFINITE:$Name" }; $map[[string]$metric.name] = $numeric }
    $definitionNames = @($Plan.metricDefinitions | ForEach-Object { [string]$_.name } | Sort-Object); if (($definitionNames -join '|') -cne (@($map.Keys | Sort-Object) -join '|')) { throw 'ADAPTIVE_METRIC_SET_MISMATCH' }; return $map
}

function Get-ESABCDMetricDeltas($Plan, $BaselineMetrics, $CandidateMetrics, [string]$Partition) {
    $baseline = ConvertTo-ESABCDMetricValueMap $Plan $BaselineMetrics "baseline-$Partition"; $current = ConvertTo-ESABCDMetricValueMap $Plan $CandidateMetrics "candidate-$Partition"
    $regressions = [Collections.Generic.List[string]]::new(); $improvements = [Collections.Generic.List[string]]::new(); $deltas = [Collections.Generic.List[object]]::new()
    foreach ($definition in @($Plan.metricDefinitions)) { $name = [string]$definition.name; $direction = [string]$definition.direction; $signedDelta = if ($direction -eq 'maximize') { [double]$current[$name] - [double]$baseline[$name] } else { [double]$baseline[$name] - [double]$current[$name] }; if ($signedDelta -lt -[double]$definition.noRegressionTolerance) { [void]$regressions.Add("${Partition}:$name") }; if ($signedDelta -ge [double]$definition.minDelta) { [void]$improvements.Add("${Partition}:$name") }; [void]$deltas.Add([pscustomobject][ordered]@{ partition=$Partition; name=$name; direction=$direction; baseline=[double]$baseline[$name]; candidate=[double]$current[$name]; signedDelta=$signedDelta; minDelta=[double]$definition.minDelta; tolerance=[double]$definition.noRegressionTolerance }) }
    [pscustomobject][ordered]@{ deltas=@($deltas); regressions=@($regressions); improvements=@($improvements); noRegressionPassed=($regressions.Count -eq 0); paretoEligible=($regressions.Count -eq 0 -and $improvements.Count -gt 0) }
}

function New-ESABCDPolicyEvaluation {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Plan, [Parameter(Mandatory)]$Candidate, [Parameter(Mandatory)]$BaselineValidationMetrics, [Parameter(Mandatory)]$CandidateValidationMetrics, [Parameter(Mandatory)][ValidateRange(1,10000000)][int]$ValidationCaseCount, [Parameter(Mandatory)][string]$EvaluatorId, [Parameter(Mandatory)][string]$AuditorId, [Parameter(Mandatory)][string]$AuthorizationProof, $KnownCandidates)
    foreach ($id in @($EvaluatorId, $AuditorId)) { Assert-ESABCDAdaptiveId $id 'Evaluator/Auditor identity' }
    if ($EvaluatorId -ceq $AuditorId -or $EvaluatorId -ceq [string]$Plan.generatorId -or $AuditorId -ceq [string]$Plan.generatorId) { throw 'ADAPTIVE_EVALUATOR_AUDITOR_INDEPENDENCE_REQUIRED' }
    if ([string]::IsNullOrWhiteSpace($AuthorizationProof)) { throw 'ADAPTIVE_EVALUATION_AUTHORIZATION_REQUIRED' }
    if ($ValidationCaseCount -gt [int]$Plan.budgets.maxCases) { throw 'ADAPTIVE_CASE_BUDGET_EXHAUSTED' }
    $metricCalls = @($Plan.metricDefinitions).Count * $ValidationCaseCount; if ($metricCalls -gt [int]$Plan.budgets.maxMetricCalls) { throw 'ADAPTIVE_METRIC_CALL_BUDGET_EXHAUSTED' }
    $known = if ($null -eq $KnownCandidates) { @() } else { @($KnownCandidates) }; $candidateCheck = Test-ESABCDPolicyCandidate -Plan $Plan -Candidate $Candidate -KnownCandidates $known; if ($candidateCheck.status -ne 'passed') { throw 'ADAPTIVE_POLICY_CANDIDATE_INVALID' }
    $comparison = Get-ESABCDMetricDeltas $Plan $BaselineValidationMetrics $CandidateValidationMetrics 'validation'
    $evaluation = [ordered]@{ schemaVersion=1; contractId='es://automation/contracts/abcd/adaptive-learning/v1'; recordType='ABCDPolicyEvaluation'; evaluationId=$null; candidateRef=[ordered]@{candidateId=[string]$Candidate.candidateId;candidateHash=[string]$Candidate.candidateHash;roundNo=[int]$Candidate.roundNo}; datasetRef=[ordered]@{partition='validation';partitionHash=[string]$Plan.datasetRef.validationHash}; evaluator=[ordered]@{evaluatorId=$EvaluatorId;authorizationProof=$AuthorizationProof}; auditor=[ordered]@{auditorId=$AuditorId;authorizationProof=$AuthorizationProof}; caseCount=$ValidationCaseCount;metricCallCount=$metricCalls; metricDeltas=$comparison.deltas;regressions=$comparison.regressions;improvements=$comparison.improvements;noRegressionPassed=$comparison.noRegressionPassed;paretoEligible=$comparison.paretoEligible;decision=if($comparison.paretoEligible){'pareto-candidate'}else{'rejected'};promotionAllowed=$false;evaluationHash=$null }
    $evaluation.evaluationId='abcd-ale-'+(Get-ESABCDAdaptiveHash ([ordered]@{candidateHash=[string]$Candidate.candidateHash;validationHash=[string]$Plan.datasetRef.validationHash;evaluatorId=$EvaluatorId;auditorId=$AuditorId})).Substring(0,32)
    $result=[pscustomobject]$evaluation;$result.evaluationHash=Get-ESABCDAdaptiveHash (Get-ESABCDAdaptiveHashInput $result 'evaluationHash');return $result
}

function New-ESABCDHoldoutGate {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Plan,[Parameter(Mandatory)]$SelectedCandidate,[Parameter(Mandatory)]$SelectionReceipt,[Parameter(Mandatory)]$BaselineHoldoutMetrics,[Parameter(Mandatory)]$CandidateHoldoutMetrics,[Parameter(Mandatory)][ValidateRange(1,10000000)][int]$HoldoutCaseCount,[Parameter(Mandatory)][string]$EvaluatorId,[Parameter(Mandatory)][string]$AuditorId,[Parameter(Mandatory)][string]$AuthorizationProof,[ValidateRange(1,1)][int]$GateInvocationNo=1)
    try { Assert-ESABCDAdaptiveHash ([string]$SelectionReceipt.receiptHash) 'SelectionReceiptHash'; if ((Get-ESABCDAdaptiveHash (Get-ESABCDAdaptiveHashInput $SelectionReceipt 'receiptHash')) -cne [string]$SelectionReceipt.receiptHash) { throw 'HOLDOUT_SELECTION_RECEIPT_HASH_MISMATCH' } } catch { throw $_.Exception.Message }
    try { Assert-ESABCDAdaptiveHash ([string]$SelectedCandidate.candidateHash) 'SelectedCandidateHash'; if ((Get-ESABCDAdaptiveHash (Get-ESABCDAdaptiveHashInput $SelectedCandidate 'candidateHash')) -cne [string]$SelectedCandidate.candidateHash) { throw 'HOLDOUT_CANDIDATE_HASH_MISMATCH' } } catch { throw $_.Exception.Message }
    if (@($SelectionReceipt.paretoFrontier | Where-Object { [string]$_.candidateId -ceq [string]$SelectedCandidate.candidateId -and [string]$_.candidateHash -ceq [string]$SelectedCandidate.candidateHash }).Count -ne 1) { throw 'HOLDOUT_SELECTED_CANDIDATE_NOT_ON_FRONTIER' }
    $selectionKey = [string]$SelectionReceipt.receiptHash
    if ($script:HoldoutConsumptionRegistry.ContainsKey($selectionKey)) { throw 'HOLDOUT_GATE_ALREADY_CONSUMED' }
    foreach($id in @($EvaluatorId,$AuditorId)){Assert-ESABCDAdaptiveId $id 'Holdout evaluator/auditor identity'};if($EvaluatorId-ceq$AuditorId-or$EvaluatorId-ceq[string]$Plan.generatorId-or$AuditorId-ceq[string]$Plan.generatorId){throw 'HOLDOUT_EVALUATOR_AUDITOR_INDEPENDENCE_REQUIRED'}
    if([string]::IsNullOrWhiteSpace($AuthorizationProof)){throw 'HOLDOUT_AUTHORIZATION_REQUIRED'};if($HoldoutCaseCount-gt[int]$Plan.budgets.maxCases){throw 'HOLDOUT_CASE_BUDGET_EXHAUSTED'};$metricCalls=@($Plan.metricDefinitions).Count*$HoldoutCaseCount;if($metricCalls-gt[int]$Plan.budgets.maxMetricCalls){throw 'HOLDOUT_METRIC_CALL_BUDGET_EXHAUSTED'}
    $comparison=Get-ESABCDMetricDeltas $Plan $BaselineHoldoutMetrics $CandidateHoldoutMetrics 'holdout'
    $gate=[ordered]@{schemaVersion=1;contractId='es://automation/contracts/abcd/adaptive-learning/v1';recordType='ABCDHoldoutGate';gateId=$null;gateInvocationNo=$GateInvocationNo;selectionRef=[ordered]@{runId=[string]$SelectionReceipt.runId;receiptHash=[string]$SelectionReceipt.receiptHash};candidateRef=[ordered]@{candidateId=[string]$SelectedCandidate.candidateId;candidateHash=[string]$SelectedCandidate.candidateHash};datasetRef=[ordered]@{partition='holdout';partitionHash=[string]$Plan.datasetRef.holdoutHash};evaluator=[ordered]@{evaluatorId=$EvaluatorId;authorizationProof=$AuthorizationProof};auditor=[ordered]@{auditorId=$AuditorId;authorizationProof=$AuthorizationProof};caseCount=$HoldoutCaseCount;metricCallCount=$metricCalls;metricDeltas=$comparison.deltas;noRegressionPassed=$comparison.noRegressionPassed;decision=if($comparison.noRegressionPassed){'await-explicit-review'}else{'rejected'};promotion=[ordered]@{promotionAllowed=$false;requiresExplicitApply=$true};gateHash=$null;nonClaims=@('holdout-final-gate-only','not-used-for-candidate-selection','no-automatic-promotion','not-task-completion')}
    $gate.gateId='abcd-alh-'+(Get-ESABCDAdaptiveHash ([ordered]@{selectionHash=[string]$SelectionReceipt.receiptHash;candidateHash=[string]$SelectedCandidate.candidateHash;holdoutHash=[string]$Plan.datasetRef.holdoutHash})).Substring(0,32);$result=[pscustomobject]$gate;$result.gateHash=Get-ESABCDAdaptiveHash (Get-ESABCDAdaptiveHashInput $result 'gateHash');$script:HoldoutConsumptionRegistry[$selectionKey] = [string]$result.gateHash;return $result
}

function Invoke-ESABCDAdaptiveLearningSelection {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Plan, [Parameter(Mandatory)]$Candidates, [Parameter(Mandatory)]$Evaluations, $AcceptedEvaluations)
    $candidateArray = @($Candidates); $evaluationArray = @($Evaluations)
    if ($candidateArray.Count -gt [int]$Plan.budgets.maxCandidates -or $evaluationArray.Count -gt [int]$Plan.budgets.maxEvaluations) { throw 'ADAPTIVE_LEARNING_BUDGET_EXHAUSTED' }
    if (@($candidateArray | Where-Object { [int]$_.roundNo -gt [int]$Plan.budgets.maxRounds }).Count -gt 0) { throw 'ADAPTIVE_ROUND_BUDGET_EXHAUSTED' }
    $caseCalls = [int](($evaluationArray | Measure-Object -Property caseCount -Sum).Sum); $metricCalls = [int](($evaluationArray | Measure-Object -Property metricCallCount -Sum).Sum); if ($caseCalls -gt [int]$Plan.budgets.maxCases -or $metricCalls -gt [int]$Plan.budgets.maxMetricCalls) { throw 'ADAPTIVE_AGGREGATE_EVALUATION_BUDGET_EXHAUSTED' }
    $candidateMap = @{}; $candidateHashes = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($candidate in $candidateArray) { Assert-ESABCDAdaptiveHash ([string]$candidate.candidateHash) 'CandidateHash'; if ((Get-ESABCDAdaptiveHash (Get-ESABCDAdaptiveHashInput $candidate 'candidateHash')) -cne [string]$candidate.candidateHash) { throw 'ADAPTIVE_CANDIDATE_HASH_MISMATCH' }; if ($candidateMap.ContainsKey([string]$candidate.candidateId)) { throw 'ADAPTIVE_DUPLICATE_CANDIDATE_ID' }; if (-not $candidateHashes.Add([string]$candidate.candidateHash)) { throw 'ADAPTIVE_DUPLICATE_CANDIDATE_HASH' }; $candidateMap[[string]$candidate.candidateId] = $candidate }
    foreach($evaluation in $evaluationArray){try{Assert-ESABCDAdaptiveHash ([string]$evaluation.evaluationHash) 'EvaluationHash';if((Get-ESABCDAdaptiveHash (Get-ESABCDAdaptiveHashInput $evaluation 'evaluationHash'))-cne[string]$evaluation.evaluationHash){throw 'hash'}}catch{throw 'ADAPTIVE_EVALUATION_HASH_MISMATCH'};if([string]$evaluation.datasetRef.partition-cne'validation'-or[string]$evaluation.datasetRef.partitionHash-cne[string]$Plan.datasetRef.validationHash){throw 'ADAPTIVE_HOLDOUT_LEAKAGE_IN_SELECTION'};if(-not $candidateMap.ContainsKey([string]$evaluation.candidateRef.candidateId)){throw 'ADAPTIVE_EVALUATION_CANDIDATE_NOT_FOUND'};$candidate=$candidateMap[[string]$evaluation.candidateRef.candidateId];if([string]$evaluation.candidateRef.candidateHash-cne[string]$candidate.candidateHash-or[int]$evaluation.candidateRef.roundNo-ne[int]$candidate.roundNo){throw 'ADAPTIVE_EVALUATION_CANDIDATE_MISMATCH'}}
    $eligible = @($evaluationArray | Where-Object { $_.paretoEligible -eq $true -and $_.noRegressionPassed -eq $true })
    $acceptedArray = if ($null -eq $AcceptedEvaluations) { @() } else { @($AcceptedEvaluations) }
    foreach ($accepted in $acceptedArray) {
        try { Assert-ESABCDAdaptiveHash ([string]$accepted.evaluationHash) 'AcceptedEvaluationHash'; if ((Get-ESABCDAdaptiveHash (Get-ESABCDAdaptiveHashInput $accepted 'evaluationHash')) -cne [string]$accepted.evaluationHash) { throw 'hash' } } catch { throw 'ADAPTIVE_ACCEPTED_EVALUATION_HASH_MISMATCH' }
        if (-not $candidateMap.ContainsKey([string]$accepted.candidateRef.candidateId)) { throw 'ADAPTIVE_ACCEPTED_EVALUATION_CANDIDATE_NOT_FOUND' }
        $bound = $candidateMap[[string]$accepted.candidateRef.candidateId]
        if ([string]$accepted.candidateRef.candidateHash -cne [string]$bound.candidateHash -or [string]$accepted.planRef.planHash -cne [string]$Plan.planHash -or [string]$accepted.datasetRef.partition -cne 'validation' -or [string]$accepted.datasetRef.partitionHash -cne [string]$Plan.datasetRef.validationHash) { throw 'ADAPTIVE_ACCEPTED_EVALUATION_CONTEXT_MISMATCH' }
    }
    $dominators = @($eligible) + @($acceptedArray | Where-Object { $_.noRegressionPassed -eq $true })
    $frontier = [Collections.Generic.List[object]]::new()
    foreach ($evaluation in $eligible) {
        $candidateDeltas = @($evaluation.metricDeltas); $dominated = $false
        foreach ($other in $dominators) { if ([string]$other.evaluationId -ceq [string]$evaluation.evaluationId) { continue }; $allNoWorse = $true; $strictlyBetter = $false; foreach ($delta in $candidateDeltas) { $peer = @($other.metricDeltas | Where-Object { [string]$_.partition -ceq [string]$delta.partition -and [string]$_.name -ceq [string]$delta.name }) | Select-Object -First 1; if ($null -eq $peer -or [double]$peer.signedDelta -lt [double]$delta.signedDelta) { $allNoWorse = $false; break }; if ([double]$peer.signedDelta -gt [double]$delta.signedDelta) { $strictlyBetter = $true } }; if ($allNoWorse -and $strictlyBetter) { $dominated = $true; break } }
        if (-not $dominated) { [void]$frontier.Add([pscustomobject][ordered]@{ candidateId = [string]$evaluation.candidateRef.candidateId; candidateHash = [string]$evaluation.candidateRef.candidateHash; evaluationId = [string]$evaluation.evaluationId; evaluationHash = [string]$evaluation.evaluationHash }) }
    }
    $roundScores = @($evaluationArray | Group-Object { [int]$_.candidateRef.roundNo } | Sort-Object { [int]$_.Name } | ForEach-Object { $best = ($_.Group | ForEach-Object { ($_.metricDeltas | Measure-Object -Property signedDelta -Sum).Sum } | Sort-Object -Descending | Select-Object -First 1); [pscustomobject][ordered]@{ roundNo = [int]$_.Name; bestScore = [double]$best } })
    $converged = $false; $window = [int]$Plan.budgets.convergenceWindow
    $convergenceThreshold=[double](($Plan.metricDefinitions|Measure-Object -Property minDelta -Minimum).Minimum);if ($roundScores.Count -ge $window) { $recent = @($roundScores | Select-Object -Last $window); $range = [double](($recent | Measure-Object -Property bestScore -Maximum).Maximum) - [double](($recent | Measure-Object -Property bestScore -Minimum).Minimum); $converged = ($range -lt $convergenceThreshold) }
    $receipt = [ordered]@{ schemaVersion = 1; contractId = 'es://automation/contracts/abcd/adaptive-learning/v1'; recordType = 'ABCDAdaptiveLearningReceipt'; runId = 'abcd-alr-' + (Get-ESABCDAdaptiveHash ([ordered]@{ planHash = [string]$Plan.planHash; candidateHashes = @($candidateArray | ForEach-Object { [string]$_.candidateHash }); evaluationHashes = @($evaluationArray | ForEach-Object { [string]$_.evaluationHash }) })).Substring(0, 32); planRef = [ordered]@{ planId = [string]$Plan.planId; planHash = [string]$Plan.planHash }; candidateCount = $candidateArray.Count; evaluationCount = $evaluationArray.Count; caseCount = $caseCalls; metricCallCount = $metricCalls; paretoFrontier = @($frontier | Sort-Object candidateId); roundScores = $roundScores; convergence = [ordered]@{ converged = $converged; window = $window; minImprovement = $convergenceThreshold; ownsTaskCompletion = $false }; decision = if ($frontier.Count -eq 0) { 'no-safe-candidate' } elseif ($converged) { 'await-explicit-review' } else { 'continue-within-budget' }; promotion = [ordered]@{ promotionAllowed = $false; requiresExplicitApply = $true }; receiptHash = $null; nonClaims = @('no-automatic-promotion', 'static-evaluation-only', 'holdout-not-consumed', 'not-task-completion', 'no-runtime-or-release-claim') }
    $result = [pscustomobject]$receipt; $result.receiptHash = Get-ESABCDAdaptiveHash (Get-ESABCDAdaptiveHashInput $result 'receiptHash'); return $result
}

function Test-ESABCDAdaptiveLearningReceipt {
    [CmdletBinding()] param([Parameter(Mandatory)]$Receipt, [Parameter(Mandatory)]$Plan)
    $issues = [Collections.Generic.List[string]]::new()
    try { Assert-ESABCDAdaptiveHash ([string]$Receipt.receiptHash) 'ReceiptHash'; if ((Get-ESABCDAdaptiveHash (Get-ESABCDAdaptiveHashInput $Receipt 'receiptHash')) -cne [string]$Receipt.receiptHash) { [void]$issues.Add('ADAPTIVE_RECEIPT_HASH_MISMATCH') } } catch { [void]$issues.Add($_.Exception.Message) }
    if ([string]$Receipt.planRef.planHash -cne [string]$Plan.planHash) { [void]$issues.Add('ADAPTIVE_RECEIPT_PLAN_MISMATCH') }
    if ([int]$Receipt.candidateCount -gt [int]$Plan.budgets.maxCandidates -or [int]$Receipt.evaluationCount -gt [int]$Plan.budgets.maxEvaluations) { [void]$issues.Add('ADAPTIVE_RECEIPT_BUDGET_EXCEEDED') }
    if ([int]$Receipt.caseCount -gt [int]$Plan.budgets.maxCases -or [int]$Receipt.metricCallCount -gt [int]$Plan.budgets.maxMetricCalls) { [void]$issues.Add('ADAPTIVE_RECEIPT_CALL_BUDGET_EXCEEDED') }
    if ($Receipt.convergence.ownsTaskCompletion -ne $false) { [void]$issues.Add('ADAPTIVE_CONVERGENCE_CANNOT_OWN_TASK_COMPLETION') }
    if ($Receipt.promotion.promotionAllowed -ne $false -or $Receipt.promotion.requiresExplicitApply -ne $true) { [void]$issues.Add('ADAPTIVE_RECEIPT_AUTO_PROMOTION_FORBIDDEN') }
    [pscustomobject][ordered]@{ status = if ($issues.Count) { 'failed' } else { 'passed' }; issues = @($issues); promotionAllowed = $false; runtimeStatus = 'runtime-not-run' }
}

Export-ModuleMember -Function ConvertTo-ESABCDAdaptiveCanonical,Get-ESABCDAdaptiveHash,New-ESABCDDatasetPartitionManifest,Test-ESABCDDatasetPartitionManifest,New-ESABCDAdaptiveLearningPlan,New-ESABCDPolicyCandidate,Test-ESABCDPolicyCandidate,New-ESABCDPolicyEvaluation,New-ESABCDHoldoutGate,Invoke-ESABCDAdaptiveLearningSelection,Test-ESABCDAdaptiveLearningReceipt
