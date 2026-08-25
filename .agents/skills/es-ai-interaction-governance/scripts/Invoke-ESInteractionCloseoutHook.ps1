[CmdletBinding()]
param(
  [string]$InputJson = '',
  [string]$CloseoutScriptPath = ''
)

$ErrorActionPreference='Stop'
$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)

function Read-HookInput {
  param([string]$Explicit)
  if(-not [string]::IsNullOrWhiteSpace($Explicit)){return $Explicit}
  $stream=[Console]::OpenStandardInput()
  try {
    $reader=[IO.StreamReader]::new($stream,[Text.UTF8Encoding]::new($false,$true),$true)
    try{return $reader.ReadToEnd()}finally{$reader.Dispose()}
  } finally {$stream.Dispose()}
}
function Get-HookValue {
  param($Object,[string]$Name,$Default=$null)
  $property=$Object.PSObject.Properties[$Name]
  if($null -eq $property -or $null -eq $property.Value){return $Default}
  return $property.Value
}
function Emit-Unverifiable {
  param([string]$Reason)
  [pscustomobject][ordered]@{
    continue=$true
    systemMessage=('[ES evidence-first closeout] status=unverifiable; finding=' + $Reason + '; score=unavailable; no completion claim is permitted.')
  } | ConvertTo-Json -Compress -Depth 5
}

try {
  $hookText=Read-HookInput $InputJson
  if([string]::IsNullOrWhiteSpace($hookText)){return}
  $hook=$hookText|ConvertFrom-Json
  if([string](Get-HookValue $hook 'hook_event_name' '') -ne 'Stop'){return}
  if([bool](Get-HookValue $hook 'stop_hook_active' $false)){return}
  $transcript=[string](Get-HookValue $hook 'transcript_path' '')
  if([string]::IsNullOrWhiteSpace($transcript)){Emit-Unverifiable 'missing-transcript-path';return}
  if(-not [IO.Path]::IsPathRooted($transcript)){Emit-Unverifiable 'transcript-path-not-absolute';return}
  $resolved=[IO.Path]::GetFullPath($transcript)
  if([IO.Path]::GetExtension($resolved) -ne '.jsonl' -or -not (Test-Path -LiteralPath $resolved -PathType Leaf)){Emit-Unverifiable 'transcript-path-not-readable-jsonl';return}
  $script=if([string]::IsNullOrWhiteSpace($CloseoutScriptPath)){Join-Path $PSScriptRoot 'Invoke-ESInteractionCloseout.ps1'}else{[IO.Path]::GetFullPath($CloseoutScriptPath)}
  if(-not (Test-Path -LiteralPath $script -PathType Leaf)){Emit-Unverifiable 'closeout-script-not-found';return}
  $scopeWrites=$hook.PSObject.Properties['allow_writes']
  $scopeRuntime=$hook.PSObject.Properties['allow_runtime']
  if($null -eq $scopeWrites -and $null -eq $scopeRuntime) {
    $last=[string](Get-HookValue $hook 'last_assistant_message' '')
    $message='[ES evidence-first closeout] status=unverifiable; finding=missing-explicit-scope; score=unavailable; Hook payload did not carry authorization scope, so writes/runtime are not classified.'
    if($last -notmatch '(?i)evidence-first closeout|\u8bc1\u636e\u8bc4\u4ef7\u72b6\u6001|\u89c2\u5bdf\u8bc1\u636e') {[pscustomobject][ordered]@{decision='block';reason=($message + "`nNext turn must report scope as unavailable and must not claim authorization or completion.")}|ConvertTo-Json -Compress -Depth 5} else {[pscustomobject][ordered]@{continue=$true;systemMessage=$message}|ConvertTo-Json -Compress -Depth 5}
    return
  }
  $closeout=& $script -SessionPath $resolved -AllowWrites:([bool](Get-HookValue $hook 'allow_writes' $false)) -AllowRuntime:([bool](Get-HookValue $hook 'allow_runtime' $false)) -RuntimeRequired:([bool](Get-HookValue $hook 'runtime_required' $false)) | ConvertFrom-Json
  $e=$closeout.evidence
  $findings=if(@($closeout.findings).Count){(@($closeout.findings)-join ',')}else{'none'}
  $loop=$closeout.feedbackLoop
  $metrics=$closeout.observationMetrics
  $diagnosis=if(@($closeout.diagnosticCodes).Count){@($closeout.diagnosticCodes)-join ','}else{'none'}
  $message='[ES evidence-first closeout] status={0}; user={1}; assistant={2}; tools={3}; verification={4}; records={5}; elapsedMs={6}; truncated={7}; writes={8}/{9}; writeTargets={10}; runtime={11}/{12}; findings={13}; diagnosis={14}; correctionState={15}; userAcceptance={16}; resolution={17}; next={18}; score=unavailable.' -f ([string]$closeout.status),$e.userMessages,$e.assistantMessages,$e.toolEvents,$e.verificationEvents,$metrics.recordsRead,$metrics.elapsedMs,$metrics.textTruncated,$closeout.observed.writesObserved,$closeout.observed.writesAllowed,@($closeout.writeTargetHints).Count,$closeout.observed.runtimeObserved,$closeout.observed.runtimeAllowed,$findings,$diagnosis,[string]$closeout.correctionState,[bool]$loop.userAcceptanceObserved,[string]$loop.resolutionClaim,[string]$closeout.nextAction
  $last=[string](Get-HookValue $hook 'last_assistant_message' '')
  if($last -notmatch '(?i)evidence-first closeout|\u8bc1\u636e\u8bc4\u4ef7\u72b6\u6001|\u89c2\u5bdf\u8bc1\u636e') {
    [pscustomobject][ordered]@{decision='block';reason=($message + "`nNext turn must publish evidence status, observation counts, findings, and unproven claims; use score=unavailable when no real evaluator result exists.")}|ConvertTo-Json -Compress -Depth 5
  }
  else {
    [pscustomobject][ordered]@{continue=$true;systemMessage=$message}|ConvertTo-Json -Compress -Depth 5
  }
}
catch {
  Emit-Unverifiable 'hook-input-or-evaluation-error'
}
