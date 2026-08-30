[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($ProjectRoot)
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw "project-root-not-found" }

$rendererPaths = @(
    'Assets/Settings/URP-Performant-Renderer.asset',
    'Assets/Settings/URP-Balanced-Renderer.asset',
    'Assets/Settings/URP-HighFidelity-Renderer.asset'
)
$missing = @($rendererPaths | Where-Object { -not (Test-Path -LiteralPath (Join-Path $root $_) -PathType Leaf) })
$styleFile = Join-Path $root 'Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderStylePreset.cs'
$catalogFile = Join-Path $root 'Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderStyleCatalog.cs'
$mapFile = Join-Path $root 'Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderTemplateResourceMap.cs'
$contentTypeFile = Join-Path $root 'Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderContentTypeProfile.cs'
$resolverFile = Join-Path $root 'Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderConfigurationResolver.cs'
$effectsFile = Join-Path $root 'Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderEffectsRecipe.cs'
$manifestFile = Join-Path $root 'Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderTemplateManifest.json'
$profilesFile = Join-Path $root 'Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderTemplateProfiles.json'
$contentManifestFile = Join-Path $root 'Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderContentTypeManifest.json'
$sceneManifestFile = Join-Path $root 'Assets/Plugins/ES/0_Stand/BaseDefine_RunTime/ShaderSystem/ESRenderSceneTemplateManifest.json'
$renderModuleFile = Join-Path $root 'Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESRenderModule.cs'
$gameManagerFile = Join-Path $root 'Assets/Scripts/ESLogic/Runtime/GameManager/-GameManager_Core/ESGameManager.cs'
$moduleTestFile = Join-Path $root 'Assets/Plugins/ES/Editor/ESShader/Tests/ESRenderModuleContractTests.cs'
$files = @($styleFile,$catalogFile,$mapFile,$contentTypeFile,$resolverFile,$effectsFile,$contentManifestFile,$sceneManifestFile,$manifestFile,$profilesFile,$renderModuleFile,$moduleTestFile)
$missingFiles = @($files | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
$boundaryViolations = @()
foreach($file in $files){
    if(Test-Path -LiteralPath $file -PathType Leaf){
        $text = [IO.File]::ReadAllText($file, [Text.UTF8Encoding]::new($false,$true))
        if($text -match '(?m)^\s*using\s+UnityEngine\s*;|:\s*(MonoBehaviour|ScriptableObject)\b'){$boundaryViolations += $file}
    }
}
$enumText = [IO.File]::ReadAllText($styleFile, [Text.UTF8Encoding]::new($false,$true))
$enumCount = ([regex]::Matches($enumText, '(?m)^\s*[A-Za-z][A-Za-z0-9]*\s*=\s*\d+\s*,?$')).Count
$enumNames = @([regex]::Matches($enumText, '(?m)^\s*(?<name>[A-Za-z][A-Za-z0-9]*)\s*=\s*\d+\s*,?$') | ForEach-Object { $_.Groups['name'].Value })
$contentEnumText = [IO.File]::ReadAllText($contentTypeFile, [Text.UTF8Encoding]::new($false,$true))
$contentEnumBlock = [regex]::Match($contentEnumText, '(?ms)enum\s+ESRenderContentTypeId(?:\s*:\s*[^\{]+)?\s*\{(?<body>.*?)\}')
$contentEnumNames = if($contentEnumBlock.Success){ @([regex]::Matches($contentEnumBlock.Groups['body'].Value, '(?m)^\s*(?<name>[A-Za-z][A-Za-z0-9]*)\s*=\s*\d+\s*,?$') | ForEach-Object { $_.Groups['name'].Value }) } else { @() }
$manifestError = $null
$manifestCount = 0
$profileCount = 0
if($missingFiles.Count -eq 0){
    try {
        $manifest = Get-Content -LiteralPath $manifestFile -Raw -Encoding UTF8 | ConvertFrom-Json
        $manifestCount = @($manifest.templates).Count
        $styles = @($manifest.templates | ForEach-Object style)
        $unknownStyles = @($styles | Where-Object { $_ -notin $enumNames })
        if($manifest.schemaVersion -ne 1 -or $manifest.pipeline -ne 'URP' -or $manifestCount -ne $enumCount -or @($styles | Sort-Object -Unique).Count -ne $manifestCount -or $unknownStyles.Count -gt 0){$manifestError='manifest-identity-or-style-count-invalid'}
        foreach($entry in @($manifest.templates)){
            if($rendererPaths -notcontains [string]$entry.renderer -or [string]::IsNullOrWhiteSpace($entry.volume) -or [string]::IsNullOrWhiteSpace($entry.material) -or [string]::IsNullOrWhiteSpace($entry.shader)){$manifestError='manifest-resource-binding-incomplete';break}
            foreach($field in @('volume','material','shader')){
                if(([string]$entry.$field).StartsWith('Assets/',[StringComparison]::OrdinalIgnoreCase)){
                    $resourcePath = Join-Path $root ([string]$entry.$field)
                    if(-not (Test-Path -LiteralPath $resourcePath -PathType Leaf)){$manifestError='manifest-physical-resource-missing';break}
                    if(-not (Test-Path -LiteralPath ($resourcePath + '.meta') -PathType Leaf)){$manifestError='manifest-resource-meta-missing';break}
                }
            }
            if($manifestError){break}
        }
    } catch {$manifestError='manifest-json-invalid'}
}
$profileError = $null
if(Test-Path -LiteralPath $profilesFile -PathType Leaf){
    try {
        $profiles = Get-Content -LiteralPath $profilesFile -Raw -Encoding UTF8 | ConvertFrom-Json
        $profileCount = @($profiles.profiles).Count
        $profileStyles = @($profiles.profiles | ForEach-Object style)
        $unknownProfileStyles = @($profileStyles | Where-Object { $_ -notin $enumNames })
        if($profiles.schemaVersion -ne 1 -or $profileCount -ne $enumCount -or @($profileStyles | Sort-Object -Unique).Count -ne $profileCount -or $unknownProfileStyles.Count -gt 0){$profileError='profile-identity-or-style-count-invalid'}
        foreach($entry in @($profiles.profiles)){if($null -eq $entry.saturation -or $null -eq $entry.contrast -or $null -eq $entry.exposure -or $null -eq $entry.bloom -or $null -eq $entry.shadowSoftness){$profileError='profile-numeric-fields-incomplete';break}}
    } catch {$profileError='profile-json-invalid'}
}
$contentManifestError = $null
if(Test-Path -LiteralPath $contentManifestFile -PathType Leaf){
    try {
        $contentManifest = Get-Content -LiteralPath $contentManifestFile -Raw -Encoding UTF8 | ConvertFrom-Json
        $contentProfileCount = @($contentManifest.profiles).Count
        $contentTypeNames = @($contentManifest.profiles | ForEach-Object contentType)
        $unknownContentTypes = @($contentTypeNames | Where-Object { $_ -notin $contentEnumNames })
        if($contentManifest.schemaVersion -ne 1 -or $contentManifest.pipeline -ne 'URP' -or $contentProfileCount -ne 8 -or @($contentTypeNames | Sort-Object -Unique).Count -ne 8 -or $unknownContentTypes.Count -gt 0){$contentManifestError='content-manifest-identity-or-count-invalid'}
        foreach($entry in @($contentManifest.profiles)){if($null -eq $entry.style -or $null -eq $entry.intent -or $null -eq $entry.transparencyBudgetScale -or $null -eq $entry.particleBudgetScale){$contentManifestError='content-manifest-profile-incomplete';break}}
    } catch {$contentManifestError='content-manifest-json-invalid'}
}
else {$contentManifestError='content-manifest-missing'}
$contentTypeProfileCount = if($contentManifest){ @($contentManifest.profiles).Count } else { 0 }
$sceneManifestError = $null
if(Test-Path -LiteralPath $sceneManifestFile -PathType Leaf){
    if(-not (Test-Path -LiteralPath ($sceneManifestFile + '.meta') -PathType Leaf)){$sceneManifestError='scene-manifest-meta-missing'}
    try {
        if($sceneManifestError){ throw $sceneManifestError }
        $sceneManifest = Get-Content -LiteralPath $sceneManifestFile -Raw -Encoding UTF8 | ConvertFrom-Json
        $sceneCount = @($sceneManifest.templates).Count
        $sceneContentTypes = @($sceneManifest.templates | ForEach-Object contentType)
        $unknownSceneContentTypes = @($sceneContentTypes | Where-Object { $_ -notin $contentEnumNames })
        if($sceneManifest.schemaVersion -ne 1 -or $sceneManifest.pipeline -ne 'URP' -or $sceneCount -ne 8 -or @($sceneContentTypes | Sort-Object -Unique).Count -ne $sceneCount -or $unknownSceneContentTypes.Count -gt 0){$sceneManifestError='scene-manifest-identity-or-count-invalid'}
        foreach($entry in @($sceneManifest.templates)){
            foreach($field in @('renderer','material','volume','shader')){ $path = [string]$entry.$field; if(-not $path.StartsWith('Assets/',[StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath (Join-Path $root $path) -PathType Leaf)){$sceneManifestError='scene-manifest-resource-missing';break} }
            if($sceneManifestError){break}
        }
        if($sceneManifestError -eq $null -and $contentManifest){
            foreach($contentProfile in @($contentManifest.profiles)){
                $sceneEntry = @($sceneManifest.templates | Where-Object { $_.contentType -eq $contentProfile.contentType }) | Select-Object -First 1
                if($null -eq $sceneEntry -or $sceneEntry.style -ne $contentProfile.style -or $sceneEntry.intent -ne $contentProfile.intent){$sceneManifestError='scene-content-manifest-mismatch';break}
            }
        }
    } catch {$sceneManifestError='scene-manifest-json-invalid'}
}
else {$sceneManifestError='scene-manifest-missing'}
$sceneTemplateCount = if($sceneManifest){ @($sceneManifest.templates).Count } else { 0 }
$bindingError = $null
$shaderMeta = Join-Path $root 'Assets/Plugins/ES/0_Stand/Rendering/ESStyleLit.shader.meta'
if(Test-Path -LiteralPath $shaderMeta -PathType Leaf){
    $shaderMetaText = [IO.File]::ReadAllText($shaderMeta, [Text.UTF8Encoding]::new($false,$true))
    $shaderGuid = ([regex]::Match($shaderMetaText, '(?m)^guid:\s*([0-9a-f]{32})$')).Groups[1].Value
    if([string]::IsNullOrWhiteSpace($shaderGuid)){$bindingError='shader-guid-missing'}
    if($bindingError -eq $null -and $manifest -ne $null){
        foreach($entry in @($manifest.templates)){
            $matPath = [string]$entry.material
            if($matPath.StartsWith('Assets/',[StringComparison]::OrdinalIgnoreCase)){
                $matFile = Join-Path $root $matPath
                $matText = [IO.File]::ReadAllText($matFile, [Text.UTF8Encoding]::new($false,$true))
                $materialGuid = ([regex]::Match($matText, 'm_Shader:\s*\{fileID:\s*4800000,\s*guid:\s*([0-9a-f]{32})')).Groups[1].Value
                if($materialGuid -ne $shaderGuid -or $matText -notmatch '_BaseColor' -or $matText -notmatch '_StyleContrast'){$bindingError='material-shader-or-property-mismatch';break}
            }
        }
    }
}
else {$bindingError='shader-meta-missing'}
if($bindingError -eq $null -and (Test-Path -LiteralPath $renderModuleFile -PathType Leaf) -and (Test-Path -LiteralPath $gameManagerFile -PathType Leaf)){
    $moduleText = [IO.File]::ReadAllText($renderModuleFile, [Text.UTF8Encoding]::new($false,$true))
    $managerText = [IO.File]::ReadAllText($gameManagerFile, [Text.UTF8Encoding]::new($false,$true))
    $contentText = [IO.File]::ReadAllText($contentTypeFile, [Text.UTF8Encoding]::new($false,$true))
    if(($moduleText -match ':\s*(MonoBehaviour|ScriptableObject)\b') -or ($moduleText -notmatch 'class\s+ESRenderModule\s*:\s*ESSystemModule') -or ($moduleText -notmatch 'ESRenderTemplatePlan\.TryCreate') -or ($moduleText -notmatch 'RequestApply\(') -or ($moduleText -notmatch 'RequestRollback\(') -or ($moduleText -notmatch 'RequestContentType\(') -or ($moduleText -notmatch 'RequestSceneTemplate\(') -or ($moduleText -notmatch 'RecordApplyReceipt\(') -or ($moduleText -notmatch 'RecordRollbackReceipt\(') -or ($moduleText -notmatch 'ESRenderModuleEvidenceState') -or ($moduleText -notmatch 'class\s+ESRenderModuleBackendAdapter') -or ($moduleText -notmatch 'TryBuildGateRequest\(')){$bindingError='render-module-contract-invalid'}
    if($bindingError -eq $null -and (($contentText -notmatch 'enum\s+ESRenderContentTypeId') -or ($contentText -notmatch 'Count\s*=\s*8') -or ($contentText -notmatch 'ValidateBuiltIn') -or ($contentText -notmatch 'class\s+ESRenderSceneTemplateCatalog') -or ($contentText -notmatch 'ESRenderTemplateResourceMap\.TryGet'))){$bindingError='content-type-matrix-contract-invalid'}
    $resolverText = [IO.File]::ReadAllText($resolverFile, [Text.UTF8Encoding]::new($false,$true))
    $effectsText = [IO.File]::ReadAllText($effectsFile, [Text.UTF8Encoding]::new($false,$true))
    if(($bindingError -eq $null) -and (($resolverText -notmatch 'ESRenderContentTypeId contentType') -or ($resolverText -notmatch 'WithBudgetScale') -or ($effectsText -notmatch 'WithBudgetScale\('))){$bindingError='content-type-budget-projection-missing'}
    if(($bindingError -eq $null) -and (($managerText -notmatch 'autoCreateRenderModule') -or ($managerText -notmatch 'GetMoudle<ESRenderModule>'))){$bindingError='render-module-not-registered'}
    $testText = [IO.File]::ReadAllText($moduleTestFile, [Text.UTF8Encoding]::new($false,$true))
    if(($bindingError -eq $null) -and (($testText -notmatch 'class\s+ESRenderModuleContractTests') -or ($testText -notmatch 'ContentTypeRequest') -or ($testText -notmatch 'GateRequest') -or ($testText -notmatch 'ApplyAndRollbackReceipts'))){$bindingError='render-module-contract-tests-missing'}
}
$status = if($missing.Count -eq 0 -and $missingFiles.Count -eq 0 -and $boundaryViolations.Count -eq 0 -and $enumCount -ge 10 -and $manifestError -eq $null -and $profileError -eq $null -and $contentManifestError -eq $null -and $sceneManifestError -eq $null -and $bindingError -eq $null){'passed'}else{'blocked'}
[pscustomobject]@{
    validator='es-render-template-resource-closure'; status=$status; rendererAssetCount=($rendererPaths.Count-$missing.Count)
    requiredRendererAssetCount=$rendererPaths.Count; styleCount=$enumCount; manifestTemplateCount=$manifestCount; profileTemplateCount=$profileCount; contentTypeProfileCount=$contentTypeProfileCount; sceneTemplateCount=$sceneTemplateCount; minimumStyleCount=10; manifestError=$manifestError; profileError=$profileError; contentManifestError=$contentManifestError; sceneManifestError=$sceneManifestError; bindingError=$bindingError
    missingRendererAssets=$missing; missingContractFiles=$missingFiles; boundaryViolations=$boundaryViolations
    runtimeStatus='runtime-not-run'; claimsNotProven=@('Unity asset import','Volume/Material/Shader runtime binding','visual and performance behavior')
} | ConvertTo-Json -Depth 5
if($status -ne 'passed'){ exit 1 }
