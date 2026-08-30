Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ESAuthorityDecisionPolicy {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ProjectRoot,
        [Parameter(Mandatory)]
        [ValidateSet('ai-collaboration', 'game-logic', 'editor-tooling', 'release')]
        [string]$Domain
    )
    $root = (Resolve-Path -LiteralPath $ProjectRoot).Path
    $path = Join-Path $root 'ES/Automation/Contracts/es-authority-ai-decision-policy-v1.json'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw 'AUTHORITY_POLICY_CONTRACT_MISSING' }
    $contract = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($null -eq $contract.domains -or $null -eq $contract.domains.$Domain) { throw 'AUTHORITY_POLICY_DOMAIN_MISSING' }
    $entry = $contract.domains.$Domain
    [pscustomobject][ordered]@{
        schemaVersion = [int]$contract.schemaVersion
        contractId = [string]$contract.contractId
        domain = $Domain
        safeDefaultFields = @($entry.safeDefaultFields | ForEach-Object { [string]$_ } | Select-Object -Unique)
        strictOnUnresolved = [bool]$entry.strictOnUnresolved
        description = [string]$entry.description
        contractPath = $path
        contractHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

Export-ModuleMember -Function Get-ESAuthorityDecisionPolicy
