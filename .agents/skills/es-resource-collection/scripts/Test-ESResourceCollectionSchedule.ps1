[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$JsonPath,
    [string]$SchemaPath=(Join-Path $PSScriptRoot '..\references\es-resource-collection-schedule.v1.schema.json')
)
$ErrorActionPreference='Stop'
$errors=[Collections.Generic.List[string]]::new()
try {
    $full=(Resolve-Path -LiteralPath $JsonPath).Path
    $j=Get-Content -LiteralPath $full -Raw -Encoding UTF8 | ConvertFrom-Json
    if($j.schemaVersion -ne 1){[void]$errors.Add('schemaVersion must be 1')}
    if($null -eq $j.packageId){[void]$errors.Add('missing packageId')}
    if($null -eq $j.autoParallel){[void]$errors.Add('missing autoParallel')}
    if($j.maxFiles -lt 1 -or $j.maxFiles -gt 100000){[void]$errors.Add('maxFiles out of range')}
    if($j.maxParallel -lt 1 -or $j.maxParallel -gt 32){[void]$errors.Add('maxParallel out of range')}
    if($j.maxFileSizeMb -lt 1 -or $j.maxFileSizeMb -gt 1024){[void]$errors.Add('maxFileSizeMb out of range')}
    $raw=(Get-Content -LiteralPath $full -Raw -Encoding UTF8).TrimStart([char]0xFEFF)
    if($raw -match '[\uFFFD]'){[void]$errors.Add('replacement character detected')}
} catch { [void]$errors.Add($_.Exception.Message) }
[ordered]@{validator='Test-ESResourceCollectionSchedule';path=$JsonPath;schemaPath=$SchemaPath;valid=($errors.Count -eq 0);errorCount=$errors.Count;errors=@($errors);runtimeStatus='runtime-not-run'}|ConvertTo-Json -Depth 8
if($errors.Count){exit 1}
