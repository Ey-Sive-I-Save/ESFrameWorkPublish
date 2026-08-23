[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [string]$RouteId = 'runtime-hot-container'
)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
if([string]::IsNullOrWhiteSpace($RouteId) -or $RouteId -match '[\\/:\s]'){throw 'RouteId must be a single route token.'}
$catalog=(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/Plugins/ES/AIWarnings') -Recurse -File -Filter 'AIWarningsRouteCatalog.json' | Select-Object -First 1).FullName
if(-not(Test-Path -LiteralPath $catalog -PathType Leaf)){throw 'AIWarnings route catalog is missing.'}
$strict=[Text.UTF8Encoding]::new($false,$true);$json=$strict.GetString([IO.File]::ReadAllBytes($catalog))|ConvertFrom-Json
if($json.schemaVersion -ne 1 -or $null -eq $json.routes){throw 'AIWarnings route catalog schema is invalid.'}
$ids=@{};$selected=$null
foreach($route in $json.routes){$id=[string]$route.id;if([string]::IsNullOrWhiteSpace($id)-or $ids.ContainsKey($id)){throw "Duplicate or empty AIWarnings route id: $id"};$ids[$id]=$true;if([string]$route.state -notin @('current','reserved')){throw "Unsupported route state: $id"};if(@($route.mustRead).Count -eq 0){throw "Route has no mustRead paths: $id"};foreach($path in @($route.mustRead)){$p=[string]$path;if([IO.Path]::IsPathRooted($p)-or $p -notmatch '^Assets/Plugins/ES/AIWarnings/' -or $p.Contains('..')){throw "Route escapes AIWarnings root: $id"};$full=Join-Path $root ($p.Replace('/',[IO.Path]::DirectorySeparatorChar));if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw "Route mustRead missing: $p"}};if($id -eq $RouteId){$selected=$route}}
if($null -eq $selected){throw "Unknown AIWarnings route: $RouteId"}
Write-Output "PASS: AIWarnings route is bounded and readable: $RouteId"
