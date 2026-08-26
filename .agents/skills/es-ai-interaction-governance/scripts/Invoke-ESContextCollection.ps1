[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$ProjectRoot,
  [Parameter(Mandatory=$true)][string]$TaskKey,
  [Parameter(Mandatory=$true)][ValidatePattern('^[0-9a-fA-F]{64}$')][string]$PlanHash,
  [Parameter(Mandatory=$true)][ValidateSet('skill-only','knowledge-only','aiwarnings-only','skill-knowledge','skill-knowledge-aiwarnings')][string]$Selection,
  [Parameter(Mandatory=$true)][string[]]$ReadPaths,
  [string]$OutputPath='ES/Output/Interaction/context-collection-receipt.json'
)
$ErrorActionPreference='Stop'
$pathPolicy=(Resolve-Path (Join-Path $PSScriptRoot 'ESInteractionPathPolicy.ps1')).Path
. $pathPolicy
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$expectedRoot=Get-ESInteractionProjectRoot
if(-not $root.Equals($expectedRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'ProjectRoot must match the current ESFramework project root.'}
$limits=@{skill=3;knowledge=3;aiwarnings=3}
$kindFor={param($p) if($p -match '(?i)\.agents[\\/]skills[\\/]'){ 'skill' } elseif($p -match '(?i)Documentation[\\/]AIKnowledge[\\/]'){ 'knowledge' } elseif($p -match '(?i)Assets[\\/]Plugins[\\/]ES[\\/]AIWarnings[\\/]'){ 'aiwarnings' } else { 'other' }}
$seen=@{};$items=@();$counts=@{skill=0;knowledge=0;aiwarnings=0}
foreach($raw in @($ReadPaths)){
  $resolved=Resolve-ESContainedRelativePath -Candidate $raw -ContainerRoot $root -Label 'ReadPath'
  $full=$resolved.FullPath
  if(-not (Test-Path -LiteralPath $full -PathType Leaf)){throw "read path is missing: $raw"}
  $relative=$resolved.RelativePath
  if($seen.ContainsKey($relative)){throw "duplicate read path: $relative"};$seen[$relative]=$true
  $kind=& $kindFor $relative
  if($kind -eq 'other'){throw "read path is outside Skill/Knowledge/AIWarnings scopes: $relative"}
  $counts[$kind]++
  if($counts[$kind] -gt $limits[$kind]){throw "read limit exceeded for $kind"}
  $h=(Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant()
  $items+=,[ordered]@{path=$relative;kind=$kind;sha256=$h;length=(Get-Item -LiteralPath $full).Length}
}
$selectedKinds=@{ 'skill-only'=@('skill'); 'knowledge-only'=@('knowledge'); 'aiwarnings-only'=@('aiwarnings'); 'skill-knowledge'=@('skill','knowledge'); 'skill-knowledge-aiwarnings'=@('skill','knowledge','aiwarnings') }[$Selection]
foreach($item in @($items)){if($selectedKinds -notcontains $item.kind){throw "selection does not permit read kind: $($item.kind)"}}
$canonical=($items|ForEach-Object{"$($_.kind)|$($_.path)|$($_.sha256)"}|Sort-Object)-join "`n"
$sha=[Security.Cryptography.SHA256]::Create();$readSetHash=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical)))).Replace('-','').ToLowerInvariant()
$out=[ordered]@{schemaVersion=1;toolId='es-context-collection';taskKey=$TaskKey;planHash=$PlanHash.ToLowerInvariant();selection=$Selection;mode='read-only-bounded';limits=$limits;readSet=@($items);readSetHash=$readSetHash;stale=@();nonClaims=@('No route authority was inferred by this executor','No writes, Runtime, network, or external process was executed','ReadSet completeness is not proven without an upstream route decision');decision='collected';runtimeStatus='runtime-not-run';generatedUtc=[DateTime]::UtcNow.ToString('o')}
$target=Resolve-ESInteractionReportPath -Candidate $OutputPath -Label 'OutputPath';$dir=Split-Path $target -Parent;New-Item -ItemType Directory -Path $dir -Force|Out-Null;$target=Resolve-ESInteractionReportPath -Candidate $target -Label 'OutputPath';[IO.File]::WriteAllText($target,($out|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)));$out|ConvertTo-Json -Depth 8
