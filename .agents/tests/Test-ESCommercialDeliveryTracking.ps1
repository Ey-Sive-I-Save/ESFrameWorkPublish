[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$commercialScript = Join-Path $projectRoot '.agents/skills/es-skill-governance/scripts/Test-ESCommercialCoherence.ps1'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($commercialScript, [ref]$tokens, [ref]$parseErrors)
if (@($parseErrors).Count -gt 0) {
    throw "Commercial coherence script has parser errors: $(@($parseErrors).Message -join '; ')"
}
$classifier = $ast.Find({
    param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Resolve-ESDeliveryArtifactVersionState'
}, $true)
if ($null -eq $classifier) { throw 'Delivery artifact version-state classifier is missing.' }
Set-Item -LiteralPath 'Function:\Resolve-ESDeliveryArtifactVersionState' -Value $classifier.Body.GetScriptBlock()

$cases = @(
    @{ Name = 'untracked'; Expected = 'untracked'; Git = $true; Work = $true; WorkId = 'a'; Index = $false; IndexId = ''; Head = $false; HeadId = '' },
    @{ Name = 'index-only staged new'; Expected = 'index-only-staged-new'; Git = $true; Work = $true; WorkId = 'a'; Index = $true; IndexId = 'a'; Head = $false; HeadId = '' },
    @{ Name = 'worktree differs from index'; Expected = 'worktree-differs-from-index'; Git = $true; Work = $true; WorkId = 'b'; Index = $true; IndexId = 'a'; Head = $true; HeadId = 'a' },
    @{ Name = 'index differs from HEAD'; Expected = 'index-differs-from-head'; Git = $true; Work = $true; WorkId = 'b'; Index = $true; IndexId = 'b'; Head = $true; HeadId = 'a' },
    @{ Name = 'committed clean'; Expected = 'committed-clean'; Git = $true; Work = $true; WorkId = 'a'; Index = $true; IndexId = 'a'; Head = $true; HeadId = 'a' },
    @{ Name = 'worktree missing'; Expected = 'worktree-missing'; Git = $true; Work = $false; WorkId = ''; Index = $true; IndexId = 'a'; Head = $true; HeadId = 'a' },
    @{ Name = 'git unavailable'; Expected = 'git-error'; Git = $false; Work = $true; WorkId = ''; Index = $false; IndexId = ''; Head = $false; HeadId = '' }
)
foreach ($case in $cases) {
    $actual = Resolve-ESDeliveryArtifactVersionState -GitAvailable $case.Git -WorktreeExists $case.Work -WorktreeObjectId $case.WorkId -TrackedInIndex $case.Index -IndexObjectId $case.IndexId -TrackedInHead $case.Head -HeadObjectId $case.HeadId
    if ($actual -cne $case.Expected) {
        throw "Delivery state '$($case.Name)' classified as '$actual' instead of '$($case.Expected)'."
    }
}

$scriptText = [IO.File]::ReadAllText($commercialScript, [Text.UTF8Encoding]::new($false, $true))
foreach ($requiredMarker in @(
    'artifactVersionStates = $deliveryArtifactVersionStates',
    "versionState -ne 'committed-clean'",
    'currentCommitCarriesWorktree = $currentCommitCarriesWorktree',
    'if ($deliveryTrackingStatus -eq ''passed'') {',
    'nextAction = if ($overall -eq ''static-coherent'')',
    'Remote publication or cross-machine availability of the current local HEAD commit'
)) {
    if ($scriptText.IndexOf($requiredMarker, [StringComparison]::Ordinal) -lt 0) {
        throw "Commercial delivery tracking wiring is missing: $requiredMarker"
    }
}
if ($scriptText.Contains('Critical governance artifact version-control tracking')) {
    throw 'Commercial claimsProven still contains the unconditional legacy delivery-tracking claim.'
}

Write-Output 'PASS: commercial delivery tracking distinguishes untracked, staged-only, dirty-index, staged-change, and committed-clean states without Git writes.'
