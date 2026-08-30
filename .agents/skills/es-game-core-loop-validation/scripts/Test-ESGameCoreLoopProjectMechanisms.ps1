[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path)
$ErrorActionPreference='Stop'
function Invoke-Receipt([string]$Path,[string[]]$Args){
  $raw=& powershell -NoProfile -File $Path @Args 2>&1
  if($LASTEXITCODE -ne 0){throw "PROJECT_MECHANISM_FAILED:$([IO.Path]::GetFileName($Path))"}
  $json=[regex]::Match((($raw -join "`n").Trim()),'(?s)\{.*\}\s*$').Value
  if([string]::IsNullOrWhiteSpace($json)){throw "PROJECT_MECHANISM_NOT_JSON:$([IO.Path]::GetFileName($Path))"}
  $json|ConvertFrom-Json
}
$platform=Join-Path $ProjectRoot 'ES/Automation/TaskContextRuntime/Test-ESPlatformEvidenceContract.ps1'
$evaluation=Join-Path $ProjectRoot 'ES/Automation/TaskContextRuntime/Test-ESTaskContextEvaluationAdapter.ps1'
$cross=Join-Path $ProjectRoot 'ES/Automation/TaskContextRuntime/Test-ESTaskContextCrossProcess.ps1'
$report=Join-Path ([IO.Path]::GetTempPath()) ('es-core-cross-process-'+[guid]::NewGuid().ToString('N')+'.json')
try {
  $p=Invoke-Receipt $platform @()
  $e=Invoke-Receipt $evaluation @()
  $x=Invoke-Receipt $cross @('-ProjectRoot',$ProjectRoot,'-ReportPath',$report)
  foreach($r in @($p,$e,$x)){if([string]$r.status -ne 'passed'){throw 'PROJECT_MECHANISM_STATUS_NOT_PASSED'}}
  [ordered]@{status='passed';platformCases=$p.passedCount;evaluationCases=$e.passedCount;crossProcessCases=$x.passedCount;runtimeStatus='runtime-not-run';deterministic=$true}|ConvertTo-Json
} finally { if(Test-Path -LiteralPath $report){Remove-Item -LiteralPath $report -Force -ErrorAction SilentlyContinue} }
