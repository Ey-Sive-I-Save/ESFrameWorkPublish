[CmdletBinding()]
param([string]$ProjectRoot)
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($ProjectRoot)){$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path}
Import-Module (Join-Path $ProjectRoot 'ES/Automation/ABCD/ESABCInnovationRun.psm1') -Force
$valid=[pscustomobject]@{innovationDifferentialLevel=4;innovationDifferentialAssessment="qualitative engineering delta";baselinePlayerLevel=2;playerIncrementDelta=0;counterfactualRegression=@(1..3|ForEach-Object{[pscustomobject]@{round=$_;baselineOutcome="baseline-$($_)";candidateOutcome="candidate-$($_)";baselineScore=2;candidateScore=2;qualitativeDifference="qualitative-$($_)"}})}
$invalid=[pscustomobject]@{innovationDifferentialLevel=4;innovationDifferentialAssessment="missing round";counterfactualRegression=@([pscustomobject]@{round=1;baselineOutcome="b";candidateOutcome="c";qualitativeDifference="q"})}
$a=Test-ESABCEngineeringCreativeDifferential -Architecture $valid;$b=Test-ESABCEngineeringCreativeDifferential -Architecture $valid;$c=Test-ESABCEngineeringCreativeDifferential -Architecture $invalid
$stable=(($a|ConvertTo-Json -Compress -Depth 8) -ceq ($b|ConvertTo-Json -Compress -Depth 8));$pass=($a.status -eq "passed" -and $c.status -eq "failed" -and $stable);$state="failed";if($pass){$state="passed"}
[pscustomobject]@{status=$state;valid=$a.status;invalid=$c.status;deterministic=$stable;requiredCounterfactualRounds=$a.counterfactualRounds}
if(-not $pass){exit 1}
