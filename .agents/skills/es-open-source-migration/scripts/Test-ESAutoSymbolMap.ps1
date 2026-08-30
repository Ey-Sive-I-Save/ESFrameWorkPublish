[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
    [string]$ReportPath = 'ES/Output/StaticReplay/es-auto-symbol-map.json'
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$projectFull = (Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\')
$generator = Join-Path $PSScriptRoot 'New-ESTransparentSymbolMap.ps1'
$remapper = Join-Path $PSScriptRoot 'Invoke-ESTransparentNamespaceRemap.ps1'
$oneShot = Join-Path $PSScriptRoot 'Invoke-ESAutoTransparentNamespaceRemap.ps1'
$reportFull = [IO.Path]::GetFullPath((Join-Path $projectFull $ReportPath))
if (-not $reportFull.StartsWith($projectFull + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'ReportPath must remain inside ProjectRoot.' }
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('es-auto-map-' + [Guid]::NewGuid().ToString('N'))
$source = Join-Path $testRoot 'source'
$mapPath = Join-Path $testRoot 'generated-map.json'
$output = Join-Path $testRoot 'output'
$results = [Collections.Generic.List[object]]::new()
$esStudio = 'ES' + [char]0x5DE5 + [char]0x4F5C + [char]0x5BA4

function Add-Case([string]$Id, [bool]$Passed, [string]$Detail) {
    $results.Add([pscustomobject]@{ id = $Id; status = if ($Passed) { 'passed' } else { 'failed' }; detail = $Detail })
}

function Invoke-ExpectFailure([string]$Id, [scriptblock]$Action) {
    try { & $Action | Out-Null; Add-Case $Id $false 'expected failure did not occur' }
    catch { Add-Case $Id $true $_.Exception.Message }
}

try {
    $authoritativeMapPath = Join-Path $projectFull 'ES/AISpace/Public/es-open-source-migration/migration-map.json'
    $authoritativeMap = Get-Content -LiteralPath $authoritativeMapPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $executionContract = $authoritativeMap.executionContract
    Add-Case 'authority-execution-contract' (
        $authoritativeMap.scope.mode -eq 'external-checkout-in-place' -and
        $executionContract.mode -eq 'external-checkout-in-place' -and
        [bool]$executionContract.wholeRepository -and
        [bool]$executionContract.renamePathSegments -and
        $executionContract.developerName -eq $esStudio -and
        $executionContract.identityHardeningVersion -eq 'v8' -and
        $executionContract.missingStagingPolicy.staging -eq 'recover-before-commit' -and
        $executionContract.missingStagingPolicy.'commit-started' -eq 'fail-closed' -and
        @($executionContract.planHashInputs) -contains 'identityHardeningVersion' -and
        @($executionContract.protectedPaths) -contains 'src/pro/**' -and
        $executionContract.evidenceSeparation.static -match '(?i)static' -and
        $executionContract.evidenceSeparation.runtime -match '(?i)runtime'
    ) 'persistent migration map binds whole-repository, recovery, hash-version, boundary, and evidence authority'
    New-Item -ItemType Directory -Path (Join-Path $source 'src/pro') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $source 'package.json'), '{"name":"@fixture/demo"}', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $source 'main.ts'), '/* class Commented {} */ export class Worker {} export class worker {} export type Alias = string; import type { ExternalType } from "external"; const example = "class Fake {}"; export function run() { return new Worker(); } export function gitOutput() { return "git"; }', [Text.UTF8Encoding]::new($false))
    New-Item -ItemType Directory -Path (Join-Path $source 'Worker') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $source 'Worker/Worker.ts'), 'export class Worker {}', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $source 'docs.py'), @'
"""
class PythonDocOnly {}
"""
class PythonReal:
    pass
