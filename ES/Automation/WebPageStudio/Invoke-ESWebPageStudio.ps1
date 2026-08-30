[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Objective,
    [ValidateSet('marketing', 'dashboard')][string]$PageKind = 'marketing',
    [ValidateNotNullOrEmpty()][string]$PrimaryAction = 'Learn more',
    [string]$Audience = 'unspecified audience',
    [ValidateSet('en','zh-CN','ar')][string]$Language = 'en',
    [ValidateSet('premium-tech','editorial','aurora','minimal')][string]$VisualStyle = 'premium-tech',
    [ValidateSet('none','subtle','expressive')][string]$MotionLevel = 'subtle',
    [ValidateSet('airy','balanced','compact')][string]$LayoutDensity = 'balanced',
    [string]$PublicBaseUrl = '',
    [ValidateSet('always','hourly','daily','weekly','monthly','yearly','never')][string]$SitemapChangeFreq = 'weekly',
    [ValidateRange(0.0,1.0)][double]$SitemapPriority = 0.8,
    [string[]]$SitemapPaths = @('./'),
    [switch]$EnableNetwork,
    [ValidateSet('mock-contract-only','local-adapter')][string]$BackendMode = 'mock-contract-only',
    [string]$BackendFixturePath = '',
    [string]$ApiBase = '',
    [string[]]$Allowlist = @(),
    [ValidateRange(1, 300)][int]$TimeoutSeconds = 10,
    [switch]$RunPreview,
    [switch]$RunValidationBundle,
    [switch]$ApplyRevision,
    [string]$ModelResponsePath = '',
    [string]$AiSolutionPath = '',
    [string[]]$RoundPaths = @(),
    [string]$RevisionReceiptPath = '',
    [bool]$AutoOpen = $true
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$root = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\') + '\'
$newScript = Join-Path $root 'ES/Automation/WebPageStudio/New-ESWebPageStudioRequest.ps1'
$testRequest = Join-Path $root 'ES/Automation/WebPageStudio/Test-ESWebPageStudioRequest.ps1'
$staticScript = Join-Path $root 'ES/Automation/WebPageStudio/Invoke-ESWebPageStudioStatic.ps1'
$convertScript = Join-Path $root 'ES/Automation/WebPageStudio/Convert-ESWebPageStudioRequest.ps1'
$testContract = Join-Path $root 'ES/Automation/WebPageStudio/Test-ESWebPageStudioContract.ps1'
$deepDesignScript = Join-Path $root 'ES/Automation/WebPageStudio/Invoke-ESWebPageStudioDeepDesign.ps1'

$requestPath = Join-Path $root "ES/Output/WebPageStudio/requests/request-$([guid]::NewGuid().ToString('N')).json"
$newArgs = @{ Objective = $Objective; PageKind = $PageKind; PrimaryAction = $PrimaryAction; Audience = $Audience; Language = $Language; VisualStyle = $VisualStyle; MotionLevel = $MotionLevel; LayoutDensity = $LayoutDensity; PublicBaseUrl = $PublicBaseUrl; SitemapChangeFreq = $SitemapChangeFreq; SitemapPriority = $SitemapPriority; SitemapPaths = $SitemapPaths; OutputPath = $requestPath; TimeoutSeconds = $TimeoutSeconds }
if ($EnableNetwork) { $newArgs.EnableNetwork = $true; $newArgs.ApiBase = $ApiBase; $newArgs.Allowlist = $Allowlist }
& $newScript @newArgs | Out-Null
& $testRequest -RequestPath $requestPath | Out-Null
$request = Get-Content -Raw -Encoding UTF8 -LiteralPath $requestPath | ConvertFrom-Json

$preflightPath = Join-Path $root "ES/Output/WebPageStudio/requests/preflight-$($request.requestId).json"
$preflightArgs = @{ RequestPath = $requestPath; OutputPath = $preflightPath }
if (-not [string]::IsNullOrWhiteSpace($AiSolutionPath)) { $preflightArgs.AiSolutionPath = $AiSolutionPath }
if (@($RoundPaths).Count -eq 5) { $preflightArgs.RoundPaths = $RoundPaths }
$preflight = & (Join-Path $root 'ES/Automation/WebPageStudio/Invoke-ESWebPageStudioPreflight.ps1') @preflightArgs | ConvertFrom-Json
if ([string]$preflight.status -ne 'accepted') { throw 'P0_PREFLIGHT_BLOCKED: intent/prompt/layout/knowledge preflight did not pass.' }
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $root ([string]$request.output.outputDirectory)))
$designPath = Join-Path $root "ES/Output/WebPageStudio/requests/deep-design-$($request.requestId).json"
$deepArgs = @{ PreflightPath = $preflightPath; OutputPath = $designPath }
if (-not [string]::IsNullOrWhiteSpace($ModelResponsePath)) { $deepArgs.ModelResponsePath = $ModelResponsePath }
$design = & $deepDesignScript @deepArgs | ConvertFrom-Json
if ([string]$design.designStatus -ne 'accepted' -or [string]$design.decisionStatus -ne 'accepted') { throw 'P0_DESIGN_NOT_ACCEPTED: deep design was not accepted.' }

