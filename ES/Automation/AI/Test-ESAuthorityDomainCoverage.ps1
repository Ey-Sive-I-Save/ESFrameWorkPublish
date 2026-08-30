[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)
$ErrorActionPreference='Stop';$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$files=@(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/Plugins/ES/Editor') -Recurse -Filter '*.cs' -File |
 Where-Object { $_.FullName -notmatch '[\\/]Tests[\\/]' } |
 ForEach-Object { $_.FullName.Substring($root.Length + 1).Replace('\','/') })
$findings=@()
foreach($relative in $files){
 $path=Join-Path $root $relative
 if(-not(Test-Path -LiteralPath $path -PathType Leaf)){ $findings+=,[pscustomobject]@{file=$relative;status='failed';reason='file-missing'};continue }
 $lines=Get-Content -LiteralPath $path -Encoding UTF8
 for($i=0;$i -lt $lines.Count;$i++){
  if($lines[$i] -match 'new\s+ESAutomationCompletionDecision\b'){
   $end=[Math]::Min($lines.Count-1,$i+120);$window=($lines[$i..$end]-join "`n")
   $hasDomain=$window -match '(?m)^\s*authorityDomain\s*='
   $findings+=,[pscustomobject]@{file=$relative;line=$i+1;status=if($hasDomain){'passed'}else{'failed'};reason=if($hasDomain){'explicit-domain'}else{'authority-domain-missing'}}
  }
 }
}
$failed=@($findings|Where-Object status -eq 'failed')
$catalogPath=Join-Path $root 'Assets/Plugins/ES/AICommands/AICommandCatalog.json';$catalog=Get-Content -LiteralPath $catalogPath -Raw -Encoding UTF8|ConvertFrom-Json;$high=@($catalog.commands|Where-Object {$_.riskLevel -in @('L2','L3')});$explicit=@($high|Where-Object {$_.authorityDomain -and $_.authorityRiskClass})
[pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESAuthorityDomainCoverage';status=if($failed.Count){'failed'}else{'passed'};staticStatus=if($failed.Count){'static-failed'}else{'static-passed'};runtimeStatus='runtime-not-run';caseCount=$findings.Count;passedCount=$findings.Count-$failed.Count;failedCount=$failed.Count;findings=$findings;catalogHighRiskCount=$high.Count;catalogExplicitDomainCount=$explicit.Count;catalogImplicitDomainCount=$high.Count-$explicit.Count;claimsNotProven=@('runtime behavior','dynamic reflection-created decisions')}|ConvertTo-Json -Depth 10
if($failed.Count){exit 1}
