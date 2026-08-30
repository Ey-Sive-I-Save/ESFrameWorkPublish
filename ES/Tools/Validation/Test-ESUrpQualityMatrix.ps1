[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$manifestPath = Join-Path $ProjectRoot 'Packages/manifest.json'
$policyPath = Join-Path $ProjectRoot 'Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderQualityPolicy.cs'
if (-not (Test-Path -LiteralPath $manifestPath) -or -not (Test-Path -LiteralPath $policyPath)) { throw 'URP matrix inputs are missing' }
$controlWindowPath = Join-Path $ProjectRoot 'Assets/Plugins/ES/Editor/ESShader/ESUrpRenderControlWindow.cs'
$qualityWriterPath = Join-Path $ProjectRoot 'Assets/Plugins/ES/Editor/ESShader/ESRenderBackendUnityWriter.cs'
$launcherPath = Join-Path $ProjectRoot 'Assets/Plugins/ES/Editor/EditorTools/ESWindowLauncher.cs'

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$urp = [string]$manifest.dependencies.'com.unity.render-pipelines.universal'
$policy = Get-Content -LiteralPath $policyPath -Raw -Encoding UTF8
$profiles = @('Performant','Balanced','HighFidelity','CombatReadability','CinematicShowcase','MobileStable')
$missing = @($profiles | Where-Object { $policy -notmatch ("case ESRenderQualityProfileId\." + [regex]::Escape($_) + ':') })
$rendererAssets = @(
    'Assets/Settings/URP-Performant-Renderer.asset',
    'Assets/Settings/URP-Balanced-Renderer.asset',
    'Assets/Settings/URP-HighFidelity-Renderer.asset'
)
$missingRendererAssets = @($rendererAssets | Where-Object { -not (Test-Path -LiteralPath (Join-Path $ProjectRoot $_)) })
$rendererFeatureCounts = [ordered]@{}
$rendererStructureFailures = @()
foreach ($asset in $rendererAssets) {
    $assetPath = Join-Path $ProjectRoot $asset
    if (-not (Test-Path -LiteralPath $assetPath)) { continue }
    $assetText = Get-Content -LiteralPath $assetPath -Raw -Encoding UTF8
    if ($assetText -notmatch '(?m)^\s*m_RendererFeatures:') { $rendererStructureFailures += $asset; continue }
    $featureSection = [regex]::Match($assetText, '(?ms)^\s*m_RendererFeatures:\s*(?<features>.*?)(?=^\s*m_RendererFeatureMap:)')
    $rendererFeatureCounts[$asset] = if ($featureSection.Success) {
        ([regex]::Matches($featureSection.Groups['features'].Value, '(?m)^\s*-\s*\{fileID:')).Count
    } else { 0 }
}
$dependencyNames = @($manifest.dependencies.PSObject.Properties.Name)
$unsupportedDependencies = @($dependencyNames | Where-Object { $_ -match 'render-pipelines\.high-definition|builtin' })
$qualityBypassPaths = @()
foreach ($sourceFile in Get-ChildItem -Path (Join-Path $ProjectRoot 'Assets') -Recurse -Filter '*.cs' -File) {
    if ($sourceFile.FullName -like '*\Assets\Plugins\ES\Obsolete\*') { continue }
    $sourceText = Get-Content -LiteralPath $sourceFile.FullName -Raw -Encoding UTF8
    if ($sourceText -match 'QualitySettings\.SetQualityLevel' -and $sourceFile.FullName -ne $qualityWriterPath) {
        $qualityBypassPaths += $sourceFile.FullName.Substring($ProjectRoot.Length + 1)
    }
}
$esControlEntryPresent = $false
if (Test-Path -LiteralPath $controlWindowPath) {
    $controlText = Get-Content -LiteralPath $controlWindowPath -Raw -Encoding UTF8
    $esControlEntryPresent = $controlText.Contains('ES URP') -and $controlText.Contains('RenderControlWindow')
}
$esAutomationEntryPresent = $false
if (Test-Path -LiteralPath $launcherPath) {
    $launcherText = Get-Content -LiteralPath $launcherPath -Raw -Encoding UTF8
    $esAutomationEntryPresent = $launcherText.Contains('urp_render_control')
}
$graphicsText = Get-Content -LiteralPath (Join-Path $ProjectRoot 'ProjectSettings/GraphicsSettings.asset') -Raw -Encoding UTF8
$qualityText = Get-Content -LiteralPath (Join-Path $ProjectRoot 'ProjectSettings/QualitySettings.asset') -Raw -Encoding UTF8
$pipelineGuids = @([regex]::Matches(($graphicsText + "`n" + $qualityText), 'customRenderPipeline: \{fileID: [^,]+, guid: ([0-9a-f]+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
$configurationFailures = @()
foreach ($guid in $pipelineGuids) {
    $meta = Get-ChildItem -Path (Join-Path $ProjectRoot 'Assets') -Recurse -Filter '*.meta' -File | Where-Object {
        Select-String -LiteralPath $_.FullName -Pattern ("^guid: " + [regex]::Escape($guid) + '$') -Quiet
    } | Select-Object -First 1
    if ($null -eq $meta) { $configurationFailures += "missing-pipeline-guid:$guid"; continue }
    $assetPath = $meta.FullName.Substring(0, $meta.FullName.Length - 5)
    $assetText = Get-Content -LiteralPath $assetPath -Raw -Encoding UTF8
    if ($assetPath -notmatch '\\Assets\\Settings\\URP-[^\\]+\.asset$' -or $assetText -notmatch '(?m)^\s*m_Script:') { $configurationFailures += "non-urp-pipeline:$guid" }
}
$shaderFiles = Get-ChildItem -Path (Join-Path $ProjectRoot 'Assets') -Recurse -Filter '*.shader' -File
$shaderKeywordLines = 0
foreach ($shaderFile in $shaderFiles) {
    $shaderKeywordLines += ([regex]::Matches((Get-Content -LiteralPath $shaderFile.FullName -Raw -Encoding UTF8), '(?m)^\s*#pragma\s+(multi_compile|shader_feature)')).Count
}
$result = [ordered]@{
    validator = 'es-urp-quality-matrix'
    pipelineScope = 'URP-only'
    currentUrpPackage = $urp
    expectedUrpPackage = '14.0.11'
    unity6MaximumCandidate = $true
    builtInSupported = $false
    hdrpSupported = $false
    profilesChecked = $profiles.Count
    missingProfiles = $missing
    rendererAssetsChecked = $rendererAssets.Count
    missingRendererAssets = $missingRendererAssets
    rendererFeatureCounts = $rendererFeatureCounts
    rendererStructureFailures = $rendererStructureFailures
    unsupportedDependencies = $unsupportedDependencies
    esControlEntryPresent = $esControlEntryPresent
    esAutomationEntryPresent = $esAutomationEntryPresent
    qualityBypassPaths = $qualityBypassPaths
    pipelineGuidsChecked = $pipelineGuids.Count
    configurationFailures = $configurationFailures
    shaderAssetsChecked = $shaderFiles.Count
    shaderKeywordDirectiveCount = $shaderKeywordLines
    volumeProfileStatus = 'unknown-without-editor-asset-database'
    status = if ($urp -eq '14.0.11' -and $missing.Count -eq 0 -and $missingRendererAssets.Count -eq 0 -and $unsupportedDependencies.Count -eq 0 -and $rendererStructureFailures.Count -eq 0 -and $configurationFailures.Count -eq 0 -and $qualityBypassPaths.Count -eq 0 -and $esControlEntryPresent -and $esAutomationEntryPresent) { 'passed' } else { 'failed' }
    runtimeStatus = 'runtime-not-run'
    nonClaims = @('no-unity6-runtime-claim','no-profiler-player-release-claim','no-unity-or-build-execution')
}
if ($Json) { $result | ConvertTo-Json -Depth 6 } else { $result | Format-List }
if ($result.status -ne 'passed') { exit 1 }
