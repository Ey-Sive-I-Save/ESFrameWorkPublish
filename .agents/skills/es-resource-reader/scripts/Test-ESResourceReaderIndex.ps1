[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$IndexPath,[string]$SchemaPath=(Join-Path $PSScriptRoot '..\references\resource-index.v1.schema.json'))
$ErrorActionPreference='Stop'; $issues=@();
if(-not(Test-Path -LiteralPath $IndexPath -PathType Leaf)){throw "Index not found: $IndexPath"}
if(-not(Test-Path -LiteralPath $SchemaPath -PathType Leaf)){throw "Schema not found: $SchemaPath"}
try{$index=Get-Content -LiteralPath $IndexPath -Raw -Encoding UTF8|ConvertFrom-Json -ErrorAction Stop}catch{throw 'Index JSON is invalid.'}
$schema=Get-Content -LiteralPath $SchemaPath -Raw -Encoding UTF8|ConvertFrom-Json -ErrorAction Stop
if($index.schemaVersion -ne 1){$issues+='schemaVersion must be 1'}; if($index.indexId -ne 'es-resource-reader.resource-index.v1'){$issues+='indexId mismatch'}
$items=@($index.items); if($index.fileCount -ne $items.Count){$issues+='fileCount mismatch'}; $paths=@{}; foreach($item in $items){$path=[string]$item.sourcePath;if([IO.Path]::IsPathRooted($path)-or $path -match '(^|[\\/])\.\.([\\/]|$)'){$issues+="unsafe sourcePath: $path"};if($paths.ContainsKey($path)){$issues+="duplicate sourcePath: $path"};$paths[$path]=$true;if([string]$item.sourceSha256 -notmatch '^[a-f0-9]{64}$'){$issues+="invalid sourceSha256: $path"};if([string]$item.status -notin @('ready','error')){$issues+="invalid status: $path"};foreach($n in @('byteCount','entryCount','objectCount','dependencyCount','warningCount','errorCount')){if([int64]$item.$n -lt 0){$issues+="negative ${n}: $path"}}}
$result=[ordered]@{validator='Test-ESResourceReaderIndex';indexPath=(Resolve-Path $IndexPath).Path;schemaPath=(Resolve-Path $SchemaPath).Path;valid=($issues.Count -eq 0);itemCount=$items.Count;issues=$issues;runtimeStatus='runtime-not-run'};$result|ConvertTo-Json -Depth 8;if($issues.Count -gt 0){exit 1}
