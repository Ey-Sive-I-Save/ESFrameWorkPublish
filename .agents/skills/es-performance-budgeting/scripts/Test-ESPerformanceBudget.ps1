[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot,[Parameter(Mandatory=$true)][string]$BudgetPath)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
if([IO.Path]::IsPathRooted($BudgetPath)){throw 'BudgetPath must be project-relative.'}
$relative=$BudgetPath.Replace('\','/').Trim()
if($relative.Contains('..')-or $relative -notmatch '^ES/Output/.+\.json$'){throw 'BudgetPath must remain under ES/Output.'}
$full=Join-Path $root ($relative.Replace('/',[IO.Path]::DirectorySeparatorChar))
if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw "Performance budget missing: $relative"}
$outputRoot=(Resolve-Path -LiteralPath (Join-Path $root 'ES\Output')).Path.TrimEnd('\','/')
$resolved=(Resolve-Path -LiteralPath $full).Path
if(-not $resolved.StartsWith("$outputRoot$([IO.Path]::DirectorySeparatorChar)",[StringComparison]::OrdinalIgnoreCase)){throw 'Resolved budget path escapes ES/Output.'}
$budget=[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($resolved))|ConvertFrom-Json
foreach($property in @('schemaVersion','platform','scenario','evidenceLevel','declaredOutcome','metrics')){if($null -eq $budget.PSObject.Properties[$property]){throw "Missing budget field: $property"}}
if([string]$budget.schemaVersion -ne '1'-or [string]$budget.evidenceLevel -notmatch '^S[0-6]$'){throw 'Invalid schemaVersion or evidenceLevel.'}
if([string]$budget.declaredOutcome -notmatch '^(designed|measured|blocked)$'){throw 'Invalid declaredOutcome.'}
if(@($budget.metrics).Count -eq 0){throw 'At least one metric is required.'}
$names=@{}
foreach($metric in @($budget.metrics)){
  foreach($property in @('metric','unit','threshold','comparator','phase','baseline','inputSize','warmup','measurementArtifact','owner','staleWhen')){if([string]::IsNullOrWhiteSpace([string]$metric.$property)){throw "Metric missing $property."}}
  if([string]$metric.comparator -notmatch '^(lt|lte|gt|gte|eq)$'){throw "Invalid comparator: $($metric.comparator)"}
  if([string]$metric.phase -notmatch '^(first-run|steady-state|peak)$'){throw "Invalid phase: $($metric.phase)"}
  if($names.ContainsKey([string]$metric.metric)){throw "Duplicate metric: $($metric.metric)"};$names[[string]$metric.metric]=$true
  if([string]$budget.evidenceLevel -match '^S[5-6]$'-and [string]$metric.measurementArtifact -eq 'not-run'){throw 'S5/S6 performance evidence requires a measurement artifact.'}
}
if([string]$budget.declaredOutcome -eq 'measured'-and @($budget.metrics|Where-Object{[string]$_.measurementArtifact -eq 'not-run'}).Count -gt 0){throw 'A measured outcome cannot contain not-run artifacts.'}
Write-Output "PASS: performance budget has explicit thresholds and honest evidence boundaries: $relative"
