[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,[string]$ReportPath='ES/Output/StaticReplay/es-abcd-domain-policy.json')
$ErrorActionPreference='Stop';$root=(Resolve-Path $ProjectRoot).Path
Import-Module (Join-Path $root 'ES/Automation/ABCD/ESABCDAuthorityKernel.psm1') -Force
$e=[pscustomobject]@{status='passed'}
$collab=Resolve-ESABCDAuthorityDecision -Mode core-high-risk -Domain ai-collaboration -Evidence $e -MissingFields @('capturedUtc')
$collabGap=Resolve-ESABCDAuthorityDecision -Mode core-high-risk -Domain ai-collaboration -Evidence $e -MissingFields @('artifactHash')
$logic=Resolve-ESABCDAuthorityDecision -Mode core-high-risk -Domain game-logic -Evidence $e -MissingFields @('stateTransition','ownership')
$logicSafe=Resolve-ESABCDAuthorityDecision -Mode core-high-risk -Domain game-logic -Evidence $e -MissingFields @('capturedUtc')
$editor=Resolve-ESABCDAuthorityDecision -Mode core-high-risk -Domain editor-tooling -Evidence $e -MissingFields @('assetOwnership')
$release=Resolve-ESABCDAuthorityDecision -Mode core-high-risk -Domain release -Evidence $e -MissingFields @('artifactHash')
$policyContract=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'ES/Automation/Contracts/es-authority-ai-decision-policy-v1.json')|ConvertFrom-Json
$policyDomains=@($policyContract.domains.PSObject.Properties.Name)
$cases=@(
 [pscustomobject]@{case='collaboration-safe-default-continues';status=if($collab.status -eq 'accepted' -and $collab.mechanismsContinue -and $collab.normalizedFields.capturedUtc){'passed'}else{'failed'}},
 [pscustomobject]@{case='collaboration-evidence-gap-claim-cap';status=if($collabGap.status -eq 'claim-cap' -and $collabGap.mechanismsContinue -and $collabGap.nextAction -eq 'replan'){'passed'}else{'failed'}},
 [pscustomobject]@{case='game-logic-core-gap-blocks';status=if($logic.status -eq 'blocked' -and -not $logic.mechanismsContinue -and $logic.defect.defectDetected -and -not $logic.defect.suppressed){'passed'}else{'failed'}},
 [pscustomobject]@{case='game-logic-metadata-defaults';status=if($logicSafe.status -eq 'accepted' -and $logicSafe.mechanismsContinue){'passed'}else{'failed'}},
 [pscustomobject]@{case='defect-is-explicit';status=if($logic.reasonCode -eq 'UNRESOLVED_CORE_EVIDENCE' -and $logic.nextAction -eq 'stop-and-report'){'passed'}else{'failed'}},
 [pscustomobject]@{case='editor-tooling-strict';status=if($editor.status -eq 'blocked' -and -not $editor.mechanismsContinue){'passed'}else{'failed'}},
 [pscustomobject]@{case='release-strict';status=if($release.status -eq 'blocked' -and -not $release.mechanismsContinue){'passed'}else{'failed'}},
 [pscustomobject]@{case='policy-contract-four-domains';status=if((( @('ai-collaboration','game-logic','editor-tooling','release')|Where-Object {$_ -notin $policyDomains}|Measure-Object).Count) -eq 0){'passed'}else{'failed'}},
 [pscustomobject]@{case='policy-projection-is-visible';status=if($logic.domainPolicy.strictOnUnresolved -and $collab.domainPolicy.safeDefaultFields.Count -gt 0){'passed'}else{'failed'}}
)
$failed=@($cases|Where-Object {$_.status -eq 'failed'});$overall='passed';if($failed.Count){$overall='failed'}
$refs=@('ES/Automation/ABCD/ESABCDAuthorityKernel.psm1','ES/Automation/AI/ESAuthorityDecisionPolicy.psm1','ES/Automation/ABCD/ESAuthorityDecisionPolicy.psm1','ES/Automation/ABCD/Test-ESABCDDomainPolicy.ps1','ES/Automation/Contracts/es-authority-ai-decision-policy-v1.json');$hashes=[ordered]@{};foreach($ref in $refs){$hashes[$ref]=(Get-FileHash (Join-Path $root $ref) -Algorithm SHA256).Hash.ToLowerInvariant()}
$report=[ordered]@{schemaVersion=1;validator='Test-ESABCDDomainPolicy';status=$overall;staticStatus=if($failed.Count){'static-failed'}else{'static-passed'};runtimeStatus='runtime-not-run';evidenceLevel='S1';capturedUtc=[DateTime]::UtcNow.ToString('o');caseCount=$cases.Count;passedCount=($cases.Count-$failed.Count);failedCount=$failed.Count;cases=$cases;authorizationKind='read-only';sourceRefs=$refs;sourceRefHashes=$hashes;evidenceContractId='es.skill-evidence-receipt';evidenceContractHash=(Get-FileHash (Join-Path $root 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json') -Algorithm SHA256).Hash.ToLowerInvariant();skillName='es-ai-abc-core';case='abcd-domain-policy';toolId='es-abcd-domain-policy-validator';receiptPath=$ReportPath.Replace('\','/');unityVersion='not-run';claimsNotProven=@('domain-classifier completeness','Unity runtime behavior')}
$out=Join-Path $root $ReportPath;New-Item -ItemType Directory -Force (Split-Path $out)|Out-Null;[IO.File]::WriteAllText($out,($report|ConvertTo-Json -Depth 30),[Text.UTF8Encoding]::new($false));$report|ConvertTo-Json -Depth 30;if($failed.Count){exit 1}
