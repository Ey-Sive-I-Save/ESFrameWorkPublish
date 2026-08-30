[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$SolutionPath,
  [string]$ProjectRoot=''
)
$ErrorActionPreference='Stop'
$root=if([string]::IsNullOrWhiteSpace($ProjectRoot)){(Get-Location).Path}else{[IO.Path]::GetFullPath($ProjectRoot)}
$root=[IO.Path]::GetFullPath($root).TrimEnd('\')+'\'
function Resolve-ProjectPath([string]$p){
  $f=if([IO.Path]::IsPathRooted($p)){[IO.Path]::GetFullPath($p)}else{[IO.Path]::GetFullPath((Join-Path $root $p))}
  if(-not $f.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'SolutionPath must remain under project root.'}
  $f
}
$full=Resolve-ProjectPath $SolutionPath
$schema=Join-Path $root 'ES/Automation/Contracts/es-ai-web-solution-response-v1.schema.json'
$findings=[Collections.Generic.List[string]]::new()
if(-not(Test-Path -LiteralPath $full -PathType Leaf)){$findings.Add('blocked.solution-response.missing')}
if(-not(Test-Path -LiteralPath $schema -PathType Leaf)){$findings.Add('blocked.solution-response.schema-missing')}
if($findings.Count -eq 0){
  try{$value=Get-Content -Raw -Encoding UTF8 $full|ConvertFrom-Json}catch{$findings.Add('blocked.solution-response.invalid-json')}
}
if($findings.Count -eq 0){
  Import-Module (Join-Path $root 'ES/Automation/Contracts/ESJsonSchemaLite.psm1') -Force
  $schemaErrors=@(Test-ESJsonSchemaValue -SchemaPath $schema -Value $value)
  foreach($e in $schemaErrors){$findings.Add(('blocked.solution-response.schema:'+([string]$e)))}
  if([string]$value.providerRunId -match '(?i)normalized-|synthetic|fixture'){$findings.Add('blocked.solution-response.provider-id-not-real')}
  if([string]$value.provenance.actor -notin @('current-ai-session','provider')){$findings.Add('blocked.solution-response.provenance-invalid')}
  foreach($spec in @(@('intent',80),@('informationArchitecture',120),@('interactionDesign',120),@('visualDesign',120),@('responsiveAndStates',80),@('dataAndAcceptance',80),@('aiAnalysis',80),@('execution',40))){$n=$spec[0];$min=[int]$spec[1];if(([string]$value.$n).Trim().Length -lt $min){$findings.Add("blocked.solution-response.$n-too-short")}}
}
$hash=if(Test-Path -LiteralPath $full -PathType Leaf){(Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant()}else{$null}
[ordered]@{schemaVersion=1;recordType='AiWebSolutionResponseValidationReceipt';status=if($findings.Count){'blocked'}else{'passed'};solutionPath=$SolutionPath.Replace('\','/');solutionHash=$hash;schemaPath='ES/Automation/Contracts/es-ai-web-solution-response-v1.schema.json';schemaHash=if(Test-Path -LiteralPath $schema){(Get-FileHash -LiteralPath $schema -Algorithm SHA256).Hash.ToLowerInvariant()}else{$null};findings=@($findings);runtimeStatus='runtime-not-run';claimsNotProven=@('Validation does not prove provider quality, browser rendering, or interaction behavior.') }|ConvertTo-Json -Depth 10
if($findings.Count){exit 1}
