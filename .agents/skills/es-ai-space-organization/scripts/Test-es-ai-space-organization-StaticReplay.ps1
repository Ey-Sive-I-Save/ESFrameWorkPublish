[CmdletBinding()]
param(
  [string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
  [string]$ReportPath='ES/Output/StaticReplay/es-ai-space-organization.json'
)
$shared=Join-Path $PSScriptRoot '..\..\es-static-deep-replay\scripts\Invoke-ESStaticDeepReplay.ps1'
$authority=Join-Path $PSScriptRoot 'Test-ESAISpaceAuthority.ps1'
& powershell -NoProfile -File $authority -ProjectRoot $ProjectRoot
$authorityExitCode=$LASTEXITCODE
if($authorityExitCode -ne 0){exit $authorityExitCode}
& $shared -ProjectRoot $ProjectRoot -ManifestPath '.agents/skills/es-ai-space-organization/static-replay.manifest.json' -ReportPath $ReportPath
$exitCode=$LASTEXITCODE
if($exitCode -eq 0){
  $report=Get-Content -LiteralPath (Join-Path $ProjectRoot $ReportPath) -Raw -Encoding UTF8 | ConvertFrom-Json
  $skillFile=Join-Path $ProjectRoot '.agents/skills/es-ai-space-organization/SKILL.md'
  $govFile=Join-Path $ProjectRoot '.agents/skills/es-ai-space-organization/governance.json'
  $validatorFile=Join-Path $ProjectRoot '.agents/skills/es-skill-validator/scripts/Invoke-ESSkillValidation.ps1'
  $skillHash=(Get-FileHash -LiteralPath $skillFile -Algorithm SHA256).Hash.ToLowerInvariant()
  $governanceHash=(Get-FileHash -LiteralPath $govFile -Algorithm SHA256).Hash.ToLowerInvariant()
  $validatorHash=(Get-FileHash -LiteralPath $validatorFile -Algorithm SHA256).Hash.ToLowerInvariant()
  foreach($case in @('positive','invalid-input','denied-expansion')){
    $receiptPath="ES/Output/es-ai-space-organization-$case-Receipt.json"
    $r=[ordered]@{schemaVersion=1;evidenceContractId=$report.evidenceContractId;evidenceContractHash=$report.evidenceContractHash;skillName='es-ai-space-organization';case=$case;status='passed';evidenceLevel='S1';receiptPath=$receiptPath;sourceRefs=@($report.sourceRefs);sourceRefHashes=$report.sourceRefHashes;toolId='es-ai-space-organization-static-replay';unityVersion='not-run';capturedUtc=$report.capturedUtc;authorizationKind='read-only';planHash=$report.planHash;profile='StaticReview';responsibilityProfile='governance';staticStatus='static-passed';runtimeStatus='runtime-not-run';overallVerdict='StaticDeepReplayComplete';claimsNotProven=@($report.claimsNotProven);skillHash=$skillHash;governanceHash=$governanceHash;validatorHash=$validatorHash}
    [IO.File]::WriteAllText((Join-Path $ProjectRoot $receiptPath),($r|ConvertTo-Json -Depth 12),(New-Object Text.UTF8Encoding($false)))
  }
}
exit $exitCode
