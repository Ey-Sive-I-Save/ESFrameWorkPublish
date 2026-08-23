<#
.SYNOPSIS
Runs the shared read-only StaticDeepReplay contract for es-feishu-cli.
.PARAMETER ProjectRoot
Project root containing .agents. Read scope is limited to manifest-declared source roots.
.PARAMETER ReportPath
Managed project-relative JSON report path. Write scope is limited to this report.
.NOTES
Idempotent for unchanged inputs. Exit code follows the shared runner: 0 pass, nonzero blocked/failed.
On failure, preserve the report and fix source/manifest drift before retrying. This script never starts Unity, Node or Feishu network work.
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
    [string]$ReportPath = 'ES/Output/StaticReplay/es-feishu-cli.json'
)

$shared = Join-Path $PSScriptRoot '..\..\es-static-deep-replay\scripts\Invoke-ESStaticDeepReplay.ps1'
& $shared -ProjectRoot $ProjectRoot -ManifestPath '.agents/skills/es-feishu-cli/static-replay.manifest.json' -ReportPath $ReportPath
