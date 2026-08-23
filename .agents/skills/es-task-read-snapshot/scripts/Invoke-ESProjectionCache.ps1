[CmdletBinding()]
param(
    [ValidateSet('Read','Write','Invalidate')][string]$Mode='Read',
    [string]$ProjectRoot='.',
    [Parameter(Mandatory=$true)][string]$SourcePath,
    [Parameter(Mandatory=$true)][string]$ParserId,
    [Parameter(Mandatory=$true)][string]$ParserVersion,
    [string]$ProjectionPath,
    [string]$CacheRoot='ES/Output/FileProjectionCache'
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\','/')
function Assert-ProjectRelativePath([string]$Value,[string]$Name) {
    if([string]::IsNullOrWhiteSpace($Value) -or [IO.Path]::IsPathRooted($Value) -or $Value -match '(^|[\\/])\.\.([\\/]|$)' -or $Value -match '[*?]'){ throw "$Name must be project-relative and bounded." }
    $full=[IO.Path]::GetFullPath((Join-Path $root ($Value.Replace('/',[IO.Path]::DirectorySeparatorChar))))
    if(-not ($full.Equals($root,[StringComparison]::OrdinalIgnoreCase) -or $full.StartsWith($root + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase))){ throw "$Name escapes ProjectRoot." }
    return $full
}
if($ParserId -notmatch '^[A-Za-z0-9._-]{1,64}$' -or $ParserVersion -notmatch '^[A-Za-z0-9._-]{1,32}$'){throw 'ParserId or ParserVersion is unsafe.'}
$source=(Resolve-Path -LiteralPath (Assert-ProjectRelativePath $SourcePath 'SourcePath') -ErrorAction Stop).Path
$item=Get-Item -LiteralPath $source; if($item.LinkType){throw 'Reparse-point source is denied.'}
$sha=[Security.Cryptography.SHA256]::Create(); try{$sourceHash=([BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($source)))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
$keyInput="$($SourcePath.Replace('\','/'))|$sourceHash|$ParserId|$ParserVersion"; $sha=[Security.Cryptography.SHA256]::Create(); try{$key=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($keyInput)))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
$cacheDir=Assert-ProjectRelativePath $CacheRoot 'CacheRoot'; $manifest=Join-Path $cacheDir "$key.json"; $artifact=Join-Path $cacheDir "$key.projection"
if($Mode -eq 'Invalidate'){if(Test-Path $manifest){Remove-Item -LiteralPath $manifest -Force};if(Test-Path $artifact){Remove-Item -LiteralPath $artifact -Force};[ordered]@{status='invalidated';key=$key;sourcePath=$SourcePath}|ConvertTo-Json; exit 0}
if($Mode -eq 'Read'){
    if(-not(Test-Path $manifest -PathType Leaf)-or -not(Test-Path $artifact -PathType Leaf)){[ordered]@{status='miss';key=$key;sourcePath=$SourcePath;reason='projection-not-cached'}|ConvertTo-Json;exit 2}
    try{$m=Get-Content $manifest -Raw -Encoding UTF8|ConvertFrom-Json -ErrorAction Stop}catch{throw 'Projection cache manifest is corrupt.'}
    if($m.sourceHash -ne $sourceHash-or $m.parserId -ne $ParserId-or $m.parserVersion -ne $ParserVersion){[ordered]@{status='stale';key=$key;sourcePath=$SourcePath;reason='source-or-parser-drift'}|ConvertTo-Json;exit 2}
    $sha=[Security.Cryptography.SHA256]::Create();try{$actualProjectionHash=([BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($artifact)))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
    if($m.projectionHash -ne $actualProjectionHash){[ordered]@{status='corrupt';key=$key;sourcePath=$SourcePath;reason='projection-integrity-mismatch'}|ConvertTo-Json;exit 2}
    [ordered]@{status='hit';key=$key;sourcePath=$SourcePath;projectionPath=$artifact;sourceHash=$sourceHash;projectionHash=$actualProjectionHash;parserId=$ParserId;parserVersion=$ParserVersion}|ConvertTo-Json; exit 0
}
if([string]::IsNullOrWhiteSpace($ProjectionPath)){throw 'ProjectionPath is required for Write.'}
$projection=(Resolve-Path -LiteralPath (Assert-ProjectRelativePath $ProjectionPath 'ProjectionPath') -ErrorAction Stop).Path
$bytes=[IO.File]::ReadAllBytes($projection); if($bytes.Length -gt 100MB){throw 'Projection exceeds 100 MB bound.'}; $null=([Text.UTF8Encoding]::new($false,$true)).GetString($bytes)|ConvertFrom-Json
New-Item -ItemType Directory -Force -Path $cacheDir|Out-Null
$tmp="$artifact.$([Guid]::NewGuid().ToString('N')).tmp"; try{[IO.File]::WriteAllBytes($tmp,$bytes);Move-Item $tmp $artifact -Force}finally{if(Test-Path $tmp){Remove-Item $tmp -Force -ErrorAction SilentlyContinue}}
$m=[ordered]@{schemaVersion=1;key=$key;sourcePath=$SourcePath.Replace('\','/');sourceHash=$sourceHash;parserId=$ParserId;parserVersion=$ParserVersion;projectionPath=$artifact.Substring($root.Length).TrimStart([char]92,[char]47).Replace([string][char]92,'/');createdUtc=[DateTime]::UtcNow.ToString('o')}
$sha=[Security.Cryptography.SHA256]::Create();try{$projectionHash=([BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($artifact)))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
$m.projectionHash=$projectionHash
$manifestTmp="$manifest.$([Guid]::NewGuid().ToString('N')).tmp";try{[IO.File]::WriteAllText($manifestTmp,($m|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)));Move-Item $manifestTmp $manifest -Force}finally{if(Test-Path $manifestTmp){Remove-Item $manifestTmp -Force -ErrorAction SilentlyContinue}}
$m.status='stored';$m|ConvertTo-Json
