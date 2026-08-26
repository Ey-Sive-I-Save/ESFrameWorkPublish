[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$SessionPath,
  [string]$OutputPath,
  [ValidateRange(1,100000)][int]$MaxTextChars=4000,
  [switch]$AllowWrites,
  [switch]$AllowRuntime,
  [switch]$RuntimeRequired
)

$ErrorActionPreference='Stop'
$pathPolicy=(Resolve-Path (Join-Path $PSScriptRoot 'ESInteractionPathPolicy.ps1')).Path
. $pathPolicy
$resolvedSessionPath=(Resolve-Path -LiteralPath $SessionPath).Path
if([IO.Path]::GetExtension($resolvedSessionPath) -cne '.jsonl'){throw 'SessionPath must identify a .jsonl transcript.'}
$timer=[Diagnostics.Stopwatch]::StartNew()
$truncatedCount=0
function Get-TextFromContent($content){
  if($null -eq $content){return ''}
  return ((@($content)|ForEach-Object{if($_.text){$_.text}}) -join "`n").Trim()
}
function Is-Mutating($text){return [bool]($text -match '(?i)apply_patch|Set-Content|Out-File|Add-Content|New-Item|Remove-Item|Move-Item|Copy-Item|git\s+(add|commit|reset|checkout|clean)|WriteAllText|WriteAllBytes')}
# Runtime is an observed execution claim, not a topic keyword. Do not classify
# paths, documentation, or ordinary words such as "Runtime"/"Player" as a run.
# Count only recognizable execution/build/test entry points in tool input.
function Is-Runtime($text){
  if($text -match '(?s)\*\*\*\s+Begin\s+Patch|const\s+patch\s*='){return $false}
  return [bool]($text -match '(?i)(&\s*["''][^"'']*Unity\.exe["'']|-executeMethod\b|-batchmode\b|-runTests\b|-testPlatform\s+(?:editmode|playmode)\b|\bStart-Process\b|\bIL2CPP\s+(?:build|compile)\b|\b(?:PlayMode|EditMode)\s+tests?\b)')
}
function Is-Verification($text){return [bool]($text -match '(?i)Test-|validator|validate|git\s+diff\s+--check|Exit code:\s*0|status["'']?\s*:\s*["'']passed|passed|\u9a8c\u8bc1|\u9a8c\u6536')}

