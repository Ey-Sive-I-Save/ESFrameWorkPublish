[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CandidateEnvelopePath,
    [Parameter(Mandatory)][string]$CandidateId,
    [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$CurrentHead,
    [Parameter(Mandatory)][string]$AuthorizationRef,
    [Parameter(Mandatory)][ValidateSet('DesignChange','RuntimeChange','DataMigration','ExternalSourceAdoption','PerformanceCritical','ReleaseCandidate')][string]$Scenario,
    [string[]]$SourceFiles=@(),
    [string[]]$AllowedWriteScopes=@(),
    [string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [string]$ReceiptPath=''
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
Import-Module (Join-Path $root 'ES/Automation/ABCD/ESABCDCapabilityDispatcher.psm1') -Force
$full=[IO.Path]::GetFullPath($CandidateEnvelopePath)
if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw 'ABCD_CODEX_CANDIDATE_ENVELOPE_MISSING'}
$envelope=Get-Content -LiteralPath $full -Raw -Encoding UTF8|ConvertFrom-Json
if([string]$envelope.status -cne 'candidate' -and [string]$envelope.Status -cne 'candidate'){throw 'ABCD_CODEX_CANDIDATE_STATUS_REQUIRED'}
$candidate=@($envelope.candidates|Where-Object{[string]$_.candidateId -ceq $CandidateId}|Select-Object -First 1)
if($candidate.Count -ne 1){throw 'ABCD_CODEX_CANDIDATE_ID_NOT_FOUND_OR_AMBIGUOUS'}
if(@($SourceFiles).Count -eq 0){$SourceFiles=@($candidate[0].proposedChanges|ForEach-Object{[string]$_.path}|Where-Object{$_})}
if(@($AllowedWriteScopes).Count -eq 0){throw 'ABCD_CODEX_ALLOWED_WRITE_SCOPE_REQUIRED'}
$ctx=[pscustomobject][ordered]@{scope=($AllowedWriteScopes -join ';');authorization=$AuthorizationRef;candidateEnvelope=$envelope;candId=$CandidateId;candidate=$candidate[0];scenario=$Scenario;currentHead=$CurrentHead.ToLowerInvariant();authorizationRef=$AuthorizationRef;sourceFiles=$SourceFiles;allowedWriteScopes=$AllowedWriteScopes;projectRoot=$root}
$result=Invoke-ESABCDBoundedPatchCandidateAction -Context $ctx
$receipt=[ordered]@{schemaVersion=1;contractId='es://automation/contracts/abcd/codex-candidate-bridge-receipt/v1';candidateEnvelopePath=$full;candidateId=$CandidateId;authorizationRef=$AuthorizationRef;result=$result;status='candidate-only';nonClaims=@('no-Apply','no-Git','no-Unity-runtime','no-release');capturedUtc=[DateTime]::UtcNow.ToString('o')}
if(-not [string]::IsNullOrWhiteSpace($ReceiptPath)){$out=[IO.Path]::GetFullPath($ReceiptPath);$dir=[IO.Path]::GetDirectoryName($out);if(-not(Test-Path -LiteralPath $dir)){New-Item -ItemType Directory -Force -Path $dir|Out-Null};[IO.File]::WriteAllText($out,($receipt|ConvertTo-Json -Depth 40),[Text.UTF8Encoding]::new($false));$receipt.receiptPath=$out}
$receipt|ConvertTo-Json -Depth 40
