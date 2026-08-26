[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [Parameter(Mandatory=$true)][string]$TargetPath,
    [string]$EvidencePath,
    [string]$ReportPath,
    [string]$RuntimeAuthorizationPath,
    [ValidateSet('Auto','EditorWindow','Workbench','AdvancedDropdown','TransientPopup','PreviewWindow','InspectorDrawer','ShaderGUI','MenuAction','PreviewImport','BackgroundService')][string]$TargetKind='Auto',
    [string]$TargetContractPath,
    [ValidateRange(1,8760)][int]$MaxEvidenceAgeHours=168,
    [ValidateSet('StaticReview','Development','Acceptance','Release')][string]$ValidationMode='StaticReview'
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($ProjectRoot)
if(-not (Test-Path -LiteralPath $root -PathType Container)){throw "ProjectRoot not found: $ProjectRoot"}
$rootFull=[IO.Path]::GetFullPath($root).TrimEnd('\','/')
function Rel([string]$path) {
    $full=[IO.Path]::GetFullPath($path)
    if(-not $full.StartsWith($rootFull+'\',[StringComparison]::OrdinalIgnoreCase)){ throw 'Path escapes ProjectRoot' }
    $full.Substring($rootFull.Length+1).Replace('\','/')
}
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
function ReadStrict([string]$path){[IO.File]::ReadAllText($path,(New-Object Text.UTF8Encoding($false,$true)))}
function HasAny([string]$text,[string[]]$patterns){foreach($p in $patterns){if($text -match $p){return $true}};return $false}
function Row([string]$id,[string]$status,[string]$details,[string]$evidence='', [int]$weight=1, [bool]$critical=$false){
    [pscustomobject][ordered]@{id=$id;status=$status;evidenceLevel='S1';weight=$weight;critical=$critical;details=$details;evidence=$evidence;bounds=$null;viewports=@();strategy=''}
}
if([IO.Path]::IsPathRooted($TargetPath)){throw 'TargetPath must be project-relative'}
$targetFull=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$TargetPath))
if(-not $targetFull.StartsWith($rootFull+'\',[StringComparison]::OrdinalIgnoreCase)){ throw 'TargetPath escapes ProjectRoot' }
if(-not (Test-Path -LiteralPath $targetFull)){ throw "TargetPath not found: $TargetPath" }
$files=@()
if(Test-Path -LiteralPath $targetFull -PathType Leaf){$files=@(Get-Item -LiteralPath $targetFull)}else{$files=@(Get-ChildItem -LiteralPath $targetFull -Recurse -File)}
$rows=New-Object 'System.Collections.Generic.List[object]'
$findings=New-Object 'System.Collections.Generic.List[object]'
$windowRecords=New-Object 'System.Collections.Generic.List[object]'
$advancedDropdownRecords=New-Object 'System.Collections.Generic.List[object]'
$transientPopupRecords=New-Object 'System.Collections.Generic.List[object]'
$previewWindowRecords=New-Object 'System.Collections.Generic.List[object]'
$drawerRecords=New-Object 'System.Collections.Generic.List[object]'
$shaderGuiRecords=New-Object 'System.Collections.Generic.List[object]'
$editorFiles=@($files|Where-Object {$_.Extension -in @('.cs','.uxml','.uss','.asmdef','.json')})
$relatedEditorFiles = New-Object 'System.Collections.Generic.List[object]'
foreach($file in $editorFiles){ [void]$relatedEditorFiles.Add($file) }
if($TargetKind -eq 'Workbench'){
    # Workbench contracts are intentionally split: the domain window owns identity and
    # layout, while the shared base/host owns Undo delegation and event routing. Scan
    # that bounded contract closure together instead of judging each file in isolation.
    $workbenchContractPaths = @(
        'Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchWindowBase.cs',
        'Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchUIToolkitHost.cs',
        'Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchViewportFoundation.cs',
        'Assets/Scripts/ESLogic/Editor/Workbench/ESWorkbenchAuthoringContracts.cs'
    )
    foreach($relativePath in $workbenchContractPaths){
        $contractPath = Join-Path $root $relativePath
        if(Test-Path -LiteralPath $contractPath -PathType Leaf){
            [void]$relatedEditorFiles.Add((Get-Item -LiteralPath $contractPath))
        }
    }
}
if($TargetKind -eq 'ShaderGUI'){
    $targetDirectory = Split-Path -Parent $targetFull
    $targetStem = (([IO.Path]::GetFileNameWithoutExtension($targetFull) -split '\.')[0])
    foreach($partialFile in @(Get-ChildItem -LiteralPath $targetDirectory -File -Filter ($targetStem + '*.cs'))){
        [void]$relatedEditorFiles.Add($partialFile)
    }
}
$editorFiles=@($relatedEditorFiles | Sort-Object -Property FullName -Unique)
if($editorFiles.Count -eq 0){[void]$findings.Add([pscustomobject]@{code='TargetNotEditorAsset';path=(Rel $targetFull);detail='No editor assets or configuration files found.'})}
foreach($file in $editorFiles){
    try{$contents=ReadStrict $file.FullName}catch{[void]$findings.Add([pscustomobject]@{code='InvalidUtf8';path=(Rel $file.FullName);detail=$_.Exception.Message});continue}
    if($file.Extension -ne '.cs'){continue}
    $relative=Rel $file.FullName
    $isWindowDeclaration=$contents -match '(?im)^\s*(?:(?:public|internal|private|protected|abstract|sealed|partial|static)\s+)*class\s+\w+(?:<[^>{}\r\n]+>)?\s*:\s*[^\{\r\n]*(?:\bEditorWindow\b|\bESSinglePageWindow\b|\bESSinglePageIMGUIWindow\b|\bESWorkbenchWindowBase\b)'
    $isTestFile = $relative -match '(?i)(^|/)Tests?(/|$)'
    if($isWindowDeclaration){
        $isAbstractWindow = $contents -match '(?im)\babstract\s+class\s+\w+(?:<[^>{}\r\n]+>)?\s*:'
        if(-not $isTestFile){
            [void]$windowRecords.Add([pscustomobject]@{path=$relative;contents=$contents;isAbstract=$isAbstractWindow})
        }
    }
    $isAdvancedDropdownDeclaration=$contents -match '(?im)^\s*(?:(?:public|internal|private|protected|abstract|sealed|partial|static)\s+)*class\s+\w+(?:<[^>{}\r\n]+>)?\s*:\s*[^\{\r\n]*\bAdvancedDropdown\b'
    if($isAdvancedDropdownDeclaration -and -not $isTestFile){
        [void]$advancedDropdownRecords.Add([pscustomobject]@{path=$relative;contents=$contents})
    }
    $isTransientPopupDeclaration = $contents -match '(?i)ESWindowSleepContract\s*\(\s*(?:ES\.)?ESWindowSleepMode\.Transient\s*,\s*(?:ES\.)?ESWindowSurfaceKind\.(?:Popup|Utility)'
    if($isTransientPopupDeclaration -and -not $isTestFile){
        [void]$transientPopupRecords.Add([pscustomobject]@{path=$relative;contents=$contents})
    }
    $isPreviewWindowDeclaration = $contents -match '(?i)ESWindowSleepContract\s*\(\s*(?:ES\.)?ESWindowSleepMode\.(?:Full|Transient)\s*,\s*(?:ES\.)?ESWindowSurfaceKind\.Preview'
    if($isPreviewWindowDeclaration -and -not $isTestFile){
        [void]$previewWindowRecords.Add([pscustomobject]@{path=$relative;contents=$contents})
    }
    $isDrawerDeclaration = $contents -match '(?i)(CustomPropertyDrawer\s*\(|\bclass\s+\w+\s*:\s*(?:PropertyDrawer|OdinAttributeDrawer|OdinValueDrawer|OdinGroupDrawer)|\bCustomEditor\s*\()'
    if($isDrawerDeclaration -and -not $isTestFile){
        [void]$drawerRecords.Add([pscustomobject]@{path=$relative;contents=$contents})
    }
    $isShaderGuiDeclaration = $contents -match '(?im)\bclass\s+\w+(?:<[^>{}\r\n]+>)?\s*:\s*[^\{\r\n]*\bShaderGUI\b'
    if($isShaderGuiDeclaration -and -not $isTestFile){
        [void]$shaderGuiRecords.Add([pscustomobject]@{path=$relative;contents=$contents})
    }
    $editorScopedPath = $relative -match '(?i)(^|/)Editor(/|$)'
    # ES keeps a small set of editor-only preview implementations beside their
    # runtime partials. An explicit UNITY_EDITOR compilation guard is the scope
    # boundary for PreviewImport targets; do not classify those files as runtime
    # implementations merely because their directory is named Runtime.
    $editorScopedByGuard = $TargetKind -eq 'PreviewImport' -and
        $contents -match '(?im)^\s*#if\s+UNITY_EDITOR\b'
    if(-not ($editorScopedPath -or $editorScopedByGuard)){
        [void]$findings.Add([pscustomobject]@{code='EditorScope';path=$relative;detail='Editor extension implementation is outside an Editor-scoped path.'})
    }
    # Only a real Unity initialization attribute on production code is a startup
    # scan risk. Test contracts and comments may mention InitializeOnLoad while
    # deliberately exercising bounded discovery helpers; they must not turn a
    # static detector into a false production finding.
    $hasUnityInitializationAttribute = $contents -match '(?im)^\s*\[(?:InitializeOnLoad|InitializeOnLoadMethod)\b[^\]]*\]'
    if(-not $isTestFile -and $hasUnityInitializationAttribute -and $contents -match '(?i)(FindAssets|GetFiles\(|Directory\.GetFiles|GetDirectories\(|Directory\.GetDirectories)'){
        [void]$findings.Add([pscustomobject]@{code='StartupScanRisk';path=$relative;detail='InitializeOnLoad is combined with broad asset or filesystem scanning; require explicit trigger or bounded incremental invalidation.'})
    }
    if($isWindowDeclaration -and -not $isTestFile -and -not $isAbstractWindow -and $contents -notmatch '(?i)(CreateGUI|OnGUI|ESWindow_DrawIMGUI)'){
        [void]$findings.Add([pscustomobject]@{code='WindowLifecycle';path=$relative;detail='EditorWindow implementation has no visible GUI entry (OnGUI/CreateGUI).'})
    }
    $delegatedUndoEvidence = $false
    if($contents -match '(?i)(ESEditorSerializedMutation\.TryApply|TryMutateTargets\s*\()'){
        $delegatedUndoEvidence = $true
    }
    if($TargetKind -eq 'Workbench' -and $contents -match '(?i)SerializedObject|SerializedProperty'){
        $delegatedUndoEvidence = $contents -match '(?i)ESWorkbench_Record\s*\(' -or
            (@($relatedEditorFiles | ForEach-Object { try { ReadStrict $_.FullName } catch { '' } }) -join "`n") -match '(?i)ESWorkbench_Record\s*\('
    }
    # Merely reading a SerializedObject/SerializedProperty does not create a
    # mutation boundary. Require an actual serialized write before demanding
    # an Undo operation; this avoids blocking diagnostic windows that only
    # project read-only state into their UI.
    # ApplyModifiedProperties is a commit boundary, not proof that this file
    # changed serialized state: read-only inspectors commonly call it after
    # drawing nested controls. Require an actual property/array mutation.
    $hasSerializedWrite = $contents -match '(?i)(\.\s*(?:intValue|stringValue|boolValue|longValue|floatValue|doubleValue|enumValueIndex|objectReferenceValue|managedReferenceValue|exposedReferenceValue)\s*=(?!=)|(?:InsertArrayElementAtIndex|DeleteArrayElementAtIndex|ClearArray)\s*\()'
    $intentionalNoUndo = $contents -match '(?im)^\s*//\s*ES-EDITOR-VALIDATOR:\s*intentional-no-undo\b'
    if(-not $isTestFile -and -not $intentionalNoUndo -and $contents -match '(?i)SerializedObject|SerializedProperty' -and $hasSerializedWrite -and
        $contents -notmatch '(?i)Undo\.(RecordObject|RegisterCompleteObjectUndo)' -and
        -not $delegatedUndoEvidence){
        [void]$findings.Add([pscustomobject]@{code='UndoMissing';path=$relative;detail='Serialized editing detected without an Undo operation in the same file; verify delegated Undo ownership.'})
    }
    if($contents -match '(?i)EditorApplication\.(update|delayCall)|AssemblyReloadEvents' -and $contents -notmatch '(?i)(-=|Dispose|Unsubscribe|RemoveListener)'){
        [void]$findings.Add([pscustomobject]@{code='CallbackCleanup';path=$relative;detail='Editor callback registration has no visible cleanup path; require reload/close disposal evidence.'})
    }
}
$windowContents=(@($windowRecords|ForEach-Object {[string]$_.contents}) -join "`n")
$advancedDropdownContents=(@($advancedDropdownRecords|ForEach-Object {[string]$_.contents}) -join "`n")
$transientPopupContents=(@($transientPopupRecords|ForEach-Object {[string]$_.contents}) -join "`n")
$previewWindowContents=(@($previewWindowRecords|ForEach-Object {[string]$_.contents}) -join "`n")
$drawerContents=(@($drawerRecords|ForEach-Object {[string]$_.contents}) -join "`n")
$shaderGuiSourceFiles = @($editorFiles)
if($shaderGuiRecords.Count -gt 0 -or $TargetKind -eq 'ShaderGUI'){
    $shaderGuiDirectory = Split-Path -Parent $targetFull
    $shaderGuiPrefix = (([IO.Path]::GetFileNameWithoutExtension($targetFull) -split '\.')[0])
    $shaderGuiSourceFiles = @(Get-ChildItem -LiteralPath $shaderGuiDirectory -File -Filter ($shaderGuiPrefix + '*.cs'))
}
$shaderGuiContents=(@($shaderGuiSourceFiles|ForEach-Object { try { ReadStrict $_.FullName } catch { '' } }) -join "`n")
$isEditorWindow=$windowRecords.Count -gt 0
$inferredKind=if($advancedDropdownRecords.Count -gt 0){'AdvancedDropdown'}elseif($transientPopupRecords.Count -gt 0){'TransientPopup'}elseif($previewWindowRecords.Count -gt 0){'PreviewWindow'}elseif($shaderGuiRecords.Count -gt 0){'ShaderGUI'}elseif($drawerRecords.Count -gt 0){'InspectorDrawer'}elseif($isEditorWindow){if($windowContents -match '(?i)(Workbench|ESWorkbenchWindowBase)'){'Workbench'}else{'EditorWindow'}}elseif($windowContents -match '(?i)(MenuItem|ValidateCommand|commandId)'){'MenuAction'}elseif($windowContents -match '(?i)(AssetPreview|ScriptedImporter|OnImportAsset|AssetPostprocessor)'){'PreviewImport'}else{'BackgroundService'}
$resolvedTargetKind=if($TargetKind -eq 'Auto'){$inferredKind}else{$TargetKind}
$targetKindSource=if($TargetKind -eq 'Auto'){'inferred'}else{'explicit'}
$matrix=[ordered]@{
  EditorWindow=@('compile','reloadDomain','interaction','visual','recovery','performance')
  Workbench=@('compile','reloadDomain','interaction','visual','recovery','performance')
  AdvancedDropdown=@('compile','reloadDomain','interaction','visual','recovery','performance')
  TransientPopup=@('compile','reloadDomain','interaction','visual','recovery','performance')
  PreviewWindow=@('compile','reloadDomain','interaction','visual','recovery','performance')
  InspectorDrawer=@('compile','serialization','multiSelection','undo','prefabOverride','visual')
  ShaderGUI=@('compile','serialization','multiSelection','undo','prefabOverride','visual')
  MenuAction=@('compile','menuReachability','invalidInput','boundary','repeatIdempotency')
  PreviewImport=@('compile','previewLifecycle','missingAsset','cleanup','reloadDomain','performance')
  BackgroundService=@('compile','startupScope','cancellation','cleanup','reloadDomain','performance')
}
$required=@($matrix[$resolvedTargetKind])
$criticalByKind=[ordered]@{EditorWindow=@('framework-integration','visual');Workbench=@('framework-integration','visual');AdvancedDropdown=@('visual');TransientPopup=@('visual');PreviewWindow=@('visual');InspectorDrawer=@('visual');ShaderGUI=@('visual');MenuAction=@();PreviewImport=@();BackgroundService=@()}
$criticalDimensions=@($criticalByKind[$resolvedTargetKind])
$registryPath=Join-Path $root '.agents/skills/es-editor-availability-validator/references/editor-rule-registry.json'
$ruleRegistry=$null
if(Test-Path -LiteralPath $registryPath){try{$ruleRegistry=ReadStrict $registryPath|ConvertFrom-Json}catch{[void]$findings.Add([pscustomobject]@{code='RuleRegistryInvalid';path=(Rel $registryPath);detail=$_.Exception.Message})}}
$targetContract=$null
if($TargetContractPath){
    if([IO.Path]::IsPathRooted($TargetContractPath)){throw 'TargetContractPath must be project-relative'}
    $contractFull=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$TargetContractPath))
    if(-not $contractFull.StartsWith($rootFull+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'TargetContractPath escapes ProjectRoot'}
    if(-not(Test-Path -LiteralPath $contractFull -PathType Leaf)){throw "TargetContractPath not found: $TargetContractPath"}
    try{$targetContract=ReadStrict $contractFull|ConvertFrom-Json}catch{[void]$findings.Add([pscustomobject]@{code='TargetContractInvalid';path=$TargetContractPath;detail=$_.Exception.Message})}
}
$hasFixedMaxBound=$windowContents -match '(?i)(ESWindow_MaxSize\s*=>|ESWindow_MaxSize\s*\{|(?:window\.)?maxSize\s*=|MaximumSize\s*[:=]|MaximumWindow(?:Width|Height)\s*[:=])'
$hasAdaptiveResolveMaxBound=$windowContents -match '(?i)(ResolveAdaptiveMaximum\s*\(|AdaptiveMaximumWindow)'
$hasContentAdaptiveMaxBound=$windowContents -match '(?i)(ContentAdaptiveWindow|AdjustMaximumWindow|content.?adaptive.?window)'
$hasAdaptiveMaxBound=$hasAdaptiveResolveMaxBound -or $hasContentAdaptiveMaxBound
$hasHostMaxBound=$windowContents -match '(?i)(HostWindowBounds|HostBoundedWindow|ApplyHostWindowBounds)'
$hasFlexibleMaxBound=$windowContents -match '(?i)(UnboundedFlexibleLayout|AllowUnboundedWindow|flexible.?unbounded)'
$hasMaxBound=$hasFixedMaxBound -or $hasAdaptiveMaxBound -or $hasHostMaxBound -or $hasFlexibleMaxBound
$maxBoundStrategy=if($hasFixedMaxBound){'fixed'}elseif($hasAdaptiveResolveMaxBound){'adaptive-resolve'}elseif($hasContentAdaptiveMaxBound){'content-adaptive'}elseif($hasHostMaxBound){'host-bounded'}elseif($hasFlexibleMaxBound){'unbounded-flexible'}else{''}
$frameworkFindings=New-Object 'System.Collections.Generic.List[object]'
$layoutFindings=New-Object 'System.Collections.Generic.List[object]'
$dropdownFindings=New-Object 'System.Collections.Generic.List[object]'
if($resolvedTargetKind -eq 'AdvancedDropdown'){
    if($advancedDropdownRecords.Count -eq 0){
        [void]$dropdownFindings.Add([pscustomobject]@{code='AdvancedDropdownTargetMissing';path=(Rel $targetFull);detail='TargetKind AdvancedDropdown did not find an AdvancedDropdown implementation.'})
    }else{
        if($advancedDropdownContents -notmatch '(?i)(minimumSize\s*=|AbsoluteMinimumWidth|NormalizeMinimumWindowSize)'){
            [void]$dropdownFindings.Add([pscustomobject]@{code='AdvancedDropdownSizeBoundaryMissing';path=(Rel $targetFull);detail='AdvancedDropdown has no visible minimum-size boundary.'})
        }
        if($advancedDropdownContents -notmatch '(?i)(DetachFromPanelEvent|Dispose\(\)|Close\()'){
            [void]$dropdownFindings.Add([pscustomobject]@{code='AdvancedDropdownLifecycleMissing';path=(Rel $targetFull);detail='AdvancedDropdown has no visible detach/close cleanup path.'})
        }
    }
}
$popupFindings=New-Object 'System.Collections.Generic.List[object]'
if($resolvedTargetKind -eq 'TransientPopup'){
    if($transientPopupRecords.Count -eq 0){
        [void]$popupFindings.Add([pscustomobject]@{code='TransientPopupTargetMissing';path=(Rel $targetFull);detail='TargetKind TransientPopup did not find a Transient Popup/Utility contract.'})
    }else{
        if($transientPopupContents -notmatch '(?i)(ESWindowFoundation\.BindTransient|BindTransient\s*\()'){
            [void]$popupFindings.Add([pscustomobject]@{code='TransientPopupBindMissing';path=(Rel $targetFull);detail='Transient Popup has no visible BindTransient lifecycle binding.'})
        }
        if($transientPopupContents -notmatch '(?i)(ESWindowFoundation\.Suspend|ESWindowFoundation\.Close|OnDisable|OnDestroy)'){
            [void]$popupFindings.Add([pscustomobject]@{code='TransientPopupLifecycleMissing';path=(Rel $targetFull);detail='Transient Popup has no visible suspend/close lifecycle path.'})
        }
        if($transientPopupContents -notmatch '(?i)(minSize\s*=|MinimumSize\s*[:=]|MinimumPopupWidth|NormalizePopupSize)'){
            [void]$popupFindings.Add([pscustomobject]@{code='TransientPopupSizeBoundaryMissing';path=(Rel $targetFull);detail='Transient Popup has no visible minimum-size boundary.'})
        }
    }
}
$previewFindings=New-Object 'System.Collections.Generic.List[object]'
if($resolvedTargetKind -eq 'PreviewWindow'){
    if($previewWindowRecords.Count -eq 0){
        [void]$previewFindings.Add([pscustomobject]@{code='PreviewWindowTargetMissing';path=(Rel $targetFull);detail='TargetKind PreviewWindow did not find a Preview surface contract.'})
    }else{
        if($previewWindowContents -notmatch '(?i)(RenderGUI\s*\(|PreviewScene|PreviewSession|PreviewView|AssetPreview)'){
            [void]$previewFindings.Add([pscustomobject]@{code='PreviewWindowRenderBoundaryMissing';path=(Rel $targetFull);detail='Preview window has no visible render/session boundary.'})
        }
        if($previewWindowContents -notmatch '(?i)(ESWindowFoundation\.SetSleepOwner|ESWindow_SleepOwner|ESWindow_SleepLinkMode)'){
            [void]$previewFindings.Add([pscustomobject]@{code='PreviewWindowOwnerBoundaryMissing';path=(Rel $targetFull);detail='Preview window has no visible owner/reload boundary.'})
        }
        if($previewWindowContents -notmatch '(?i)(Dispose\(\)|OnDisable|ESWindow_OnHostDisable)'){
            [void]$previewFindings.Add([pscustomobject]@{code='PreviewWindowCleanupMissing';path=(Rel $targetFull);detail='Preview window has no visible preview cleanup boundary.'})
        }
    }
}
$drawerFindings=New-Object 'System.Collections.Generic.List[object]'
if($resolvedTargetKind -eq 'InspectorDrawer'){
    if($drawerRecords.Count -eq 0){
        [void]$drawerFindings.Add([pscustomobject]@{code='InspectorDrawerTargetMissing';path=(Rel $targetFull);detail='TargetKind InspectorDrawer did not find a PropertyDrawer, CustomEditor, or Odin drawer declaration.'})
    }else{
        if($drawerContents -notmatch '(?i)(SerializedProperty|SerializedObject|PropertyTree|ValueEntry)'){
            [void]$drawerFindings.Add([pscustomobject]@{code='InspectorDrawerSerializationBoundaryMissing';path=(Rel $targetFull);detail='Inspector/Drawer has no visible serialized-property or PropertyTree boundary.'})
        }
        $drawerWrites = $drawerContents -match '(?i)(\.\s*(?:intValue|stringValue|boolValue|floatValue|doubleValue|objectReferenceValue|managedReferenceValue)\s*=|ApplyModifiedProperties|SetValue\s*\(|Write\s*\()'
        if($drawerWrites -and $drawerContents -notmatch '(?i)(Undo\.(?:RecordObject|RecordObjects|RegisterCompleteObjectUndo)|ESWorkbench_Record\s*\()'){
            [void]$drawerFindings.Add([pscustomobject]@{code='InspectorDrawerUndoBoundaryMissing';path=(Rel $targetFull);detail='Inspector/Drawer write path has no visible Undo boundary.'})
        }
        if($drawerContents -match '(?i)hasMultipleDifferentValues' -and $drawerContents -notmatch '(?i)(showMixedValue|mixed)'){
            [void]$drawerFindings.Add([pscustomobject]@{code='InspectorDrawerMixedStateMissing';path=(Rel $targetFull);detail='Multi-object state is queried without visible mixed-value presentation.'})
        }
    }
}
$shaderGuiFindings=New-Object 'System.Collections.Generic.List[object]'
if($resolvedTargetKind -eq 'ShaderGUI'){
    if($shaderGuiRecords.Count -eq 0){
        [void]$shaderGuiFindings.Add([pscustomobject]@{code='ShaderGUITargetMissing';path=(Rel $targetFull);detail='TargetKind ShaderGUI did not find a ShaderGUI implementation.'})
    }else{
        if($shaderGuiContents -notmatch '(?i)(MaterialEditor|MaterialProperty)'){
            [void]$shaderGuiFindings.Add([pscustomobject]@{code='ShaderGUIPropertyBoundaryMissing';path=(Rel $targetFull);detail='ShaderGUI has no visible MaterialEditor/MaterialProperty boundary.'})
        }
        if($shaderGuiContents -notmatch '(?i)editor\.targets|targets\.Length|targetObjects'){
            [void]$shaderGuiFindings.Add([pscustomobject]@{code='ShaderGUIMultiSelectionMissing';path=(Rel $targetFull);detail='ShaderGUI has no visible multi-material target handling.'})
        }
        $shaderGuiWrites = $shaderGuiContents -match '(?i)(\.\s*(?:SetFloat|SetColor|SetVector|SetTexture|EnableKeyword|DisableKeyword|SetOverrideTag|SetShaderPassEnabled|renderQueue\s*=)|ApplyModifiedProperties|PropertiesChanged\s*\()'
        if($shaderGuiWrites -and $shaderGuiContents -notmatch '(?i)Undo\.(?:RecordObject|RecordObjects|RegisterCompleteObjectUndo)'){
            [void]$shaderGuiFindings.Add([pscustomobject]@{code='ShaderGUIUndoBoundaryMissing';path=(Rel $targetFull);detail='ShaderGUI mutation path has no visible Undo boundary.'})
        }
    }
}
if($isEditorWindow){
    foreach($window in $windowRecords){
        if($window.isAbstract){ continue }
        $contents=[string]$window.contents
        $hasWindowBase=$contents -match '(?i)(?:ESSinglePageWindow|ESSinglePageIMGUIWindow|ESWorkbenchWindowBase)'
        $hasWindowBind=$contents -match '(?i)ES(?:\.)?WindowFoundation\s*\.\s*Bind(?:WithStandardSystemHost|FullSleep|Transient)?\s*\('
        $hasWindowSuspend=$contents -match '(?i)ES(?:\.)?WindowFoundation\s*\.\s*Suspend\s*\('
        $hasWindowClose=$contents -match '(?i)ES(?:\.)?WindowFoundation\s*\.\s*Close\s*\('
        $hasLegacyBooleanUnbind=$contents -match '(?i)ES(?:\.)?WindowFoundation\s*\.\s*Unbind\s*\([^\)]*,\s*(?:true|false)\s*\)'
        $hasWindowMin=$contents -match '(?i)(ESWindow_MinSize\s*=>|ESWindow_MinSize\s*\{|(?:window\.)?minSize\s*=|MinimumSize\s*[:=]|ResolveAdaptiveMinimum\s*\()'
        $hasWindowMax=$contents -match '(?i)(ESWindow_MaxSize\s*=>|ESWindow_MaxSize\s*\{|(?:window\.)?maxSize\s*=|MaximumSize\s*[:=]|MaximumWindow(?:Width|Height)\s*[:=]|ResolveAdaptiveMaximum\s*\(|AdaptiveMaximumWindow|ContentAdaptiveWindow|AdjustMaximumWindow|content.?adaptive.?window|HostWindowBounds|HostBoundedWindow|ApplyHostWindowBounds|UnboundedFlexibleLayout|AllowUnboundedWindow|flexible.?unbounded|ESWorkbench_LayoutMaxStrategy)' -or
            ($resolvedTargetKind -eq 'Workbench' -and $windowContents -match '(?i)ESWorkbench_LayoutMaxStrategy\s*=>')
        if(-not ($hasWindowBase -or $hasWindowBind)){
            [void]$frameworkFindings.Add([pscustomobject]@{code='ESFrameworkIntegrationMissing';path=$window.path;detail='EditorWindow is not connected to an approved ES window base or ESWindowFoundation.Bind.'})
        }
        if($hasWindowBind -and -not $hasWindowBase -and -not $hasWindowSuspend){
            [void]$frameworkFindings.Add([pscustomobject]@{code='ESFrameworkSuspendMissing';path=$window.path;detail='Direct ESWindowFoundation binding has no visible Suspend closure for OnDisable.'})
        }
        if($hasWindowBind -and -not $hasWindowBase -and -not $hasWindowClose){
            [void]$frameworkFindings.Add([pscustomobject]@{code='ESFrameworkCloseMissing';path=$window.path;detail='Direct ESWindowFoundation binding has no visible Close closure for OnDestroy.'})
        }
        if($hasLegacyBooleanUnbind){
            [void]$frameworkFindings.Add([pscustomobject]@{code='ESFrameworkLegacyUnbind';path=$window.path;detail='Boolean Unbind lifecycle routing is obsolete; use Unbind, Suspend, or Close by semantic phase.'})
        }
        if(-not $hasWindowMin){
            [void]$layoutFindings.Add([pscustomobject]@{code='LayoutMinSizeMissing';path=$window.path;detail='No minimum or adaptive minimum window bound was found.'})
        }
        if(-not $hasWindowMax){
            [void]$layoutFindings.Add([pscustomobject]@{code='LayoutMaxStrategyMissing';path=$window.path;detail='No fixed maximum or approved adaptive maximum strategy was found.'})
        }
    }
}
$structStatus=if(@($findings|Where-Object {$_.code -in @('TargetNotEditorAsset','InvalidUtf8','EditorScope')}).Count -gt 0){'failed'}else{'passed'}
[void]$rows.Add((Row 'structural' $structStatus 'Target, editor scope, and UTF-8 checks completed.'))
[void]$rows.Add((Row 'framework-integration' $(if(-not $isEditorWindow){'not-applicable'}elseif($frameworkFindings.Count -gt 0){'blocked'}else{'passed'}) 'ES framework foundation, sleep lifecycle, and unbind integration check.' '' 3 ($criticalDimensions -contains 'framework-integration')))
$dropdownStatus=if($resolvedTargetKind -ne 'AdvancedDropdown'){'not-applicable'}elseif($dropdownFindings.Count -gt 0){'blocked'}else{'passed'}
[void]$rows.Add((Row 'advanced-dropdown-contract' $dropdownStatus 'AdvancedDropdown size, provider-build and detach cleanup boundary.' '' 3 ($resolvedTargetKind -eq 'AdvancedDropdown')))
$popupStatus=if($resolvedTargetKind -ne 'TransientPopup'){'not-applicable'}elseif($popupFindings.Count -gt 0){'blocked'}else{'passed'}
[void]$rows.Add((Row 'transient-popup-contract' $popupStatus 'Transient Popup/Utility binding, lifecycle, and size boundary.' '' 3 ($resolvedTargetKind -eq 'TransientPopup')))
$previewStatus=if($resolvedTargetKind -ne 'PreviewWindow'){'not-applicable'}elseif($previewFindings.Count -gt 0){'blocked'}else{'passed'}
[void]$rows.Add((Row 'preview-window-contract' $previewStatus 'Preview render/session, owner, and cleanup boundary.' '' 3 ($resolvedTargetKind -eq 'PreviewWindow')))
$drawerStatus=if($resolvedTargetKind -ne 'InspectorDrawer'){'not-applicable'}elseif($drawerFindings.Count -gt 0){'blocked'}else{'passed'}
[void]$rows.Add((Row 'inspector-drawer-contract' $drawerStatus 'Inspector/Drawer serialization, Undo, and mixed-value boundary.' '' 3 ($resolvedTargetKind -eq 'InspectorDrawer')))
$shaderGuiStatus=if($resolvedTargetKind -ne 'ShaderGUI'){'not-applicable'}elseif($shaderGuiFindings.Count -gt 0){'blocked'}else{'passed'}
[void]$rows.Add((Row 'shader-gui-contract' $shaderGuiStatus 'ShaderGUI MaterialProperty, multi-selection, and Undo boundary.' '' 3 ($resolvedTargetKind -eq 'ShaderGUI')))
$boundaryStatus=if(@($findings|Where-Object {$_.code -in @('StartupScanRisk','UndoMissing','CallbackCleanup','WindowLifecycle')}).Count -gt 0){'blocked'}else{'passed'}
[void]$rows.Add((Row 'static-boundary' $boundaryStatus 'Static lifecycle, scan, Undo, and callback checks completed.' '' 2 $false))
[void]$findings.AddRange($frameworkFindings); [void]$findings.AddRange($layoutFindings); [void]$findings.AddRange($dropdownFindings); [void]$findings.AddRange($popupFindings); [void]$findings.AddRange($previewFindings); [void]$findings.AddRange($drawerFindings); [void]$findings.AddRange($shaderGuiFindings)
if($EvidencePath){
    if([IO.Path]::IsPathRooted($EvidencePath)){throw 'EvidencePath must be project-relative'}
    $evidenceFull=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$EvidencePath))
    if(-not $evidenceFull.StartsWith($rootFull+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'EvidencePath escapes ProjectRoot'}
    if(-not (Test-Path -LiteralPath $evidenceFull -PathType Leaf)){throw "EvidencePath not found: $EvidencePath" }
    try{$manifest=ReadStrict $evidenceFull|ConvertFrom-Json}catch{throw 'EvidencePath is not strict UTF-8 JSON'}
    if([string]$manifest.targetPath -ne (Rel $targetFull)){[void]$findings.Add([pscustomobject]@{code='EvidenceTargetMismatch';path=(Rel $evidenceFull);detail='Manifest targetPath does not match TargetPath.'})}
    $receiptFields=@('toolId','unityVersion','capturedUtc','planHash','sourceRefs','sourceRefHashes','receiptPath')
    foreach($field in $receiptFields){if(-not ($manifest.PSObject.Properties.Name -contains $field) -or [string]::IsNullOrWhiteSpace([string]$manifest.$field)){[void]$findings.Add([pscustomobject]@{code='EvidenceReceiptFieldMissing';path=(Rel $evidenceFull);detail="Evidence receipt field '$field' is required for hash-bound claims."})}}
    if($manifest.PSObject.Properties.Name -contains 'capturedUtc'){
        try{$captured=[DateTime]::Parse([string]$manifest.capturedUtc).ToUniversalTime();if(([DateTime]::UtcNow-$captured).TotalHours -gt $MaxEvidenceAgeHours){[void]$findings.Add([pscustomobject]@{code='EvidenceStale';path=(Rel $evidenceFull);detail="Evidence is older than $MaxEvidenceAgeHours hours."})}}catch{[void]$findings.Add([pscustomobject]@{code='EvidenceTimestampInvalid';path=(Rel $evidenceFull);detail='capturedUtc is not an ISO timestamp.'})}
    }
    if($manifest.PSObject.Properties.Name -contains 'receiptPath' -and -not [string]::IsNullOrWhiteSpace([string]$manifest.receiptPath)){
        $receiptRel=[string]$manifest.receiptPath;if([IO.Path]::IsPathRooted($receiptRel)){[void]$findings.Add([pscustomobject]@{code='ReceiptPathAbsolute';path=(Rel $evidenceFull);detail='receiptPath must be project-relative.'})}else{$receiptFull=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$receiptRel));if(-not $receiptFull.StartsWith($rootFull+'\',[StringComparison]::OrdinalIgnoreCase) -or -not(Test-Path -LiteralPath $receiptFull -PathType Leaf)){[void]$findings.Add([pscustomobject]@{code='ReceiptPathMissing';path=(Rel $evidenceFull);detail='receiptPath does not resolve to an existing project file.'})}}
    }
    foreach($id in $required){
        $check=@($manifest.checks|Where-Object {[string]$_.id -eq $id}|Select-Object -First 1)
        $weight=if($id -eq 'visual'){3}elseif($id -eq 'interaction'){2}else{1}
        $critical=($id -in $criticalDimensions)
        if($check.Count -eq 0){
            [void]$rows.Add((Row $id 'blocked' 'Required evidence row is missing.' '' $weight $critical))
            continue
        }
        $state=[string]$check.status
        if($state -notin @('passed','failed','blocked','not-run')){$state='blocked'}
        $details=[string]$check.details
        $row=Row $id $state $details ([string]$check.receiptPath) $weight $critical
        if($id -eq 'visual' -and $state -eq 'passed'){
            $visualProblems=New-Object 'System.Collections.Generic.List[string]'
            $bounds=$check.bounds
            $viewports=@($check.viewports|ForEach-Object {[string]$_}|Where-Object {$_})
            $strategy=[string]$bounds.strategy
            $adaptive=$false
            if($null -ne $bounds -and $bounds.PSObject.Properties.Name -contains 'adaptive'){$adaptive=[bool]$bounds.adaptive}
            $minimum=if($null -ne $bounds -and $bounds.PSObject.Properties.Name -contains 'minimum'){[string]$bounds.minimum}else{''}
            $maximum=if($null -ne $bounds -and $bounds.PSObject.Properties.Name -contains 'maximum'){[string]$bounds.maximum}else{''}
            $approvedStrategies=@('fixed','adaptive-resolve','content-adaptive','host-bounded','unbounded-flexible')
            if($null -eq $bounds){[void]$visualProblems.Add('bounds object is missing')}
            if([string]::IsNullOrWhiteSpace($minimum)){[void]$visualProblems.Add('bounds.minimum is missing')}
            if([string]::IsNullOrWhiteSpace($strategy) -or $approvedStrategies -notcontains $strategy){[void]$visualProblems.Add('bounds.strategy is not approved')}
            if($strategy -eq 'fixed' -and [string]::IsNullOrWhiteSpace($maximum)){[void]$visualProblems.Add('fixed strategy requires bounds.maximum')}
            if($strategy -ne 'fixed' -and -not $adaptive){[void]$visualProblems.Add('adaptive strategy requires bounds.adaptive=true')}
            foreach($requiredViewport in @('narrow','wide','high-dpi','extreme-resolution')){if($viewports -notcontains $requiredViewport){[void]$visualProblems.Add("viewports missing $requiredViewport")}}
            if($visualProblems.Count -gt 0){
                $state='blocked'
                [void]$findings.Add([pscustomobject]@{code='VisualEvidenceSchemaInvalid';path=(Rel $evidenceFull);detail=($visualProblems -join '; ')})
            }
            $row.bounds=$bounds
            $row.viewports=$viewports
            $row.strategy=$strategy
        }
        [void]$rows.Add($row)
    }
}else{
    foreach($id in $required){
        $weight=if($id -eq 'visual'){3}elseif($id -eq 'interaction'){2}else{1}
        $critical=($id -in $criticalDimensions)
        [void]$rows.Add((Row $id 'not-run' 'Unity or behavioral evidence manifest was not supplied.' '' $weight $critical))
    }
}
$staticRules=New-Object 'System.Collections.Generic.List[object]'
$ruleFailures=New-Object 'System.Collections.Generic.List[object]'
$allTargetText=(@($editorFiles|ForEach-Object {try{ReadStrict $_.FullName}catch{''}}) -join "`n")
$rulePatterns=@{
 'EW-01'=@('EditorWindow|CustomEditor|PropertyDrawer|MenuItem|ScriptedImporter'); 'EW-02'=@('GetWindow\s*<|GetWindow\s*\(|singleton|single.?instance'); 'EW-03'=@('position\s*=|ShowAsDropDown|ShowUtility|ShowModalUtility|opening.?position'); 'EW-04'=@('minSize|maxSize|MinimumSize|MaximumSize|ESWindow_MinSize|ESWindow_MaxSize|AdaptiveMaximum|HostBound'); 'EW-05'=@('title|status|primary|summary|empty'); 'EW-06'=@('phase|stage|section|step'); 'EW-07'=@('SectionId|NavigatorId|section.?id|navigator.?id'); 'EW-08'=@('scroll|ScrollView|single.?axis|scroll.?owner'); 'EW-09'=@('action.?host|toolbar|menu|inspector|page'); 'EW-10'=@('primary.?action|Apply|Build|Create|Run'); 'EW-11'=@('error|failed|empty|recovery|status|state'); 'EW-12'=@('Undo\.|SetDirty|Dirty|AssetDatabase\.|SerializedObject|write'); 'EW-13'=@('StopPropagation|Focus|GUIUtility|event|pointer|drag'); 'EW-14'=@('owner(Key)?|parent|child|Bind|Unbind|Suspend|Close|Dispose'); 'EW-15'=@('sleep|wake|ESWindowFoundation|ESWindowSleepContract'); 'EW-16'=@('ReloadDomain|AssemblyReloadEvents|PlayMode|Unbind|Suspend|Close|rebind'); 'EW-17'=@('cache|budget|performance|Profiler|incremental|memo'); 'EW-18'=@('PreviewSession|RenderTexture|DestroyImmediate|cleanup|release'); 'EW-19'=@('ES.?Token|theme|USS|style|color'); 'EW-20'=@('tooltip|keyboard|shortcut|GUID|Hash|long|path|DPI|ellipsis'); 'EW-21'=@('RegisterCallback<DragUpdatedEvent>|RegisterCallback<DragPerformEvent>|RegisterCallback<DragLeaveEvent>|TrickleDown\.TrickleDown')
}
$eventRouteFailures=New-Object 'System.Collections.Generic.List[string]'
$eventRouteEvidence=$true
$hasConcreteWindow = @($windowRecords | Where-Object { -not $_.isAbstract }).Count -gt 0
$targetIsSharedWorkbenchHost = (Rel $targetFull) -match '(?i)(^|/)ESWorkbenchUIToolkitHost\.cs$'
if($resolvedTargetKind -eq 'Workbench'){
    $eventRoutePatterns=@(
        'RegisterCallback<DragUpdatedEvent>\s*\(\s*OnDragUpdated\s*,\s*TrickleDown\.TrickleDown\s*\)',
        'RegisterCallback<DragPerformEvent>\s*\(\s*OnDragPerform\s*,\s*TrickleDown\.TrickleDown\s*\)',
        'RegisterCallback<DragLeaveEvent>\s*\(\s*OnDragLeave\s*,\s*TrickleDown\.TrickleDown\s*\)',
        'UnregisterCallback<DragUpdatedEvent>\s*\(\s*OnDragUpdated\s*,\s*TrickleDown\.TrickleDown\s*\)',
        'UnregisterCallback<DragPerformEvent>\s*\(\s*OnDragPerform\s*,\s*TrickleDown\.TrickleDown\s*\)',
        'UnregisterCallback<DragLeaveEvent>\s*\(\s*OnDragLeave\s*,\s*TrickleDown\.TrickleDown\s*\)',
        'RegisterCallback<DragExitedEvent>\s*\(\s*OnDragExited\s*,\s*TrickleDown\.TrickleDown\s*\)',
        'UnregisterCallback<DragExitedEvent>\s*\(\s*OnDragExited\s*,\s*TrickleDown\.TrickleDown\s*\)',
        'RegisterCallback<PointerCaptureOutEvent>\s*\(\s*OnRootPointerCaptureOut\s*,\s*TrickleDown\.TrickleDown\s*\)',
        'RegisterCallback<FocusOutEvent>\s*\(\s*OnRootFocusOut\s*,\s*TrickleDown\.TrickleDown\s*\)'
    )
    foreach($pattern in $eventRoutePatterns){if($allTargetText -notmatch $pattern){[void]$eventRouteFailures.Add($pattern)}}
    if(@([regex]::Matches($allTargetText,'(?s)(?:OnDragLeave|OnDragExited|OnRootPointerCaptureOut|OnRootFocusOut|OnRootDetachedFromPanel)\s*\([^)]*\).*?CancelWorkbenchDrag\(true\)')).Count -lt 5){
        [void]$eventRouteFailures.Add('all-owner-release-paths-call-CancelWorkbenchDrag(true)')
    }
    $eventRouteEvidence=($eventRouteFailures.Count -eq 0)
}
if($null -ne $ruleRegistry){
    foreach($rule in @($ruleRegistry.rules)){
        $applicable=@($rule.applicableTo) -contains $resolvedTargetKind
        # Shared workbench hosts participate in the Workbench contract but do not
        # own the singleton/menu entry. Apply EW-02 only when the bounded closure
        # actually contains a concrete EditorWindow declaration.
        if([string]$rule.ruleId -eq 'EW-02' -and ($targetIsSharedWorkbenchHost -or -not $hasConcreteWindow)){
            [void]$staticRules.Add([pscustomobject]@{ruleId=[string]$rule.ruleId;title=[string]$rule.title;status='not-applicable';severity=[string]$rule.severity;details='Shared workbench host has no concrete EditorWindow declaration; singleton entry is owned by the domain window.';evidenceLevel='S2'});continue
        }
        if(-not $applicable){[void]$staticRules.Add([pscustomobject]@{ruleId=[string]$rule.ruleId;title=[string]$rule.title;status='not-applicable';severity=[string]$rule.severity;details="TargetKind $resolvedTargetKind is outside this rule's scope.";evidenceLevel='S1'});continue}
        if([string]$rule.ruleId -eq 'EW-21'){
            $ruleStatus=if($eventRouteEvidence){'passed'}else{'blocked'}
            $detail=if($eventRouteEvidence){'Workbench drag events use host trickle-down routing with matching unregistration and all owner-release paths call CancelWorkbenchDrag(true).'}else{"Event-routing contract failed: $($eventRouteFailures -join '; ')"}
        }else{
            $matched=HasAny $allTargetText $rulePatterns[[string]$rule.ruleId]
            if($resolvedTargetKind -eq 'InspectorDrawer' -and [string]$rule.ruleId -eq 'EW-01'){
                $matched = $matched -or $allTargetText -match '(?i)Odin(?:Attribute)?Drawer|PropertyDrawer|CustomPropertyDrawer'
            }
            if($resolvedTargetKind -eq 'InspectorDrawer' -and [string]$rule.ruleId -eq 'EW-17'){
                $matched = $matched -or $allTargetText -match '(?i)Dictionary|HashSet|cache|memo|incremental'
            }
            $ruleStatus=if($matched){'passed'}else{'blocked'}
            $detail=if($matched){'Static source evidence matched the responsibility-specific rule pattern.'}else{'No responsibility-specific static evidence matched; declare an approved exception or add implementation evidence.'}
        }
        $item=[pscustomobject]@{ruleId=[string]$rule.ruleId;title=[string]$rule.title;status=$ruleStatus;severity=[string]$rule.severity;details=$detail;evidenceLevel='S2';runtimeChecks=@($rule.runtimeChecks)}
        [void]$staticRules.Add($item)
        if($ruleStatus -eq 'blocked'){[void]$ruleFailures.Add($item)}
    }
}
$visualRow=@($rows|Where-Object {$_.id -eq 'visual'}|Select-Object -First 1)
if($layoutFindings.Count -gt 0 -and $visualRow.Count -gt 0){$visualRow[0].status='blocked';$visualRow[0].details='Layout bounds failed; a fixed maximum or approved adaptive strategy is required.'}
$hard=@($rows|Where-Object {$_.status -in @('failed','blocked')})
$notRun=@($rows|Where-Object {$_.status -eq 'not-run'})
$criticalHard=@($rows|Where-Object {$_.critical -and $_.status -in @('failed','blocked')})
$criticalMissing=@($rows|Where-Object {$_.critical -and $_.status -eq 'not-run'})
$runtimeRequired=$ValidationMode -in @('Acceptance','Release')
$runtimeAuthorizationValid=$false
$runtimeAuthorizationStatus=if($runtimeRequired){'missing'}else{'not-required'}
if($runtimeRequired){
    if([string]::IsNullOrWhiteSpace($RuntimeAuthorizationPath)){
        [void]$findings.Add([pscustomobject]@{code='RuntimeAuthorizationMissing';path='';detail='Acceptance/Release requires an explicit one-time Runtime authorization manifest.'})
    }else{
        try{
            $authValidator=Join-Path $root '.agents/skills/es-skill-governance/scripts/Test-ESRuntimeAuthorization.ps1'
            if(-not(Test-Path -LiteralPath $authValidator -PathType Leaf)){throw 'Runtime authorization validator is missing'}
            $authOutput=& powershell -NoProfile -File $authValidator -ProjectRoot $root -AuthorizationPath $RuntimeAuthorizationPath 2>&1 | Out-String
            if($LASTEXITCODE -ne 0){throw $authOutput.Trim()}
            $runtimeAuthorizationValid=$true;$runtimeAuthorizationStatus='validated'
        }catch{
            $runtimeAuthorizationStatus='invalid'
            [void]$findings.Add([pscustomobject]@{code='RuntimeAuthorizationInvalid';path=$RuntimeAuthorizationPath;detail=$_.Exception.Message})
        }
    }
}
$staticCriticalRuleFailures=@($ruleFailures|Where-Object {$_.severity -eq 'critical'})
$staticHard=@($rows|Where-Object {$_.id -in @('structural','framework-integration','static-boundary','transient-popup-contract','preview-window-contract','inspector-drawer-contract','shader-gui-contract') -and $_.status -in @('failed','blocked')})
$staticVisualBlocked=$layoutFindings.Count -gt 0
$runtimeHard=@($rows|Where-Object {
    $isStaticVisual = $_.id -eq 'visual' -and $staticVisualBlocked
    $isRuntimeDimension = $_.id -in @('compile','reloadDomain','interaction','visual','recovery','performance')
    $isHardState = $_.status -in @('failed','blocked')
    $isRuntimeDimension -and $isHardState -and -not $isStaticVisual
})
$runtimeFailed=@($runtimeHard|Where-Object {$_.status -eq 'failed'})
$runtimeBlocked=@($runtimeHard|Where-Object {$_.status -eq 'blocked'})
$status=if($structStatus -eq 'failed'){'Unavailable'}elseif($staticHard.Count -gt 0 -or $criticalHard.Count -gt 0 -or $staticCriticalRuleFailures.Count -gt 0){'Blocked'}elseif($runtimeRequired -and (-not $runtimeAuthorizationValid -or $criticalMissing.Count -gt 0 -or $runtimeHard.Count -gt 0 -or $notRun.Count -gt 0)){'Blocked'}elseif($ValidationMode -eq 'Development' -and $criticalMissing.Count -gt 0){'Blocked'}elseif($ValidationMode -eq 'Development' -and ($runtimeHard.Count -gt 0 -or $notRun.Count -gt 0)){'Degraded'}else{'Ready'}
$evidenceDisplay=''
if($EvidencePath){$evidenceDisplay=$EvidencePath.Replace('\','/')}
$targetHashes=@($editorFiles|ForEach-Object {[pscustomobject]@{path=(Rel $_.FullName);sha256=(Hash $_.FullName)}})
$result=[ordered]@{}
$result.schemaVersion=1
$result.validator='es-editor-availability-validator'
$result.validationMode=$ValidationMode
$result.status=$status
$result.staticStatus=if($staticHard.Count -gt 0 -or $criticalHard.Count -gt 0 -or $staticCriticalRuleFailures.Count -gt 0 -or $structStatus -eq 'failed'){'static-blocked'}else{'static-passed'}
$result.runtimeStatus=if($runtimeFailed.Count -gt 0){'runtime-failed'}elseif($runtimeBlocked.Count -gt 0 -or ($runtimeRequired -and $notRun.Count -gt 0)){'runtime-blocked'}elseif($notRun.Count -gt 0){'runtime-not-run'}else{'runtime-passed'}
$result.runtimeAuthorizationRequired=$runtimeRequired
$result.runtimeAuthorizationStatus=$runtimeAuthorizationStatus
$result.overallVerdict=if($staticHard.Count -gt 0 -or $criticalHard.Count -gt 0 -or $staticCriticalRuleFailures.Count -gt 0 -or $structStatus -eq 'failed'){'StaticBlocked'}elseif($ValidationMode -eq 'StaticReview'){'StaticCompleteRuntimePending'}elseif($runtimeFailed.Count -gt 0){'RuntimeFailed'}elseif($runtimeBlocked.Count -gt 0 -or ($runtimeRequired -and ($notRun.Count -gt 0 -or -not $runtimeAuthorizationValid))){'RuntimeRequiredForSelectedProfile'}elseif($ValidationMode -eq 'Development' -and $status -eq 'Degraded'){'EngineeringDiagnosticDegraded'}elseif($ValidationMode -eq 'Development'){'EngineeringDiagnosticComplete'}elseif($ValidationMode -eq 'Acceptance'){'RuntimeAcceptanceComplete'}else{'ReleaseAcceptanceComplete'}
$result.scope=if($ValidationMode -eq 'StaticReview'){'source/configuration/boundary-only'}elseif($ValidationMode -eq 'Development'){'engineering-diagnostic-with-optional-runtime'}elseif($ValidationMode -eq 'Acceptance'){'runtime-acceptance'}else{'release-acceptance'}
$result.claimsNotProven=@($rows|Where-Object {$_.status -in @('not-run','blocked')}|ForEach-Object {[string]$_.id})
$result.nextAction=if($result.overallVerdict -eq 'StaticBlocked'){'Fix source/configuration/boundary findings; do not start Runtime.'}elseif($result.overallVerdict -eq 'StaticCompleteRuntimePending'){'Static review is complete; do not modify source solely for missing Runtime evidence. Request developer authorization only if Runtime claims are needed.'}elseif($result.overallVerdict -eq 'RuntimeRequiredForSelectedProfile' -and -not $runtimeAuthorizationValid){'Provide a valid one-time authorization bound to PlanHash, AICommand, TaskContract, target, budget, timeout and stop condition before Runtime.'}elseif($result.overallVerdict -eq 'RuntimeRequiredForSelectedProfile'){'Run only the missing evidence under the validated one-time authorization.'}elseif($result.overallVerdict -eq 'RuntimeFailed'){'Inspect the Runtime receipt and fix the observed external behavior before rerunning the authorized profile.'}elseif($result.overallVerdict -eq 'EngineeringDiagnosticDegraded'){'Resolve non-critical missing Runtime dimensions or request bounded Runtime authorization; do not call this release-ready.'}else{'No further evidence action is required for this profile.'}
$result.targetPath=(Rel $targetFull)
$result.targetKind=$resolvedTargetKind
$result.targetKindSource=$targetKindSource
$result.requiredDimensions=$required
$result.targetContractProvided=($null -ne $targetContract)
$registryId='';if($null -ne $ruleRegistry){$registryId=[string]$ruleRegistry.registryId}
$result.editorRuleRegistry=[ordered]@{registryId=$registryId;ruleCount=$staticRules.Count;staticRules=@($staticRules.ToArray());blockedCount=$ruleFailures.Count}
$result.targetHashes=$targetHashes
$result.dimensions=@($rows.ToArray())
$result.policy=[ordered]@{defaultOrder='StaticDeepReplay-first';staticWeight=0.7;runtimeWeight=0.3;runtimeAuthorizationRequired=$runtimeRequired;criticalDimensions=$criticalDimensions;weights=[ordered]@{'framework-integration'=3;'visual'=3;'interaction'=2;'structural'=1;'static-boundary'=2;'compile'=1;'reloadDomain'=1;'recovery'=1;'performance'=1};layoutBoundsRequired=($resolvedTargetKind -in @('EditorWindow','Workbench','InspectorDrawer'));detectedStaticMaxStrategy=$maxBoundStrategy;approvedMaxStrategies=@('fixed','adaptive-resolve','content-adaptive','host-bounded','unbounded-flexible');extremeViewportEvidenceRequired=($resolvedTargetKind -in @('EditorWindow','Workbench','InspectorDrawer'));targetKindMatrix=$matrix}
$result.findings=@($findings.ToArray())
$result.evidencePath=$evidenceDisplay
$result.generatedUtc=[DateTime]::UtcNow.ToString('o')
if($ReportPath){if([IO.Path]::IsPathRooted($ReportPath)){throw 'ReportPath must be project-relative'};$reportFull=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$ReportPath));if(-not $reportFull.StartsWith($rootFull+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'ReportPath escapes ProjectRoot'};$parent=Split-Path -Parent $reportFull;if(-not(Test-Path $parent)){New-Item -ItemType Directory -Path $parent -Force|Out-Null};[IO.File]::WriteAllText($reportFull,($result|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)))}
$result|ConvertTo-Json -Depth 8
if($status -eq 'Ready'){exit 0}elseif($status -eq 'Unavailable'){exit 2}else{exit 1}
