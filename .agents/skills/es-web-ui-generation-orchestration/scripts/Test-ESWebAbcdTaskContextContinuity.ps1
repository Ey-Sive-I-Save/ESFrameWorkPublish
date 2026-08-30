[CmdletBinding()]
param([string]$BootstrapRoot='ES/Output/WebPageStudio/bootstrap')
$ErrorActionPreference='Stop';$root=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path;$dir=Join-Path $root $BootstrapRoot
$files=@('round-01-intake-github.json','round-02-focus-github.json','round-03-task-context-github-verified.json','round-04-knowledge-route.json','round-05-capability-design-accepted.json','round-06-deep-design-github.json')
$required=@('abcdEnvelope','subAgentEnvelope','taskContextRef','actualUsage')
$findings=@();$rows=@()
foreach($f in $files){$p=Join-Path $dir $f;if(-not(Test-Path -LiteralPath $p)){ $findings+="context-chain.missing-file:$f";continue };$j=Get-Content -Raw -Encoding UTF8 $p|ConvertFrom-Json;$missing=@($required|Where-Object{$null -eq $j.PSObject.Properties[$_]});if($missing.Count){$findings+="$f missing: $($missing -join ',')"};$rows+=[ordered]@{file=$f;status=$j.status;taskId=[string]$j.taskId;missing=$missing}}
$parity=@('bounded-tool-action','failure-recovery','branch-evaluation','state-transition-guard','environment-trust-gate','audit-evidence-chain');$rows|ForEach-Object{if($_.file -match 'round-05|round-06'){$j=Get-Content -Raw -Encoding UTF8 (Join-Path $dir $_.file)|ConvertFrom-Json;$caps=@($j.abcdEnvelope.requiredCapabilities);foreach($c in $parity){if($caps -notcontains $c){$findings+="$($_.file) missing ABCC capability:$c"}}}}
[ordered]@{schemaVersion=1;recordType='WebAbcdTaskContextContinuityReceipt';status=if($findings.Count){'blocked'}else{'passed'};requiredStageEnvelope=$required;requiredAbccParity=$parity;stages=$rows;findings=$findings;nonClaims=@('does-not-prove external model execution','does-not-prove browser/network/runtime') }|ConvertTo-Json -Depth 20
