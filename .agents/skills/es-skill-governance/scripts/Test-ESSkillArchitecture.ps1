[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [string]$ReportPath,
    [switch]$BuildManifest,
    [ValidateSet('CurrentUserDirect','ManagedAIBrain')][string]$AuthorizationLane='CurrentUserDirect'
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\','/')
. (Join-Path $PSScriptRoot 'ESPathBoundary.Common.ps1')
function Rel([string]$path){$full=[IO.Path]::GetFullPath($path);if(-not $full.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){return $null};$full.Substring($root.Length+1).Replace('\','/')}
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
function ReadStrict([string]$path){[IO.File]::ReadAllText($path,(New-Object Text.UTF8Encoding($false,$true)))}
function YamlBlock([string]$text,[string]$name){$m=[regex]::Match($text,'(?ms)^  '+[regex]::Escape($name)+':\s*\n(?:(?!^  [a-z0-9][a-z0-9-]*:).)*');if($m.Success){$m.Value}else{$null}}
function Scalar([string]$block,[string]$key){$m=[regex]::Match($block,'(?m)^\s+'+[regex]::Escape($key)+':\s*(?<value>[^\r\n]+)');if($m.Success){$m.Groups['value'].Value.Trim().Trim([char]34,[char]39)}else{''}}
function InlineList([string]$block,[string]$key){$m=[regex]::Match($block,'(?m)^\s+'+[regex]::Escape($key)+':\s*\[([^\]]*)\]');if(-not $m.Success){return @()};@($m.Groups[1].Value.Split(',')|ForEach-Object {$_.Trim().Trim([char]34,[char]39)}|Where-Object {$_})}
function KnowledgeBlocks([string]$text){@([regex]::Matches($text,'(?ms)^\s*-\s+knowledgeId:.*?(?=^\s*-\s+knowledgeId:|\z)')|ForEach-Object {$_.Value})}
function JsonHash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
function Add-ManagedFinding([string]$code,[string]$skill,[string]$detail){
    $severity=if($AuthorizationLane -eq 'ManagedAIBrain'){'blocked'}else{'review'}
    [void]$findings.Add([pscustomobject]@{severity=$severity;code=$code;skill=$skill;detail=$detail})
}
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
    # Generic keys are valid auxiliary signals when a domain key is present. The
    # preceding generic-route-only check already hard-blocks a Skill that has no
    # domain route; retaining this as an info finding avoids turning an expected
    # composite route into a false Architecture review.
    if(@($routes|Where-Object {$generic -contains $_}).Count -gt 0){[void]$findings.Add([pscustomobject]@{severity='info';code='generic-route-overlap';skill=$name;detail='Generic routeKey is auxiliary; a domain routeKey is present and remains authoritative.'})}
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
$bindingJson=$null
$commandJson=$null
try{$bindingJson=Get-Content $bindingPath -Raw -Encoding UTF8|ConvertFrom-Json}catch{Add-ManagedFinding 'command-binding-registry-missing-or-invalid' '*' $_.Exception.Message}
try{$commandJson=Get-Content $commandCatalogPath -Raw -Encoding UTF8|ConvertFrom-Json}catch{Add-ManagedFinding 'command-catalog-missing-or-invalid' '*' $_.Exception.Message}
$bindingEntries=if($bindingJson){@($bindingJson.entries)}else{@()}
$commandEntries=if($commandJson){@($commandJson.commands)}else{@()}
$exemptions=if($bindingJson){@($bindingJson.nonExecutionExemptions)}else{@()}
$exemptNames=@($exemptions|ForEach-Object {[string]$_.skillName}|Where-Object {$_})
foreach($exemption in $exemptions){
    $exemptSkill=[string]$exemption.skillName
    if(@($skillDirectories|Where-Object Name -eq $exemptSkill).Count -ne 1){Add-ManagedFinding 'command-exemption-skill-missing' $exemptSkill 'Non-execution exemption must reference exactly one Skill.'}
    if([string]$exemption.reason -notmatch '\S'){Add-ManagedFinding 'command-exemption-reason-missing' $exemptSkill 'Non-execution exemption requires a reason.'}
    if(@($exemption.allowedOutputs|Where-Object {[string]$_ -match '\S'}).Count -eq 0){Add-ManagedFinding 'command-exemption-output-missing' $exemptSkill 'Non-execution exemption requires bounded output types.'}
}
$boundNames=@($bindingEntries|ForEach-Object {[string]$_.skillName}|Sort-Object -Unique)
foreach($binding in $bindingEntries){
    $name=[string]$binding.skillName;$commandId=[string]$binding.commandId
    if(@($skillDirectories|Where-Object Name -eq $name).Count -ne 1){Add-ManagedFinding 'command-binding-skill-missing' $name 'Command binding references a missing or duplicate Skill.';continue}
    $command=@($commandEntries|Where-Object {[string]$_.id -ceq $commandId})
    if($command.Count -ne 1){Add-ManagedFinding 'command-binding-command-missing' $name "Command binding does not resolve uniquely: $commandId";continue}
    $commandPath=Join-Path $root ([string]$command[0].path)
    if(-not(Test-Path $commandPath -PathType Leaf)){Add-ManagedFinding 'command-body-missing' $name $commandPath;continue}
    if([string]$binding.commandHash -ne (Hash $commandPath)){Add-ManagedFinding 'command-binding-hash-stale' $name $commandId}
    if($binding.PSObject.Properties.Name -contains 'taskContractRequired' -and [bool]$binding.taskContractRequired -and [string]$binding.taskContractRef -notmatch '\S'){
        Add-ManagedFinding 'task-contract-ref-missing' $name $commandId
    }
    $taskContractRef = if($binding.PSObject.Properties.Name -contains 'taskContractRef'){[string]$binding.taskContractRef}else{''}
    if($taskContractRef -match '\S' -and -not(Test-Path (Join-Path $root $taskContractRef) -PathType Leaf)){
        Add-ManagedFinding 'task-contract-missing' $name $taskContractRef
    }
}
foreach($dir in $skillDirectories){
    $gov=Get-Content (Join-Path $dir.FullName 'governance.json') -Raw -Encoding UTF8|ConvertFrom-Json
    if([string]$gov.writePolicy -notin @('read-only','report-only-explicit-path') -and $boundNames -notcontains $dir.Name -and $exemptNames -notcontains $dir.Name){
        Add-ManagedFinding 'command-binding-unresolved' $dir.Name 'Skill declares a managed-channel write policy but has no entry in command-binding-registry.json.'
    }
    if($exemptNames -contains $dir.Name){
        [void]$findings.Add([pscustomobject]@{severity='info';code='command-binding-not-applicable';skill=$dir.Name;detail='Registered non-execution exemption; a managed AIBrain side effect still requires its channel binding.'})
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
        # Resource Index points to the manifest by path but does not embed its hash,
        # so the manifest can bind the Resource Index without a circular hash.
        foreach($metadataPath in @('.agents/SKILL_DISCOVERY_POLICY.json','.agents/SKILL_RESOURCE_INDEX.yaml','.agents/SKILL_CATALOG.yaml','Documentation/AIKnowledge/AIBRAIN_ENTRY.md','Assets/Plugins/ES/AICommands/AICommandCatalog.json')){
            $full=Join-Path $root $metadataPath;$declared=$manifest.metadata.PSObject.Properties[$metadataPath]
            $actualHash=if(Test-Path -LiteralPath $full -PathType Leaf){Hash $full}else{'missing'}
            if($null -eq $declared -or [string]$declared.Value -ne $actualHash){
                if($metadataPath -eq 'Assets/Plugins/ES/AICommands/AICommandCatalog.json'){
                    Add-ManagedFinding 'registry-managed-metadata-stale' '*' "Registry managed-channel metadata hash stale: $metadataPath"
                }else{
                    [void]$findings.Add([pscustomobject]@{severity='blocked';code='registry-metadata-stale';skill='*';detail="Registry metadata hash stale: $metadataPath"})
                }
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
$blocked=@($findings|Where-Object severity -eq 'blocked');$reviews=@($findings|Where-Object severity -eq 'review');$infos=@($findings|Where-Object severity -eq 'info')
$status=if($blocked.Count -gt 0){'blocked'}elseif($reviews.Count -gt 0){'review'}else{'passed'}
$skillCount=($stateCounts.Values|Measure-Object -Sum|Select-Object -ExpandProperty Sum)
$manifestRelative=if(Test-Path $manifestPath){Rel $manifestPath}else{$null}
$result=[ordered]@{schemaVersion=1;validator='es-skill-architecture';generatedUtc=[DateTime]::UtcNow.ToString('o');authorizationLane=$AuthorizationLane;status=$status;skillCount=$skillCount;stateCounts=$stateCounts;findings=@($findings.ToArray());blockedCount=$blocked.Count;reviewCount=$reviews.Count;infoCount=$infos.Count;manifestPath=$manifestRelative}
if($ReportPath){
    try{$reportTarget=Resolve-ESContainedRelativePath -Candidate $ReportPath -ContainerRoot $root -Label 'ReportPath'}
    catch{Write-Error $_.Exception.Message -ErrorAction Continue;exit 2}
    $reportFull=$reportTarget.FullPath
    $parent=Split-Path -Parent $reportFull
    if(-not(Test-Path $parent)){New-Item -ItemType Directory -Path $parent -Force|Out-Null}
    $reportFull=(Resolve-ESContainedRelativePath -Candidate $reportTarget.RelativePath -ContainerRoot $root -Label 'ReportPath').FullPath
    $temporary="$reportFull.tmp-$([Guid]::NewGuid().ToString('N'))"
    try{
        [IO.File]::WriteAllText($temporary,($result|ConvertTo-Json -Depth 12),(New-Object Text.UTF8Encoding($false)))
        Move-Item -LiteralPath $temporary -Destination $reportFull -Force
    }finally{
        if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary -Force}
    }
}
$result|ConvertTo-Json -Depth 12
if($status -eq 'blocked'){exit 1};exit 0
