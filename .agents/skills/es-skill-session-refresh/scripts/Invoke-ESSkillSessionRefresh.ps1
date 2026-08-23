[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateSet('Build','Compare')][string]$Mode,
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [Parameter(Mandatory=$true)][string]$SessionId,
    [string]$BaselinePath,
    [string]$SnapshotPath='ES/Output/SkillSessionSnapshots/current.json',
    [string]$ReportPath,
    [string[]]$SkillNames=@(),
    [string[]]$RouteKeys=@()
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\','/')
if(-not (Test-Path -LiteralPath $root -PathType Container)){throw "ProjectRoot not found: $ProjectRoot"}
if([string]::IsNullOrWhiteSpace($SessionId) -or $SessionId -notmatch '^[A-Za-z0-9._:-]{1,160}$'){throw 'SessionId is invalid.'}
$routeFilter=@($RouteKeys|ForEach-Object {([string]$_).Trim().ToLowerInvariant()}|Where-Object {$_}|Sort-Object -Unique)
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
    [void]$list.Add([pscustomobject][ordered]@{skillName=$name;routeKeys=@(GetRouteKeys $name);files=@($files|Sort-Object path);skillHash=(Hash $skillPath)})
}
function RouteMatch([object]$skill){
    if($routeFilter.Count -eq 0){return $true}
    @($skill.routeKeys|Where-Object {$routeFilter -contains ([string]$_).ToLowerInvariant()}).Count -gt 0
}
function SkillFromChange([object]$change){if($change.PSObject.Properties.Name -contains 'skillName'){return [string]$change.skillName};return ''}
$metadata=New-Object 'System.Collections.Generic.List[object]'
foreach($index in @('.agents/SKILL_RESOURCE_INDEX.yaml','.agents/SKILL_CATALOG.yaml','Documentation/AIKnowledge/KnowledgeIndex.yaml','Documentation/AIKnowledge/AIBRAIN_ENTRY.md','.agents/skills/es-skill-governance/references/capability-mode-registry.json','.agents/skills/es-skill-governance/references/command-binding-registry.json','Assets/Plugins/ES/AICommands/AICommandCatalog.json')){AddFile $metadata $index}
$skillsRoot=Join-Path $root '.agents/skills'
$names=if($SkillNames.Count -gt 0){@($SkillNames|Sort-Object -Unique)}else{@(Get-ChildItem -LiteralPath $skillsRoot -Directory|Where-Object{Test-Path (Join-Path $_.FullName 'SKILL.md')}|Select-Object -ExpandProperty Name|Sort-Object)}
$skillRecords=New-Object 'System.Collections.Generic.List[object]'
foreach($name in $names){if($name -notmatch '^[a-z0-9-]+$'){throw "Invalid Skill name: $name"};CollectSkill $skillRecords $name}
$canonical=[ordered]@{schemaVersion=2;sessionId=$SessionId;metadata=@($metadata|Sort-Object path);skills=@($skillRecords|Sort-Object skillName)}
$canonicalJson=$canonical|ConvertTo-Json -Depth 12 -Compress
$sha=[Security.Cryptography.SHA256]::Create();$snapshotHash=([BitConverter]::ToString($sha.ComputeHash([Text.UTF8Encoding]::new($false).GetBytes($canonicalJson)))).Replace('-','').ToLowerInvariant();$sha.Dispose()
$current=[ordered]@{schemaVersion=2;sessionId=$SessionId;generatedUtc=[DateTime]::UtcNow.ToString('o');snapshotHash=$snapshotHash;metadata=$canonical.metadata;skills=$canonical.skills;routeKeys=@($routeFilter);routeSelection=if($routeFilter.Count -gt 0){'scoped'}else{'unscoped'}}
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
foreach($change in $changes){
    $name=SkillFromChange $change
    $selected=$false
    if($name){$record=@($current.skills|Where-Object {$_.skillName -eq $name})[0];if(-not $record -and $baseline){$record=@($baseline.skills|Where-Object {$_.skillName -eq $name})[0]};$selected=RouteMatch $record}
    else{$selected=$true}
    if($selected){[void]$selectedChanges.Add($change);if($name){[void]$selectedNames.Add($name);[void]$invalidated.Add([pscustomobject][ordered]@{binding=$name;reason='bound-skill-change';kind=$change.kind;path=$change.path})}else{[void]$invalidated.Add([pscustomobject][ordered]@{binding='route-index';reason='routing-metadata-change';kind=$change.kind;path=$change.path})}}else{[void]$ignoredChanges.Add([pscustomobject][ordered]@{reason='out-of-scope-route';change=$change})}
}
$status=if(-not $baseline){'refreshed'}elseif($changes.Count -eq 0){'unchanged'}elseif($selectedChanges.Count -gt 0){'stale'}else{'refreshed'}
$baselineHash='';if($baseline){$baselineHash=[string]$baseline.snapshotHash}
$nextAction=if($invalidated.Count -gt 0){'replan'}elseif($selectedChanges.Count -gt 0){'read-selected'}else{'none'}
$result=[ordered]@{schemaVersion=2;validator='es-skill-session-refresh';mode=$Mode;sessionId=$SessionId;routeKeys=@($routeFilter);routeSelection=$current.routeSelection;baselineSnapshotHash=$baselineHash;currentSnapshotHash=$snapshotHash;status=$status;changes=@($changes.ToArray());selectedSkills=@($selectedNames|Sort-Object);ignoredChanges=@($ignoredChanges.ToArray());invalidatedBindings=@($invalidated.ToArray());nextAction=$nextAction;snapshot=$current}
$snapshotFull=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$SnapshotPath));if(-not $snapshotFull.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'SnapshotPath escapes ProjectRoot.'};$parent=Split-Path -Parent $snapshotFull;if(-not(Test-Path $parent)){New-Item -ItemType Directory -Path $parent -Force|Out-Null};[IO.File]::WriteAllText($snapshotFull,($current|ConvertTo-Json -Depth 12),(New-Object Text.UTF8Encoding($false)))
if($ReportPath){$reportFull=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$ReportPath));if(-not $reportFull.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'ReportPath escapes ProjectRoot.'};$reportParent=Split-Path -Parent $reportFull;if(-not(Test-Path $reportParent)){New-Item -ItemType Directory -Path $reportParent -Force|Out-Null};[IO.File]::WriteAllText($reportFull,($result|ConvertTo-Json -Depth 14),(New-Object Text.UTF8Encoding($false)))}
$result|ConvertTo-Json -Depth 14
if($status -eq 'unchanged' -or $status -eq 'refreshed'){exit 0}else{exit 1}
