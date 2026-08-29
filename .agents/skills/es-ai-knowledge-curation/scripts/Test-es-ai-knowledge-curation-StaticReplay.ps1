[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,[string]$ReportPath='ES/Output/StaticReplay/es-ai-knowledge-curation.json')
$shared=Join-Path $PSScriptRoot '..\..\es-static-deep-replay\scripts\Invoke-ESStaticDeepReplay.ps1'
& $shared -ProjectRoot $ProjectRoot -ManifestPath '.agents/skills/es-ai-knowledge-curation/static-replay.manifest.json' -ReportPath $ReportPath
$exitCode=$LASTEXITCODE
if($exitCode -eq 0){
  $report=Get-Content -LiteralPath (Join-Path $ProjectRoot $ReportPath) -Raw -Encoding UTF8 | ConvertFrom-Json
  $skillFile=Join-Path $ProjectRoot '.agents/skills/es-ai-knowledge-curation/SKILL.md'
  $govFile=Join-Path $ProjectRoot '.agents/skills/es-ai-knowledge-curation/governance.json'
  $validatorFile=Join-Path $ProjectRoot '.agents/skills/es-skill-validator/scripts/Invoke-ESSkillValidation.ps1'
  $skillHash=(Get-FileHash -LiteralPath $skillFile -Algorithm SHA256).Hash.ToLowerInvariant()
  $governanceHash=(Get-FileHash -LiteralPath $govFile -Algorithm SHA256).Hash.ToLowerInvariant()
  $validatorHash=(Get-FileHash -LiteralPath $validatorFile -Algorithm SHA256).Hash.ToLowerInvariant()
  foreach($case in @('positive','invalid-input','hash-change-cache-invalidation')){
    $receiptPath="ES/Output/es-ai-knowledge-curation-$case-Receipt.json"
    $receipt=[ordered]@{schemaVersion=1;evidenceContractId=$report.evidenceContractId;evidenceContractHash=$report.evidenceContractHash;skillName='es-ai-knowledge-curation';case=$case;status='passed';evidenceLevel='S1';receiptPath=$receiptPath;sourceRefs=@($report.sourceRefs);sourceRefHashes=$report.sourceRefHashes;toolId='es-ai-knowledge-curation-static-replay';unityVersion='not-run';capturedUtc=$report.capturedUtc;authorizationKind='read-only';planHash=$report.planHash;profile='StaticReview';responsibilityProfile='knowledge';staticStatus='static-passed';runtimeStatus='runtime-not-run';overallVerdict='StaticDeepReplayComplete';claimsNotProven=@($report.claimsNotProven);skillHash=$skillHash;governanceHash=$governanceHash;validatorHash=$validatorHash}
    [IO.File]::WriteAllText((Join-Path $ProjectRoot $receiptPath),($receipt|ConvertTo-Json -Depth 12),(New-Object Text.UTF8Encoding($false)))
  }
}
exit $exitCode

