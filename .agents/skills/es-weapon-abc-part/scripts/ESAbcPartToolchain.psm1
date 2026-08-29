Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:StrictUtf8 = New-Object Text.UTF8Encoding($false, $true)
$script:Utf8NoBom = New-Object Text.UTF8Encoding($false)

function Get-ESAbcSha256Bytes([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
}

function Get-ESAbcSha256Text([string]$Text) {
    return Get-ESAbcSha256Bytes $script:Utf8NoBom.GetBytes($Text)
}

function Resolve-ESAbcProjectPath {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [switch]$MustExist
    )
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath)) {
        throw "Path must be project-relative: $RelativePath"
    }
    $root = [IO.Path]::GetFullPath((Get-Item -LiteralPath $ProjectRoot -Force -ErrorAction Stop).FullName).TrimEnd('\', '/')
    $full = [IO.Path]::GetFullPath([IO.Path]::Combine($root, $RelativePath.Replace('/', '\')))
    $prefix = $root + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes ProjectRoot: $RelativePath"
    }
    if ($MustExist -and -not (Test-Path -LiteralPath $full -PathType Leaf)) {
        throw "Required project file is missing: $RelativePath"
    }
    return $full
}

function Get-ESAbcRelativePath {
    param([Parameter(Mandatory = $true)][string]$ProjectRoot, [Parameter(Mandatory = $true)][string]$FullPath)
    $root = [IO.Path]::GetFullPath((Get-Item -LiteralPath $ProjectRoot -Force -ErrorAction Stop).FullName).TrimEnd('\', '/')
    $full = [IO.Path]::GetFullPath($FullPath)
    $prefix = $root + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside ProjectRoot: $FullPath"
    }
    return $full.Substring($prefix.Length).Replace('\', '/')
}

function Read-ESAbcJson {
    param([Parameter(Mandatory = $true)][string]$Path)
    $text = $script:StrictUtf8.GetString([IO.File]::ReadAllBytes($Path))
    return $text | ConvertFrom-Json -ErrorAction Stop
}

function Write-ESAbcJson {
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)][object]$Value)
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        [IO.Directory]::CreateDirectory($parent) | Out-Null
    }
    $json = $Value | ConvertTo-Json -Depth 24
    [IO.File]::WriteAllText($Path, $json, $script:Utf8NoBom)
}

function Get-ESAbcCoreContract {
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)
    return Read-ESAbcJson (Resolve-ESAbcProjectPath -ProjectRoot $ProjectRoot -RelativePath 'ES/Automation/Contracts/es-ai-abc-core-v1.json' -MustExist)
}

function Get-ESAbcRouteStageRegistry {
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)
    return Read-ESAbcJson (Resolve-ESAbcProjectPath -ProjectRoot $ProjectRoot -RelativePath 'ES/Automation/Contracts/es-route-stage.registry.json' -MustExist)
}

function Get-ESAbcModeRegistry {
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)
    return Read-ESAbcJson (Resolve-ESAbcProjectPath -ProjectRoot $ProjectRoot -RelativePath 'ES/Automation/Contracts/es-ai-abc-mode.registry.json' -MustExist)
}

function Get-ESAbcPartAuthorityRegistry {
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)
    return Read-ESAbcJson (Resolve-ESAbcProjectPath -ProjectRoot $ProjectRoot -RelativePath 'ES/Automation/Contracts/es-weapon-abc-part-authority-v1.json' -MustExist)
}

function Test-ESAbcProperty([object]$Object, [string]$Name) {
    return ($null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name])
}

function Get-ESAbcStringSet([object]$Value) {
    if ($null -eq $Value) { return @() }
    return @($Value | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
}

function Add-ESAbcCheck {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][System.Collections.Generic.List[object]]$Checks,
        [Parameter(Mandatory = $true)][string]$Id,
        [Parameter(Mandatory = $true)][bool]$Passed,
        [Parameter(Mandatory = $true)][string]$Detail,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][System.Collections.Generic.List[string]]$Issues
    )
    [void]$Checks.Add([pscustomobject][ordered]@{ id = $Id; status = if ($Passed) { 'passed' } else { 'blocked' }; detail = $Detail })
    if (-not $Passed) { [void]$Issues.Add("$Id`: $Detail") }
}

