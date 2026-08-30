[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
Import-Module (Join-Path $root 'ES/Automation/AI/ESAuthorityDecisionPolicy.psm1') -Force
$domains=@('ai-collaboration','game-logic','editor-tooling','release')
$policies=@($domains|ForEach-Object { Get-ESAuthorityDecisionPolicy -ProjectRoot $root -Domain $_ })
$cases=@(
 [pscustomobject]@{case='all-authority-domains-resolve';status=if($policies.Count -eq 4 -and @($policies|Where-Object {$_.contractHash -and $_.safeDefaultFields.Count -gt 0}).Count -eq 4){'passed'}else{'failed'}},
 [pscustomobject]@{case='collaboration-is-lenient';status=if(-not ($policies|Where-Object domain -eq 'ai-collaboration').strictOnUnresolved){'passed'}else{'failed'}},
 [pscustomobject]@{case='game-logic-is-strict';status=if(($policies|Where-Object domain -eq 'game-logic').strictOnUnresolved){'passed'}else{'failed'}},
 [pscustomobject]@{case='editor-tooling-is-strict';status=if(($policies|Where-Object domain -eq 'editor-tooling').strictOnUnresolved){'passed'}else{'failed'}},
 [pscustomobject]@{case='release-is-strict';status=if(($policies|Where-Object domain -eq 'release').strictOnUnresolved){'passed'}else{'failed'}}
)
$failed=@($cases|Where-Object status -eq 'failed')
[pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESAuthorityDecisionPolicy';status=if($failed.Count){'failed'}else{'passed'};caseCount=$cases.Count;passedCount=$cases.Count-$failed.Count;failedCount=$failed.Count;cases=$cases;contractHashes=@($policies|ForEach-Object {[pscustomobject]@{domain=$_.domain;hash=$_.contractHash}});runtimeStatus='runtime-not-run';claimsNotProven=@('consumer integration completeness','Unity runtime behavior')}|ConvertTo-Json -Depth 10
if($failed.Count){exit 1}