'@, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $source 'src/pro/Secret.ts'), 'export class Secret {}', [Text.UTF8Encoding]::new($false))

    $generated = & $generator -SourceRoot $source -OutputMapPath $mapPath -ProjectRoot $ProjectRoot | ConvertFrom-Json
    $map = Get-Content -LiteralPath $mapPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $workerRule = @($map.symbols | Where-Object { $_.source -ceq 'Worker' -and $_.es -ceq 'ESWorker' }).Count -eq 1
    $rootRule = @($map.symbols | Where-Object { $_.source -ceq 'Demo' -and $_.es -ceq 'ESDemo' }).Count -eq 1
    Add-Case 'auto-map-positive' ($generated.status -eq 'passed' -and $workerRule -and $rootRule) "symbols=$($map.symbols.Count)"
    $caseSensitiveRules = @($map.symbols | Where-Object { ($_.source -ceq 'Worker' -and $_.es -ceq 'ESWorker') -or ($_.source -ceq 'worker' -and $_.es -ceq 'ESworker') }).Count -eq 2
    Add-Case 'auto-map-case-sensitive' $caseSensitiveRules 'case-distinct source identities remain distinct'
    $typeAliasRule = @($map.symbols | Where-Object { $_.source -ceq 'Alias' -and $_.es -ceq 'ESAlias' }).Count -eq 1
    $importTypeExcluded = @($map.symbols | Where-Object { $_.source -ceq 'ExternalType' }).Count -eq 0
    Add-Case 'auto-map-type-filter' ($typeAliasRule -and $importTypeExcluded) 'type aliases map; import type names do not'
    $noiseExcluded = @($map.symbols | Where-Object { $_.source -ceq 'Commented' -or $_.source -ceq 'Fake' }).Count -eq 0
    Add-Case 'auto-map-noise-filter' $noiseExcluded 'comments and literals do not manufacture declarations'
    $genericProtected = @($map.symbols | Where-Object { $_.source -ceq 'run' -or $_.source -ceq 'gitOutput' }).Count -eq 0
    Add-Case 'auto-map-protocol-identifiers-protected' $genericProtected 'generic protocol/tooling declarations are not prefixed into executable ESgit/ESrun tokens'
    $pythonDocExcluded = @($map.symbols | Where-Object { $_.source -ceq 'PythonDocOnly' }).Count -eq 0
    $pythonRealIncluded = @($map.symbols | Where-Object { $_.source -ceq 'PythonReal' -and $_.es -ceq 'ESPythonReal' }).Count -eq 1
    Add-Case 'auto-map-triple-quote-filter' ($pythonDocExcluded -and $pythonRealIncluded) 'triple-quoted documentation is ignored while Python declarations remain mapped'

    $remapped = & $remapper -SourceRoot $source -OutputRoot $output -MappingPath $mapPath -ProjectRoot $ProjectRoot -RenamePathSegments | ConvertFrom-Json
    $text = Get-Content -LiteralPath (Join-Path $output 'main.ts') -Raw -Encoding UTF8
    Add-Case 'auto-map-remap-integration' ($remapped.status -eq 'passed' -and $text.Contains('ESWorker')) 'generated map accepted by remapper'
    Add-Case 'auto-map-path-remap' ((Test-Path -LiteralPath (Join-Path $output 'ESWorker/ESWorker.ts')) -and -not (Test-Path -LiteralPath (Join-Path $output 'Worker/Worker.ts'))) 'path segments follow the same explicit identity map'
    Add-Case 'auto-map-license-exclusion' (-not (Test-Path -LiteralPath (Join-Path $output 'src/pro/Secret.ts'))) 'src/pro excluded by remapper'
    $wholeOutput = Join-Path $testRoot 'whole-copy-output'
    $wholeResult = & $remapper -SourceRoot $source -OutputRoot $wholeOutput -MappingPath $mapPath -ProjectRoot $ProjectRoot -WholeRepository -RenamePathSegments | ConvertFrom-Json
    $wholeText = Get-Content -LiteralPath (Join-Path $wholeOutput 'main.ts') -Raw -Encoding UTF8
    Add-Case 'auto-map-copy-whole-code' ($wholeResult.status -eq 'passed' -and $wholeText.Contains('ESWorker')) 'copy compatibility mode applies declaration symbols when WholeRepository is explicit'

    $oneShotOutput = Join-Path $testRoot 'one-shot-output'
    $oneShotResult = & $oneShot -SourceRoot $source -OutputRoot $oneShotOutput -ProjectRoot $ProjectRoot -SourceRevision 'fixture:one-shot' -RenamePathSegments | ConvertFrom-Json
    Add-Case 'auto-map-one-shot' ($oneShotResult.status -eq 'passed' -and $oneShotResult.mapSymbolCount -ge 5 -and (Test-Path -LiteralPath (Join-Path $oneShotOutput 'ESWorker/ESWorker.ts'))) 'one command generates the map and publishes the isolated remap'
    $dryOutput = Join-Path $testRoot 'one-shot-dry-run'
    $dryResult = & $oneShot -SourceRoot $source -OutputRoot $dryOutput -ProjectRoot $ProjectRoot -SourceRevision 'fixture:dry-run' -DryRun | ConvertFrom-Json
    Add-Case 'auto-map-one-shot-dry-run' ($dryResult.status -eq 'not-run' -and $dryResult.remap.supportedFileCount -ge 1 -and -not (Test-Path -LiteralPath $dryOutput)) 'dry-run reports a plan without publishing transformed files'

    # The default contract is direct in-place whole-repository replacement.
    # The fixture covers code, paths, package metadata, docs/comments/UI text,
    # an explicit author token, and the protected LICENSE/src/pro boundaries.
    $inPlaceSource = Join-Path $testRoot 'Dyad'
    New-Item -ItemType Directory -Path (Join-Path $inPlaceSource 'Legacy-Dyad') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $inPlaceSource 'src/pro') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $inPlaceSource 'without-author') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $inPlaceSource 'package.json'), '{"name":"dyad","productName":"Dyad App","author":{"name":"Legacy Author","email":"legacy@example.com"},"publisher":{"name":"Legacy Publisher","email":"publisher@example.com"},"contributors":[{"name":"Legacy Contributor","email":"contributor@example.com"}],"description":"Dyad UI","repository":{"type":"git","url":"https' + '://example.test/dyad"}}', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $inPlaceSource 'without-author/package.json'), '{"name":"fixture-package"}', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $inPlaceSource 'Legacy-Dyad/Dyad-Worker.ts'), @'
