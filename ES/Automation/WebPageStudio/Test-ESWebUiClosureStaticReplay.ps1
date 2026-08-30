[CmdletBinding()]
param([string]$ReportPath='')
$ErrorActionPreference='Stop'
$tests=@('Test-ESWebPageStudioStaticPipeline.ps1','Test-ESWebKnowledgeStaticGate.ps1')
$results=@()
foreach($name in $tests){
    $path=Join-Path $PSScriptRoot $name
    try {
        if($name -eq 'Test-ESWebKnowledgeStaticGate.ps1') {
            $projectRoot=Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
            $path=Join-Path $projectRoot 'Documentation/AIKnowledge/tools/Test-ESWebKnowledgeStaticGate.ps1'
            $raw=& $path -ProjectRoot $projectRoot 2>$null
        } elseif($name -eq 'Test-ESWebPageStudioStaticPipeline.ps1') {
            $projectRoot=Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
            $artifact=Join-Path $projectRoot 'ES/Output/WebPageStudio/create-a-compact-command-atlas-layout'
            $raw=& $path -ArtifactDirectory $artifact 2>$null
        } else { $raw=& $path 2>$null }
        $json=($raw -join "`n")|ConvertFrom-Json
        $findings=@(); if($json.PSObject.Properties['checks']){$findings=@($json.checks|Where-Object {[string]$_.status -ne 'passed'}|ForEach-Object {[ordered]@{check=[string]$_.check;status=[string]$_.status;detail=[string]$_.detail}})}
        $results += [pscustomobject]@{test=$name;status=[string]$json.status;runtimeStatus=[string]$json.runtimeStatus;findings=$findings}
    } catch { $results += [pscustomobject]@{test=$name;status='failed';runtimeStatus='runtime-not-run';findings=@([ordered]@{check='validator-exception';status='failed';detail=$_.Exception.Message})} }
}
$failed=@($results|Where-Object status -ne 'passed')
$schemaResult=$results|Where-Object test -eq 'Test-ESWebUiReceiptSchemas.ps1'|Select-Object -First 1
$configNames=@{'environmentLock'='Test-ESWebUiEnvironmentMatrixGuards.ps1';'visualMatrixHash'='Test-ESWebUiVisualMatrixHash.ps1';'releaseBudgetHash'='Test-ESWebUiReleaseBudgetHash.ps1';'releaseStagingIdentity'='Test-ESWebUiReleaseStagingIdentity.ps1'}
$configurationCoverage=[ordered]@{};foreach($key in $configNames.Keys){$hit=$results|Where-Object test -eq $configNames[$key]|Select-Object -First 1;$configurationCoverage[$key]=if($hit){[string]$hit.status}else{'not-run'}}
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..')).TrimEnd('\')
$sourceHashes=[ordered]@{};foreach($relative in @('ES/Automation/WebPageStudio/ui-validation-matrix.yaml','ES/Automation/WebPageStudio/browser-environment.lock.json','ES/Automation/WebPageStudio/performance-budget.yaml','ES/Automation/Contracts/es-web-network-runtime-receipt-v1.schema.json','ES/Automation/Contracts/es-web-preview-runtime-receipt-v1.schema.json','ES/Automation/Contracts/es-web-visual-regression-receipt-v1.schema.json','ES/Automation/Contracts/es-web-release-acceptance-receipt-v1.schema.json','ES/Automation/Contracts/es-web-ui-sub-agent-schedule-v1.schema.json','Documentation/AIKnowledge/KnowledgeIndex.yaml','Documentation/AIKnowledge/WebKnowledgeExternalSourcePlan.yaml')){$sourcePath=Join-Path $projectRoot $relative;if(Test-Path -LiteralPath $sourcePath -PathType Leaf){$sourceHashes[$relative]=(Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()}}
$report=[ordered]@{schemaVersion=1;recordType='WebPageStudioStaticReplayReport';validator='web-ui-closure-static-replay';status=if($failed.Count){'failed'}else{'passed'};testCount=$results.Count;passedCount=(@($results|Where-Object status -eq 'passed').Count);failedCount=$failed.Count;tests=$results;schemaCoverage=[ordered]@{validator='web-ui-receipt-schemas';status=if($schemaResult){[string]$schemaResult.status}else{'not-run'};schemaCount=4};configurationCoverage=$configurationCoverage;sourceHashes=$sourceHashes;runtimeStatus='runtime-not-run';nonClaims=@('static-replay-only','does-not-prove-browser-network-unity-or-release','no-worker-dispatch');reportHash=$null}
Import-Module (Join-Path $PSScriptRoot '..\ABCD\ESABCDEvidence.psm1') -Force
$reportInput=($report|ConvertTo-Json -Depth 8 -Compress|ConvertFrom-Json)
$hashInput=[ordered]@{}; foreach($property in $reportInput.PSObject.Properties){if($property.Name -ne 'reportHash'){$hashInput[$property.Name]=$property.Value}}
$report.reportHash=Get-ESABCDEvidenceHash $hashInput
$json=$report|ConvertTo-Json -Depth 8
if($ReportPath){$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..')).TrimEnd('\')+'\';$reportFull=[IO.Path]::GetFullPath((Join-Path (Get-Location) $ReportPath));if(-not $reportFull.StartsWith($projectRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'ReportPath must remain under the project root.'};$parent=Split-Path -Parent $reportFull;if(-not(Test-Path -LiteralPath $parent -PathType Container)){New-Item -ItemType Directory -Path $parent -Force|Out-Null};[IO.File]::WriteAllText($reportFull,$json,[Text.UTF8Encoding]::new($false))}
$json
if($failed.Count){exit 1}
