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
$contextMenu = & $invoke -PromptText 'AG 分析 AISpace' | ConvertFrom-Json
if ($contextMenu.contextSummary.projectArea -ne 'aispace' -or $contextMenu.contextSummary.agentMode -ne 'sub-agent') { throw 'Context summary did not identify AISpace sub-agent intent.' }
if (@($contextMenu.quickAccess).Count -ne 3 -or @($contextMenu.quickAccess | Where-Object { $_.recommended }).Count -ne 1 -or ($contextMenu.quickAccess | Where-Object id -eq 'agent-delegation').number -ne 'A') { throw 'Quick-access menu contract is incomplete.' }
if ($contextMenu.display.version -ne 'es-menu-display.v1' -or $contextMenu.display.layout -ne 'compact-cards' -or @($contextMenu.display.mainOptions).Count -ne 7) { throw 'Visual menu display model is incomplete.' }
if (($contextMenu.display.mainOptions | Where-Object { $_.recommended }).Count -gt 1 -or (($contextMenu.display.mainOptions | Where-Object id -eq 'ai-mechanism-atlas').icon -ne '◇')) { throw 'Visual menu display model violates recommendation or icon contract.' }
if (@($contextMenu.display.mainOptions | Where-Object { [string]::IsNullOrWhiteSpace($_.subtitle) }).Count -ne 0) { throw 'Visual menu options must expose Chinese subtitles.' }
if (@($contextMenu.display.mainOptions | Where-Object { @($_.examples).Count -lt 2 }).Count -ne 0) { throw 'Visual menu options must expose at least two examples.' }
if ((@($contextMenu.quickAccess | Where-Object { $_.recommended }).Count + @($contextMenu.display.mainOptions | Where-Object { $_.recommended }).Count) -ne 1) { throw 'Menu must expose exactly one global recommendation marker.' }
if ($contextMenu.display.skillDisclosurePolicy.userFacingFormat -ne 'stable-skill-name-only' -or @($contextMenu.display.skillDisclosurePolicy.forbiddenPatterns).Count -lt 4) { throw 'Skill disclosure presentation policy is missing.' }
$atlasOptions = @($menu.options | Where-Object { $_.id -eq 'ai-mechanism-atlas' } | Select-Object -First 1).submenu.options
if (-not (@($atlasOptions | Where-Object { $_.id -eq 'atlas-super-semantics-catalog' -and $_.label -match '大全|图鉴' }).Count)) { throw 'Super-semantics catalog entry is missing.' }
$governMenu = (& $invoke -PromptText '检查框架权限边界' | Out-String) | ConvertFrom-Json
if ($governMenu.recommendedOptionId -ne 'govern-framework') { throw 'Specific governance phrase was incorrectly routed to validation.' }
$sessionMenu = (& $invoke -PromptText '新建协作窗口' | Out-String) | ConvertFrom-Json
if ($sessionMenu.recommendedOptionId -ne 'coordinate-session' -or $sessionMenu.recommendedSubmenuId -ne 'session-new') { throw 'New collaboration window was not routed to the session submenu.' }
$routeResolver = Join-Path (Resolve-Path $ProjectRoot) '.agents/skills/es-skill-governance/scripts/Resolve-ESChineseSkillRoute.ps1'
$menuAlias = (& $routeResolver -ProjectRoot $projectRoot -Objective '打开菜单' | Out-String) | ConvertFrom-Json
if ($menuAlias.status -ne 'Matched' -or @($menuAlias.matches).Count -ne 1 -or $menuAlias.matches[0].skillName -ne 'es-ai-collaboration-menu') { throw 'Menu alias did not resolve to the collaboration menu Skill.' }
$renderer = Join-Path (Resolve-Path $ProjectRoot) '.agents/skills/es-ai-collaboration-menu/scripts/Show-ESCollaborationMenu.ps1'
$renderedText = & $renderer -PromptText '菜单' -NoColor 6>&1 | Out-String
if ($renderedText -notmatch 'ES AI 协作中心' -or $renderedText -notmatch '输入编号选择入口' -or $renderedText -notmatch 'ESGameCoreRuntimeData') { throw 'Terminal menu renderer did not include title, footer, and framework examples.' }
$richRenderer = Join-Path (Resolve-Path $ProjectRoot) '.agents/skills/es-ai-collaboration-menu/scripts/Render-ESCollaborationMenuRichText.ps1'
$richText = & $richRenderer -PromptText '菜单' | Out-String
if ($richText -notmatch '╭─' -or $richText -notmatch '╰─' -or $richText -notmatch 'ES AI 协作中心' -or $richText -notmatch '🧭 工作方向' -or $richText -notmatch 'ESGameCoreRuntimeData') { throw 'Rich-text renderer did not include fixed frame, sections, and framework examples.' }
$richHtml = & $richRenderer -PromptText '菜单' -HtmlColor | Out-String
if ($richHtml -notmatch '<span style="color:#') { throw 'Rich-text HTML color mode did not emit theme colors.' }
$ag = & $routeResolver -ProjectRoot (Resolve-Path $ProjectRoot) -Objective 'aG 分析 AISpace' | ConvertFrom-Json
if ($ag.status -ne 'Matched' -or $ag.matches[0].skillName -ne 'es-aibrain-route-authoring') { throw 'Case-insensitive AG route discovery failed.' }
$picked = & $invoke -PromptText '菜单' -Selection 'R2.4' | ConvertFrom-Json
if ($picked.selection.level -ne 'route-directory' -or $picked.selection.routeKey -ne 'evidence') { throw 'Route-directory numeric selection failed.' }
$pickedAtlas = & $invoke -PromptText '菜单' -Selection '7.1' | ConvertFrom-Json
if ($pickedAtlas.selection.submenuOptionId -ne 'atlas-super-semantics') { throw 'AI mechanism submenu numeric selection failed.' }

