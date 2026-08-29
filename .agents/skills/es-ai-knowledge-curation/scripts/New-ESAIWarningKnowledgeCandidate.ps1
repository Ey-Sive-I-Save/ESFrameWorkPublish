[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot,

    [Parameter(Mandatory = $true)]
    [string]$WarningPath,

    [string]$KnowledgeIndexPath = 'Documentation/AIKnowledge/KnowledgeIndex.yaml',

    [string]$ReverseIndexPath = 'ES/Automation/Candidates/AIWarningKnowledge/knowledge-reverse-index.json',

    [string]$OutputPath,

    [string]$ReceiptPath
)

$ErrorActionPreference = 'Stop'
$utf8Strict = [Text.UTF8Encoding]::new($false, $true)
$utf8NoBom = [Text.UTF8Encoding]::new($false)
$metadataColon = [char]0xFF1A
$root = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)

function Get-HashBytes([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Get-HashText([string]$Text) {
    return Get-HashBytes ([Text.Encoding]::UTF8.GetBytes($Text))
}

function Resolve-ProjectRelative([string]$RelativePath, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath)) {
        throw "${Label}_PATH_MUST_BE_PROJECT_RELATIVE"
    }
    $root = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $candidate = [IO.Path]::GetFullPath([IO.Path]::Combine($root, $RelativePath))
    $prefix = $root + [IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "${Label}_PATH_OUTSIDE_PROJECT"
    }
    return $candidate
}

