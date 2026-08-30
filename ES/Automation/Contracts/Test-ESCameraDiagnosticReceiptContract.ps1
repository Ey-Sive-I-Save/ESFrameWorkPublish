[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$schemaPath = Join-Path $root 'ES/Automation/Contracts/es-camera-diagnostic-receipt-v1.schema.json'
$litePath = Join-Path $root 'ES/Automation/Contracts/ESJsonSchemaLite.psm1'
Import-Module $litePath -Force

$valid = [ordered]@{
    schemaVersion = 1
    contractId = 'es://automation/contracts/camera/diagnostic-receipt/v1'
    frame = 42
    viewKey = 'MainView'
    sceneEpoch = 7
    activeRequestCount = 1
    hasWinner = $true
    winnerKind = 'Base'
    winnerDefinitionKey = 'player.third_person'
    winnerOwnerName = 'Player'
    scenePath = 'Assets/Scenes/Main.unity'
    platform = 'WindowsEditor'
    buildId = 'build-001'
}

$validErrors = @(Test-ESJsonSchemaValue -SchemaPath $schemaPath -Value ([pscustomobject]$valid))
$missing = [ordered]@{} + $valid
$missing.Remove('scenePath')
$missingErrors = @(Test-ESJsonSchemaValue -SchemaPath $schemaPath -Value ([pscustomobject]$missing))
$extra = [ordered]@{} + $valid
$extra['unexpected'] = $true
$extraErrors = @(Test-ESJsonSchemaValue -SchemaPath $schemaPath -Value ([pscustomobject]$extra))

$result = [pscustomobject]@{
    contractId = $valid.contractId
    validCase = [pscustomobject]@{ passed = ($validErrors.Count -eq 0); errors = $validErrors }
    missingRequiredCase = [pscustomobject]@{ rejected = ($missingErrors.Count -gt 0); errors = $missingErrors }
    additionalPropertyCase = [pscustomobject]@{ rejected = ($extraErrors.Count -gt 0); errors = $extraErrors }
}
$result | ConvertTo-Json -Depth 8
if (-not $result.validCase.passed -or -not $result.missingRequiredCase.rejected -or -not $result.additionalPropertyCase.rejected) {
    exit 1
}
