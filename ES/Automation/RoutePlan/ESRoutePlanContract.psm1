Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:RoutePlanSchemaPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Contracts\es-route-plan-v1.schema.json'))
$script:RouteStageRegistrySchemaPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Contracts\es-route-stage-registry-v1.schema.json'))
$script:JsonSchemaLitePath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Contracts\ESJsonSchemaLite.psm1'))
$script:DefaultRegistryProjectPath = 'ES/Automation/Contracts/es-route-stage.registry.json'
$script:HashPattern = '^[a-f0-9]{64}$'
Import-Module $script:JsonSchemaLitePath -ErrorAction Stop

function ConvertTo-ESRoutePlanCanonicalJson {
    [CmdletBinding()]
    param([AllowNull()]$Value)

    if ($null -eq $Value) { return 'null' }
    if ($Value -is [string] -or $Value -is [char]) { return ([string]$Value | ConvertTo-Json -Compress) }
    if ($Value -is [bool]) { return $(if ($Value) { 'true' } else { 'false' }) }
    if ($Value -is [datetime]) { return ($Value.ToUniversalTime().ToString('o') | ConvertTo-Json -Compress) }
    if ($Value -is [Collections.IDictionary]) {
        $parts = foreach ($key in @($Value.Keys | ForEach-Object { [string]$_ } | Sort-Object -CaseSensitive)) {
            '{0}:{1}' -f ($key | ConvertTo-Json -Compress), (ConvertTo-ESRoutePlanCanonicalJson $Value[$key])
        }
        return '{' + ($parts -join ',') + '}'
    }
    if ($Value -is [pscustomobject]) {
        $parts = foreach ($property in @($Value.PSObject.Properties | Sort-Object Name -CaseSensitive)) {
            '{0}:{1}' -f ($property.Name | ConvertTo-Json -Compress), (ConvertTo-ESRoutePlanCanonicalJson $property.Value)
        }
        return '{' + ($parts -join ',') + '}'
    }
    if ($Value -is [Collections.IEnumerable] -and -not ($Value -is [string])) {
        return '[' + (@($Value | ForEach-Object { ConvertTo-ESRoutePlanCanonicalJson $_ }) -join ',') + ']'
    }
    if ($Value -is [IFormattable]) { return $Value.ToString($null, [Globalization.CultureInfo]::InvariantCulture) }
    return ([string]$Value | ConvertTo-Json -Compress)
}

function Get-ESRoutePlanCanonicalHash {
    [CmdletBinding()]
    param([AllowNull()]$Value)

    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes((ConvertTo-ESRoutePlanCanonicalJson $Value))
        return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','').ToLowerInvariant()
    } finally { $sha.Dispose() }
}

function Get-ESRoutePlanHashInput {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)]$RoutePlan)

    [ordered]@{
        schemaVersion = [int]$RoutePlan.schemaVersion
        contractId = [string]$RoutePlan.contractId
        status = [string]$RoutePlan.status
        routeState = [string]$RoutePlan.routeState
        evidenceState = [string]$RoutePlan.evidenceState
        effect = [string]$RoutePlan.effect
        profile = [string]$RoutePlan.profile
        scope = [string]$RoutePlan.scope
        routeKeys = @($RoutePlan.routeKeys | ForEach-Object { [string]$_ })
        goalRevision = $RoutePlan.goalRevision
        stages = @($RoutePlan.stages)
        maxDepth = [int]$RoutePlan.maxDepth
        budget = $RoutePlan.budget
        stopConditions = @($RoutePlan.stopConditions)
        issues = @($RoutePlan.issues)
        snapshot = $RoutePlan.snapshot
        shadowIntegration = $RoutePlan.shadowIntegration
        compatibility = $RoutePlan.compatibility
        executionEnabled = [bool]$RoutePlan.executionEnabled
    }
}

