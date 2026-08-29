[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string[]]$SemanticIds,
    [int]$Rounds = 3,
    [string]$StatePath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path '.agents\runtime\super-semantics-suppression.json')
)
$ErrorActionPreference = 'Stop'
$parent = Split-Path -Parent $StatePath
if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
[ordered]@{ schemaVersion = 1; updatedUtc = [DateTime]::UtcNow.ToString('o'); remainingRounds = [Math]::Max(0, $Rounds); semanticIds = @($SemanticIds | Sort-Object -Unique) } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $StatePath -Encoding UTF8
Get-Item -LiteralPath $StatePath | Select-Object FullName, Length
