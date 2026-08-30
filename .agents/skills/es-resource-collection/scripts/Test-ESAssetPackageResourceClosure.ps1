[CmdletBinding()]
param([string]$ProjectRoot = '.')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$source = Join-Path $root 'Assets/Plugins/ES/Editor/ESMenuTreeWindow/AssetPackageBakeWindow/Data/ESResourceCollectionBatchImporter.cs'
$hostSource = Join-Path $root 'Assets/Plugins/ES/Editor/ESMenuTreeWindow/AssetPackageBakeWindow/ESAssetPackageBakeWindow.cs'
if (-not (Test-Path -LiteralPath $source) -or -not (Test-Path -LiteralPath $hostSource)) { throw 'AssetPackage importer source not found' }
$text = Get-Content -Raw -Encoding UTF8 $source
$hostText = Get-Content -Raw -Encoding UTF8 $hostSource
$checks = [ordered]@{
    menuEntry = $hostText.Contains('asset-package.import-resource-collection-candidates') -and $hostText.Contains('ESResourceCollectionBatchImporter.ImportValidatedBatchToSelectedBake')
    selectedBake = $text.Contains('GetSelectedBakeForResourceCollection')
    batchContract = $text.Contains('es-resource-collection.batch.v1')
    hashVerification = $text.Contains('ComputeSha256(full)')
    assetsBoundary = $text.Contains('ToAssetPath(full, projectRoot)')
    guidResolution = $text.Contains('AssetDatabase.AssetPathToGUID(assetPath)')
    externalRejected = $text.Contains('return string.Empty')
    noSyntheticGuid = $text.Contains('ToAssetPath(full, projectRoot)') -and $text.Contains('AssetPathToGUID(assetPath)')
}
$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
[ordered]@{ schemaVersion=1; status=if($failed.Count -eq 0){'passed'}else{'failed'}; importerPath=$source; checks=$checks; failed=@($failed.Name); runtimeStatus='runtime-not-run'; nonClaims=@('Unity Editor execution','actual AssetDatabase import','Runtime/Player behavior') } | ConvertTo-Json -Depth 5
if ($failed.Count -gt 0) { exit 1 }
