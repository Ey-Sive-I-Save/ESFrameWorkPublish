[CmdletBinding()]param([string]$ProjectRoot='.')
$ErrorActionPreference='Stop';$root=(Resolve-Path $ProjectRoot).Path;$m=Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot '..\static-replay.manifest.json')|ConvertFrom-Json
if(@($m.cases).Count -lt 7){throw 'static replay manifest must contain seven cases'}
$checks=@(
  @{path='NORMALIZER';markers=@('selected-candidate-required','web-design-missing','evidence-incomplete')},
  @{path='Invoke-ESRound055PageDesignInstantiation.ps1';markers=@('model-evidence-incomplete','ai-provenance-missing')},
  @{path='Test-ESRound06AiDesignAgentReceipt.ps1';markers=@('round-specific-evidence-missing','after-hash-mismatch')},
  @{path='..\references\round-06-deep-design-html-contract.json';markers=@('requiredAgentContract','reducedMotionFallback','real page navigation')},
  @{path='Invoke-ESWebPageStudioDeepDesign.ps1';markers=@('BLOCKED_WEB_DEEP_DESIGN_MODEL_RESPONSE_REQUIRED','ModelResponsePath','AI-owned page instance')},
  @{path='Invoke-ESWebPageStudioAiMaterialization.ps1';markers=@('BLOCKED_WEB_MATERIALIZATION_AI_REVISION_REQUIRED','sourceRevisionReceipt','Copy-Item $artifactFile','changedFiles')},
  @{path='Invoke-ESWebPageStudioKernel.ps1';markers=@('BLOCKED_WEB_KERNEL_DYNAMIC_AI_RUNTIME_MISSING','runtime.mjs')},
  @{path='Convert-ESWebPageStudioRequest.ps1';markers=@('BLOCKED_WEB_CONTRACT_DESIGN_REQUIRED','BLOCKED_WEB_CONTRACT_AI_INFORMATION_ARCHITECTURE_REQUIRED','BLOCKED_WEB_CONTRACT_AI_COMPONENT_INVENTORY_REQUIRED','DesignSpecPath','ai-design')},
  @{path='Invoke-ESWebPageStudioPreflight.ps1';markers=@('BLOCKED_WEB_PREFLIGHT_AI_SOLUTION_REQUIRED','BLOCKED_WEB_PREFLIGHT_ROUND_PATHS_REQUIRED','AiSolutionPath','RoundPaths','providerRunId','aiAnalysis','toolAnalysis','aiEvidenceRequired','returnReceipt','evidence','promptPlan alone is not an AI solution')}
)
$results=@();foreach($c in $checks){$f=if($c.path -eq 'NORMALIZER'){Join-Path $root '.agents/skills/es-web-generation-round-05-capability-design/scripts/Convert-ESAbcCandidateToWebModelResponse.ps1'}elseif($c.path -in @('Invoke-ESWebPageStudioDeepDesign.ps1','Invoke-ESWebPageStudioAiMaterialization.ps1','Invoke-ESWebPageStudioKernel.ps1','Convert-ESWebPageStudioRequest.ps1','Invoke-ESWebPageStudioPreflight.ps1')){Join-Path (Join-Path $root 'ES/Automation/WebPageStudio') $c.path}else{Join-Path $PSScriptRoot $c.path};if(-not(Test-Path $f -PathType Leaf)){throw "static-replay-required-file-missing:$($c.path)"};$txt=Get-Content -Raw -Encoding UTF8 $f;$missing=@($c.markers|Where-Object{$txt.IndexOf($_,[StringComparison]::OrdinalIgnoreCase)-lt 0});$results+=[pscustomobject]@{path=$f.Substring($root.Length+1).Replace('\','/');status=if($missing.Count){'failed'}else{'passed'};missing=$missing};if($missing.Count){throw "static-replay-gate-marker-missing:$($c.path)"}}
$deep=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'ES/Automation/WebPageStudio/Invoke-ESWebPageStudioDeepDesign.ps1')
if($deep -match 'repository-overview|code-browser|issue-triage|\$isGithub\s*=') { throw 'static-replay-gate-hardcoded-page-template-detected' }
$converter=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'ES/Automation/WebPageStudio/Convert-ESWebPageStudioRequest.ps1')
if($converter -match 'dashboard-intro|dashboard-metrics|marketing-hero|#75e1d1') { throw 'static-replay-gate-converter-fixed-layout-fallback-detected' }
[pscustomobject]@{status='passed';skill='es-web-generation-round-06-deep-design-html';cases=@($m.cases).Count;gateChecks=$results;runtimeStatus='runtime-not-run';claimsNotProven=@('real AI provider response','browser rendering','visual quality','performance','Unity','network','release')}
