[CmdletBinding()]
param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$registryPath = Join-Path $root 'ES/Automation/Contracts/es-authority-consumer-registry-v1.json'
if (-not (Test-Path -LiteralPath $registryPath -PathType Leaf)) { throw 'AUTHORITY_CONSUMER_REGISTRY_MISSING' }
$registry = Get-Content -LiteralPath $registryPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ([int]$registry.schemaVersion -ne 1 -or [string]$registry.contractId -cne 'es://automation/contracts/authority-consumer-registry/v1') { throw 'AUTHORITY_CONSUMER_REGISTRY_INVALID' }
$cases = foreach ($consumer in @($registry.consumers)) {
    $path = Join-Path $root ([string]$consumer.path)
    $issues = [Collections.Generic.List[string]]::new()
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { [void]$issues.Add('missing-file') }
    else {
        $text = Get-Content -LiteralPath $path -Raw -Encoding UTF8
        foreach ($marker in @($consumer.requiredMarkers)) { if ($text.IndexOf([string]$marker, [StringComparison]::Ordinal) -lt 0) { [void]$issues.Add('missing-marker:' + [string]$marker) } }
        if ([string]$consumer.kind -eq 'final-decision' -and $text -notmatch '(?i)authority') { [void]$issues.Add('no-authority-signal') }
        if ([string]$consumer.kind -in @('candidate-only','projection-only') -and $text -notmatch '(?i)completionDecision\s*=\s*\$null|completionDecisionRequired\s*=\s*\$true|self-accept|never declares|not.*completion|does-not-authorize') { [void]$issues.Add('candidate-boundary-not-visible') }
    }
    [pscustomobject][ordered]@{ id=[string]$consumer.id; path=[string]$consumer.path; kind=[string]$consumer.kind; status=if($issues.Count){'failed'}else{'passed'}; issues=@($issues); sourceSha256=if(Test-Path -LiteralPath $path -PathType Leaf){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}else{$null} }
}
$registered = @($registry.consumers | ForEach-Object { ([string]$_.path).Replace('/','\') })
$implicit = [Collections.Generic.List[object]]::new()
foreach ($scanRoot in @((Join-Path $root 'Assets/Plugins/ES'), (Join-Path $root 'Assets/Scripts/ESLogic/Runtime/Developer/AITest'), (Join-Path $root 'Assets/Scripts/ESLogic/Editor'), (Join-Path $root 'ES/Automation'))) {
foreach ($file in @(Get-ChildItem -LiteralPath $scanRoot -Recurse -File -Include '*.cs','*.ps1','*.psm1')) {
    if ($file.Extension -notin @('.cs','.ps1','.psm1')) { continue }
    if ($file.FullName -match '\\(Tests?|fixtures?)\\') { continue }
    # Validator/test scripts intentionally contain positive assertions such as
    # promotionAllowed=true; they are not production authority consumers.
    if ($file.Name -like 'Test-*') { continue }
    $relative = $file.FullName.Substring($root.Length).TrimStart('\','/').Replace('/','\')
    if ($registered -contains $relative) { continue }
    $text = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    $hasAiCapabilityAcceptance = ($text -match '(?m)accepted\s*=\s*true') -and ($text -match '(?i)allowAiInvoke|fromAi|completionDecision|ESAutomation')
    $finalSignalPattern = '(?m)completionDecision\s*=\s*new\s+|completionDecision\.accepted\s*=|decisionStatus\s*=|promotionAllowed\s*=\s*\$?true|ESAutomationTaskInvocationResult\.(Accepted|Completed)\s*\(|allowAiInvoke\s*=\s*true'
    if ($hasAiCapabilityAcceptance) { $finalSignalPattern += '|accepted\s*=\s*true' }
    if ($text -match $finalSignalPattern) {
        $reason = if ($text -match '(?m)promotionAllowed\s*=\s*\$?true') { 'unregistered-promotion-signal' } elseif ($hasAiCapabilityAcceptance) { 'unregistered-capability-acceptance-signal' } elseif ($text -match 'ESAutomationTaskInvocationResult\.(Accepted|Completed)\s*\(') { 'unregistered-automation-result-signal' } elseif ($text -match 'allowAiInvoke\s*=\s*true') { 'unregistered-ai-entrypoint' } else { 'unregistered-final-decision-write' }
        [void]$implicit.Add([pscustomobject]@{path=$relative; reason=$reason})
    }
}
}
$failed=@($cases | Where-Object status -eq 'failed')
if ($implicit.Count) { $failed += $implicit }
[pscustomobject][ordered]@{
    schemaVersion=1; validator='Test-ESAuthorityConsumerCoverage'; contractId=[string]$registry.contractId
    status=if($failed.Count){'failed'}else{'passed'}; consumerCount=$cases.Count; finalDecisionCount=@($cases|Where-Object kind -eq 'final-decision').Count
    candidateOnlyCount=@($cases|Where-Object kind -eq 'candidate-only').Count; passedCount=@($cases|Where-Object status -eq 'passed').Count; failedCount=$failed.Count
    cases=$cases; implicitUnregisteredFinalSignals=@($implicit); runtimeStatus='runtime-not-run'; claimsNotProven=@('host invocation frequency','Unity runtime behavior','external process behavior')
    registryHash=(Get-FileHash -LiteralPath $registryPath -Algorithm SHA256).Hash.ToLowerInvariant(); capturedUtc=[DateTime]::UtcNow.ToString('o')
} | ConvertTo-Json -Depth 12
if ($failed.Count) { exit 1 }
