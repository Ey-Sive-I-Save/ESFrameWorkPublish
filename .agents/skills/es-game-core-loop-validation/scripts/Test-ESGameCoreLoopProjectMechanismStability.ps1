[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,[ValidateRange(2,5)][int]$Rounds=3)
$ErrorActionPreference='Stop';$runner=Join-Path $PSScriptRoot 'Test-ESGameCoreLoopProjectMechanisms.ps1';$runs=@()
for($n=1;$n -le $Rounds;$n++){
  $raw=& powershell -NoProfile -File $runner -ProjectRoot $ProjectRoot 2>&1
  if($LASTEXITCODE -ne 0){throw "PROJECT_MECHANISM_STABILITY_FAILED:round=$n"}
  $json=[regex]::Match((($raw -join "`n").Trim()),'(?s)\{.*\}\s*$').Value
  if([string]::IsNullOrWhiteSpace($json)){throw "PROJECT_MECHANISM_STABILITY_NOT_JSON:round=$n"}
  $r=$json|ConvertFrom-Json;if([string]$r.status -ne 'passed'){throw "PROJECT_MECHANISM_STABILITY_STATUS_FAILED:round=$n"};$runs+=[pscustomobject]@{round=$n;status=$r.status;platformCases=$r.platformCases;evaluationCases=$r.evaluationCases;crossProcessCases=$r.crossProcessCases}
}
[ordered]@{status='passed';rounds=$Rounds;passedRounds=@($runs|Where-Object status -eq 'passed').Count;runs=$runs;runtimeStatus='runtime-not-run';deterministic=$true}|ConvertTo-Json -Depth 8
