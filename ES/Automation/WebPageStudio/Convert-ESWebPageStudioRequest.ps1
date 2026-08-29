[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RequestPath,
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$projectRoot = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\') + '\'
$requestFull = (Resolve-Path -LiteralPath $RequestPath -ErrorAction Stop).Path
if (-not $requestFull.StartsWith($projectRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'RequestPath must remain under the project root.' }
& (Join-Path (Get-Location) 'ES/Automation/WebPageStudio/Test-ESWebPageStudioRequest.ps1') -RequestPath $requestFull | Out-Null
$request = Get-Content -Raw -Encoding UTF8 -LiteralPath $requestFull | ConvertFrom-Json
if ([bool]$request.network.enabled -or [string]$request.backend.mode -ne 'mock-contract-only') {
    throw 'WebPageStudio static contract conversion currently supports mock-contract-only with network disabled. Use a dedicated authorized backend adapter before requesting network execution.'
}

function Get-Sha256([string]$text) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($algorithm.ComputeHash([Text.UTF8Encoding]::new($false).GetBytes($text))).Replace('-', '').ToLowerInvariant()) }
    finally { $algorithm.Dispose() }
}
function New-Id([string]$value) {
    $normalized = (($value.ToLowerInvariant() -replace '[^a-z0-9]+', '-') -replace '(^-|-$)', '')
    if ([string]::IsNullOrWhiteSpace($normalized)) { $normalized = 'id-generated' }
    if ($normalized -notmatch '^[a-z]') { $normalized = 'id-' + $normalized }
    return $normalized.Substring(0, [Math]::Min(128, $normalized.Length))
}

