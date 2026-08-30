[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')
$result=& (Join-Path $root 'ES\Automation\WebPageStudio\Test-ESWebUiSubAgentScheduler.ps1') | ConvertFrom-Json
if([string]$result.status -ne 'passed'){throw 'WEB_UI_SCHEDULER_STATIC_REPLAY_FAILED'}
[ordered]@{validator='es-web-ui-generation-static-replay';status='passed';runtimeStatus='runtime-not-run';checks=@($result.checks);nonClaims=@('does-not-start-external-worker','does-not-prove-cross-process-throughput','does-not-prove-browser-or-Unity-behavior')}|ConvertTo-Json -Depth 8
