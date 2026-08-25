[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\','/')
$catalogPath=Join-Path $root '.agents/SKILL_CATALOG.yaml'
$policy=Get-Content (Join-Path $root '.agents/SKILL_DISCOVERY_POLICY.json') -Raw -Encoding UTF8|ConvertFrom-Json
$raw=[IO.File]::ReadAllText($catalogPath,(New-Object Text.UTF8Encoding($false,$true)))
function ResolveExpected([object]$gov){
    $state=$policy.states.PSObject.Properties[[string]$gov.maturity];if($null -eq $state){throw "Unknown maturity: $($gov.maturity)"}
    $d=[string]$state.Value.discoveryState;$p=[string]$state.Value.planEligibility;$r=[string]$state.Value.runtimeEligibility
    $override=$policy.deliveryOverrides.PSObject.Properties[[string]$gov.delivery]
    if($null -ne $override){
        if($override.Value.PSObject.Properties.Name -contains 'discoveryState'){$d=[string]$override.Value.discoveryState}
        if($override.Value.PSObject.Properties.Name -contains 'planEligibility'){$p=[string]$override.Value.planEligibility}
        if($override.Value.PSObject.Properties.Name -contains 'runtimeEligibility'){$r=[string]$override.Value.runtimeEligibility}
    }
    [ordered]@{discoveryState=$d;planEligibility=$p;runtimeEligibility=$r;reviewRequired=$true}
}
foreach($dir in Get-ChildItem (Join-Path $root '.agents/skills') -Directory|Where-Object {Test-Path (Join-Path $_.FullName 'governance.json')}|Sort-Object Name){
    $name=$dir.Name;$gov=Get-Content (Join-Path $dir.FullName 'governance.json') -Raw -Encoding UTF8|ConvertFrom-Json;$expected=ResolveExpected $gov
    $pattern='(?ms)^  '+[regex]::Escape($name)+':\s*\n(?:(?!^  [a-z0-9][a-z0-9-]*:\s*$).)*'
    $match=[regex]::Match($raw,$pattern);if(-not $match.Success){throw "Catalog record missing: $name"}
    $block=$match.Value
    foreach($field in @('discoveryState','planEligibility','runtimeEligibility','reviewRequired')){
        $value=if($field -eq 'reviewRequired'){'true'}else{[string]$expected[$field]}
        $line="    $field`: $value"
        $fieldPattern='(?m)^[ \t]+'+[regex]::Escape($field)+':[ \t]*[^\r\n]*(?:\r?$)'
        if($block -match $fieldPattern){$block=[regex]::Replace($block,$fieldPattern,"    $field`: $value")}
        else{$block=[regex]::Replace($block,'(?m)^([ \t]+registrationState:[ \t]*[^\r\n]*)(?:\r?$)',"`$1`r`n$line",1)}
    }
    $raw=$raw.Remove($match.Index,$match.Length).Insert($match.Index,$block)
}
$temp="$catalogPath.tmp-$([Guid]::NewGuid().ToString('N'))";[IO.File]::WriteAllText($temp,$raw,(New-Object Text.UTF8Encoding($false)));Move-Item -LiteralPath $temp -Destination $catalogPath -Force
Write-Output 'PASS: Catalog lifecycle fields synchronized with SKILL_DISCOVERY_POLICY.json'
