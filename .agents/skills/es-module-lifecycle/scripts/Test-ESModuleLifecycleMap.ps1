[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot,[Parameter(Mandatory=$true)][string]$MapPath)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path;if([IO.Path]::IsPathRooted($MapPath)){throw 'MapPath must be project-relative.'}
$rel=$MapPath.Replace('\','/').Trim();if($rel.Contains('..')-or $rel -notmatch '^ES/Output/.+\.json$'){throw 'MapPath must remain under ES/Output.'};$full=Join-Path $root ($rel.Replace('/',[IO.Path]::DirectorySeparatorChar));if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw "Lifecycle map missing: $rel"}
$m=([Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($full))|ConvertFrom-Json);if([string]$m.schemaVersion -ne '1'-or [string]::IsNullOrWhiteSpace($m.moduleId)-or @($m.states).Count -lt 2){throw 'Lifecycle map requires schemaVersion, moduleId and states.'};$seen=@{}
foreach($s in @($m.states)){if([string]::IsNullOrWhiteSpace([string]$s.id)-or $seen.ContainsKey([string]$s.id)){throw 'Lifecycle state id is empty or duplicated.'};$seen[[string]$s.id]=$true;if([string]::IsNullOrWhiteSpace([string]$s.onEnter)-or [string]::IsNullOrWhiteSpace([string]$s.onExit)){throw "Lifecycle state lacks enter/exit: $($s.id)"}}
if([string]::IsNullOrWhiteSpace([string]$m.recovery)){throw 'Lifecycle map requires recovery.'}
Write-Output "PASS: module lifecycle map is bounded and recoverable: $rel"