function Test-ESAbcPartContractObject {
    param(
        [Parameter(Mandatory = $true)][object]$Part,
        [Parameter(Mandatory = $true)][object]$Core,
        [Parameter(Mandatory = $true)][object]$RouteRegistry,
        [Parameter(Mandatory = $true)][object]$ModeRegistry,
        [object]$AuthorityRegistry
    )
    $issues = New-Object 'System.Collections.Generic.List[string]'
    $checks = New-Object 'System.Collections.Generic.List[object]'
    $required = @('schemaVersion', 'contractId', 'partId', 'mode', 'domain', 'coreRef', 'coreContractRef', 'capabilityRefs', 'aProfile', 'bProfile', 'cProfile', 'routePlanTemplate', 'evidenceProfile', 'compatibility', 'fallback')
    $missing = @($required | Where-Object { -not (Test-ESAbcProperty $Part $_) })
    Add-ESAbcCheck $checks 'part-required-fields' ($missing.Count -eq 0) ($(if ($missing.Count -eq 0) { 'all Part contract fields are present' } else { 'missing: ' + ($missing -join ', ') })) $issues
    if ($missing.Count -gt 0) {
        return [pscustomobject][ordered]@{ status = 'blocked'; issues = @($issues.ToArray()); checks = @($checks.ToArray()) }
    }

    Add-ESAbcCheck $checks 'part-identity' ([int]$Part.schemaVersion -eq 1 -and [string]$Part.contractId -eq 'es://automation/contracts/ai-abc/part/v1' -and [string]$Part.mode -eq 'ABCP.Part') 'schemaVersion, contractId and mode are fixed' $issues
    $domain = [string]$Part.domain
    Add-ESAbcCheck $checks 'domain-identity' ($domain -match '^[a-z0-9][a-z0-9-]{0,63}$' -and [string]$Part.partId -match ('^es\.' + [regex]::Escape($domain) + '\.abc\.part\.v[0-9]+$')) 'domain and partId are stable and aligned' $issues
    Add-ESAbcCheck $checks 'weapon-domain-scope' ($domain -eq 'weapon') 'This toolchain is scoped to the registered Weapon ABCP domain' $issues
    Add-ESAbcCheck $checks 'core-binding' ([string]$Part.coreRef -eq [string]$Core.coreId -and [string]$Part.coreContractRef -eq 'ES/Automation/Contracts/es-ai-abc-core-v1.json') 'Part binds the authoritative ABCC Core by stable ID and project-relative contract path' $issues

    $coreCapabilities = @(Get-ESAbcStringSet $Core.capabilities | ForEach-Object { [string]$_ })
    $coreCapabilityIds = @($Core.capabilities | ForEach-Object { [string]$_.capabilityId } | Sort-Object -Unique)
    $requiredCore = @($Core.parityContract.requiredCapabilities | ForEach-Object { [string]$_ })
    $missingCore = @($requiredCore | Where-Object { $_ -notin $coreCapabilityIds })
    Add-ESAbcCheck $checks 'abcd-parity' ($missingCore.Count -eq 0 -and $coreCapabilityIds.Count -eq $requiredCore.Count) ($(if ($missingCore.Count -eq 0) { 'ABCC declares the complete six-capability parity set' } else { 'ABCC parity missing: ' + ($missingCore -join ', ') })) $issues
    $partCapabilities = @($Part.capabilityRefs | ForEach-Object { [string]$_ })
    $duplicatePartCapabilities = @($partCapabilities | Group-Object | Where-Object Count -gt 1)
    $unknownPartCapabilities = @($partCapabilities | Where-Object { $_ -notin $coreCapabilityIds })
    Add-ESAbcCheck $checks 'capability-subset' ($partCapabilities.Count -gt 0 -and $duplicatePartCapabilities.Count -eq 0 -and $unknownPartCapabilities.Count -eq 0) ($(if ($unknownPartCapabilities.Count -eq 0) { 'Part capabilityRefs are a unique subset of ABCC capabilities' } else { 'unknown capabilities: ' + ($unknownPartCapabilities -join ', ') })) $issues

    $mappingList = @($Part.aToBMappings)
    $mappingIds = @($mappingList | ForEach-Object { [string]$_.intentId })
    $mappingCapabilities = @($mappingList | ForEach-Object { [string]$_.bCapability })
    $duplicateMappingIds = @($mappingIds | Group-Object | Where-Object Count -gt 1)
    $duplicateMappingCapabilities = @($mappingCapabilities | Group-Object | Where-Object Count -gt 1)
    $unmapped = @($partCapabilities | Where-Object { $_ -notin $mappingCapabilities })
    $unboundMappings = @($mappingCapabilities | Where-Object { $_ -notin $partCapabilities -or $_ -notin $coreCapabilityIds })
    $intentKinds = @($Part.aProfile.intentKinds | ForEach-Object { [string]$_ })
    $unmappedIntentKinds = @($intentKinds | Where-Object { $_ -notin $mappingIds })
    $unknownMappingIntentIds = @($mappingIds | Where-Object { $_ -notin $intentKinds })
    Add-ESAbcCheck $checks 'a-to-b-mapping-closure' ($mappingList.Count -gt 0 -and $duplicateMappingIds.Count -eq 0 -and $duplicateMappingCapabilities.Count -eq 0 -and $unmapped.Count -eq 0 -and $unboundMappings.Count -eq 0 -and $unmappedIntentKinds.Count -eq 0 -and $unknownMappingIntentIds.Count -eq 0) ($(if ($unmapped.Count -eq 0 -and $unboundMappings.Count -eq 0 -and $duplicateMappingCapabilities.Count -eq 0 -and $unmappedIntentKinds.Count -eq 0 -and $unknownMappingIntentIds.Count -eq 0) { 'every declared A intent and Part capability has exactly one deterministic A-to-B mapping' } else { 'unmapped capabilities: ' + ($unmapped -join ', ') + '; duplicate capability mappings: ' + (($duplicateMappingCapabilities | ForEach-Object Name) -join ', ') + '; unbound mappings: ' + ($unboundMappings -join ', ') + '; unmapped intents: ' + ($unmappedIntentKinds -join ', ') + '; unknown mapping intents: ' + ($unknownMappingIntentIds -join ', ') })) $issues

    $compat = $Part.compatibility
    $fallback = $Part.fallback
    Add-ESAbcCheck $checks 'compatibility-boundary' ([string]$compat.canonicalAuthority -eq 'part' -and [bool]$compat.dualTrackAllowed -eq $true -and [bool]$compat.noSilentMerge -eq $true) 'Part is the canonical owner and dual-track merge is explicit-only' $issues
    Add-ESAbcCheck $checks 'dynamic-fallback-boundary' ([string]$fallback.mode -eq 'ABCD.Dynamic' -and [string]$fallback.fallbackContractId -eq 'es.ai-abc.dynamic-fallback.v1' -and [bool]$fallback.explicitOnly -eq $true) 'ABCD.Dynamic fallback is explicit-only' $issues
    Add-ESAbcCheck $checks 'evidence-boundary' (@($Part.evidenceProfile.required).Count -gt 0 -and @($Part.evidenceProfile.runtimeClaimsNotProven).Count -gt 0) 'evidence requirements and runtime non-claims are declared' $issues

    $stages = @($Part.routePlanTemplate)
    $stageIds = @($stages | ForEach-Object { [string]$_.stage })
    $duplicateStages = @($stageIds | Group-Object | Where-Object Count -gt 1)
    $registryStages = @($RouteRegistry.stages)
    $externalInputs = @(Get-ESAbcStringSet $RouteRegistry.externalInputs)
    $available = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($input in $externalInputs) { [void]$available.Add($input) }
    $stageIssues = New-Object 'System.Collections.Generic.List[string]'
    foreach ($stage in $stages) {
        $stageName = [string]$stage.stage
        $registryStage = @($registryStages | Where-Object { [string]$_.stageContractId -eq ('es.route-stage.' + $stageName + '.v1') }) | Select-Object -First 1
        if ($null -eq $registryStage) { [void]$stageIssues.Add("unregistered stage $stageName"); continue }
        $requires = @(Get-ESAbcStringSet $stage.requires)
        $produces = @(Get-ESAbcStringSet $stage.produces)
        $registeredRequires = @(Get-ESAbcStringSet $registryStage.requires)
        $registeredProduces = @(Get-ESAbcStringSet $registryStage.produces)
        if (@(Compare-Object $requires $registeredRequires).Count -gt 0 -or @(Compare-Object $produces $registeredProduces).Count -gt 0) { [void]$stageIssues.Add("stage $stageName differs from RouteStage Registry") }
        foreach ($requirement in $requires) { if (-not $available.Contains($requirement)) { [void]$stageIssues.Add("stage $stageName requires unavailable artifact $requirement") } }
        foreach ($produced in $produces) { [void]$available.Add($produced) }
    }
    Add-ESAbcCheck $checks 'route-stage-closure' ($stages.Count -gt 0 -and $duplicateStages.Count -eq 0 -and $stageIssues.Count -eq 0 -and $available.Contains('weapon-abc-completion')) ($(if ($stageIssues.Count -eq 0) { 'all Part stages are registered and data-flow closes to weapon-abc-completion' } else { $stageIssues -join '; ' })) $issues

    $mode = @($ModeRegistry.modes | Where-Object { [string]$_.modeId -eq 'ABCP.Part' }) | Select-Object -First 1
    Add-ESAbcCheck $checks 'mode-registry-binding' ($null -ne $mode -and [bool]$mode.dependsOnCore -eq $true -and [string]$mode.fallback -eq 'explicit-only') 'ABCP.Part mode registry binding is explicit and Core-dependent' $issues
    $authorityValid = $null -ne $AuthorityRegistry -and [int]$AuthorityRegistry.schemaVersion -eq 1 -and [string]$AuthorityRegistry.registryId -eq 'es.weapon.abc.part.authority.v1' -and [string]$AuthorityRegistry.domain -eq 'weapon'
    Add-ESAbcCheck $checks 'authority-registry-identity' $authorityValid 'Weapon ABCP intent/provider authority registry is present and stable' $issues
    if ($authorityValid) {
        $allowedIntentIds = @($AuthorityRegistry.intents | ForEach-Object { [string]$_.intentId })
        $allowedProviders = @($AuthorityRegistry.providers | ForEach-Object { [string]$_ })
        $unknownIntents = @($intentKinds | Where-Object { $_ -notin $allowedIntentIds })
        $unknownProviders = @($Part.bProfile.providers | ForEach-Object { [string]$_ } | Where-Object { $_ -notin $allowedProviders })
        $authorityMappingIssues = @()
        foreach ($mapping in $mappingList) {
            $expected = @($AuthorityRegistry.intents | Where-Object { [string]$_.intentId -ceq [string]$mapping.intentId }) | Select-Object -First 1
            if ($null -eq $expected -or [string]$expected.bCapability -cne [string]$mapping.bCapability) { $authorityMappingIssues += [string]$mapping.intentId }
        }
        Add-ESAbcCheck $checks 'authority-intent-allowlist' ($unknownIntents.Count -eq 0) ($(if ($unknownIntents.Count -eq 0) { 'all declared Weapon intents are registered' } else { 'unregistered intents: ' + ($unknownIntents -join ', ') })) $issues
        Add-ESAbcCheck $checks 'authority-provider-allowlist' ($unknownProviders.Count -eq 0 -and @($Part.bProfile.providers).Count -gt 0) ($(if ($unknownProviders.Count -eq 0) { 'all declared B providers are registered' } else { 'unregistered providers: ' + ($unknownProviders -join ', ') })) $issues
        Add-ESAbcCheck $checks 'authority-mapping-allowlist' ($authorityMappingIssues.Count -eq 0) ($(if ($authorityMappingIssues.Count -eq 0) { 'every intent maps to the canonical registered capability' } else { 'authority mapping mismatch: ' + ($authorityMappingIssues -join ', ') })) $issues
    }
    return [pscustomobject][ordered]@{ status = if ($issues.Count -eq 0) { 'passed' } else { 'blocked' }; issues = @($issues.ToArray()); checks = @($checks.ToArray()) }
}

