[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot,[Parameter(Mandatory=$true)][string]$MatrixPath)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path;if([IO.Path]::IsPathRooted($MatrixPath)){throw 'MatrixPath must be project-relative.'}
$rel=$MatrixPath.Replace('\','/').Trim();if($rel.Contains('..')-or $rel -notmatch '^ES/Output/.+\.json$'){throw 'MatrixPath must remain under ES/Output.'};$full=Join-Path $root ($rel.Replace('/',[IO.Path]::DirectorySeparatorChar));if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw "API matrix missing: $rel"}
$m=([Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($full))|ConvertFrom-Json);if([string]$m.schemaVersion -ne '1'-or @($m.contracts).Count -eq 0){throw 'API matrix requires schemaVersion and contracts.'}
foreach($c in @($m.contracts)){foreach($p in @('id','owner','input','output','lifecycle','compatibility','risk')){if([string]::IsNullOrWhiteSpace([string]$c.$p)){throw "API contract missing ${p}: $($c.id)"}};if([string]$c.risk -notin @('low','medium','high')){throw "Invalid API risk: $($c.id)"}}
Write-Output "PASS: API contract matrix is bounded and replayable: $rel"
