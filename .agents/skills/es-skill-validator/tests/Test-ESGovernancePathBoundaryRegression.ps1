[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$governanceScripts = Join-Path $projectRoot '.agents/skills/es-skill-governance/scripts'
$pathBoundary = Join-Path $governanceScripts 'ESPathBoundary.Common.ps1'
. $pathBoundary

$valid = Resolve-ESContainedRelativePath -Candidate 'ES/Output/Governance/fixture.json' -ContainerRoot $projectRoot -Label 'fixture'
if (-not $valid.FullPath.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Valid project-relative path was not contained.'
}

$skillRoot = Join-Path $projectRoot '.agents/skills/es-skill-governance'
$invalidCases = @(
    @{ Candidate = '../outside.json'; Root = $projectRoot; Label = 'project traversal' },
    @{ Candidate = '../other-skill/evidence.json'; Root = $skillRoot; Label = 'Skill-root traversal' },
    @{ Candidate = 'ES/Output/report.json:stream'; Root = $projectRoot; Label = 'alternate data stream' },
    @{ Candidate = (Join-Path $projectRoot 'ES/Output/absolute.json'); Root = $projectRoot; Label = 'absolute path' }
)
foreach ($case in $invalidCases) {
    $rejected = $false
    try {
        $null = Resolve-ESContainedRelativePath -Candidate $case.Candidate -ContainerRoot $case.Root -Label $case.Label
    } catch {
        $rejected = $true
    }
    if (-not $rejected) { throw "Unsafe path was accepted: $($case.Label)" }
}

$portfolioScript = Join-Path $projectRoot '.agents/skills/es-skill-validator/scripts/Test-ESSkillPortfolio.ps1'
$portfolioValidator = Join-Path $projectRoot '.agents/skills/es-skill-validator/scripts/Invoke-ESSkillValidation.ps1'
$commercialScript = Join-Path $governanceScripts 'Test-ESCommercialCoherence.ps1'
$coverageScript = Join-Path $governanceScripts 'Test-ESStaticAcceptanceCoverage.ps1'
if (-not (Test-Path -LiteralPath $portfolioValidator -PathType Leaf)) { throw 'Portfolio validator prerequisite is missing.' }
$managedReportScripts = @(
    @{ Name = 'Test-ESSkillPortfolio.ps1'; Path = $portfolioScript; Parameter = 'ReportPath'; GuardMarker = '$reportTarget = Resolve-ESPortfolioOutputPath'; WorkMarker = '$skillDirs =' },
    @{ Name = 'Test-ESCommercialCoherence.ps1'; Path = $commercialScript; Parameter = 'OutputPath'; GuardMarker = '$outputTarget = Resolve-ESCommercialOutputPath'; WorkMarker = '$architecturePath =' }
)

foreach ($script in $managedReportScripts) {
    $scriptText = [IO.File]::ReadAllText($script.Path, [Text.UTF8Encoding]::new($false, $true))
    if ($scriptText -notmatch 'ESPathBoundary\.Common\.ps1') { throw "$($script.Name) does not load the shared path boundary." }
    if ($scriptText -notmatch 'Resolve-ESContainedRelativePath') { throw "$($script.Name) does not enforce the shared path boundary." }
    if ($scriptText -notmatch "StartsWith\('ES/Output/'") { throw "$($script.Name) does not restrict reports to ES/Output." }
    if ($scriptText -notmatch 'Move-Item\s+-LiteralPath\s+\$temporary\s+-Destination') { throw "$($script.Name) does not replace reports atomically." }
    $guardIndex = $scriptText.IndexOf($script.GuardMarker, [StringComparison]::Ordinal)
    $workIndex = $scriptText.IndexOf($script.WorkMarker, [StringComparison]::Ordinal)
    if ($guardIndex -lt 0 -or $workIndex -lt 0 -or $guardIndex -gt $workIndex) {
        throw "$($script.Name) does not reject unsafe output before running its aggregate work."
    }
}

foreach ($scriptName in @('Test-ESAutomationCompatibility.ps1', 'Test-ESStaticAcceptanceCoverage.ps1', 'Test-ESSkillArchitecture.ps1')) {
    $scriptText = [IO.File]::ReadAllText((Join-Path $governanceScripts $scriptName), [Text.UTF8Encoding]::new($false, $true))
    if ($scriptText -notmatch 'Resolve-ESContainedRelativePath') { throw "$scriptName does not enforce the shared path boundary." }
    if ($scriptText -notmatch 'Move-Item\s+-LiteralPath\s+\$temporary\s+-Destination') { throw "$scriptName does not replace reports atomically." }
}

$hostExecutable = if ($PSVersionTable.PSEdition -eq 'Core') {
    Join-Path $PSHOME 'pwsh.exe'
} else {
    Join-Path $PSHOME 'powershell.exe'
}
if (-not (Test-Path -LiteralPath $hostExecutable -PathType Leaf)) { throw "PowerShell host executable is missing: $hostExecutable" }

$previousPreference = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    & $hostExecutable -NoProfile -File $coverageScript -OutputPath 'ES/Output/Governance/missing-root.json' *> $null
    $missingRootExit = $LASTEXITCODE
} finally {
    $ErrorActionPreference = $previousPreference
}
if ($missingRootExit -ne 2) {
    throw "Static acceptance coverage misclassified a missing ProjectRoot parameter (exit $missingRootExit)."
}

