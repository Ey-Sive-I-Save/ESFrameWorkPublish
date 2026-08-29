[CmdletBinding()]
param([string]$ReportPath='ES/Output/StaticReplay/task-focus-context.json')
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$tests=@('Test-ESTaskFocusContext.ps1','Test-ESTaskFocusDefaultActivation.ps1','Test-ESTaskFocusRuntimeAdapter.ps1','Test-ESTaskFocusRuntimeIntegration.ps1')
$results=@()
foreach($name in $tests){
  $path=Join-Path $PSScriptRoot $name
  $raw=& $path 2>&1 | Out-String
  $jsonLine=($raw -split "`r?`n" | Where-Object {$_.TrimStart().StartsWith('{') -or $_.TrimStart().StartsWith('[')} | Select-Object -Last 1)
  try{$result=$raw|ConvertFrom-Json}catch{$result=[pscustomobject]@{status='failed';error=$raw.Trim()}}
  $results+= [pscustomobject]@{test=$name;status=[string]$result.status;caseCount=$result.caseCount;passedCount=$result.passedCount;failedCount=$result.failedCount}
}
$failed=@($results|Where-Object status -ne 'passed')
$report=[pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESTaskFocusStaticReplay';status=$(if($failed.Count){'failed'}else{'passed'});phase='completed';mutatesSources=$false;startsRuntime=$false;tests=@($results);runtimeStatus='runtime-not-run';claimsNotProven=@('Unity/host runtime','external framework runtime','production release')}
$full=[IO.Path]::GetFullPath((Join-Path $root $ReportPath))
$rootPrefix=([IO.Path]::GetFullPath($root)).TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if(-not $full.StartsWith($rootPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'ReportPath must remain within the project root.'}
$parent=Split-Path -Parent $full
if(-not(Test-Path -LiteralPath $parent)){New-Item -ItemType Directory -Path $parent -Force|Out-Null}
[IO.File]::WriteAllText($full,($report|ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false))
$report|ConvertTo-Json -Depth 20
if($failed.Count){exit 1}
