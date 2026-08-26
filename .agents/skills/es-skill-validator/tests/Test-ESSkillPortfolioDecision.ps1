[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$module = Join-Path $PSScriptRoot '..\scripts\ESPortfolioDecision.psm1'
Import-Module $module -Force

$cases = @(
    @{ name = 'closed'; inner = $true; hard = 0; pending = 0; review = 0; status = 'passed'; decision = 'static-passed'; effect = 'none' },
    @{ name = 'evidence-pending'; inner = $true; hard = 0; pending = 1; review = 1; status = 'review'; decision = 'evidence-pending'; effect = 'claim-cap' },
    @{ name = 'review-only'; inner = $true; hard = 0; pending = 0; review = 1; status = 'review'; decision = 'review'; effect = 'review' },
    @{ name = 'hard-failure'; inner = $true; hard = 1; pending = 0; review = 0; status = 'blocked'; decision = 'blocked'; effect = 'hard-block' },
    @{ name = 'missing-inner'; inner = $false; hard = 0; pending = 0; review = 0; status = 'blocked'; decision = 'validator-error'; effect = 'hard-block' }
)

foreach ($case in $cases) {
    $actual = Resolve-ESPortfolioDecision -InnerResultAvailable $case.inner -HardFailureCount $case.hard -EvidencePendingCount $case.pending -ValidatorReviewCount $case.review
    foreach ($field in @('status', 'decisionStatus', 'effect')) {
        $expectedField = if ($field -eq 'decisionStatus') { 'decision' } else { $field }
        if ([string]$actual.$field -ne [string]$case.$expectedField) { throw "$($case.name) decision mismatch: $field" }
    }
}

$fixture = [pscustomobject][ordered]@{
    catalogHash = 'a' * 64; resourceIndexHash = 'b' * 64; validatorHash = 'c' * 64; innerReportHash = 'd' * 64
    skillCount = 1; staticReadyCount = 1; evidencePendingCount = 1; runtimeRequiredCount = 1; runtimeNotRunCount = 1; validatorReviewCount = 1
    innerResultAvailable = $true; status = 'review'; decisionStatus = 'evidence-pending'; effect = 'claim-cap'; staticStatus = 'static-passed'; evidenceStatus = 'evidence-pending'; runtimeStatus = 'runtime-not-run'; blockingLayer = 'none'
    contractFailures = @(); resourceFailures = @(); validatorFailures = @(); validatorBlocked = @(); validatorNotRun = @()
    sourceRefHashes = [ordered]@{ 'fixture/a' = ('e' * 64) }
}
$first = Get-ESPortfolioProjectionHash -Receipt $fixture
$second = Get-ESPortfolioProjectionHash -Receipt $fixture
if ($first -ne $second -or $first -notmatch '^[0-9a-f]{64}$') { throw 'Portfolio projection hash is not deterministic.' }
$fixture.effect = 'hard-block'
if ((Get-ESPortfolioProjectionHash -Receipt $fixture) -eq $first) { throw 'Portfolio projection hash ignored a decision-field change.' }

Write-Output "PASS: $($cases.Count) Portfolio decision cases and canonical hash binding"
