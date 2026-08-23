[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot,[Parameter(Mandatory=$true)][string]$MatrixPath)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
if([IO.Path]::IsPathRooted($MatrixPath)){throw 'MatrixPath must be project-relative.'}
$rel=$MatrixPath.Replace('\','/').Trim();if($rel.Contains('..') -or $rel -notmatch '^ES/Output/.+\.json$'){throw 'MatrixPath must remain under ES/Output.'}
$full=Join-Path $root ($rel.Replace('/',[IO.Path]::DirectorySeparatorChar));if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw "Acceptance matrix missing: $rel"}
$strict=[Text.UTF8Encoding]::new($false,$true);$m=$strict.GetString([IO.File]::ReadAllBytes($full))|ConvertFrom-Json
if([string]$m.schemaVersion -ne '1' -or [string]$m.decision -notin @('approved','conditional','blocked')){throw 'Acceptance matrix schema or decision is invalid.'}
if(@($m.rows).Count -eq 0){throw 'Acceptance matrix must contain evidence rows.'}
$allowed=@('passed','failed','blocked','not-required','not-run');foreach($row in @($m.rows)){if([string]$row.name -eq '' -or [string]$row.status -notin $allowed){throw 'Acceptance row has invalid name or status.'}}
if(@($m.rows|?{$_.status -in @('failed','blocked','not-run')}).Count -gt 0 -and $m.decision -eq 'approved'){throw 'Blocked or not-run evidence cannot be approved.'}
Write-Output "PASS: acceptance matrix is bounded and evidence-layer safe: $rel"