function Get-ESRoutePlanShadowDecisionInput {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]$RoutePlan,
        [Parameter(Mandatory=$true)][string]$LegacyPlanStatus
    )

    [ordered]@{
        contractId = 'es://automation/contracts/route-plan-shadow-candidate/v1'
        mode = 'read-only-shadow'
        algorithmId = 'route-shadow-canonical-v1'
        profile = [string]$RoutePlan.profile
        scope = [string]$RoutePlan.scope
        legacyPlanStatus = $LegacyPlanStatus
        status = [string]$RoutePlan.status
        routeState = [string]$RoutePlan.routeState
        evidenceState = [string]$RoutePlan.evidenceState
        effect = [string]$RoutePlan.effect
        routeKeys = @($RoutePlan.routeKeys | ForEach-Object { [string]$_ })
        goalRevision = $RoutePlan.goalRevision
        stages = @($RoutePlan.stages)
        maxDepth = [int]$RoutePlan.maxDepth
        issues = @($RoutePlan.issues)
        snapshot = $RoutePlan.snapshot
    }
}

function Add-ESRoutePlanShadowIntegration {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]$Core,
        [Parameter(Mandatory=$true)][string]$LegacyPlanStatus
    )

    $selectedProfile = 'governance'
    $selectedScope = 'task-object'
    $eligible = [string]$Core.profile -ceq $selectedProfile -and [string]$Core.scope -ceq $selectedScope
    $decisionHash = $null
    $decisionId = $null
    if ($eligible) {
        $decisionHash = Get-ESRoutePlanCanonicalHash (Get-ESRoutePlanShadowDecisionInput -RoutePlan $Core -LegacyPlanStatus $LegacyPlanStatus)
        $decisionId = 'route-decision-' + $decisionHash.Substring(0, 32)
    }
    $shadow = [ordered]@{
        contractId = 'es://automation/contracts/route-plan-shadow-candidate/v1'
        mode = 'read-only-shadow'
        algorithmId = 'route-shadow-canonical-v1'
        selectedProfile = $selectedProfile
        selectedScope = $selectedScope
        candidateStatus = $(if ($eligible) { 'candidate-emitted' } else { 'not-selected' })
        decisionHash = $decisionHash
        decisionId = $decisionId
        legacyPlanStatusBefore = $LegacyPlanStatus
        legacyPlanStatusAfter = $LegacyPlanStatus
        stateChanged = $false
        verificationRequired = $true
        rollbackState = 'available'
        rollbackAction = 'discard-shadow-candidate'
        productionRouteIntegrated = $false
        globalP0Integrated = $false
        observationCodes = $(if ($eligible) {
            @('SHADOW.SCOPED_MATCH','SHADOW.ROLLBACK_AVAILABLE','SHADOW.NO_PRODUCTION_TAKEOVER')
        } else {
            @('SHADOW.PROFILE_SCOPE_NOT_SELECTED','SHADOW.ROLLBACK_AVAILABLE','SHADOW.NO_PRODUCTION_TAKEOVER')
        })
    }
    if ($Core -is [Collections.IDictionary]) { $Core['shadowIntegration'] = $shadow }
    else { $Core | Add-Member -NotePropertyName shadowIntegration -NotePropertyValue ([pscustomobject]$shadow) -Force }
    $Core
}

