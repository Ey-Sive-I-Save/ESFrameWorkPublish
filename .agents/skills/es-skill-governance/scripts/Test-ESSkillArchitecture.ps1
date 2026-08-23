[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [string]$ReportPath='ES/Output/SkillArchitecture/architecture.json',
    [switch]$BuildManifest
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\','/')
function Rel([string]$path){$full=[IO.Path]::GetFullPath($path);if(-not $full.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){return $null};$full.Substring($root.Length+1).Replace('\','/')}
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
function ReadStrict([string]$path){[IO.File]::ReadAllText($path,(New-Object Text.UTF8Encoding($false,$true)))}
function YamlBlock([string]$text,[string]$name){$m=[regex]::Match($text,'(?ms)^  '+[regex]::Escape($name)+':\s*\n(?:(?!^  [a-z0-9][a-z0-9-]*:).)*');if($m.Success){$m.Value}else{$null}}
function Scalar([string]$block,[string]$key){$m=[regex]::Match($block,'(?m)^\s+'+[regex]::Escape($key)+':\s*(?<value>[^\r\n]+)');if($m.Success){$m.Groups['value'].Value.Trim().Trim([char]34,[char]39)}else{''}}
function InlineList([string]$block,[string]$key){$m=[regex]::Match($block,'(?m)^\s+'+[regex]::Escape($key)+':\s*\[([^\]]*)\]');if(-not $m.Success){return @()};@($m.Groups[1].Value.Split(',')|ForEach-Object {$_.Trim().Trim([char]34,[char]39)}|Where-Object {$_})}
function KnowledgeBlocks([string]$text){@([regex]::Matches($text,'(?ms)^\s*-\s+knowledgeId:.*?(?=^\s*-\s+knowledgeId:|\z)')|ForEach-Object {$_.Value})}
function JsonHash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
function ResolveExpected([object]$policy,[object]$gov){
    $state=$policy.states.PSObject.Properties[[string]$gov.maturity]
    if($null -eq $state){throw "maturity not registered: $($gov.maturity)"}
    $d=[string]$state.Value.discoveryState;$p=[string]$state.Value.planEligibility;$r=[string]$state.Value.runtimeEligibility
    $override=$policy.deliveryOverrides.PSObject.Properties[[string]$gov.delivery]
    if($null -ne $override){
        if($override.Value.PSObject.Properties.Name -contains 'discoveryState'){$d=[string]$override.Value.discoveryState}
        if($override.Value.PSObject.Properties.Name -contains 'planEligibility'){$p=[string]$override.Value.planEligibility}
        if($override.Value.PSObject.Properties.Name -contains 'runtimeEligibility'){$r=[string]$override.Value.runtimeEligibility}
    }
    [ordered]@{discoveryState=$d;planEligibility=$p;runtimeEligibility=$r}
}
$policyPath=Join-Path $root '.agents/SKILL_DISCOVERY_POLICY.json'
$policy=Get-Content $policyPath -Raw -Encoding UTF8|ConvertFrom-Json
$catalogPath=Join-Path $root '.agents/SKILL_CATALOG.yaml';$catalog=ReadStrict $catalogPath
$generic=@($policy.genericRouteKeys|ForEach-Object {[string]$_})
$findings=New-Object 'System.Collections.Generic.List[object]';$stateCounts=@{}
$skillDirectories=@(Get-ChildItem (Join-Path $root '.agents/skills') -Directory|Where-Object {Test-Path (Join-Path $_.FullName 'SKILL.md')}|Sort-Object Name)
foreach($dir in $skillDirectories){
    $name=$dir.Name;$govPath=Join-Path $dir.FullName 'governance.json';$gov=Get-Content $govPath -Raw -Encoding UTF8|ConvertFrom-Json
    try{$expected=ResolveExpected $policy $gov}catch{[void]$findings.Add([pscustomobject]@{severity='blocked';code='unknown-lifecycle';skill=$name;detail=$_.Exception.Message});continue}
    $actual=$expected.discoveryState;$previous=0;if($stateCounts.ContainsKey($actual)){$previous=[int]$stateCounts[$actual]};$stateCounts[$actual]=1+$previous
    $block=YamlBlock $catalog $name
    if(-not $block){[void]$findings.Add([pscustomobject]@{severity='blocked';code='missing-catalog';skill=$name;detail='Catalog record missing.'});continue}
    $catalogDiscovery=Scalar $block 'discoveryState';$catalogPlan=Scalar $block 'planEligibility';$catalogRuntime=Scalar $block 'runtimeEligibility'
    if($catalogDiscovery -and $catalogDiscovery -ne $expected.discoveryState){[void]$findings.Add([pscustomobject]@{severity='blocked';code='catalog-discovery-mismatch';skill=$name;detail="$catalogDiscovery != $($expected.discoveryState)"})}
    if($catalogPlan -and $catalogPlan -ne $expected.planEligibility){[void]$findings.Add([pscustomobject]@{severity='blocked';code='catalog-plan-mismatch';skill=$name;detail="$catalogPlan != $($expected.planEligibility)"})}
    if($catalogRuntime -and $catalogRuntime -ne $expected.runtimeEligibility){[void]$findings.Add([pscustomobject]@{severity='blocked';code='catalog-runtime-mismatch';skill=$name;detail="$catalogRuntime != $($expected.runtimeEligibility)"})}
    $routes=@($gov.routeKeys|ForEach-Object {[string]$_}|Where-Object {$_})
    if(@($routes|Where-Object {$generic -notcontains $_}).Count -eq 0){[void]$findings.Add([pscustomobject]@{severity='blocked';code='generic-route-only';skill=$name;detail='At least one domain routeKey is required.'})}
    if(@($routes|Where-Object {$generic -contains $_}).Count -gt 0){[void]$findings.Add([pscustomobject]@{severity='review';code='generic-route-overlap';skill=$name;detail='Generic routeKey is auxiliary and must not be used as the only intent signal.'})}
}
$knowledgePath=Join-Path $root 'Documentation/AIKnowledge/KnowledgeIndex.yaml'
$knowledgeText=ReadStrict $knowledgePath
$knowledgeBlocks=KnowledgeBlocks $knowledgeText
foreach($dir in $skillDirectories){
    $name=$dir.Name;$gov=Get-Content (Join-Path $dir.FullName 'governance.json') -Raw -Encoding UTF8|ConvertFrom-Json
    $routes=@($gov.routeKeys|ForEach-Object {[string]$_}|Where-Object {$_})
    $matches=@($knowledgeBlocks|Where-Object {$_ -match ('relatedSkills:\s*\[[^\]]*\b'+[regex]::Escape($name)+'\b')})
    $routeMatch=$false
    foreach($block in $matches){$knowledgeRoutes=InlineList $block 'routeKeys';if(@($knowledgeRoutes|Where-Object {$routes -contains $_}).Count -gt 0){$routeMatch=$true}}
    if($matches.Count -eq 0){[void]$findings.Add([pscustomobject]@{severity='blocked';code='knowledge-binding-missing';skill=$name;detail='No Knowledge relatedSkills binding exists.'})}
    elseif(-not $routeMatch){[void]$findings.Add([pscustomobject]@{severity='blocked';code='knowledge-route-disjoint';skill=$name;detail='Knowledge binding exists but has no routeKey intersection.'})}
}
$bindingPath=Join-Path $root '.agents/skills/es-skill-governance/references/command-binding-registry.json'
$commandCatalogPath=Join-Path $root 'Assets/Plugins/ES/AICommands/AICommandCatalog.json'
$bindingJson=Get-Content $bindingPath -Raw -Encoding UTF8|ConvertFrom-Json
$commandJson=Get-Content $commandCatalogPath -Raw -Encoding UTF8|ConvertFrom-Json
$boundNames=@($bindingJson.entries|ForEach-Object {[string]$_.skillName}|Sort-Object -Unique)
foreach($binding in @($bindingJson.entries)){
    $name=[string]$binding.skillName;$commandId=[string]$binding.commandId
    if(@($skillDirectories|Where-Object Name -eq $name).Count -ne 1){[void]$findings.Add([pscustomobject]@{severity='blocked';code='command-binding-skill-missing';skill=$name;detail='Command binding references a missing or duplicate Skill.'});continue}
    $command=@($commandJson.commands|Where-Object {[string]$_.id -ceq $commandId})
    if($command.Count -ne 1){[void]$findings.Add([pscustomobject]@{severity='blocked';code='command-binding-command-missing';skill=$name;detail="Command binding does not resolve uniquely: $commandId"});continue}
    $commandPath=Join-Path $root ([string]$command[0].path)
    if(-not(Test-Path $commandPath -PathType Leaf)){[void]$findings.Add([pscustomobject]@{severity='blocked';code='command-body-missing';skill=$name;detail=$commandPath});continue}
    if([string]$binding.commandHash -ne (Hash $commandPath)){[void]$findings.Add([pscustomobject]@{severity='blocked';code='command-binding-hash-stale';skill=$name;detail=$commandId})}
    if($binding.PSObject.Properties.Name -contains 'taskContractRequired' -and [bool]$binding.taskContractRequired -and [string]$binding.taskContractRef -notmatch '\S'){
        [void]$findings.Add([pscustomobject]@{severity='blocked';code='task-contract-ref-missing';skill=$name;detail=$commandId})
    }
    if([string]$binding.taskContractRef -match '\S' -and -not(Test-Path (Join-Path $root ([string]$binding.taskContractRef)) -PathType Leaf)){
        [void]$findings.Add([pscustomobject]@{severity='blocked';code='task-contract-missing';skill=$name;detail=[string]$binding.taskContractRef})
    }
}
foreach($dir in $skillDirectories){
    $gov=Get-Content (Join-Path $dir.FullName 'governance.json') -Raw -Encoding UTF8|ConvertFrom-Json
    if([string]$gov.writePolicy -notin @('read-only','report-only-explicit-path') -and $boundNames -notcontains $dir.Name){
        [void]$findings.Add([pscustomobject]@{severity='review';code='command-binding-unresolved';skill=$dir.Name;detail='Skill declares an authorized write policy but has no entry in command-binding-registry.json.'})
    }
}
$manifestPath=Join-Path $root '.agents/SKILL_REGISTRY.manifest.json'
if(-not (Test-Path $manifestPath)){
    [void]$findings.Add([pscustomobject]@{severity='blocked';code='registry-manifest-missing';skill='*';detail='Run Build-ESSkillRegistryManifest.ps1 before architecture acceptance.'})
} else {
    try {
        $manifest=Get-Content $manifestPath -Raw -Encoding UTF8|ConvertFrom-Json
        if([int]$manifest.schemaVersion -ne 1 -or [string]$manifest.manifestId -ne 'esframework-skill-registry'){
            [void]$findings.Add([pscustomobject]@{severity='blocked';code='registry-manifest-schema';skill='*';detail='Registry manifest schema or identity is invalid.'})
        }
        foreach($metadataPath in @('.agents/SKILL_DISCOVERY_POLICY.json','.agents/SKILL_RESOURCE_INDEX.yaml','.agents/SKILL_CATALOG.yaml','Documentation/AIKnowledge/KnowledgeIndex.yaml','Assets/Plugins/ES/AICommands/AICommandCatalog.json')){
            $full=Join-Path $root $metadataPath;$declared=$manifest.metadata.PSObject.Properties[$metadataPath]
            if($null -eq $declared -or [string]$declared.Value -ne (Hash $full)){
                [void]$findings.Add([pscustomobject]@{severity='blocked';code='registry-metadata-stale';skill='*';detail="Registry metadata hash stale: $metadataPath"})
            }
        }
        $manifestNames=@($manifest.skills|ForEach-Object {[string]$_.skillName});$actualNames=@($skillDirectories|ForEach-Object {[string]$_.Name})
        if($manifestNames.Count -ne $actualNames.Count -or @($actualNames|Where-Object {$manifestNames -notcontains $_}).Count -gt 0){[void]$findings.Add([pscustomobject]@{severity='blocked';code='registry-skill-set-mismatch';skill='*';detail='Registry manifest Skill set does not match direct Skill roots.'})}
        foreach($record in @($manifest.skills)){
            $name=[string]$record.skillName;$dir=Join-Path $root ".agents/skills/$name";$govPath=Join-Path $dir 'governance.json';$skillPath=Join-Path $dir 'SKILL.md'
            if(-not (Test-Path $skillPath) -or -not (Test-Path $govPath)){continue}
            if([string]$record.skillHash -ne (Hash $skillPath) -or [string]$record.governanceHash -ne (Hash $govPath)){[void]$findings.Add([pscustomobject]@{severity='blocked';code='registry-skill-hash-stale';skill=$name;detail='Registry Skill or governance hash is stale.'})}
        }
    } catch { [void]$findings.Add([pscustomobject]@{severity='blocked';code='registry-manifest-invalid';skill='*';detail=$_.Exception.Message}) }
}
$blocked=@($findings|Where-Object severity -eq 'blocked');$reviews=@($findings|Where-Object severity -eq 'review')
$status=if($blocked.Count -gt 0){'blocked'}elseif($reviews.Count -gt 0){'review'}else{'passed'}
$skillCount=($stateCounts.Values|Measure-Object -Sum|Select-Object -ExpandProperty Sum)
$manifestRelative=if(Test-Path $manifestPath){Rel $manifestPath}else{$null}
$result=[ordered]@{schemaVersion=1;validator='es-skill-architecture';generatedUtc=[DateTime]::UtcNow.ToString('o');status=$status;skillCount=$skillCount;stateCounts=$stateCounts;findings=@($findings.ToArray());manifestPath=$manifestRelative}
$reportFull=Join-Path $root $ReportPath.Replace('/','\');$parent=Split-Path -Parent $reportFull;if(-not(Test-Path $parent)){New-Item -ItemType Directory -Path $parent -Force|Out-Null};[IO.File]::WriteAllText($reportFull,($result|ConvertTo-Json -Depth 12),(New-Object Text.UTF8Encoding($false)))
$result|ConvertTo-Json -Depth 12
if($status -eq 'blocked'){exit 1};exit 0
