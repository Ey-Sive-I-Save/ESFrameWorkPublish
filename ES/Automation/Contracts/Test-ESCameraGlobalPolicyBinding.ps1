[CmdletBinding()]
param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$path = Join-Path $root 'Assets\Scripts\ESLogic\Runtime\Camera\Scene\ESCameraSceneBinding.cs'
$text = Get-Content -Raw -Encoding UTF8 -LiteralPath $path
$previewPath = Join-Path $root 'Assets\Scripts\ESLogic\Runtime\Camera\Preview\ESCameraPreviewView.cs'
$previewText = Get-Content -Raw -Encoding UTF8 -LiteralPath $previewPath
$required = @(
    'globalPolicy != null && !globalPolicy.TryValidate',
    'TryValidateRigDependencies(rigCatalog, globalPolicy, out string catalogError)',
    'new ESCameraCinemachine2ViewAdapter(outputCamera, brain, definitionCatalog, rigCatalog, rigRoot, globalPolicy)'
)
$missing = @($required | Where-Object { -not $text.Contains($_) })
if ($previewText -notmatch 'Transform\s+rigRoot,\s*ESCameraGlobalPolicy\s+globalPolicy\s*=\s*null' -or $previewText -notmatch 'globalPolicy\s*!=\s*null\s*&&\s*!globalPolicy\.TryValidate' -or $previewText -notmatch 'rigRoot,\s*globalPolicy') {
    $missing += 'preview must accept and forward ESCameraGlobalPolicy'
}
if ($missing.Count -gt 0) {
    [ordered]@{ status = 'blocked'; contract = 'camera-global-policy-binding-v1'; missing = $missing } | ConvertTo-Json -Depth 4
    exit 1
}

[ordered]@{
    status = 'passed'
    contract = 'camera-global-policy-binding-v1'
    invariants = @('validate policy before catalog', 'catalog receives same policy instance', 'adapter receives same policy instance')
    nonClaims = @('Unity runtime execution', 'serialization and prefab behavior')
} | ConvertTo-Json -Depth 4
