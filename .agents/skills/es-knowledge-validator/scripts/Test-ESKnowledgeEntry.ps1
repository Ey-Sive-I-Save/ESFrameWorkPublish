[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [Parameter(Mandatory = $true)][string]$EntryPath
)

$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot 'Invoke-ESKnowledgeValidation.ps1'
& powershell -NoProfile -File $validator -ProjectRoot $ProjectRoot -Mode Entry -EntryPath $EntryPath
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