function Resolve-ESAbcExecutionMode {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][object]$Part,
        [Parameter(Mandatory=$true)][bool]$CoreBindingPresent,
        [bool]$FallbackRequested = $false,
        [string]$FallbackAuthorizationRef
    )
    if ($null -eq $Part.fallback -or [string]$Part.fallback.mode -cne 'ABCD.Dynamic' -or [string]$Part.fallback.fallbackContractId -cne 'es.ai-abc.dynamic-fallback.v1' -or [bool]$Part.fallback.explicitOnly -ne $true) { throw 'Dynamic fallback contract is invalid.' }
    if($CoreBindingPresent){
        return [pscustomobject][ordered]@{status='selected';mode='ABCP.Part';reason='abc-core-binding-present';eventType='mode-selected';explicit=$true;authorizationRef=$null}
    }
    if(-not $FallbackRequested){
        return [pscustomobject][ordered]@{status='blocked';mode=$null;reason='ABC_CORE_BINDING_REQUIRED';eventType='fallback-denied';explicit=$false;authorizationRef=$null}
    }
    if([string]::IsNullOrWhiteSpace($FallbackAuthorizationRef) -or $FallbackAuthorizationRef -notmatch '^[A-Za-z0-9][A-Za-z0-9._:/-]{0,127}$'){
        return [pscustomobject][ordered]@{status='blocked';mode=$null;reason='EXPLICIT_FALLBACK_AUTH_REQUIRED';eventType='fallback-denied';explicit=$false;authorizationRef=$null}
    }
    return [pscustomobject][ordered]@{status='selected';mode='ABCD.Dynamic';reason='explicit-fallback-authorization';eventType='fallback-selected';explicit=$true;authorizationRef=$FallbackAuthorizationRef}
}

