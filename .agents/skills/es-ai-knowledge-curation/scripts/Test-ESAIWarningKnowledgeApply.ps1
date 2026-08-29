[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$ProjectRoot,
    [Parameter(Mandatory = $true)] [string]$CandidatePath
)

$ErrorActionPreference = 'Stop'
$utf8 = [Text.UTF8Encoding]::new($false, $true)
$root = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)

function Resolve-ProjectPath([string]$p, [string]$label) {
    if ([string]::IsNullOrWhiteSpace($p) -or [IO.Path]::IsPathRooted($p)) { throw "${label}_PATH_NOT_PROJECT_RELATIVE" }
    $full = [IO.Path]::GetFullPath([IO.Path]::Combine($root, $p))
    if (-not $full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "${label}_PATH_OUTSIDE_PROJECT" }
    return $full
}
function Hash-Bytes([byte[]]$b) {
    $sha = [Security.Cryptography.SHA256]::Create(); try { return ([BitConverter]::ToString($sha.ComputeHash($b))).Replace('-', '').ToLowerInvariant() } finally { $sha.Dispose() }
}
function Hash-File([string]$p) { return Hash-Bytes ([IO.File]::ReadAllBytes($p)) }
function Body-Hash([string]$text) {
    $n = $text -replace "`r`n", "`n" -replace "`r", "`n"
    $lines = @($n -split "`n" | ForEach-Object { if ($_ -match '(?i)^\s*`?EntryBodyHash`?\s*\p{P}') { return $null }; $_.TrimEnd(' ', "`t") } | Where-Object { $null -ne $_ })
    while ($lines.Count -gt 0 -and [string]::IsNullOrWhiteSpace($lines[$lines.Count - 1])) { if ($lines.Count -eq 1) { $lines = @(); break }; $lines = @($lines[0..($lines.Count - 2)]) }
    $body = if ($lines.Count -eq 0) { "`n" } else { ($lines -join "`n") + "`n" }
    return Hash-Bytes ([Text.Encoding]::UTF8.GetBytes($body))
}
function Add-Finding([Collections.Generic.List[object]]$list, [string]$code, [string]$message) { $list.Add([ordered]@{ code = $code; message = $message }) }

$findings = [Collections.Generic.List[object]]::new()
$candidateFull = Resolve-ProjectPath $CandidatePath 'CANDIDATE'
if (-not (Test-Path -LiteralPath $candidateFull -PathType Leaf)) { throw 'CANDIDATE_NOT_FOUND' }
$candidate = $utf8.GetString([IO.File]::ReadAllBytes($candidateFull)) | ConvertFrom-Json
if ($candidate.replay.candidateOnly -ne $true -or $candidate.replay.applyRequired -ne $true) { Add-Finding $findings 'APPLY_BOUNDARY_INVALID' 'Candidate is not explicitly candidate-only/apply-required.' }
$snap = $candidate.sourceSnapshot
try { $warningFull = Resolve-ProjectPath ([string]$snap.warningPath) 'WARNING' } catch { Add-Finding $findings 'WARNING_PATH_INVALID' $_.Exception.Message; $warningFull = $null }
if ($warningFull -and -not (Test-Path -LiteralPath $warningFull -PathType Leaf)) { Add-Finding $findings 'WARNING_NOT_FOUND' ([string]$snap.warningPath) }
if ($warningFull -and (Test-Path -LiteralPath $warningFull -PathType Leaf) -and (Hash-File $warningFull) -ne [string]$snap.warningHash) { Add-Finding $findings 'WARNING_HASH_DRIFT' 'Warning changed after candidate snapshot; Apply must stop.' }

$target = [string]$candidate.proposedEntry.targetPath
try { $entryFull = Resolve-ProjectPath $target 'ENTRY' } catch { Add-Finding $findings 'ENTRY_PATH_INVALID' $_.Exception.Message; $entryFull = $null }
if ($candidate.match.decision -eq 'new') {
    Add-Finding $findings 'ENTRY_CONTENT_REQUIRED' 'candidate-created has no proposed Markdown body; Apply cannot invent Knowledge content.'
}
elseif ($entryFull -and -not (Test-Path -LiteralPath $entryFull -PathType Leaf)) {
    Add-Finding $findings 'ENTRY_NOT_FOUND' 'Attached candidate target entry is missing.'
}
elseif ($entryFull -and (Test-Path -LiteralPath $entryFull -PathType Leaf)) {
    $bodyHash = Body-Hash ($utf8.GetString([IO.File]::ReadAllBytes($entryFull)))
    $expected = [string]$candidate.proposedEntry.expectedHashes.entryBodyHash
    if ($expected -and $bodyHash -ne $expected) { Add-Finding $findings 'ENTRY_BODY_HASH_DRIFT' 'Target Knowledge body changed after candidate snapshot.' }
}

$state = if ($findings.Count -eq 0) { 'ready-noop-attached' } elseif (@($findings | Where-Object code -in @('WARNING_HASH_DRIFT','ENTRY_BODY_HASH_DRIFT')).Count -gt 0) { 'stale/retry-required' } else { 'blocked' }
[ordered]@{
    schemaVersion = 1
    recordType = 'AIWarningKnowledgeApplyPreflight'
    state = $state
    candidatePath = $CandidatePath.Replace('\','/')
    candidateId = [string]$candidate.candidateId
    candidateHash = Hash-File $candidateFull
    transactionExecuted = $false
    formalRegistration = 'not-run'
    findings = @($findings | Sort-Object code)
    nonClaims = @('This preflight is read-only and never writes Knowledge, KnowledgeIndex, ledgers or formal receipts.', 'A ready result means only that an attached candidate has matching CAS inputs; it is not formal Apply acceptance.', 'candidate-created is blocked until an explicit Markdown body and index patch are supplied.')
} | ConvertTo-Json -Depth 20
