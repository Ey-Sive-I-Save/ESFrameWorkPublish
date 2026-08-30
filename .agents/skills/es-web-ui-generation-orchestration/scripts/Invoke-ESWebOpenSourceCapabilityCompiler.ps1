[CmdletBinding()]
param([Parameter(Mandatory)][string]$ProfilePath,[string]$ManifestPath,[string]$OutputPath)
$ErrorActionPreference='Stop'
function Read-Json($p){Get-Content -Raw -Encoding UTF8 $p|ConvertFrom-Json}
$profile=Read-Json $ProfilePath
if(!$ManifestPath){$ManifestPath=Join-Path $PSScriptRoot '..\references\open-source-source-manifest.json'}
$manifest=Read-Json $ManifestPath
$expected=@('nextjs','astro','nuxt','sveltekit','remix','qwik')
$entries=@($manifest.entries)
$missing=@($expected|?{$_ -notin @($entries.framework)})
$snapshotRoot=[string]$manifest.snapshotRoot
$evidence=@()
$invalidEvidence=@()
foreach($entry in $entries){
  $sourceFile=Join-Path $snapshotRoot ([string]$entry.sourceFile)
  $licenseFile=Join-Path $snapshotRoot ([string]$entry.licenseFile)
  if(!(Test-Path -LiteralPath $sourceFile) -or !(Test-Path -LiteralPath $licenseFile)) { $invalidEvidence += "missing-snapshot:$($entry.framework)"; continue }
  $sourceHash=(Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash.ToLowerInvariant()
  $licenseHash=(Get-FileHash -LiteralPath $licenseFile -Algorithm SHA256).Hash.ToLowerInvariant()
  if($sourceHash -ne ([string]$entry.sourceSha256).ToLowerInvariant()){$invalidEvidence += "source-hash-mismatch:$($entry.framework)"}
  if($licenseHash -ne ([string]$entry.licenseSha256).ToLowerInvariant()){$invalidEvidence += "license-hash-mismatch:$($entry.framework)"}
  $evidence += [ordered]@{framework=$entry.framework;mechanism=[string]$entry.mechanism;capabilities=@($entry.capabilities);sourcePath=$entry.sourcePath;sourceSha256=$sourceHash;licenseSha256=$licenseHash;verified=$true}
}
$status=if($profile.status -in @('accepted','calibrated-static') -and !$missing -and !$invalidEvidence){'accepted'}else{'blocked'}
$capabilitySources=@{}
foreach($ev in $evidence){ foreach($cap in @($ev.capabilities)){ if(!$capabilitySources.ContainsKey($cap)){$capabilitySources[$cap]=@()}; $capabilitySources[$cap]+=$ev.framework } }
$requiredCapabilities=@($profile.capabilities.PSObject.Properties.Name)
$missingCapabilityEvidence=@($requiredCapabilities|?{$_ -notin $capabilitySources.Keys})
if($missingCapabilityEvidence.Count -gt 0){$status='blocked';$invalidEvidence += $missingCapabilityEvidence|%{"missing-capability-evidence:$_"}}
$strategies=[ordered]@{
 renderPolicyExecutor=[ordered]@{mode=$profile.capabilities.renderPolicy.default; fallback='static-html'; cacheKey='route+dataHash'}
 componentBoundaryExecutor=[ordered]@{mode=$profile.capabilities.componentBoundary.default; interactiveRegions=3; marker='data-component-boundary'}
 routeDataContractExecutor=[ordered]@{requiredFields=@($profile.capabilities.routeDataContract.fields); error='inline-error-state'; marker='data-route-contract'}
 interactionStateMachineExecutor=[ordered]@{events=@($profile.capabilities.interactionStateMachine.events); terminalStates=@('idle','loading','success','error','cancelled'); marker='data-state-machine'}
 progressiveEnhancementExecutor=[ordered]@{fallback=$profile.capabilities.progressiveEnhancement.staticFallback; hydration='opt-in'; marker='data-progressive-enhancement'}
 resumabilityBudgetExecutor=[ordered]@{maxHydratedRegions=$profile.capabilities.resumabilityBudget.maxHydratedRegions; marker='data-resumability-budget'}
 performanceBudgetExecutor=[ordered]@{lcpP75Ms=$profile.capabilities.performanceBudget.lcpP75Ms; inpP75Ms=$profile.capabilities.performanceBudget.inpP75Ms; clsP75=$profile.capabilities.performanceBudget.clsP75; marker='data-performance-budget'}
}
$result=[ordered]@{schemaVersion=2;recordType='ESWebOpenSourceCapabilityCompilerReceipt';status=$status;decisionStatus=$status;profileId=$profile.profileId;frameworks=@($entries.framework);sourceEvidence=$evidence;capabilitySources=$capabilitySources;compiledStrategies=$strategies;nonClaims=@('static strategy compilation only','no framework runtime vendored','no browser/network/Unity/release evidence');blockedReasons=@(($missing|%{"missing-framework:$_"})+$invalidEvidence)}
$json=$result|ConvertTo-Json -Depth 20
if($OutputPath){[IO.File]::WriteAllText((Resolve-Path $OutputPath),$json,[Text.UTF8Encoding]::new($false))}
$json
if($status -ne 'accepted'){exit 1}
