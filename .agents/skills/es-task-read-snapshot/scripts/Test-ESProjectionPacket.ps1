[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot='.',
    [Parameter(Mandatory=$true)][string]$PacketPath,
    [string]$SourcePath
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\','/')
if([IO.Path]::IsPathRooted($PacketPath)-or $PacketPath -match '(^|[\\/])\.\.([\\/]|$)'){throw 'PacketPath must be project-relative.'}
$packet=(Resolve-Path -LiteralPath (Join-Path $root ($PacketPath.Replace('/',[IO.Path]::DirectorySeparatorChar))) -ErrorAction Stop).Path
$utf8=[Text.UTF8Encoding]::new($false,$true); try{$raw=$utf8.GetString([IO.File]::ReadAllBytes($packet))}catch{throw 'Projection packet is not strict UTF-8.'}
try{$p=$raw|ConvertFrom-Json -ErrorAction Stop}catch{throw 'Projection packet is not valid JSON.'}
foreach($field in @('schemaVersion','sourcePath','sourceHash','parserId','parserVersion','projectionKind','generatedUtc','records')){if($null -eq $p.PSObject.Properties[$field]){throw "Projection field missing: $field"}}
if([int]$p.schemaVersion -ne 1){throw 'Projection schemaVersion must be 1.'}
if([string]$p.sourceHash -notmatch '^[0-9a-fA-F]{64}$'){throw 'sourceHash must be SHA-256.'}
if([string]$p.parserId -notmatch '^[A-Za-z0-9._-]{1,64}$'-or [string]$p.parserVersion -notmatch '^[A-Za-z0-9._-]{1,32}$'){throw 'Parser identity is invalid.'}
if([string]$p.projectionKind -notin @('binary-structure','text-index','asset-manifest','document-structure','custom')){throw 'projectionKind is invalid.'}
if($null -eq $p.records -or -not ($p.records -is [System.Collections.IEnumerable])){throw 'records must be an array.'}
if($SourcePath){if([IO.Path]::IsPathRooted($SourcePath)-or $SourcePath -match '(^|[\\/])\.\.([\\/]|$)'){throw 'SourcePath must be project-relative.'};$source=(Resolve-Path -LiteralPath (Join-Path $root ($SourcePath.Replace('/',[IO.Path]::DirectorySeparatorChar))) -ErrorAction Stop).Path;$sha=[Security.Cryptography.SHA256]::Create();try{$actual=([BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($source)))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()};if($actual -ne ([string]$p.sourceHash).ToLowerInvariant()){throw 'Projection sourceHash does not match SourcePath.'}}
[ordered]@{status='passed';packetPath=$PacketPath;sourcePath=$p.sourcePath;sourceHash=$p.sourceHash;parserId=$p.parserId;parserVersion=$p.parserVersion;projectionKind=$p.projectionKind;recordCount=@($p.records).Count}|ConvertTo-Json