function New-ESAbcPartContractObject {
    param(
        [Parameter(Mandatory = $true)][object]$Request,
        [Parameter(Mandatory = $true)][object]$Core,
        [Parameter(Mandatory = $true)][object]$RouteRegistry,
        [Parameter(Mandatory = $true)][object]$ModeRegistry,
        [object]$AuthorityRegistry
    )
    $requestRequired = @('schemaVersion', 'requestId', 'partId', 'domain', 'capabilityRefs', 'aProfile', 'bProfile', 'cProfile', 'aToBMappings', 'routePlanTemplate')
    $missing = @($requestRequired | Where-Object { -not (Test-ESAbcProperty $Request $_) })
    if ($missing.Count -gt 0) { throw ('Authoring request missing: ' + ($missing -join ', ')) }
    if ([int]$Request.schemaVersion -ne 1) { throw 'Authoring request schemaVersion must be 1.' }
    $part = [ordered]@{
        schemaVersion = 1
        contractId = 'es://automation/contracts/ai-abc/part/v1'
        partId = [string]$Request.partId
        mode = 'ABCP.Part'
        domain = [string]$Request.domain
        coreRef = [string]$Core.coreId
        coreContractRef = 'ES/Automation/Contracts/es-ai-abc-core-v1.json'
        capabilityRefs = @($Request.capabilityRefs | ForEach-Object { [string]$_ })
        aProfile = [ordered]@{ responsibility = [string]$Request.aProfile.responsibility; intentKinds = @($Request.aProfile.intentKinds | ForEach-Object { [string]$_ }) }
        bProfile = [ordered]@{ responsibility = [string]$Request.bProfile.responsibility; providers = @($Request.bProfile.providers | ForEach-Object { [string]$_ }) }
        cProfile = [ordered]@{ collaboratorKinds = @($Request.cProfile.collaboratorKinds | ForEach-Object { [string]$_ }); acceptanceInputs = @($Request.cProfile.acceptanceInputs | ForEach-Object { [string]$_ }) }
        aToBMappings = @($Request.aToBMappings | ForEach-Object { [ordered]@{ intentId = [string]$_.intentId; bCapability = [string]$_.bCapability; input = [string]$_.input; output = [string]$_.output } })
        routePlanTemplate = @($Request.routePlanTemplate | ForEach-Object { [ordered]@{ stage = [string]$_.stage; requires = @($_.requires | ForEach-Object { [string]$_ }); produces = @($_.produces | ForEach-Object { [string]$_ }) } })
        evidenceProfile = if (Test-ESAbcProperty $Request 'evidenceProfile') { [ordered]@{ required = @($Request.evidenceProfile.required | ForEach-Object { [string]$_ }); runtimeClaimsNotProven = @($Request.evidenceProfile.runtimeClaimsNotProven | ForEach-Object { [string]$_ }) } } else { [ordered]@{ required = @('source-snapshot', 'capability-offer', 'normalized-result', 'receipt', 'non-claims'); runtimeClaimsNotProven = @('Unity/Runtime behavior', 'Player/IL2CPP/release behavior') } }
        compatibility = [ordered]@{ canonicalAuthority = 'part'; dualTrackAllowed = $true; legacyAdapter = 'explicit-only'; noSilentMerge = $true }
        fallback = [ordered]@{ mode = 'ABCD.Dynamic'; fallbackContractId = 'es.ai-abc.dynamic-fallback.v1'; explicitOnly = $true }
    }
    $result = Test-ESAbcPartContractObject -Part ([pscustomobject]$part) -Core $Core -RouteRegistry $RouteRegistry -ModeRegistry $ModeRegistry -AuthorityRegistry $AuthorityRegistry
    if ([string]$result.status -ne 'passed') { throw ('Generated Part contract is invalid: ' + ($result.issues -join '; ')) }
    return $part
}

