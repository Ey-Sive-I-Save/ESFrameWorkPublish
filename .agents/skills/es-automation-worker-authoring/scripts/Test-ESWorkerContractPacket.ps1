[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProjectRoot,[Parameter(Mandatory=$true)][string]$PacketPath)

Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\','/')
$prefix=$root+[IO.Path]::DirectorySeparatorChar

function Read-StrictJson([string]$Path) {
    $utf8=[Text.UTF8Encoding]::new($false,$true)
    return $utf8.GetString([IO.File]::ReadAllBytes($Path)) | ConvertFrom-Json -ErrorAction Stop
}
function Canonical([object]$Value) {
    if($null -eq $Value){return 'null'}
    if($Value -is [string] -or $Value -is [char]){return ([string]$Value|ConvertTo-Json -Compress)}
    if($Value -is [bool]){return $(if($Value){'true'}else{'false'})}
    if($Value -is [Collections.IDictionary]){
        return '{'+((@($Value.Keys|ForEach-Object{[string]$_}|Sort-Object)|ForEach-Object{('{0}:{1}' -f ($_|ConvertTo-Json -Compress),(Canonical $Value[$_]))})-join ',')+'}'
    }
    if($Value -is [pscustomobject]){
        return '{'+((@($Value.PSObject.Properties|Sort-Object Name)|ForEach-Object{('{0}:{1}' -f ($_.Name|ConvertTo-Json -Compress),(Canonical $_.Value))})-join ',')+'}'
    }
    if($Value -is [Collections.IEnumerable] -and $Value -isnot [string]){return '['+((@($Value)|ForEach-Object{Canonical $_})-join ',')+']'}
    return ([string]$Value|ConvertTo-Json -Compress)
}
function HashValue([object]$Value){$sha=[Security.Cryptography.SHA256]::Create();try{return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes((Canonical $Value)))).Replace('-','').ToLowerInvariant())}finally{$sha.Dispose()}}
function Resolve-Relative([string]$Path,[string]$Name) {
    if([string]::IsNullOrWhiteSpace($Path) -or $Path -ne $Path.Trim() -or [IO.Path]::IsPathRooted($Path) -or $Path -match '^[A-Za-z]:' -or $Path -match '^[\\/]{2}' -or $Path -match '(^|[\\/])\.\.([\\/]|$)' -or $Path -match '[*?]'){throw "$Name must be a normalized project-relative path: $Path"}
    $full=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$Path.Replace('/',[IO.Path]::DirectorySeparatorChar)))
    if(-not($full.Equals($root,[StringComparison]::OrdinalIgnoreCase)-or$full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase))){throw "$Name escapes ProjectRoot: $Path"}
    return $full
}
function Is-Under([string]$Child,[string]$Parent){$c=[IO.Path]::GetFullPath($Child).TrimEnd('\','/');$p=[IO.Path]::GetFullPath($Parent).TrimEnd('\','/');return $c.Equals($p,[StringComparison]::OrdinalIgnoreCase)-or$c.StartsWith($p+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)}

if([IO.Path]::IsPathRooted($PacketPath)){throw 'PacketPath must be project-relative.'}
$relative=$PacketPath.Replace('\','/').Trim()
if($relative.Contains('..') -or $relative -notmatch '^ES/Output/.+\.json$'){throw 'PacketPath must remain under ES/Output.'}
$full=Resolve-Relative $relative 'PacketPath'
if(-not(Test-Path -LiteralPath $full -PathType Leaf)){throw "Worker packet missing: $relative"}
$p=Read-StrictJson $full
foreach($x in @('WorkerId','Version','TaskContract','PlanHash','AllowedRoots','ArgumentsSchema','Environment','SecretsPolicy','Timeout','Concurrency','Artifacts','Cancel','Recovery','RunRecord','Owner','StaleWhen')){if($null -eq $p.PSObject.Properties[$x]){throw "Worker field missing: $x"}}
if([string]$p.PlanHash -notmatch '^[0-9a-fA-F]{64}$'){throw 'PlanHash must be SHA-256.'}
if($null -eq $p.PSObject.Properties['Plan']){throw 'Plan is required so PlanHash can be recomputed.'}
$planHash=HashValue $p.Plan
if($planHash -cne ([string]$p.PlanHash).ToLowerInvariant()){throw 'PlanHash does not match canonical Plan content.'}
if([string]$p.SecretsPolicy -notmatch '^(deny|redacted)$'){throw 'SecretsPolicy must be deny or redacted.'}
if([int]$p.Timeout -lt 1 -or [int]$p.Concurrency -lt 1){throw 'Timeout and Concurrency must be positive.'}
if(@($p.AllowedRoots).Count -eq 0){throw 'AllowedRoots is required.'}
$allowedFull=@()
foreach($path in @($p.AllowedRoots)){$allowedFull+=Resolve-Relative ([string]$path) 'AllowedRoots'}
if(@($allowedFull|Sort-Object -Unique).Count -ne @($allowedFull).Count){throw 'AllowedRoots must be unique.'}
$artifactPaths=@($p.Artifacts)
if($artifactPaths.Count -eq 0){throw 'Artifacts must contain at least one output path.'}
foreach($artifact in $artifactPaths){$artifactFull=Resolve-Relative ([string]$artifact) 'Artifacts';if(-not(@($allowedFull|Where-Object{Is-Under $artifactFull $_}).Count)){throw "Artifact is outside AllowedRoots: $artifact"}}
if([string]$p.TaskContract -eq 'missing' -or [string]$p.Cancel -eq 'unsupported' -or [string]$p.Recovery -eq 'none'){throw 'Worker packet must declare TaskContract, cancellation and recovery.'}
Write-Output "PASS: strict managed Worker contract bounds PlanHash, roots, artifacts, secrets, timeout, cancellation and recovery: $relative"
