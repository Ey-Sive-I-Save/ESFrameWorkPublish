[CmdletBinding()]
param([string]$SkillRoot = (Join-Path (Get-Location) '.agents/skills/es-game-core-loop-validation'))
$ErrorActionPreference='Stop';$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$tmp=Join-Path ([IO.Path]::GetTempPath()) ('es-loop-worker-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $tmp|Out-Null
try {
  $task='task-worker-fixture';$hash=('a'*64)
  $w=[ordered]@{taskId='es.task-context.evaluate';status='Passed';inputManifestHash=$hash}|ConvertTo-Json
  $e=[ordered]@{taskId=$task;recordType='EvaluationRecord';decisionScope='task-object';decision='accepted';evidenceState='observed'}|ConvertTo-Json
  $wp=Join-Path $tmp 'worker.json';$ep=Join-Path $tmp 'eval.json';[IO.File]::WriteAllText($wp,$w,(New-Object Text.UTF8Encoding($false)));[IO.File]::WriteAllText($ep,$e,(New-Object Text.UTF8Encoding($false)))
  $ok=& (Join-Path $SkillRoot 'scripts/Convert-ESGameCoreLoopWorkerEvidence.ps1') -WorkerResultPath $wp -EvaluationRecordPath $ep -TaskId $task -Layer structure|ConvertFrom-Json
  if($ok.status -ne 'passed' -or $ok.completionAuthority -ne 'ABCD-final-decision'){throw 'WORKER_CONVERSION_POSITIVE_FAILED'}
  $bad=$e|ConvertFrom-Json;$bad.taskId='other-task';[IO.File]::WriteAllText($ep,($bad|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
  $failed=$false;try{& (Join-Path $SkillRoot 'scripts/Convert-ESGameCoreLoopWorkerEvidence.ps1') -WorkerResultPath $wp -EvaluationRecordPath $ep -TaskId $task -Layer structure|Out-Null}catch{$failed=$true};if(-not $failed){throw 'WORKER_TASK_BINDING_NOT_ENFORCED'}
  [ordered]@{status='passed';positive='accepted receipt';negative='task binding rejected';deterministic=$true}|ConvertTo-Json
} finally {Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue}