function Assert-ESRoutePlanShadowIntegration {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)]$RoutePlan)

    $shadow = $RoutePlan.shadowIntegration
    $legacyStatus = [string]$RoutePlan.compatibility.legacyPlanStatus
    $eligible = [string]$RoutePlan.profile -ceq 'governance' -and [string]$RoutePlan.scope -ceq 'task-object'
    if ([string]$shadow.legacyPlanStatusBefore -cne $legacyStatus -or
        [string]$shadow.legacyPlanStatusAfter -cne $legacyStatus) {
        throw 'RoutePlan shadow candidate changed or mismatched the legacy plan status.'
    }
    if ([bool]$shadow.stateChanged -or -not [bool]$shadow.verificationRequired -or
        [bool]$shadow.productionRouteIntegrated -or [bool]$shadow.globalP0Integrated -or
        [string]$shadow.rollbackState -cne 'available' -or
        [string]$shadow.rollbackAction -cne 'discard-shadow-candidate') {
        throw 'RoutePlan shadow candidate violated the read-only rollback boundary.'
    }
    $codes = @($shadow.observationCodes | ForEach-Object { [string]$_ })
    if ($codes.Count -ne 3 -or $codes -cnotcontains 'SHADOW.ROLLBACK_AVAILABLE' -or
        $codes -cnotcontains 'SHADOW.NO_PRODUCTION_TAKEOVER') {
        throw 'RoutePlan shadow candidate observation codes are incomplete or expanded.'
    }
    if ($eligible) {
        if ($codes -cnotcontains 'SHADOW.SCOPED_MATCH') { throw 'RoutePlan shadow match code is missing.' }
        if ([string]$shadow.candidateStatus -cne 'candidate-emitted') { throw 'RoutePlan shadow candidate was not emitted for the selected Profile/scope.' }
        $expectedHash = Get-ESRoutePlanCanonicalHash (Get-ESRoutePlanShadowDecisionInput -RoutePlan $RoutePlan -LegacyPlanStatus $legacyStatus)
        $expectedId = 'route-decision-' + $expectedHash.Substring(0, 32)
        if ([string]$shadow.decisionHash -cne $expectedHash -or
            [string]$shadow.decisionId -cne $expectedId) {
            throw 'RoutePlan shadow decisionId or snapshot binding mismatch.'
        }
    } else {
        if ($codes -cnotcontains 'SHADOW.PROFILE_SCOPE_NOT_SELECTED') { throw 'RoutePlan shadow not-selected code is missing.' }
        if ([string]$shadow.candidateStatus -cne 'not-selected' -or
            $null -ne $shadow.decisionHash -or $null -ne $shadow.decisionId) {
            throw 'RoutePlan shadow candidate expanded beyond its selected Profile/scope.'
        }
    }
}

function New-ESRoutePlanDocument {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)]$Core)

    $hash = Get-ESRoutePlanCanonicalHash $Core
    $document = [ordered]@{
        schemaVersion = [int]$Core.schemaVersion
        contractId = [string]$Core.contractId
        routePlanId = 'route-' + $hash.Substring(0, 32)
        routePlanHash = $hash
    }
    $names = if ($Core -is [Collections.IDictionary]) { @($Core.Keys) } else { @($Core.PSObject.Properties.Name) }
    $arrayFields = @('routeKeys','stages','stopConditions','issues')
    foreach ($name in $names) {
        if ([string]$name -notin @('schemaVersion','contractId')) {
            $value = if ($Core -is [Collections.IDictionary]) { $Core[$name] } else { $Core.([string]$name) }
            if ($arrayFields -ccontains [string]$name) {
                $document[[string]$name] = [object[]]@($value)
            } else {
                $document[[string]$name] = $value
            }
        }
    }
    [pscustomobject]$document
}

function Read-ESRoutePlanStrictJson {
    param([Parameter(Mandatory=$true)][string]$Path)
    try {
        $raw = [Text.UTF8Encoding]::new($false, $true).GetString([IO.File]::ReadAllBytes($Path))
        return $raw | ConvertFrom-Json -ErrorAction Stop
    } catch { throw "Invalid strict UTF-8 JSON: $Path. $($_.Exception.Message)" }
}

function Resolve-ESRoutePlanProjectFile {
    param([Parameter(Mandatory=$true)][string]$ProjectRoot, [Parameter(Mandatory=$true)][string]$ProjectPath)

    $root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    if ([string]::IsNullOrWhiteSpace($ProjectPath) -or $ProjectPath.Contains('\') -or [IO.Path]::IsPathRooted($ProjectPath) -or
        $ProjectPath -match '(^|/)\.\.?(/|$)' -or $ProjectPath -match '[*?]') {
        throw 'RoutePlan project path is not normalized and bounded.'
    }
    $full = [IO.Path]::GetFullPath((Join-Path $root $ProjectPath))
    $prefix = $root + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'RoutePlan project path escapes the project root.' }

    $relative = $full.Substring($prefix.Length)
    $cursor = $root
    foreach ($segment in $relative.Split([IO.Path]::DirectorySeparatorChar)) {
        $cursor = Join-Path $cursor $segment
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "RoutePlan project path traverses a reparse point: $ProjectPath" }
        }
    }
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "RoutePlan project file is missing: $ProjectPath" }
    [pscustomobject]@{ Full=$full; Relative=$ProjectPath }
}

