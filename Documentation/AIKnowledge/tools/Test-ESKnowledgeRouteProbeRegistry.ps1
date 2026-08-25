[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [string]$RegistryPath = 'Documentation/AIKnowledge/RouteProbeRegistry.json'
)

$ErrorActionPreference = 'Stop'
$strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
$findings = [Collections.Generic.List[object]]::new()

function Add-Finding([string]$Code, [string]$ProbeId, [string]$Message) {
    $findings.Add([pscustomobject][ordered]@{
        code = $Code
        probeId = $ProbeId
        message = $Message
    })
}

function Resolve-ProjectFile([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath)) {
        throw "Project-relative file path required: $RelativePath"
    }
    $candidate = [IO.Path]::GetFullPath([IO.Path]::Combine($script:root, $RelativePath))
    if (-not $candidate.StartsWith($script:root + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes ProjectRoot: $RelativePath"
    }
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "File not found: $RelativePath"
    }
    return $candidate
}

function Read-StrictText([string]$RelativePath) {
    $fullPath = Resolve-ProjectFile $RelativePath
    return $strictUtf8.GetString([IO.File]::ReadAllBytes($fullPath))
}

function Get-FileSha256([string]$RelativePath) {
    $fullPath = Resolve-ProjectFile $RelativePath
    return (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Read-Strings([object]$Value, [string]$Field, [string]$ProbeId, [switch]$AllowEmpty) {
    $values = @($Value | ForEach-Object { [string]$_ })
    if (-not $AllowEmpty -and $values.Count -eq 0) {
        Add-Finding 'REGISTRY_REQUIRED_ARRAY_EMPTY' $ProbeId "$Field must not be empty."
    }
    if (@($values | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -gt 0) {
        Add-Finding 'REGISTRY_STRING_EMPTY' $ProbeId "$Field contains an empty value."
    }
    if (@($values | Sort-Object -CaseSensitive -Unique).Count -ne $values.Count) {
        Add-Finding 'REGISTRY_DUPLICATE_VALUE' $ProbeId "$Field contains duplicate values."
    }
    return $values
}

function Parse-InlineList([string]$Value) {
    $trimmed = $Value.Trim()
    if ($trimmed.StartsWith('[') -and $trimmed.EndsWith(']')) {
        $trimmed = $trimmed.Substring(1, $trimmed.Length - 2)
    }
    if ([string]::IsNullOrWhiteSpace($trimmed)) { return @() }
    return @($trimmed.Split(',') | ForEach-Object { $_.Trim().Trim('"', "'") })
}

function Read-KnowledgeIndex([string]$RelativePath) {
    $text = Read-StrictText $RelativePath
    $entries = [Collections.Generic.List[object]]::new()
    $current = $null
    $readingRequiredReads = $false
    $schemaVersion = 0
    foreach ($raw in $text -split "`r?`n") {
        $trimmed = $raw.Trim()
        if ($trimmed.StartsWith('schemaVersion:', [StringComparison]::Ordinal)) {
            [void][int]::TryParse($trimmed.Substring('schemaVersion:'.Length).Trim(), [ref]$schemaVersion)
            continue
        }
        if ($trimmed.StartsWith('- knowledgeId:', [StringComparison]::Ordinal)) {
            if ($null -ne $current) { $entries.Add([pscustomobject]$current) }
            $current = [ordered]@{
                knowledgeId = $trimmed.Substring('- knowledgeId:'.Length).Trim().Trim('"', "'")
                routeKeys = [Collections.Generic.List[string]]::new()
                requiredReads = [Collections.Generic.List[string]]::new()
            }
            $readingRequiredReads = $false
            continue
        }
        if ($null -eq $current) { continue }
        if ($trimmed.StartsWith('routeKeys:', [StringComparison]::Ordinal)) {
            foreach ($item in Parse-InlineList $trimmed.Substring('routeKeys:'.Length)) {
                $current.routeKeys.Add($item)
            }
            $readingRequiredReads = $false
            continue
        }
        if ($trimmed.StartsWith('requiredReads:', [StringComparison]::Ordinal)) {
            $inline = $trimmed.Substring('requiredReads:'.Length).Trim()
            foreach ($item in Parse-InlineList $inline) { $current.requiredReads.Add($item) }
            $readingRequiredReads = $inline.Length -eq 0
            continue
        }
        if ($readingRequiredReads -and $trimmed.StartsWith('- ', [StringComparison]::Ordinal)) {
            $current.requiredReads.Add($trimmed.Substring(2).Trim().Trim('"', "'"))
            continue
        }
        if ($trimmed -match '^(authority|evidenceLevel|contentHash|staleWhen):') {
            $readingRequiredReads = $false
        }
    }
    if ($null -ne $current) { $entries.Add([pscustomobject]$current) }
    if ($schemaVersion -ne 1) { throw "KnowledgeIndex schemaVersion must be 1, actual: $schemaVersion" }
    return $entries.ToArray()
}

function Select-Knowledge([object[]]$Entries, [string[]]$RouteKeys) {
    if ($RouteKeys.Count -eq 0) { return @() }
    $routeSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($routeKey in $RouteKeys) { [void]$routeSet.Add($routeKey) }
    $candidates = [Collections.Generic.List[object]]::new()
    foreach ($entry in $Entries) {
        $matchedCount = @($entry.routeKeys | Where-Object { $routeSet.Contains($_) }).Count
        if ($matchedCount -gt 0) {
            $candidates.Add([pscustomobject]@{
                entry = $entry
                matchedCount = $matchedCount
                ratio = [double]$matchedCount / [Math]::Max(1, $entry.routeKeys.Count)
            })
        }
    }
    $bestByRoute = @{}
    foreach ($routeKey in $RouteKeys) {
        $maximum = @($candidates | Where-Object { $_.entry.routeKeys.Contains($routeKey) } |
            ForEach-Object { $_.matchedCount } | Measure-Object -Maximum).Maximum
        $bestByRoute[$routeKey] = if ($null -eq $maximum) { 0 } else { [int]$maximum }
    }
    return @($candidates | Where-Object {
            $candidate = $_
            $keep = $false
            foreach ($routeKey in $RouteKeys) {
                if ($candidate.entry.routeKeys.Contains($routeKey) -and
                    $candidate.matchedCount -eq $bestByRoute[$routeKey]) {
                    $keep = $true
                    break
                }
            }
            $keep
        } | Sort-Object @{ Expression = 'matchedCount'; Descending = $true },
            @{ Expression = 'ratio'; Descending = $true },
            @{ Expression = { $_.entry.knowledgeId }; Descending = $false } |
        Select-Object -First 3 | ForEach-Object { $_.entry })
}

$root = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd(
    [IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
try {
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw "ProjectRoot not found: $root" }
    $registryText = Read-StrictText $RegistryPath
    $registry = $registryText | ConvertFrom-Json
    $schemaUri = [string]$registry.'$schema'
    $schemaPath = 'ES/Automation/Contracts/es-knowledge-route-probe-registry.schema.json'
    if ($schemaUri -cne '../../ES/Automation/Contracts/es-knowledge-route-probe-registry.schema.json') {
        Add-Finding 'REGISTRY_SCHEMA_URI' '' 'The relative $schema URI is invalid.'
    }
    $schema = (Read-StrictText $schemaPath) | ConvertFrom-Json

    if ($registry.schemaVersion -ne 1) { Add-Finding 'REGISTRY_SCHEMA_VERSION' '' 'schemaVersion must be 1.' }
    if ($registry.registryId -cne 'esframework-knowledge-route-probes') { Add-Finding 'REGISTRY_ID' '' 'registryId is invalid.' }
    if ($registry.lifecycleState -cne 'operational-static') { Add-Finding 'REGISTRY_LIFECYCLE' '' 'lifecycleState must be operational-static.' }
    if ($registry.ownerKnowledgeId -cne 'es.knowledge.routing-quality.v1') { Add-Finding 'REGISTRY_OWNER' '' 'ownerKnowledgeId is invalid.' }
    if ($registry.rankingVersion -cne 'per-route-best-top3-v1' -or
        $registry.rankingVersion -cne $schema.properties.rankingVersion.const) {
        Add-Finding 'REGISTRY_RANKING_VERSION' '' 'rankingVersion is unsupported or differs from Schema.'
    }
    if ($registry.knowledgeIndexPath -cne 'Documentation/AIKnowledge/KnowledgeIndex.yaml') {
        Add-Finding 'REGISTRY_INDEX_PATH' '' 'knowledgeIndexPath is invalid.'
    }

    $consumers = $registry.consumers
    $expectedCli = 'Documentation/AIKnowledge/tools/Test-ESKnowledgeRouteProbeRegistry.ps1'
    $expectedUnity = 'Assets/Plugins/ES/1_Design/Tests/ESAIBrainKnowledgeRoutingTests.cs'
    $expectedMethod = 'Plan_RouteProbeRegistry_MatchesFixedCrossDomainExpectations'
    $expectedBridgeOperation = 'runKnowledgeRouteProbes'
    $expectedProductionSurfaceId = 'diagnostic.knowledge-route-probes'
    if ($consumers.cliValidator -cne $expectedCli -or $consumers.unityTestSource -cne $expectedUnity -or
        $consumers.unityTestMethod -cne $expectedMethod -or
        $consumers.bridgeOperation -cne $expectedBridgeOperation -or
        $consumers.productionSurfaceId -cne $expectedProductionSurfaceId) {
        Add-Finding 'REGISTRY_CONSUMER_BINDING' '' 'Consumer registration is invalid.'
    }
    foreach ($consumerPath in @($consumers.cliValidator, $consumers.unityTestSource)) {
        try { [void](Resolve-ProjectFile $consumerPath) }
        catch { Add-Finding 'REGISTRY_CONSUMER_MISSING' '' $_.Exception.Message }
    }
    $unityTestText = Read-StrictText $expectedUnity
    if ($unityTestText -notmatch [regex]::Escape($expectedMethod) -or
        $unityTestText -notmatch [regex]::Escape($RegistryPath)) {
        Add-Finding 'REGISTRY_UNITY_CONSUMER_STALE' '' 'Unity consumer does not bind the registered method and registry path.'
    }
    $coordinatorText = Read-StrictText 'Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs'
    if ($coordinatorText -notmatch ('KnowledgeRankingVersion\s*=\s*"' +
            [regex]::Escape([string]$registry.rankingVersion) + '"')) {
        Add-Finding 'REGISTRY_COORDINATOR_VERSION' '' 'Coordinator ranking version differs from the registry.'
    }
    if ($coordinatorText -notmatch [regex]::Escape($expectedProductionSurfaceId)) {
        Add-Finding 'REGISTRY_PRODUCTION_SURFACE_MISSING' '' 'AIBrain production surface does not register the route probe diagnostic.'
    }
    $bridgeText = Read-StrictText 'Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs'
    if ($bridgeText -notmatch ('case\s+"' + [regex]::Escape($expectedBridgeOperation) + '"')) {
        Add-Finding 'REGISTRY_BRIDGE_OPERATION_MISSING' '' 'AI Bridge operation is not registered.'
    }

    $probes = @($registry.probes)
    if ($probes.Count -lt 10) { Add-Finding 'REGISTRY_PROBE_COUNT' '' 'At least 10 probes are required.' }
    $probeIds = @($probes | ForEach-Object { [string]$_.probeId })
    if (@($probeIds | Sort-Object -CaseSensitive -Unique).Count -ne $probeIds.Count) {
        Add-Finding 'REGISTRY_DUPLICATE_PROBE' '' 'probeId values must be unique.'
    }

    $entries = Read-KnowledgeIndex ([string]$registry.knowledgeIndexPath)
    foreach ($probe in $probes) {
        $probeId = [string]$probe.probeId
        if ([string]::IsNullOrWhiteSpace($probe.objective)) { Add-Finding 'REGISTRY_OBJECTIVE_EMPTY' $probeId 'objective is required.' }
        if ($probe.evidenceBoundary -cne 'static-routing-only') { Add-Finding 'REGISTRY_EVIDENCE_BOUNDARY' $probeId 'evidenceBoundary is invalid.' }
        $repeatCount = [int]$probe.repeatCount
        if ($repeatCount -lt 2 -or $repeatCount -gt 5) { Add-Finding 'REGISTRY_REPEAT_COUNT' $probeId 'repeatCount must be between 2 and 5.' }
        $explicitRouteKeys = @(Read-Strings $probe.explicitRouteKeys 'explicitRouteKeys' $probeId -AllowEmpty)
        $routeKeys = @(Read-Strings $probe.expectedRouteKeys 'expectedRouteKeys' $probeId -AllowEmpty)
        foreach ($explicitRouteKey in $explicitRouteKeys) {
            if ($routeKeys -cnotcontains $explicitRouteKey) {
                Add-Finding 'REGISTRY_EXPLICIT_ROUTE_MISSING' $probeId "Expected routeKeys omit explicit key: $explicitRouteKey"
            }
        }
        $expectations = @($probe.expectedKnowledgeTop3)
        $zeroHitAllowed = [bool]$probe.zeroHitAllowed
        if ($expectations.Count -gt 3 -or ($zeroHitAllowed -ne ($expectations.Count -eq 0))) {
            Add-Finding 'REGISTRY_ZERO_HIT_CONTRACT' $probeId 'Top-3 and zeroHitAllowed are inconsistent.'
        }
        $forbiddenIds = @(Read-Strings $probe.forbiddenKnowledgeIds 'forbiddenKnowledgeIds' $probeId)
        $expectedIds = @($expectations | ForEach-Object { [string]$_.knowledgeId })
        if (@($expectedIds | Sort-Object -CaseSensitive -Unique).Count -ne $expectedIds.Count) {
            Add-Finding 'REGISTRY_DUPLICATE_EXPECTED_KNOWLEDGE' $probeId 'Expected Knowledge IDs must be unique.'
        }
        foreach ($expectedId in $expectedIds) {
            if ($forbiddenIds -ccontains $expectedId) {
                Add-Finding 'REGISTRY_EXPECTED_FORBIDDEN_OVERLAP' $probeId "Expected Knowledge is forbidden: $expectedId"
            }
        }

        $baseline = $null
        for ($attempt = 0; $attempt -lt $repeatCount; $attempt++) {
            $selected = @(Select-Knowledge $entries $routeKeys)
            $actualIds = @($selected | ForEach-Object { [string]$_.knowledgeId })
            $signature = $actualIds -join '|'
            if ($attempt -eq 0) { $baseline = $signature }
            elseif ($signature -cne $baseline) { Add-Finding 'ROUTE_NON_DETERMINISTIC' $probeId 'Repeated ranking changed order.' }
        }
        if ($baseline -cne ($expectedIds -join '|')) {
            Add-Finding 'ROUTE_TOP3_MISMATCH' $probeId "Expected [$($expectedIds -join ', ')], actual [$($baseline -replace '\|', ', ')]."
        }
        foreach ($forbiddenId in $forbiddenIds) {
            if (($baseline -split '\|') -ccontains $forbiddenId) {
                Add-Finding 'ROUTE_FORBIDDEN_HIT' $probeId "Forbidden Knowledge selected: $forbiddenId"
            }
        }
        foreach ($expectation in $expectations) {
            $knowledgeId = [string]$expectation.knowledgeId
            $expectedReads = @(Read-Strings $expectation.requiredReads 'requiredReads' $probeId)
            $binding = @($entries | Where-Object { $_.knowledgeId -ceq $knowledgeId })
            if ($binding.Count -ne 1) {
                Add-Finding 'ROUTE_KNOWLEDGE_BINDING' $probeId "Knowledge binding count is $($binding.Count): $knowledgeId"
                continue
            }
            $actualReads = @($binding[0].requiredReads)
            if (($actualReads -join '|') -cne ($expectedReads -join '|')) {
                Add-Finding 'ROUTE_REQUIRED_READS_MISMATCH' $probeId "requiredReads differ: $knowledgeId"
            }
            foreach ($requiredRead in $expectedReads) {
                try { [void](Resolve-ProjectFile $requiredRead) }
                catch { Add-Finding 'ROUTE_REQUIRED_READ_MISSING' $probeId $_.Exception.Message }
            }
        }
    }

    $orderedFindings = @($findings | Sort-Object code, probeId, message)
    $status = if ($orderedFindings.Count -eq 0) { 'passed' } else { 'blocked' }
    [pscustomobject][ordered]@{
        schemaVersion = 1
        validator = 'es-knowledge-route-probe-registry'
        registryId = [string]$registry.registryId
        lifecycleState = [string]$registry.lifecycleState
        rankingVersion = [string]$registry.rankingVersion
        status = $status
        staticStatus = if ($status -eq 'passed') { 'static-passed' } else { 'static-blocked' }
        runtimeStatus = 'runtime-not-run'
        objectiveInferenceStatus = 'registered-unity-consumer-not-run'
        probeCount = $probes.Count
        replayCount = [int](($probes | Measure-Object repeatCount -Sum).Sum)
        registryHash = Get-FileSha256 $RegistryPath
        knowledgeIndexHash = Get-FileSha256 ([string]$registry.knowledgeIndexPath)
        findingCount = $orderedFindings.Count
        findings = $orderedFindings
        nonClaims = @('Unity Test Runner passed', 'Unity compilation passed', 'Runtime or release behavior passed')
    } | ConvertTo-Json -Depth 10
    if ($status -ne 'passed') { exit 1 }
}
catch {
    [pscustomobject][ordered]@{
        schemaVersion = 1
        validator = 'es-knowledge-route-probe-registry'
        status = 'blocked'
        staticStatus = 'static-blocked'
        runtimeStatus = 'runtime-not-run'
        findingCount = 1
        findings = @([pscustomobject][ordered]@{ code = 'VALIDATOR_EXCEPTION'; probeId = ''; message = $_.Exception.Message })
    } | ConvertTo-Json -Depth 8
    exit 1
}
