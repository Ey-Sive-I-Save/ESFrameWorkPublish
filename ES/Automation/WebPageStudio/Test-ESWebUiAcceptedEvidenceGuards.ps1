[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path;$f=Join-Path $PSScriptRoot 'fixtures'
$paths=@('network','preview','visual','release'|ForEach-Object{Join-Path $f ($_+'-receipt.accepted.synthetic.json')})
$raw=(& (Join-Path $PSScriptRoot 'Invoke-ESWebUiEvidenceAggregate.ps1') -NetworkReceiptPath $paths[0] -PreviewReceiptPath $paths[1] -VisualReceiptPath $paths[2] -ReleaseReceiptPath $paths[3]) -join "`n";$base=$raw|ConvertFrom-Json
function Write-Case($name,$obj){$path=Join-Path $env:TEMP ('es-web-ui-'+$name+'-'+[guid]::NewGuid().ToString('N')+'.json');[IO.File]::WriteAllText($path,($obj|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false));return $path}
function Blocked($path){try{& (Join-Path $PSScriptRoot 'Convert-ESWebUiAggregateToABCD.ps1') -AggregatePath $path | Out-Null;return $false}catch{return $true}}
$missing=$raw|ConvertFrom-Json;$missing.layers=@($missing.layers|Select-Object -First 3);$missingPath=Write-Case 'missing-layer' $missing
$noHash=$raw|ConvertFrom-Json;$noHash.layers[0].receiptHash='';$noHashPath=Write-Case 'missing-hash' $noHash
$drift=$raw|ConvertFrom-Json;$drift.layers[0].receiptHash=('0'*64);$driftPath=Write-Case 'hash-drift' $drift
$reportPath=Join-Path $env:TEMP ('es-web-ui-report-'+[guid]::NewGuid().ToString('N')+'.json');[IO.File]::WriteAllText($reportPath,'{"recordType":"WebPageStudioStaticReplayReport"}',[Text.UTF8Encoding]::new($false))
$reportBlocked=$false;try{& (Join-Path $PSScriptRoot 'Convert-ESWebUiAggregateToABCD.ps1') -AggregatePath (Write-Case 'report-base' $base) -StaticReplayReportPath $reportPath -StaticReplayReportHash ('0'*64)|Out-Null}catch{$reportBlocked=$_.Exception.Message -like '*STATIC_REPLAY_REPORT_HASH_DRIFT*'}
$checks=@([pscustomobject]@{case='missing-layer-blocked';passed=(Blocked $missingPath)},[pscustomobject]@{case='missing-hash-blocked';passed=(Blocked $noHashPath)},[pscustomobject]@{case='receipt-hash-drift-blocked';passed=(Blocked $driftPath)},[pscustomobject]@{case='static-report-hash-drift-blocked';passed=$reportBlocked})
$failed=@($checks|Where-Object{-not $_.passed});[ordered]@{validator='web-ui-accepted-evidence-guards';status=if($failed.Count){'failed'}else{'passed'};checks=$checks;runtimeStatus='runtime-not-run';nonClaims=@('static-negative-fixtures','no-abcd-promotion','temporary-files-outside-project')}|ConvertTo-Json -Depth 8;if($failed.Count){exit 1}