function Assert-ESRoutePlanOrdinalSet {
    param([object[]]$Values, [string]$Name, [switch]$AllowEmpty)

    $items = @($Values | ForEach-Object { [string]$_ })
    if (-not $AllowEmpty -and $items.Count -eq 0) { throw "$Name must not be empty." }
    if (@($items | Sort-Object -CaseSensitive -Unique).Count -ne $items.Count) { throw "$Name must be unique." }
    $sorted = @($items | Sort-Object -CaseSensitive)
    for ($index = 0; $index -lt $items.Count; $index++) {
        if ($items[$index] -cne $sorted[$index]) { throw "$Name must use canonical ordinal order." }
    }
    return $items
}

function Test-ESRoutePlanArrayEqual {
    param([object[]]$Left, [object[]]$Right)
    $a = @($Left | ForEach-Object { [string]$_ })
    $b = @($Right | ForEach-Object { [string]$_ })
    if ($a.Count -ne $b.Count) { return $false }
    for ($index = 0; $index -lt $a.Count; $index++) { if ($a[$index] -cne $b[$index]) { return $false } }
    return $true
}

function Assert-ESRoutePlanRegistrySemantics {
    param([Parameter(Mandatory=$true)]$Plan, [Parameter(Mandatory=$true)]$Registry)

    $routeKeys = @(Assert-ESRoutePlanOrdinalSet @($Plan.routeKeys) 'RoutePlan routeKeys')
    $coverage = @(Assert-ESRoutePlanOrdinalSet @($Plan.snapshot.coverage.includes) 'RoutePlan snapshot coverage')
    foreach ($requiredCoverage in @('goal-revision-artifact','route-stage-registry')) {
        if ($coverage -cnotcontains $requiredCoverage) { throw "RoutePlan snapshot coverage is missing: $requiredCoverage" }
    }

    $stages = @($Plan.stages)
    if ([string]$Plan.status -ceq 'Ready' -and $stages.Count -eq 0) { throw 'Ready RoutePlan must contain at least one registered stage.' }
    $definitions = @($Registry.stages)
    $selected = [Collections.Generic.List[object]]::new()
    $byId = @{}
    for ($index = 0; $index -lt $stages.Count; $index++) {
        $stage = $stages[$index]
        $matches = @($definitions | Where-Object { [string]$_.stageContractId -ceq [string]$stage.stageContractId })
        if ($matches.Count -ne 1) { throw "RoutePlan stage is not registered exactly once: $($stage.stageContractId)" }
        $definition = $matches[0]
        if ([string]$stage.skillName -cne [string]$definition.skillName) { throw "RoutePlan stage Skill mismatch: $($stage.stageContractId)" }
        if (@($definition.profiles) -cnotcontains [string]$Plan.profile) { throw "RoutePlan stage Profile mismatch: $($stage.stageContractId)" }
        if (@($definition.routeKeys | Where-Object { $routeKeys -ccontains [string]$_ }).Count -eq 0) { throw "RoutePlan stage routeKey mismatch: $($stage.stageContractId)" }
        foreach ($field in @('requires','produces','failureConditions')) {
            $actual = @(Assert-ESRoutePlanOrdinalSet @($stage.$field) "RoutePlan stage $field" -AllowEmpty)
            $expected = @($definition.$field | ForEach-Object { [string]$_ } | Sort-Object -CaseSensitive)
            if (-not (Test-ESRoutePlanArrayEqual $actual $expected)) { throw "RoutePlan stage $field mismatch: $($stage.stageContractId)" }
        }
        if ([string]$stage.depthReasonCode -cne [string]$definition.depthReasonCode) { throw "RoutePlan stage depth reason mismatch: $($stage.stageContractId)" }
        $expectedStageId = 'stage-' + ($index + 1).ToString('00') + '-' + [string]$stage.skillName
        if ([string]$stage.stageId -cne $expectedStageId) { throw "RoutePlan stage order identity mismatch: $($stage.stageContractId)" }
        if ($byId.ContainsKey([string]$stage.stageContractId)) { throw "RoutePlan contains a duplicate stage: $($stage.stageContractId)" }
        $byId[[string]$stage.stageContractId] = $stage
        [void]$selected.Add([pscustomobject]@{ Plan=$stage; Definition=$definition })
    }

    $external = @($Registry.externalInputs | ForEach-Object { [string]$_ })
    $producer = @{}
    foreach ($item in $selected) {
        foreach ($token in @($item.Plan.produces)) {
            if ($producer.ContainsKey([string]$token)) { throw "RoutePlan has duplicate product: $token" }
            $producer[[string]$token] = [string]$item.Plan.stageContractId
        }
    }
    $dependencies = @{}
    foreach ($item in $selected) {
        $set = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($token in @($item.Plan.requires)) {
            if ($external -ccontains [string]$token) { continue }
            if (-not $producer.ContainsKey([string]$token)) { throw "RoutePlan stage input is not produced: $token" }
            [void]$set.Add([string]$producer[[string]$token])
        }
        $dependencies[[string]$item.Plan.stageContractId] = $set
    }

    $remaining = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($item in $selected) { [void]$remaining.Add([string]$item.Plan.stageContractId) }
    $ordered = [Collections.Generic.List[string]]::new()
    $depths = @{}
    while ($remaining.Count -gt 0) {
        $ready = @($remaining | Where-Object {
            $stageId = $_
            @($dependencies[$stageId] | Where-Object { -not $ordered.Contains($_) }).Count -eq 0
        } | Sort-Object -CaseSensitive)
        if ($ready.Count -eq 0) { throw 'RoutePlan stage dependencies contain a cycle.' }
        foreach ($stageId in $ready) {
            $parents = @($dependencies[$stageId])
            $depths[$stageId] = if ($parents.Count -eq 0) { 0 } else { 1 + ($parents | ForEach-Object { [int]$depths[$_] } | Measure-Object -Maximum).Maximum }
            [void]$ordered.Add($stageId)
            [void]$remaining.Remove($stageId)
        }
    }
    $actualOrder = @($stages | ForEach-Object { [string]$_.stageContractId })
    if (-not (Test-ESRoutePlanArrayEqual $actualOrder @($ordered))) { throw 'RoutePlan stages are not in deterministic dependency order.' }

    $maximumDepth = 0
    foreach ($item in $selected) {
        $stage = $item.Plan
        $depth = [int]$depths[[string]$stage.stageContractId]
        if ([int]$stage.depth -ne $depth) { throw "RoutePlan stage depth mismatch: $($stage.stageContractId)" }
        if ($depth -gt [int]$Registry.maxDepth) { throw "RoutePlan stage exceeds registry maxDepth: $($stage.stageContractId)" }
        if ($depth -gt [int]$Registry.defaultMaxDepth) {
            $authorizations = @($Registry.depthAuthorizations | Where-Object {
                [string]$_.reasonCode -ceq [string]$stage.depthReasonCode -and
                [int]$_.authorizesDepth -eq $depth -and
                @($_.profiles) -ccontains [string]$Plan.profile -and
                @($_.routeKeys | Where-Object { $routeKeys -ccontains [string]$_ }).Count -gt 0
            })
            if ($authorizations.Count -ne 1) { throw "RoutePlan depth authorization is missing: $($stage.stageContractId)" }
        } elseif (-not [string]::IsNullOrEmpty([string]$stage.depthReasonCode)) {
            throw "RoutePlan depth reason is misapplied: $($stage.stageContractId)"
        }
        if ($depth -gt $maximumDepth) { $maximumDepth = $depth }
    }
    if ([int]$Plan.maxDepth -ne $maximumDepth) { throw 'RoutePlan maxDepth does not match the selected dependency graph.' }
    if (($maximumDepth -eq 0 -and [string]$Plan.routeState -cne 'core') -or
        ($maximumDepth -gt 0 -and [string]$Plan.routeState -cne 'extension')) {
        throw 'RoutePlan routeState does not match the selected dependency graph.'
    }
}

