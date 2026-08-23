[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot,[Parameter(Mandatory=$true)][string]$RunRecordPath)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path;if([IO.Path]::IsPathRooted($RunRecordPath)){throw 'RunRecordPath must be project-relative.'}
$rel=$RunRecordPath.Replace('\','/').Trim();if($rel.Contains('..')-or $rel -notmatch '^ES/Output/.+\.json$'){throw 'RunRecordPath must remain under ES/Output.'};$full=Join-Path $root ($rel.Replace('/',[IO.Path]::DirectorySeparatorChar));if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw "RunRecord missing: $rel"}
$r=([Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($full))|ConvertFrom-Json);foreach($p in @('schemaVersion','taskId','planHash','status','recovery')){if([string]::IsNullOrWhiteSpace([string]$r.$p)){throw "RunRecord missing ${p}."}};if([string]$r.status -notin @('passed','failed','blocked','cancelled','not-run')){throw 'RunRecord status is invalid.'};if(@($r.sourceRefs).Count -eq 0){throw 'RunRecord requires sourceRefs.'}
Write-Output "PASS: observable RunRecord is replayable: $rel"
