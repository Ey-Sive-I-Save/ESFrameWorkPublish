[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SessionsRoot,
    [ValidateRange(1,256)][int]$MaxSessions = 256,
    [ValidateRange(1,8192)][int]$MaxMessagesPerSession = 512,
    [ValidateRange(1024,4194304)][int]$MaxMessageBytes = 262144,
    [string]$RoundObservationsPath
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ESAITalkHumanLightFlow.psm1') -Force
$params = @{ SessionsRoot=$SessionsRoot; MaxSessions=$MaxSessions; MaxMessagesPerSession=$MaxMessagesPerSession; MaxMessageBytes=$MaxMessageBytes }
if (-not [string]::IsNullOrWhiteSpace($RoundObservationsPath)) { $params.RoundObservations = @([Text.UTF8Encoding]::new($false,$true).GetString([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $RoundObservationsPath).Path)) | ConvertFrom-Json) }
Invoke-ESAITalkHumanLightFlow @params | ConvertTo-Json -Depth 30
