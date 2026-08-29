[CmdletBinding()]
param([Parameter(Mandatory)][string]$SessionsRoot,[ValidateRange(1,256)][int]$MaxSessions = 256,[ValidateRange(1,8192)][int]$MaxMessagesPerSession = 512,[ValidateRange(1024,4194304)][int]$MaxMessageBytes = 262144)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ESAITalkAggregation.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'ESAITalkProjectAggregation.psm1') -Force
Invoke-ESAITalkProjectAggregation -SessionsRoot $SessionsRoot -MaxSessions $MaxSessions -MaxMessagesPerSession $MaxMessagesPerSession -MaxMessageBytes $MaxMessageBytes | ConvertTo-Json -Depth 30
