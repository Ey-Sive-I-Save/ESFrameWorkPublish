[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$TaskReceiptPath,
    [Parameter(Mandatory=$true)][string[]]$EntryPaths,
    [string]$OutputPath = 'ES/Output/WebPageStudio/bootstrap/round-04-knowledge-route.json',
    [string]$AiEvidencePath = ''
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
function Read-StrictJson([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "input-not-found: $Path" }
    $raw = [Text.UTF8Encoding]::new($false, $true).GetString([IO.File]::ReadAllBytes((Resolve-Path $Path).Path))
    $raw | ConvertFrom-Json
}
function Hash-Object([object]$Value) {
    $json = $Value | ConvertTo-Json -Depth 30 -Compress
    $sha = [Security.Cryptography.SHA256]::Create(); try { ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($json)))).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose() }
}
function Read-UsableKnowledge([string]$FullPath,[string]$RelativePath) {
    $raw = [Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($FullPath))
    $idLine=($raw -split "`n" | Where-Object { $_ -like '*KnowledgeId*' } | Select-Object -First 1)
    $authorityLine=($raw -split "`n" | Where-Object { $_ -like '*Authority*' } | Select-Object -First 1)
    [ordered]@{ projectRelativePath=$RelativePath; title=([regex]::Match($raw,'(?m)^#\s+(.+?)\s*$')).Groups[1].Value; knowledgeId=(($idLine -split '`')[3]); authority=(($authorityLine -split '`')[3]); content=$raw }
}
$task = Read-StrictJson $TaskReceiptPath
if ([string]::IsNullOrWhiteSpace($AiEvidencePath)) { throw 'blocked.round-04.ai-evidence-required' }
$aiEvidenceFull = if ([IO.Path]::IsPathRooted($AiEvidencePath)) { [IO.Path]::GetFullPath($AiEvidencePath) } else { [IO.Path]::GetFullPath((Join-Path $projectRoot $AiEvidencePath)) }
if (-not $aiEvidenceFull.StartsWith($projectRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $aiEvidenceFull -PathType Leaf)) { throw 'blocked.round-04.ai-evidence-missing' }
$aiEvidence = Read-StrictJson $aiEvidenceFull
if ([string]$aiEvidence.taskContextHash -cne [string]$task.taskContextHash -or [string]$aiEvidence.sourceScopeHash -cne [string]$task.sourceScopeHash) { throw 'blocked.round-04.ai-evidence-task-hash-mismatch' }
if (@($EntryPaths | Where-Object { @($aiEvidence.entryPaths) -notcontains $_ }).Count -gt 0) { throw 'blocked.round-04.ai-evidence-entry-set-mismatch' }
foreach ($field in @('aiAnalysis','execution','knowledgeRationale','returnReceipt')) { if ($null -eq $aiEvidence.PSObject.Properties[$field] -or [string]::IsNullOrWhiteSpace([string]$aiEvidence.$field)) { throw "blocked.round-04.ai-evidence-incomplete:$field" } }
if(([string]$aiEvidence.aiAnalysis).Trim().Length -lt 80 -or ([string]$aiEvidence.execution).Trim().Length -lt 40 -or ([string]$aiEvidence.knowledgeRationale).Trim().Length -lt 80){throw 'blocked.round-04.ai-evidence-too-shallow'}
if ([string]$aiEvidence.aiAnalysis -match '(?i)select the minimum Knowledge set|preserve validator findings') { throw 'blocked.round-04.synthetic-ai-evidence' }
if ([string]$task.recordType -cne 'TaskContextCreationReceipt' -or [string]$task.roundId -cne 'web-generation-round-03' -or [string]$task.status -cne 'accepted') { throw 'blocked.round-04.missing-task' }
if ([string]$task.sourceScopeHash -notmatch '^[a-f0-9]{64}$' -or [string]$task.taskContextHash -notmatch '^[a-f0-9]{64}$') { throw 'blocked.round-04.task-context-hash-missing' }
if ([string]$task.taskId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,80}$' -or [int]$task.taskRevision -lt 1 -or [int]$task.contextVersion -lt 1) { throw 'blocked.round-04.invalid-task-identity' }
if (@($EntryPaths).Count -lt 2 -or @($EntryPaths).Count -gt 4) { throw 'blocked.round-04.route-coverage-insufficient' }
$validator = Join-Path $projectRoot '.agents\skills\es-knowledge-validator\scripts\Invoke-ESKnowledgeValidation.ps1'
$validations = @()
foreach ($entry in @($EntryPaths)) {
    $full = [IO.Path]::GetFullPath((Join-Path $projectRoot $entry))
    if (-not $full.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "blocked.round-04.unsafe-path: $entry" }
    $result = & powershell -NoProfile -File $validator -ProjectRoot $projectRoot -Mode Entry -EntryPath $entry | ConvertFrom-Json
    $hard = @($result.findings | Where-Object { [string]$_.code -match 'UNSAFE|NOT_FOUND|DUPLICATE_ID|INDEX_BINDING_COUNT|ENTRY_FIELD_COUNT|PARSE' })
    $entryStatus = if ($hard.Count -gt 0) { 'blocked' } elseif ([string]$result.status -in @('passed','static-passed')) { 'accepted' } else { 'partial' }
    $validations += [pscustomobject]@{ entryPath=$entry.Replace('\','/'); status=$entryStatus; usableKnowledge=(Read-UsableKnowledge $full $entry.Replace('\','/')); findings=@($result.findings); reportHash=(Hash-Object $result); nonClaims=@('SourceRefs/ContentHash freshness is unproven when status=partial','usable content is navigation input, not authority') }
}
$contentChars=(@($validations | % { [string]$_.usableKnowledge.content } | % Length) | Measure-Object -Sum).Sum
if($contentChars -lt 1000){ throw 'blocked.round-04.knowledge-content-insufficient' }
$failed = @($validations | Where-Object { [string]$_.status -eq 'blocked' })
$partial = @($validations | Where-Object { [string]$_.status -eq 'partial' })
$status = if ($failed.Count -gt 0) { 'blocked' } elseif ($partial.Count -gt 0) { 'partial' } else { 'accepted' }
$routeHash = Hash-Object ([ordered]@{taskId=[string]$task.taskId;taskRevision=[int]$task.taskRevision;entries=@($validations | ForEach-Object { $_.entryPath });validationHashes=@($validations | ForEach-Object { $_.reportHash })})
$outFull = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputPath)); $parent=Split-Path -Parent $outFull
if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
$receipt = [ordered]@{ schemaVersion=1; recordType='KnowledgeRouteReceipt'; roundId='web-generation-round-04'; stageId='knowledge-route'; status=$status; taskId=$task.taskId; taskRevision=$task.taskRevision; contextVersion=$task.contextVersion; sourceScopeHash=if($task.PSObject.Properties['sourceScopeHash']){$task.sourceScopeHash}else{$null}; routePlanHash=if($task.PSObject.Properties['routePlanHash']){$task.routePlanHash}else{$null}; taskContextHash=if($task.PSObject.Properties['taskContextHash']){$task.taskContextHash}else{$null}; selectedEntries=@($validations); routeHash=$routeHash; aiAnalysis=[string]$aiEvidence.aiAnalysis; execution=[string]$aiEvidence.execution; decision=if($status -eq 'accepted'){'accepted-for-capability-design'}else{'blocked-knowledge-closure'}; returnReceipt=[ordered]@{status=$status;aiReturn=$aiEvidence.returnReceipt;nextRound='web-generation-round-05-capability-design'}; nonClaims=@('not Knowledge repair','not design','not HTML generation','not Runtime/network/Unity/release') }
$json = $receipt | ConvertTo-Json -Depth 40
[IO.File]::WriteAllText($outFull, $json, [Text.UTF8Encoding]::new($false))
[pscustomobject]@{status=$status;outputPath=$outFull;taskId=$task.taskId;routeHash=$routeHash;validatedCount=$validations.Count;failedCount=$failed.Count}
