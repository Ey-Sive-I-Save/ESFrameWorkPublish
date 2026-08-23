[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [string]$ReportPath='ES/Output/AutomationGovernance/compatibility.json'
)

$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\','/')
$utf8=New-Object Text.UTF8Encoding($false,$true)

function ReadStrict([string]$relativePath) {
    $full=Join-Path $root ($relativePath.Replace('/','\'))
    if(-not(Test-Path -LiteralPath $full -PathType Leaf)){ throw "Required ES compatibility source is missing: $relativePath" }
    [IO.File]::ReadAllText($full,$utf8)
}
function AddCheck([System.Collections.Generic.List[object]]$checks,[string]$id,[string]$claim,[bool]$passed,[string]$detail) {
    [void]$checks.Add([pscustomobject]@{checkId=$id;claim=$claim;status=if($passed){'passed'}else{'blocked'};detail=$detail})
}
function Has([string]$text,[string]$pattern) { [regex]::IsMatch($text,$pattern,[Text.RegularExpressions.RegexOptions]::Singleline) }

$checks=New-Object 'System.Collections.Generic.List[object]'
$sources=[ordered]@{
    center='Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs'
    facade='Assets/Plugins/ES/Editor/ESAutomation/ESAutomationFacade.cs'
    brain='Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs'
    bridge='Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs'
}
$text=@{}
foreach($key in $sources.Keys){ try { $text[$key]=ReadStrict $sources[$key]; AddCheck $checks "source-$key" "ES $key source remains present" $true $sources[$key] } catch { AddCheck $checks "source-$key" "ES $key source remains present" $false $_.Exception.Message } }

if($text.ContainsKey('center')){
    AddCheck $checks 'center-contract' 'Existing TaskContract and Worker registration remain authoritative' `
        (Has $text.center 'class\s+ESAutomationTaskContract' -and Has $text.center 'class\s+ESAutomationTaskRegistry' -and Has $text.center 'class\s+ESAutomationProcessRunner') `
        'TaskContract, TaskRegistry and ProcessRunner must remain available.'
}
if($text.ContainsKey('facade')){
    AddCheck $checks 'facade-entry' 'ES Facade remains the single task execution entry' `
        (Has $text.facade 'public\s+static\s+class\s+ESAutomationFacade' -and Has $text.facade 'public\s+static\s+ESAutomationTaskInvocationResult\s+RunTask\s*\(' -and Has $text.facade 'ESAutomationTaskRegistry\.TryGet') `
        'Facade must resolve a registered TaskContract before dispatch.'
    AddCheck $checks 'facade-endpoint-boundary' 'Facade dispatch remains Endpoint/Contract bound' `
        (Has $text.facade 'IESAutomationContractBoundEndpoint' -and Has $text.facade 'endpoint\.Run\(invocation\)') `
        'Facade must require a contract-bound endpoint and dispatch through it.'
}
if($text.ContainsKey('brain')){
    AddCheck $checks 'brain-facade-routing' 'AIBrain continues to route execution through ES Facade' `
        (Has $text.brain 'public\s+static\s+ESAutomationTaskInvocationResult\s+Run\s*\(' -and Has $text.brain 'ESAutomationFacade\.RunTask\(invocation\)') `
        'AIBrain may plan and authorize, but execution must terminate at ESAutomationFacade.'
    AddCheck $checks 'brain-no-direct-process' 'AIBrain does not directly launch arbitrary processes' `
        (-not (Has $text.brain '(?<!ESAutomationProcessRunner\.)\bProcess\.Start\s*\(')) `
        'Process launch belongs to a registered ES ProcessRunner adapter.'
}
if($text.ContainsKey('bridge')){
    AddCheck $checks 'bridge-actions' 'Existing AI Bridge actions remain discoverable' `
        (Has $text.bridge 'planTask' -and Has $text.bridge 'runTask') `
        'The ES AI Bridge must retain planTask and runTask compatibility actions.'
    AddCheck $checks 'bridge-brain-routing' 'AI Bridge routes through AIBrain' `
        (Has $text.bridge 'ESAIBrainCoordinator\.') `
        'AI Bridge must not become a parallel execution authority.'
}

$blocked=@($checks|Where-Object status -eq 'blocked')
$relative=$ReportPath.Replace('\','/')
$fullReport=Join-Path $root ($ReportPath.Replace('/','\'))
$parent=Split-Path -Parent $fullReport
if(-not(Test-Path -LiteralPath $parent)){New-Item -ItemType Directory -Path $parent -Force|Out-Null}
$result=[ordered]@{
    schemaVersion=1
    validator='es-automation-compatibility'
    generatedUtc=[DateTime]::UtcNow.ToString('o')
    status=if($blocked.Count -gt 0){'blocked'}else{'passed'}
    compatibilityPolicy='preserve-es-entry-points-and-add-governance-at-boundaries'
    checks=@($checks.ToArray())
    claimsNotProven=@('Unity runtime behavior','actual process execution','performance and visual behavior')
    reportPath=$relative
}
[IO.File]::WriteAllText($fullReport,($result|ConvertTo-Json -Depth 8),$utf8)
$result|ConvertTo-Json -Depth 8
if($blocked.Count -gt 0){exit 1};exit 0
