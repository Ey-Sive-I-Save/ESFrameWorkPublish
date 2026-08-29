[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path)
$ErrorActionPreference = 'Stop'
$runner = Join-Path (Resolve-Path $ProjectRoot) '.agents/skills/es-ai-collaboration-menu/scripts/Test-es-ai-collaboration-menu-StaticReplay.ps1'
& $runner
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$invoke = Join-Path (Resolve-Path $ProjectRoot) '.agents/skills/es-ai-collaboration-menu/scripts/Invoke-ESCollaborationMenu.ps1'
$menu = & $invoke -PromptText '菜单' | ConvertFrom-Json
if ($menu.options.Count -ne 7 -or $menu.routeDirectory.categories.Count -ne 3) { throw 'Menu or categorized route directory is incomplete.' }
$atlas = $menu.options | Where-Object { $_.id -eq 'ai-mechanism-atlas' }
if ($null -eq $atlas.superSemantics -or @($atlas.superSemantics.entries).Count -lt 1 -or @($atlas.superSemantics.precedenceRules).Count -lt 1) { throw 'Super-semantics registry projection is missing from the menu.' }
$picked = & $invoke -PromptText '菜单' -Selection 'R2.4' | ConvertFrom-Json
if ($picked.selection.level -ne 'route-directory' -or $picked.selection.routeKey -ne 'evidence') { throw 'Route-directory numeric selection failed.' }
$pickedAtlas = & $invoke -PromptText '菜单' -Selection '7.1' | ConvertFrom-Json
if ($pickedAtlas.selection.submenuOptionId -ne 'atlas-super-semantics') { throw 'AI mechanism submenu numeric selection failed.' }

