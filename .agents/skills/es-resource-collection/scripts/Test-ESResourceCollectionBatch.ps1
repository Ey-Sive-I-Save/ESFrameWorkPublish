[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$JsonPath,
    [string]$SchemaPath = (Join-Path $PSScriptRoot '..\references\es-resource-collection-batch.v1.schema.json')
)
$ErrorActionPreference='Stop';$errors=[Collections.Generic.List[string]]::new()
try {
    $full=(Resolve-Path -LiteralPath $JsonPath).Path
    $raw=[IO.File]::ReadAllText($full,[Text.UTF8Encoding]::new($false,$true));$j=$raw|ConvertFrom-Json
    if ($j.batchId -ne 'es-resource-collection.batch.v1') {[void]$errors.Add('invalid batchId')}
    $files=@($j.files);if ($j.fileCount -ne $files.Count){[void]$errors.Add('fileCount mismatch')}
    if ($j.reusedCount -lt 0 -or $j.parsedCount -lt 0 -or $j.failedCount -lt 0 -or $j.reusedCount+$j.parsedCount+$j.failedCount -ne $files.Count){[void]$errors.Add('status counts mismatch')}
    if ($j.incrementalHitRate -lt 0 -or $j.incrementalHitRate -gt 1){[void]$errors.Add('incrementalHitRate out of range')}
    if ($j.effectiveParallel -lt 1 -or $j.effectiveParallel -gt $j.maxParallel){[void]$errors.Add('effectiveParallel out of range')}
    if ($null -ne $j.totalBytes -and $j.totalBytes -lt 0){[void]$errors.Add('totalBytes out of range')}
    if ($null -ne $j.parallelReason -and [string]::IsNullOrWhiteSpace([string]$j.parallelReason)){[void]$errors.Add('parallelReason must not be empty')}
    if ($j.elapsedMilliseconds -lt 0 -or $j.filesPerSecond -lt 0){[void]$errors.Add('invalid performance metrics')}
    $seen=@{};$paths=@();foreach($f in $files){$p=[string]$f.path;$paths+=$p;if($seen.ContainsKey($p)){[void]$errors.Add("duplicate path: $p")}else{$seen[$p]=$true};if([IO.Path]::IsPathRooted($p) -or $p -match '(^|[\\/])\.\.([\\/]|$)' -or $p -match '^[A-Za-z]:'){[void]$errors.Add("non-canonical path: $p")};if([string]$f.sha256 -notmatch '^[0-9a-f]{64}$'){[void]$errors.Add("invalid sha256: $p")};if(@('parsed','reused','failed') -notcontains [string]$f.status){[void]$errors.Add("invalid status: $p")}}
    $sorted=@($paths|Sort-Object);if((ConvertTo-Json $paths -Compress) -ne (ConvertTo-Json $sorted -Compress)){[void]$errors.Add('files must be sorted by path')}
} catch {[void]$errors.Add($_.Exception.Message)}
[ordered]@{validator='Test-ESResourceCollectionBatch';path=$JsonPath;schemaPath=$SchemaPath;valid=($errors.Count -eq 0);errorCount=$errors.Count;errors=@($errors);runtimeStatus='runtime-not-run'}|ConvertTo-Json -Depth 8
if($errors.Count){exit 1};exit 0
