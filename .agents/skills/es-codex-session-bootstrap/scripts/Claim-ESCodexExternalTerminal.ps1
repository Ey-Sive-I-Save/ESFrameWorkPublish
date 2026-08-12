[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ClaimId,

    [Parameter(Mandatory = $true)]
    [string]$ClaimToken,

    [string]$StateRoot = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

& (Join-Path $PSScriptRoot 'Invoke-ESCodexExternalClaim.ps1') `
    -Action Respond `
    -ClaimId $ClaimId `
    -ClaimToken $ClaimToken `
    -StateRoot $StateRoot