// Dyad Legacy Author
export class Worker {}
const title = "Dyad";
const api = getDyadAppPath("DYAD_ENGINE_URL");
const compound = DyadAddDependency;
'@, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $inPlaceSource 'DyadAddDependency.ts'), 'export const DyadAddDependency = "Dyad"; const env = "DYAD_ENGINE_URL";', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $inPlaceSource 'README.md'), 'Dyad Legacy Author UI by wwwillchen/ESdyad-assistant and keppo-bot', [Text.UTF8Encoding]::new($false))
    New-Item -ItemType Directory -Path (Join-Path $inPlaceSource '.github') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $inPlaceSource '.github/legacy.md'), 'wwwillchen ESdyad-assistant keppo-bot', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $inPlaceSource 'LICENSE'), 'Copyright Dyad Legacy Author', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $inPlaceSource 'src/pro/Secret.ts'), 'export class Secret {}', [Text.UTF8Encoding]::new($false))
    try {
        $savedErrorAction = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $inPlaceRaw = @(& $oneShot -SourceRoot $inPlaceSource -ProjectRoot $ProjectRoot -SourceRevision 'fixture:in-place' -SourceTextTokens 'Legacy Author' 2>&1)
        $ErrorActionPreference = $savedErrorAction
        $rawErrors = @($inPlaceRaw | Where-Object { $_ -is [Management.Automation.ErrorRecord] })
        if ($rawErrors.Count -gt 0) { throw ('raw in-place errors: ' + (($rawErrors | ForEach-Object { $_.ToString() }) -join ' | ')) }
        $inPlaceResult = $inPlaceRaw | ConvertFrom-Json
    } catch {
        throw ('in-place fixture execution failed: ' + $_.Exception.Message + ' ' + $_.ScriptStackTrace)
    }
    $inPlaceFile = Join-Path $inPlaceSource 'Legacy-ESDyad/ESDyad-ESWorker.ts'
    $inPlaceText = Get-Content -LiteralPath $inPlaceFile -Raw -Encoding UTF8
    $inPlaceReadme = Get-Content -LiteralPath (Join-Path $inPlaceSource 'README.md') -Raw -Encoding UTF8
    $inPlaceHidden = Get-Content -LiteralPath (Join-Path $inPlaceSource '.github/legacy.md') -Raw -Encoding UTF8
    $inPlacePackage = Get-Content -LiteralPath (Join-Path $inPlaceSource 'package.json') -Raw -Encoding UTF8
    $inPlacePackageObject = $inPlacePackage | ConvertFrom-Json
    $withoutAuthorPackageObject = Get-Content -LiteralPath (Join-Path $inPlaceSource 'without-author/package.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $inPlaceManifest = Get-Content -LiteralPath (Join-Path $inPlaceSource '.es-migration/es-remap-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    Add-Case 'auto-map-in-place-default' ($inPlaceResult.status -eq 'passed' -and $inPlaceResult.mode -eq 'in-place-whole-repository' -and $inPlaceResult.outputRoot -eq $inPlaceSource) 'default one-shot targets the source checkout'
    $wholeTextChecks = @(
        $inPlaceText.Contains(('ESDyad ' + $esStudio)),
        $inPlaceText.Contains('ESWorker'),
        $inPlaceText.Contains('getESdyadAppPath'),
        $inPlaceText.Contains('ESDYAD_ENGINE_URL'),
        $inPlacePackageObject.name -eq 'ESdyad',
        $inPlacePackageObject.productName -eq 'ESDyad App',
        $inPlacePackageObject.author.name -eq $esStudio,
        $null -eq $inPlacePackageObject.author.email,
        $inPlacePackageObject.publisher.name -eq $esStudio,
        $inPlacePackageObject.contributors[0].name -eq $esStudio,
        $withoutAuthorPackageObject.author.name -eq $esStudio,
        -not $inPlaceReadme.Contains('wwwillchen'),
        -not $inPlaceReadme.Contains('ESdyad-assistant'),
        -not $inPlaceReadme.Contains('keppo-bot'),
        -not $inPlaceHidden.Contains('wwwillchen'),
        -not $inPlaceHidden.Contains('ESdyad-assistant'),
        -not $inPlaceHidden.Contains('keppo-bot'),
        $inPlacePackage.Contains('"type":  "git"'),
        $inPlacePackage.Contains('https://example.test/ESdyad')
    )
    Add-Case 'auto-map-in-place-whole-text' (-not ($wholeTextChecks -contains $false)) ('whole-repository mode rewrites compound project/author traces and forces ES studio metadata; checks=' + ($wholeTextChecks -join ','))
    Add-Case 'auto-map-in-place-paths' ((Test-Path -LiteralPath $inPlaceFile) -and -not (Test-Path -LiteralPath (Join-Path $inPlaceSource 'Legacy-Dyad/Dyad-Worker.ts')) -and -not (Test-Path -LiteralPath (Join-Path $inPlaceSource 'Legacy-Dyad'))) 'in-place mode renames path segments, filenames, and removes empty legacy directories'
    Add-Case 'auto-map-in-place-compound-paths' ((Test-Path -LiteralPath (Join-Path $inPlaceSource 'ESDyadAddDependency.ts')) -and -not (Test-Path -LiteralPath (Join-Path $inPlaceSource 'DyadAddDependency.ts')) -and (Get-Content -LiteralPath (Join-Path $inPlaceSource 'ESDyadAddDependency.ts') -Raw -Encoding UTF8).Contains('ESDyadAddDependency') -and (Get-Content -LiteralPath (Join-Path $inPlaceSource 'ESDyadAddDependency.ts') -Raw -Encoding UTF8).Contains('ESDYAD_ENGINE_URL')) 'compound root fragments are remapped in filenames and text even without declaration discovery'
    Add-Case 'auto-map-in-place-boundaries' ((Get-Content -LiteralPath (Join-Path $inPlaceSource 'LICENSE') -Raw -Encoding UTF8) -eq 'Copyright Dyad Legacy Author' -and (Test-Path -LiteralPath (Join-Path $inPlaceSource 'src/pro/Secret.ts')) -and $inPlaceManifest.output.locatorPolicy -like 'in-place*') 'LICENSE and src/pro remain protected while the checkout is mutated'
    $inPlaceReplay = & $oneShot -SourceRoot $inPlaceSource -ProjectRoot $ProjectRoot -SourceRevision 'fixture:in-place' -SourceTextTokens 'Legacy Author' | ConvertFrom-Json
    Add-Case 'auto-map-in-place-replay' ($inPlaceReplay.status -eq 'passed' -and [bool]$inPlaceReplay.mapReceipt.idempotentReplay -and [bool]$inPlaceReplay.remap.receipt.idempotentReplay -and [bool]$inPlaceReplay.remap.receipt.identityHardeningApplied -and $inPlaceReplay.remap.receipt.identityHardeningVersion -eq 'v8' -and $inPlaceReplay.remap.receipt.developerName -eq $esStudio) 'accepted in-place output replays with identity-hardening evidence'
    $staleJournalPath = Join-Path $inPlaceSource '.es-migration/es-remap-journal.json'
    # A stale staging journal may point at a destination that already exists
    # because the checkout was fully transformed before the journal status was
    # persisted.  The explicit staging phase must close this safely; a raw
    # destination-presence check would incorrectly block recovery.
    $staleJournal = [ordered]@{
        schemaVersion = 1
        status = 'staging'
        stagingRoot = (Join-Path $inPlaceSource '.es-migration/.staging-missing-fixture')
        rows = @([ordered]@{ sourceRelativePath = 'DyadAddDependency.ts'; outputRelativePath = 'ESDyadAddDependency.ts' })
    }
    [IO.File]::WriteAllText($staleJournalPath, ($staleJournal | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    $journalRecovery = & $oneShot -SourceRoot $inPlaceSource -ProjectRoot $ProjectRoot -SourceRevision 'fixture:in-place' -SourceTextTokens 'Legacy Author' | ConvertFrom-Json
    $journalAfterRecovery = Get-Content -LiteralPath $staleJournalPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Add-Case 'auto-map-in-place-missing-staging-recovery' ($journalRecovery.status -eq 'passed' -and $journalAfterRecovery.status -eq 'recovered' -and $journalAfterRecovery.phase -eq 'recovered-before-commit' -and [bool]$journalAfterRecovery.recoveredUtc) 'stale staging-phase journal closes safely even when a renamed destination is present'
    $commitStartedJournal = [ordered]@{
        schemaVersion = 1
        status = 'interrupted'
        phase = 'commit-started'
        stagingRoot = (Join-Path $inPlaceSource '.es-migration/.staging-missing-after-commit-fixture')
        rows = @([ordered]@{ sourceRelativePath = 'DyadAddDependency.ts'; outputRelativePath = 'ESDyadAddDependency.ts' })
    }
    [IO.File]::WriteAllText($staleJournalPath, ($commitStartedJournal | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    Invoke-ExpectFailure 'auto-map-in-place-commit-started-missing-staging-blocks' { & $oneShot -SourceRoot $inPlaceSource -ProjectRoot $ProjectRoot -SourceRevision 'fixture:in-place' -SourceTextTokens 'Legacy Author' }
    $safeJournal = [ordered]@{ schemaVersion = 1; status = 'staging'; phase = 'staging'; stagingRoot = (Join-Path $inPlaceSource '.es-migration/.staging-missing-fixture'); rows = @() }
    [IO.File]::WriteAllText($staleJournalPath, ($safeJournal | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    $receiptPath = Join-Path $inPlaceSource '.es-migration/es-remap-receipt.json'
    $tamperedReceipt = Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $tamperedReceipt.identityHardeningVersion = 'v0'
    [IO.File]::WriteAllText($receiptPath, ($tamperedReceipt | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
    $versionReplay = & $oneShot -SourceRoot $inPlaceSource -ProjectRoot $ProjectRoot -SourceRevision 'fixture:in-place' -SourceTextTokens 'Legacy Author' | ConvertFrom-Json
    $versionAfter = Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Add-Case 'auto-map-in-place-hardening-version-drift' ($versionReplay.status -eq 'passed' -and $versionAfter.identityHardeningVersion -eq 'v8' -and [bool]$versionAfter.identityHardeningApplied) 'hardening-version drift invalidates replay and rebuilds the accepted receipt'
    $dryInPlaceSource = Join-Path $testRoot 'DryDyad'
    New-Item -ItemType Directory -Path $dryInPlaceSource -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $dryInPlaceSource 'package.json'), '{"name":"drydyad"}', [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $dryInPlaceSource 'README.md'), 'drydyad', [Text.UTF8Encoding]::new($false))
    $dryInPlaceResult = & $oneShot -SourceRoot $dryInPlaceSource -ProjectRoot $ProjectRoot -SourceRevision 'fixture:in-place-dry-run' -DryRun | ConvertFrom-Json
    Add-Case 'auto-map-in-place-dry-run' ($dryInPlaceResult.status -eq 'not-run' -and -not (Test-Path -LiteralPath (Join-Path $dryInPlaceSource '.es-migration')) -and (Get-Content -LiteralPath (Join-Path $dryInPlaceSource 'README.md') -Raw -Encoding UTF8) -eq 'drydyad') 'in-place dry-run leaves the checkout and control directory untouched'

    $replay = & $generator -SourceRoot $source -OutputMapPath $mapPath -ProjectRoot $ProjectRoot | ConvertFrom-Json
    Add-Case 'auto-map-idempotency' ([bool]$replay.idempotentReplay -and $replay.planHash -eq $generated.planHash) 'same generated plan hash accepted'
    $mapReceiptPath = [IO.Path]::ChangeExtension($mapPath, '.receipt.json')
    [IO.File]::WriteAllText($mapReceiptPath, ('{"status":"tampered","planHash":"' + $generated.planHash + '"}'), [Text.UTF8Encoding]::new($false))
    Invoke-ExpectFailure 'auto-map-receipt-drift' { & $generator -SourceRoot $source -OutputMapPath $mapPath -ProjectRoot $ProjectRoot }

    $recoveryMapPath = Join-Path $testRoot 'recovery-map.json'
    $recoveryReceiptPath = [IO.Path]::ChangeExtension($recoveryMapPath, '.receipt.json')
    [IO.File]::WriteAllText($recoveryReceiptPath, '{"status":"interrupted"}', [Text.UTF8Encoding]::new($false))
    $recovered = & $generator -SourceRoot $source -OutputMapPath $recoveryMapPath -ProjectRoot $ProjectRoot | ConvertFrom-Json
    $recoveryReceipt = Get-Content -LiteralPath $recoveryReceiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Add-Case 'auto-map-interrupted-recovery' ($recovered.status -eq 'passed' -and $recoveryReceipt.status -eq 'passed' -and (Test-Path $recoveryMapPath)) 'stale receipt is safely replaced when map was not published'

    $collisionSource = Join-Path $testRoot 'collision-source'
    New-Item -ItemType Directory -Path $collisionSource -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $collisionSource 'collision.cs'), 'class Foo {} class ESFoo {}', [Text.UTF8Encoding]::new($false))
    Invoke-ExpectFailure 'auto-map-collision' { & $generator -SourceRoot $collisionSource -OutputMapPath (Join-Path $testRoot 'collision-map.json') -ProjectRoot $ProjectRoot }
    Invoke-ExpectFailure 'auto-map-budget' { & $generator -SourceRoot $source -OutputMapPath (Join-Path $testRoot 'budget-map.json') -ProjectRoot $ProjectRoot -MaxFiles 1 }

    $fallbackSource = Join-Path $testRoot 'NoPackageRoot'
    New-Item -ItemType Directory -Path $fallbackSource -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $fallbackSource 'main.cs'), 'public class Hero {}', [Text.UTF8Encoding]::new($false))
    $fallbackMapPath = Join-Path $testRoot 'fallback-map.json'
    $fallback = & $generator -SourceRoot $fallbackSource -OutputMapPath $fallbackMapPath -ProjectRoot $ProjectRoot | ConvertFrom-Json
    $fallbackMap = Get-Content -LiteralPath $fallbackMapPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $fallbackRoot = @($fallbackMap.symbols | Where-Object { $_.source -ceq 'NoPackageRoot' -and $_.es -ceq 'ESNoPackageRoot' }).Count -eq 1
    Add-Case 'auto-map-directory-fallback' ($fallback.status -eq 'passed' -and $fallbackRoot) 'directory name supplies the root when package.json is absent'
}
catch {
    throw ($_.Exception.ToString() + "`n" + $_.InvocationInfo.PositionMessage + "`n" + $_.ScriptStackTrace)
}
finally {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}

$failed = @($results | Where-Object status -eq 'failed')
$capturedUtc = [DateTime]::UtcNow.ToString('o')
$generatorRelative = $generator.Substring($projectFull.Length + 1).Replace('\', '/')
$remapperRelative = $remapper.Substring($projectFull.Length + 1).Replace('\', '/')
$oneShotRelative = $oneShot.Substring($projectFull.Length + 1).Replace('\', '/')
$testRelative = $PSCommandPath.Substring($projectFull.Length + 1).Replace('\', '/')
$authoritativeMapRelative = $authoritativeMapPath.Substring($projectFull.Length + 1).Replace('\', '/')
$contractPath = Join-Path $projectFull 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json'
$contractHash = (Get-FileHash -LiteralPath $contractPath -Algorithm SHA256).Hash.ToLowerInvariant()
$reportRelative = $reportFull.Substring($projectFull.Length + 1).Replace('\', '/')
$receipt = [ordered]@{
    evidenceContractId = 'es.skill-evidence-receipt'
    evidenceContractHash = $contractHash
    skillName = 'es-open-source-migration'
    case = 'automatic-transparent-symbol-map-static-replay'
    status = if ($failed.Count -eq 0) { 'passed' } else { 'failed' }
    evidenceLevel = 'S1'
    receiptPath = $reportRelative
    sourceRefs = @($generatorRelative, $remapperRelative, $oneShotRelative, $testRelative, $authoritativeMapRelative)
    sourceRefHashes = [ordered]@{
        $generatorRelative = (Get-FileHash -LiteralPath $generator -Algorithm SHA256).Hash.ToLowerInvariant()
        $remapperRelative = (Get-FileHash -LiteralPath $remapper -Algorithm SHA256).Hash.ToLowerInvariant()
        $oneShotRelative = (Get-FileHash -LiteralPath $oneShot -Algorithm SHA256).Hash.ToLowerInvariant()
        $testRelative = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $authoritativeMapRelative = (Get-FileHash -LiteralPath $authoritativeMapPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    toolId = 'Test-ESAutoSymbolMap'
    unityVersion = 'not-run'
    capturedUtc = $capturedUtc
    timestampUtc = $capturedUtc
    authorizationKind = 'read-only'
    cases = $results
    nonClaims = @('No target project files were written.', 'No license clearance is granted.', 'No Unity or Runtime compatibility is proven.')
}
$reportDirectory = Split-Path -Parent $reportFull
if (-not (Test-Path -LiteralPath $reportDirectory)) { New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null }
[IO.File]::WriteAllText($reportFull, ($receipt | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
$receipt | ConvertTo-Json -Depth 12
if ($failed.Count -gt 0) { exit 1 }
