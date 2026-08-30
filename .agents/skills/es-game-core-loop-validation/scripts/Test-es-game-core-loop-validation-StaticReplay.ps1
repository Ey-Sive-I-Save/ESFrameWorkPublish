# Responsibility profile: engineering
[CmdletBinding()]
param([string]$SkillRoot=(Join-Path (Get-Location) '.agents/skills/es-game-core-loop-validation'))
$ErrorActionPreference='Stop'
$broot=Join-Path $SkillRoot 'references/bindings'
$required=@(
  'SKILL.md','governance.json','static-replay.manifest.json','evidence-contract.binding.json',
  'references/static-replay-adapter.md',
  'references/bindings/multi-agent.binding.json','references/bindings/integration.binding.json',
  'references/bindings/state-machine.binding.json','references/bindings/capability.matrix.json',
  'references/bindings/execution.binding.json','references/bindings/static-claim.coverage.json',
  'scripts/Test-ESGameCoreLoopProjectMechanisms.ps1')
foreach($f in $required){if(-not(Test-Path -LiteralPath (Join-Path $SkillRoot $f) -PathType Leaf)){throw "SKILL_RESOURCE_MISSING:$f"}}
$b=Get-Content -Raw -Encoding UTF8 (Join-Path $broot 'multi-agent.binding.json')|ConvertFrom-Json
$i=Get-Content -Raw -Encoding UTF8 (Join-Path $broot 'integration.binding.json')|ConvertFrom-Json
$s=Get-Content -Raw -Encoding UTF8 (Join-Path $broot 'state-machine.binding.json')|ConvertFrom-Json
$c=Get-Content -Raw -Encoding UTF8 (Join-Path $broot 'capability.matrix.json')|ConvertFrom-Json
$x=Get-Content -Raw -Encoding UTF8 (Join-Path $broot 'execution.binding.json')|ConvertFrom-Json
$manifest=Get-Content -Raw -Encoding UTF8 (Join-Path $SkillRoot 'static-replay.manifest.json')|ConvertFrom-Json
$coverage=Get-Content -Raw -Encoding UTF8 (Join-Path $broot 'static-claim.coverage.json')|ConvertFrom-Json
if(@($b.fanOut.requiredAgents).Count -ne 4 -or @($b.abcdCapabilities).Count -ne 6){throw 'MULTI_AGENT_OR_ABCD_CLOSURE_INVALID'}
if($i.abcdIntegration.finalAuthority -ne 'ABCD-final-decision'){throw 'FINAL_AUTHORITY_INVALID'}
if(@($s.states).Count -ne 10 -or @($c.layers.PSObject.Properties).Count -ne 4){throw 'STATE_OR_LAYER_CLOSURE_INVALID'}
if(@($x.operations).Count -lt 12){throw 'EXECUTION_BINDING_INCOMPLETE'}
$projectRoot=(Resolve-Path (Join-Path $SkillRoot '..\..\..')).Path
foreach($claim in @($manifest.staticClaims)){
  $entry=$coverage.coverage.PSObject.Properties[[string]$claim]
  if($null -eq $entry -or @($entry.Value).Count -eq 0){throw "STATIC_CLAIM_UNBOUND:$claim"}
  foreach($ref in @($entry.Value)){
    $candidate=if([IO.Path]::IsPathRooted([string]$ref)){[string]$ref}elseif(([string]$ref) -match '^(SKILL\.md|references/|scripts/)'){Join-Path $SkillRoot ([string]$ref)}else{Join-Path $projectRoot ([string]$ref)}
    if(-not(Test-Path -LiteralPath $candidate -PathType Leaf)){throw "STATIC_CLAIM_EVIDENCE_MISSING:${claim}:$ref"}
  }
}

# Every deterministic static case is executed; a manifest-only declaration is insufficient.
$tests=@(
  'Test-ESGameCoreLoopStateTransition.ps1','Test-ESGameCoreLoopStateMachine.ps1',
  'Test-ESGameCoreLoopExecutionBinding.ps1','Test-ESGameCoreLoopEvidenceJoin.ps1',
  'Test-ESGameCoreLoopEvidenceRecovery.ps1','Test-ESGameCoreLoopWorkerEvidence.ps1',
  'Test-ESGameCoreLoopWorkerIsolation.ps1','Test-ESGameCoreLoopChildReceiptIdentity.ps1',
  'Test-ESGameCoreLoopABCDCapabilities.ps1','Test-ESGameCoreLoopAdversarialMechanisms.ps1',
  'Test-ESGameCoreLoopStrictReceipt.ps1','Test-ESGameCoreLoopFinalGate.ps1',
  'Test-ESGameCoreLoopStress.ps1','Test-ESGameCoreLoopProjectMechanisms.ps1')
$results=[ordered]@{}
foreach($name in $tests){
  $path=Join-Path $SkillRoot "scripts\$name"
  if(-not(Test-Path -LiteralPath $path -PathType Leaf)){throw "REPLAY_TEST_MISSING:$name"}
  if($name -eq 'Test-ESGameCoreLoopProjectMechanisms.ps1'){
    $raw=& powershell -NoProfile -File $path -ProjectRoot $projectRoot 2>&1
  } else {
    $raw=& powershell -NoProfile -File $path 2>&1
  }
  if($LASTEXITCODE -ne 0){throw "REPLAY_TEST_FAILED:$name :: $($raw -join ' ')"}
  try{
    $text=($raw -join "`n").Trim()
    # PowerShell module warnings may precede the receipt; parse only the final JSON object.
    $json=[regex]::Match($text,'(?s)\{.*\}\s*$').Value
    if([string]::IsNullOrWhiteSpace($json)){throw 'empty-json'}
    $results[$name]=$json|ConvertFrom-Json
  }catch{throw "REPLAY_TEST_NOT_JSON:$name"}
}
[ordered]@{
  status='passed';skillName='es-game-core-loop-validation';requiredAgents=@($b.fanOut.requiredAgents)
  abcdCapabilities=@($b.abcdCapabilities);stateCount=@($s.states).Count;layerCount=@($c.layers.PSObject.Properties).Count
  operationCount=@($x.operations).Count;executedStaticTests=$results.Keys;testResults=$results
  runtimeStatus='runtime-not-run';deterministic=$true
}|ConvertTo-Json -Depth 20
