[CmdletBinding()]param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$rel='ES/Automation/Contracts/es-abcd-capability-receipt-v1.schema.json';$content='{}'
$candidateEnvelope=[ordered]@{schemaVersion=1;status='candidate';candidateSetHash=('e'*64);generationMode='engineering';candidates=@([ordered]@{candidateId='codex-candidate-1';proposedChanges=@([ordered]@{path=$rel;afterContent=$content;changeId='bridge-noop'})})}
$envPath=Join-Path $root 'ES/Output/StaticReplay/es-codex-candidate-envelope-test.json';[IO.File]::WriteAllText($envPath,($candidateEnvelope|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false))
$bridge=Join-Path $root 'ES/Automation/ABCD/Invoke-ESCodexCandidateThroughABCD.ps1';$output=& $bridge -ProjectRoot $root -CandidateEnvelopePath $envPath -CandidateId 'codex-candidate-1' -CurrentHead ('a'*40) -AuthorizationRef 'user:test' -Scenario DesignChange -AllowedWriteScopes @('ES/Automation/Contracts') -SourceFiles @($rel)|ConvertFrom-Json
$passed=([string]$output.status -ceq 'candidate-only' -and [string]$output.result.status -ceq 'candidate-only' -and -not [bool]$output.result.effects.writesAllowed)
[pscustomobject]@{status=if($passed){'passed'}else{'failed'};bridgeStatus=$output.status;planStatus=$output.result.status;operationCount=$output.result.operationCount;writesAllowed=$output.result.effects.writesAllowed}
if(-not $passed){exit 1}
