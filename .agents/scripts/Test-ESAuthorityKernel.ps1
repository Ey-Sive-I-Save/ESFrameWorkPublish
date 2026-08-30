$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$resolver=& (Join-Path $PSScriptRoot 'Resolve-ESSuperSemantics.ps1') -PromptText '帮我包装' -ProjectRoot $root | Out-String
$kernel=Join-Path $PSScriptRoot 'Resolve-ESAuthorityKernel.ps1'
$a=(& $kernel -ResolverReceipt $resolver | ConvertFrom-Json)
if($a.decisionStatus -ne 'ClaimCap' -or $a.completionAccepted){throw 'missing host receipt must claim-cap'}
$b=(& $kernel -ResolverReceipt $resolver -HostConsumerId 'codex-host' -ConsumedAtUtc '2026-08-29T00:00:00Z' | ConvertFrom-Json)
if($b.decisionStatus -ne 'Accepted' -or -not $b.completionAccepted){throw 'host receipt should accept'}
$m=(& $kernel -ResolverReceipt $resolver -HostConsumerId 'codex-host' -ConsumedAtUtc '2026-08-29T00:00:00Z' -HostResolverReceiptHash ('0'*64) | ConvertFrom-Json)
if($m.decisionStatus -ne 'Blocked'){throw 'hash mismatch must block'}
$bad='{"input":{"rawPrompt":"x"},"executionIntent":"none","canExecute":false}'
$c=(& $kernel -ResolverReceipt $bad | ConvertFrom-Json)
if($c.decisionStatus -ne 'ClaimCap' -or $c.completionAccepted){throw 'missing core fields must claim-cap without blocking'}
'passed: host receipt, no-receipt claim-cap, hash mismatch blocked, and missing-core claim-cap'