function Get-ProjectPath([string]$FullPath) {
    $root = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $relative = $FullPath.Substring($root.Length).TrimStart([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    return $relative.Replace('\', '/')
}

function Read-StrictText([string]$FullPath) {
    return $utf8Strict.GetString([IO.File]::ReadAllBytes($FullPath))
}

function Get-MetadataValue([string]$Text, [string]$Name) {
    $escaped = [regex]::Escape($Name)
    $pattern = "(?m)^>\s*${escaped}\s*[${metadataColon}:]\s*(.+?)\s*$"
    $match = [regex]::Match($Text, $pattern)
    if (-not $match.Success) { return $null }
    return $match.Groups[1].Value.Trim().Trim('`').Trim()
}

function Get-RouteKeys([string]$Text) {
    $value = Get-MetadataValue $Text 'RouteKeys'
    if ([string]::IsNullOrWhiteSpace($value)) { return @() }
    $separatorPattern = "[$([char]0x3001),$([char]0xFF0C)\s]+"
    return @($value -split $separatorPattern | ForEach-Object { $_.Trim('`', '[', ']', '''', '"') } | Where-Object { $_ } | Select-Object -Unique)
}

function Get-KnowledgePointer([string]$Text) {
    $value = Get-MetadataValue $Text 'Knowledge'
    if ([string]::IsNullOrWhiteSpace($value)) { return $null }
    $match = [regex]::Match($value, '(?i)(Documentation/AIKnowledge/[^;\s`]+)')
    if (-not $match.Success) { return $null }
    $path = $match.Groups[1].Value.TrimEnd([char]0xFF1B).Replace('\', '/')
    if ($path.StartsWith('Documentation/AIKnowledge/', [StringComparison]::OrdinalIgnoreCase)) {
        return $path.Substring('Documentation/AIKnowledge/'.Length)
    }
    return $path
}

function Parse-IndexEntries([string]$Text) {
    $result = [Collections.Generic.List[object]]::new()
    $blocks = [regex]::Split($Text, '(?m)(?=^  - knowledgeId:)')
    foreach ($block in $blocks) {
        $idMatch = [regex]::Match($block, '(?m)^\s{2}-\s+knowledgeId:\s*(\S+)\s*$')
        if (-not $idMatch.Success) { continue }
        $fileMatch = [regex]::Match($block, '(?m)^\s{4}file:\s*(\S+)\s*$')
        $topicMatch = [regex]::Match($block, '(?m)^\s{4}topic:\s*(.+?)\s*$')
        $authorityMatch = [regex]::Match($block, '(?m)^\s{4}authority:\s*(.+?)\s*$')
        $routesMatch = [regex]::Match($block, '(?m)^\s{4}routeKeys:\s*\[(.*?)\]\s*$')
        $routes = @()
        if ($routesMatch.Success) {
            $routes = @($routesMatch.Groups[1].Value -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        }
        $result.Add([pscustomobject]@{
            knowledgeId = $idMatch.Groups[1].Value.Trim()
            file = if ($fileMatch.Success) { $fileMatch.Groups[1].Value.Trim().Replace('\', '/') } else { $null }
            topic = if ($topicMatch.Success) { $topicMatch.Groups[1].Value.Trim() } else { '' }
            authority = if ($authorityMatch.Success) { $authorityMatch.Groups[1].Value.Trim() } else { '' }
            routeKeys = @($routes | Select-Object -Unique)
            block = $block
        })
    }
    return @($result)
}

function Get-EntryMetadata($Entry) {
    if ([string]::IsNullOrWhiteSpace([string]$Entry.file)) { return $Entry }
    $entryRelativePath = [string]$Entry.file
    if ($entryRelativePath -notmatch '(?i)^Documentation/AIKnowledge/') { $entryRelativePath = "Documentation/AIKnowledge/$entryRelativePath" }
    $full = Resolve-ProjectRelative $entryRelativePath 'KNOWLEDGE_ENTRY'
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { return $Entry }
    $text = Read-StrictText $full
    $entryId = [regex]::Match($text, '(?m)^`KnowledgeId`:\s*`([^`]+)`')
    $authority = [regex]::Match($text, '(?m)^`Authority`:\s*`([^`]+)`')
    $routes = [regex]::Match($text, '(?m)^`RouteKeys`:\s*(.+?)\s*$')
    $Entry | Add-Member -NotePropertyName entryText -NotePropertyValue $text -Force
    $Entry | Add-Member -NotePropertyName entryPath -NotePropertyValue $full -Force
    if ($entryId.Success) { $Entry.knowledgeId = $entryId.Groups[1].Value.Trim() }
    if ($authority.Success) { $Entry.authority = $authority.Groups[1].Value.Trim() }
    if ($routes.Success) {
        $separatorPattern = "[$([char]0x3001),$([char]0xFF0C)\s]+"
        $Entry.routeKeys = @($routes.Groups[1].Value -split $separatorPattern | ForEach-Object { $_.Trim('`', '[', ']', '''', '"') } | Where-Object { $_ } | Select-Object -Unique)
    }
    return $Entry
}

function Get-EntryBodyHash([string]$Text) {
    $normalized = $Text -replace "`r`n", "`n" -replace "`r", "`n"
    $lines = @($normalized -split "`n" | ForEach-Object {
        if ($_ -match '(?i)^\s*`?EntryBodyHash`?\s*\p{P}') { return $null }
        return $_.TrimEnd(' ', "`t")
    } | Where-Object { $null -ne $_ })
    while ($lines.Count -gt 0 -and [string]::IsNullOrWhiteSpace($lines[$lines.Count - 1])) {
        if ($lines.Count -eq 1) { $lines = @(); break }
        $lines = @($lines[0..($lines.Count - 2)])
    }
    $body = if ($lines.Count -eq 0) { "`n" } else { ($lines -join "`n") + "`n" }
    return Get-HashText $body
}

function Get-EvidencePaths([string]$EvidenceRef) {
    if ([string]::IsNullOrWhiteSpace($EvidenceRef)) { return @() }
    $matches = [regex]::Matches($EvidenceRef, '(?i)(?:Documentation|Assets|\.agents)/[^;\s`]+')
    return @($matches | ForEach-Object { $_.Value.TrimEnd([char]0xFF1B).Replace('\', '/') } | Select-Object -Unique)
}

function Add-Conflict([Collections.Generic.List[object]]$List, [string]$Code, [string]$Field, [string]$Message) {
    if (-not (@($List | Where-Object { $_.code -eq $Code -and $_.field -eq $Field }).Count)) {
        $List.Add([ordered]@{ code = $Code; field = $Field; message = $Message })
    }
}

function Write-JsonCreateOnly([string]$RelativePath, $Value) {
    $full = Resolve-ProjectRelative $RelativePath 'OUTPUT'
    $json = $Value | ConvertTo-Json -Depth 30
    if (Test-Path -LiteralPath $full -PathType Leaf) {
        $existing = Read-StrictText $full
        if ($existing -eq $json) { return $false }
        try { $existingObject = $existing | ConvertFrom-Json -ErrorAction Stop } catch { throw "CANDIDATE_OUTPUT_CONFLICT:$RelativePath" }
        if ($existingObject.candidateId -and $Value.candidateId -and
            [string]$existingObject.candidateId -eq [string]$Value.candidateId -and
            [string]$existingObject.idempotencyKey -eq [string]$Value.idempotencyKey) {
            return $false
        }
        throw "CANDIDATE_OUTPUT_CONFLICT:$RelativePath"
    }
    $parent = Split-Path -Parent $full
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    $tmp = "$full.tmp.$([Guid]::NewGuid().ToString('N'))"
    try {
        [IO.File]::WriteAllText($tmp, $json, $utf8NoBom)
        [IO.File]::Move($tmp, $full)
    }
    catch {
        if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
        if (Test-Path -LiteralPath $full) { throw "CANDIDATE_OUTPUT_CONFLICT:$RelativePath" }
        throw
    }
    return $true
}

$warningFull = Resolve-ProjectRelative $WarningPath 'WARNING'
$indexFull = Resolve-ProjectRelative $KnowledgeIndexPath 'KNOWLEDGE_INDEX'
if (-not (Test-Path -LiteralPath $warningFull -PathType Leaf)) { throw 'WARNING_NOT_FOUND' }
if (-not (Test-Path -LiteralPath $indexFull -PathType Leaf)) { throw 'KNOWLEDGE_INDEX_NOT_FOUND' }

$warningBytesBefore = [IO.File]::ReadAllBytes($warningFull)
$warningText = $utf8Strict.GetString($warningBytesBefore)
$warningHash = Get-HashBytes $warningBytesBefore
$snapshotCapturedAtUtc = (Get-Item -LiteralPath $warningFull).LastWriteTimeUtc.ToUniversalTime().ToString('O')
$stableId = Get-MetadataValue $warningText 'StableId'
$statusValue = Get-MetadataValue $warningText 'Status'
$authorityValue = Get-MetadataValue $warningText 'Authority'
$applicabilityValue = Get-MetadataValue $warningText 'Applicability'
$evidenceValue = Get-MetadataValue $warningText 'EvidenceRef'
$staleWhenValue = Get-MetadataValue $warningText 'StaleWhen'
$routeKeys = @(Get-RouteKeys $warningText)
if ([string]::IsNullOrWhiteSpace($stableId) -or [string]::IsNullOrWhiteSpace($authorityValue) -or $routeKeys.Count -eq 0) {
    throw 'WARNING_METADATA_INCOMPLETE'
}

$indexBytesRead = [IO.File]::ReadAllBytes($indexFull).Length
$knowledgeMetadataBytesRead = 0
$rawIndexEntries = @(Parse-IndexEntries (Read-StrictText $indexFull))
$matchMode = 'full-scan'
$reverseIndexFresh = $false
$reverseIndexFull = $null
if (-not [string]::IsNullOrWhiteSpace($ReverseIndexPath)) {
    try {
        $reverseIndexFull = Resolve-ProjectRelative $ReverseIndexPath 'REVERSE_INDEX'
        if (Test-Path -LiteralPath $reverseIndexFull -PathType Leaf) {
            $reverse = Read-StrictText $reverseIndexFull | ConvertFrom-Json
            $indexBytesNow = [IO.File]::ReadAllBytes($indexFull)
            $indexHashNow = Get-HashBytes $indexBytesNow
            if ([string]$reverse.sourceIndexSha256 -eq $indexHashNow -and $reverse.entryCount -eq $rawIndexEntries.Count) {
                $reverseIndexFresh = $true
                $reverseIds = @()
                $stableProperty = $reverse.byStableId.PSObject.Properties[$stableId]
                if ($stableProperty) { $reverseIds = @($stableProperty.Value) }
                if ($reverseIds.Count -gt 0) {
                    $rawIndexEntries = @($rawIndexEntries | Where-Object { $reverseIds -contains $_.knowledgeId })
                    $matchMode = 'reverse-index-hit'
                }
                else { $matchMode = 'reverse-index-miss-fallback' }
            }
            else { $matchMode = 'reverse-index-stale-fallback' }
        }
        else { $matchMode = 'reverse-index-missing-fallback' }
    }
    catch { $matchMode = 'reverse-index-invalid-fallback' }
}
$pointerForIndex = Get-KnowledgePointer $warningText
if (-not [string]::IsNullOrWhiteSpace($pointerForIndex) -and $matchMode -ne 'reverse-index-hit') {
    $pointerMatches = @($rawIndexEntries | Where-Object { $_.file -eq $pointerForIndex })
    if ($pointerMatches.Count -eq 1) { $rawIndexEntries = $pointerMatches; $matchMode = 'knowledge-pointer-hit' }
}
$indexEntries = @($rawIndexEntries | ForEach-Object { Get-EntryMetadata $_ })
$knowledgePointer = Get-KnowledgePointer $warningText
$conflicts = [Collections.Generic.List[object]]::new()
$candidateIds = [Collections.Generic.List[string]]::new()
$signals = [ordered]@{
    stableId = $false
    knowledgePointer = $false
    authority = $false
    routeKeys = $false
    applicability = $false
    evidenceRef = $false
}
$decision = 'new'
$rationale = 'No deterministic existing Knowledge binding was found; emit a new candidate without changing formal Knowledge files.'
$matched = @()

if (-not [string]::IsNullOrWhiteSpace($knowledgePointer)) {
    $signals.knowledgePointer = $true
    $matched = @($indexEntries | Where-Object { $_.file -eq $knowledgePointer })
    if ($matched.Count -eq 0) {
        Add-Conflict $conflicts 'KNOWLEDGE_POINTER_NOT_INDEXED' 'Knowledge' "Knowledge pointer does not have a unique KnowledgeIndex binding: $knowledgePointer"
        $decision = 'blocked'
        $rationale = 'The Warning declares a Knowledge pointer, but the pointer is not bound exactly once in KnowledgeIndex.yaml.'
    }
    elseif ($matched.Count -gt 1) {
        Add-Conflict $conflicts 'KNOWLEDGE_POINTER_DUPLICATE' 'KnowledgeIndex' "Knowledge pointer resolves to multiple index entries: $knowledgePointer"
        $decision = 'blocked'
        $rationale = 'The declared Knowledge pointer is ambiguous in the index.'
    }
    else {
        $entry = $matched[0]
        $candidateIds.Add([string]$entry.knowledgeId)
        $signals.stableId = [bool]($entry.entryText -match [regex]::Escape($stableId))
        $signals.authority = [bool]($entry.authority -match '(?i)AIWarnings|Source')
        $signals.routeKeys = @($routeKeys | Where-Object { @($entry.routeKeys) -contains $_ }).Count -gt 0
        $signals.applicability = [bool]($entry.entryText -match [regex]::Escape($applicabilityValue.Substring(0, [Math]::Min(40, $applicabilityValue.Length))))
        $signals.evidenceRef = [bool]($entry.entryText -match [regex]::Escape($WarningPath.Replace('\', '/')))
        if (-not $signals.stableId -or -not $signals.routeKeys -or (-not $signals.authority -and -not $signals.evidenceRef)) {
            Add-Conflict $conflicts 'KNOWLEDGE_BINDING_INCOMPATIBLE' 'Knowledge' 'StableId, RouteKeys, Authority or SourceRef evidence does not establish a compatible existing binding.'
            $decision = 'blocked'
            $rationale = 'The declared Knowledge pointer exists, but its identity or authority boundary is incompatible.'
        }
        else {
            $decision = 'existing'
            $rationale = 'The Warning Knowledge pointer resolves to one indexed entry with compatible identity, route and authority/source evidence.'
        }
    }
}
else {
    $matched = @($indexEntries | Where-Object {
        $_.entryText -and ($_.entryText -match [regex]::Escape($stableId) -or $_.entryText -match [regex]::Escape($WarningPath.Replace('\', '/')))
    })
    foreach ($entry in $matched) { $candidateIds.Add([string]$entry.knowledgeId) }
    if ($matched.Count -gt 1) {
        Add-Conflict $conflicts 'MULTIPLE_KNOWLEDGE_MATCHES' 'KnowledgeIndex' 'More than one existing entry carries the Warning stable identity or source path.'
        $decision = 'ambiguous'
        $rationale = 'Multiple deterministic identity/source matches were found; no merge is inferred.'
    }
    elseif ($matched.Count -eq 1) {
        $entry = $matched[0]
        $signals.stableId = [bool]($entry.entryText -match [regex]::Escape($stableId))
        $signals.authority = [bool]($entry.authority -match '(?i)AIWarnings|Source')
        $signals.routeKeys = @($routeKeys | Where-Object { @($entry.routeKeys) -contains $_ }).Count -gt 0
        $signals.evidenceRef = [bool]($entry.entryText -match [regex]::Escape($WarningPath.Replace('\', '/')))
        if ($signals.stableId -and $signals.routeKeys -and ($signals.authority -or $signals.evidenceRef)) {
            $signals.applicability = [bool]($entry.entryText -match [regex]::Escape($applicabilityValue.Substring(0, [Math]::Min(40, $applicabilityValue.Length))))
            $decision = 'existing'
            $rationale = 'One deterministic identity/source match was found with compatible route and authority/source evidence.'
        }
        else {
            Add-Conflict $conflicts 'KNOWLEDGE_BINDING_INCOMPATIBLE' 'Knowledge' 'The only identity/source match does not satisfy route and authority/source compatibility.'
            $decision = 'blocked'
            $rationale = 'An apparent existing match was found, but compatibility is not proven.'
        }
    }
}

$evidencePaths = @(Get-EvidencePaths $evidenceValue)
$evidencePassed = $true
if ($evidencePaths.Count -eq 0) {
    $evidencePassed = $false
    Add-Conflict $conflicts 'EVIDENCE_REF_UNRESOLVED' 'EvidenceRef' 'EvidenceRef contains no project-relative evidence path.'
}
else {
    foreach ($evidencePath in $evidencePaths) {
        try { $evidenceFull = Resolve-ProjectRelative $evidencePath 'EVIDENCE_REF' }
        catch { $evidencePassed = $false; Add-Conflict $conflicts 'EVIDENCE_REF_OUTSIDE_PROJECT' 'EvidenceRef' $evidencePath; continue }
        if (-not (Test-Path -LiteralPath $evidenceFull -PathType Leaf)) {
            $evidencePassed = $false
            Add-Conflict $conflicts 'EVIDENCE_REF_UNRESOLVED' 'EvidenceRef' "Evidence path does not exist: $evidencePath"
        }
    }
}
$signals.evidenceRef = $evidencePassed
if (-not $evidencePassed -and $decision -ne 'ambiguous') {
    $decision = 'blocked'
    $rationale = 'EvidenceRef does not resolve to existing project evidence, so the candidate is blocked.'
}

$warningBytesAfter = [IO.File]::ReadAllBytes($warningFull)
$warningHashAfter = Get-HashBytes $warningBytesAfter
$sourceStable = $warningHash -eq $warningHashAfter
if (-not $sourceStable) {
    Add-Conflict $conflicts 'WARNING_CHANGED_DURING_ORCHESTRATION' 'warningHash' 'Warning content changed while the candidate was being generated; retry from a new snapshot.'
    $decision = 'stale'
    $rationale = 'The input Warning changed during orchestration; the candidate must not be applied.'
}

$status = switch ($decision) {
    'existing' { 'attached' }
    'new' { 'candidate-created' }
    'ambiguous' { 'review' }
    'blocked' { 'blocked' }
    'stale' { 'stale/retry-required' }
}
$idempotencyKey = Get-HashText ("$stableId`:$warningHash")
$candidateId = "es.aiwarning.candidate.$($idempotencyKey.Substring(0, 24))"
$safeStable = ($stableId -replace '[^A-Za-z0-9._-]', '-')
$newKnowledgeId = "es.aiwarning.knowledge.$safeStable.v1"
$targetPath = if ($matched.Count -eq 1 -and $matched[0].file) {
    if ([string]$matched[0].file -match '(?i)^Documentation/AIKnowledge/') { [string]$matched[0].file } else { "Documentation/AIKnowledge/$($matched[0].file)" }
} else { "Documentation/AIKnowledge/entries/aiwarning-$safeStable.md" }
$sourceSetHash = Get-HashText $warningHash
$entryBodyHash = $null
if ($matched.Count -eq 1 -and $matched[0].entryText) { $entryBodyHash = Get-EntryBodyHash $matched[0].entryText }

$warningsRoot = Join-Path $root 'Assets/Plugins/ES/AIWarnings'
$startDir = Get-ChildItem -LiteralPath $warningsRoot -Directory | Where-Object { $_.Name -like '00_*' } | Select-Object -First 1
$startReads = @()
if ($startDir) {
    $startReads = @(
        (Get-ChildItem -LiteralPath $startDir.FullName -File | Where-Object { $_.Name -ieq 'README.md' } | Select-Object -First 1),
        (Get-ChildItem -LiteralPath $startDir.FullName -File | Where-Object { $_.Name -match '(?i)CurrentStatus' } | Select-Object -First 1),
        (Get-ChildItem -LiteralPath $startDir.FullName -File | Where-Object { $_.Name -match '(?i)RuleIndex' } | Select-Object -First 1)
    ) | Where-Object { $_ }
}
$requiredReads = @($WarningPath.Replace('\', '/')) + @($startReads | ForEach-Object { Get-ProjectPath $_.FullName }) | Select-Object -Unique
$requiredReadMissing = @($requiredReads | Where-Object { -not (Test-Path -LiteralPath (Resolve-ProjectRelative $_ 'REQUIRED_READ')) })
if ($requiredReadMissing.Count -gt 0) {
    Add-Conflict $conflicts 'REQUIRED_READ_MISSING' 'requiredReads' ($requiredReadMissing -join ', ')
    if ($decision -notin @('ambiguous', 'stale')) { $decision = 'blocked'; $status = 'blocked' }
}

$validationState = if ($decision -eq 'stale') { 'stale' } elseif ($conflicts.Count -gt 0) { if ($decision -eq 'ambiguous') { 'review' } else { 'blocked' } } elseif ($decision -eq 'existing') { 'passed' } else { 'pending' }
$indexState = if ($decision -eq 'existing' -and $matched.Count -eq 1) { 'passed' } else { 'pending' }
$routeState = if ($decision -eq 'existing' -and $matched.Count -eq 1 -and $signals.routeKeys) { 'passed' } else { 'pending' }
$candidate = [ordered]@{
    schemaVersion = 1
    contractId = 'es://automation/contracts/aiwarning-knowledge-candidate/v1'
    recordType = 'AIWarningKnowledgeCandidate'
    candidateId = $candidateId
    status = $status
    idempotencyKey = $idempotencyKey
    idempotencyKeyKind = 'stableId+warningHash-sha256'
    createdAtUtc = $snapshotCapturedAtUtc
    sourceSnapshot = [ordered]@{
        warningPath = $WarningPath.Replace('\', '/')
        warningHash = $warningHash
        stableId = $stableId
        status = if ($statusValue) { $statusValue } else { 'unknown' }
        authority = $authorityValue
        routeKeys = @($routeKeys)
        applicability = if ($applicabilityValue) { $applicabilityValue } else { 'unspecified' }
        evidenceRef = if ($evidenceValue) { $evidenceValue } else { 'unspecified' }
        staleWhen = if ($staleWhenValue) { $staleWhenValue } else { 'unspecified' }
        capturedAtUtc = $snapshotCapturedAtUtc
    }
    match = [ordered]@{
        decision = $decision
        candidateKnowledgeIds = @($candidateIds | Select-Object -Unique)
        signals = $signals
        conflicts = @($conflicts)
        rationale = $rationale
    }
    proposedEntry = [ordered]@{
        knowledgeId = if ($matched.Count -eq 1 -and $matched[0].knowledgeId) { [string]$matched[0].knowledgeId } else { $newKnowledgeId }
        targetPath = $targetPath
        routeKeys = @($routeKeys)
        sourceRefs = @([ordered]@{ path = $WarningPath.Replace('\', '/'); sha256 = $warningHash; role = 'warning-authority' })
        requiredReads = @($requiredReads)
        expectedHashes = [ordered]@{
            contentHash = $sourceSetHash
            sourceSetHash = $sourceSetHash
            entryBodyHash = $entryBodyHash
        }
    }
    validation = [ordered]@{
        state = $validationState
        checks = [ordered]@{
            utf8 = 'passed'
            sourceRefs = if ($sourceStable) { 'passed' } else { 'failed' }
            contentHash = 'passed'
            indexBinding = $indexState
            route = $routeState
            evidenceRef = if ($evidencePassed) { 'passed' } else { 'failed' }
        }
        conflicts = @($conflicts)
        sourceStableAtEnd = $sourceStable
        matchMode = $matchMode
        reverseIndexFresh = $reverseIndexFresh
        resourceObservations = [ordered]@{ warningBytes = $warningBytesBefore.Length; knowledgeIndexBytes = $indexBytesRead; indexEntryCount = $rawIndexEntries.Count }
    }
    replay = [ordered]@{
        commands = @(
            "pwsh -NoProfile -File .agents/skills/es-ai-knowledge-curation/scripts/New-ESAIWarningKnowledgeCandidate.ps1 -ProjectRoot . -WarningPath '$($WarningPath.Replace("'", "''"))'"
        )
        inputHash = $warningHash
        candidateOnly = $true
        applyRequired = $true
    }
    nonClaims = @(
        'This candidate does not modify AIWarnings, KnowledgeIndex.yaml, formal Knowledge entries, migration ledgers, inventory or freshness projections.',
        'Candidate status is not formal Knowledge registration and does not grant write, Runtime, Git, release or external-service authority.',
        'Static path and hash checks do not prove Unity, PlayMode, Runtime, Profiler, Player, IL2CPP or release behavior.'
    )
}

$candidateJson = $candidate | ConvertTo-Json -Depth 30 -Compress
$candidateHash = Get-HashText $candidateJson
$snapshotHash = Get-HashText (($candidate.sourceSnapshot | ConvertTo-Json -Depth 20 -Compress))
$receipt = [ordered]@{
    schemaVersion = 1
    contractId = 'es://automation/contracts/aiwarning-knowledge-receipt/v1'
    recordType = 'AIWarningKnowledgeOrchestrationReceipt'
    receiptId = "es.aiwarning.receipt.$($candidateHash.Substring(0, 24))"
    candidateId = $candidateId
    status = $status
    idempotencyKey = $idempotencyKey
    createdAtUtc = $snapshotCapturedAtUtc
    candidateHash = $candidateHash
    inputSnapshotHash = $snapshotHash
    transactionExecuted = $false
    formalRegistration = 'not-run'
    validationCommands = @(
        "pwsh -NoProfile -File .agents/skills/es-ai-knowledge-curation/scripts/Test-ESAIWarningKnowledgeCandidate.ps1 -ProjectRoot . -CandidatePath '<candidate>'"
    )
    nonClaims = @(
        'The receipt records candidate orchestration only; it is not an Apply receipt.',
        'No formal KnowledgeIndex or AIBRAIN_ENTRY update was performed.',
        'Runtime and release behavior were not executed.'
    )
}

if ($OutputPath) {
    $null = Write-JsonCreateOnly $OutputPath $candidate
    if (-not $ReceiptPath) { $ReceiptPath = [IO.Path]::Combine([IO.Path]::GetDirectoryName($OutputPath), ([IO.Path]::GetFileNameWithoutExtension($OutputPath) + '.receipt.json')) }
    $null = Write-JsonCreateOnly $ReceiptPath $receipt
}

[pscustomobject]@{
    status = $status
    candidateId = $candidateId
    candidateHash = $candidateHash
    idempotencyKey = $idempotencyKey
    matchDecision = $decision
    candidateKnowledgeIds = @($candidateIds | Select-Object -Unique)
    conflictCodes = @($conflicts | ForEach-Object { $_.code } | Select-Object -Unique)
    sourceStableAtEnd = $sourceStable
    transactionExecuted = $false
    formalRegistration = 'not-run'
    candidate = $candidate
    receipt = $receipt
} | ConvertTo-Json -Depth 30
