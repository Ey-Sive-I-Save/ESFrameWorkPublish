[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [Parameter(Mandatory = $true)][string]$ReceiptPath,
    [switch]$SkipCurrentInputCheck,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()
. (Join-Path $PSScriptRoot 'ESUnityBuildIdentity.Common.ps1')

function Assert-ESBuildReceiptKeys {
    param(
        [Parameter(Mandatory = $true)][object]$Value,
        [Parameter(Mandatory = $true)][string[]]$Required,
        [Parameter(Mandatory = $true)][string[]]$Allowed,
        [Parameter(Mandatory = $true)][string]$Context
    )
    if ($null -eq $Value) { throw "$Context must be an object." }
    $actual = @($Value.PSObject.Properties.Name)
    foreach ($name in $Required) {
        if ($actual -notcontains $name) { throw "$Context is missing required property: $name" }
    }
    foreach ($name in $actual) {
        if ($Allowed -notcontains $name) { throw "$Context contains an unsupported property: $name" }
    }
}

function Assert-ESBuildReceiptRelativePath {
    param([string]$Root, [string]$Path, [string]$Context, [string]$RequiredPrefix)
    if ([string]::IsNullOrWhiteSpace($Path)) { throw "$Context must not be empty." }
    return Resolve-ESBuildRelativePath -Root $Root -RelativePath $Path -RequiredPrefix $RequiredPrefix
}

function Assert-ESBuildReceiptHash {
    param([string]$Value, [string]$Context, [switch]$AllowState)
    if ($Value -match '^[0-9a-f]{64}$') { return }
    if ($AllowState -and $Value -in @('deleted', 'not-applicable', 'unknown')) { return }
    throw "$Context is not a valid lowercase SHA-256 value."
}

function Assert-ESBuildReceiptDate {
    param([string]$Value, [string]$Context)
    $parsed = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParse($Value, [ref]$parsed)) { throw "$Context is not a valid timestamp." }
    return $parsed
}

function Assert-ESBuildFileEntries {
    param([string]$Root, [object[]]$Entries, [string]$Context, [switch]$RequireHash)
    $paths = @{}
    foreach ($entry in @($Entries)) {
        Assert-ESBuildReceiptKeys -Value $entry -Required @('path', 'byteLength', 'sha256') -Allowed @('path', 'byteLength', 'sha256') -Context "$Context entry"
        $relative = [string]$entry.path
        [void](Assert-ESBuildReceiptRelativePath -Root $Root -Path $relative -Context "$Context path")
        if ($paths.ContainsKey($relative)) { throw "$Context contains a duplicate path: $relative" }
        $paths[$relative] = $true
        if ([long]$entry.byteLength -lt 0) { throw "$Context byteLength must not be negative: $relative" }
        Assert-ESBuildReceiptHash -Value ([string]$entry.sha256) -Context "$Context sha256" -AllowState:(-not $RequireHash)
    }
}

function Compare-ESBuildReceiptValue {
    param([object]$Expected, [object]$Actual, [string]$Context)
    $expectedJson = ConvertTo-ESBuildCanonicalJson -Value $Expected
    $actualJson = ConvertTo-ESBuildCanonicalJson -Value $Actual
    if ($expectedJson -cne $actualJson) { throw "$Context does not match the current hashed source." }
}

