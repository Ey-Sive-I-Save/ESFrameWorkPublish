[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [Parameter(Mandatory=$true)][string]$Objective,
    [string]$AliasPath='.agents/SKILL_ROUTE_ALIASES.zh-CN.json'
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$registry=Get-Content -Raw -Encoding UTF8 (Join-Path $root $AliasPath.Replace('/','\'))|ConvertFrom-Json
$normalizedObjective=$Objective
$matches=@()
foreach($entry in @($registry.skills.PSObject.Properties)){
    $skillName=$entry.Name;$skillPath=[IO.Path]::Combine($root,'.agents','skills',$skillName)
    $aliases=@($entry.Value|ForEach-Object {[string]$_}|Where-Object {$_})
    $hits=@($aliases|Where-Object {$normalizedObjective.IndexOf($_,[StringComparison]::OrdinalIgnoreCase) -ge 0})
    if($hits.Count -gt 0){
        $govPath=Join-Path $skillPath 'governance.json';$gov=$null
        if(Test-Path -LiteralPath $govPath -PathType Leaf){
            try{$gov=Get-Content -Raw -Encoding UTF8 $govPath|ConvertFrom-Json}
            catch{throw "Matched Skill governance is invalid for '$skillName': $($_.Exception.Message)"}
        }
        $matches += [pscustomobject]@{skillName=$skillName;matchedAliases=$hits;routeKeys=@($gov.routeKeys|ForEach-Object {[string]$_});skillPath=('.agents'+'/'+'skills'+'/'+$skillName);skillFileExists=(Test-Path -LiteralPath (Join-Path $skillPath 'SKILL.md') -PathType Leaf)}
    }
}
$status=if($matches.Count -eq 0){'NoSkillRoute'}elseif($matches.Count -eq 1){'Matched'}else{'Ambiguous'}
[ordered]@{schemaVersion=1;resolver='es-chinese-skill-route';locale='zh-CN';objective=$Objective;status=$status;matches=@($matches);nextAction=if($status -eq 'Matched'){'Read the matched project SKILL.md and governance.json; do not infer permission from the alias.'}elseif($status -eq 'Ambiguous'){'Read only shared upstream context, then disambiguate by object/action/risk before loading a Skill.'}else{'Report NoSkillRoute and use AIBRAIN_ENTRY/KnowledgeIndex fallback; do not guess a Skill.'}}|ConvertTo-Json -Depth 8
