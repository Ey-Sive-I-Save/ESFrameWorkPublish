[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path,
    [string]$ReportPath = 'ES/Output/StaticReplay/es-transparent-namespace-remap.json'
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$tool = Join-Path $PSScriptRoot 'Invoke-ESTransparentNamespaceRemap.ps1'
$projectFull = (Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\')
$reportFull = [IO.Path]::GetFullPath((Join-Path $projectFull $ReportPath))
if (-not $reportFull.StartsWith($projectFull + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'ReportPath must remain inside ProjectRoot.' }
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('es-transparent-remap-' + [Guid]::NewGuid().ToString('N'))
$source = Join-Path $testRoot 'source'
$output = Join-Path $testRoot 'output'
$mapping = Join-Path $testRoot 'mapping.json'
$existingOutput = Join-Path $testRoot 'existing-output'
$projectBoundary = Join-Path $testRoot 'project-boundary'
$results = [Collections.Generic.List[object]]::new()

function Add-Case([string]$Id, [bool]$Passed, [string]$Detail) {
    $results.Add([pscustomobject]@{ id = $Id; status = if ($Passed) { 'passed' } else { 'failed' }; detail = $Detail })
}

function Invoke-ExpectFailure([string]$Id, [scriptblock]$Action) {
    try { & $Action | Out-Null; Add-Case $Id $false 'expected failure did not occur' }
    catch { Add-Case $Id $true $_.Exception.Message }
}

try {
    New-Item -ItemType Directory -Path (Join-Path $source 'src/pro') -Force | Out-Null
    New-Item -ItemType Directory -Path $projectBoundary -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $source 'main.cs') -Value @'
namespace Dyad.Core; // ActorHost comment
public class ActorHost { string text = "ActorHost Dyad"; }
'@ -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $source 'package.json') -Value '{"name":"Dyad","script":"ActorHost"}' -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $source 'LICENSE') -Value 'Copyright Dyad; Apache-2.0' -Encoding UTF8
    [IO.File]::WriteAllBytes((Join-Path $source 'icon.bin'), [byte[]](0x00, 0x01, 0xFE, 0xFF))
    Set-Content -LiteralPath (Join-Path $source 'src/pro/Secret.cs') -Value 'class Secret { }' -Encoding UTF8
    $map = [ordered]@{
        schemaVersion = 1
        mapId = 'fixture-es-symbol-map.v1'
        symbols = @(
            [ordered]@{ source = 'Dyad'; es = 'ES' }
            [ordered]@{ source = 'ActorHost'; es = 'ESExecutionHost' }
        )
    }
    [IO.File]::WriteAllText($mapping, ($map | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))

    $dryJson = & $tool -SourceRoot $source -OutputRoot $output -MappingPath $mapping -ProjectRoot $ProjectRoot -DryRun | ConvertFrom-Json
    Add-Case 'positive-dry-run' ($dryJson.manifest.status -eq 'dry-run' -and $dryJson.manifest.counts.changedFiles -eq 1) "changed=$($dryJson.manifest.counts.changedFiles)"

    $writeJson = & $tool -SourceRoot $source -OutputRoot $output -MappingPath $mapping -ProjectRoot $ProjectRoot | ConvertFrom-Json
    $rewritten = Get-Content -LiteralPath (Join-Path $output 'main.cs') -Raw -Encoding UTF8
    $license = Get-Content -LiteralPath (Join-Path $output 'LICENSE') -Raw -Encoding UTF8
    Add-Case 'positive-write' ($writeJson.status -eq 'passed' -and $rewritten.Contains('namespace ES.Core') -and $rewritten.Contains('ESExecutionHost')) 'external output written with transparent remap'
    Add-Case 'code-aware-preserves-literals' ($rewritten.Contains('// ActorHost comment') -and $rewritten.Contains('"ActorHost Dyad"')) 'comments and string literals remain byte-stable'
    Add-Case 'structured-metadata-preserved' ((Get-Content -LiteralPath (Join-Path $output 'package.json') -Raw -Encoding UTF8) -eq (Get-Content -LiteralPath (Join-Path $source 'package.json') -Raw -Encoding UTF8)) 'structured metadata is not lexically rewritten'
    Add-Case 'license-preserved' ($license -eq (Get-Content -LiteralPath (Join-Path $source 'LICENSE') -Raw -Encoding UTF8)) 'LICENSE copied verbatim'
    Add-Case 'binary-preserved' ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $output 'icon.bin'))) -eq [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $source 'icon.bin')))) 'binary file copied byte-for-byte'
    Add-Case 'licensed-tree-excluded' (-not (Test-Path -LiteralPath (Join-Path $output 'src/pro/Secret.cs'))) 'src/pro excluded'

    $replay = & $tool -SourceRoot $source -OutputRoot $output -MappingPath $mapping -ProjectRoot $ProjectRoot | ConvertFrom-Json
    Add-Case 'repeat-idempotency' ([bool]$replay.idempotentReplay -and $replay.planHash -eq $writeJson.planHash) 'same plan hash accepted'
    Add-Content -LiteralPath (Join-Path $output 'main.cs') -Value '// drift' -Encoding UTF8
    Invoke-ExpectFailure 'output-drift-detected' { & $tool -SourceRoot $source -OutputRoot $output -MappingPath $mapping -ProjectRoot $ProjectRoot }
    [IO.File]::WriteAllText((Join-Path $output 'es-remap-receipt.json'), '{"status":"tampered","planHash":"' + $writeJson.planHash + '"}', [Text.UTF8Encoding]::new($false))
    Invoke-ExpectFailure 'receipt-drift-detected' { & $tool -SourceRoot $source -OutputRoot $output -MappingPath $mapping -ProjectRoot $ProjectRoot }

    Invoke-ExpectFailure 'deny-source-inside-project' { & $tool -SourceRoot $source -OutputRoot (Join-Path $testRoot 'inside-output') -MappingPath $mapping -ProjectRoot $source }
    Invoke-ExpectFailure 'deny-output-inside-project' { & $tool -SourceRoot $source -OutputRoot (Join-Path $projectBoundary 'generated') -MappingPath $mapping -ProjectRoot $projectBoundary }

    New-Item -ItemType Directory -Path $existingOutput -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $existingOutput 'unrelated.txt') -Value 'conflict' -Encoding UTF8
    Invoke-ExpectFailure 'deny-existing-unaccepted-output' { & $tool -SourceRoot $source -OutputRoot $existingOutput -MappingPath $mapping -ProjectRoot $ProjectRoot }

    $badSource = Join-Path $testRoot 'bad-source'
    New-Item -ItemType Directory -Path $badSource -Force | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $badSource 'bad.cs'), [byte[]](0xFF, 0xFE, 0x41))
    Invoke-ExpectFailure 'strict-utf8' { & $tool -SourceRoot $badSource -OutputRoot (Join-Path $testRoot 'bad-output') -MappingPath $mapping -ProjectRoot $ProjectRoot }

    $badMap = Join-Path $testRoot 'bad-map.json'
    $badMapObject = [ordered]@{ schemaVersion = 1; mapId = 'bad'; symbols = @([ordered]@{ source = 'A'; es = 'ES' }, [ordered]@{ source = 'B'; es = 'ES' }) }
    [IO.File]::WriteAllText($badMap, ($badMapObject | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    Invoke-ExpectFailure 'duplicate-es-identity' { & $tool -SourceRoot $source -OutputRoot (Join-Path $testRoot 'bad-map-output') -MappingPath $badMap -ProjectRoot $ProjectRoot }

    $badSourceMap = Join-Path $testRoot 'bad-source-map.json'
    $badSourceMapObject = [ordered]@{ schemaVersion = 1; mapId = 'bad-source'; symbols = @([ordered]@{ source = 'ActorHost'; es = 'ESOne' }, [ordered]@{ source = 'ActorHost'; es = 'ESTwo' }) }
    [IO.File]::WriteAllText($badSourceMap, ($badSourceMapObject | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    Invoke-ExpectFailure 'duplicate-source-identity' { & $tool -SourceRoot $source -OutputRoot (Join-Path $testRoot 'bad-source-map-output') -MappingPath $badSourceMap -ProjectRoot $ProjectRoot }

    $unsafeMap = Join-Path $testRoot 'unsafe-map.json'
    $unsafeMapObject = [ordered]@{ schemaVersion = 1; mapId = 'unsafe'; symbols = @([ordered]@{ source = 'ActorHost'; es = '../escape' }) }
    [IO.File]::WriteAllText($unsafeMap, ($unsafeMapObject | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    Invoke-ExpectFailure 'unsafe-identifier' { & $tool -SourceRoot $source -OutputRoot (Join-Path $testRoot 'unsafe-output') -MappingPath $unsafeMap -ProjectRoot $ProjectRoot }
    Invoke-ExpectFailure 'file-budget' { & $tool -SourceRoot $source -OutputRoot (Join-Path $testRoot 'budget-output') -MappingPath $mapping -ProjectRoot $ProjectRoot -MaxFiles 1 }
}
finally {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}

$failed = @($results | Where-Object status -eq 'failed')
$capturedUtc = [DateTime]::UtcNow.ToString('o')
$toolRelative = $tool.Substring($projectFull.Length + 1).Replace('\', '/')
$testRelative = $PSCommandPath.Substring($projectFull.Length + 1).Replace('\', '/')
$contractPath = Join-Path $projectFull 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json'
$contractHash = (Get-FileHash -LiteralPath $contractPath -Algorithm SHA256).Hash.ToLowerInvariant()
$reportRelative = $reportFull.Substring($projectFull.Length + 1).Replace('\', '/')
$receipt = [ordered]@{
    evidenceContractId = 'es.skill-evidence-receipt'
    evidenceContractHash = $contractHash
    skillName = 'es-open-source-migration'
    case = 'transparent-namespace-remap-static-replay'
    status = if ($failed.Count -eq 0) { 'passed' } else { 'failed' }
    evidenceLevel = 'S1'
    receiptPath = $reportRelative
    sourceRefs = @($toolRelative, $testRelative)
    sourceRefHashes = [ordered]@{
        $toolRelative = (Get-FileHash -LiteralPath $tool -Algorithm SHA256).Hash.ToLowerInvariant()
        $testRelative = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    toolId = 'Test-ESTransparentNamespaceRemap'
    unityVersion = 'not-run'
    capturedUtc = $capturedUtc
    timestampUtc = $capturedUtc
    authorizationKind = 'read-only'
    cases = $results
    nonClaims = @('No target project files were written.', 'No Unity or Runtime compatibility is proven.')
}
$reportDirectory = Split-Path -Parent $reportFull
if (-not (Test-Path -LiteralPath $reportDirectory)) { New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null }
[IO.File]::WriteAllText($reportFull, ($receipt | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
$receipt | ConvertTo-Json -Depth 12
if ($failed.Count -gt 0) { exit 1 }
