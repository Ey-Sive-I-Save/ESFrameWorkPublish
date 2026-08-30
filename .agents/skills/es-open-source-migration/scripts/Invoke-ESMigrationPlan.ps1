[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$SourceRoot,
 [Parameter(Mandatory=$true)][string]$MappingPath,
 [Parameter(Mandatory=$true)][string]$PlanPath,
 [ValidateRange(1,20)][int]$MaxBatches=1,
 [string]$BatchId,
 [switch]$Execute
)
$ErrorActionPreference='Stop';$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$root=(Resolve-Path -LiteralPath $SourceRoot).Path.TrimEnd('\');$plan=Get-Content -Raw -Encoding UTF8 -LiteralPath $PlanPath|ConvertFrom-Json
if([int]$plan.maxFilesPerBatch -gt 20){throw 'Plan exceeds the global 20-file resource limit.'}
$runner=Join-Path (Split-Path -Parent $PSCommandPath) 'Invoke-ESMigrationBatch.ps1';$processed=@{};$receiptDir=Join-Path $root '.es-migration\batches';if(Test-Path $receiptDir){Get-ChildItem -LiteralPath $receiptDir -Filter '*.json' -File|ForEach-Object{try{$old=Get-Content -Raw -Encoding UTF8 $_.FullName|ConvertFrom-Json;foreach($row in @($old.rows)){$processed[[string]$row.relativePath.Replace('\','/')]=[string]$_.Name}}catch{}}};if($BatchId){$selected=@($plan.batches|Where-Object batchId -eq $BatchId);if($selected.Count -eq 0){throw "BatchId not found in plan: $BatchId"}}else{$selected=@($plan.batches|Select-Object -First $MaxBatches)};$results=@();$ordinal=0
foreach($batch in $selected){$ordinal++;$started=[DateTime]::UtcNow.ToString('o');Write-Output ("[ES migration] {0}/{1} START {2} ({3} files, mode={4})" -f $ordinal,$selected.Count,$batch.batchId,$batch.fileCount,($(if($Execute){'execute'}else{'dry-run'})));$status='complete';$detail=$null;$receipt=$null
 $already=@($batch.relativePaths|Where-Object{$processed.ContainsKey([string]$_)});if($already.Count -eq @($batch.relativePaths).Count){$status='skipped';$detail='already-processed';Write-Output ("[ES migration] {0}/{1} SKIP {2} already-processed" -f $ordinal,$selected.Count,$batch.batchId)}elseif($already.Count -gt 0){$status='failed';$detail="partially-processed ($($already.Count)/$($batch.fileCount)); regenerate plan";Write-Output ("[ES migration] {0}/{1} FAIL {2}: {3}" -f $ordinal,$selected.Count,$batch.batchId,$detail)}else{try{$raw=& $runner -SourceRoot $root -MappingPath $MappingPath -RelativePaths @($batch.relativePaths) -BatchId $batch.batchId -DryRun:(-not $Execute)|Out-String;$receipt=$raw|ConvertFrom-Json;if($Execute){$dir=Join-Path $root '.es-migration\batches';[IO.Directory]::CreateDirectory($dir)|Out-Null;[IO.File]::WriteAllText((Join-Path $dir ($batch.batchId+'.json')),$raw,(New-Object Text.UTF8Encoding($false)))};Write-Output ("[ES migration] {0}/{1} DONE {2} changed={3}" -f $ordinal,$selected.Count,$batch.batchId,$receipt.changedFiles)}catch{$status='failed';$detail=$_.Exception.Message;Write-Output ("[ES migration] {0}/{1} FAIL {2}: {3}" -f $ordinal,$selected.Count,$batch.batchId,$detail)}}
 $results+=[ordered]@{ordinal=$ordinal;batchId=$batch.batchId;group=$batch.group;fileCount=$batch.fileCount;status=$status;startedUtc=$started;completedUtc=[DateTime]::UtcNow.ToString('o');changedFiles=if($receipt){$receipt.changedFiles}else{$null};error=$detail}
 if($status -eq 'failed'){break}
}
$out=[ordered]@{schemaVersion=1;runner='es-migration-plan-executor';planPath=$PlanPath;execute=[bool]$Execute;requestedBatches=$MaxBatches;processedBatches=$results.Count;stoppedOnFailure=(@($results|Where-Object status -eq 'failed').Count -gt 0);results=$results;generatedUtc=[DateTime]::UtcNow.ToString('o')}
$reportDir=Join-Path $root '.es-migration\runs';[IO.Directory]::CreateDirectory($reportDir)|Out-Null;$reportPath=Join-Path $reportDir ('run-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.json');[IO.File]::WriteAllText($reportPath,($out|ConvertTo-Json -Depth 10),(New-Object Text.UTF8Encoding($false)));Write-Output ("[ES migration] RUN RECEIPT {0}" -f $reportPath);$out|ConvertTo-Json -Depth 5
