[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot,[Parameter(Mandatory=$true)][string]$PacketPath)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path;if([IO.Path]::IsPathRooted($PacketPath)){throw 'PacketPath must be project-relative.'}
$rel=$PacketPath.Replace('\','/').Trim();if($rel.Contains('..')-or $rel -notmatch '^ES/Output/.+\.json$'){throw 'PacketPath must remain under ES/Output.'};$full=Join-Path $root ($rel.Replace('/',[IO.Path]::DirectorySeparatorChar));if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw "Analysis packet missing: $rel"}
$p=([Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($full))|ConvertFrom-Json);foreach($f in @('schemaVersion','intent','invariants','assumptions','options','evidenceGaps')){if([string]::IsNullOrWhiteSpace([string]$p.$f)-and @($p.$f).Count -eq 0){throw "Analysis packet missing ${f}."}};if([string]$p.schemaVersion -ne '1'){throw 'Analysis packet schemaVersion must be 1.'}
Write-Output "PASS: first-principles analysis packet is bounded: $rel"