try {
    $root = Resolve-ESBuildProjectRoot -ProjectRoot $ProjectRoot
    $receiptResolved = Resolve-ESBuildRelativePath -Root $root -RelativePath $ReceiptPath `
        -RequiredPrefix $script:ESBuildIdentityReceiptRoot -PathType File -MustExist
    if (-not $receiptResolved.relative.EndsWith('.json', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'ReceiptPath must end in .json.'
    }
    $receipt = Read-ESBuildIdentityJson -Path $receiptResolved.full

    $topKeys = @(
        'schemaVersion', 'fingerprintSchemaVersion', 'contractRef', 'contractHash', 'receiptId', 'phase',
        'capturedAtUtc', 'project', 'intent', 'inputIdentity', 'buildInputFingerprint', 'execution',
        'artifacts', 'artifactManifestHash', 'provenanceVerdict', 'claimsNotProven', 'staleWhen'
    )
    Assert-ESBuildReceiptKeys -Value $receipt -Required $topKeys -Allowed $topKeys -Context 'receipt'
    if ([int]$receipt.schemaVersion -ne 1 -or [int]$receipt.fingerprintSchemaVersion -ne 1) { throw 'Only receipt and fingerprint schema version 1 are accepted.' }
    if ([string]$receipt.contractRef -cne $script:ESBuildIdentityContractRef) { throw 'contractRef does not identify the authoritative build identity contract.' }
    $contract = Get-ESBuildContractIdentity -Root $root
    Assert-ESBuildReceiptHash -Value ([string]$receipt.contractHash) -Context 'contractHash'
    if ([string]$receipt.contractHash -cne $contract.hash) { throw 'contractHash is stale or does not match the authoritative contract.' }
    if ([string]$receipt.receiptId -notmatch '^[a-z0-9][a-z0-9._-]{7,127}$') { throw 'receiptId is invalid.' }
    if ([string]$receipt.phase -notin @('input-snapshot', 'finalized')) { throw 'phase is invalid.' }
    [void](Assert-ESBuildReceiptDate -Value ([string]$receipt.capturedAtUtc) -Context 'capturedAtUtc')
    if ([string]::IsNullOrWhiteSpace([string]$receipt.staleWhen)) { throw 'staleWhen must not be empty.' }
    if (@($receipt.claimsNotProven).Count -lt 1 -or @($receipt.claimsNotProven | Where-Object { [string]::IsNullOrWhiteSpace([string]$_) }).Count -gt 0) {
        throw 'claimsNotProven must contain at least one non-empty statement.'
    }

    $projectKeys = @('projectId', 'projectRoot', 'branch', 'gitHead', 'unityVersion', 'unityRevision')
    Assert-ESBuildReceiptKeys -Value $receipt.project -Required $projectKeys -Allowed $projectKeys -Context 'project'
    if ([string]$receipt.project.projectId -notmatch '^[a-z0-9][a-z0-9._-]{2,127}$') { throw 'project.projectId is invalid.' }
    if ([string]$receipt.project.gitHead -notmatch '^[0-9a-f]{40}$') { throw 'project.gitHead is not a lowercase Git commit identity.' }
    foreach ($name in @('projectRoot', 'branch', 'unityVersion', 'unityRevision')) {
        if ([string]::IsNullOrWhiteSpace([string]$receipt.project.$name)) { throw "project.$name must not be empty." }
    }

    $intentKeys = @('buildTarget', 'buildTargetGroup', 'architecture', 'scriptingBackend', 'development', 'buildOptions', 'outputPath')
    Assert-ESBuildReceiptKeys -Value $receipt.intent -Required $intentKeys -Allowed $intentKeys -Context 'intent'
    foreach ($name in @('buildTarget', 'buildTargetGroup', 'architecture')) {
        if ([string]::IsNullOrWhiteSpace([string]$receipt.intent.$name)) { throw "intent.$name must not be empty." }
    }
    if ([string]$receipt.intent.scriptingBackend -notin @('Mono', 'IL2CPP')) { throw 'intent.scriptingBackend is invalid.' }
    $output = Assert-ESBuildReceiptRelativePath -Root $root -Path ([string]$receipt.intent.outputPath) -Context 'intent.outputPath' -RequiredPrefix 'ES/Output/Builds'
    $options = @($receipt.intent.buildOptions | ForEach-Object { [string]$_ })
    if (@($options | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -gt 0 -or @($options | Sort-Object -Unique).Count -ne $options.Count) {
        throw 'intent.buildOptions must contain unique non-empty values.'
    }

    $inputKeys = @(
        'worktreeState', 'worktreeExclusions', 'worktreeManifest', 'scopedChangeManifestHash',
        'projectSettingsHash', 'projectConfigurationManifest', 'projectConfigurationManifestHash',
        'packageManifestHash', 'packageLockHash', 'scenes', 'sceneListHash', 'defineSymbols',
        'managedStrippingLevel', 'stripEngineCode', 'hybridClr'
    )
    Assert-ESBuildReceiptKeys -Value $receipt.inputIdentity -Required $inputKeys -Allowed $inputKeys -Context 'inputIdentity'
    if ([string]$receipt.inputIdentity.worktreeState -notin @('clean', 'dirty')) { throw 'inputIdentity.worktreeState is invalid.' }
    $exclusions = @($receipt.inputIdentity.worktreeExclusions | ForEach-Object { [string]$_ })
    if ($exclusions.Count -lt 1 -or @($exclusions | Sort-Object -Unique).Count -ne $exclusions.Count) { throw 'worktreeExclusions must be a non-empty unique set.' }
    foreach ($excluded in $exclusions) { [void](Assert-ESBuildReceiptRelativePath -Root $root -Path $excluded -Context 'worktreeExclusions entry') }
    if ($exclusions -notcontains $script:ESBuildIdentityReceiptRoot -or $exclusions -notcontains $output.relative) {
        throw 'worktreeExclusions must declare the receipt root and exact build output path.'
    }

    $worktreePaths = @{}
    foreach ($entry in @($receipt.inputIdentity.worktreeManifest)) {
        $keys = @('status', 'path', 'originalPath', 'byteLength', 'sha256')
        Assert-ESBuildReceiptKeys -Value $entry -Required $keys -Allowed $keys -Context 'worktreeManifest entry'
        if ([string]$entry.status -notmatch '^[ MADRCU?!]{2}$') { throw 'worktreeManifest status is invalid.' }
        $path = [string]$entry.path
        [void](Assert-ESBuildReceiptRelativePath -Root $root -Path $path -Context 'worktreeManifest path')
        if ($worktreePaths.ContainsKey($path)) { throw "worktreeManifest contains a duplicate path: $path" }
        $worktreePaths[$path] = $true
        if ([string]$entry.originalPath -ne 'not-applicable') { [void](Assert-ESBuildReceiptRelativePath -Root $root -Path ([string]$entry.originalPath) -Context 'worktreeManifest originalPath') }
        if ([long]$entry.byteLength -lt 0) { throw 'worktreeManifest byteLength must not be negative.' }
        Assert-ESBuildReceiptHash -Value ([string]$entry.sha256) -Context 'worktreeManifest sha256' -AllowState
    }
    if ((@($receipt.inputIdentity.worktreeManifest).Count -eq 0) -ne ([string]$receipt.inputIdentity.worktreeState -eq 'clean')) {
        throw 'worktreeState does not match worktreeManifest.'
    }
    Assert-ESBuildReceiptHash -Value ([string]$receipt.inputIdentity.scopedChangeManifestHash) -Context 'scopedChangeManifestHash'
    if ([string]$receipt.inputIdentity.scopedChangeManifestHash -cne (Get-ESBuildObjectHash -Value @($receipt.inputIdentity.worktreeManifest))) {
        throw 'scopedChangeManifestHash does not match worktreeManifest.'
    }

    Assert-ESBuildFileEntries -Root $root -Entries @($receipt.inputIdentity.projectConfigurationManifest) -Context 'projectConfigurationManifest' -RequireHash
    if (@($receipt.inputIdentity.projectConfigurationManifest).Count -lt 1) { throw 'projectConfigurationManifest must not be empty.' }
    foreach ($name in @('projectSettingsHash', 'projectConfigurationManifestHash', 'packageManifestHash', 'packageLockHash', 'sceneListHash')) {
        Assert-ESBuildReceiptHash -Value ([string]$receipt.inputIdentity.$name) -Context $name
    }
    if ([string]$receipt.inputIdentity.projectConfigurationManifestHash -cne (Get-ESBuildObjectHash -Value @($receipt.inputIdentity.projectConfigurationManifest))) {
        throw 'projectConfigurationManifestHash does not match projectConfigurationManifest.'
    }

    $scenePaths = @{}
    $expectedOrder = 0
    foreach ($scene in @($receipt.inputIdentity.scenes)) {
        $keys = @('order', 'path', 'sha256')
        Assert-ESBuildReceiptKeys -Value $scene -Required $keys -Allowed $keys -Context 'scene entry'
        if ([int]$scene.order -ne $expectedOrder) { throw 'Scene order must be contiguous and deterministic.' }
        $expectedOrder++
        $scenePath = [string]$scene.path
        [void](Assert-ESBuildReceiptRelativePath -Root $root -Path $scenePath -Context 'scene path' -RequiredPrefix 'Assets')
        if (-not $scenePath.EndsWith('.unity', [StringComparison]::OrdinalIgnoreCase) -or $scenePaths.ContainsKey($scenePath)) { throw "Scene path is invalid or duplicated: $scenePath" }
        $scenePaths[$scenePath] = $true
        Assert-ESBuildReceiptHash -Value ([string]$scene.sha256) -Context 'scene sha256'
    }
    if ([string]$receipt.inputIdentity.sceneListHash -cne (Get-ESBuildObjectHash -Value @($receipt.inputIdentity.scenes))) { throw 'sceneListHash does not match scenes.' }
    $defines = @($receipt.inputIdentity.defineSymbols | ForEach-Object { [string]$_ })
    if (@($defines | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -gt 0 -or @($defines | Sort-Object -Unique).Count -ne $defines.Count) {
        throw 'defineSymbols must contain unique non-empty values.'
    }
    if ([string]::IsNullOrWhiteSpace([string]$receipt.inputIdentity.managedStrippingLevel)) { throw 'managedStrippingLevel must not be empty.' }

    $hybridKeys = @(
        'mode', 'settingsHash', 'packageVersion', 'packageManifestHash', 'hotUpdateDllManifest',
        'hotUpdateDllManifestHash', 'strippedAotDllManifest', 'strippedAotDllManifestHash',
        'linkXmlHash', 'aotGenericReferencesHash'
    )
    Assert-ESBuildReceiptKeys -Value $receipt.inputIdentity.hybridClr -Required $hybridKeys -Allowed $hybridKeys -Context 'hybridClr'
    if ([string]$receipt.inputIdentity.hybridClr.mode -notin @('enabled', 'disabled')) { throw 'hybridClr.mode is invalid.' }
    Assert-ESBuildReceiptHash -Value ([string]$receipt.inputIdentity.hybridClr.settingsHash) -Context 'hybridClr.settingsHash' -AllowState
    Assert-ESBuildReceiptHash -Value ([string]$receipt.inputIdentity.hybridClr.packageManifestHash) -Context 'hybridClr.packageManifestHash' -AllowState
    if ([string]::IsNullOrWhiteSpace([string]$receipt.inputIdentity.hybridClr.packageVersion)) { throw 'hybridClr.packageVersion must not be empty.' }
    Assert-ESBuildFileEntries -Root $root -Entries @($receipt.inputIdentity.hybridClr.hotUpdateDllManifest) -Context 'hybridClr.hotUpdateDllManifest' -RequireHash
    Assert-ESBuildFileEntries -Root $root -Entries @($receipt.inputIdentity.hybridClr.strippedAotDllManifest) -Context 'hybridClr.strippedAotDllManifest' -RequireHash
    foreach ($name in @('hotUpdateDllManifestHash', 'strippedAotDllManifestHash', 'linkXmlHash', 'aotGenericReferencesHash')) {
        Assert-ESBuildReceiptHash -Value ([string]$receipt.inputIdentity.hybridClr.$name) -Context "hybridClr.$name" -AllowState
    }
    $hotUpdateManifestHash = [string]$receipt.inputIdentity.hybridClr.hotUpdateDllManifestHash
    if ($hotUpdateManifestHash -match '^[0-9a-f]{64}$' -and $hotUpdateManifestHash -cne (Get-ESBuildObjectHash -Value @($receipt.inputIdentity.hybridClr.hotUpdateDllManifest))) {
        throw 'hybridClr.hotUpdateDllManifestHash does not match its manifest.'
    }
    $strippedAotManifestHash = [string]$receipt.inputIdentity.hybridClr.strippedAotDllManifestHash
    if ($strippedAotManifestHash -match '^[0-9a-f]{64}$' -and $strippedAotManifestHash -cne (Get-ESBuildObjectHash -Value @($receipt.inputIdentity.hybridClr.strippedAotDllManifest))) {
        throw 'hybridClr.strippedAotDllManifestHash does not match its manifest.'
    }
    if ([string]$receipt.inputIdentity.hybridClr.mode -eq 'disabled' -and (
        @($receipt.inputIdentity.hybridClr.hotUpdateDllManifest).Count -ne 0 -or @($receipt.inputIdentity.hybridClr.strippedAotDllManifest).Count -ne 0)) {
        throw 'Disabled HybridCLR identity must not contain generated-input manifests.'
    }
    $identityIncomplete = Test-ESBuildIdentityIncomplete -InputIdentity $receipt.inputIdentity

    Assert-ESBuildReceiptHash -Value ([string]$receipt.buildInputFingerprint) -Context 'buildInputFingerprint'
    $storedFingerprint = Get-ESBuildInputFingerprint -Project $receipt.project -Intent $receipt.intent -InputIdentity $receipt.inputIdentity
    if ([string]$receipt.buildInputFingerprint -cne $storedFingerprint) { throw 'buildInputFingerprint does not match the stored input identity.' }

    $artifacts = @($receipt.artifacts)
    if ($artifacts.Count -gt 256) { throw 'Artifact count exceeds the contract budget.' }
    $roles = @{}
    $recomputedArtifacts = New-Object Collections.Generic.List[object]
    foreach ($artifact in $artifacts) {
        $keys = @('role', 'path', 'kind', 'byteLength', 'sha256', 'files')
        Assert-ESBuildReceiptKeys -Value $artifact -Required $keys -Allowed $keys -Context 'artifact'
        $role = [string]$artifact.role
        if ($role -notmatch '^[a-z0-9][a-z0-9._-]{1,63}$' -or $roles.ContainsKey($role)) { throw "Artifact role is invalid or duplicated: $role" }
        $roles[$role] = $true
        if ([string]$artifact.kind -notin @('file', 'directory')) { throw "Artifact kind is invalid: $role" }
        Assert-ESBuildFileEntries -Root $root -Entries @($artifact.files) -Context "artifact $role files" -RequireHash
        Assert-ESBuildReceiptHash -Value ([string]$artifact.sha256) -Context "artifact $role sha256"
        $actual = Get-ESBuildArtifactIdentity -Root $root -OutputRoot $output.relative -Role $role -Path ([string]$artifact.path)
        Compare-ESBuildReceiptValue -Expected $actual -Actual $artifact -Context "artifact $role"
        [void]$recomputedArtifacts.Add($actual)
    }
    $sortedArtifacts = @($recomputedArtifacts.ToArray() | Sort-Object role, path)
    Compare-ESBuildReceiptValue -Expected $sortedArtifacts -Actual $artifacts -Context 'artifact ordering'

    if ([string]$receipt.phase -eq 'input-snapshot') {
        if ($null -ne $receipt.execution -or $artifacts.Count -ne 0 -or [string]$receipt.artifactManifestHash -ne 'not-applicable') {
            throw 'input-snapshot must not contain execution or artifacts.'
        }
        $expectedSnapshotVerdict = if ($identityIncomplete) { 'identity-incomplete' } else { 'input-captured' }
        if ([string]$receipt.provenanceVerdict -ne $expectedSnapshotVerdict) { throw 'input-snapshot provenanceVerdict does not match identity completeness.' }
    }
    else {
        $executionKeys = @(
            'actorId', 'taskId', 'planHash', 'commandHash', 'skillHashes', 'startedAtUtc', 'finishedAtUtc',
            'unityExecutableHash', 'toolchainIdentity', 'effectiveArguments', 'status', 'failure', 'recovery',
            'inputIdentityHashBefore', 'inputIdentityHashAfter'
        )
        Assert-ESBuildReceiptKeys -Value $receipt.execution -Required $executionKeys -Allowed $executionKeys -Context 'execution'
        foreach ($name in @('actorId', 'taskId', 'toolchainIdentity', 'recovery')) {
            if ([string]::IsNullOrWhiteSpace([string]$receipt.execution.$name)) { throw "execution.$name must not be empty." }
        }
        foreach ($name in @('planHash', 'commandHash')) {
            $value = [string]$receipt.execution.$name
            if ($value -ne 'not-applicable') { Assert-ESBuildReceiptHash -Value $value -Context "execution.$name" }
        }
        foreach ($skillHash in @($receipt.execution.skillHashes)) { Assert-ESBuildReceiptHash -Value ([string]$skillHash) -Context 'execution.skillHashes entry' }
        $started = Assert-ESBuildReceiptDate -Value ([string]$receipt.execution.startedAtUtc) -Context 'execution.startedAtUtc'
        $finished = Assert-ESBuildReceiptDate -Value ([string]$receipt.execution.finishedAtUtc) -Context 'execution.finishedAtUtc'
        if ($finished -lt $started) { throw 'execution timestamps are out of order.' }
        $unityHash = [string]$receipt.execution.unityExecutableHash
        if ($unityHash -ne 'not-applicable') { Assert-ESBuildReceiptHash -Value $unityHash -Context 'execution.unityExecutableHash' }
        if ([string]$receipt.execution.status -notin @('passed', 'failed', 'blocked', 'cancelled', 'interrupted', 'input-drifted')) { throw 'execution.status is invalid.' }
        Assert-ESBuildReceiptHash -Value ([string]$receipt.execution.inputIdentityHashBefore) -Context 'execution.inputIdentityHashBefore'
        Assert-ESBuildReceiptHash -Value ([string]$receipt.execution.inputIdentityHashAfter) -Context 'execution.inputIdentityHashAfter'
        if ([string]$receipt.execution.inputIdentityHashBefore -cne [string]$receipt.buildInputFingerprint) { throw 'execution inputIdentityHashBefore does not match the captured fingerprint.' }
        if ([string]$receipt.artifactManifestHash -notmatch '^[0-9a-f]{64}$' -or [string]$receipt.artifactManifestHash -cne (Get-ESBuildObjectHash -Value $artifacts)) {
            throw 'artifactManifestHash does not match artifacts.'
        }
        if ([string]$receipt.execution.status -eq 'passed') {
            if (@($artifacts | Where-Object { $_.role -in @('build-log', 'build-report') -and $_.kind -eq 'file' -and [long]$_.byteLength -gt 0 }).Count -eq 0) { throw 'A passed receipt requires a non-empty build-log or build-report file.' }
            if ($unityHash -eq 'not-applicable') { throw 'A passed receipt requires a hashed Unity executable.' }
            if ([string]$receipt.intent.scriptingBackend -eq 'IL2CPP' -and [string]$receipt.execution.toolchainIdentity -in @('not-run', 'not-applicable', 'unknown')) { throw 'A passed IL2CPP receipt requires a concrete toolchain identity.' }
        }
        $finalizeDrift = [string]$receipt.execution.inputIdentityHashBefore -cne [string]$receipt.execution.inputIdentityHashAfter
        if ($finalizeDrift -ne ([string]$receipt.execution.status -eq 'input-drifted')) { throw 'execution status does not match the finalize-time input drift state.' }
        if ($finalizeDrift -ne ([string]$receipt.provenanceVerdict -eq 'input-drifted')) { throw 'provenanceVerdict does not match the finalize-time input drift state.' }
        if (-not $finalizeDrift) {
            $expectedFinalVerdict = if ($identityIncomplete) { 'identity-incomplete' } else { 'provenance-bound' }
            if ([string]$receipt.provenanceVerdict -ne $expectedFinalVerdict) { throw 'Finalized provenanceVerdict does not match identity completeness.' }
        }
    }

    $currentFingerprint = 'not-checked'
    $stale = $false
    if (-not $SkipCurrentInputCheck) {
        $scenePathValues = @($receipt.inputIdentity.scenes | Sort-Object order | ForEach-Object { [string]$_.path })
        $current = New-ESBuildInputState -Root $root -ProjectId ([string]$receipt.project.projectId) `
            -BuildTarget ([string]$receipt.intent.buildTarget) -BuildTargetGroup ([string]$receipt.intent.buildTargetGroup) `
            -Architecture ([string]$receipt.intent.architecture) -ScriptingBackend ([string]$receipt.intent.scriptingBackend) `
            -Development ([bool]$receipt.intent.development) -BuildOption @($receipt.intent.buildOptions) `
            -OutputPath ([string]$receipt.intent.outputPath) -ScenePath $scenePathValues `
            -DefineSymbol @($receipt.inputIdentity.defineSymbols) -ManagedStrippingLevel ([string]$receipt.inputIdentity.managedStrippingLevel) `
            -StripEngineCode ([bool]$receipt.inputIdentity.stripEngineCode)
        $currentFingerprint = Get-ESBuildInputFingerprint -Project $current.project -Intent $current.intent -InputIdentity $current.inputIdentity
        $expectedCurrent = if ([string]$receipt.phase -eq 'finalized') { [string]$receipt.execution.inputIdentityHashAfter } else { [string]$receipt.buildInputFingerprint }
        $rootNormalized = $root.Replace('\', '/')
        if ([string]$receipt.project.projectRoot -cne $rootNormalized -or [string]$receipt.project.branch -cne [string]$current.project.branch -or
            [string]$receipt.project.gitHead -cne [string]$current.project.gitHead -or [string]$receipt.project.unityVersion -cne [string]$current.project.unityVersion -or
            [string]$receipt.project.unityRevision -cne [string]$current.project.unityRevision -or $currentFingerprint -cne $expectedCurrent) {
            $stale = $true
        }
    }
    if ([string]$receipt.phase -eq 'finalized' -and [string]$receipt.execution.status -eq 'input-drifted') { $stale = $true }

    $result = [ordered]@{
        status = if ($stale) { 'stale' } else { 'passed' }
        receiptPath = $receiptResolved.relative
        phase = [string]$receipt.phase
        buildInputFingerprint = [string]$receipt.buildInputFingerprint
        currentInputFingerprint = $currentFingerprint
        artifactCount = $artifacts.Count
        provenanceVerdict = [string]$receipt.provenanceVerdict
        currentInputChecked = -not [bool]$SkipCurrentInputCheck
        evidenceLevel = 'S1'
        runtime = 'runtime-not-run'
    }
    if ($Json) { $result | ConvertTo-Json -Depth 8 } else { [pscustomobject]$result }
    if ($stale) { exit 2 }
}
catch {
    $failure = [ordered]@{
        status = 'invalid'
        receiptPath = $ReceiptPath
        reason = $_.Exception.Message
        evidenceLevel = 'S1'
        runtime = 'runtime-not-run'
    }
    if ($Json) { $failure | ConvertTo-Json -Depth 8 } else { [pscustomobject]$failure }
    exit 1
}
