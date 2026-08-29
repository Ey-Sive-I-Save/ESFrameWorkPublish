[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Objective,
    [ValidateSet('marketing', 'dashboard')][string]$PageKind = 'marketing',
    [ValidateNotNullOrEmpty()][string]$PrimaryAction = 'Learn more',
    [string]$Audience = 'unspecified audience',
    [ValidateSet('en','zh-CN','ar')][string]$Language = 'en',
    [string]$VisualStyle = 'premium-tech',
    [string]$PublicBaseUrl = '',
    [ValidateSet('always','hourly','daily','weekly','monthly','yearly','never')][string]$SitemapChangeFreq = 'weekly',
    [ValidateRange(0.0,1.0)][double]$SitemapPriority = 0.8,
    [string[]]$SitemapPaths = @('./'),
    [switch]$EnableNetwork,
    [string]$ApiBase = '',
    [string[]]$Allowlist = @(),
    [ValidateRange(1, 300)][int]$TimeoutSeconds = 10,
    [switch]$RunPreview,
    [switch]$ApplyRevision
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$root = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\') + '\'
$newScript = Join-Path $root 'ES/Automation/WebPageStudio/New-ESWebPageStudioRequest.ps1'
$testRequest = Join-Path $root 'ES/Automation/WebPageStudio/Test-ESWebPageStudioRequest.ps1'
$staticScript = Join-Path $root 'ES/Automation/WebPageStudio/Invoke-ESWebPageStudioStatic.ps1'
$convertScript = Join-Path $root 'ES/Automation/WebPageStudio/Convert-ESWebPageStudioRequest.ps1'
$testContract = Join-Path $root 'ES/Automation/WebPageStudio/Test-ESWebPageStudioContract.ps1'

$requestPath = Join-Path $root "ES/Output/WebPageStudio/requests/request-$([guid]::NewGuid().ToString('N')).json"
$newArgs = @{ Objective = $Objective; PageKind = $PageKind; PrimaryAction = $PrimaryAction; Audience = $Audience; Language = $Language; VisualStyle = $VisualStyle; PublicBaseUrl = $PublicBaseUrl; SitemapChangeFreq = $SitemapChangeFreq; SitemapPriority = $SitemapPriority; SitemapPaths = $SitemapPaths; OutputPath = $requestPath; TimeoutSeconds = $TimeoutSeconds }
if ($EnableNetwork) { $newArgs.EnableNetwork = $true; $newArgs.ApiBase = $ApiBase; $newArgs.Allowlist = $Allowlist }
& $newScript @newArgs | Out-Null
& $testRequest -RequestPath $requestPath | Out-Null

$request = Get-Content -Raw -Encoding UTF8 -LiteralPath $requestPath | ConvertFrom-Json
if ($EnableNetwork) {
    throw 'This one-command entry currently stops at an explicit network boundary. Use an authorized backend adapter before enabling network execution.'
}
& $staticScript -RequestPath $requestPath | Out-Null
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $root ([string]$request.output.outputDirectory)))
$contractPath = Join-Path $artifactRoot 'web-page-contract.json'
$validationPath = Join-Path $artifactRoot 'contract-validation.json'
& $convertScript -RequestPath $requestPath -OutputPath $contractPath | Out-Null
$contractReport = & $testContract -ContractPath $contractPath -ReportPath $validationPath | ConvertFrom-Json
$runtimeReceiptPath = $null
if ($RunPreview) {
    $previewScript = Join-Path $root 'ES/Automation/WebPageStudio/Invoke-ESWebPageStudioPreview.ps1'
    $previewArgs = @{ ContractPath = $contractPath }
    if ($ApplyRevision) { $previewArgs.ApplyRevision = $true }
    $runtimeReceipt = & $previewScript @previewArgs | ConvertFrom-Json
    $runtimeReceiptPath = Join-Path $artifactRoot "$($runtimeReceipt.runId)-receipt.json"
}

[ordered]@{
    status = if ([string]$contractReport.status -ne 'passed') { 'static-validation-failed' } elseif ($RunPreview) { 'runtime-review' } else { 'static-ready' }
    requestPath = $requestPath
    artifactDirectory = $artifactRoot
    entryFile = Join-Path $artifactRoot ([string]$request.output.entryFile)
    contractPath = $contractPath
    validationPath = $validationPath
    runtimeStatus = if ($RunPreview) { 'runtime-verified-review' } else { 'runtime-not-run' }
    runtimeReceiptPath = $runtimeReceiptPath
    network = 'disabled'
    claimsNotProven = if ($RunPreview) { @('independent pixel baseline diff', 'human visual sign-off', 'backend service behavior', 'network availability', 'Unity/Worker runtime', 'release acceptance') } else { @('browser screenshot and visual runtime', 'backend service behavior', 'network availability', 'Unity/Worker runtime', 'release acceptance') }
} | ConvertTo-Json -Depth 12
if ([string]$contractReport.status -ne 'passed') { exit 1 }
