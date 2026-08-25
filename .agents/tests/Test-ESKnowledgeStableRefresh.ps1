[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$ProjectRoot)

$ErrorActionPreference = 'Stop'
$utf8 = [Text.UTF8Encoding]::new($false)
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\', '/')
$fixture = Join-Path $root ('ES/Output/SkillValidationFixtures/es-knowledge-refresh-' + [Guid]::NewGuid().ToString('N'))
$prefix = $root + [IO.Path]::DirectorySeparatorChar
if (-not [IO.Path]::GetFullPath($fixture).StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture escaped ProjectRoot.' }
$exporter = Join-Path $root '.agents/skills/es-knowledge-validator/scripts/Export-ESKnowledgeRefreshPlan.ps1'
$applier = Join-Path $root '.agents/skills/es-knowledge-validator/scripts/Invoke-ESKnowledgeStableRefresh.ps1'

function Write-Utf8([string]$Path, [string]$Text) {
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    [IO.File]::WriteAllText($Path, $Text, $utf8)
}
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function ContentHash([string[]]$Hashes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes((@($Hashes | Sort-Object -CaseSensitive) -join ''))))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}
function TextHash([string]$Text) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text)))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}
function Join-PlanOrdinal([string[]]$Values, [string]$Separator) {
    $copy = [string[]]@($Values)
    [Array]::Sort($copy, [StringComparer]::Ordinal)
    $copy -join $Separator
}
function Format-PlanCanonicalField($Value) {
    if ($null -eq $Value) { $text = '' }
    elseif ($Value -is [bool]) { $text = if ([bool]$Value) { 'true' } else { 'false' } }
    else { $text = [string]$Value }
    "$($text.Length):$text"
}
function New-PlanCanonicalRecord([string]$Kind, [object[]]$Values) {
    $fields = [Collections.Generic.List[string]]::new()
    $fields.Add((Format-PlanCanonicalField $Kind))
    foreach ($value in @($Values)) { $fields.Add((Format-PlanCanonicalField $value)) }
    $fields -join '|'
}
function Get-CanonicalPlanHash($Plan) {
    $records = [Collections.Generic.List[string]]::new()
    $records.Add((New-PlanCanonicalRecord -Kind 'plan' -Values @($Plan.schemaVersion, $Plan.toolId, $Plan.refreshAlgorithmVersion, $Plan.indexHash)))
    foreach ($entry in @($Plan.entrySnapshots)) {
        $records.Add((New-PlanCanonicalRecord -Kind 'entry' -Values @(
            $entry.entry, $entry.knowledgeId, $entry.entryMode, $entry.hashSchema,
            $entry.entryHash, $entry.status, $entry.sourceSetHash, $entry.sourceRefCount,
            $entry.indexBindingCount, $entry.declaredContentHash,
            $entry.declaredSourceSetHash, $entry.declaredEntryBodyHash,
            $entry.expectedContentHash, $entry.expectedSourceSetHash, $entry.expectedEntryBodyHash
        )))
        foreach ($bindingId in @($entry.indexBindingIds)) {
            $records.Add((New-PlanCanonicalRecord -Kind 'index-binding' -Values @($entry.entry, $bindingId)))
        }
        foreach ($binding in @($entry.indexBindings)) {
            $records.Add((New-PlanCanonicalRecord -Kind 'index-binding-projection' -Values @(
                $entry.entry, $binding.id, $binding.hashSchema, $binding.contentHash,
                $binding.sourceSetHash, $binding.entryBodyHash, $binding.expectedContentHash,
                $binding.expectedSourceSetHash, $binding.expectedEntryBodyHash
            )))
        }
        foreach ($source in @($entry.sourceRefs)) {
            $records.Add((New-PlanCanonicalRecord -Kind 'source' -Values @(
                $entry.entry, $source.path, $source.declaredHash, $source.currentHash,
                $source.firstSampleHash, $source.snapshotStable
            )))
        }
    }
    foreach ($finding in @($Plan.findings)) {
        $records.Add((New-PlanCanonicalRecord -Kind 'finding' -Values @(
            $finding.code, $finding.entry, $finding.entryHash, $finding.source,
            $finding.declaredHash, $finding.currentHash, $finding.firstSampleHash,
            $finding.snapshotStable, $finding.declaredContentHash, $finding.action,
            $finding.reason
        )))
    }
    TextHash (Join-PlanOrdinal $records.ToArray() "`n")
}
function EntryBodyHash([string]$Text) {
    $normalized = $Text.Replace("`r`n", "`n").Replace("`r", "`n")
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($line in $normalized.Split([char]10)) {
        if ($line -match '(?i)^\s*`EntryBodyHash`\s*[:\uFF1A]\s*`[^`]*`\s*$') { continue }
        $lines.Add($line.TrimEnd([char[]]@(' ', "`t")))
    }
    while ($lines.Count -gt 0 -and $lines[$lines.Count - 1].Length -eq 0) { $lines.RemoveAt($lines.Count - 1) }
    TextHash (($lines -join "`n") + "`n")
}
function Assert([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function New-ScriptStartInfo([string]$Script, [string[]]$Arguments) {
    $quoted = @('-NoProfile', '-File', ('"' + $Script + '"')) + @($Arguments | ForEach-Object {
        if ($_ -match '\s') { '"' + $_.Replace('"', '\"') + '"' } else { $_ }
    })
    $start = [Diagnostics.ProcessStartInfo]::new('powershell.exe', ($quoted -join ' '))
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start
}
function Invoke-Script([string]$Script, [string[]]$Arguments) {
    $process = [Diagnostics.Process]::Start((New-ScriptStartInfo $Script $Arguments))
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    [pscustomobject]@{ exitCode = $process.ExitCode; output = (($stdout + "`n" + $stderr).Trim()) }
}
function Start-ScriptProcess([string]$Script, [string[]]$Arguments) {
    [Diagnostics.Process]::Start((New-ScriptStartInfo $Script $Arguments))
}
function Complete-ScriptProcess([Diagnostics.Process]$Process, [int]$TimeoutMilliseconds = 30000) {
    if (-not $Process.WaitForExit($TimeoutMilliseconds)) {
        try { $Process.Kill() } catch {}
        throw "Timed out waiting for child PowerShell process $($Process.Id)."
    }
    $stdout = $Process.StandardOutput.ReadToEnd()
    $stderr = $Process.StandardError.ReadToEnd()
    [pscustomobject]@{ exitCode = $Process.ExitCode; output = (($stdout + "`n" + $stderr).Trim()) }
}
function Wait-ForPath([string]$Path, [int]$TimeoutMilliseconds = 10000) {
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path -PathType Leaf) { return }
        Start-Sleep -Milliseconds 25
    }
    throw "Timed out waiting for test coordination path: $Path"
}
function Write-InstrumentedApplier([string]$Name, [hashtable]$Injections) {
    $source = Get-Content -LiteralPath $applier -Raw -Encoding UTF8
    foreach ($marker in $Injections.Keys) {
        Assert (@([regex]::Matches($source, [regex]::Escape([string]$marker))).Count -eq 1) "instrumentation marker is not unique: $marker"
        $source = $source.Replace([string]$marker, ([string]$marker + "`n" + [string]$Injections[$marker]))
    }
    $path = Join-Path $fixture "ES/Output/$Name.ps1"
    Write-Utf8 $path $source
    $path
}
function Remove-JsonProperty($Object, [string]$Path) {
    $segments = @($Path.Split('/'))
    $current = $Object
    for ($i = 0; $i -lt $segments.Count - 1; $i++) {
        if ($segments[$i] -match '^\d+$') { $current = @($current)[[int]$segments[$i]] }
        else { $current = $current.($segments[$i]) }
    }
    [void]$current.PSObject.Properties.Remove($segments[$segments.Count - 1])
}
function Reset-Fixture {
    $fixturePrefix = [IO.Path]::GetFullPath($fixture).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    foreach ($relative in @('Documentation/AIKnowledge', 'src', 'ES/Output')) {
        $target = [IO.Path]::GetFullPath((Join-Path $fixture $relative))
        if (-not $target.StartsWith($fixturePrefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Fixture reset escaped its root: $target" }
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
    }
}
function Write-Fixture([switch]$SecondEntryCurrent) {
    Reset-Fixture
    $oldHash = '0' * 64
    $initialContentHashes = @{}
    foreach ($name in @('one', 'two')) {
        $source = Join-Path $fixture "src/$name.txt"
        Write-Utf8 $source ("current-$name`n")
        $declaredPrimaryHash = if ($SecondEntryCurrent -and $name -eq 'two') { Hash $source } else { $oldHash }
        $steadySource = Join-Path $fixture "src/steady-$name.txt"
        Write-Utf8 $steadySource ("steady-$name`n")
        $steadyHash = Hash $steadySource
        Write-Utf8 (Join-Path $fixture "src/external-$name.txt") ("external-$name`n")
        $initialContentHashes[$name] = ContentHash @($declaredPrimaryHash, $steadyHash)
        $entry = @"
# $name

``KnowledgeId``: ``fixture.$name.v1``
``Authority``: ``Source``
``RouteKeys``: ``knowledge``
``ContentHash``: ``$($initialContentHashes[$name])``
``EvidenceLevel``: ``S1``
``StaleWhen``: source changes

## SourceRefs

- ``src/$name.txt`` (``$declaredPrimaryHash``)
- ``src\steady-$name.txt`` (``$steadyHash``)

## ExternalReferences

- ``BuildReport``: ``https://example.invalid/$name``; SHA-256 ``$oldHash``
- ``src/external-$name.txt`` (``$oldHash``)
"@
        Write-Utf8 (Join-Path $fixture "Documentation/AIKnowledge/entries/$name.md") $entry
    }
    $index = @"
schemaVersion: 1
entries:
  - knowledgeId: fixture.one.v1
    file: entries/one.md
    contentHash: $($initialContentHashes['one'])
  - knowledgeId: fixture.two.v1
    file: entries/two.md
    contentHash: $($initialContentHashes['two'])
"@
    Write-Utf8 (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml') $index
}

function Write-SharedFixture {
    Reset-Fixture
    $oldHash = '0' * 64
    $source = Join-Path $fixture 'src/shared.txt'
    Write-Utf8 $source "current-shared`n"
    $initialContentHash = ContentHash @($oldHash)
    $entry = @"
# shared

``KnowledgeId``: ``fixture.shared.v1``
``EntryMode``: ``SharedRouteProjection``
``Authority``: ``Source``
``RouteKeys``: ``route-a``, ``route-b``
``ContentHash``: ``$initialContentHash``
``EvidenceLevel``: ``S1``
``StaleWhen``: source changes

## SourceRefs

- ``src/shared.txt`` (``$oldHash``)

## RouteProjections

- ``fixture.shared.a.v1``: ``route-a``
- ``fixture.shared.b.v1``: ``route-b``
"@
    Write-Utf8 (Join-Path $fixture 'Documentation/AIKnowledge/entries/shared.md') $entry
    $index = @"
schemaVersion: 1
qualityGate:
  deduplication:
    sharedRouteProjectionAllowed: true
entries:
  - knowledgeId: fixture.shared.a.v1
    file: entries/shared.md
    contentHash: $initialContentHash
  - knowledgeId: fixture.shared.b.v1
    file: entries/shared.md
    contentHash: $initialContentHash
"@
    Write-Utf8 (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml') $index
}

function Write-V2Fixture([switch]$Current) {
    Reset-Fixture
    $source = Join-Path $fixture 'src/v2.txt'
    Write-Utf8 $source "current-v2`n"
    $declaredSourceHash = if ($Current) { Hash $source } else { '0' * 64 }
    $sourceSetHash = ContentHash @($declaredSourceHash)
    $entryBodyPlaceholder = 'f' * 64
    $entry = @"
# v2

``KnowledgeId``: ``fixture.v2.v1``
``Authority``: ``Source``
``RouteKeys``: ``knowledge``
``HashSchema``: ``v2``
``ContentHash``: ``$sourceSetHash``
``SourceSetHash``: ``$sourceSetHash``
``EntryBodyHash``: ``$entryBodyPlaceholder``
``EvidenceLevel``: ``S1``
``StaleWhen``: source changes

## SourceRefs

- ``src/v2.txt`` (``$declaredSourceHash``)

## Decision

The v2 body hash excludes only its own metadata line.
"@
    $entryBodyHash = EntryBodyHash $entry
    $entry = $entry.Replace($entryBodyPlaceholder, $entryBodyHash)
    $entryPath = Join-Path $fixture 'Documentation/AIKnowledge/entries/v2.md'
    Write-Utf8 $entryPath $entry
    $index = @"
schemaVersion: 1
entries:
  - knowledgeId: fixture.v2.v1
    file: entries/v2.md
    contentHash: $sourceSetHash
    hashSchema: v2
    sourceSetHash: $sourceSetHash
    entryBodyHash: $entryBodyHash
"@
    $indexPath = Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml'
    Write-Utf8 $indexPath $index
    [pscustomobject]@{
        sourcePath = $source
        entryPath = $entryPath
        indexPath = $indexPath
        declaredSourceHash = $declaredSourceHash
        sourceSetHash = $sourceSetHash
        entryBodyHash = $entryBodyHash
    }
}

try {
    Write-Fixture
    $export = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($export.exitCode -eq 0) ('plan export failed: ' + $export.output)
    $exportPlan = $export.output | ConvertFrom-Json
    Assert ($exportPlan.schemaVersion -eq 3 -and [string]$exportPlan.refreshAlgorithmVersion -ceq 'es-knowledge-stable-refresh-v2-source-normalized' -and [string]$exportPlan.indexHash -match '^[0-9a-f]{64}$') 'plan baseline binding is missing'
    Assert ($exportPlan.planStatus -eq 'ready' -and $exportPlan.blockerCount -eq 0) 'valid fixture produced a blocked plan'
    Assert ($exportPlan.findingCount -eq 2) 'plan must include only the two SourceRefs'
    Assert (@($exportPlan.findings | Where-Object { $_.source -notmatch '^src/(one|two)\.txt$' }).Count -eq 0) 'ExternalReferences leaked into the refresh plan'
    Assert ($exportPlan.entrySnapshots.Count -eq 2) 'plan did not bind both target entries'
    Assert (@($exportPlan.entrySnapshots | Where-Object {
        [string]$_.knowledgeId -cnotmatch '^fixture\.(one|two)\.v1$' -or
        [string]$_.entryMode -cne 'Canonical' -or [string]$_.hashSchema -cne 'legacy' -or
        [int]$_.indexBindingCount -ne 1 -or @($_.indexBindingIds).Count -ne 1 -or
        [string]$_.indexBindingIds[0] -cne [string]$_.knowledgeId
    }).Count -eq 0) 'plan did not bind canonical Entry/Index identity metadata'
    $plannedSources = @($exportPlan.entrySnapshots | ForEach-Object { @($_.sourceRefs) })
    Assert ($plannedSources.Count -eq 4) 'plan did not bind each target entry complete SourceRef set'
    Assert (@($exportPlan.entrySnapshots | Where-Object { $_.sourceRefCount -ne 2 -or [string]$_.sourceSetHash -notmatch '^[0-9a-f]{64}$' }).Count -eq 0) 'entry source-set binding is incomplete'
    Assert (@($exportPlan.entrySnapshots | Where-Object {
        [string]$_.expectedContentHash -cnotmatch '^[0-9a-f]{64}$' -or
        -not [string]::IsNullOrEmpty([string]$_.expectedSourceSetHash) -or
        -not [string]::IsNullOrEmpty([string]$_.expectedEntryBodyHash)
    }).Count -eq 0) 'legacy expected refresh projection is incomplete'
    foreach ($snapshot in @($exportPlan.entrySnapshots)) {
        Assert (@($snapshot.indexBindings | Where-Object {
            [string]$_.expectedContentHash -cne [string]$snapshot.expectedContentHash -or
            [string]$_.expectedSourceSetHash -cne [string]$snapshot.expectedSourceSetHash -or
            [string]$_.expectedEntryBodyHash -cne [string]$snapshot.expectedEntryBodyHash
        }).Count -eq 0) "expected Index projection differs from Entry: $($snapshot.entry)"
    }
    Assert (@($plannedSources | Where-Object { $_.path -match '\\' -or $_.path -match 'external-' }).Count -eq 0) 'SourceRef normalization or ExternalReferences exclusion failed'
    $deterministicExport = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($deterministicExport.exitCode -eq 0) 'deterministic repeat export failed'
    $deterministicPlan = $deterministicExport.output | ConvertFrom-Json
    Assert ($deterministicPlan.planHash -ceq $exportPlan.planHash) 'unchanged complete SourceRef snapshots produced different plan hashes'
    $deniedPlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-OutputPath', 'Documentation/AIKnowledge/forbidden-plan.json', '-SampleDelayMilliseconds', '0')
    Assert ($deniedPlan.exitCode -ne 0) 'refresh plan output escaped ES/Output'

    $previewEntryHash = Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')
    $preview = Invoke-Script $applier @('-ProjectRoot', $fixture)
    Assert ($preview.exitCode -eq 0) ('preview failed: ' + $preview.output)
    $previewReceipt = $preview.output | ConvertFrom-Json
    Assert ($previewReceipt.transactionExecuted -eq $false -and $previewReceipt.atomicBatch -eq $false -and
        [string]$previewReceipt.transactionMode -ceq 'preview-no-transaction' -and $previewReceipt.crashSafe -eq $false -and
        $previewReceipt.applied -eq $false) 'preview receipt falsely claimed a transaction'
    Assert ((Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')) -ceq $previewEntryHash) 'preview mutated an entry'
    $stableRefreshLock = Join-Path $fixture 'ES/Output/KnowledgeValidation/stable-refresh.lock'
    Assert (-not (Test-Path -LiteralPath $stableRefreshLock)) 'preview created the stable refresh lock file'
    $deniedReceipt = Invoke-Script $applier @('-ProjectRoot', $fixture, '-OutputPath', 'Documentation/AIKnowledge/forbidden-receipt.json')
    Assert ($deniedReceipt.exitCode -ne 0) 'stable refresh receipt output escaped ES/Output'

    $whatIfReceiptRelative = 'ES/Output/KnowledgeValidation/stable-refresh-whatif-receipt.json'
    $whatIfReceiptPath = Join-Path $fixture $whatIfReceiptRelative
    $whatIf = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply', '-WhatIf', '-OutputPath', $whatIfReceiptRelative)
    Assert ($whatIf.exitCode -eq 0 -and (Test-Path -LiteralPath $whatIfReceiptPath -PathType Leaf)) ('WhatIf did not emit its truthful receipt: ' + $whatIf.output)
    $whatIfReceipt = Get-Content -LiteralPath $whatIfReceiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert ($whatIfReceipt.transactionExecuted -eq $false -and $whatIfReceipt.atomicBatch -eq $false -and
        [string]$whatIfReceipt.transactionMode -ceq 'apply-not-executed' -and $whatIfReceipt.crashSafe -eq $false -and
        $whatIfReceipt.applied -eq $false -and $whatIfReceipt.changeCount -eq 2) 'WhatIf receipt falsely claimed an executed transaction'
    Assert (-not (Test-Path -LiteralPath $stableRefreshLock)) 'WhatIf created the stable refresh lock file'
    Assert ((Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')) -ceq $previewEntryHash) 'WhatIf mutated an entry'

    $apply = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($apply.exitCode -eq 0) ('atomic apply failed: ' + $apply.output)
    $receipt = $apply.output | ConvertFrom-Json
    Assert ($receipt.transactionExecuted -eq $true -and $receipt.atomicBatch -eq $true -and [string]$receipt.transactionMode -ceq 'locked-exception-rollback' -and
        $receipt.crashSafe -eq $false -and $receipt.applied -eq $true -and $receipt.changeCount -eq 2) 'transaction receipt is incomplete or overclaims crash safety'
    Assert (Test-Path -LiteralPath $stableRefreshLock -PathType Leaf) 'apply did not preserve the project-unique stable refresh lock file'
    foreach ($name in @('one', 'two')) {
        $actual = Hash (Join-Path $fixture "src/$name.txt")
        $steadyActual = Hash (Join-Path $fixture "src/steady-$name.txt")
        $expectedContentHash = ContentHash @($actual, $steadyActual)
        $entryText = Get-Content -LiteralPath (Join-Path $fixture "Documentation/AIKnowledge/entries/$name.md") -Raw -Encoding UTF8
        Assert ($entryText.Contains($actual)) "entry $name was not refreshed"
        Assert ($entryText.Contains($steadyActual)) "entry $name did not retain the complete SourceRef set"
        Assert ($entryText.Contains("- ``src/external-$name.txt`` (``$('0' * 64)``)")) "ExternalReferences in entry $name were modified"
        $indexText = Get-Content -LiteralPath (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml') -Raw -Encoding UTF8
        Assert ([regex]::IsMatch($indexText, "(?ms)^  - knowledgeId: fixture\.$name\.v1\r?`n    file: entries/$name\.md\r?`n    contentHash: $expectedContentHash(?:\r?`n|$)")) "index binding for $name was structurally damaged or not refreshed"
    }
    $indexText = Get-Content -LiteralPath (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml') -Raw -Encoding UTF8
    Assert (@([regex]::Matches($indexText, '(?m)^  - knowledgeId:')).Count -eq 2) 'index binding headers were merged or duplicated'
    Assert (-not [regex]::IsMatch($indexText, '(?m)^    contentHash: [0-9a-f]{64}[ \t]+- knowledgeId:')) 'index hash replacement consumed a line break'

    $repeatExport = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($repeatExport.exitCode -eq 0) 'repeat plan export failed'
    $repeat = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    $repeatReceipt = $repeat.output | ConvertFrom-Json
    Assert ($repeat.exitCode -eq 0 -and $repeatReceipt.changeCount -eq 0 -and $repeatReceipt.applied -eq $false -and
        $repeatReceipt.transactionExecuted -eq $false -and $repeatReceipt.atomicBatch -eq $false -and
        [string]$repeatReceipt.transactionMode -ceq 'apply-no-changes' -and $repeatReceipt.crashSafe -eq $false) 'repeat apply was not an idempotent no-transaction receipt'

    Write-Fixture
    $indexPath = Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml'
    $indexText = (Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8).Replace("`r`n", "`n").Replace("`n", "`r`n")
    Write-Utf8 $indexPath $indexText
    $crlfExport = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($crlfExport.exitCode -eq 0) ('CRLF plan export failed: ' + $crlfExport.output)
    $crlfApply = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($crlfApply.exitCode -eq 0) ('CRLF apply failed: ' + $crlfApply.output)
    $crlfIndex = Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8
    Assert (@([regex]::Matches($crlfIndex, '(?m)^  - knowledgeId:')).Count -eq 2) 'CRLF index binding headers were merged or duplicated'

    Write-Fixture
    $stalePlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($stalePlan.exitCode -eq 0) 'stale plan export failed'
    $entryBefore = Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')
    $indexBefore = Hash (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml')
    Write-Utf8 (Join-Path $fixture 'src/steady-two.txt') "changed-after-plan-without-a-drift-finding`n"
    $staleApply = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($staleApply.exitCode -eq 2) 'complete-set source drift was not rejected'
    $staleReceipt = $staleApply.output | ConvertFrom-Json
    Assert (@($staleReceipt.staleAtApply | Where-Object { $_ -match 'steady-two\.txt' }).Count -gt 0) 'complete-set rejection did not identify the unplanned source finding'
    Assert ((Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')) -ceq $entryBefore) 'stale batch partially changed an entry'
    Assert ((Hash (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml')) -ceq $indexBefore) 'stale batch partially changed the index'

    Write-Fixture -SecondEntryCurrent
    $nonTargetPlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($nonTargetPlan.exitCode -eq 0) 'non-target CAS plan export failed'
    $nonTargetPlanJson = $nonTargetPlan.output | ConvertFrom-Json
    Assert ($nonTargetPlanJson.findingCount -eq 1 -and $nonTargetPlanJson.findings[0].entry -match 'entries/one\.md$') 'fixture did not isolate one write target and one CAS-only Entry'
    $oneBeforeNonTargetDrift = Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')
    $indexBeforeNonTargetDrift = Hash (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml')
    $twoPath = Join-Path $fixture 'Documentation/AIKnowledge/entries/two.md'
    [IO.File]::AppendAllText($twoPath, "`nConcurrent non-target Entry edit.`n", $utf8)
    $twoConcurrentHash = Hash $twoPath
    $nonTargetApply = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($nonTargetApply.exitCode -eq 2) 'a CAS-only Entry changed after planning without rejecting the whole batch'
    $nonTargetReceipt = $nonTargetApply.output | ConvertFrom-Json
    Assert (@($nonTargetReceipt.staleAtApply | Where-Object { $_ -match 'plan-entry-drift.*entries/two\.md' }).Count -gt 0) 'CAS-only Entry drift was not identified'
    Assert ((Hash $twoPath) -ceq $twoConcurrentHash) 'CAS rejection overwrote the non-target Entry edit'
    Assert ((Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')) -ceq $oneBeforeNonTargetDrift) 'CAS-only Entry drift caused a target write'
    Assert ((Hash (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml')) -ceq $indexBeforeNonTargetDrift) 'CAS-only Entry drift caused an Index write'

    Write-Fixture
    $addedEntryPlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($addedEntryPlan.exitCode -eq 0) 'added-candidate baseline export failed'
    $oneBeforeAddedEntry = Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')
    $indexBeforeAddedEntry = Hash (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml')
    Write-Utf8 (Join-Path $fixture 'Documentation/AIKnowledge/entries/three.md') "``KnowledgeId``: ``fixture.three.v1`` `n"
    $addedEntryApply = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($addedEntryApply.exitCode -eq 2) 'plan accepted a post-plan Knowledge Entry addition'
    $addedEntryReceipt = $addedEntryApply.output | ConvertFrom-Json
    Assert (@($addedEntryReceipt.staleAtApply | Where-Object { $_ -match 'plan-entry-set-added.*entries/three\.md' }).Count -gt 0) 'post-plan Entry addition was not identified'
    Assert ((Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')) -ceq $oneBeforeAddedEntry) 'post-plan Entry addition caused a target write'
    Assert ((Hash (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml')) -ceq $indexBeforeAddedEntry) 'post-plan Entry addition caused an Index write'

    Write-Fixture
    $removedEntryPlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($removedEntryPlan.exitCode -eq 0) 'removed-candidate baseline export failed'
    $oneBeforeRemovedEntry = Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')
    $indexBeforeRemovedEntry = Hash (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml')
    $removedEntryPath = [IO.Path]::GetFullPath((Join-Path $fixture 'Documentation/AIKnowledge/entries/two.md'))
    Assert ($removedEntryPath.StartsWith(([IO.Path]::GetFullPath($fixture).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar), [StringComparison]::OrdinalIgnoreCase)) 'removed-candidate fixture path escaped its root'
    Remove-Item -LiteralPath $removedEntryPath -Force
    $removedEntryApply = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($removedEntryApply.exitCode -eq 2) 'plan accepted a post-plan Knowledge Entry deletion'
    $removedEntryReceipt = $removedEntryApply.output | ConvertFrom-Json
    Assert (@($removedEntryReceipt.staleAtApply | Where-Object { $_ -match 'plan-entry-set-missing.*entries/two\.md' }).Count -gt 0) 'post-plan Entry deletion was not identified'
    Assert ((Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')) -ceq $oneBeforeRemovedEntry) 'post-plan Entry deletion caused a target write'
    Assert ((Hash (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml')) -ceq $indexBeforeRemovedEntry) 'post-plan Entry deletion caused an Index write'

    Write-Fixture -SecondEntryCurrent
    $finalCasPlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($finalCasPlan.exitCode -eq 0) 'final-CAS baseline export failed'
    $applierSource = Get-Content -LiteralPath $applier -Raw -Encoding UTF8
    $casMarker = '    # ESKNOWLEDGE_FINAL_CAS_BARRIER'
    Assert ($applierSource.Contains($casMarker)) 'final-CAS instrumentation marker is missing'
    $casInjection = @'
    # ESKNOWLEDGE_FINAL_CAS_BARRIER
    $casInjectionMarker = Join-Path $root 'ES/Output/final-cas-injected.txt'
    if (-not (Test-Path -LiteralPath $casInjectionMarker)) {
        [IO.File]::WriteAllText($casInjectionMarker, 'injected', $strictUtf8)
        [IO.File]::AppendAllText((Join-Path $root 'Documentation/AIKnowledge/entries/two.md'), [Environment]::NewLine + 'Injected at final CAS.' + [Environment]::NewLine, $strictUtf8)
    }
'@
    $instrumentedApplier = Join-Path $fixture 'ES/Output/Invoke-ESKnowledgeStableRefresh.Instrumented.ps1'
    Write-Utf8 $instrumentedApplier $applierSource.Replace($casMarker, $casInjection.TrimEnd("`r", "`n"))
    $oneBeforeFinalCas = Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')
    $indexBeforeFinalCas = Hash (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml')
    $finalCasApply = Invoke-Script $instrumentedApplier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($finalCasApply.exitCode -eq 2) 'final CAS accepted a concurrent non-target Entry edit'
    $finalCasReceipt = $finalCasApply.output | ConvertFrom-Json
    Assert (@($finalCasReceipt.staleAtApply | Where-Object { $_ -match 'precommit-entry-drift.*entries/two\.md' }).Count -gt 0) 'final CAS did not identify the injected non-target Entry drift'
    Assert ((Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')) -ceq $oneBeforeFinalCas) 'final CAS drift caused a target write'
    Assert ((Hash (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml')) -ceq $indexBeforeFinalCas) 'final CAS drift caused an Index write'

    Write-Fixture -SecondEntryCurrent
    $finalSourceCasPlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($finalSourceCasPlan.exitCode -eq 0) 'final source-CAS baseline export failed'
    $finalSourceCasApplier = Write-InstrumentedApplier 'Invoke-ESKnowledgeStableRefresh.FinalSourceCas' @{
        '    # ESKNOWLEDGE_FINAL_CAS_BARRIER' = @'
    [IO.File]::WriteAllText((Join-Path $root 'src/steady-two.txt'), "injected-source-at-final-cas`n", $strictUtf8)
'@
    }
    $oneBeforeFinalSourceCas = Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')
    $indexBeforeFinalSourceCas = Hash (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml')
    $finalSourceCasApply = Invoke-Script $finalSourceCasApplier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($finalSourceCasApply.exitCode -eq 2) 'final CAS accepted a concurrent SourceRef edit'
    $finalSourceCasReceipt = $finalSourceCasApply.output | ConvertFrom-Json
    Assert (@($finalSourceCasReceipt.staleAtApply | Where-Object { $_ -match 'precommit-source-drift.*steady-two\.txt' }).Count -gt 0) 'final CAS did not identify the injected SourceRef drift'
    Assert ((Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')) -ceq $oneBeforeFinalSourceCas) 'final SourceRef CAS drift caused a target write'
    Assert ((Hash (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml')) -ceq $indexBeforeFinalSourceCas) 'final SourceRef CAS drift caused an Index write'

    Write-Fixture -SecondEntryCurrent
    $finalIndexCasPlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($finalIndexCasPlan.exitCode -eq 0) 'final index-CAS baseline export failed'
    $finalIndexCasApplier = Write-InstrumentedApplier 'Invoke-ESKnowledgeStableRefresh.FinalIndexCas' @{
        '    # ESKNOWLEDGE_FINAL_CAS_BARRIER' = @'
    [IO.File]::AppendAllText((Join-Path $root 'Documentation/AIKnowledge/KnowledgeIndex.yaml'), "`n# injected at final CAS`n", $strictUtf8)
'@
    }
    $oneBeforeFinalIndexCas = Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')
    $finalIndexCasApply = Invoke-Script $finalIndexCasApplier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($finalIndexCasApply.exitCode -eq 2) 'final CAS accepted a concurrent KnowledgeIndex edit'
    $finalIndexCasReceipt = $finalIndexCasApply.output | ConvertFrom-Json
    Assert (@($finalIndexCasReceipt.staleAtApply | Where-Object { $_ -match 'precommit-index-drift' }).Count -gt 0) 'final CAS did not identify the injected KnowledgeIndex drift'
    Assert ((Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')) -ceq $oneBeforeFinalIndexCas) 'final Index CAS drift caused a target write'

    Write-Fixture -SecondEntryCurrent
    $lockedRacePlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($lockedRacePlan.exitCode -eq 0) 'locked post-CAS race baseline export failed'
    $raceReady = Join-Path $fixture 'ES/Output/post-final-cas-ready.txt'
    $raceRelease = Join-Path $fixture 'ES/Output/post-final-cas-release.txt'
    $lockedRaceApplier = Write-InstrumentedApplier 'Invoke-ESKnowledgeStableRefresh.LockedRace' @{
        '            # ESKNOWLEDGE_POST_FINAL_CAS_BARRIER' = @'
            [IO.File]::WriteAllText((Join-Path $root 'ES/Output/post-final-cas-ready.txt'), 'ready', $strictUtf8)
            $testRelease = Join-Path $root 'ES/Output/post-final-cas-release.txt'
            $testDeadline = [DateTime]::UtcNow.AddSeconds(15)
            while (-not (Test-Path -LiteralPath $testRelease -PathType Leaf)) {
                if ([DateTime]::UtcNow -ge $testDeadline) { throw 'INJECTED_POST_FINAL_CAS_WAIT_TIMEOUT' }
                Start-Sleep -Milliseconds 25
            }
'@
    }
    $oneBeforeLockedRace = Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')
    $indexBeforeLockedRace = Hash (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml')
    $primaryRaceProcess = $null
    $primaryRaceResult = $null
    try {
        $primaryRaceProcess = Start-ScriptProcess $lockedRaceApplier @('-ProjectRoot', $fixture, '-Apply')
        Wait-ForPath $raceReady
        $competingApply = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
        Assert ($competingApply.exitCode -ne 0 -and $competingApply.output.Contains('Stable refresh lock unavailable')) 'a second applier acquired the project-unique lock'
        Assert ((Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')) -ceq $oneBeforeLockedRace) 'competing applier wrote an Entry inside the post-CAS window'
        Assert ((Hash (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml')) -ceq $indexBeforeLockedRace) 'competing applier wrote the Index inside the post-CAS window'
    }
    finally {
        Write-Utf8 $raceRelease 'release'
        if ($null -ne $primaryRaceProcess) { $primaryRaceResult = Complete-ScriptProcess $primaryRaceProcess 30000 }
    }
    Assert ($primaryRaceResult.exitCode -eq 0) ('the lock owner failed after the competing applier was rejected: ' + $primaryRaceResult.output)
    Assert (Test-Path -LiteralPath (Join-Path $fixture 'ES/Output/KnowledgeValidation/stable-refresh.lock') -PathType Leaf) 'locked apply deleted the persistent lock file'

    Write-Fixture
    $commitFailurePlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($commitFailurePlan.exitCode -eq 0) 'second-commit failure baseline export failed'
    $transactionPaths = @(
        (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md'),
        (Join-Path $fixture 'Documentation/AIKnowledge/entries/two.md'),
        (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml')
    )
    $transactionBaselineHashes = @{}
    foreach ($transactionPath in $transactionPaths) { $transactionBaselineHashes[$transactionPath] = Hash $transactionPath }
    $commitFailureApplier = Write-InstrumentedApplier 'Invoke-ESKnowledgeStableRefresh.CommitFailure' @{
        '            # ESKNOWLEDGE_BEFORE_COMMIT_REPLACE' = @'
            if ($commitOrdinal -eq 2) { [IO.File]::Delete([string]$item.temporary) }
'@
    }
    $commitFailureApply = Invoke-Script $commitFailureApplier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($commitFailureApply.exitCode -ne 0 -and $commitFailureApply.output.Contains('Stable refresh commit failed:') -and
        $commitFailureApply.output.Contains('Exception calling "Replace"') -and $commitFailureApply.output.Contains('rollback completed')) ('real File.Replace failure did not report a completed rollback: ' + $commitFailureApply.output)
    foreach ($transactionPath in $transactionPaths) {
        Assert ((Hash $transactionPath) -ceq [string]$transactionBaselineHashes[$transactionPath]) "second commit failure did not restore: $transactionPath"
    }
    $transactionArtifacts = @(Get-ChildItem -LiteralPath $fixture -Recurse -File | Where-Object { $_.Name -match '\.(?:tmp|bak|restore)-[0-9a-f]{32}$' })
    Assert ($transactionArtifacts.Count -eq 0) 'successful rollback left transaction artifacts'

    Write-Fixture
    $missingBackupPlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($missingBackupPlan.exitCode -eq 0) 'missing-backup baseline export failed'
    $missingBackupApplier = Write-InstrumentedApplier 'Invoke-ESKnowledgeStableRefresh.MissingBackup' @{
        '            # ESKNOWLEDGE_BEFORE_COMMIT_REPLACE' = @'
            if ($commitOrdinal -eq 3) { throw 'INJECTED_THIRD_COMMIT_FAILURE' }
'@
        '        # ESKNOWLEDGE_ROLLBACK_BATCH_BARRIER' = @'
        if ($rollbackCandidates.Count -gt 0) { [IO.File]::Delete([string]$rollbackCandidates[0].backup) }
'@
    }
    $missingBackupApply = Invoke-Script $missingBackupApplier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($missingBackupApply.exitCode -ne 0 -and $missingBackupApply.output.Contains('INJECTED_THIRD_COMMIT_FAILURE') -and
        $missingBackupApply.output.Contains('Rollback backup is missing') -and $missingBackupApply.output.Contains('rollback is incomplete')) 'missing rollback backup was not diagnosed with the primary commit error'
    $preservedBackups = @(Get-ChildItem -LiteralPath $fixture -Recurse -File | Where-Object { $_.Name -match '\.bak-[0-9a-f]{32}$' })
    Assert ($preservedBackups.Count -gt 0) 'incomplete rollback did not preserve remaining backups'

    Write-Fixture
    $rollbackFailurePlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($rollbackFailurePlan.exitCode -eq 0) 'rollback-failure baseline export failed'
    $rollbackFailureApplier = Write-InstrumentedApplier 'Invoke-ESKnowledgeStableRefresh.RollbackFailure' @{
        '            # ESKNOWLEDGE_BEFORE_COMMIT_REPLACE' = @'
            if ($commitOrdinal -eq 3) { throw 'INJECTED_THIRD_COMMIT_FAILURE' }
'@
        '                # ESKNOWLEDGE_BEFORE_ROLLBACK_RESTORE' = @'
                if ($rollbackIndex -eq ($rollbackCandidates.Count - 1)) { throw 'INJECTED_ROLLBACK_FAILURE' }
'@
    }
    $rollbackFailureApply = Invoke-Script $rollbackFailureApplier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($rollbackFailureApply.exitCode -ne 0 -and $rollbackFailureApply.output.Contains('INJECTED_THIRD_COMMIT_FAILURE') -and
        $rollbackFailureApply.output.Contains('INJECTED_ROLLBACK_FAILURE') -and $rollbackFailureApply.output.Contains('rollback is incomplete')) 'rollback failure did not retain both failure causes'
    $rollbackFailureBackups = @(Get-ChildItem -LiteralPath $fixture -Recurse -File | Where-Object { $_.Name -match '\.bak-[0-9a-f]{32}$' })
    Assert ($rollbackFailureBackups.Count -gt 0) 'rollback failure did not preserve backups'

    Write-Fixture
    $postRestoreFailurePlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($postRestoreFailurePlan.exitCode -eq 0) 'post-restore verification baseline export failed'
    $postRestoreBaselineHashes = @{}
    foreach ($transactionPath in $transactionPaths) { $postRestoreBaselineHashes[$transactionPath] = Hash $transactionPath }
    $postRestoreFailureApplier = Write-InstrumentedApplier 'Invoke-ESKnowledgeStableRefresh.PostRestoreFailure' @{
        '            # ESKNOWLEDGE_BEFORE_COMMIT_REPLACE' = @'
            if ($commitOrdinal -eq 3) { throw 'INJECTED_THIRD_COMMIT_FAILURE' }
'@
        '                # ESKNOWLEDGE_AFTER_ROLLBACK_RESTORE_BARRIER' = @'
                if ($rollbackIndex -eq ($rollbackCandidates.Count - 1)) {
                    [IO.File]::AppendAllText([string]$item.path, "`nINJECTED_ROLLBACK_TARGET_TAMPER`n", $strictUtf8)
                }
'@
    }
    $postRestoreFailureApply = Invoke-Script $postRestoreFailureApplier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($postRestoreFailureApply.exitCode -ne 0 -and $postRestoreFailureApply.output.Contains('INJECTED_THIRD_COMMIT_FAILURE') -and
        $postRestoreFailureApply.output.Contains('Rollback hash mismatch') -and $postRestoreFailureApply.output.Contains('rollback is incomplete')) 'post-restore hash failure obscured the primary error or claimed a complete rollback'
    $postRestoreTamperedTarget = $transactionPaths[1]
    $postRestoreTamperedText = Get-Content -LiteralPath $postRestoreTamperedTarget -Raw -Encoding UTF8
    Assert ($postRestoreTamperedText.Contains('INJECTED_ROLLBACK_TARGET_TAMPER') -and
        (Hash $postRestoreTamperedTarget) -cne [string]$postRestoreBaselineHashes[$postRestoreTamperedTarget]) 'post-restore verification failure did not preserve the recovery scene'
    $postRestoreBackups = @(Get-ChildItem -LiteralPath $fixture -Recurse -File | Where-Object { $_.Name -match '\.bak-[0-9a-f]{32}$' })
    Assert ($postRestoreBackups.Count -gt 0) 'post-restore verification failure did not preserve rollback backups'

    Write-Fixture
    $cleanupFailurePlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($cleanupFailurePlan.exitCode -eq 0) 'cleanup-failure baseline export failed'
    $cleanupFailureApplier = Write-InstrumentedApplier 'Invoke-ESKnowledgeStableRefresh.CleanupFailure' @{
        '            # ESKNOWLEDGE_BEFORE_COMMIT_REPLACE' = @'
            if ($commitOrdinal -eq 2) { throw 'INJECTED_SECOND_COMMIT_FAILURE' }
'@
        '                # ESKNOWLEDGE_BEFORE_CLEANUP_ARTIFACT' = @'
                if ($artifact.kind -eq 'temporary') { throw 'INJECTED_CLEANUP_FAILURE' }
'@
    }
    $cleanupFailureApply = Invoke-Script $cleanupFailureApplier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($cleanupFailureApply.exitCode -ne 0 -and $cleanupFailureApply.output.Contains('INJECTED_SECOND_COMMIT_FAILURE') -and
        $cleanupFailureApply.output.Contains('INJECTED_CLEANUP_FAILURE') -and $cleanupFailureApply.output.Contains('cleanup failures')) 'cleanup failure obscured the primary commit error'

    Write-Fixture
    $sourceSetPlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($sourceSetPlan.exitCode -eq 0) 'source-set plan export failed'
    $addedSource = Join-Path $fixture 'src/added-one.txt'
    Write-Utf8 $addedSource "added-after-plan`n"
    $addedHash = Hash $addedSource
    $entryPath = Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md'
    $entryText = Get-Content -LiteralPath $entryPath -Raw -Encoding UTF8
    $entryText = $entryText.Replace("## ExternalReferences", "- ``src/added-one.txt`` (``$addedHash``)`n`n## ExternalReferences")
    Write-Utf8 $entryPath $entryText
    $addedEntryHash = Hash $entryPath
    $sourceSetPreview = Invoke-Script $applier @('-ProjectRoot', $fixture)
    Assert ($sourceSetPreview.exitCode -eq 2) ('preview accepted a post-plan SourceRef addition: ' + $sourceSetPreview.output)
    $sourceSetPreviewReceipt = $sourceSetPreview.output | ConvertFrom-Json
    Assert (@($sourceSetPreviewReceipt.staleAtApply | Where-Object { $_ -match 'plan-source-set-drift' }).Count -gt 0) 'preview did not report SourceRef set drift'
    Assert ((Hash $entryPath) -ceq $addedEntryHash) 'source-set preview overwrote the concurrent entry'
    $sourceSetApply = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($sourceSetApply.exitCode -eq 2) 'apply accepted a post-plan SourceRef addition'
    Assert ((Hash $entryPath) -ceq $addedEntryHash) 'source-set apply overwrote the concurrent entry'

    Write-Fixture
    $declaredHashPlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($declaredHashPlan.exitCode -eq 0) 'declared-hash plan export failed'
    $entryPath = Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md'
    $entryText = Get-Content -LiteralPath $entryPath -Raw -Encoding UTF8
    $steadyHash = Hash (Join-Path $fixture 'src/steady-one.txt')
    Write-Utf8 $entryPath $entryText.Replace($steadyHash, ('1' * 64))
    $declaredHashPreview = Invoke-Script $applier @('-ProjectRoot', $fixture)
    Assert ($declaredHashPreview.exitCode -eq 2) 'preview accepted a post-plan declared SourceRef hash change'
    $declaredHashReceipt = $declaredHashPreview.output | ConvertFrom-Json
    Assert (@($declaredHashReceipt.staleAtApply | Where-Object { $_ -match 'plan-source-set-drift' }).Count -gt 0) 'declared SourceRef hash drift was not reported as source-set drift'

    Write-Fixture
    $entryPath = Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md'
    $entryText = Get-Content -LiteralPath $entryPath -Raw -Encoding UTF8
    $entryText = [regex]::Replace($entryText, '(?ms)^## SourceRefs\s*.*?(?=^## ExternalReferences)', '')
    Write-Utf8 $entryPath $entryText
    $missingSection = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($missingSection.exitCode -eq 2) 'exporter silently accepted a target entry without SourceRefs'
    $missingSectionPlan = $missingSection.output | ConvertFrom-Json
    Assert ($missingSectionPlan.planStatus -eq 'blocked' -and @($missingSectionPlan.findings | Where-Object code -eq 'SOURCE_REFS_SECTION_MISSING').Count -eq 1) 'missing SourceRefs did not yield a plan blocker'
    $missingSectionPreview = Invoke-Script $applier @('-ProjectRoot', $fixture)
    Assert ($missingSectionPreview.exitCode -eq 2) 'stable refresh accepted a structurally blocked plan'

    Write-Fixture
    $entryPath = Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md'
    $entryText = Get-Content -LiteralPath $entryPath -Raw -Encoding UTF8
    $entryText = $entryText.Replace("- ``src/one.txt`` (``$('0' * 64)``)", "- src/one.txt ($('0' * 64))")
    Write-Utf8 $entryPath $entryText
    $malformedBullet = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($malformedBullet.exitCode -eq 2) 'exporter silently ignored a malformed SourceRef bullet'
    $malformedPlan = $malformedBullet.output | ConvertFrom-Json
    Assert (@($malformedPlan.findings | Where-Object code -eq 'SOURCE_REF_BULLET_MALFORMED').Count -eq 1) 'malformed SourceRef bullet did not yield a blocker'

    Write-Fixture
    $entryPath = Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md'
    $entryText = Get-Content -LiteralPath $entryPath -Raw -Encoding UTF8
    $entryText = $entryText.Replace('src/one.txt', '../outside.txt')
    Write-Utf8 $entryPath $entryText
    $traversalPlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($traversalPlan.exitCode -eq 2) 'exporter accepted a traversing SourceRef'
    $traversalPlanJson = $traversalPlan.output | ConvertFrom-Json
    Assert (@($traversalPlanJson.findings | Where-Object { $_.code -eq 'SOURCE_REF_INVALID' -and $_.reason -match 'project-relative' }).Count -eq 1) 'traversing SourceRef did not yield a containment blocker'
    $traversalPreview = Invoke-Script $applier @('-ProjectRoot', $fixture)
    Assert ($traversalPreview.exitCode -eq 2) 'stable refresh did not preserve the traversing SourceRef blocker'
    $traversalReceipt = $traversalPreview.output | ConvertFrom-Json
    Assert (@($traversalReceipt.staleAtApply | Where-Object { $_ -match 'plan-blocker SOURCE_REF_INVALID' }).Count -eq 1) 'traversal blocker was not represented in the stable refresh receipt'

    Write-Fixture
    $entryPath = Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md'
    $entryText = Get-Content -LiteralPath $entryPath -Raw -Encoding UTF8
    Write-Utf8 $entryPath $entryText.Replace('fixture.one.v1', 'fixture.wrong.v1')
    $canonicalIdentityPlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($canonicalIdentityPlan.exitCode -eq 2) 'exporter accepted a canonical Entry/Index KnowledgeId mismatch'
    $canonicalIdentityJson = $canonicalIdentityPlan.output | ConvertFrom-Json
    Assert (@($canonicalIdentityJson.findings | Where-Object code -eq 'INDEX_BINDING_IDENTITY_INVALID').Count -gt 0) 'canonical KnowledgeId mismatch did not yield an identity blocker'

    Write-Fixture
    $indexPath = Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml'
    $indexText = Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8
    $oneContentHash = [regex]::Match($indexText, '(?ms)^  - knowledgeId: fixture\.one\.v1.*?^    contentHash: (?<hash>[0-9a-f]{64})\s*$').Groups['hash'].Value
    Write-Utf8 $indexPath ($indexText + "  - knowledgeId: fixture.one.extra.v1`n    file: entries/one.md`n    contentHash: $oneContentHash`n")
    $duplicateFilePlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($duplicateFilePlan.exitCode -eq 2) 'exporter accepted multiple canonical bindings for one Entry file'
    $duplicateFileJson = $duplicateFilePlan.output | ConvertFrom-Json
    Assert (@($duplicateFileJson.findings | Where-Object code -eq 'INDEX_BINDING_IDENTITY_INVALID').Count -gt 0) 'duplicate same-file binding did not yield an identity blocker'

    Write-Fixture
    $indexPath = Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml'
    $indexText = Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8
    Write-Utf8 $indexPath $indexText.Replace('fixture.two.v1', 'fixture.one.v1')
    $duplicateIdPlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($duplicateIdPlan.exitCode -eq 2) 'exporter accepted a globally duplicated KnowledgeIndex identity'
    $duplicateIdJson = $duplicateIdPlan.output | ConvertFrom-Json
    Assert (@($duplicateIdJson.findings | Where-Object code -eq 'INDEX_IDENTITY_INVALID').Count -gt 0) 'global duplicate KnowledgeId did not yield an Index identity blocker'

    Write-SharedFixture
    $sharedPlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($sharedPlan.exitCode -eq 0) ('valid SharedRouteProjection export failed: ' + $sharedPlan.output)
    $sharedPlanJson = $sharedPlan.output | ConvertFrom-Json
    Assert ($sharedPlanJson.planStatus -eq 'ready' -and $sharedPlanJson.entrySnapshots.Count -eq 1) 'valid SharedRouteProjection did not produce one ready snapshot'
    Assert ([string]$sharedPlanJson.entrySnapshots[0].entryMode -ceq 'SharedRouteProjection') 'SharedRouteProjection mode was not plan-bound'
    Assert ((@($sharedPlanJson.entrySnapshots[0].indexBindingIds) -join ',') -ceq 'fixture.shared.a.v1,fixture.shared.b.v1') 'SharedRouteProjection binding identities were not complete and Ordinal-sorted'
    $sharedApply = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($sharedApply.exitCode -eq 0) ('valid SharedRouteProjection apply failed: ' + $sharedApply.output)
    $sharedIndex = Get-Content -LiteralPath (Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml') -Raw -Encoding UTF8
    $sharedCurrentHash = Hash (Join-Path $fixture 'src/shared.txt')
    $sharedContentHash = ContentHash @($sharedCurrentHash)
    Assert (@([regex]::Matches($sharedIndex, "(?m)^    contentHash: $sharedContentHash(?:\r?$)")).Count -eq 2) 'SharedRouteProjection did not update every exact Index binding'

    Write-SharedFixture
    $sharedEntryPath = Join-Path $fixture 'Documentation/AIKnowledge/entries/shared.md'
    $sharedEntryText = Get-Content -LiteralPath $sharedEntryPath -Raw -Encoding UTF8
    Write-Utf8 $sharedEntryPath $sharedEntryText.Replace("- ``fixture.shared.b.v1``: ``route-b``", '')
    $missingProjectionPlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($missingProjectionPlan.exitCode -eq 2) 'exporter accepted a SharedRouteProjection missing one binding declaration'
    $missingProjectionJson = $missingProjectionPlan.output | ConvertFrom-Json
    Assert (@($missingProjectionJson.findings | Where-Object { $_.code -in @('ROUTE_PROJECTION_IDENTITY_INVALID', 'INDEX_BINDING_IDENTITY_INVALID') }).Count -gt 0) 'missing SharedRouteProjection identity did not yield a blocker'

    $v2Current = Write-V2Fixture -Current
    $v2CurrentExport = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($v2CurrentExport.exitCode -eq 0) ('valid no-drift v2 export failed: ' + $v2CurrentExport.output)
    $v2CurrentPlan = $v2CurrentExport.output | ConvertFrom-Json
    Assert ($v2CurrentPlan.planStatus -eq 'ready' -and $v2CurrentPlan.blockerCount -eq 0 -and $v2CurrentPlan.findingCount -eq 0) 'valid no-drift v2 Entry was blocked'
    $v2CurrentSnapshot = $v2CurrentPlan.entrySnapshots[0]
    Assert ([string]$v2CurrentSnapshot.hashSchema -ceq 'v2') 'v2 HashSchema was not plan-bound'
    Assert ([string]$v2CurrentSnapshot.declaredContentHash -ceq [string]$v2Current.sourceSetHash) 'v2 declared ContentHash was not plan-bound'
    Assert ([string]$v2CurrentSnapshot.declaredSourceSetHash -ceq [string]$v2Current.sourceSetHash) 'v2 declared SourceSetHash was not plan-bound'
    Assert ([string]$v2CurrentSnapshot.declaredEntryBodyHash -ceq [string]$v2Current.entryBodyHash) 'v2 declared EntryBodyHash was not plan-bound'
    Assert ([string]$v2CurrentSnapshot.expectedContentHash -ceq [string]$v2Current.sourceSetHash -and [string]$v2CurrentSnapshot.expectedSourceSetHash -ceq [string]$v2Current.sourceSetHash) 'v2 expected source-set projection was not plan-bound'
    Assert ([string]$v2CurrentSnapshot.expectedEntryBodyHash -ceq [string]$v2Current.entryBodyHash) 'v2 expected EntryBodyHash was not plan-bound'
    Assert (@($v2CurrentSnapshot.indexBindings).Count -eq 1 -and [string]$v2CurrentSnapshot.indexBindings[0].entryBodyHash -ceq [string]$v2Current.entryBodyHash) 'v2 Index projection was not plan-bound'
    Assert ([string]$v2CurrentSnapshot.indexBindings[0].expectedContentHash -ceq [string]$v2CurrentSnapshot.expectedContentHash -and
        [string]$v2CurrentSnapshot.indexBindings[0].expectedSourceSetHash -ceq [string]$v2CurrentSnapshot.expectedSourceSetHash -and
        [string]$v2CurrentSnapshot.indexBindings[0].expectedEntryBodyHash -ceq [string]$v2CurrentSnapshot.expectedEntryBodyHash) 'v2 expected Index projection was not plan-bound'
    $v2CurrentEntryBefore = Hash $v2Current.entryPath
    $v2CurrentIndexBefore = Hash $v2Current.indexPath
    $v2CurrentApply = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    $v2CurrentReceipt = $v2CurrentApply.output | ConvertFrom-Json
    Assert ($v2CurrentApply.exitCode -eq 0 -and $v2CurrentReceipt.changeCount -eq 0 -and $v2CurrentReceipt.applied -eq $false) 'no-drift v2 apply was not idempotent'
    Assert ((Hash $v2Current.entryPath) -ceq $v2CurrentEntryBefore -and (Hash $v2Current.indexPath) -ceq $v2CurrentIndexBefore) 'no-drift v2 apply mutated Knowledge'

    $v2Drift = Write-V2Fixture
    $v2DriftExport = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($v2DriftExport.exitCode -eq 0) ('v2 drift export failed: ' + $v2DriftExport.output)
    $v2DriftPlan = $v2DriftExport.output | ConvertFrom-Json
    Assert ($v2DriftPlan.planStatus -eq 'ready' -and @($v2DriftPlan.findings | Where-Object code -eq 'SOURCE_HASH_DRIFT').Count -eq 1) 'v2 source drift did not produce one review finding'
    $v2EntryBeforePreview = Hash $v2Drift.entryPath
    $v2IndexBeforePreview = Hash $v2Drift.indexPath
    $v2Preview = Invoke-Script $applier @('-ProjectRoot', $fixture)
    $v2PreviewReceipt = $v2Preview.output | ConvertFrom-Json
    Assert ($v2Preview.exitCode -eq 0 -and $v2PreviewReceipt.changeCount -eq 1 -and $v2PreviewReceipt.applied -eq $false) ('v2 drift preview failed: ' + $v2Preview.output)
    Assert ((Hash $v2Drift.entryPath) -ceq $v2EntryBeforePreview -and (Hash $v2Drift.indexPath) -ceq $v2IndexBeforePreview) 'v2 preview mutated Knowledge'
    $v2Apply = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($v2Apply.exitCode -eq 0) ('v2 drift apply failed: ' + $v2Apply.output)
    $v2Receipt = $v2Apply.output | ConvertFrom-Json
    Assert ($v2Receipt.transactionExecuted -eq $true -and $v2Receipt.atomicBatch -eq $true -and [string]$v2Receipt.transactionMode -ceq 'locked-exception-rollback' -and
        $v2Receipt.crashSafe -eq $false -and $v2Receipt.applied -eq $true -and $v2Receipt.changeCount -eq 1) 'v2 apply receipt is incomplete or overclaims crash safety'
    $v2CurrentSourceHash = Hash $v2Drift.sourcePath
    $v2ExpectedSourceSetHash = ContentHash @($v2CurrentSourceHash)
    $v2EntryText = Get-Content -LiteralPath $v2Drift.entryPath -Raw -Encoding UTF8
    $v2EntryContentHash = [regex]::Match($v2EntryText, '(?m)^`ContentHash`\s*:\s*`(?<hash>[0-9a-f]{64})`\s*$').Groups['hash'].Value
    $v2EntrySourceSetHash = [regex]::Match($v2EntryText, '(?m)^`SourceSetHash`\s*:\s*`(?<hash>[0-9a-f]{64})`\s*$').Groups['hash'].Value
    $v2EntryBodyHash = [regex]::Match($v2EntryText, '(?m)^`EntryBodyHash`\s*:\s*`(?<hash>[0-9a-f]{64})`\s*$').Groups['hash'].Value
    Assert ($v2EntryText.Contains($v2CurrentSourceHash)) 'v2 SourceRef hash was not refreshed'
    Assert ($v2EntryContentHash -ceq $v2ExpectedSourceSetHash -and $v2EntrySourceSetHash -ceq $v2ExpectedSourceSetHash) 'v2 Entry ContentHash/SourceSetHash did not close'
    Assert ($v2EntryBodyHash -ceq (EntryBodyHash $v2EntryText)) 'v2 EntryBodyHash did not converge after metadata replacement'
    $v2IndexText = Get-Content -LiteralPath $v2Drift.indexPath -Raw -Encoding UTF8
    Assert ([regex]::IsMatch($v2IndexText, "(?m)^    contentHash: $v2ExpectedSourceSetHash(?:\r?`$)")) 'v2 Index contentHash was not refreshed'
    Assert ([regex]::IsMatch($v2IndexText, "(?m)^    sourceSetHash: $v2ExpectedSourceSetHash(?:\r?`$)")) 'v2 Index sourceSetHash was not refreshed'
    Assert ([regex]::IsMatch($v2IndexText, "(?m)^    entryBodyHash: $v2EntryBodyHash(?:\r?`$)")) 'v2 Index entryBodyHash was not refreshed'

    $v2Tamper = Write-V2Fixture
    $v2TamperExport = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($v2TamperExport.exitCode -eq 0) 'v2 plan-tamper baseline export failed'
    $v2PlanPath = Join-Path $fixture 'ES/Output/KnowledgeValidation/refresh-plan.json'
    $v2TamperedPlan = Get-Content -LiteralPath $v2PlanPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $v2TamperedPlan.entrySnapshots[0].indexBindings[0].entryBodyHash = 'e' * 64
    Write-Utf8 $v2PlanPath ($v2TamperedPlan | ConvertTo-Json -Depth 12)
    $v2TamperedApply = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($v2TamperedApply.exitCode -ne 0 -and $v2TamperedApply.output.Contains('plan hash mismatch')) 'tampered v2 Index projection was not plan-hash rejected'

    foreach ($projectionCase in @(
        [pscustomobject]@{ scope = 'entry'; field = 'expectedContentHash'; value = 'a' * 64; rejection = 'Refresh plan expected ContentHash mismatch' },
        [pscustomobject]@{ scope = 'entry'; field = 'expectedSourceSetHash'; value = 'b' * 64; rejection = 'Refresh plan expected SourceSetHash mismatch' },
        [pscustomobject]@{ scope = 'entry'; field = 'expectedEntryBodyHash'; value = 'c' * 64; rejection = 'plan-expected-entry-body-hash-mismatch' },
        [pscustomobject]@{ scope = 'binding'; field = 'expectedContentHash'; value = 'd' * 64; rejection = 'expected Index projection does not match the Entry' },
        [pscustomobject]@{ scope = 'binding'; field = 'expectedSourceSetHash'; value = 'e' * 64; rejection = 'expected Index projection does not match the Entry' },
        [pscustomobject]@{ scope = 'binding'; field = 'expectedEntryBodyHash'; value = 'f' * 64; rejection = 'expected Index projection does not match the Entry' }
    )) {
        [void](Write-V2Fixture)
        $projectionBaseline = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
        Assert ($projectionBaseline.exitCode -eq 0) "expected projection tamper baseline failed: $($projectionCase.scope)/$($projectionCase.field)"
        $projectionPlan = Get-Content -LiteralPath $v2PlanPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($projectionCase.scope -eq 'entry') {
            $projectionPlan.entrySnapshots[0].($projectionCase.field) = $projectionCase.value
            foreach ($binding in @($projectionPlan.entrySnapshots[0].indexBindings)) {
                $binding.($projectionCase.field) = $projectionCase.value
            }
        }
        else {
            $projectionPlan.entrySnapshots[0].indexBindings[0].($projectionCase.field) = $projectionCase.value
        }
        $projectionPlan.planHash = Get-CanonicalPlanHash $projectionPlan
        Write-Utf8 $v2PlanPath ($projectionPlan | ConvertTo-Json -Depth 12)
        $projectionRejected = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
        Assert ($projectionRejected.exitCode -ne 0 -and -not $projectionRejected.output.Contains('plan hash mismatch') -and
            $projectionRejected.output.Contains([string]$projectionCase.rejection)) "re-signed $($projectionCase.scope)/$($projectionCase.field) tamper was not independently rejected: $($projectionRejected.output)"
    }

    $v2UppercaseEntry = Write-V2Fixture -Current
    $v2UppercaseEntryText = Get-Content -LiteralPath $v2UppercaseEntry.entryPath -Raw -Encoding UTF8
    $v2SourceSetLine = '`SourceSetHash`: `' + [string]$v2UppercaseEntry.sourceSetHash + '`'
    $v2UppercaseSourceSetLine = '`SourceSetHash`: `' + ([string]$v2UppercaseEntry.sourceSetHash).ToUpperInvariant() + '`'
    Write-Utf8 $v2UppercaseEntry.entryPath $v2UppercaseEntryText.Replace($v2SourceSetLine, $v2UppercaseSourceSetLine)
    $v2UppercaseEntryExport = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($v2UppercaseEntryExport.exitCode -eq 2) 'exporter accepted uppercase v2 Entry metadata hash'
    $v2UppercaseEntryPlan = $v2UppercaseEntryExport.output | ConvertFrom-Json
    Assert (@($v2UppercaseEntryPlan.findings | Where-Object code -eq 'ENTRY_HASH_SCHEMA_PARTIAL').Count -gt 0) 'uppercase v2 Entry metadata did not fail the lowercase contract'

    $v2UppercaseIndex = Write-V2Fixture -Current
    $v2UppercaseIndexText = Get-Content -LiteralPath $v2UppercaseIndex.indexPath -Raw -Encoding UTF8
    $v2IndexSourceSetLine = '    sourceSetHash: ' + [string]$v2UppercaseIndex.sourceSetHash
    $v2UppercaseIndexSourceSetLine = '    sourceSetHash: ' + ([string]$v2UppercaseIndex.sourceSetHash).ToUpperInvariant()
    Write-Utf8 $v2UppercaseIndex.indexPath $v2UppercaseIndexText.Replace($v2IndexSourceSetLine, $v2UppercaseIndexSourceSetLine)
    $v2UppercaseIndexExport = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($v2UppercaseIndexExport.exitCode -eq 2) 'exporter accepted uppercase v2 Index metadata hash'
    $v2UppercaseIndexPlan = $v2UppercaseIndexExport.output | ConvertFrom-Json
    Assert (@($v2UppercaseIndexPlan.findings | Where-Object code -eq 'INDEX_BINDING_INVALID').Count -gt 0) 'uppercase v2 Index metadata did not fail the lowercase contract'

    $v2EntryStale = Write-V2Fixture
    $v2EntryStaleExport = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($v2EntryStaleExport.exitCode -eq 0) 'v2 entry-stale baseline export failed'
    [IO.File]::AppendAllText($v2EntryStale.entryPath, "`nConcurrent v2 entry edit.`n", $utf8)
    $v2EntryStaleApply = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    $v2EntryStaleReceipt = $v2EntryStaleApply.output | ConvertFrom-Json
    Assert ($v2EntryStaleApply.exitCode -eq 2 -and @($v2EntryStaleReceipt.staleAtApply | Where-Object { $_ -match 'plan-entry-drift' }).Count -gt 0) 'tampered v2 Entry was not stale-rejected'

    $v2IndexStale = Write-V2Fixture
    $v2IndexStaleExport = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($v2IndexStaleExport.exitCode -eq 0) 'v2 index-stale baseline export failed'
    $v2IndexText = Get-Content -LiteralPath $v2IndexStale.indexPath -Raw -Encoding UTF8
    Write-Utf8 $v2IndexStale.indexPath $v2IndexText.Replace([string]$v2IndexStale.entryBodyHash, ('d' * 64))
    $v2IndexStaleApply = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    $v2IndexStaleReceipt = $v2IndexStaleApply.output | ConvertFrom-Json
    Assert ($v2IndexStaleApply.exitCode -eq 2 -and @($v2IndexStaleReceipt.staleAtApply | Where-Object { $_ -match 'plan-index-drift' }).Count -gt 0) 'tampered v2 Index was not stale-rejected'

    $v2SourceStale = Write-V2Fixture
    $v2SourceStaleExport = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($v2SourceStaleExport.exitCode -eq 0) 'v2 source-stale baseline export failed'
    Write-Utf8 $v2SourceStale.sourcePath "changed-after-v2-plan`n"
    $v2SourceStaleApply = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    $v2SourceStaleReceipt = $v2SourceStaleApply.output | ConvertFrom-Json
    Assert ($v2SourceStaleApply.exitCode -eq 2 -and @($v2SourceStaleReceipt.staleAtApply | Where-Object { $_ -match 'plan-source-drift' }).Count -gt 0) 'tampered v2 SourceRef source was not stale-rejected'

    Write-Fixture
    $entryPath = Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md'
    $entryText = Get-Content -LiteralPath $entryPath -Raw -Encoding UTF8
    $partialV2Fields = "``SourceSetHash``: ``$('1' * 64)```n``EvidenceLevel``"
    Write-Utf8 $entryPath $entryText.Replace('`EvidenceLevel`', $partialV2Fields)
    $partialV2Plan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($partialV2Plan.exitCode -eq 2) ('exporter accepted partial v2 metadata without HashSchema: ' + $partialV2Plan.output)
    $partialV2Json = $partialV2Plan.output | ConvertFrom-Json
    Assert (@($partialV2Json.findings | Where-Object code -eq 'ENTRY_HASH_SCHEMA_PARTIAL').Count -eq 1) 'partial v2 metadata did not yield the dedicated blocker'

    Write-Fixture
    $entryPath = Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md'
    $entryText = Get-Content -LiteralPath $entryPath -Raw -Encoding UTF8
    Write-Utf8 $entryPath $entryText.Replace('`EvidenceLevel`', "``HashSchema``: ``v3```n``EvidenceLevel``")
    $unsupportedSchemaPlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($unsupportedSchemaPlan.exitCode -eq 2) 'exporter accepted an unsupported Entry HashSchema'
    $unsupportedSchemaJson = $unsupportedSchemaPlan.output | ConvertFrom-Json
    Assert (@($unsupportedSchemaJson.findings | Where-Object code -eq 'ENTRY_HASH_SCHEMA_INVALID').Count -eq 1) 'unsupported Entry HashSchema did not fail closed'

    Write-Fixture
    $entryBaselinePlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($entryBaselinePlan.exitCode -eq 0) 'entry-baseline plan export failed'
    $entryPath = Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md'
    $entryText = Get-Content -LiteralPath $entryPath -Raw -Encoding UTF8
    Write-Utf8 $entryPath ($entryText + "`nConcurrent entry edit.`n")
    $concurrentEntryHash = Hash $entryPath
    $entryBaselineApply = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($entryBaselineApply.exitCode -eq 2) 'entry drift after planning was not rejected'
    Assert ((Hash $entryPath) -ceq $concurrentEntryHash) 'entry drift rejection overwrote the concurrent edit'

    Write-Fixture
    $indexBaselinePlan = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($indexBaselinePlan.exitCode -eq 0) 'index-baseline plan export failed'
    $indexPath = Join-Path $fixture 'Documentation/AIKnowledge/KnowledgeIndex.yaml'
    $indexText = Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8
    Write-Utf8 $indexPath ($indexText + "`n# concurrent index edit`n")
    $concurrentIndexHash = Hash $indexPath
    $entryBeforeIndexDrift = Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')
    $indexBaselineApply = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($indexBaselineApply.exitCode -eq 2) 'index drift after planning was not rejected'
    Assert ((Hash $indexPath) -ceq $concurrentIndexHash) 'index drift rejection overwrote the concurrent edit'
    Assert ((Hash (Join-Path $fixture 'Documentation/AIKnowledge/entries/one.md')) -ceq $entryBeforeIndexDrift) 'index drift caused a partial entry update'

    Write-Fixture
    $tamperBaseline = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($tamperBaseline.exitCode -eq 0) 'plan-tamper baseline export failed'
    $planPath = Join-Path $fixture 'ES/Output/KnowledgeValidation/refresh-plan.json'
    $plan = Get-Content -LiteralPath $planPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $plan.entrySnapshots[0].sourceRefs[0].currentHash = 'f' * 64
    Write-Utf8 $planPath ($plan | ConvertTo-Json -Depth 12)
    $tampered = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($tampered.exitCode -ne 0 -and $tampered.output.Contains('plan hash mismatch')) 'tampered complete SourceRef snapshot was not plan-hash rejected'

    Write-Fixture
    $knowledgeIdTamperBaseline = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($knowledgeIdTamperBaseline.exitCode -eq 0) 'KnowledgeId tamper baseline export failed'
    $plan = Get-Content -LiteralPath $planPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $plan.entrySnapshots[0].knowledgeId = 'fixture.tampered.v1'
    Write-Utf8 $planPath ($plan | ConvertTo-Json -Depth 12)
    $knowledgeIdTampered = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($knowledgeIdTampered.exitCode -ne 0 -and $knowledgeIdTampered.output.Contains('plan hash mismatch')) 'tampered KnowledgeId was not plan-hash rejected'

    Write-Fixture
    $bindingTamperBaseline = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($bindingTamperBaseline.exitCode -eq 0) 'binding tamper baseline export failed'
    $plan = Get-Content -LiteralPath $planPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $plan.entrySnapshots[0].indexBindingIds[0] = 'fixture.tampered.v1'
    Write-Utf8 $planPath ($plan | ConvertTo-Json -Depth 12)
    $bindingTampered = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($bindingTampered.exitCode -ne 0 -and $bindingTampered.output.Contains('plan hash mismatch')) 'tampered indexBindingIds were not plan-hash rejected'

    Write-Fixture
    $requiredFieldBaseline = Invoke-Script $exporter @('-ProjectRoot', $fixture, '-SampleDelayMilliseconds', '0')
    Assert ($requiredFieldBaseline.exitCode -eq 0) 'required-field fail-closed baseline export failed'
    $requiredFieldBaselineJson = Get-Content -LiteralPath $planPath -Raw -Encoding UTF8
    $requiredFieldPaths = @(
        'schemaVersion', 'toolId', 'refreshAlgorithmVersion', 'planHash', 'indexHash', 'planStatus', 'targetEntryCount', 'entrySnapshots',
        'findingCount', 'findings', 'blockerCount', 'unstableFindingCount',
        'entrySnapshots/0/entry', 'entrySnapshots/0/knowledgeId', 'entrySnapshots/0/entryMode', 'entrySnapshots/0/hashSchema',
        'entrySnapshots/0/entryHash', 'entrySnapshots/0/status', 'entrySnapshots/0/sourceSetHash',
        'entrySnapshots/0/declaredContentHash', 'entrySnapshots/0/declaredSourceSetHash', 'entrySnapshots/0/declaredEntryBodyHash',
        'entrySnapshots/0/expectedContentHash', 'entrySnapshots/0/expectedSourceSetHash', 'entrySnapshots/0/expectedEntryBodyHash',
        'entrySnapshots/0/sourceRefCount', 'entrySnapshots/0/sourceRefs', 'entrySnapshots/0/indexBindingCount',
        'entrySnapshots/0/indexBindingIds', 'entrySnapshots/0/indexBindings',
        'entrySnapshots/0/sourceRefs/0/path', 'entrySnapshots/0/sourceRefs/0/declaredHash', 'entrySnapshots/0/sourceRefs/0/currentHash',
        'entrySnapshots/0/sourceRefs/0/firstSampleHash', 'entrySnapshots/0/sourceRefs/0/snapshotStable',
        'entrySnapshots/0/indexBindings/0/id', 'entrySnapshots/0/indexBindings/0/hashSchema', 'entrySnapshots/0/indexBindings/0/contentHash',
        'entrySnapshots/0/indexBindings/0/sourceSetHash', 'entrySnapshots/0/indexBindings/0/entryBodyHash',
        'entrySnapshots/0/indexBindings/0/expectedContentHash', 'entrySnapshots/0/indexBindings/0/expectedSourceSetHash',
        'entrySnapshots/0/indexBindings/0/expectedEntryBodyHash',
        'findings/0/code', 'findings/0/entry', 'findings/0/entryHash', 'findings/0/source', 'findings/0/declaredHash',
        'findings/0/currentHash', 'findings/0/firstSampleHash', 'findings/0/snapshotStable', 'findings/0/declaredContentHash',
        'findings/0/action', 'findings/0/reason'
    )
    foreach ($requiredFieldPath in $requiredFieldPaths) {
        $missingFieldPlan = $requiredFieldBaselineJson | ConvertFrom-Json
        Remove-JsonProperty $missingFieldPlan $requiredFieldPath
        Write-Utf8 $planPath ($missingFieldPlan | ConvertTo-Json -Depth 12)
        $missingFieldRejected = Invoke-Script $applier @('-ProjectRoot', $fixture)
        Assert ($missingFieldRejected.exitCode -ne 0 -and $missingFieldRejected.output.Contains('missing')) "missing schema v3 field was not rejected: $requiredFieldPath"
    }

    $signedSchema2Plan = $requiredFieldBaselineJson | ConvertFrom-Json
    $signedSchema2Plan.schemaVersion = 2
    $signedSchema2Plan.planHash = Get-CanonicalPlanHash $signedSchema2Plan
    Write-Utf8 $planPath ($signedSchema2Plan | ConvertTo-Json -Depth 12)
    $signedSchema2Rejected = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($signedSchema2Rejected.exitCode -ne 0 -and -not $signedSchema2Rejected.output.Contains('plan hash mismatch') -and
        $signedSchema2Rejected.output.Contains('Unsupported refresh plan contract.')) 'internally complete, re-signed schemaVersion 2 plan did not fail closed at the contract gate'

    $oldSchema2Plan = $requiredFieldBaselineJson | ConvertFrom-Json
    $oldSchema2Plan.schemaVersion = 2
    Remove-JsonProperty $oldSchema2Plan 'refreshAlgorithmVersion'
    foreach ($entry in @($oldSchema2Plan.entrySnapshots)) {
        foreach ($name in @('expectedContentHash', 'expectedSourceSetHash', 'expectedEntryBodyHash')) { [void]$entry.PSObject.Properties.Remove($name) }
        foreach ($binding in @($entry.indexBindings)) {
            foreach ($name in @('expectedContentHash', 'expectedSourceSetHash', 'expectedEntryBodyHash')) { [void]$binding.PSObject.Properties.Remove($name) }
        }
    }
    Write-Utf8 $planPath ($oldSchema2Plan | ConvertTo-Json -Depth 12)
    $oldSchema2Rejected = Invoke-Script $applier @('-ProjectRoot', $fixture, '-Apply')
    Assert ($oldSchema2Rejected.exitCode -ne 0 -and $oldSchema2Rejected.output.Contains('missing plan.refreshAlgorithmVersion')) 'old schemaVersion 2 plan without v3-bound projection fields did not fail closed'

    Write-Output 'PASS: stable Knowledge refresh supports schema v3 projections, locked CAS, verified rollback, identity binding, and fail-closed plans.'
} finally {
    $resolved = [IO.Path]::GetFullPath($fixture)
    if ($resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolved)) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