$requestJson = $request | ConvertTo-Json -Depth 20 -Compress
$inputHash = Get-Sha256 $requestJson
$pageId = New-Id ([string]$request.requestId)
$revisionId = 'rev-0001'
$profile = @($request.responsiveProfiles)[0]
$state = [string]@($request.states)[0]
$profileId = New-Id ([string]$profile.id)
$stateId = New-Id $state
$rootId = 'page-root'
$sourceId = 'request-source'
$tokenId = 'token-accent'
$componentId = if ([string]$request.pageKind -eq 'dashboard') { 'dashboard-shell' } else { 'marketing-shell' }
$nodes = [System.Collections.Generic.List[object]]::new()
$nodes.Add([ordered]@{ nodeId = $rootId; parentId = $null; kind = 'document'; semanticRole = [string]$request.pageKind; children = @($componentId); sourceRefs = @($sourceId); tokenRefs = @($tokenId) })
if ([string]$request.pageKind -eq 'dashboard') {
    $nodes.Add([ordered]@{ nodeId = $componentId; parentId = $rootId; kind = 'section'; semanticRole = 'primary-content'; children = @('dashboard-intro','dashboard-metrics','dashboard-panel'); sourceRefs = @($sourceId); tokenRefs = @($tokenId) })
    $nodes.Add([ordered]@{ nodeId = 'dashboard-intro'; parentId = $componentId; kind = 'section'; semanticRole = 'intro'; children = @(); sourceRefs = @($sourceId); tokenRefs = @($tokenId) })
    $nodes.Add([ordered]@{ nodeId = 'dashboard-metrics'; parentId = $componentId; kind = 'data'; semanticRole = 'metrics-grid'; children = @('metric-signal-health','metric-active-flows','metric-response-time','metric-coverage'); sourceRefs = @($sourceId); tokenRefs = @($tokenId) })
    foreach ($metric in @('signal-health','active-flows','response-time','coverage')) {
        $nodes.Add([ordered]@{ nodeId = "metric-$metric"; parentId = 'dashboard-metrics'; kind = 'data'; semanticRole = "metric-$metric"; children = @(); sourceRefs = @($sourceId); tokenRefs = @($tokenId) })
    }
    $nodes.Add([ordered]@{ nodeId = 'dashboard-panel'; parentId = $componentId; kind = 'section'; semanticRole = 'trend-panel'; children = @('dashboard-primary-action'); sourceRefs = @($sourceId); tokenRefs = @($tokenId) })
    $nodes.Add([ordered]@{ nodeId = 'dashboard-primary-action'; parentId = 'dashboard-panel'; kind = 'button'; semanticRole = 'primary-action'; children = @(); sourceRefs = @($sourceId); tokenRefs = @($tokenId) })
} else {
    $nodes.Add([ordered]@{ nodeId = $componentId; parentId = $rootId; kind = 'section'; semanticRole = 'primary-content'; children = @('marketing-hero','marketing-signal','marketing-proof','marketing-closing'); sourceRefs = @($sourceId); tokenRefs = @($tokenId) })
    $nodes.Add([ordered]@{ nodeId = 'marketing-hero'; parentId = $componentId; kind = 'section'; semanticRole = 'hero'; children = @('marketing-hero-copy','marketing-hero-art'); sourceRefs = @($sourceId); tokenRefs = @($tokenId) })
    $nodes.Add([ordered]@{ nodeId = 'marketing-hero-copy'; parentId = 'marketing-hero'; kind = 'container'; semanticRole = 'hero-copy'; children = @('marketing-primary-action'); sourceRefs = @($sourceId); tokenRefs = @($tokenId) })
    $nodes.Add([ordered]@{ nodeId = 'marketing-hero-art'; parentId = 'marketing-hero'; kind = 'container'; semanticRole = 'hero-art'; children = @(); sourceRefs = @($sourceId); tokenRefs = @($tokenId) })
    $nodes.Add([ordered]@{ nodeId = 'marketing-primary-action'; parentId = 'marketing-hero-copy'; kind = 'button'; semanticRole = 'primary-action'; children = @(); sourceRefs = @($sourceId); tokenRefs = @($tokenId) })
    $nodes.Add([ordered]@{ nodeId = 'marketing-signal'; parentId = $componentId; kind = 'section'; semanticRole = 'signal'; children = @(); sourceRefs = @($sourceId); tokenRefs = @($tokenId) })
    $nodes.Add([ordered]@{ nodeId = 'marketing-proof'; parentId = $componentId; kind = 'section'; semanticRole = 'proof'; children = @(); sourceRefs = @($sourceId); tokenRefs = @($tokenId) })
    $nodes.Add([ordered]@{ nodeId = 'marketing-closing'; parentId = $componentId; kind = 'section'; semanticRole = 'closing'; children = @('marketing-closing-action'); sourceRefs = @($sourceId); tokenRefs = @($tokenId) })
    $nodes.Add([ordered]@{ nodeId = 'marketing-closing-action'; parentId = 'marketing-closing'; kind = 'button'; semanticRole = 'secondary-action'; children = @(); sourceRefs = @($sourceId); tokenRefs = @($tokenId) })
}
$tokens = @([ordered]@{ tokenId = $tokenId; role = 'accent'; value = '#75e1d1'; valueType = 'color' })
$components = @([ordered]@{ componentId = $componentId; semanticRole = 'primary-content'; allowedElements = @('section', 'article', 'button', 'h1', 'p'); fallback = 'plain-content' })
$responsive = @($request.responsiveProfiles | ForEach-Object { [ordered]@{ profileId = New-Id ([string]$_.id); viewport = [ordered]@{ width = [int]$_.width; height = [int]$_.height }; semanticEquivalence = 'preserve-primary-content-and-action' } })
$states = @($request.states | ForEach-Object { [ordered]@{ stateId = New-Id ([string]$_); effects = @() } })
$sourcePath = ([Uri]::new($projectRoot)).MakeRelativeUri([Uri]::new($requestFull)).ToString().Replace('\', '/')
$artifactDirectory = [string]$request.output.outputDirectory
$entryFile = [string]$request.output.entryFile
$artifactRootFull = [IO.Path]::GetFullPath((Join-Path $projectRoot $artifactDirectory))
if (-not $artifactRootFull.StartsWith($projectRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Artifact directory must remain under the project root.' }
$artifactFileFull = [IO.Path]::GetFullPath((Join-Path $artifactRootFull $entryFile))
if (-not $artifactFileFull.StartsWith($artifactRootFull, [StringComparison]::OrdinalIgnoreCase)) { throw 'Artifact entry file must remain under its output directory.' }
$artifactExists = Test-Path -LiteralPath $artifactFileFull -PathType Leaf
$artifactHash = if ($artifactExists) { (Get-FileHash -LiteralPath $artifactFileFull -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }
$contentHashes = [ordered]@{}
if ($artifactExists) { $contentHashes[$entryFile] = $artifactHash }
$accessibilityFile = 'accessibility-receipt.json'
$accessibilityFull = Join-Path $artifactRootFull $accessibilityFile
$accessibilityHash = $null
$qualityFile = 'quality-receipt.json'
$qualityFull = Join-Path $artifactRootFull $qualityFile
$qualityHash = $null
$manifestFile = 'site.webmanifest'
$manifestFull = Join-Path $artifactRootFull $manifestFile
$manifestHash = $null
$iconFile = 'icon.svg'
$iconFull = Join-Path $artifactRootFull $iconFile
$iconHash = $null
$robotsFile = 'robots.txt'
$robotsFull = Join-Path $artifactRootFull $robotsFile
$robotsHash = $null
$sitemapFile = 'sitemap.xml'
$sitemapFull = Join-Path $artifactRootFull $sitemapFile
$sitemapHash = $null
if ($artifactExists) {
    $scanJson = (& (Join-Path $projectRoot 'ES/Automation/WebPageStudio/Test-ESWebPageStudioAccessibility.ps1') -HtmlPath $artifactFileFull | Out-String).Trim()
    [IO.File]::WriteAllText($accessibilityFull, $scanJson, [Text.UTF8Encoding]::new($false))
    $accessibilityHash = (Get-FileHash -LiteralPath $accessibilityFull -Algorithm SHA256).Hash.ToLowerInvariant()
    $contentHashes[$accessibilityFile] = $accessibilityHash
    $qualityJson = (& (Join-Path $projectRoot 'ES/Automation/WebPageStudio/Test-ESWebPageStudioQuality.ps1') -HtmlPath $artifactFileFull | Out-String).Trim()
    [IO.File]::WriteAllText($qualityFull, $qualityJson, [Text.UTF8Encoding]::new($false))
    $qualityHash = (Get-FileHash -LiteralPath $qualityFull -Algorithm SHA256).Hash.ToLowerInvariant()
    $contentHashes[$qualityFile] = $qualityHash
    if (Test-Path -LiteralPath $manifestFull -PathType Leaf) {
        $manifestHash = (Get-FileHash -LiteralPath $manifestFull -Algorithm SHA256).Hash.ToLowerInvariant()
        $contentHashes[$manifestFile] = $manifestHash
    }
    if (Test-Path -LiteralPath $iconFull -PathType Leaf) { $iconHash = (Get-FileHash -LiteralPath $iconFull -Algorithm SHA256).Hash.ToLowerInvariant(); $contentHashes[$iconFile] = $iconHash }
    if (Test-Path -LiteralPath $robotsFull -PathType Leaf) { $robotsHash = (Get-FileHash -LiteralPath $robotsFull -Algorithm SHA256).Hash.ToLowerInvariant(); $contentHashes[$robotsFile] = $robotsHash }
    if (Test-Path -LiteralPath $sitemapFull -PathType Leaf) { $sitemapHash = (Get-FileHash -LiteralPath $sitemapFull -Algorithm SHA256).Hash.ToLowerInvariant(); $contentHashes[$sitemapFile] = $sitemapHash }
}
$outputFiles = if ($artifactExists) { @($entryFile, $accessibilityFile, $qualityFile) + $(if ($manifestHash) { @($manifestFile) } else { @() }) + $(if ($iconHash) { @($iconFile) } else { @() }) + $(if ($robotsHash) { @($robotsFile) } else { @() }) + $(if ($sitemapHash) { @($sitemapFile) } else { @() }) } else { @($entryFile) }
$contractStatus = if ($artifactExists) { 'implemented-unverified' } else { 'designed' }
$acceptanceStatus = if ($artifactExists) { 'static-verified' } else { 'designed' }
$design = [ordered]@{
    version = '1.0'; rootNodeId = $rootId; nodes = $nodes
    sourceMap = @([ordered]@{ sourceId = $sourceId; targetNodeId = $rootId; sourceHash = $inputHash; sourcePath = $sourcePath })
    tokens = $tokens; components = $components; responsiveProfiles = $responsive; states = $states; assets = @()
    measurementUncertainty = @(); knownLoss = @(); fallback = [ordered]@{ mode = 'explicit'; description = 'Unsupported visual effects remain reviewable and never silently execute.' }
}
$designHash = Get-Sha256 ($design | ConvertTo-Json -Depth 20 -Compress)
$outputHash = if ($artifactExists) { $artifactHash } else { $designHash }
$evidenceReceipts = [System.Collections.Generic.List[object]]::new()
$evidenceReceipts.Add([ordered]@{ stage = 'request-to-contract'; receiptId = 'request-contract-receipt'; receiptHash = $outputHash })
if ($accessibilityHash) { $evidenceReceipts.Add([ordered]@{ stage = 'accessibility-static-scan'; receiptId = 'accessibility-receipt'; receiptHash = $accessibilityHash }) }
if ($qualityHash) { $evidenceReceipts.Add([ordered]@{ stage = 'seo-performance-static-scan'; receiptId = 'quality-receipt'; receiptHash = $qualityHash }) }
$contract = [ordered]@{
    schemaVersion = 1; recordType = 'WebPageGenerationContract'; status = $contractStatus
    identity = [ordered]@{ projectKey = 'ESFramework'; pageId = $pageId; revisionId = $revisionId; profileId = $profileId; stateId = $stateId; inputHash = $inputHash; specHash = $designHash }
    webPageIntent = [ordered]@{ pageKind = [string]$request.pageKind; objective = [string]$request.objective; language = if ($request.PSObject.Properties['language']) { [string]$request.language } else { 'en' }; primaryAction = [string]$request.primaryAction; allowedOutput = 'static-html-css'; nonGoals = @('react', 'tailwind', 'business-data-mutation', 'unity-runtime') }
    designSpec = $design
    artifactPlan = [ordered]@{ entryFile = $entryFile; outputFiles = $outputFiles; fileAllowlist = $outputFiles; dependencyPolicy = 'no-dependencies' }
    webArtifact = [ordered]@{ rootDirectory = $artifactDirectory; files = $outputFiles; contentHashes = $contentHashes; externalLinks = @(); scripts = @() }
    previewRun = [ordered]@{ runId = 'pending-preview'; specHash = $designHash; profileId = $profileId; stateId = $stateId; nodeVersion = 'not-run'; browser = 'not-run'; network = 'disabled'; rootDirectory = $artifactDirectory; fileAllowlist = @($outputFiles); budgets = [ordered]@{ processSeconds = 30; port = 0; memoryMb = 512 }; executionPolicy = [ordered]@{ allowInstall = $false; allowGeneratedCode = $false; allowShell = $false }; securityChecks = @('html-script-policy', 'external-link-policy', 'xss-sanitization', 'asset-path-policy', 'file-allowlist') }
    previewSnapshot = [ordered]@{ snapshotId = 'pending-snapshot'; runId = 'pending-preview'; specHash = $designHash; profileId = $profileId; stateId = $stateId; htmlHash = if ($artifactHash) { $artifactHash } else { ('0' * 64) }; screenshotPath = 'pending/preview.png'; domSummary = [ordered]@{ nodeCount = 0; interactiveCount = 0 } }
    visualChecks = @('dom-structure', 'geometry', 'token', 'asset', 'pixel', 'human-review' | ForEach-Object { [ordered]@{ checkId = "pending-$($_)"; category = $_; status = 'not-run'; targetId = $rootId; finding = 'No preview run has been authorized.'; evidenceRefs = @() } })
    revisionPatches = @()
    evidence = [ordered]@{ inputHash = $inputHash; outputHash = $outputHash; receipts = @($evidenceReceipts); acceptanceStatus = $acceptanceStatus; runtimeStatus = 'runtime-not-run' }
    nonClaims = @('No browser, backend, network, Unity or release process was run by this converter.')
}
$json = $contract | ConvertTo-Json -Depth 30
if ([string]::IsNullOrWhiteSpace($OutputPath)) { $OutputPath = Join-Path (Split-Path -Parent $requestFull) 'web-page-contract.json' }
$outputFull = [IO.Path]::GetFullPath($OutputPath)
if (-not $outputFull.StartsWith($projectRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputPath must remain under the project root.' }
if (Test-Path -LiteralPath $outputFull) { throw "Refusing to overwrite existing contract: $outputFull" }
$parent = Split-Path -Parent $outputFull
if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
[IO.File]::WriteAllText($outputFull, $json, [Text.UTF8Encoding]::new($false))
$contract
