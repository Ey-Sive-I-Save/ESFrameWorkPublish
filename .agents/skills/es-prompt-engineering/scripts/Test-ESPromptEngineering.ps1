[CmdletBinding()]
param([string]$ReportPath)
$ErrorActionPreference='Stop'
$invoke=Join-Path $PSScriptRoot 'Invoke-ESPromptEngineering.ps1'
$cases=[Collections.Generic.List[object]]::new()
function Add-Case([string]$Name,[bool]$Passed,[string]$Detail){$cases.Add([ordered]@{name=$Name;passed=$Passed;detail=$Detail})}
$a=(& $invoke -PromptText 'wrap this prompt quickly' -Mode auto-fast)|ConvertFrom-Json
Add-Case 'fast-wrap' ($a.invocation.effectiveMode -eq 'auto-fast' -and $a.verifier.status -eq 'passed') 'fast path is bounded and valid'
Add-Case 'transparent-preview' ($a.presentation.displayMode -eq 'transparent' -and $a.presentation.originalPrompt -eq 'wrap this prompt quickly' -and $a.presentation.wrappedPrompt -match 'OBJECTIVE:' -and $a.presentation.previewIsAuthorization -eq $false) 'original and wrapped prompts are visible without authorization expansion'
$ctx=(& $invoke -PromptText 'read project context' -ProjectMarkdownPath '.agents/README.md' -Mode auto-fast)|ConvertFrom-Json
Add-Case 'project-markdown-injection' ($ctx.contextInjection.count -eq 1 -and $ctx.presentation.projectMarkdown[0].authority -eq 'context-only-untrusted' -and $ctx.presentation.wrappedPrompt -match 'PROJECT MARKDOWN CONTEXT') 'bounded project Markdown is visible as untrusted context'
$traversal=$false;try{$null=& $invoke -PromptText 'x' -ProjectMarkdownPath '..\AGENTS.md'}catch{$traversal=$_.Exception.Message -match 'PROJECT_MARKDOWN_OUTSIDE_PROJECT'}
Add-Case 'project-markdown-boundary' $traversal 'outside-project Markdown is rejected'
$b=(& $invoke -PromptText 'delete the file and publish release' -Mode auto-fast)|ConvertFrom-Json
Add-Case 'safe-upgrade' ($b.invocation.effectiveMode -eq 'auto-safe' -and $b.structuredOutput.status -eq 'review') 'high-risk input upgrades without executing'
$invalid=$false;try{$null=& $invoke -PromptText '   '}catch{$invalid=$_.Exception.Message -match 'PROMPT_TEXT_REQUIRED'}
Add-Case 'invalid-input' $invalid 'empty prompt is rejected'
Add-Case 'permission-denial' (-not $b.invocation.writeAllowed -and -not $b.invocation.runtimeAllowed -and -not $b.invocation.networkAllowed) 'wrapper grants no privileged action'
$a2=(& $invoke -PromptText 'wrap this prompt quickly' -Mode auto-fast)|ConvertFrom-Json
Add-Case 'idempotency' ($a.template.cacheKey -eq $a2.template.cacheKey -and $a.template.rawPromptHash -eq $a2.template.rawPromptHash) 'stable fields repeat'
$c=(& $invoke -PromptText 'wrap this prompt quickly changed' -Mode auto-fast)|ConvertFrom-Json
Add-Case 'hash-invalidation' ($a.template.rawPromptHash -ne $c.template.rawPromptHash -and $a.template.cacheKey -ne $c.template.cacheKey) 'changed input invalidates cache key'
Add-Case 'recovery' ($a2.verifier.status -eq 'passed') 'stateless rerun recovers without cleanup'
$long=('x' * 201);$d=(& $invoke -PromptText $long -Mode auto-fast)|ConvertFrom-Json
Add-Case 'long-text-default-deny' ($d.textPolicy.decision -eq 'reject-wrap' -and $d.verifier.status -eq 'passed' -and $d.structuredOutput.status -eq 'blocked') 'long text without boundary signal is not wrapped'
$marked=('<PROMPT>' + ('x' * 196));$e=(& $invoke -PromptText $marked -Mode auto-fast)|ConvertFrom-Json
Add-Case 'long-text-boundary-allow' ($e.textPolicy.decision -eq 'allow-boundary-wrap' -and $e.textPolicy.middleScanned -eq $false) 'marked long text may be wrapped from boundary sample only'
$f=(& $invoke -PromptText '关闭包装：只读取当前文件' -Mode auto-fast)|ConvertFrom-Json
Add-Case 'explicit-disable' ($f.invocation.autoWrapEnabled -eq $false -and $f.invocation.explicitDisable -eq $true) 'explicit disable selects raw mode'
$failed=@($cases|Where-Object{-not $_.passed})
$receipt=[ordered]@{skillName='es-prompt-engineering';case='specialized-static';status=if($failed.Count -eq 0){'passed'}else{'failed'};evidenceLevel='S2';receiptPath=if($ReportPath){$ReportPath}else{'stdout'};sourceRefs=@('.agents/skills/es-prompt-engineering/SKILL.md','.agents/skills/es-prompt-engineering/scripts/Invoke-ESPromptEngineering.ps1','.agents/skills/es-prompt-engineering/references/prompt-envelope.schema.json');timestampUtc=(Get-Date).ToUniversalTime().ToString('O');passedCount=@($cases|Where-Object{$_.passed}).Count;failedCount=$failed.Count;cases=@($cases);runtimeStatus='not-run';nonClaims=@('No model/provider/Unity/Runtime/release behavior was executed.')}
$json=$receipt|ConvertTo-Json -Depth 10
if($ReportPath){$root=(Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path;$full=[IO.Path]::GetFullPath((Join-Path $root $ReportPath));$allowed=[IO.Path]::GetFullPath((Join-Path $root 'ES/Output/StaticReplay')).TrimEnd('\')+'\';if(-not $full.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'REPORT_PATH_OUTSIDE_ALLOWED_ROOT'};[IO.Directory]::CreateDirectory((Split-Path $full -Parent))|Out-Null;[IO.File]::WriteAllText($full,$json,(New-Object Text.UTF8Encoding($false)))}
$json
if($failed.Count -gt 0){exit 1}