function Resolve-ESRoutePlanArtifact {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$ProjectRoot,
        [Parameter(Mandatory=$true)][string]$RoutePlanPath,
        $ExpectedGoal,
        [switch]$RequireReady,
        [string]$RegistryProjectPath = $script:DefaultRegistryProjectPath
    )

    $resolved = Resolve-ESRoutePlanProjectFile $ProjectRoot $RoutePlanPath
    $plan = Read-ESRoutePlanStrictJson $resolved.Full
    $schemaErrors = @(Test-ESJsonSchemaValue -SchemaPath $script:RoutePlanSchemaPath -Value $plan)
    if ($schemaErrors.Count -gt 0) { throw ('RoutePlan schema validation failed: ' + ($schemaErrors -join '; ')) }
    if ($RequireReady -and ([string]$plan.status -cne 'Ready' -or @('core','extension') -cnotcontains [string]$plan.routeState)) {
        throw 'TaskContext requires a Ready core or extension RoutePlan.'
    }
    if ([bool]$plan.executionEnabled -or [string]$plan.compatibility.executionAuthority -cne 'none') { throw 'RoutePlan cannot carry execution authority.' }

    Assert-ESRoutePlanShadowIntegration -RoutePlan $plan

    $computedHash = Get-ESRoutePlanCanonicalHash (Get-ESRoutePlanHashInput $plan)
    if ([string]$plan.routePlanHash -cne $computedHash) { throw 'RoutePlan hash mismatch.' }
    $expectedId = 'route-' + $computedHash.Substring(0, 32)
    if ([string]$plan.routePlanId -cne $expectedId) { throw 'RoutePlan identity does not match its canonical hash.' }

    $sourceRefs = @($plan.snapshot.sourceRefs)
    if ($sourceRefs.Count -eq 0) { throw 'RoutePlan snapshot must contain SourceRefs.' }
    $normalizedRefs = [Collections.Generic.List[object]]::new()
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $previous = $null
    foreach ($sourceRef in $sourceRefs) {
        $projectPath = [string]$sourceRef.projectPath
        if (-not $seen.Add($projectPath)) { throw 'RoutePlan SourceRefs contain a duplicate path.' }
        if ($null -ne $previous -and [StringComparer]::Ordinal.Compare($previous, $projectPath) -ge 0) { throw 'RoutePlan SourceRefs are not in canonical ordinal order.' }
        $previous = $projectPath
        $hash = [string]$sourceRef.sha256
        if ($hash -cnotmatch $script:HashPattern) { throw 'RoutePlan SourceRef hash must be a lowercase SHA-256.' }
        $source = Resolve-ESRoutePlanProjectFile $ProjectRoot $projectPath
        $currentHash = (Get-FileHash -LiteralPath $source.Full -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($currentHash -cne $hash) { throw "RoutePlan SourceRef drift: $projectPath" }
        [void]$normalizedRefs.Add([ordered]@{projectPath=$projectPath;sha256=$hash})
    }
    if ((Get-ESRoutePlanCanonicalHash @($normalizedRefs)) -cne [string]$plan.snapshot.sourceRefsHash) { throw 'RoutePlan SourceRefs hash mismatch.' }

    $headOutput = @(& git -C $ProjectRoot rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or $headOutput.Count -ne 1) { throw 'RoutePlan Git HEAD is unavailable.' }
    $head = ([string]$headOutput[0]).Trim().ToLowerInvariant()
    if ($head -cnotmatch '^[a-f0-9]{40}$' -or $head -cne [string]$plan.snapshot.head) { throw 'RoutePlan Git HEAD drift.' }

    $registryFile = Resolve-ESRoutePlanProjectFile $ProjectRoot $RegistryProjectPath
    $registryHash = (Get-FileHash -LiteralPath $registryFile.Full -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($registryHash -cne [string]$plan.snapshot.registryHash) { throw 'RoutePlan Registry hash drift.' }
    $registryRefs = @($sourceRefs | Where-Object { [string]$_.projectPath -ceq $RegistryProjectPath })
    if ($registryRefs.Count -ne 1 -or [string]$registryRefs[0].sha256 -cne $registryHash) { throw 'RoutePlan Registry is not bound exactly once in SourceRefs.' }
    $registry = Read-ESRoutePlanStrictJson $registryFile.Full
    $registryErrors = @(Test-ESJsonSchemaValue -SchemaPath $script:RouteStageRegistrySchemaPath -Value $registry)
    if ($registryErrors.Count -gt 0) { throw ('RoutePlan Registry schema validation failed: ' + ($registryErrors -join '; ')) }

    $goal = $plan.goalRevision
    if ($null -ne $goal) {
        $goalRefs = @($sourceRefs | Where-Object { [string]$_.projectPath -ceq [string]$goal.projectPath })
        if ($goalRefs.Count -ne 1 -or [string]$goalRefs[0].sha256 -cne [string]$goal.artifactHash) { throw 'RoutePlan GoalRevision is not bound exactly once in SourceRefs.' }
    }
    if ($null -ne $ExpectedGoal) {
        if ($null -eq $goal -or [string]$goal.goalId -cne [string]$ExpectedGoal.goalId -or
            [string]$goal.goalRevision -cne [string]$ExpectedGoal.goalRevision -or
            [string]$goal.revisionHash -cne [string]$ExpectedGoal.goalRevisionHash -or
            [string]$goal.projectPath -cne [string]$ExpectedGoal.path -or
            [string]$goal.artifactHash -cne [string]$ExpectedGoal.artifactHash) {
            throw 'RoutePlan GoalRevision binding mismatch.'
        }
    }

    Assert-ESRoutePlanRegistrySemantics $plan $registry
    [pscustomobject][ordered]@{
        routePlanId = [string]$plan.routePlanId
        routePlanHash = $computedHash
        routePlanArtifactHash = (Get-FileHash -LiteralPath $resolved.Full -Algorithm SHA256).Hash.ToLowerInvariant()
        routePlanPath = $resolved.Relative
        profile = [string]$plan.profile
        routeState = [string]$plan.routeState
        routeKeys = @($plan.routeKeys | ForEach-Object { [string]$_ })
        snapshotHash = Get-ESRoutePlanCanonicalHash $plan.snapshot
        head = $head
        sourceRefsHash = [string]$plan.snapshot.sourceRefsHash
        registryHash = $registryHash
        shadowCandidateStatus = [string]$plan.shadowIntegration.candidateStatus
        shadowObservationStatus = $(if ([string]$plan.shadowIntegration.candidateStatus -ceq 'candidate-emitted') { 'verified' } else { 'not-selected' })
        shadowDecisionId = [string]$plan.shadowIntegration.decisionId
        shadowDecisionIdMatched = [string]$plan.shadowIntegration.candidateStatus -ceq 'candidate-emitted'
        shadowBypassDetected = $false
        shadowRollbackState = [string]$plan.shadowIntegration.rollbackState
        shadowRollbackAction = [string]$plan.shadowIntegration.rollbackAction
    }
}

Export-ModuleMember -Function ConvertTo-ESRoutePlanCanonicalJson,Get-ESRoutePlanCanonicalHash,Get-ESRoutePlanHashInput,Get-ESRoutePlanShadowDecisionInput,Add-ESRoutePlanShadowIntegration,Assert-ESRoutePlanShadowIntegration,New-ESRoutePlanDocument,Resolve-ESRoutePlanArtifact
