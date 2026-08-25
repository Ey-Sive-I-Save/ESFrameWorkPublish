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
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$limits=@{skill=3;knowledge=3;aiwarnings=3}
$kindFor={param($p) if($p -match '(?i)\.agents[\\/]skills[\\/]'){ 'skill' } elseif($p -match '(?i)Documentation[\\/]AIKnowledge[\\/]'){ 'knowledge' } elseif($p -match '(?i)Assets[\\/]Plugins[\\/]ES[\\/]AIWarnings[\\/]'){ 'aiwarnings' } else { 'other' }}
$seen=@{};$items=@();$counts=@{skill=0;knowledge=0;aiwarnings=0}
foreach($raw in @($ReadPaths)){
  $full=[IO.Path]::GetFullPath((Join-Path $root $raw))
  if(-not $full.StartsWith($root + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw "read path escapes project root: $raw"}
  if(-not (Test-Path -LiteralPath $full -PathType Leaf)){throw "read path is missing: $raw"}
  $relative=$full.Substring($root.Length+1).Replace('\','/')
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
$target=[IO.Path]::GetFullPath((Join-Path $root $OutputPath));$dir=Split-Path $target -Parent;if(-not $dir.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'output path escapes project root'};New-Item -ItemType Directory -Path $dir -Force|Out-Null;[IO.File]::WriteAllText($target,($out|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)));$out|ConvertTo-Json -Depth 8
