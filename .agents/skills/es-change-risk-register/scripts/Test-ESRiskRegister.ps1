[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot,[Parameter(Mandatory=$true)][string]$RegisterPath)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path;if([IO.Path]::IsPathRooted($RegisterPath)){throw 'RegisterPath must be project-relative.'}
$rel=$RegisterPath.Replace('\','/').Trim();if($rel.Contains('..')-or $rel -notmatch '^ES/Output/.+\.json$'){throw 'RegisterPath must remain under ES/Output.'};$full=Join-Path $root ($rel.Replace('/',[IO.Path]::DirectorySeparatorChar));if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw "Risk register missing: $rel"}
$r=([Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($full))|ConvertFrom-Json);if([string]$r.schemaVersion -ne '1'-or [string]::IsNullOrWhiteSpace($r.owner)-or @($r.risks).Count -eq 0){throw 'Risk register requires schemaVersion, owner and risks.'}
foreach($risk in @($r.risks)){foreach($p in @('id','owner','riskClass','mitigation','detection','rollback','status')){if([string]::IsNullOrWhiteSpace([string]$risk.$p)){throw "Risk row missing ${p}: $($risk.id)"}};if([string]$risk.status -notin @('open','mitigated','blocked','accepted')){throw "Invalid risk status: $($risk.id)"}}
Write-Output "PASS: bounded risk register is replayable: $rel"
