[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$RequestPath,[Parameter(Mandatory=$true)][string]$DesignSpecPath,[Parameter(Mandatory=$true)][string]$RevisionReceiptPath,[switch]$AllowOverwrite)
$ErrorActionPreference='Stop'
# Compatibility name only: static packaging is AI-artifact-only; no HTML/CSS/JS template generation remains here.
& (Join-Path $PSScriptRoot 'Invoke-ESWebPageStudioAiMaterialization.ps1') -RequestPath $RequestPath -DesignSpecPath $DesignSpecPath -RevisionReceiptPath $RevisionReceiptPath -AllowOverwrite:$AllowOverwrite
