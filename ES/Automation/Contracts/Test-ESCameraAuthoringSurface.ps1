[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$editorPath = Join-Path $root 'Assets\Plugins\ES\Editor\Camera\ESCameraDefinitionEditors.cs'
if (-not (Test-Path -LiteralPath $editorPath -PathType Leaf)) { throw "Missing camera authoring editor: $editorPath" }
$text = Get-Content -Raw -Encoding UTF8 -LiteralPath $editorPath
$viewEditor = [regex]::Match($text, '(?s)internal sealed class ESCameraViewDefinitionEditor.*?(?=\s*\[CustomEditor\(typeof\(ESCameraGlobalPolicy\)\)\])').Value
if ([string]::IsNullOrWhiteSpace($viewEditor)) { throw 'Unable to isolate ESCameraViewDefinitionEditor.' }

$forbidden = @(
    'Draw("povLookSensitivity"', 'Draw("freeLookSensitivity"', 'Draw("pointerLookScale"',
    'Draw("maxPovLookRate"', 'Draw("maxFreeLookRate"', 'Draw("invertVerticalLook"',
    'Draw("enableObstruction"', 'Draw("obstructionMask"', 'Draw("obstructionCameraRadius"',
    'Draw("obstructionMinimumDistance"', 'Draw("obstructionMaximumEffort"',
    'Draw("obstructionDamping"', 'Draw("obstructionDampingWhenOccluded"'
)
$violations = @($forbidden | Where-Object { $viewEditor.Contains($_) })
if ($violations.Count -gt 0) {
    [ordered]@{ status = 'blocked'; contract = 'camera-authoring-surface-v1'; violations = $violations } | ConvertTo-Json -Depth 4
    exit 1
}

$required = @('Draw("definition"', 'Draw("rigKey"', 'Draw("baseFieldOfView"', 'Draw("baseDistanceScale"', 'Draw("baseShoulderOffset"', 'Draw("baseShakeAmplitude"')
$missing = @($required | Where-Object { -not $viewEditor.Contains($_) })
if ($missing.Count -gt 0) {
    [ordered]@{ status = 'blocked'; contract = 'camera-authoring-surface-v1'; missing = $missing } | ConvertTo-Json -Depth 4
    exit 1
}

[ordered]@{
    status = 'passed'
    contract = 'camera-authoring-surface-v1'
    definitionSurface = @('definition', 'rigKey', 'baseFieldOfView', 'baseDistanceScale', 'baseShoulderOffset', 'baseShakeAmplitude')
    globalPolicySurface = 'separate ESCameraGlobalPolicy inspector'
    nonClaims = @('Unity serialization', 'visual layout', 'multi-selection runtime behavior')
} | ConvertTo-Json -Depth 4
