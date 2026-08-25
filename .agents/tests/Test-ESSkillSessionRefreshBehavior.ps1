[CmdletBinding()]
param(
    [string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
    [string]$ReportPath='ES/Output/StaticReplay/es-skill-session-refresh-behavior.json'
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\','/')
$invoke=Join-Path $root '.agents/skills/es-skill-session-refresh/scripts/Invoke-ESSkillSessionRefresh.ps1'
$snapshot='ES/Output/StaticReplay/session-refresh-behavior-current.json'
$baseline='ES/Output/StaticReplay/session-refresh-behavior-baseline.json'
$scopedSnapshot='ES/Output/StaticReplay/session-refresh-behavior-scoped.json'
$unscopedSnapshot='ES/Output/StaticReplay/session-refresh-behavior-unscoped.json'
$reportFull=Join-Path $root $ReportPath.Replace('/','\')
function WriteJson([string]$relative,[object]$value){$full=Join-Path $root $relative.Replace('/','\');$parent=Split-Path -Parent $full;if(-not(Test-Path $parent)){New-Item -ItemType Directory -Path $parent -Force|Out-Null};[IO.File]::WriteAllText($full,($value|ConvertTo-Json -Depth 16),(New-Object Text.UTF8Encoding($false)))}
function RunRefresh([string[]]$arguments){$text=& powershell -NoProfile -ExecutionPolicy Bypass -File $invoke @arguments 2>&1 | Out-String;$exit=$LASTEXITCODE;try{$json=$text|ConvertFrom-Json}catch{throw "Refresh output is not JSON: $text"};[pscustomobject]@{exit=$exit;json=$json}}
$build=RunRefresh @('-Mode','Build','-ProjectRoot',$root,'-SessionId','behavior-fixture','-SnapshotPath',$snapshot,'-DiscoveryMode','Operational')
if($build.exit -ne 0){throw 'Behavior fixture Build failed.'}
$current=Get-Content (Join-Path $root $snapshot) -Raw -Encoding UTF8|ConvertFrom-Json
$target=@($current.skills|Where-Object {$_.skillName -eq 'es-skill-session-refresh'})[0]
if($null -eq $target){throw 'Behavior fixture target Skill is missing.'}
$targetFile=@($target.files)[0]
if($null -eq $targetFile){throw 'Behavior fixture target Skill has no files.'}
$targetFile.sha256=('0'*64)
WriteJson $baseline $current
$scoped=RunRefresh @('-Mode','Compare','-ProjectRoot',$root,'-SessionId','behavior-fixture','-BaselinePath',$baseline,'-SnapshotPath',$scopedSnapshot,'-RouteKeys','incremental-discovery','-DiscoveryMode','Operational')
$scopedPassed=($scoped.exit -eq 1 -and [string]$scoped.json.status -eq 'stale' -and @($scoped.json.selectedSkills) -contains 'es-skill-session-refresh')
$unscoped=RunRefresh @('-Mode','Compare','-ProjectRoot',$root,'-SessionId','behavior-fixture','-BaselinePath',$baseline,'-SnapshotPath',$unscopedSnapshot,'-DiscoveryMode','Operational')
$unscopedPassed=($unscoped.exit -eq 1 -and [string]$unscoped.json.status -eq 'blocked' -and [string]$unscoped.json.nextAction -eq 'replan')
$result=[ordered]@{schemaVersion=1;validator='es-skill-session-refresh-behavior';status=if($scopedPassed -and $unscopedPassed){'passed'}else{'failed'};cases=@(
    [ordered]@{id='route-scoped-delta';status=if($scopedPassed){'passed'}else{'failed'};assertion='A changed Skill is selected only when a specific current route matches.'},
    [ordered]@{id='route-scope-required';status=if($unscopedPassed){'passed'}else{'failed'};assertion='A compare without RouteKeys blocks and requests replan instead of selecting the portfolio.'}
);artifacts=@($snapshot,$baseline,$scopedSnapshot,$unscopedSnapshot)}
WriteJson $ReportPath $result
$result|ConvertTo-Json -Depth 12
if($result.status -eq 'passed'){exit 0};exit 1
