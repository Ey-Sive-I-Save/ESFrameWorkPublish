[CmdletBinding()]
param(
  [Parameter(ParameterSetName='Evidence',Mandatory=$true)][string]$EvidenceInputPath,
  [Parameter(ParameterSetName='Transcript',Mandatory=$true)][string]$SessionPath,
  [Parameter(ParameterSetName='SessionId',Mandatory=$true)][Guid]$SessionId,
  [Parameter(ParameterSetName='Transcript')][Parameter(ParameterSetName='SessionId')][switch]$AllowWrites,
  [Parameter(ParameterSetName='Transcript')][Parameter(ParameterSetName='SessionId')][switch]$AllowRuntime,
  [Parameter(ParameterSetName='Transcript')][Parameter(ParameterSetName='SessionId')][switch]$RuntimeRequired,
  [Parameter(ParameterSetName='Transcript')][Parameter(ParameterSetName='SessionId')][ValidateRange(1,100000)][int]$MaxTextChars=4000,
  [string]$ReportPath
)

$ErrorActionPreference='Stop'
$base=Split-Path $PSScriptRoot -Parent
$effectiveEvidencePath=$EvidenceInputPath
$temporaryEvidence=$null
$resolvedSessionPath=$SessionPath
try {
  if($PSCmdlet.ParameterSetName -eq 'SessionId') {
    $codexHome=if([string]::IsNullOrWhiteSpace($env:CODEX_HOME)){Join-Path $env:USERPROFILE '.codex'}else{[IO.Path]::GetFullPath($env:CODEX_HOME)}
    $sessionRoot=Join-Path $codexHome 'sessions'
    if(-not (Test-Path -LiteralPath $sessionRoot -PathType Container)){throw "Codex session root not found: $sessionRoot"}
    $matches=@()
    foreach($candidate in Get-ChildItem -LiteralPath $sessionRoot -Filter 'rollout-*.jsonl' -File -Recurse -ErrorAction Stop) {
      try {
        $stream=[IO.File]::Open($candidate.FullName,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
        $reader=[IO.StreamReader]::new($stream,[Text.UTF8Encoding]::new($false,$true),$true)
        try {$first=$reader.ReadLine()} finally {$reader.Dispose();$stream.Dispose()}
        if([string]::IsNullOrWhiteSpace($first)){continue}
        $meta=$first|ConvertFrom-Json
        $payload=$meta.payload
        if([string]$payload.session_id -eq $SessionId.ToString() -or [string]$payload.id -eq $SessionId.ToString()){$matches+=[pscustomobject]@{path=$candidate.FullName;lastWriteUtc=$candidate.LastWriteTimeUtc}}
      } catch { continue }
    }
    if($matches.Count -eq 0){throw "Exact Codex session resolution found no rollout for $SessionId"}
    $orderedMatches=@($matches|Sort-Object lastWriteUtc -Descending)
    if($orderedMatches.Count -gt 1 -and $orderedMatches[0].lastWriteUtc -eq $orderedMatches[1].lastWriteUtc){throw "Exact Codex session resolution has a latest-snapshot tie for $SessionId"}
    $resolvedSessionPath=[string]$orderedMatches[0].path
  }
  if($PSCmdlet.ParameterSetName -eq 'Transcript') {
    $resolvedSessionPath=$SessionPath
  }
  if($PSCmdlet.ParameterSetName -in @('Transcript','SessionId')) {
    $temporaryEvidence=[IO.Path]::GetTempFileName()
    & (Join-Path $PSScriptRoot 'Convert-CodexTranscriptToEvidence.ps1') -SessionPath $resolvedSessionPath -OutputPath $temporaryEvidence -MaxTextChars $MaxTextChars -AllowWrites:$AllowWrites -AllowRuntime:$AllowRuntime -RuntimeRequired:$RuntimeRequired | Out-Null
    $effectiveEvidencePath=$temporaryEvidence
  }
  $assessment=& (Join-Path $PSScriptRoot 'Invoke-ESInteractionEvidenceAssessment.ps1') -InputPath $effectiveEvidencePath | ConvertFrom-Json
}
finally {
  if($temporaryEvidence -and (Test-Path -LiteralPath $temporaryEvidence)) { Remove-Item -LiteralPath $temporaryEvidence -Force -ErrorAction SilentlyContinue }
}
$risk=@($assessment.findings)
$status=[string]$assessment.status
$closeout=[ordered]@{
  schemaVersion=1
  outputMode='evidence-first-closeout'
  status=$status
  score=$assessment.score
  evidence=$assessment.evidence
  observationMetrics=$assessment.observationMetrics
  writeTargetHints=@($assessment.writeTargetHints)
  writeTargetResolution=@($assessment.writeTargetResolution)
  observed=$assessment.observed
  correctionEvidence=@($assessment.correctionEvidence)
  correctionState=[string]$assessment.correctionState
  feedbackLoop=$assessment.feedbackLoop
  diagnosticCodes=@($assessment.diagnosticCodes)
  source=if($PSCmdlet.ParameterSetName -eq 'SessionId'){'exact-session-id-latest-snapshot'}else{'explicit-input'}
  sessionPath=if($PSCmdlet.ParameterSetName -in @('Transcript','SessionId')){$resolvedSessionPath}else{$null}
  findings=$risk
  claimsNotProven=@($assessment.claimsNotProven)
  nextAction=if($status -eq 'unverifiable'){'collect-missing-observations'}elseif($status -eq 'misaligned'){'review-findings-before-claiming-completion'}elseif($status -eq 'partial' -and [string]$assessment.correctionState -eq 'followup-observed'){'await-user-confirmation'}elseif($status -eq 'partial'){'address-findings-and-recheck'}else{'report-observed-scope-only'}
  nonClaims=@('No numeric quality claim is made','Completion is not inferred from assistant text alone','Runtime/release claims require their own evidence')
}
$json=$closeout|ConvertTo-Json -Depth 10
if($ReportPath){$full=[IO.Path]::GetFullPath($ReportPath);$dir=Split-Path $full -Parent;if(!(Test-Path $dir)){New-Item -ItemType Directory -Path $dir -Force|Out-Null};[IO.File]::WriteAllText($full,$json,(New-Object Text.UTF8Encoding($false)))}
$json
