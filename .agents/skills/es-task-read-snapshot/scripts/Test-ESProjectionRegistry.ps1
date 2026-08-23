[CmdletBinding()]
param([string]$ProjectRoot='.',[string]$RegistryPath='ES/Output/FileProjectionParsers.json')
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
if([IO.Path]::IsPathRooted($RegistryPath)-or $RegistryPath -match '(^|[\\/])\.\.([\\/]|$)'){throw 'RegistryPath must be project-relative.'}
$full=(Resolve-Path -LiteralPath (Join-Path $root ($RegistryPath.Replace('/',[IO.Path]::DirectorySeparatorChar))) -ErrorAction Stop).Path
$p=Get-Content $full -Raw -Encoding UTF8|ConvertFrom-Json
if([int]$p.schemaVersion -ne 1 -or [string]$p.registryId -ne 'es-file-projection-parsers.v1'){throw 'Invalid projection registry identity.'}
$ids=@{};foreach($x in @($p.parsers)){foreach($f in 'parserId','parserVersion','extensions','projectionKind','mode'){if($null -eq $x.PSObject.Properties[$f]){throw "Parser field missing: $f"}};if($ids.ContainsKey([string]$x.parserId)){throw "Duplicate parserId: $($x.parserId)"};$ids[[string]$x.parserId]=$true;if([string]$x.mode -ne 'external-authorized'){throw 'Registry may only declare external-authorized parsers.'};if(@($x.extensions).Count -eq 0){throw "Parser has no extensions: $($x.parserId)"}}
[ordered]@{status='passed';registryPath=$RegistryPath;parserCount=@($p.parsers).Count}|ConvertTo-Json
