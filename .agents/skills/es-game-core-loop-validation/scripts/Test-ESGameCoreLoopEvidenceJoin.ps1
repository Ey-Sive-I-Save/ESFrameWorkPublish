[CmdletBinding()]
param()
$ErrorActionPreference='Stop';$root=Join-Path ([IO.Path]::GetTempPath()) ('es-game-loop-join-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Force $root|Out-Null
try {
  $skill=Resolve-Path (Join-Path $PSScriptRoot '..');$planPath=Join-Path $root 'plan.json'
  & (Join-Path $PSScriptRoot 'New-ESGameCoreLoopValidationPlan.ps1') -GoalRevisionPath (Join-Path $skill 'governance.json') -RoutePlanPath (Join-Path $skill 'references/bindings/integration.binding.json') -TaskId join-fixture -OutputPath $planPath|Out-Null
  $plan=Get-Content -Raw -Encoding UTF8 $planPath|ConvertFrom-Json;$paths=@()
  foreach($layer in @('structure','implementation','presentation','performance')){
    $p=Join-Path $root ($layer+'.json');$r=[ordered]@{taskId='join-fixture';agentId="game-core-loop-$layer";layer=$layer;entryPoint="fixture/$layer";expected='receipt accepted';observed='receipt accepted';status='passed';sourceHash=('a'*64);runtimeStatus='runtime-not-run';claimsNotProven=@('Unity Runtime');planHash=$plan.planHash;sourceSnapshotHash=$plan.sourceSnapshotHash};[IO.File]::WriteAllText($p,($r|ConvertTo-Json),[Text.UTF8Encoding]::new($false));$paths+=$p
  }
  $join=& (Join-Path $PSScriptRoot 'Join-ESGameCoreLoopEvidence.ps1') -PlanPath $planPath -ReceiptPath $paths|ConvertFrom-Json
  if($join.decision -ne 'partial'){throw 'POSITIVE_JOIN_FAILED'}
  $tampered=Get-Content -Raw -Encoding UTF8 $paths[0]|ConvertFrom-Json;$tampered.sourceSnapshotHash=('b'*64);[IO.File]::WriteAllText($paths[0],($tampered|ConvertTo-Json),[Text.UTF8Encoding]::new($false));$negative=& (Join-Path $PSScriptRoot 'Join-ESGameCoreLoopEvidence.ps1') -PlanPath $planPath -ReceiptPath $paths|ConvertFrom-Json
  if($negative.decision -ne 'unverifiable' -or -not @($negative.findings|Where-Object code -eq 'SOURCE_SNAPSHOT_MISMATCH')){throw 'SNAPSHOT_DRIFT_NOT_REJECTED'}
  [ordered]@{status='passed';positiveDecision=$join.decision;negativeDecision=$negative.decision;receiptCount=$join.receiptCount;snapshotDriftRejected=$true;runtimeStatus='runtime-not-run'}|ConvertTo-Json
} finally {Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue}
