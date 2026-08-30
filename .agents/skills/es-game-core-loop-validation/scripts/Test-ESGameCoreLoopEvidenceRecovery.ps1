[CmdletBinding()]
param([string]$SkillRoot = (Join-Path (Get-Location) '.agents/skills/es-game-core-loop-validation'))
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $SkillRoot '..\..\..')).Path
$tmp=Join-Path $SkillRoot '.tmp-recovery';New-Item -ItemType Directory -Force $tmp|Out-Null
try {
 Import-Module (Join-Path $root 'ES/Automation/ABCD/ESABCDEvidenceRecovery.psm1') -Force
 $h=('a'*64)
 $store=[pscustomobject]@{storeId='recovery-store';taskId='recovery-fixture';taskRevision=1;contextVersion=1;routePlanHash=$h;sourceScopeHash=$h;taskBindingRef=[pscustomobject]@{bindingId='binding';bindingHash=$h};authorizationRef='auth';maxRounds=2;attemptsPerRound=2;currentRound=0;attemptsUsed=@{};stopped=$false;events=[Collections.Generic.List[object]]::new();idempotency=@{};branches=@{'branch-a'=[pscustomobject]@{branchId='branch-a'}};audits=@{};selected=@{'branch-a'=[pscustomobject]@{decision='retry'}};selectedBranchId='branch-a';cycles=@{};verifications=@{};auditorRegistry=@{};receiptRegistry=@{};projectRoot=$null;requireImmutableSnapshots=$false;requireVerificationReceiptEntity=$false}
 $ev=Join-Path $tmp 'evidence.json';[IO.File]::WriteAllText($ev,'{"schemaVersion":1,"status":"candidate"}',[Text.UTF8Encoding]::new($false));$finding=Join-Path $tmp 'findings'
 $r=Invoke-ESABCDEvidenceRecovery -Store $store -EvidencePath $ev -FindingDirectory $finding
 if($r.status -ne 'normalized-and-retry' -or $r.claimLevel -ne 'claim-cap'){throw 'EVIDENCE_RECOVERY_EXPECTED_RETRY'}
 [ordered]@{status='passed';recoveryStatus=$r.status;claimLevel=$r.claimLevel;nextAction=$r.nextAction;runtimeStatus='runtime-not-run';deterministic=$true}|ConvertTo-Json
} finally {Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue}
