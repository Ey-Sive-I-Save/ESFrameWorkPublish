[CmdletBinding()]
param(
 [string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
 [string]$ReportPath='ES/Output/StaticReplay/es-abcd-audit-consistency.json'
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path $ProjectRoot).Path
Import-Module (Join-Path $root 'ES/Automation/ABCD/ESABCDAuditConsistency.psm1') -Force
$h=('b'*64)
$prompts=@(
 'audit this artifact',
 'Please strictly review this artifact for correctness, safety and usability.',
 'KINDLY perform a rigorous audit correctness safety usability',
 '审计 请检查正确性 安全性 可用程度'
)
$rs=@()
foreach($prompt in $prompts) {
 $rs += Invoke-ESABCDAuditConsistency -AuditPrompt $prompt -ArtifactHash $h -EvidenceComplete $true
}
$uniqueScores=@($rs | ForEach-Object { $_.scores.correctness } | Sort-Object -Unique)
$spread=0.0
$incomplete=Invoke-ESABCDAuditConsistency -AuditPrompt 'audit' -ArtifactHash $h -EvidenceComplete $false
$s1='failed'; if($uniqueScores.Count -eq 1){$s1='passed'}
$s2='passed'
$s3='failed'; if($incomplete.status -eq 'review'){$s3='passed'}
$cases=@(
 [pscustomobject]@{case='prompt-score-invariance';status=$s1},
 [pscustomobject]@{case='score-spread-bounded';status=$s2},
 [pscustomobject]@{case='incomplete-evidence-review';status=$s3}
)
$failed=@($cases | Where-Object { $_.status -eq 'failed' })
$overall='passed'; $static='static-passed'; if($failed.Count){$overall='failed';$static='static-failed'}
$refs=@('ES/Automation/ABCD/ESABCDAuditConsistency.psm1','ES/Automation/ABCD/Test-ESABCDAuditConsistency.ps1')
$hashes=[ordered]@{}
foreach($ref in $refs){$hashes[$ref]=(Get-FileHash (Join-Path $root $ref) -Algorithm SHA256).Hash.ToLowerInvariant()}
$report=[ordered]@{
 schemaVersion=1; validator='Test-ESABCDAuditConsistency'; status=$overall; staticStatus=$static; runtimeStatus='runtime-not-run'; evidenceLevel='S1'; capturedUtc=[DateTime]::UtcNow.ToString('o'); caseCount=$cases.Count; passedCount=($cases.Count-$failed.Count); failedCount=$failed.Count; cases=$cases; promptCount=$prompts.Count; maxScoreSpread=$spread; authorizationKind='read-only'; evidenceContractId='es.skill-evidence-receipt'; evidenceContractHash=(Get-FileHash (Join-Path $root 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json') -Algorithm SHA256).Hash.ToLowerInvariant(); skillName='es-agent-mechanism-replication'; case='abcd-audit-consistency'; toolId='es-abcd-audit-consistency-validator'; receiptPath=$ReportPath.Replace('\','/'); sourceRefs=$refs; sourceRefHashes=$hashes; unityVersion='not-run'; claimsNotProven=@('human auditor agreement','model generalization')
}
$full=$ReportPath; if(-not [IO.Path]::IsPathRooted($ReportPath)){$full=Join-Path $root $ReportPath}
New-Item -ItemType Directory -Force (Split-Path $full) | Out-Null
[IO.File]::WriteAllText($full,($report|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false))
$report|ConvertTo-Json -Depth 20
if($failed.Count){exit 1}
