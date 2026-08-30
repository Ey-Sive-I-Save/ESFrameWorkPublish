[CmdletBinding()]
param([Parameter(Mandatory)][string]$ReportPath,[string]$ProjectRoot=(Get-Location).Path)
$ErrorActionPreference='Stop'
$report=Get-Content -Raw -Encoding UTF8 -LiteralPath $ReportPath|ConvertFrom-Json
$input=[ordered]@{};foreach($p in $report.PSObject.Properties){if($p.Name -ne 'reportHash'){$input[$p.Name]=$p.Value}}
Import-Module (Join-Path $PSScriptRoot '..\ABCD\ESABCDEvidence.psm1') -Force
$hashOk=([string]$report.reportHash -match '^[a-f0-9]{64}$' -and (Get-ESABCDEvidenceHash $input) -ceq [string]$report.reportHash)
$countsOk=([int]$report.testCount -eq @($report.tests).Count -and [int]$report.passedCount -eq @($report.tests|Where-Object status -eq 'passed').Count -and [int]$report.failedCount -eq @($report.tests|Where-Object status -eq 'failed').Count)
$statusOk=([string]$report.status -eq $(if($report.failedCount -gt 0){'failed'}else{'passed'}))
$sourceOk=$false;if($report.PSObject.Properties['sourceHashes'] -and @($report.sourceHashes.PSObject.Properties).Count -gt 0){$sourceOk=$true;foreach($p in $report.sourceHashes.PSObject.Properties){$sourcePath=Join-Path (Resolve-Path -LiteralPath $ProjectRoot).Path $p.Name;if(-not(Test-Path -LiteralPath $sourcePath -PathType Leaf) -or (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant() -cne [string]$p.Value){$sourceOk=$false}}}
$ok=([string]$report.recordType -ceq 'WebPageStudioStaticReplayReport' -and [string]$report.runtimeStatus -ceq 'runtime-not-run' -and $hashOk -and $countsOk -and $statusOk -and $sourceOk)
[ordered]@{validator='web-ui-static-replay-report';status=if($ok){'passed'}else{'failed'};hashVerified=$hashOk;sourceHashesVerified=$sourceOk;countsConsistent=$countsOk;statusConsistent=$statusOk;runtimeStatus='runtime-not-run';nonClaims=@('report-shape-and-hash-only','does-not-prove-runtime-or-release')}|ConvertTo-Json -Depth 8
if(-not $ok){exit 1}
