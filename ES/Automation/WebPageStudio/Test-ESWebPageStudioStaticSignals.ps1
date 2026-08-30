[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$HtmlPath,
    [Parameter(Mandatory = $true)][string]$ContractPath,
    [string]$ReportPath = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$root = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\') + '\'
function Resolve-ProjectFile([string]$path, [string]$name) {
    $full = (Resolve-Path -LiteralPath $path -ErrorAction Stop).Path
    if (-not $full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw "$name must remain under the project root." }
    return $full
}
$html = Resolve-ProjectFile $HtmlPath 'HtmlPath'
$contract = Resolve-ProjectFile $ContractPath 'ContractPath'

function Invoke-JsonValidator([string]$script, [hashtable]$arguments) {
    $raw = & (Join-Path $root "ES/Automation/WebPageStudio/$script") @arguments
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "$script exited with code $LASTEXITCODE." }
    return ($raw | ConvertFrom-Json -ErrorAction Stop)
}

$quality = Invoke-JsonValidator 'Test-ESWebPageStudioQuality.ps1' @{ HtmlPath = $html }
$accessibility = Invoke-JsonValidator 'Test-ESWebPageStudioAccessibility.ps1' @{ HtmlPath = $html }
$contractReceipt = Invoke-JsonValidator 'Test-ESWebPageStudioContract.ps1' @{ ContractPath = $contract }
$utf8Script = Join-Path $root '.agents/skills/es-utf8-guard/scripts/Test-ESUtf8.ps1'
$utf8Raw = @(& $utf8Script -ProjectRoot $root -Path @($html, $contract) -Json 2>$null)
$utf8Receipt = ($utf8Raw -join [Environment]::NewLine) | ConvertFrom-Json -ErrorAction Stop
$utf8Signal = [ordered]@{
    recordType = 'ESUtf8ValidationReceipt'
    status = if ([bool]$utf8Receipt.valid) { 'passed' } else { 'failed' }
    evidenceLevel = 'S1'
    failedChecks = @($utf8Receipt.files | Where-Object { $_ -and -not $_.valid } | ForEach-Object { [string]$_.path })
    claimsNotProven = @('UTF-8 validation does not prove visual, browser, network, Unity, or release behavior.')
}
$receipts = @($quality, $accessibility, $contractReceipt, [pscustomobject]$utf8Signal)
$failed = @($receipts | Where-Object { [string]$_.status -ne 'passed' }).Count
$result = [ordered]@{
    schemaVersion = 1
    recordType = 'WebPageStudioStaticSignalsReceipt'
    status = if ($failed -eq 0) { 'passed' } else { 'failed' }
    signalCount = $receipts.Count
    passedCount = $receipts.Count - $failed
    failedCount = $failed
    runtimeStatus = 'runtime-not-run'
    signals = @($receipts | ForEach-Object {
        [ordered]@{
            recordType = if ($_.PSObject.Properties['recordType']) { [string]$_.recordType } elseif ($_.PSObject.Properties['validator']) { [string]$_.validator } else { 'unknown' }
            status = [string]$_.status
            evidenceLevel = [string]$_.evidenceLevel
            failedChecks = @($_.checks | Where-Object { $_ -and [string]$_.status -ne 'passed' } | ForEach-Object { [string]$_.check })
            claimsNotProven = @($_.claimsNotProven)
        }
    })
    nonClaims = @('Static signals do not prove browser, network, visual-pixel, Unity, or release behavior.','UTF-8 signal covers only the supplied HTML and contract paths.')
}
$json = $result | ConvertTo-Json -Depth 12
if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
    $report = [IO.Path]::GetFullPath((Join-Path (Get-Location) $ReportPath))
    if (-not $report.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'ReportPath must remain under the project root.' }
    $parent = Split-Path -Parent $report
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    [IO.File]::WriteAllText($report, $json, [Text.UTF8Encoding]::new($false))
}
$json
if ($failed -gt 0) { exit 1 }
