[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [string]$OutputPath='.agents/SKILL_REGISTRY.manifest.json'
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\','/')
function Resolve-ProjectFile([string]$relative){
    $full=[IO.Path]::GetFullPath((Join-Path $root $relative.Replace('/','\')))
    if(-not $full.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Path escapes ProjectRoot: $relative"}
    if(-not (Test-Path -LiteralPath $full -PathType Leaf)){throw "Required project file is missing: $relative"}
    return $full
}
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
function ReadJson([string]$relative){Get-Content -LiteralPath (Resolve-ProjectFile $relative) -Raw -Encoding UTF8|ConvertFrom-Json}
function GetRouteKeys([object]$governance){@($governance.routeKeys|ForEach-Object {[string]$_}|Where-Object {$_}|Sort-Object -Unique)}
function GetCatalogBlock([string]$text,[string]$name){$m=[regex]::Match($text,'(?ms)^  '+[regex]::Escape($name)+':\s*\n(?:(?!^  [a-z0-9][a-z0-9-]*:\s*$).)*');if($m.Success){$m.Value}else{$null}}
function GetCatalogScalar([string]$block,[string]$key){$m=[regex]::Match($block,'(?m)^[ \t]+'+[regex]::Escape($key)+':[ \t]*(?<value>[^\r\n]+)');if($m.Success){$m.Groups['value'].Value.Trim().Trim([char]34,[char]39)}else{''}}
function ResolveEligibility([object]$policy,[object]$governance,[string]$registrationState){
    $state=$policy.states.PSObject.Properties[[string]$governance.maturity]
    if($null -eq $state){throw "Unknown maturity in policy: $($governance.maturity)"}
    $value=$state.Value
    $discovery=[string]$value.discoveryState; $plan=[string]$value.planEligibility; $runtime=[string]$value.runtimeEligibility
    $override=$policy.deliveryOverrides.PSObject.Properties[[string]$governance.delivery]
    if($null -ne $override){
        if($override.Value.PSObject.Properties.Name -contains 'discoveryState'){$discovery=[string]$override.Value.discoveryState}
        if($override.Value.PSObject.Properties.Name -contains 'planEligibility'){$plan=[string]$override.Value.planEligibility}
        if($override.Value.PSObject.Properties.Name -contains 'runtimeEligibility'){$runtime=[string]$override.Value.runtimeEligibility}
    }
    $registration=$policy.registrationOverrides.PSObject.Properties[[string]$registrationState]
    [ordered]@{discoveryState=$discovery;planEligibility=$plan;runtimeEligibility=$runtime;reviewRequired=if($null -eq $registration){$true}else{[bool]$registration.Value.reviewRequired}}
}
$policyPath=Resolve-ProjectFile '.agents/SKILL_DISCOVERY_POLICY.json'
$policy=Get-Content -LiteralPath $policyPath -Raw -Encoding UTF8|ConvertFrom-Json
if([int]$policy.schemaVersion -ne 1){throw 'Skill discovery policy schemaVersion must be 1.'}
$metadataPaths=@(
    '.agents/SKILL_DISCOVERY_POLICY.json',
    '.agents/SKILL_RESOURCE_INDEX.yaml',
    '.agents/SKILL_CATALOG.yaml',
    'Documentation/AIKnowledge/KnowledgeIndex.yaml',
    'Assets/Plugins/ES/AICommands/AICommandCatalog.json'
)
$metadata=[ordered]@{}
$catalogText=Get-Content -LiteralPath (Resolve-ProjectFile '.agents/SKILL_CATALOG.yaml') -Raw -Encoding UTF8
foreach($path in $metadataPaths){$metadata[$path]=Hash (Resolve-ProjectFile $path)}
$records=New-Object 'System.Collections.Generic.List[object]'
$skillsRoot=Join-Path $root '.agents/skills'
foreach($dir in Get-ChildItem -LiteralPath $skillsRoot -Directory|Sort-Object Name){
    $skillPath=Join-Path $dir.FullName 'SKILL.md'; $govPath=Join-Path $dir.FullName 'governance.json'
    if(-not (Test-Path -LiteralPath $skillPath -PathType Leaf)){continue}
    if(-not (Test-Path -LiteralPath $govPath -PathType Leaf)){throw "Skill governance missing: $($dir.Name)"}
    $governance=Get-Content -LiteralPath $govPath -Raw -Encoding UTF8|ConvertFrom-Json
    $eligibility=ResolveEligibility $policy $governance 'NeedsReview'
    $catalogBlock=GetCatalogBlock $catalogText $dir.Name
    if($null -eq $catalogBlock){throw "Catalog record missing: $($dir.Name)"}
    $family=GetCatalogScalar $catalogBlock 'family';$registrationState=GetCatalogScalar $catalogBlock 'registrationState'
    if([string]::IsNullOrWhiteSpace($family) -or [string]::IsNullOrWhiteSpace($registrationState)){throw "Catalog lifecycle identity missing: $($dir.Name)"}
    [void]$records.Add([ordered]@{
        skillName=$dir.Name
        maturity=[string]$governance.maturity
        delivery=[string]$governance.delivery
        registrationState=$registrationState
        discoveryState=$eligibility.discoveryState
        planEligibility=$eligibility.planEligibility
        runtimeEligibility=$eligibility.runtimeEligibility
        reviewRequired=$eligibility.reviewRequired
        routeKeys=@(GetRouteKeys $governance)
        family=$family
        owner=[string]$governance.owner
        acceptanceOwner=[string]$governance.acceptanceOwner
        skillHash=(Hash $skillPath)
        governanceHash=(Hash $govPath)
    })
}
$canonical=[ordered]@{schemaVersion=1;policyId=[string]$policy.policyId;metadata=$metadata;skills=@($records.ToArray())}
$json=$canonical|ConvertTo-Json -Depth 12 -Compress
$sha=[Security.Cryptography.SHA256]::Create();$registryHash=([BitConverter]::ToString($sha.ComputeHash([Text.UTF8Encoding]::new($false).GetBytes($json)))).Replace('-','').ToLowerInvariant();$sha.Dispose()
$manifest=[ordered]@{schemaVersion=1;manifestId='esframework-skill-registry';generatedUtc=[DateTime]::UtcNow.ToString('o');registryHash=$registryHash;policyHash=$metadata['.agents/SKILL_DISCOVERY_POLICY.json'];metadata=$metadata;skills=@($records.ToArray())}
$outputFull=[IO.Path]::GetFullPath((Join-Path $root $OutputPath.Replace('/','\')))
if(-not $outputFull.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'OutputPath escapes ProjectRoot.'}
$parent=Split-Path -Parent $outputFull;if(-not(Test-Path $parent)){New-Item -ItemType Directory -Path $parent -Force|Out-Null}
$temp="$outputFull.tmp-$([Guid]::NewGuid().ToString('N'))"
[IO.File]::WriteAllText($temp,($manifest|ConvertTo-Json -Depth 14),(New-Object Text.UTF8Encoding($false)))
Move-Item -LiteralPath $temp -Destination $outputFull -Force
$manifest|ConvertTo-Json -Depth 14
