[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateSet('Build','Compare')][string]$Mode,
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [Parameter(Mandatory=$true)][string]$SessionId,
    [string]$BaselinePath,
    [string]$SnapshotPath='ES/Output/SkillSessionSnapshots/current.json',
    [string]$ReportPath,
    [string[]]$SkillNames=@(),
    [string[]]$RouteKeys=@(),
    [ValidateSet('Operational','CapabilityIndex','Audit')][string]$DiscoveryMode='Operational',
    [ValidateSet('explicit-user-drift','queue-update','session-resume','catalog-change','governance-change','knowledge-route-change','plan-bound-resource-change')][string]$Trigger='explicit-user-drift'
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\','/')
if(-not (Test-Path -LiteralPath $root -PathType Container)){throw "ProjectRoot not found: $ProjectRoot"}
if([string]::IsNullOrWhiteSpace($SessionId) -or $SessionId -notmatch '^[A-Za-z0-9._:-]{1,160}$'){throw 'SessionId is invalid.'}
$routeFilter=@($RouteKeys|ForEach-Object {([string]$_).Trim().ToLowerInvariant()}|Where-Object {$_}|Sort-Object -Unique)
$policyPath=Join-Path $root '.agents/SKILL_DISCOVERY_POLICY.json'
if(-not (Test-Path -LiteralPath $policyPath -PathType Leaf)){throw 'Skill discovery policy is missing.'}
try{$policy=Get-Content -LiteralPath $policyPath -Raw -Encoding UTF8|ConvertFrom-Json}catch{throw 'Skill discovery policy is invalid JSON.'}
if([int]$policy.schemaVersion -ne 1 -or $null -eq $policy.states -or $null -eq $policy.selectionModes){throw 'Skill discovery policy schema is invalid.'}
function Rel([string]$path){
    $full=[IO.Path]::GetFullPath($path)
    if(-not $full.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Path escapes ProjectRoot: $path"}
    $full.Substring($root.Length+1).Replace('\','/')
}
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
function AddFile([System.Collections.Generic.List[object]]$list,[string]$relative){
    $full=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$relative))
    if(-not $full.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Path escapes ProjectRoot: $relative"}
    if(Test-Path -LiteralPath $full -PathType Leaf){[void]$list.Add([pscustomobject][ordered]@{path=(Rel $full);sha256=(Hash $full)})}
}
function GetRouteKeys([string]$name){
    $path=Join-Path $root ".agents/skills/$name/governance.json"
    if(-not (Test-Path -LiteralPath $path -PathType Leaf)){return @()}
    $gov=Get-Content -LiteralPath $path -Raw -Encoding UTF8|ConvertFrom-Json
    @($gov.routeKeys|ForEach-Object {([string]$_).Trim().ToLowerInvariant()}|Where-Object {$_}|Sort-Object -Unique)
}
function ResolveEligibility([string]$maturity,[string]$delivery,[string]$registrationState){
    $state=$policy.states.PSObject.Properties[[string]$maturity]
    if($null -eq $state){throw "Unsupported Skill maturity: $maturity"}
    $discovery=[string]$state.Value.discoveryState
    $plan=[string]$state.Value.planEligibility
    $runtime=[string]$state.Value.runtimeEligibility
    $deliveryOverride=$policy.deliveryOverrides.PSObject.Properties[[string]$delivery]
    if($null -ne $deliveryOverride){
        $override=$deliveryOverride.Value
        if($override.PSObject.Properties.Name -contains 'discoveryState' -and -not [string]::IsNullOrWhiteSpace([string]$override.discoveryState)){$discovery=[string]$override.discoveryState}
        if($override.PSObject.Properties.Name -contains 'planEligibility' -and -not [string]::IsNullOrWhiteSpace([string]$override.planEligibility)){$plan=[string]$override.planEligibility}
        if($override.PSObject.Properties.Name -contains 'runtimeEligibility' -and -not [string]::IsNullOrWhiteSpace([string]$override.runtimeEligibility)){$runtime=[string]$override.runtimeEligibility}
    }
    $registration=$policy.registrationOverrides.PSObject.Properties[[string]$registrationState]
    [pscustomobject][ordered]@{
        discoveryState=$discovery
        planEligibility=$plan
        runtimeEligibility=$runtime
        reviewRequired=if($null -ne $registration){[bool]$registration.Value.reviewRequired}else{$true}
    }
}
function IsDiscoveryStateAllowed([string]$state){
    $allowed=@($policy.selectionModes.$DiscoveryMode|ForEach-Object {[string]$_})
    return $allowed -contains $state
}
function CollectSkill([System.Collections.Generic.List[object]]$list,[string]$name){
    $base=".agents/skills/$name"
    $files=New-Object 'System.Collections.Generic.List[object]'
    foreach($relative in @("$base/SKILL.md","$base/governance.json","$base/agents/openai.yaml","$base/static-replay.manifest.json")){AddFile $files $relative}
    foreach($kind in @('references','scripts')){
        $full=[IO.Path]::GetFullPath([IO.Path]::Combine($root,"$base/$kind"))
        if(Test-Path -LiteralPath $full -PathType Container){foreach($file in Get-ChildItem -LiteralPath $full -Recurse -File|Sort-Object FullName){AddFile $files (Rel $file.FullName)}}
    }
    $skillPath=Join-Path $root "$base/SKILL.md"
    if(-not (Test-Path -LiteralPath $skillPath -PathType Leaf)){throw "Skill SKILL.md not found: $name"}
    $govPath=Join-Path $root "$base/governance.json"
    $gov=Get-Content -LiteralPath $govPath -Raw -Encoding UTF8|ConvertFrom-Json
    $eligibility=ResolveEligibility ([string]$gov.maturity) ([string]$gov.delivery) 'NeedsReview'
    [void]$list.Add([pscustomobject][ordered]@{
        skillName=$name
        routeKeys=@(GetRouteKeys $name)
        maturity=[string]$gov.maturity
        delivery=[string]$gov.delivery
        discoveryState=$eligibility.discoveryState
        planEligibility=$eligibility.planEligibility
        runtimeEligibility=$eligibility.runtimeEligibility
        reviewRequired=$eligibility.reviewRequired
        files=@($files|Sort-Object path)
        skillHash=(Hash $skillPath)
    })
}
function RouteMatch([object]$skill){
    if($routeFilter.Count -eq 0){return $false}
    if(-not (IsDiscoveryStateAllowed ([string]$skill.discoveryState))){return $false}
    $generic=@($policy.genericRouteKeys|ForEach-Object {([string]$_).ToLowerInvariant()})
    $specificFilters=@($routeFilter|Where-Object {$generic -notcontains $_})
    if($specificFilters.Count -eq 0){return $false}
    @($skill.routeKeys|Where-Object {$specificFilters -contains ([string]$_).ToLowerInvariant()}).Count -gt 0
}
function SkillFromChange([object]$change){if($change.PSObject.Properties.Name -contains 'skillName'){return [string]$change.skillName};return ''}
$metadata=New-Object 'System.Collections.Generic.List[object]'
foreach($index in @('.agents/SKILL_RESOURCE_INDEX.yaml','.agents/SKILL_CATALOG.yaml','.agents/SKILL_DISCOVERY_POLICY.json','.agents/SKILL_REGISTRY.manifest.json','Documentation/AIKnowledge/KnowledgeIndex.yaml','Documentation/AIKnowledge/AIBRAIN_ENTRY.md','.agents/skills/es-skill-governance/references/capability-mode-registry.json','.agents/skills/es-skill-governance/references/command-binding-registry.json','Assets/Plugins/ES/AICommands/AICommandCatalog.json')){AddFile $metadata $index}
$skillsRoot=Join-Path $root '.agents/skills'
$names=if($SkillNames.Count -gt 0){@($SkillNames|Sort-Object -Unique)}else{@(Get-ChildItem -LiteralPath $skillsRoot -Directory|Where-Object{Test-Path (Join-Path $_.FullName 'SKILL.md')}|Select-Object -ExpandProperty Name|Sort-Object)}
$skillRecords=New-Object 'System.Collections.Generic.List[object]'
foreach($name in $names){if($name -notmatch '^[a-z0-9-]+$'){throw "Invalid Skill name: $name"};CollectSkill $skillRecords $name}
$canonical=[ordered]@{schemaVersion=2;sessionId=$SessionId;metadata=@($metadata|Sort-Object path);skills=@($skillRecords|Sort-Object skillName)}
$canonicalJson=$canonical|ConvertTo-Json -Depth 12 -Compress
$sha=[Security.Cryptography.SHA256]::Create();$snapshotHash=([BitConverter]::ToString($sha.ComputeHash([Text.UTF8Encoding]::new($false).GetBytes($canonicalJson)))).Replace('-','').ToLowerInvariant();$sha.Dispose()
$current=[ordered]@{schemaVersion=3;sessionId=$SessionId;generatedUtc=[DateTime]::UtcNow.ToString('o');snapshotHash=$snapshotHash;metadata=$canonical.metadata;skills=$canonical.skills;routeKeys=@($routeFilter);routeSelection=if($routeFilter.Count -gt 0){'scoped'}else{'unscoped'};discoveryMode=$DiscoveryMode}
$baseline=$null
if($Mode -eq 'Compare'){
    if([string]::IsNullOrWhiteSpace($BaselinePath)){throw 'Compare requires BaselinePath.'}
    $baselineFull=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$BaselinePath))
    if(-not $baselineFull.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'BaselinePath escapes ProjectRoot.'}
    if(-not (Test-Path -LiteralPath $baselineFull -PathType Leaf)){throw "BaselinePath not found: $BaselinePath"}
    $baseline=Get-Content -LiteralPath $baselineFull -Raw -Encoding UTF8|ConvertFrom-Json
}
$changes=New-Object 'System.Collections.Generic.List[object]'
if($baseline){
    $oldMetadata=@{};foreach($item in @($baseline.metadata)){$oldMetadata[[string]$item.path]=[string]$item.sha256}
    foreach($item in @($current.metadata)){$old=$oldMetadata[[string]$item.path];if($old -ne [string]$item.sha256){[void]$changes.Add([pscustomobject][ordered]@{kind='index-changed';path=$item.path;before=$old;after=$item.sha256})}}
    $oldSkills=@{};foreach($skill in @($baseline.skills)){$oldSkills[[string]$skill.skillName]=$skill}
    $newSkills=@{};foreach($skill in @($current.skills)){$newSkills[[string]$skill.skillName]=$skill}
    foreach($name in ($oldSkills.Keys+$newSkills.Keys|Sort-Object -Unique)){
        if(-not $oldSkills.ContainsKey($name)){[void]$changes.Add([pscustomobject][ordered]@{kind='added';skillName=$name;routeKeys=@($newSkills[$name].routeKeys)});continue}
        if(-not $newSkills.ContainsKey($name)){[void]$changes.Add([pscustomobject][ordered]@{kind='removed';skillName=$name;routeKeys=@($oldSkills[$name].routeKeys)});continue}
        $oldFiles=@{};foreach($file in @($oldSkills[$name].files)){$oldFiles[[string]$file.path]=[string]$file.sha256}
        $newFiles=@{};foreach($file in @($newSkills[$name].files)){$newFiles[[string]$file.path]=[string]$file.sha256}
        foreach($path in ($oldFiles.Keys+$newFiles.Keys|Sort-Object -Unique)){
            $before=$oldFiles[$path];$after=$newFiles[$path]
            if($before -ne $after){
                $kind=if($path -match '/governance\.json$' -and (@($oldSkills[$name].routeKeys|ForEach-Object {[string]$_}) -join ',') -ne (@($newSkills[$name].routeKeys|ForEach-Object {[string]$_}) -join ',')){'route-changed'}elseif($path -match '/SKILL\.md$|/governance\.json$|/openai\.yaml$|/static-replay\.manifest\.json$'){'metadata-changed'}else{'resource-changed'}
                [void]$changes.Add([pscustomobject][ordered]@{kind=$kind;skillName=$name;path=$path;before=$before;after=$after;routeKeys=@($newSkills[$name].routeKeys)})
            }
        }
    }
}
$selectedNames=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$selectedChanges=New-Object 'System.Collections.Generic.List[object]';$ignoredChanges=New-Object 'System.Collections.Generic.List[object]';$invalidated=New-Object 'System.Collections.Generic.List[object]'
$missingRouteScope=($Mode -eq 'Compare' -and $routeFilter.Count -eq 0 -and $changes.Count -gt 0)
foreach($change in $changes){
    $name=SkillFromChange $change
    $selected=$false
    if($name){$record=@($current.skills|Where-Object {$_.skillName -eq $name})[0];if(-not $record -and $baseline){$record=@($baseline.skills|Where-Object {$_.skillName -eq $name})[0]};$selected=RouteMatch $record}
    else{$selected=$true}
    if($missingRouteScope){$selected=$false}
    if($selected){[void]$selectedChanges.Add($change);if($name){[void]$selectedNames.Add($name);[void]$invalidated.Add([pscustomobject][ordered]@{binding=$name;reason='bound-skill-change';kind=$change.kind;path=$change.path})}else{[void]$invalidated.Add([pscustomobject][ordered]@{binding='route-index';reason='routing-metadata-change';kind=$change.kind;path=$change.path})}}else{[void]$ignoredChanges.Add([pscustomobject][ordered]@{reason=if($missingRouteScope){'missing-route-scope'}else{'out-of-scope-route'};change=$change})}
}
$status=if(-not $baseline){'refreshed'}elseif($missingRouteScope){'blocked'}elseif($changes.Count -eq 0){'unchanged'}elseif($selectedChanges.Count -gt 0){'stale'}else{'refreshed'}
$baselineHash='';if($baseline){$baselineHash=[string]$baseline.snapshotHash}
$nextAction=if($missingRouteScope){'replan'}elseif($invalidated.Count -gt 0){'replan'}elseif($selectedChanges.Count -gt 0){'read-selected'}else{'none'}
$result=[ordered]@{schemaVersion=3;validator='es-skill-session-refresh';mode=$Mode;trigger=$Trigger;refreshStrategy='metadata-first-incremental';sessionId=$SessionId;routeKeys=@($routeFilter);routeSelection=$current.routeSelection;discoveryMode=$DiscoveryMode;baselineSnapshotHash=$baselineHash;currentSnapshotHash=$snapshotHash;status=$status;changes=@($changes.ToArray());selectedSkills=@($selectedNames|Sort-Object);ignoredChanges=@($ignoredChanges.ToArray());invalidatedBindings=@($invalidated.ToArray());nextAction=$nextAction;snapshot=$current}
$snapshotFull=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$SnapshotPath));if(-not $snapshotFull.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'SnapshotPath escapes ProjectRoot.'};$parent=Split-Path -Parent $snapshotFull;if(-not(Test-Path $parent)){New-Item -ItemType Directory -Path $parent -Force|Out-Null};[IO.File]::WriteAllText($snapshotFull,($current|ConvertTo-Json -Depth 12),(New-Object Text.UTF8Encoding($false)))
if($ReportPath){$reportFull=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$ReportPath));if(-not $reportFull.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'ReportPath escapes ProjectRoot.'};$reportParent=Split-Path -Parent $reportFull;if(-not(Test-Path $reportParent)){New-Item -ItemType Directory -Path $reportParent -Force|Out-Null};[IO.File]::WriteAllText($reportFull,($result|ConvertTo-Json -Depth 14),(New-Object Text.UTF8Encoding($false)))}
$result|ConvertTo-Json -Depth 14
if($status -eq 'unchanged' -or $status -eq 'refreshed'){exit 0}else{exit 1}
