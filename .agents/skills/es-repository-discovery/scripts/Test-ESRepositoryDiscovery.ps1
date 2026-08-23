[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot,[string]$Target='.',[ValidateRange(1,1000)][int]$MaxFiles=200)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
if([IO.Path]::IsPathRooted($Target)){throw 'Target must be project-relative.'}
$rel=$Target.Replace('\','/').Trim();if($rel.Contains('..')){throw 'Target traversal is denied.'}
$targetPath=[IO.Path]::GetFullPath((Join-Path $root $rel));$prefix=$root.TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
if(-not($targetPath.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)-or $targetPath -eq $root)){throw 'Target escaped the project root.'}
$authority=@((Join-Path $root 'AGENTS.md'),(Join-Path $root '.agents/SKILL_RESOURCE_INDEX.yaml'),(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/Plugins/ES/AIWarnings') -Recurse -File -Filter 'README.md'|Select-Object -First 1).FullName);foreach($p in $authority){if([string]::IsNullOrWhiteSpace($p)-or -not(Test-Path -LiteralPath $p -PathType Leaf)){throw "Discovery authority missing: $p"}}
if(-not(Test-Path -LiteralPath $targetPath)){throw "Target does not exist: $rel"}
$files=@(Get-ChildItem -LiteralPath $targetPath -Recurse -File -ErrorAction Stop|Where-Object{$_.FullName -notmatch '\\(Library|Temp|Logs)\\'});if($files.Count -gt $MaxFiles){throw "Discovery file budget exceeded: $($files.Count) > $MaxFiles"}
Write-Output "PASS: bounded repository discovery target is valid: $rel ($($files.Count) files)"
