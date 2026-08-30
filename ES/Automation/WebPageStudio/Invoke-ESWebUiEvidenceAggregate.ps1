[CmdletBinding()]param(
  [string]$NetworkReceiptPath='', [string]$PreviewReceiptPath='', [string]$VisualReceiptPath='', [string]$ReleaseReceiptPath='',
  [string]$TaskId='web-ui-closure', [string]$FocusKey='web-ui-closure'
)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..')).TrimEnd('\')+'\'
function Read-Layer([string]$kind,[string]$path,[string]$runtimePassStatuses){
  if([string]::IsNullOrWhiteSpace($path)){return [ordered]@{layer=$kind;status='not-run';runtimeStatus='runtime-not-run';receiptPath=$null;findingCount=0}}
  $candidate=if([IO.Path]::IsPathRooted([string]$path)){[IO.Path]::GetFullPath([string]$path)}else{[IO.Path]::GetFullPath((Join-Path (Get-Location) $path))};if(-not $candidate.StartsWith($projectRoot,[StringComparison]::OrdinalIgnoreCase)){return [ordered]@{layer=$kind;status='blocked';runtimeStatus='runtime-not-run';receiptPath=$path;findingCount=1;findings=@('RECEIPT_PATH_OUTSIDE_PROJECT')}};$path=$candidate
  if(-not(Test-Path -LiteralPath $path -PathType Leaf)){return [ordered]@{layer=$kind;status='blocked';runtimeStatus='runtime-not-run';receiptPath=$path;findingCount=1;findings=@('RECEIPT_MISSING')}}
  $r=Get-Content -Raw -Encoding UTF8 -LiteralPath $path|ConvertFrom-Json;$s=[string]$r.status;$rs=[string]$r.runtimeStatus
  $identityFindings=@();if($r.PSObject.Properties['taskId'] -and [string]$r.taskId -and [string]$r.taskId -ne $TaskId){$identityFindings+='RECEIPT_TASK_MISMATCH'}
  $validator=@{'network'='Test-ESWebNetworkRuntimeReceipt.ps1';'preview'='Test-ESWebPreviewRuntimeReceipt.ps1';'visual'='Test-ESWebVisualRegressionReceipt.ps1';'release'='Test-ESWebReleaseAcceptanceReceipt.ps1'}[$kind];$validatorResult=$null;$findings=@()
  if($validator){$vp=Join-Path $PSScriptRoot $validator;$raw=& $vp -ReceiptPath $path 2>&1;try{$validatorResult=$raw|Select-Object -Last 1|ConvertFrom-Json}catch{$findings+='VALIDATOR_OUTPUT_INVALID'};if($validatorResult -and [string]$validatorResult.status -ne 'passed'){$findings+=@($validatorResult.findings)}}
  $findings+=$identityFindings;$hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant();if($findings.Count -gt 0){$s='blocked'}
  [ordered]@{layer=$kind;status=$s;runtimeStatus=$rs;receiptPath=$path;receiptId=[string]$r.receiptId;receiptHash=$hash;validatorStatus=if($validatorResult){[string]$validatorResult.status}else{'not-run'};findingCount=$findings.Count;findings=$findings}
}
$networkLayer=Read-Layer 'network' $NetworkReceiptPath 'runtime-passed'
$previewLayer=Read-Layer 'preview' $PreviewReceiptPath 'runtime-passed'
$visualLayer=Read-Layer 'visual' $VisualReceiptPath 'runtime-passed'
$releaseLayer=Read-Layer 'release' $ReleaseReceiptPath 'runtime-passed'
$layers=@($networkLayer,$previewLayer,$visualLayer,$releaseLayer)
$ids=@($layers|Where-Object {[string]$_.receiptId}|ForEach-Object {[string]$_.receiptId});$duplicateIds=@($ids|Group-Object|Where-Object Count -gt 1|ForEach-Object Name);if($duplicateIds.Count -gt 0){foreach($l in $layers){if($duplicateIds -contains [string]$l.receiptId){$l.status='blocked';$l.findings=@($l.findings)+'DUPLICATE_RECEIPT_ID';$l.findingCount=@($l.findings).Count}}}
$blocked=@($layers|Where-Object status -in @('blocked','failed'));$pending=@($layers|Where-Object { $_.status -in @('not-run','review') -or $_.runtimeStatus -eq 'runtime-not-run' })
$decision=if($blocked.Count -gt 0){'blocked'}elseif($pending.Count -gt 0){'partial'}else{'accepted'}
$aggregate=[ordered]@{schemaVersion=1;recordType='WebPageStudioUiEvidenceAggregate';aggregateId=('web-ui-agg-'+[guid]::NewGuid().ToString('N').Substring(0,12));taskId=$TaskId;focusKey=$FocusKey;decision=$decision;layers=$layers;blockedLayers=@($blocked|ForEach-Object layer);pendingLayers=@($pending|ForEach-Object layer);routeKeys=@('web-runtime','web-preview','web-visual-regression','web-release-acceptance','task-focus-context','abcd-evidence');evidencePolicy='layered-no-cross-layer-flattening';nonClaims=@('aggregate-does-not-upgrade-runtime-not-run','accepted-requires-four-layer-runtime-receipts','does-not-authorize-network-browser-or-release') }
$evidenceModule=Join-Path $PSScriptRoot '..\ABCD\ESABCDEvidence.psm1'; Import-Module $evidenceModule -Force
$aggregateObject=($aggregate|ConvertTo-Json -Depth 12 -Compress|ConvertFrom-Json)
$aggregate.aggregateHash=Get-ESABCDEvidenceHash $aggregateObject
$receiptInput=($aggregate|ConvertTo-Json -Depth 12 -Compress|ConvertFrom-Json)
$aggregate.receiptHash=Get-ESABCDEvidenceHash (Get-ESABCDReceiptHashInput $receiptInput)
$aggregate|ConvertTo-Json -Depth 12
