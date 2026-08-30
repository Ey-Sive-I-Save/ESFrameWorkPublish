[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
$authorityGate = Join-Path $root 'ES/Automation/AI/Test-ESAuthorityGovernance.ps1'
if (-not (Test-Path -LiteralPath $authorityGate -PathType Leaf)) {
    Write-Error "Authority Governance gate is missing: $authorityGate"
    exit 1
}

# This is a discovery/CI entrypoint only. The canonical gate owns all policy
# and validation logic; this wrapper must preserve its JSON and exit code.
$raw = & powershell -NoProfile -File $authorityGate -ProjectRoot $root 2>&1 | Out-String
$exitCode = $LASTEXITCODE
Write-Output $raw.TrimEnd()
if ($exitCode -ne 0) { exit $exitCode }
