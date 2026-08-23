[CmdletBinding()]
param(
    [string]$ProjectRoot='.',
    [string]$ReportPath='ES/Output/ESEditorCommercialStaticReport.json',
    [switch]$BuildEditor
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path.TrimEnd('\','/')
$checks=@();$warnings=@();$notRun=@()
function Add-Check([string]$id,[string]$status,[string]$message,[string[]]$sources=@()){$script:checks+=[ordered]@{id=$id;status=$status;message=$message;sources=$sources}}
function Rel([string]$p){$full=[IO.Path]::GetFullPath($p);$base=$root.TrimEnd('\','/');if($full.StartsWith($base,[StringComparison]::OrdinalIgnoreCase)){return $full.Substring($base.Length).TrimStart('\','/').Replace('\','/')}return $p}
function Read-Strict([string]$p){[IO.File]::ReadAllText($p,(New-Object Text.UTF8Encoding($false,$true)))}
function Exists([string]$p){Test-Path -LiteralPath (Join-Path $root $p) -PathType Leaf}
$required=@(
  'Assets/Plugins/ES/0_Stand/BaseDefine_Law/ESDialog.cs',
  'Assets/Plugins/ES/Editor/EditorTools/ESAdvancedDialog/ESAdvancedDialog.cs',
  'Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs',
  'Assets/Plugins/ES/Editor/ESDrawer/Normal/ESEditorSectionAttributeProcessor.cs',
  'Assets/Plugins/ES/Editor/ESDrawer/Normal/ESEditorSectionNavigatorDrawer.cs',
  'Assets/Plugins/ES/0_Stand/Tests/Dialog/ESDialogContractTests.cs')
$missing=@($required|Where-Object{-not(Exists $_)})
Add-Check 'required-editor-contracts' $(if($missing.Count -eq 0){'passed'}else{'failed'}) $(if($missing.Count -eq 0){'All editor dialog, presentation, Section and contract test anchors exist.'}else{'Missing: '+($missing -join ', ')}) $required
$cs=@(Get-ChildItem (Join-Path $root 'Assets/Plugins/ES') -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue)
$native=@();foreach($f in $cs){$n=0;foreach($line in [IO.File]::ReadLines($f.FullName)){ $n++;if($line -match 'EditorUtility\.DisplayDialog(?:Complex)?\s*\('){$native+=[ordered]@{path=Rel $f.FullName;line=$n;category=if($f.FullName -match '(?i)Test|Tests'){ 'test'}elseif($f.FullName -match '(?i)Installer|File|Folder|Directory|Progress|Upload|Import'){ 'system-or-progress'}else{'business-or-editor-prompt'}}}}}
$productionShader=@($native|Where-Object{$_.path -match '^Assets/Plugins/ES/Editor/ESShader/' -and $_.category -ne 'test'})
Add-Check 'composite-shader-no-native-dialog' $(if($productionShader.Count -eq 0){'passed'}else{'failed'}) $(if($productionShader.Count -eq 0){'CompositeShader production path has no direct native dialog call.'}else{'Direct native dialog calls remain in CompositeShader production.'}) @('Assets/Plugins/ES/Editor/ESShader')
$inventory=[ordered]@{schemaVersion=1;generatedUtc=[DateTime]::UtcNow.ToString('o');totalMatches=$native.Count;fileCount=@($native|ForEach-Object path|Sort-Object -Unique).Count;items=$native}
$dialogInventory=Join-Path $root 'ES/Output/EditorDialogMigrationInventory.json';New-Item -ItemType Directory -Force -Path (Split-Path $dialogInventory) | Out-Null;[IO.File]::WriteAllText($dialogInventory,($inventory|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)))
Add-Check 'dialog-migration-inventory' 'passed' ("Generated inventory: $($native.Count) matches across $($inventory.fileCount) files; categories are explicit and require per-item disposition.") @('ES/Output/EditorDialogMigrationInventory.json')
$presentation=(Read-Strict (Join-Path $root 'Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs'))+"`n"+(Read-Strict (Join-Path $root 'Assets/Plugins/ES/Editor/ESPresentation/ESEditorHealthWindow.cs'))+"`n"+(Read-Strict (Join-Path $root 'Assets/Plugins/ES/Editor/ESPresentation/ESEditorThemeWindow.cs'));$sleepContract=([regex]::Matches($presentation,'ESWindowSleepContract')).Count;$getWindow=([regex]::Matches($presentation,'GetWindow<')).Count
Add-Check 'window-lifecycle-static-contract' $(if($sleepContract -gt 0 -and $getWindow -gt 0){'passed'}else{'failed'}) ("Sleep contract references=$sleepContract; GetWindow references=$getWindow; runtime reload/PlayMode behavior remains unproven.") @('Assets/Plugins/ES/Editor/ESPresentation/Core/ESEditorPresentationCore.cs')
$section=(Read-Strict (Join-Path $root 'Assets/Plugins/ES/Editor/ESDrawer/Normal/ESEditorSectionNavigatorDrawer.cs'))+"`n"+(Read-Strict (Join-Path $root 'Assets/Plugins/ES/Editor/ESDrawer/Normal/ESEditorSectionAttributeProcessor.cs'))+"`n"+(Read-Strict (Join-Path $root 'Assets/Plugins/ES/Editor/ESPresentation/ESEditorHealthWindow.cs'));$sectionFlags=@('ConditionalWeakTable','SessionState','PropertyTree')|Where-Object{$section -match [regex]::Escape($_)}
Add-Check 'section-projection-static-contract' $(if($sectionFlags.Count -eq 3){'passed'}else{'failed'}) ("Section contract markers: $($sectionFlags -join ', ')") @('Assets/Plugins/ES/Editor/ESDrawer/Normal/ESEditorSectionNavigatorDrawer.cs')
$serializedCount=@(rg -l 'SerializedObject' (Join-Path $root 'Assets/Plugins/ES/Editor') -g '*.cs').Count
Add-Check 'serialized-object-authority-static-signal' $(if($serializedCount -gt 0){'passed'}else{'failed'}) ("Editor SerializedObject usage files=$serializedCount; static presence does not prove every Section path is correctly rebound.") @('Assets/Plugins/ES/Editor')
$dialogTests=Read-Strict (Join-Path $root 'Assets/Plugins/ES/0_Stand/Tests/Dialog/ESDialogContractTests.cs');$testFlags=@('InfoModal','AllowMainWorkspaceFallback','Presenter')|Where-Object{$dialogTests -match [regex]::Escape($_)}
Add-Check 'dialog-contract-static-tests' $(if($testFlags.Count -eq 3){'passed'}else{'failed'}) ("Dialog test markers: $($testFlags -join ', ')") @('Assets/Plugins/ES/0_Stand/Tests/Dialog/ESDialogContractTests.cs')
$editorProject=Join-Path $root 'ES_Editor.csproj';if($BuildEditor){$buildOut=@(& dotnet build $editorProject --no-restore --nologo 2>&1);$exit=$LASTEXITCODE;Add-Check 'es-editor-build' $(if($exit -eq 0){'passed'}else{'failed'}) $(if($exit -eq 0){'ES_Editor.csproj built successfully.'}else{($buildOut|Select-Object -Last 8)-join "`n"}) @('ES_Editor.csproj')}else{Add-Check 'es-editor-build' 'not-run' 'Build omitted; rerun with -BuildEditor.' @('ES_Editor.csproj');$notRun+='es-editor-build'}
foreach($t in @('ES.MenuTree.Editor.Tests.csproj','ES.CompositeShader.Editor.Tests.csproj')){if(-not(Exists $t)){Add-Check ('test-project-'+$t) 'failed' 'Test project missing.' @($t);continue};$text=Read-Strict (Join-Path $root $t);$sdk=$text -match 'Microsoft.NET.Test.Sdk|NUnit|xunit|MSTest';if($sdk){Add-Check ('test-project-'+$t) 'passed' 'Test project declares a discoverable test framework; runtime execution remains separate.' @($t)}else{Add-Check ('test-project-'+$t) 'not-run' 'Project exists but no test SDK declaration was found; no test execution is claimed.' @($t);$notRun+=$t}}
$notRun+=@('Unity ReloadDomain','Unity PlayMode','Unity multi-display/high-DPI','Unity Test Runner runtime execution')
$report=[ordered]@{schemaVersion=1;reportId='es-editor-commercial-static.v1';generatedUtc=[DateTime]::UtcNow.ToString('o');scope='ES editor framework static evidence';status=if(@($checks|Where-Object status -eq 'failed').Count -eq 0){'passed-with-not-run-runtime'}else{'failed'};checks=$checks;runtimeNotRun=$notRun;nativeDialogSummary=[ordered]@{matches=$native.Count;files=$inventory.fileCount;inventory='ES/Output/EditorDialogMigrationInventory.json'};limitations=@('Static checks do not prove Unity Editor runtime behavior.','Native dialog inventory requires per-entry migration or approved exception disposition.')}
$reportFull=Join-Path $root $ReportPath;$reportDir=Split-Path $reportFull;New-Item -ItemType Directory -Force -Path $reportDir|Out-Null;$tmp="$reportFull.$([Guid]::NewGuid().ToString('N')).tmp";try{[IO.File]::WriteAllText($tmp,($report|ConvertTo-Json -Depth 10),(New-Object Text.UTF8Encoding($false)));Move-Item $tmp $reportFull -Force}finally{if(Test-Path $tmp){Remove-Item $tmp -Force -ErrorAction SilentlyContinue}}
$report|ConvertTo-Json -Depth 10
if($report.status -eq 'failed'){exit 1};exit 0