if ($EnableNetwork) {
    throw 'This one-command entry currently stops at an explicit network boundary. Use an authorized backend adapter before enabling network execution.'
}
if ([string]::IsNullOrWhiteSpace($RevisionReceiptPath)) { throw 'BLOCKED_WEB_MATERIALIZATION_AI_REVISION_REQUIRED' }
& $staticScript -RequestPath $requestPath -DesignSpecPath $designPath -RevisionReceiptPath $RevisionReceiptPath | Out-Null
Copy-Item -LiteralPath $designPath -Destination (Join-Path $artifactRoot 'deep-design.json') -Force
$artifactPreflightPath = Join-Path $artifactRoot 'preflight.json'
Copy-Item -LiteralPath $preflightPath -Destination $artifactPreflightPath -Force
$contractPath = Join-Path $artifactRoot 'web-page-contract.json'
$validationPath = Join-Path $artifactRoot 'contract-validation.json'
& $convertScript -RequestPath $requestPath -DesignSpecPath $designPath -OutputPath $contractPath | Out-Null
$contractReport = & $testContract -ContractPath $contractPath -ReportPath $validationPath | ConvertFrom-Json
$staticSignalsPath = Join-Path $artifactRoot 'static-signals.json'
$staticSignalsScript = Join-Path $root 'ES/Automation/WebPageStudio/Test-ESWebPageStudioStaticSignals.ps1'
$entryPath = Join-Path $artifactRoot ([string]$request.output.entryFile)
$staticSignals = & $staticSignalsScript -HtmlPath $entryPath -ContractPath $contractPath -ReportPath $staticSignalsPath.Substring($root.Length) | ConvertFrom-Json
$intentAuditPath = Join-Path $artifactRoot 'intent-audit.json'
$intentAuditScript = Join-Path $root 'ES/Automation/WebPageStudio/Test-ESWebPageStudioIntentAudit.ps1'
$intentAudit = & $intentAuditScript -RequestPath $requestPath -HtmlPath $entryPath -DesignSpecPath $designPath | ConvertFrom-Json
[IO.File]::WriteAllText($intentAuditPath,($intentAudit|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
$artifactIntegrityPath = Join-Path $artifactRoot 'artifact-integrity.json'
$artifactIntegrity = $null
$backendContractPath = Join-Path $artifactRoot 'backend-contract.json'
& (Join-Path $root 'ES/Automation/WebPageStudio/New-ESWebPageStudioBackendContract.ps1') -Mode $BackendMode -ContractId ("web-backend-$($request.requestId.ToString().Substring(0,8))") -OutputPath $backendContractPath.Substring($root.Length) | Out-Null
$backendReceiptPath = $null
if ($BackendMode -eq 'local-adapter' -and -not [string]::IsNullOrWhiteSpace($BackendFixturePath)) {
    $backendReceiptPath = Join-Path $artifactRoot 'backend-local-adapter-receipt.json'
    & (Join-Path $root 'ES/Automation/WebPageStudio/Invoke-ESWebPageStudioLocalAdapter.ps1') -ContractPath $backendContractPath -FixturePath $BackendFixturePath -ReceiptPath $backendReceiptPath.Substring($root.Length) | Out-Null
}
$cachePolicyPath = $null
$dynamicReplayPath = $null
$cacheValidation = $null
$dynamicValidation = $null
if ($RunValidationBundle) {
    $cachePolicyPath = Join-Path $artifactRoot 'cache-policy.json'
    & (Join-Path $root 'ES/Automation/WebPageStudio/New-ESWebCachePolicy.ps1') -OutputPath $cachePolicyPath.Substring($root.Length) -PolicyId ("web-cache-$($request.requestId.ToString().Substring(0,8))") | Out-Null
    $cacheValidation = & (Join-Path $root 'ES/Automation/WebPageStudio/Test-ESWebCachePolicy.ps1') -PolicyPath $cachePolicyPath | ConvertFrom-Json
    [IO.File]::WriteAllText((Join-Path $artifactRoot 'cache-policy-validation.json'),($cacheValidation|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
    $dynamicReplayPath = Join-Path $artifactRoot 'dynamic-state-replay.json'
    & (Join-Path $root 'ES/Automation/WebPageStudio/Invoke-ESWebDynamicStateReplay.ps1') -CachePolicyPath $cachePolicyPath -OutputPath $dynamicReplayPath.Substring($root.Length) | Out-Null
    $dynamicValidation = & (Join-Path $root 'ES/Automation/WebPageStudio/Test-ESWebDynamicStateReplay.ps1') -ReplayPath $dynamicReplayPath | ConvertFrom-Json
    [IO.File]::WriteAllText((Join-Path $artifactRoot 'dynamic-state-replay-validation.json'),($dynamicValidation|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
}
$runtimeReceiptPath = $null
if ($RunPreview -or $RunValidationBundle) {
    $previewScript = Join-Path $root 'ES/Automation/WebPageStudio/Invoke-ESWebPageStudioPreview.ps1'
    $previewArgs = @{ ContractPath = $contractPath }
    if ($ApplyRevision) { $previewArgs.ApplyRevision = $true }
    $runtimeReceipt = & $previewScript @previewArgs | ConvertFrom-Json
    $runtimeReceiptPath = Join-Path $artifactRoot "$($runtimeReceipt.runId)-receipt.json"
}
$visualMatrixPath = $null
$performanceBaselinePath = $null
if ($RunValidationBundle) {
    $visualMatrixPath = Join-Path $artifactRoot 'visual-matrix-runtime.json'
    & (Join-Path $root 'ES/Automation/WebPageStudio/Invoke-ESWebVisualMatrix.ps1') -HtmlPath $entryPath -OutputPath $visualMatrixPath.Substring($root.Length) | Out-Null
    $performanceBaselinePath = Join-Path $artifactRoot 'performance-baseline.json'
    & (Join-Path $root 'ES/Automation/WebPageStudio/Invoke-ESWebPerformanceBaseline.ps1') -HtmlPath $entryPath -OutputPath $performanceBaselinePath.Substring($root.Length) | Out-Null
}
$artifactIntegrity = & (Join-Path $root 'ES/Automation/WebPageStudio/Test-ESWebPageStudioArtifactIntegrity.ps1') -ArtifactDirectory $artifactRoot -ReportPath $artifactIntegrityPath.Substring($root.Length) | ConvertFrom-Json
$readinessPath = $null
$readiness = $null
if ($RunValidationBundle -or $RunPreview) {
    $readinessPath = Join-Path $artifactRoot 'staging-readiness.json'
    $readiness = & (Join-Path $root 'ES/Automation/WebPageStudio/Test-ESWebPageStudioStagingReadiness.ps1') -ArtifactDirectory $artifactRoot -ReportPath $readinessPath.Substring($root.Length) | ConvertFrom-Json
}
$artifactIntegrity = & (Join-Path $root 'ES/Automation/WebPageStudio/Test-ESWebPageStudioArtifactIntegrity.ps1') -ArtifactDirectory $artifactRoot -ReportPath $artifactIntegrityPath.Substring($root.Length) | ConvertFrom-Json

$final=[ordered]@{
    status = if ([string]$contractReport.status -ne 'passed' -or [string]$staticSignals.status -ne 'passed') { 'static-generated' } elseif ([string]$intentAudit.status -ne 'passed' -or [string]$artifactIntegrity.status -ne 'passed') { 'static-validated' } else { 'static-artifact-closed' }
    staticStatus = if ([string]$contractReport.status -ne 'passed' -or [string]$staticSignals.status -ne 'passed') { 'static-generated' } elseif ([string]$intentAudit.status -ne 'passed' -or [string]$artifactIntegrity.status -ne 'passed') { 'static-validated' } else { 'static-artifact-closed' }
    releaseStatus = 'release-not-run'
    requestPath = $requestPath
    preflightPath = $artifactPreflightPath
    designSpecPath = Join-Path $artifactRoot 'deep-design.json'
    designStatus = [string]$design.designStatus
    artifactDirectory = $artifactRoot
    entryFile = Join-Path $artifactRoot ([string]$request.output.entryFile)
    contractPath = $contractPath
    validationPath = $validationPath
    runtimeStatus = if ($RunValidationBundle) { 'local-validation-review' } elseif ($RunPreview) { 'runtime-verified-review' } else { 'runtime-not-run' }
    runtimeReceiptPath = $runtimeReceiptPath
    staticSignalsPath = $staticSignalsPath
    artifactIntegrityPath = $artifactIntegrityPath
    backendContractPath = $backendContractPath
    backendReceiptPath = $backendReceiptPath
    backendMode = $BackendMode
    cachePolicyPath = $cachePolicyPath
    dynamicReplayPath = $dynamicReplayPath
    readinessPath = $readinessPath
    readinessStatus = if ($readiness) { [string]$readiness.status } else { 'release-not-run' }
    cacheValidation = if ($cacheValidation) { [ordered]@{status=[string]$cacheValidation.status;findingCount=[int]$cacheValidation.findingCount} } else { $null }
    dynamicValidation = if ($dynamicValidation) { [ordered]@{status=[string]$dynamicValidation.status;findingCount=[int]$dynamicValidation.findingCount} } else { $null }
    visualMatrixPath = $visualMatrixPath
    performanceBaselinePath = $performanceBaselinePath
    staticSignals = [ordered]@{status=[string]$staticSignals.status;passedCount=[int]$staticSignals.passedCount;failedCount=[int]$staticSignals.failedCount}
    intentAuditPath = $intentAuditPath
    intentAudit = [ordered]@{status=[string]$intentAudit.status;passedCount=[int]$intentAudit.passedCount;blockedCount=[int]$intentAudit.blockedCount}
    artifactIntegrity = [ordered]@{status=[string]$artifactIntegrity.status;passedCount=[int]$artifactIntegrity.passedCount;failedCount=[int]$artifactIntegrity.failedCount}
    network = 'disabled'
    claimsNotProven = @('human visual sign-off','backend service behavior','network availability','Unity/Worker runtime','release acceptance','LCP/INP/Lighthouse and production p75')
}
if ([string]$contractReport.status -ne 'passed') { exit 1 }
if($AutoOpen -and [string]$final.status -eq 'static-artifact-closed') { Start-Process -FilePath ([string]$final.entryFile);$final.autoOpened=$true } else {$final.autoOpened=$false}
$final | ConvertTo-Json -Depth 12
