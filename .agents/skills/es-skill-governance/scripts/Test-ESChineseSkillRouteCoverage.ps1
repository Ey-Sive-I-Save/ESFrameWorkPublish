[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [string]$AliasPath='.agents/SKILL_ROUTE_ALIASES.zh-CN.json',
    [string]$ReportPath='ES/Output/Governance/chinese-skill-route-coverage.json'
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$aliasFull=Join-Path $root $AliasPath.Replace('/','\')
if(-not(Test-Path -LiteralPath $aliasFull -PathType Leaf)){throw "Chinese Skill route registry missing: $AliasPath"}
$registry=Get-Content -Raw -Encoding UTF8 $aliasFull|ConvertFrom-Json
$skills=@(Get-ChildItem (Join-Path $root '.agents/skills') -Directory|Sort-Object Name|ForEach-Object Name)
$findings=@(); $seen=@{}
foreach($name in $skills){
    $p=$registry.skills.PSObject.Properties[$name]
    if($null -eq $p){$findings += [pscustomobject]@{skill=$name;code='missing-zh-route';detail='Skill has no Chinese route aliases.'};continue}
    $aliases=@($p.Value|ForEach-Object {[string]$_}|Where-Object {$_})
    if($aliases.Count -lt 2){$findings += [pscustomobject]@{skill=$name;code='insufficient-zh-route';detail='Skill must expose at least two responsibility-specific Chinese aliases.'}}
    if(@($aliases|Where-Object {$_ -match '[\u4e00-\u9fff]'}).Count -eq 0){$findings += [pscustomobject]@{skill=$name;code='non-chinese-route';detail='Aliases contain no Chinese natural-language trigger.'}}
    foreach($alias in $aliases){$key=$alias.Trim();if($seen.ContainsKey($key)){$seen[$key]+=$name}else{$seen[$key]=@($name)}}
}
foreach($p in @($registry.skills.PSObject.Properties)){if($skills -notcontains $p.Name){$findings += [pscustomobject]@{skill=$p.Name;code='orphan-zh-route';detail='Chinese route entry does not resolve to a direct Skill.'}}}
$duplicates=@($seen.GetEnumerator()|Where-Object {$_.Value.Count -gt 1}|ForEach-Object {[pscustomobject]@{alias=$_.Key;skills=$_.Value -join ', ';code='ambiguous-zh-route'}})
$findings += $duplicates
$status=if(@($findings).Count -eq 0){'passed'}else{'blocked'}
$result=[ordered]@{schemaVersion=1;validator='es-chinese-skill-route-coverage';generatedUtc=[DateTime]::UtcNow.ToString('o');status=$status;locale='zh-CN';skillCount=$skills.Count;coveredSkillCount=($skills.Count-@($findings|Where-Object code -eq 'missing-zh-route').Count);findings=@($findings);aliasRegistry=$AliasPath;nextAction=if($status -eq 'passed'){'Every direct Skill has discoverable Chinese route aliases.'}else{'Add or disambiguate Chinese aliases; aliases never grant permission.'}}
$report=Join-Path $root $ReportPath.Replace('/','\');$parent=Split-Path -Parent $report;if(-not(Test-Path $parent)){New-Item -ItemType Directory $parent -Force|Out-Null};[IO.File]::WriteAllText($report,($result|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)));$result|ConvertTo-Json -Depth 8;if($status -ne 'passed'){exit 1};exit 0
