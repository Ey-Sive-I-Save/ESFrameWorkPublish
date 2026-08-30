[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$TaskReceiptPath,
    [Parameter(Mandatory=$true)][string[]]$EntryPaths,
    [string]$OutputPath = 'ES/Output/WebPageStudio/bootstrap/round-04-knowledge-route.json'
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
if ([string]$task.recordType -cne 'TaskContextCreationReceipt' -or [string]$task.roundId -cne 'web-generation-round-03' -or [string]$task.status -cne 'accepted') { throw 'blocked.round-04.missing-task' }
if ([string]$task.taskId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,80}$' -or [int]$task.taskRevision -lt 1 -or [int]$task.contextVersion -lt 1) { throw 'blocked.round-04.invalid-task-identity' }
if (@($EntryPaths).Count -eq 0 -or @($EntryPaths).Count -gt 3) { throw 'blocked.round-04.route-overbreadth' }
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
$failed = @($validations | Where-Object { [string]$_.status -eq 'blocked' })
$partial = @($validations | Where-Object { [string]$_.status -eq 'partial' })
$status = if ($failed.Count -gt 0) { 'blocked' } elseif ($partial.Count -gt 0) { 'partial' } else { 'accepted' }
$routeHash = Hash-Object ([ordered]@{taskId=[string]$task.taskId;taskRevision=[int]$task.taskRevision;entries=@($validations | ForEach-Object { $_.entryPath });validationHashes=@($validations | ForEach-Object { $_.reportHash })})
$outFull = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputPath)); $parent=Split-Path -Parent $outFull
if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
$receipt = [ordered]@{ schemaVersion=1; recordType='KnowledgeRouteReceipt'; roundId='web-generation-round-04'; stageId='knowledge-route'; status=$status; taskId=$task.taskId; taskRevision=$task.taskRevision; contextVersion=$task.contextVersion; sourceScopeHash=if($task.PSObject.Properties['sourceScopeHash']){$task.sourceScopeHash}else{$null}; routePlanHash=if($task.PSObject.Properties['routePlanHash']){$task.routePlanHash}else{$null}; taskContextHash=if($task.PSObject.Properties['taskContextHash']){$task.taskContextHash}else{$null}; selectedEntries=@($validations); routeHash=$routeHash; aiAnalysis='Select the minimum Knowledge set for the frozen TaskContext and preserve validator findings without upgrading summaries to authority.'; execution='Read-only validation of selected entries and index bindings; no Knowledge write or refresh.'; decision=if($status -eq 'accepted'){'accepted-for-capability-design'}else{'blocked-knowledge-closure'}; returnReceipt=[ordered]@{status=$status;nextRound='web-generation-round-05-capability-design'}; nonClaims=@('not Knowledge repair','not design','not HTML generation','not Runtime/network/Unity/release') }
$json = $receipt | ConvertTo-Json -Depth 40
[IO.File]::WriteAllText($outFull, $json, [Text.UTF8Encoding]::new($false))
[pscustomobject]@{status=$status;outputPath=$outFull;taskId=$task.taskId;routeHash=$routeHash;validatedCount=$validations.Count;failedCount=$failed.Count}