$scriptInvalidCases = @(
    @{ Candidate = '../outside.json'; Label = 'project traversal' },
    @{ Candidate = 'ES/Output/../../outside.json'; Label = 'managed-root traversal' },
    @{ Candidate = '.agents/outside.json'; Label = 'outside ES/Output' },
    @{ Candidate = 'ES/Output/report.json:stream'; Label = 'alternate data stream' },
    @{ Candidate = (Join-Path $projectRoot 'ES/Output/absolute.json'); Label = 'absolute path' }
)

function Assert-ReportPathRejected($Script, [string]$Candidate, [string]$Label) {
    $arguments = @(
        '-NoProfile',
        '-File', $Script.Path,
        '-ProjectRoot', $projectRoot,
        ('-' + $Script.Parameter), $Candidate
    )
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $hostExecutable @arguments *> $null
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousPreference
    }
    if ($exitCode -ne 2) {
        throw "$($Script.Name) accepted or misclassified unsafe $Label output path (exit $exitCode)."
    }
}

foreach ($script in $managedReportScripts) {
    foreach ($case in $scriptInvalidCases) {
        Assert-ReportPathRejected -Script $script -Candidate $case.Candidate -Label $case.Label
    }
}

$outputRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'ES/Output')).TrimEnd('\', '/')
$fixtureRoot = Join-Path $outputRoot ('GovernancePathBoundaryRegression-' + [Guid]::NewGuid().ToString('N'))
$fixturePrefix = $outputRoot + [IO.Path]::DirectorySeparatorChar
if (-not $fixtureRoot.StartsWith($fixturePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to create an out-of-bound reparse regression fixture.'
}
$junctionPath = Join-Path $fixtureRoot 'report-link'
try {
    $junctionTarget = Join-Path $fixtureRoot 'junction-target'
    New-Item -ItemType Directory -Path $junctionTarget -Force | Out-Null
    New-Item -ItemType Junction -Path $junctionPath -Target $junctionTarget | Out-Null
    if (((Get-Item -LiteralPath $junctionPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) {
        throw 'Could not create the junction regression fixture.'
    }
    $relativeFixture = $fixtureRoot.Substring($projectRoot.TrimEnd('\', '/').Length).TrimStart('\', '/').Replace('\', '/')
    foreach ($script in $managedReportScripts) {
        Assert-ReportPathRejected -Script $script -Candidate ($relativeFixture + '/report-link/report.json') -Label 'reparse point'
    }
} finally {
    if (Test-Path -LiteralPath $junctionPath) {
        $junction = Get-Item -LiteralPath $junctionPath -Force
        if (($junction.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            [IO.Directory]::Delete($junctionPath)
        }
    }
    if (Test-Path -LiteralPath $fixtureRoot) {
        $resolvedFixture = [IO.Path]::GetFullPath($fixtureRoot)
        if (-not $resolvedFixture.StartsWith($fixturePrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Refusing to remove an out-of-bound reparse regression fixture.'
        }
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
}

$coverageFixtureRoot = Join-Path $outputRoot ('StaticAcceptanceCoverageRegression-' + [Guid]::NewGuid().ToString('N'))
if (-not $coverageFixtureRoot.StartsWith($fixturePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to create an out-of-bound static acceptance fixture.'
}
$coverageSkillRoot = Join-Path $coverageFixtureRoot '.agents/skills/es-coverage-fixture'
$coverageReferences = Join-Path $coverageSkillRoot 'references'
$coverageManifest = Join-Path $coverageSkillRoot 'static-replay.manifest.json'
$coverageProjectEvidence = Join-Path $coverageFixtureRoot '.agents/project-evidence.txt'
$coverageReportRelative = 'ES/Output/Governance/static-acceptance-coverage.json'
$coverageReport = Join-Path $coverageFixtureRoot ($coverageReportRelative.Replace('/', '\'))
$escapedReportName = 'static-acceptance-escape-' + [Guid]::NewGuid().ToString('N') + '.json'
$escapedReport = Join-Path $outputRoot $escapedReportName
$strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
$utf8NoBom = [Text.UTF8Encoding]::new($false)

function Invoke-StaticAcceptanceFixture([string]$OutputPath, [string]$FaultInjection = '') {
    $arguments = @(
        '-NoProfile',
        '-File', $coverageScript,
        '-ProjectRoot', $coverageFixtureRoot,
        '-OutputPath', $OutputPath
    )
    if (-not [string]::IsNullOrWhiteSpace($FaultInjection)) {
        $arguments += @('-FaultInjection', $FaultInjection)
    }
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $hostExecutable @arguments *> $null
        return $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousPreference
    }
}

try {
    New-Item -ItemType Directory -Path $coverageReferences -Force | Out-Null
    New-Item -ItemType Directory -Path (Split-Path -Parent $coverageReport) -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $coverageSkillRoot 'SKILL.md'), "# Fixture`n", $utf8NoBom)
    [IO.File]::WriteAllText((Join-Path $coverageReferences 'guidance.md'), "# Guidance`n", $utf8NoBom)
    [IO.File]::WriteAllText((Join-Path $coverageReferences 'evidence.txt'), "fixture`n", $utf8NoBom)
    [IO.File]::WriteAllText($coverageProjectEvidence, "project fixture`n", $utf8NoBom)

    $staleInvocationId = 'stale-passed-invocation'
    $staleGeneratedUtc = '2000-01-01T00:00:00.0000000+00:00'
    $stalePassedReport = [ordered]@{
        sentinel = 'stale-report'
        invocationId = $staleInvocationId
        generatedUtc = $staleGeneratedUtc
        status = 'passed'
    }
    [IO.File]::WriteAllText($coverageReport, ($stalePassedReport | ConvertTo-Json), $utf8NoBom)
    $faultExit = Invoke-StaticAcceptanceFixture -OutputPath $coverageReportRelative -FaultInjection 'after-sentinel'
    if ($faultExit -ne 1) { throw "Injected static acceptance failure returned exit $faultExit instead of 1." }
    $faultReport = [IO.File]::ReadAllText($coverageReport, $strictUtf8) | ConvertFrom-Json
    if ($faultReport.status -ne 'blocked' -or $faultReport.phase -ne 'failed') {
        throw 'Injected static acceptance failure did not leave a failed-closed current report.'
    }
    if ([string]::IsNullOrWhiteSpace([string]$faultReport.invocationId) -or $faultReport.invocationId -eq $staleInvocationId) {
        throw 'Injected static acceptance failure retained the stale passed invocation.'
    }
    if ($faultReport.generatedUtc -eq $staleGeneratedUtc -or $null -ne $faultReport.PSObject.Properties['sentinel']) {
        throw 'Injected static acceptance failure retained stale report content or timestamp.'
    }
    $parsedFaultTimestamp = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParse([string]$faultReport.generatedUtc, [ref]$parsedFaultTimestamp)) {
        throw 'Injected static acceptance failure did not write a parseable current timestamp.'
    }
    if (@($faultReport.validatorFindings) -notmatch 'Injected failure after current-invocation sentinel') {
        throw 'Injected static acceptance failure did not record the current failure finding.'
    }
    $faultInvocationId = [string]$faultReport.invocationId

    $incompleteManifest = [ordered]@{
        schemaVersion = 1
        skillName = 'es-coverage-fixture'
        responsibilityProfile = 'fixture'
        specializedAcceptance = [ordered]@{ id = 'fixture-acceptance' }
    }
    [IO.File]::WriteAllText($coverageManifest, ($incompleteManifest | ConvertTo-Json -Depth 8), $utf8NoBom)

    $blockedExit = Invoke-StaticAcceptanceFixture -OutputPath $coverageReportRelative
    if ($blockedExit -ne 1) { throw "Missing static acceptance metadata returned exit $blockedExit instead of 1." }
    $blockedReport = [IO.File]::ReadAllText($coverageReport, $strictUtf8) | ConvertFrom-Json
    if ($blockedReport.status -ne 'blocked' -or $blockedReport.blockedSkillCount -ne 1) {
        throw 'Missing static acceptance metadata did not write a current blocked report.'
    }
    if ($null -ne $blockedReport.PSObject.Properties['sentinel']) {
        throw 'The blocked static acceptance run retained the stale report.'
    }
    if ([string]$blockedReport.invocationId -eq $faultInvocationId -or $blockedReport.phase -ne 'completed') {
        throw 'The missing-metadata run did not write its own completed invocation report.'
    }
    $expectedFindings = @(
        'missing runtimeEscalation',
        'runtimeClaimsNotProven must not be empty',
        'no responsibility-specific static cases',
        'no evidence artifacts',
        'missing specializedGuidanceRef'
    )
    $actualFindings = @(@($blockedReport.results)[0].findings)
    if (($actualFindings -join "`n") -cne ($expectedFindings -join "`n")) {
        throw "Missing metadata findings were not deterministic. Actual: $($actualFindings -join '; ')"
    }

    $invalidCollections = @(
        [pscustomobject]@{ Name = 'null'; Value = $null; ExpectsEmptyArtifact = $true },
        [pscustomobject]@{ Name = 'empty-array'; Value = @(); ExpectsEmptyArtifact = $false },
        [pscustomobject]@{ Name = 'whitespace'; Value = @('   '); ExpectsEmptyArtifact = $true }
    )
    foreach ($variant in $invalidCollections) {
        $invalidCollectionManifest = [ordered]@{
            schemaVersion = 1
            skillName = 'es-coverage-fixture'
            responsibilityProfile = 'fixture'
            runtimeEscalation = [ordered]@{ required = $true; reason = 'Runtime evidence requires a separate run.' }
            runtimeClaimsNotProven = $variant.Value
            specializedAcceptance = [ordered]@{
                id = 'fixture-acceptance'
                requiredStaticCases = $variant.Value
                evidenceArtifacts = $variant.Value
            }
            specializedGuidanceRef = 'references/guidance.md'
        }
        [IO.File]::WriteAllText($coverageManifest, ($invalidCollectionManifest | ConvertTo-Json -Depth 8), $utf8NoBom)
        $invalidCollectionExit = Invoke-StaticAcceptanceFixture -OutputPath $coverageReportRelative
        if ($invalidCollectionExit -ne 1) { throw "The $($variant.Name) collection fixture returned exit $invalidCollectionExit instead of 1." }
        $invalidCollectionReport = [IO.File]::ReadAllText($coverageReport, $strictUtf8) | ConvertFrom-Json
        $invalidCollectionFindings = @(@($invalidCollectionReport.results)[0].findings)
        foreach ($finding in @('runtimeClaimsNotProven must not be empty', 'no responsibility-specific static cases', 'no evidence artifacts')) {
            if ($invalidCollectionFindings -cnotcontains $finding) {
                throw "The $($variant.Name) collection fixture omitted finding: $finding"
            }
        }
        $hasEmptyArtifactFinding = $invalidCollectionFindings -ccontains 'empty evidence artifact reference'
        if ($hasEmptyArtifactFinding -ne $variant.ExpectsEmptyArtifact) {
            throw "The $($variant.Name) collection fixture produced the wrong empty artifact finding state."
        }
    }

    $completeManifest = [ordered]@{
        schemaVersion = 1
        skillName = 'es-coverage-fixture'
        responsibilityProfile = 'fixture'
        runtimeEscalation = [ordered]@{ required = $true; reason = 'Runtime evidence requires a separate run.' }
        runtimeClaimsNotProven = @('runtime fixture behavior')
        specializedAcceptance = [ordered]@{
            id = 'fixture-acceptance'
            requiredStaticCases = @('fixture-case')
            evidenceArtifacts = @('references/evidence.txt', '../../project-evidence.txt')
        }
        specializedGuidanceRef = 'references/guidance.md'
    }
    [IO.File]::WriteAllText($coverageManifest, ($completeManifest | ConvertTo-Json -Depth 8), $utf8NoBom)

    $passedExit = Invoke-StaticAcceptanceFixture -OutputPath $coverageReportRelative
    if ($passedExit -ne 0) { throw "Complete static acceptance metadata returned exit $passedExit instead of 0." }
    $passedReport = [IO.File]::ReadAllText($coverageReport, $strictUtf8) | ConvertFrom-Json
    if ($passedReport.status -ne 'passed' -or $passedReport.coveredSkillCount -ne 1 -or $passedReport.blockedSkillCount -ne 0 -or @($passedReport.results)[0].evidenceArtifactCount -ne 2) {
        throw 'Complete static acceptance metadata did not write a passed report.'
    }

    $completeManifest.specializedAcceptance.evidenceArtifacts = @('../../../../escape.txt')
    [IO.File]::WriteAllText($coverageManifest, ($completeManifest | ConvertTo-Json -Depth 8), $utf8NoBom)
    $artifactEscapeExit = Invoke-StaticAcceptanceFixture -OutputPath $coverageReportRelative
    if ($artifactEscapeExit -ne 1) { throw "Out-of-project evidence artifact returned exit $artifactEscapeExit instead of 1." }
    $artifactEscapeReport = [IO.File]::ReadAllText($coverageReport, $strictUtf8) | ConvertFrom-Json
    if (@(@($artifactEscapeReport.results)[0].findings) -cnotcontains 'invalid evidence artifact path: ../../../../escape.txt') {
        throw 'Out-of-project evidence artifact was not rejected.'
    }

    $escapedExit = Invoke-StaticAcceptanceFixture -OutputPath ('../' + $escapedReportName)
    if ($escapedExit -ne 2) { throw "Out-of-bound static acceptance output returned exit $escapedExit instead of 2." }
    if (Test-Path -LiteralPath $escapedReport) { throw 'Out-of-bound static acceptance output created an escaped report.' }
} finally {
    if (Test-Path -LiteralPath $coverageFixtureRoot) {
        $resolvedCoverageFixture = [IO.Path]::GetFullPath($coverageFixtureRoot)
        if (-not $resolvedCoverageFixture.StartsWith($fixturePrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Refusing to remove an out-of-bound static acceptance fixture.'
        }
        Remove-Item -LiteralPath $resolvedCoverageFixture -Recurse -Force
    }
    if (Test-Path -LiteralPath $escapedReport) {
        $resolvedEscapedReport = [IO.Path]::GetFullPath($escapedReport)
        if (-not $resolvedEscapedReport.StartsWith($fixturePrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Refusing to remove an out-of-bound escaped report fixture.'
        }
        Remove-Item -LiteralPath $resolvedEscapedReport -Force
    }
}

Write-Output 'PASS: governance report paths reject unsafe outputs and static acceptance coverage fails closed on incomplete metadata.'
