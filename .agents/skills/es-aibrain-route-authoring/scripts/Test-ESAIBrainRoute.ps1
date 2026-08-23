[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [Parameter(Mandatory=$true)][string]$RouteKey
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
if([IO.Path]::IsPathRooted($RouteKey) -or $RouteKey -match '[\r\n\s/]'){throw 'RouteKey must be one non-empty project route token.'}
$index=Join-Path $root 'Documentation/AIKnowledge/KnowledgeIndex.yaml'
$entry=Join-Path $root 'Documentation/AIKnowledge/AIBRAIN_ENTRY.md'
foreach($p in @($index,$entry)){if(-not(Test-Path -LiteralPath $p -PathType Leaf)){throw "Required route source missing: $p"};[Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes($p))|Out-Null}
$text=[IO.File]::ReadAllText($index,[Text.UTF8Encoding]::new($false,$true))
$entryText=[IO.File]::ReadAllText($entry,[Text.UTF8Encoding]::new($false,$true))
$matches=[regex]::Matches($text,'(?m)^\s*routeKeys:\s*\[([^\]]+)\]')
$found=$false
foreach($m in $matches){$keys=$m.Groups[1].Value.Split(',')|%{$_.Trim().Trim("'").Trim('"')};if($keys -contains $RouteKey){$found=$true;break}}
if(-not $found){throw "No Knowledge route declares routeKey: $RouteKey"}
if($entryText -notmatch [regex]::Escape($RouteKey)){throw "AIBRAIN_ENTRY does not expose routeKey: $RouteKey"}
Write-Output "PASS: AIBrain route is discoverable: $RouteKey"
