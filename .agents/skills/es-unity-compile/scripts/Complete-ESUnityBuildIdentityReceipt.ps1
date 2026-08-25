[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [Parameter(Mandatory = $true)][string]$SnapshotPath,
    [Parameter(Mandatory = $true)][string]$ReceiptPath,
    [Parameter(Mandatory = $true)][string]$ActorId,
    [Parameter(Mandatory = $true)][string]$TaskId,
    [ValidatePattern('^(not-applicable|[0-9a-f]{64})$')][string]$PlanHash = 'not-applicable',
    [ValidatePattern('^(not-applicable|[0-9a-f]{64})$')][string]$CommandHash = 'not-applicable',
    [ValidatePattern('^[0-9a-f]{64}$')][string[]]$SkillHash = @(),
    [Parameter(Mandatory = $true)][string]$StartedAtUtc,
    [Parameter(Mandatory = $true)][string]$FinishedAtUtc,
    [string]$UnityExecutablePath,
    [Parameter(Mandatory = $true)][string]$ToolchainIdentity,
    [string[]]$EffectiveArgument = @(),
    [Parameter(Mandatory = $true)][ValidateSet('passed', 'failed', 'blocked', 'cancelled', 'interrupted')][string]$Status,
    [string]$Failure = '',
    [Parameter(Mandatory = $true)][string]$Recovery,
    [hashtable[]]$Artifact = @(),
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()
if (-not [IO.Path]::IsPathRooted($ProjectRoot)) { throw 'ProjectRoot must be absolute.' }
$boundedProjectRoot = [IO.Path]::GetFullPath($ProjectRoot)
. (Join-Path $PSScriptRoot 'ESUnityBuildIdentity.Common.ps1')

$root = Resolve-ESBuildProjectRoot -ProjectRoot $boundedProjectRoot
$snapshotResolved = Resolve-ESBuildRelativePath -Root $root -RelativePath $SnapshotPath -RequiredPrefix $script:ESBuildIdentityReceiptRoot -PathType File -MustExist
$snapshot = Read-ESBuildIdentityJson -Path $snapshotResolved.full
if ([int]$snapshot.schemaVersion -ne 1 -or [string]$snapshot.phase -ne 'input-snapshot') { throw 'SnapshotPath must reference a schema v1 input-snapshot receipt.' }
$contract = Get-ESBuildContractIdentity -Root $root
if ([string]$snapshot.contractRef -cne $contract.reference -or [string]$snapshot.contractHash -cne $contract.hash) { throw 'Snapshot contract identity is stale or invalid.' }
$snapshotFingerprint = Get-ESBuildInputFingerprint -Project $snapshot.project -Intent $snapshot.intent -InputIdentity $snapshot.inputIdentity
if ([string]$snapshot.buildInputFingerprint -cne $snapshotFingerprint) { throw 'Snapshot buildInputFingerprint does not match its stored input identity.' }
if ([string]$snapshot.project.projectRoot -cne $root.Replace('\', '/')) { throw 'Snapshot projectRoot does not match the current project.' }
$expectedSnapshotVerdict = if (Test-ESBuildIdentityIncomplete -InputIdentity $snapshot.inputIdentity) { 'identity-incomplete' } else { 'input-captured' }
if ([string]$snapshot.provenanceVerdict -ne $expectedSnapshotVerdict) { throw 'Snapshot provenanceVerdict does not match identity completeness.' }

$started = [DateTimeOffset]::MinValue
$finished = [DateTimeOffset]::MinValue
if (-not [DateTimeOffset]::TryParse($StartedAtUtc, [ref]$started) -or -not [DateTimeOffset]::TryParse($FinishedAtUtc, [ref]$finished) -or $finished -lt $started) {
    throw 'StartedAtUtc and FinishedAtUtc must be valid ordered timestamps.'
}

$scenePaths = @($snapshot.inputIdentity.scenes | Sort-Object order | ForEach-Object { [string]$_.path })
$state = New-ESBuildInputState -Root $root -ProjectId ([string]$snapshot.project.projectId) `
    -BuildTarget ([string]$snapshot.intent.buildTarget) -BuildTargetGroup ([string]$snapshot.intent.buildTargetGroup) `
    -Architecture ([string]$snapshot.intent.architecture) -ScriptingBackend ([string]$snapshot.intent.scriptingBackend) `
    -Development ([bool]$snapshot.intent.development) -BuildOption @($snapshot.intent.buildOptions) `
    -OutputPath ([string]$snapshot.intent.outputPath) -ScenePath $scenePaths -DefineSymbol @($snapshot.inputIdentity.defineSymbols) `
    -ManagedStrippingLevel ([string]$snapshot.inputIdentity.managedStrippingLevel) -StripEngineCode ([bool]$snapshot.inputIdentity.stripEngineCode)
$afterFingerprint = Get-ESBuildInputFingerprint -Project $state.project -Intent $state.intent -InputIdentity $state.inputIdentity
$beforeFingerprint = [string]$snapshot.buildInputFingerprint
$drifted = $beforeFingerprint -ne $afterFingerprint

$artifactEntries = New-Object Collections.Generic.List[object]
$roles = @{}
foreach ($item in @($Artifact)) {
    if (-not $item.ContainsKey('role') -or -not $item.ContainsKey('path')) { throw 'Each Artifact requires role and path keys.' }
    $role = [string]$item.role
    if ($roles.ContainsKey($role)) { throw "Artifact role must be unique: $role" }
    $roles[$role] = $true
    [void]$artifactEntries.Add((Get-ESBuildArtifactIdentity -Root $root -OutputRoot ([string]$snapshot.intent.outputPath) -Role $role -Path ([string]$item.path)))
}
$artifacts = @($artifactEntries.ToArray() | Sort-Object role, path)
if ($Status -eq 'passed' -and $artifacts.Count -eq 0) { throw 'A passed finalized receipt requires at least one hashed artifact.' }
if ($Status -eq 'passed' -and @($artifacts | Where-Object { $_.role -in @('build-log', 'build-report') -and $_.kind -eq 'file' -and [long]$_.byteLength -gt 0 }).Count -eq 0) {
    throw 'A passed finalized receipt requires a build-log or build-report artifact.'
}

$unityHash = 'not-applicable'
if (-not [string]::IsNullOrWhiteSpace($UnityExecutablePath)) {
    $unityFull = [IO.Path]::GetFullPath($UnityExecutablePath.Trim())
    if (-not (Test-Path -LiteralPath $unityFull -PathType Leaf) -or [IO.Path]::GetFileName($unityFull) -ne 'Unity.exe') {
        throw 'UnityExecutablePath must identify an existing Unity.exe file.'
    }
    $unityHash = Get-ESBuildFileHash -Path $unityFull
}
if ($Status -eq 'passed' -and $unityHash -eq 'not-applicable') { throw 'A passed finalized receipt requires UnityExecutablePath.' }
if ($Status -eq 'passed' -and [string]$snapshot.intent.scriptingBackend -eq 'IL2CPP' -and $ToolchainIdentity -in @('not-run', 'not-applicable', 'unknown')) {
    throw 'A passed IL2CPP receipt requires a concrete toolchain identity.'
}

$executionStatus = if ($drifted) { 'input-drifted' } else { $Status }
$execution = [ordered]@{
    actorId = $ActorId
    taskId = $TaskId
    planHash = $PlanHash
    commandHash = $CommandHash
    skillHashes = @($SkillHash | Sort-Object -Unique)
    startedAtUtc = $started.ToUniversalTime().ToString('o')
    finishedAtUtc = $finished.ToUniversalTime().ToString('o')
    unityExecutableHash = $unityHash
    toolchainIdentity = $ToolchainIdentity
    effectiveArguments = @($EffectiveArgument)
    status = $executionStatus
    failure = if ($drifted) { 'Build inputs changed between capture and finalize.' } else { $Failure }
    recovery = $Recovery
    inputIdentityHashBefore = $beforeFingerprint
    inputIdentityHashAfter = $afterFingerprint
}
$receipt = [ordered]@{
    schemaVersion = 1
    fingerprintSchemaVersion = 1
    contractRef = $contract.reference
    contractHash = $contract.hash
    receiptId = [string]$snapshot.receiptId
    phase = 'finalized'
    capturedAtUtc = [DateTime]::UtcNow.ToString('o')
    project = $snapshot.project
    intent = $snapshot.intent
    inputIdentity = $snapshot.inputIdentity
    buildInputFingerprint = $beforeFingerprint
    execution = $execution
    artifacts = $artifacts
    artifactManifestHash = Get-ESBuildObjectHash -Value $artifacts
    provenanceVerdict = if ($drifted) { 'input-drifted' } elseif ([string]$snapshot.provenanceVerdict -eq 'identity-incomplete') { 'identity-incomplete' } else { 'provenance-bound' }
    claimsNotProven = @(
        'Artifact provenance does not independently prove Unity compilation, Player behavior, IL2CPP native success, performance, or release acceptance.',
        'The caller-supplied execution status requires its own evidence-layer validation.'
    )
    staleWhen = [string]$snapshot.staleWhen
}
$writtenPath = Write-ESBuildIdentityJson -Root $root -ReceiptPath $ReceiptPath -Receipt $receipt
$result = [ordered]@{
    status = if ($drifted) { 'input-drifted' } else { 'finalized' }
    receiptPath = $writtenPath
    buildInputFingerprint = $beforeFingerprint
    currentInputFingerprint = $afterFingerprint
    artifactManifestHash = [string]$receipt.artifactManifestHash
    artifactCount = $artifacts.Count
    provenanceVerdict = [string]$receipt.provenanceVerdict
}
if ($Json) { $result | ConvertTo-Json -Depth 8 } else { [pscustomobject]$result }
if ($drifted) { exit 2 }
