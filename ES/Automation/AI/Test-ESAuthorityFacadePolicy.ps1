[CmdletBinding()]
param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
$facade = Join-Path $root 'Assets/Plugins/ES/Editor/ESAutomation/ESAutomationFacade.cs'
$ui = Join-Path $root 'Assets/Scripts/ESLogic/Editor/UI/ESUIAutomationMaterializerEndpoint.cs'
$multi = Join-Path $root 'Assets/Plugins/ES/Editor/ESAutomation/ESCodexMultiLaunchAutomation.cs'
foreach ($path in @($facade,$ui,$multi)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "AUTHORITY_POLICY_SOURCE_MISSING:$path" }
}

$facadeText = Get-Content -LiteralPath $facade -Raw -Encoding UTF8
$uiText = Get-Content -LiteralPath $ui -Raw -Encoding UTF8
$multiText = Get-Content -LiteralPath $multi -Raw -Encoding UTF8
$issues = [Collections.Generic.List[string]]::new()

if ($facadeText -notmatch 'private static bool RequiresStrictAuthority\s*\(') { [void]$issues.Add('missing-strict-capability-function') }
foreach ($capability in @('MaterializeUI','WriteAssets','Delete','Publish','ExternalWrite')) {
    if ($facadeText -notmatch [regex]::Escape("ESAutomationCapability.$capability")) { [void]$issues.Add("strict-capability-missing:$capability") }
}
if ($facadeText -notmatch 'invocation\.fromAi\s*&&\s*RequiresStrictAuthority') { [void]$issues.Add('strict-gate-not-ai-scoped') }
foreach ($domain in @('game-logic','editor-tooling','release')) {
    if ($facadeText -notmatch [regex]::Escape("authorityCriteria.authorityDomain != `"$domain`"")) { [void]$issues.Add("strict-domain-missing:$domain") }
}
if ($facadeText -notmatch 'authorityCriteria\.authorityRiskClass') { [void]$issues.Add('strict-risk-check-missing') }
if ($facadeText.IndexOf('authorityDomain/authorityRiskClass', [StringComparison]::Ordinal) -lt 0) { [void]$issues.Add('strict-block-reason-missing') }
if ($facadeText -notmatch 'ai-collaboration') { [void]$issues.Add('lenient-domain-compatibility-not-visible') }

foreach ($pair in @(
    @{Text=$uiText; Name='ui-materializer'; Domain='editor-tooling'; Risk='high'},
    @{Text=$multiText; Name='codex-multilaunch'; Domain='editor-tooling'; Risk='high'}
)) {
    if ($pair.Text -notmatch [regex]::Escape("authorityDomain = `"$($pair.Domain)`"")) { [void]$issues.Add("$($pair.Name)-domain-missing") }
    if ($pair.Text -notmatch [regex]::Escape("authorityRiskClass = `"$($pair.Risk)`"")) { [void]$issues.Add("$($pair.Name)-risk-missing") }
    if ($pair.Text -notmatch 'acceptanceCriteria\s*=\s*new\s+ESAutomationAcceptanceCriteria') { [void]$issues.Add("$($pair.Name)-criteria-missing") }
}

$result = [ordered]@{
    schemaVersion = 1
    validator = 'Test-ESAuthorityFacadePolicy'
    status = if ($issues.Count) { 'failed' } else { 'passed' }
    strictCapabilities = @('MaterializeUI','WriteAssets','Delete','Publish','ExternalWrite')
    strictDomains = @('game-logic','editor-tooling','release')
    checkedSources = @(
        $facade.Substring($root.Length).TrimStart('\','/').Replace('\','/'),
        $ui.Substring($root.Length).TrimStart('\','/').Replace('\','/'),
        $multi.Substring($root.Length).TrimStart('\','/').Replace('\','/')
    )
    issues = @($issues)
    runtimeStatus = 'runtime-not-run'
    claimsNotProven = @('Unity compilation','runtime invocation behavior')
    capturedUtc = [DateTime]::UtcNow.ToString('o')
}
$result | ConvertTo-Json -Depth 8
if ($issues.Count) { exit 1 }
