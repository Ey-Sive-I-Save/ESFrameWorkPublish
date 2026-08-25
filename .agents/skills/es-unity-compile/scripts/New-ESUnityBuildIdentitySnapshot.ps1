[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-z0-9][a-z0-9._-]{2,127}$')][string]$ProjectId,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-z0-9][a-z0-9._-]{7,127}$')][string]$ReceiptId,
    [Parameter(Mandatory = $true)][string]$BuildTarget,
    [Parameter(Mandatory = $true)][string]$BuildTargetGroup,
    [Parameter(Mandatory = $true)][string]$Architecture,
    [Parameter(Mandatory = $true)][ValidateSet('Mono', 'IL2CPP')][string]$ScriptingBackend,
    [Parameter(Mandatory = $true)][bool]$Development,
    [string[]]$BuildOption = @(),
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string[]]$ScenePath = @(),
    [string[]]$DefineSymbol = @(),
    [Parameter(Mandatory = $true)][ValidatePattern('^[A-Za-z][A-Za-z0-9._-]{1,63}$')][string]$ManagedStrippingLevel,
    [Parameter(Mandatory = $true)][bool]$StripEngineCode,
    [string]$ReceiptPath,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()
if (-not [IO.Path]::IsPathRooted($ProjectRoot)) { throw 'ProjectRoot must be absolute.' }
$boundedProjectRoot = [IO.Path]::GetFullPath($ProjectRoot)
. (Join-Path $PSScriptRoot 'ESUnityBuildIdentity.Common.ps1')

$root = Resolve-ESBuildProjectRoot -ProjectRoot $boundedProjectRoot
$state = New-ESBuildInputState -Root $root -ProjectId $ProjectId -BuildTarget $BuildTarget `
    -BuildTargetGroup $BuildTargetGroup -Architecture $Architecture -ScriptingBackend $ScriptingBackend `
    -Development $Development -BuildOption $BuildOption -OutputPath $OutputPath -ScenePath $ScenePath `
    -DefineSymbol $DefineSymbol -ManagedStrippingLevel $ManagedStrippingLevel -StripEngineCode $StripEngineCode
$contract = Get-ESBuildContractIdentity -Root $root
$fingerprint = Get-ESBuildInputFingerprint -Project $state.project -Intent $state.intent -InputIdentity $state.inputIdentity
$identityIncomplete = Test-ESBuildIdentityIncomplete -InputIdentity $state.inputIdentity

$receipt = [ordered]@{
    schemaVersion = 1
    fingerprintSchemaVersion = 1
    contractRef = $contract.reference
    contractHash = $contract.hash
    receiptId = $ReceiptId
    phase = 'input-snapshot'
    capturedAtUtc = [DateTime]::UtcNow.ToString('o')
    project = $state.project
    intent = $state.intent
    inputIdentity = $state.inputIdentity
    buildInputFingerprint = $fingerprint
    execution = $null
    artifacts = @()
    artifactManifestHash = 'not-applicable'
    provenanceVerdict = if ($identityIncomplete) { 'identity-incomplete' } else { 'input-captured' }
    claimsNotProven = @(
        'Unity compilation, Domain Reload, Player, IL2CPP, HybridCLR generation, and release behavior are not proven.',
        'A captured identity does not prove that a build was executed or succeeded.'
    )
    staleWhen = 'Any recorded Git, worktree, Unity, ProjectSettings, package, scene, define, stripping, HybridCLR, contract, or fingerprint input changes.'
}

$writtenPath = $null
if (-not [string]::IsNullOrWhiteSpace($ReceiptPath)) {
    $writtenPath = Write-ESBuildIdentityJson -Root $root -ReceiptPath $ReceiptPath -Receipt $receipt
}

if ($Json) {
    [ordered]@{ status = 'captured'; receiptPath = if ($null -eq $writtenPath) { 'not-written' } else { $writtenPath }; receipt = $receipt } | ConvertTo-Json -Depth 32
}
else {
    [pscustomobject]@{
        status = 'captured'
        receiptPath = if ($null -eq $writtenPath) { 'not-written' } else { $writtenPath }
        buildInputFingerprint = $fingerprint
        worktreeState = [string]$state.inputIdentity.worktreeState
        worktreeEntryCount = @($state.inputIdentity.worktreeManifest).Count
        provenanceVerdict = [string]$receipt.provenanceVerdict
    }
}
