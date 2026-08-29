param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path)
$ErrorActionPreference = "Stop"
$resolver = Join-Path $ProjectRoot ".agents\scripts\Resolve-ESSuperSemantics.ps1"
$cases = @(
    @{ Text = "给我看"; Status = "triggered" },
    @{ Text = "只读"; Status = "not-triggered" },
    @{ Text = "只读检查"; Status = "triggered" },
    @{ Text = "聚焦"; Status = "triggered" },
    @{ Text = "注意编码格式"; Status = "not-triggered" },
    @{ Text = "这个结果不是零分"; Status = "not-triggered" },
    @{ Text = "并非0分结果"; Status = "not-triggered" },
    @{ Text = "0分以上才算失败"; Status = "not-triggered" },
    @{ Text = "取消"; Status = "cancelled" },
    @{ Text = "C"; Status = "cancelled" },
    @{ Text = "不用"; Status = "cancelled" },
    @{ Text = "一般"; Status = "cancelled" },
    @{ Text = "快速"; Status = "cancelled" }
    ,@{ Text = "迭代器报错"; Status = "not-triggered" }
    ,@{ Text = "兼容性是什么意思"; Status = "not-triggered" }
)
foreach ($case in $cases) {
    $result = & $resolver -ProjectRoot $ProjectRoot -PromptText $case.Text | ConvertFrom-Json
    if ($result.status -ne $case.Status) { throw "Unexpected status for test case." }
}
$cancelled = & $resolver -ProjectRoot $ProjectRoot -PromptText "取消" | ConvertFrom-Json
if ($cancelled.suppressionRounds -ne 3) { throw "Default suppression rounds are incorrect." }
$combined = & $resolver -ProjectRoot $ProjectRoot -PromptText "给我看，只读检查" | ConvertFrom-Json
if ($combined.status -ne "triggered" -or $combined.selected.operation -ne "ReadOnlyGuard") { throw "Composable interaction semantics failed." }
if ($combined.selected.requiresUserChoice -ne $true -or $combined.selected.activationMode -ne "confirm-once") { throw "Read-only confirmation gate is missing." }
if ($combined.requiresUserChoice -ne $true) { throw "Top-level confirmation gate is missing." }
$evidenceReview = & $resolver -ProjectRoot $ProjectRoot -PromptText "给我看，检查机制证据够不够" | ConvertFrom-Json
if ($evidenceReview.status -ne "triggered" -or $evidenceReview.selected.operation -ne "EvidenceReview") { throw "Evidence presentation composition was blocked." }
$mixedHighRisk = & $resolver -ProjectRoot $ProjectRoot -PromptText "包装并迭代一下" | ConvertFrom-Json
if ($mixedHighRisk.selected.operation -ne "DeepUserRealignment" -or $mixedHighRisk.authorityDecision -ne "high-risk-over-wrapper") { throw "High-risk semantic did not override wrapper." }
$highRiskConflict = & $resolver -ProjectRoot $ProjectRoot -PromptText "包装并迭代兼容" | ConvertFrom-Json
if ($highRiskConflict.status -ne "ambiguous" -or $highRiskConflict.finalDisposition -ne "clarify-high-risk-conflict") { throw "High-risk same-layer conflict was silently selected." }
$wrapper = & $resolver -ProjectRoot $ProjectRoot -PromptText "包装" | ConvertFrom-Json
if ($wrapper.status -ne "triggered" -or $wrapper.selected.operation -ne "PromptAutoWrapToggle") { throw "Ordinary wrapper semantic failed." }
$delegatedWrapper = & $resolver -ProjectRoot $ProjectRoot -PromptText "帮我包装" | ConvertFrom-Json
if ($delegatedWrapper.selected.executionIntent -ne "delegated-current-window") { throw "Delegated wrapper intent failed." }
$outOfScopeWrapper = & $resolver -ProjectRoot $ProjectRoot -ResponsibilityKey "ui-authoring" -PromptText "帮我包装" | ConvertFrom-Json
if ($outOfScopeWrapper.status -ne "review" -or $outOfScopeWrapper.executionIntent -ne "review-responsibility-out-of-scope") { throw "Out-of-scope wrapper was not sent to review." }
$p0 = & $resolver -ProjectRoot $ProjectRoot -PromptText "包装 + 超级语义不准，请你修复" | ConvertFrom-Json
if ($p0.selected.operation -ne "P0Feedback" -or $p0.authorityDecision -ne "p0-feedback-override") { throw "P0 did not override wrapper." }
$ordinaryFix = & $resolver -ProjectRoot $ProjectRoot -PromptText "请你修复包装问题" | ConvertFrom-Json
if ($ordinaryFix.selected.operation -eq "P0Feedback") { throw "Ordinary wrapper fix was incorrectly upgraded to P0." }
$zeroP0 = & $resolver -ProjectRoot $ProjectRoot -PromptText "我给零分" | ConvertFrom-Json
if ($zeroP0.selected.operation -ne "P0Feedback" -or $zeroP0.requiresUserChoice -ne $true) { throw "Zero-score feedback did not take P0 precedence." }
$longWrapperText = ("包装" + ("x" * 250))
$longWrapper = & $resolver -ProjectRoot $ProjectRoot -PromptText $longWrapperText | ConvertFrom-Json
if ($longWrapper.status -ne "review" -or $longWrapper.selected.reviewReason -ne "long-text-boundary-signal-missing") { throw "Long wrapper without boundary was not reviewed." }
$middleBoundary = & $resolver -ProjectRoot $ProjectRoot -PromptText ("包装" + ("x" * 120) + "【包裹文字】" + ("y" * 120)) | ConvertFrom-Json
if ($middleBoundary.status -ne "review") { throw "Middle boundary signal bypassed long-text sampling." }
$historical = & $resolver -ProjectRoot $ProjectRoot -PromptText "引用历史的超级语义错误，不是当前问题" | ConvertFrom-Json
if ($historical.selected.operation -eq "P0Feedback") { throw "Historical P0 reference was treated as current feedback." }
$focus = & $resolver -ProjectRoot $ProjectRoot -PromptText "聚焦" | ConvertFrom-Json
if ($focus.status -ne "triggered" -or $focus.focusActivation.defaultDecision -ne "confirm" -or $focus.focusActivation.decisionPolicy -ne "non-blocking-default-confirm" -or $focus.focusActivation.appliesTo -ne "latest-user-message") { throw "Focus activation default contract is missing." }
if (-not [bool]$focus.focusActivation.scopeExpansionRequiresGuidance) { throw "Focus scope expansion must retain explicit guidance." }
$wrapText = [string]([char]0x5305)+[char]0x88C5
$promptText = [string]([char]0x63D0)+[char]0x793A+[char]0x8BCD
$wrap = & $resolver -ProjectRoot $ProjectRoot -PromptText $wrapText | ConvertFrom-Json
if ($wrap.status -ne "triggered" -or $wrap.selected.operation -ne "PromptAutoWrapToggle") { throw "Prompt auto-wrap semantic is missing." }
$prompt = & $resolver -ProjectRoot $ProjectRoot -PromptText $promptText | ConvertFrom-Json
if ($prompt.status -ne "triggered" -or $prompt.selected.operation -ne "PromptAutoWrapToggle") { throw "Prompt semantic alias is missing." }
Write-Output "passed"