$user=@();$assistant=@();$tools=@();$verify=@();$corrections=@();$writeTargetHints=@();$parseErrors=0;$line=0;$assistantSeen=$false
foreach($raw in Get-Content -LiteralPath $resolvedSessionPath -Encoding UTF8){
  $line++
  try{$row=$raw|ConvertFrom-Json}catch{$parseErrors++;continue}
  $ts=[string]$row.timestamp;$payload=$row.payload
  if($row.type -eq 'response_item' -and $payload.type -eq 'message'){
    $text=Get-TextFromContent $payload.content
    if($text.Length -gt $MaxTextChars){$text=$text.Substring(0,$MaxTextChars);$truncatedCount++}
    $injected=[bool]($text -match '(?i)<recommended_plugins>|<permissions instructions>|# AGENTS\.md instructions|<skills_instructions>')
    if($payload.role -eq 'user' -and $text -and !$injected){$user+=@{text=$text;timestamp=$ts;line=$line}}
    elseif($payload.role -eq 'assistant' -and $text){$assistant+=@{text=$text;timestamp=$ts;line=$line};$assistantSeen=$true}
    if($payload.role -eq 'user' -and $text -and !$injected -and $assistantSeen -and $text -match '(?i)\u4e0d\u5bf9|\u4e0d\u662f|\u6211\u8bf4|\u4e0d\u8981|\u7ea0\u6b63|\u9519\u8bef|\u4e0d\u5e94\u8be5|wrong|correction'){$corrections+=@{text=$text;timestamp=$ts;line=$line}}
  }
  elseif($row.type -eq 'event_msg' -and $payload.type -eq 'agent_message'){
    $text=[string]$payload.message;if($text){$assistant+=@{text=$text;timestamp=$ts;line=$line};$assistantSeen=$true}
  }
  elseif($row.type -eq 'response_item' -and $payload.type -in @('custom_tool_call','function_call')){
    $rawTool=if($payload.input){[string]$payload.input}else{[string]$payload.arguments}
    $mut=Is-Mutating $rawTool;$runtime=Is-Runtime $rawTool
    if($mut){
      $pathText=$rawTool -replace '\\n',"`n"
      foreach($m in [regex]::Matches($pathText,'(?m)^\*\*\*\s+(?:Update|Add|Delete)\s+File:\s*(.+?)\s*$')){if($writeTargetHints.Count -lt 64){$writeTargetHints+=(([string]$m.Groups[1].Value).Trim() -replace '\\{2,}','\')}}
      foreach($m in [regex]::Matches($pathText,'(?i)(?:-LiteralPath|-Path)\s+["'']([^"'']+)["'']')){if($writeTargetHints.Count -lt 64){$writeTargetHints+=(([string]$m.Groups[1].Value).Trim() -replace '\\{2,}','\')}}
    }
    $tools+=@{name=[string]$payload.name;mutating=$mut;runtime=$runtime;success=($payload.status -ne 'failed');timestamp=$ts;line=$line}
    if(Is-Verification $rawTool){$verify+=@{name=[string]$payload.name;status='observed';timestamp=$ts;line=$line}}
  }
  elseif($row.type -eq 'response_item' -and $payload.type -in @('custom_tool_call_output','function_call_output')){
    $out=if($payload.output -is [string]){[string]$payload.output}else{$payload.output|ConvertTo-Json -Compress -Depth 4}
    if(Is-Verification $out){$verify+=@{name='tool-output';status=if($out -match '(?i)Exit code:\s*0|passed'){ 'passed' }else{ 'observed' };timestamp=$ts;line=$line}}
  }
}

$allUserText=($user|ForEach-Object{$_.text}) -join "`n"
$timer.Stop()
$projectRoot=Get-ESInteractionProjectRoot
$targetResolution=@()
foreach($hint in @($writeTargetHints|Select-Object -Unique)){
  if($targetResolution.Count -ge 64){break}
  try{
    $clean=([string]$hint).Trim() -replace '\\{2,}','\'
    $full=if([IO.Path]::IsPathRooted($clean)){[IO.Path]::GetFullPath($clean)}else{[IO.Path]::GetFullPath((Join-Path $projectRoot $clean))}
    $rootPrefix=$projectRoot + '\'
    if($full -eq $projectRoot -or $full.StartsWith($rootPrefix,[StringComparison]::OrdinalIgnoreCase)){
      $relative=if($full -eq $projectRoot){'.'}else{$full.Substring($rootPrefix.Length)}
      if(!(Test-Path -LiteralPath $full -PathType Leaf)){$targetResolution+=([ordered]@{path=$relative;state='missing';currentWorktreeState='missing'})}
      else {
        $statusLine=@(& git -C $projectRoot status --short -- $relative 2>$null | Select-Object -First 1)
        $worktreeState=if(!$statusLine.Count){'unchanged'}elseif(([string]$statusLine[0]) -match '^\?\?'){'untracked'}else{'modified'}
        $hash=Get-FileHash -LiteralPath $full -Algorithm SHA256
        $targetResolution+=([ordered]@{path=$relative;state='exists';currentWorktreeState=$worktreeState;bytes=(Get-Item -LiteralPath $full).Length;sha256=$hash.Hash})
      }
    } else {$targetResolution+=([ordered]@{path=$clean;state='outside-project'})}
  } catch {$targetResolution+=([ordered]@{path=[string]$hint;state='unresolvable'})}
}
$scope=@{allowWrites=[bool]$AllowWrites;allowRuntime=[bool]$AllowRuntime;runtimeRequired=[bool]$RuntimeRequired;source='explicit-adapter-arguments';inference='never-inferred-as-authorization'}
$result=[ordered]@{schemaVersion=1;source='codex-jsonl';sessionPath=$resolvedSessionPath;parseErrors=$parseErrors;observationMetrics=[ordered]@{recordsRead=$line;elapsedMs=$timer.ElapsedMilliseconds;textTruncated=$truncatedCount};requestedScope=$scope;userMessages=@($user);assistantMessages=@($assistant);toolEvents=@($tools);fileChanges=@();writeTargetHints=@($writeTargetHints|Select-Object -Unique);writeTargetResolution=@($targetResolution);verificationEvents=@($verify);userCorrections=@($corrections);nonClaims=@('System/developer/world-state records excluded','Text extraction is bounded and does not prove semantic intent','Observation metrics describe this invocation only; they are not a global performance or quality claim','writeTargetHints and writeTargetResolution are observations, not proof of an applied diff or successful write')}
$json=$result|ConvertTo-Json -Depth 10
if($OutputPath){$full=Resolve-ESInteractionReportPath -Candidate $OutputPath -AllowSystemTemp -Label 'OutputPath';$dir=Split-Path $full -Parent;if(!(Test-Path $dir)){New-Item -ItemType Directory -Path $dir -Force|Out-Null};$full=Resolve-ESInteractionReportPath -Candidate $full -AllowSystemTemp -Label 'OutputPath';[IO.File]::WriteAllText($full,$json,(New-Object Text.UTF8Encoding($false)))}
$json
