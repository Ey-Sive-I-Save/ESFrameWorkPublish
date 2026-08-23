[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot,[Parameter(Mandatory=$true)][string]$GraphPath)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path;if([IO.Path]::IsPathRooted($GraphPath)){throw 'GraphPath must be project-relative.'}
$rel=$GraphPath.Replace('\','/').Trim();if($rel.Contains('..')-or $rel -notmatch '^ES/Output/.+\.json$'){throw 'GraphPath must remain under ES/Output.'};$full=Join-Path $root ($rel.Replace('/',[IO.Path]::DirectorySeparatorChar));if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw "Dependency graph missing: $rel"}
$g=([Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($full))|ConvertFrom-Json);if([string]$g.schemaVersion -ne '1'-or @($g.nodes).Count -eq 0){throw 'Dependency graph requires schemaVersion and nodes.'};$paths=@{}
foreach($n in @($g.nodes)){foreach($p in @('path','kind','owner','authority','hash')){if([string]::IsNullOrWhiteSpace([string]$n.$p)){throw "Dependency node missing ${p}."}};if($paths.ContainsKey([string]$n.path)){throw "Duplicate dependency node: $($n.path)"};$paths[[string]$n.path]=$true}
foreach($e in @($g.edges)){foreach($p in @('from','to','direction','reason','allowed','evidenceRef')){if([string]::IsNullOrWhiteSpace([string]$e.$p)){throw "Dependency edge missing ${p}."}};if(-not $paths.ContainsKey([string]$e.from)-or -not $paths.ContainsKey([string]$e.to)){throw 'Dependency edge references unknown node.'}}
Write-Output "PASS: dependency graph is bounded and replayable: $rel"
