[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path)
$ErrorActionPreference = 'Stop'
$runner = Join-Path (Resolve-Path $ProjectRoot) '.agents/skills/es-ai-collaboration-menu/scripts/Test-es-ai-collaboration-menu-StaticReplay.ps1'
& $runner
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
