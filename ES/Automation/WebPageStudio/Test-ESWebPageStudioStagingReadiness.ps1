[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ArtifactDirectory,[string]$ReportPath='')
$ErrorActionPreference='Stop';$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$root=[IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\')+'\';$dir=(Resolve-Path -LiteralPath $ArtifactDirectory -ErrorAction Stop).Path
if(-not $dir.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'ArtifactDirectory must remain under project root.'}
$gates=[Collections.Generic.List[object]]::new()
function Read-Receipt([string]$name){$p=Join-Path $dir $name;if(-not(Test-Path $p -PathType Leaf)){return $null};try{return Get-Content $p -Raw -Encoding UTF8|ConvertFrom-Json}catch{return $null}}
function Add-Gate([string]$Id,[string]$Status,[string]$Detail){$gates.Add([pscustomobject]@{gate=$Id;status=$Status;detail=$Detail})}
function PresentStatus($receipt,[string]$desired='passed',[string]$fallback='blocked'){if($receipt -and [string]$receipt.status -eq $desired){return 'passed'};return $fallback}
$static=Read-Receipt 'static-signals.json';$integrity=Read-Receipt 'artifact-integrity.json';$cache=Read-Receipt 'cache-policy-validation.json';$dynamic=Read-Receipt 'dynamic-state-replay-validation.json'
Add-Gate 'static-signals' (PresentStatus $static) 'Quality, Accessibility, Contract and UTF-8'
Add-Gate 'artifact-integrity' (PresentStatus $integrity) 'final artifact hash and required-file snapshot'
Add-Gate 'cache-policy' (PresentStatus $cache 'passed' 'review') 'route cache policy contract'
Add-Gate 'dynamic-replay' (PresentStatus $dynamic 'passed' 'review') 'offline dynamic state machine replay'
Add-Gate 'staging-http' 'not-run' 'requires explicitly authorized HTTPS staging request'
Add-Gate 'lighthouse-trace' 'not-run' 'requires real browser trace and Lighthouse execution'
Add-Gate 'rollback' 'not-run' 'requires deployment target and reversible rollback test'
$blocked=@($gates|Where-Object status -eq 'blocked');$external=@($gates|Where-Object status -eq 'not-run')
$status=if($blocked.Count){'blocked'}elseif($external.Count){'review'}else{'ready-for-staging'}
$receipt=[ordered]@{schemaVersion=1;recordType='WebPageStudioStagingReadinessReceipt';status=$status;artifactDirectory=$dir;gates=$gates;localEvidence=[ordered]@{staticSignals=if($static){$static.status}else{'missing'};artifactIntegrity=if($integrity){$integrity.status}else{'missing'};cachePolicy=if($cache){$cache.status}else{'missing'};dynamicReplay=if($dynamic){$dynamic.status}else{'missing'}};externalGates=@('staging-http','lighthouse-trace','rollback');runtimeStatus='runtime-not-run';evidenceLevel='S1';nonClaims=@('Readiness is a preflight decision and does not deploy, contact a network, run Lighthouse, or test rollback.','review means local evidence is ready for authorized staging, not production acceptance.')}
$json=$receipt|ConvertTo-Json -Depth 12;if($ReportPath){$out=[IO.Path]::GetFullPath((Join-Path (Get-Location) $ReportPath));if(-not $out.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'ReportPath must remain under project root.'};New-Item -ItemType Directory (Split-Path $out) -Force|Out-Null;[IO.File]::WriteAllText($out,$json,[Text.UTF8Encoding]::new($false))};$json
