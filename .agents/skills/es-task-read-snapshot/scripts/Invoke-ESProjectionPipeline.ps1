[CmdletBinding()]
param(
 [ValidateSet('Resolve','Commit')][string]$Mode='Resolve',
 [string]$ProjectRoot='.',[Parameter(Mandatory=$true)][string]$SourcePath,
 [string]$ProjectionPath,[string]$RegistryPath='ES/Output/FileProjectionParsers.json',
 [string]$ParserId,[string]$ParserVersion
)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\','/')
function Assert-ProjectRelativePath([string]$Value,[string]$Name){if([string]::IsNullOrWhiteSpace($Value)-or[IO.Path]::IsPathRooted($Value)-or$Value-match '(^|[\\/])\.\.([\\/]|$)'-or$Value-match '[*?]'){throw "$Name must be project-relative and bounded."};$full=[IO.Path]::GetFullPath((Join-Path $root ($Value.Replace('/',[IO.Path]::DirectorySeparatorChar))));if(-not($full.Equals($root,[StringComparison]::OrdinalIgnoreCase)-or$full.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase))){throw "$Name escapes ProjectRoot."};$full}
$source=(Resolve-Path -LiteralPath (Assert-ProjectRelativePath $SourcePath 'SourcePath') -ErrorAction Stop).Path
$registryFull=Assert-ProjectRelativePath $RegistryPath 'RegistryPath'
$ext=[IO.Path]::GetExtension($source).ToLowerInvariant();$reg=& (Join-Path $PSScriptRoot 'Test-ESProjectionRegistry.ps1') -ProjectRoot $ProjectRoot -RegistryPath $RegistryPath|ConvertFrom-Json
$raw=Get-Content $registryFull -Raw -Encoding UTF8|ConvertFrom-Json
$candidates=@($raw.parsers|Where-Object{$_.extensions -contains $ext});if($ParserId){$candidates=@($candidates|Where-Object{$_.parserId -eq $ParserId})}
if($candidates.Count -ne 1){throw "Parser resolution requires exactly one registered parser for extension $ext."};$parser=$candidates[0];if($ParserVersion -and $ParserVersion -ne $parser.parserVersion){throw 'Requested ParserVersion does not match registry.'}
if($Mode -eq 'Resolve'){[ordered]@{status='resolved';sourcePath=$SourcePath;extension=$ext;parserId=$parser.parserId;parserVersion=$parser.parserVersion;projectionKind=$parser.projectionKind;execution='external-authorized';next='Run the authorized parser, then Commit its ProjectionPacket.'}|ConvertTo-Json;exit 0}
if([string]::IsNullOrWhiteSpace($ProjectionPath)){throw 'ProjectionPath is required for Commit.'}
$packet=& (Join-Path $PSScriptRoot 'Test-ESProjectionPacket.ps1') -ProjectRoot $ProjectRoot -PacketPath $ProjectionPath -SourcePath $SourcePath|ConvertFrom-Json
if($packet.parserId -ne $parser.parserId -or $packet.parserVersion -ne $parser.parserVersion -or $packet.projectionKind -ne $parser.projectionKind){throw 'ProjectionPacket does not match resolved parser registry entry.'}
$cache=& (Join-Path $PSScriptRoot 'Invoke-ESProjectionCache.ps1') -Mode Write -ProjectRoot $ProjectRoot -SourcePath $SourcePath -ParserId $parser.parserId -ParserVersion $parser.parserVersion -ProjectionPath $ProjectionPath|ConvertFrom-Json
[ordered]@{status='committed';sourcePath=$SourcePath;parserId=$parser.parserId;parserVersion=$parser.parserVersion;projectionKind=$parser.projectionKind;cacheKey=$cache.key;projectionPath=$cache.projectionPath}|ConvertTo-Json
