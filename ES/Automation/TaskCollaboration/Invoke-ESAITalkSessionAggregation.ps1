[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SessionPath,
    [string]$ConversationId,
    [string]$ConsensusPath,
    [switch]$WriteConsensus
)
$ErrorActionPreference = 'Stop'
$module = Join-Path $PSScriptRoot 'ESAITalkAggregation.psm1'
Import-Module $module -Force
$params = @{ SessionPath = $SessionPath; WriteConsensus = $WriteConsensus }
if (-not [string]::IsNullOrWhiteSpace($ConversationId)) { $params.ConversationId = $ConversationId }
if (-not [string]::IsNullOrWhiteSpace($ConsensusPath)) { $params.ConsensusPath = $ConsensusPath }
$aggregation = Invoke-ESAITalkSessionAggregation @params
$aggregation | ConvertTo-Json -Depth 30