function Get-ESAbcCurrentHead {
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)
    $head = (& git -C $ProjectRoot rev-parse HEAD 2>$null | Select-Object -First 1)
    $head = [string]$head
    if ($head -notmatch '^[a-f0-9]{40}$') { throw 'Current Git HEAD is unavailable; pass an explicit valid 40-character head.' }
    return $head.ToLowerInvariant()
}

function New-ESAbcInterfaceReplay {
    param(
        [Parameter(Mandatory = $true)][object]$Part,
        [Parameter(Mandatory = $true)][object]$Core,
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$ReportPath,
        [Parameter(Mandatory = $true)][string]$Head
    )
    if ($Head -notmatch '^[a-f0-9]{40}$') { throw 'Head must be a lowercase 40-character SHA-1.' }
    $coreById = @{}
    foreach ($capability in @($Core.capabilities)) { $coreById[[string]$capability.capabilityId] = $capability }
    $mappingsByCapability = @{}
    foreach ($mapping in @($Part.aToBMappings)) { $mappingsByCapability[[string]$mapping.bCapability] = $mapping }
    $sourceSeed = ($Core | ConvertTo-Json -Depth 20 -Compress) + '|' + ($Part | ConvertTo-Json -Depth 20 -Compress) + '|es-ai-abc-interface-v1'
    $sourceSetHash = Get-ESAbcSha256Text $sourceSeed
    $reportRef = $ReportPath.Replace('\', '/')
    $replays = New-Object 'System.Collections.Generic.List[object]'
    foreach ($capabilityId in @($Part.capabilityRefs)) {
        $capability = $coreById[[string]$capabilityId]
        $mapping = $mappingsByCapability[[string]$capabilityId]
        if ($null -eq $capability -or $null -eq $mapping) { continue }
        $intentId = ([string]$Part.partId + '.intent.' + [string]$mapping.intentId).ToLowerInvariant()
        $exchangeId = ([string]$Part.partId + '.exchange.' + [string]$capabilityId).ToLowerInvariant()
        [void]$replays.Add([ordered]@{
            schemaVersion = 1
            contractId = 'es://automation/contracts/ai-abc/interface/v1'
            aIntentEnvelope = [ordered]@{
                contractId = 'es://automation/contracts/ai-abc/interface/v1'
                version = 'v1'
                intentId = $intentId
                goalRevision = 'r1'
                semanticGoal = 'Static ABC replay for ' + [string]$Part.partId + ' / ' + [string]$mapping.intentId
                constraints = @('ABCC-core-bound', 'execution-disabled', 'static-only')
                requestedCapabilities = @($Part.capabilityRefs | ForEach-Object { [string]$_ })
                evidenceExpectations = @($Part.evidenceProfile.required | ForEach-Object { [string]$_ })
                authorization = [ordered]@{ kind = 'read-only'; scope = [string]$Part.partId; executionEnabled = $false }
                sourceSnapshot = [ordered]@{ projectRootRelative = '.'; head = $Head; sourceSetHash = $sourceSetHash }
                adapterState = 'accepted'
            }
            bCapabilityOffer = [ordered]@{
                capabilityId = [string]$capability.capabilityId
                providerId = 'es.ai-abc.core.v1'
                semanticDescription = [string]$capability.outputSemantics
                inputSchemaRef = 'ES/Automation/Contracts/es-ai-abc-interface-v1.schema.json#/$defs/aIntentEnvelope'
                outputSchemaRef = 'ES/Automation/Contracts/es-ai-abc-interface-v1.schema.json#/$defs/adapterExchange'
                preconditions = @($capability.preconditions | ForEach-Object { [string]$_ })
                effects = @('static-replay-only')
                evidenceProduced = @($capability.evidence | ForEach-Object { [string]$_ })
                failureCodes = @($capability.failureCodes | ForEach-Object { [string]$_ })
                version = 'v1'
                compatibility = [ordered]@{ acceptsIntentVersion = 'v1'; resultVersion = 'v1'; breakingChangePolicy = 'replan' }
            }
            adapterExchange = [ordered]@{
                exchangeId = $exchangeId
                aIntentRef = 'ES/Automation/ABC/' + $exchangeId + '/a-intent.json'
                bOfferRef = 'ES/Automation/ABC/' + $exchangeId + '/b-offer.json'
                mapping = @([ordered]@{ aField = 'semanticGoal'; bField = 'inputSemantics'; transformation = [string]$mapping.input; lossPolicy = 'none' })
                normalizedResult = [ordered]@{ status = 'accepted'; summary = [string]$mapping.output; outputs = [ordered]@{ partId = [string]$Part.partId; capabilityId = [string]$capabilityId; static = $true }; failureCode = $null }
                evidenceSetRef = $reportRef
                receiptRef = $reportRef
                status = 'accepted'
            }
            cCollaboratorEnvelope = [ordered]@{
                collaboratorId = 'abc-static-replay'
                kind = 'ai'
                goal = 'Validate the ABCP contract through ABCC'
                authorization = 'read-only static replay'
                choices = @('accept-static-evidence', 'replan-on-mismatch')
                acceptance = 'accepted'
            }
        })
    }
    return @($replays.ToArray())
}

function Test-ESAbcInterfaceReplays {
    param([Parameter(Mandatory = $true)][object[]]$Replays, [Parameter(Mandatory = $true)][object]$Part)
    $issues = New-Object 'System.Collections.Generic.List[string]'
    $expected = @($Part.capabilityRefs | ForEach-Object { [string]$_ })
    if ($Replays.Count -ne $expected.Count) { [void]$issues.Add("interface replay count $($Replays.Count) does not match capability count $($expected.Count)") }
    foreach ($replay in $Replays) {
        if ([string]$replay.contractId -ne 'es://automation/contracts/ai-abc/interface/v1') { [void]$issues.Add('interface contractId mismatch') }
        if ($replay.aIntentEnvelope.authorization.executionEnabled -ne $false) { [void]$issues.Add('interface replay executionEnabled is not false') }
        $capability = [string]$replay.bCapabilityOffer.capabilityId
        if ($capability -notin $expected) { [void]$issues.Add("interface replay capability is not declared by Part: $capability") }
        if ([string]$replay.adapterExchange.status -ne 'accepted' -or [string]$replay.adapterExchange.normalizedResult.status -ne 'accepted' -or $null -ne $replay.adapterExchange.normalizedResult.failureCode) { [void]$issues.Add("interface replay did not normalize accepted result: $capability") }
        if ([string]$replay.cCollaboratorEnvelope.acceptance -ne 'accepted') { [void]$issues.Add("collaborator acceptance missing: $capability") }
        if ([string]$replay.adapterExchange.evidenceSetRef -notmatch '^(?![A-Za-z]:|/|\\).+' -or [string]$replay.adapterExchange.receiptRef -notmatch '^(?![A-Za-z]:|/|\\).+') { [void]$issues.Add("interface evidence reference is not project-relative: $capability") }
    }
    return [pscustomobject][ordered]@{ status = if ($issues.Count -eq 0) { 'passed' } else { 'blocked' }; issues = @($issues.ToArray()); replayCount = $Replays.Count }
}

Export-ModuleMember -Function @(
    'Resolve-ESAbcProjectPath', 'Get-ESAbcRelativePath', 'Read-ESAbcJson', 'Write-ESAbcJson',
    'Get-ESAbcCoreContract', 'Get-ESAbcRouteStageRegistry', 'Get-ESAbcModeRegistry', 'Get-ESAbcPartAuthorityRegistry',
    'Test-ESAbcPartContractObject', 'New-ESAbcPartContractObject', 'Resolve-ESAbcExecutionMode', 'Get-ESAbcCurrentHead',
    'New-ESAbcInterfaceReplay', 'Test-ESAbcInterfaceReplays'
)
