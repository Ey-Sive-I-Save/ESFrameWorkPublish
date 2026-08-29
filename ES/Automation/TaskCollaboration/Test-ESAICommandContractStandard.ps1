[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new()
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { $ProjectRoot = (& git rev-parse --show-toplevel 2>$null) }
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { throw 'Cannot resolve project root.' }
$ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot.Trim())

$catalogPath = Join-Path $ProjectRoot 'Assets/Plugins/ES/AICommands/AICommandCatalog.json'
$readmePath = Join-Path $ProjectRoot 'Assets/Plugins/ES/AICommands/README.md'
$commandRoot = Join-Path $ProjectRoot 'Assets/Plugins/ES/AICommands'
$indexPath = Get-ChildItem -LiteralPath $commandRoot -Filter '*.md' -File | Where-Object {
    (Get-Content -LiteralPath $_.FullName -Encoding UTF8 -TotalCount 1) -like '# AI Commands*'
} | Select-Object -First 1 -ExpandProperty FullName
if ([string]::IsNullOrWhiteSpace($indexPath)) { throw 'Navigation index document not found.' }
$errors = [Collections.Generic.List[string]]::new()
$catalog = Get-Content -LiteralPath $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
$standard = $catalog.contractStandard
$required = @('commandId','commandType','defaultWrite','riskLevel','inputSchema','outputSchema','requiredReads','executionBoundary','dryRun','confirmation','cancellation','recovery','validation','evidenceRef')
$bindings = @('userAuthorization','planHash','taskContract','commandBodyHash','idempotencyKey','writeScope')
if ($null -eq $standard -or [string]$standard.id -ne 'es.aicommand.single-task-contract.v1') { $errors.Add('contractStandard.id missing or incorrect.') }
foreach ($field in $required) { if (@($standard.requiredFields) -notcontains $field) { $errors.Add("Missing required contract field: $field") } }
foreach ($binding in $bindings) { if (@($standard.executionBindings) -notcontains $binding) { $errors.Add("Missing execution binding: $binding") } }
if ([string]$standard.driftDisposition -ne 'StalePlan') { $errors.Add('Drift disposition must be StalePlan.') }
if ([string]$standard.prioritySemantics -notmatch 'not permission') { $errors.Add('Priority semantics must distinguish navigation priority from permission.') }

$indexText = Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8
$catalogPaths = @($catalog.commands | ForEach-Object { [string]$_.path })
$missing = @($catalogPaths | Where-Object { $indexText -notmatch [regex]::Escape($_) })
if ($missing.Count -gt 0) { $errors.Add("Navigation coverage missing $($missing.Count) catalog path(s).") }
$readmeText = Get-Content -LiteralPath $readmePath -Raw -Encoding UTF8
foreach ($needle in @('AICommand','AIBrain','NoMatchingCommand','StalePlan','invocation','runtime-not-run')) {
    if ($readmeText -notmatch [regex]::Escape($needle)) { $errors.Add("README missing standard statement: $needle") }
}

$report = [pscustomobject]@{
    status = if ($errors.Count -eq 0) { 'passed' } else { 'failed' }
    standardId = [string]$standard.id
    catalogCount = @($catalog.commands).Count
    navigationMissingCount = $missing.Count
    requiredFieldCount = $required.Count
    executionBindingCount = $bindings.Count
    errors = $errors.ToArray()
    runtimeStatus = 'runtime-not-run'
}
if ($Json) { $report | ConvertTo-Json -Depth 6 } else { "AICommand contract standard: $($report.status); catalog=$($report.catalogCount); navigationMissing=$($report.navigationMissingCount)" }
if ($errors.Count -gt 0) { exit 1 }
