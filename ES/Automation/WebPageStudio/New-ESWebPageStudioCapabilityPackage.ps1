[CmdletBinding()]
param(
    [string]$OutputPath = 'ES/Output/WebPageStudio/webpagestudio-capability-package.json'
)
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$root = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\') + '\'
$out = [IO.Path]::GetFullPath((Join-Path $root $OutputPath))
if (-not $out.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputPath must remain under project root.' }
if (Test-Path -LiteralPath $out) { throw "Refusing to overwrite existing package manifest: $out" }
$paths = @(
    'ES/Automation/WebPageStudio/Invoke-ESWebPageStudio.ps1',
    'ES/Automation/WebPageStudio/Invoke-ESWebPageStudioStatic.ps1',
    'ES/Automation/WebPageStudio/New-ESWebPageStudioRequest.ps1',
    'ES/Automation/WebPageStudio/New-ESWebPageStudioLocaleBundle.ps1',
    'ES/Automation/WebPageStudio/New-ESWebPageStudioOfflinePackage.ps1',
    'ES/Automation/WebPageStudio/New-ESWebPageStudioBackendContract.ps1',
    'ES/Automation/WebPageStudio/New-ESWebPageStudioKernelRequest.ps1',
    'ES/Automation/WebPageStudio/Invoke-ESWebPageStudioKernel.ps1',
    'ES/Automation/WebPageStudio/Test-ESWebPageStudioKernel.ps1',
    'ES/Automation/WebPageStudio/Invoke-ESWebPageStudioBackend.ps1',
    'ES/Automation/WebPageStudio/Test-ESWebPageStudioBackendContract.ps1',
    'ES/Automation/WebPageStudio/Convert-ESWebPageStudioRequest.ps1',
    'ES/Automation/WebPageStudio/Test-ESWebPageStudioRequest.ps1',
    'ES/Automation/WebPageStudio/Test-ESWebPageStudioContract.ps1',
    'ES/Automation/WebPageStudio/Test-ESWebPageStudioQuality.ps1',
    'ES/Automation/WebPageStudio/Test-ESWebPageStudioAccessibility.ps1',
    'ES/Automation/WebPageStudio/Test-ESWebPageStudioFreshness.ps1',
    'ES/Automation/WebPageStudio/Test-ESWebPageStudioLocaleBundleFreshness.ps1',
    'ES/Automation/WebPageStudio/Test-ESWebPageStudioOfflinePackage.ps1',
    'ES/Automation/WebPageStudio/Test-ESWebPageStudioBundleReplayReadiness.ps1',
    'ES/Automation/Contracts/es-web-page-generation-v1.schema.json',
    'ES/Automation/Contracts/es-web-page-studio-request-v1.schema.json',
    'ES/Automation/Contracts/es-web-page-studio-kernel-v1.schema.json',
    'ES/AISpace/Public/WebPageStudio/README.md',
    'ES/AISpace/Public/WebPageStudio/INDEX.yaml',
    'ES/AISpace/Public/WebPageStudio/ACCEPTANCE_MATRIX.md',
    'Documentation/AIKnowledge/entries/web-page-generation-advanced-capabilities.md',
    'Documentation/AIKnowledge/KnowledgeIndex.yaml'
)
$files = [System.Collections.Generic.List[object]]::new()
foreach ($relative in $paths) {
    $full = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "Package source missing: $relative" }
    $files.Add([ordered]@{ path = $relative; sha256 = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant() })
}
$package = [ordered]@{
    schemaVersion = 1
    recordType = 'ESWebPageStudioCapabilityPackage'
    packageId = 'es.webpagestudio.capabilities.v1'
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    network = 'disabled'
    runtimeStatus = 'runtime-not-run'
    entrypoints = [ordered]@{
        oneCommand = 'ES/Automation/WebPageStudio/Invoke-ESWebPageStudio.ps1'
        staticGenerator = 'ES/Automation/WebPageStudio/Invoke-ESWebPageStudioStatic.ps1'
        localeBundle = 'ES/Automation/WebPageStudio/New-ESWebPageStudioLocaleBundle.ps1'
        offlinePackage = 'ES/Automation/WebPageStudio/New-ESWebPageStudioOfflinePackage.ps1'
    }
    capabilities = @('responsive-html-css','accessibility-baseline','loading-empty-error-states','seo-metadata','schema.org-webpage-microdata','favicon-and-touch-icon','web-app-manifest','robots-and-sitemap','hreflang-localization','offline-service-worker-package','backend-contract-with-redaction-retry-cancellation','hash-freshness-evidence','replay-readiness')
    evidence = [ordered]@{
        staticQuality = 'ES/Automation/WebPageStudio/Test-ESWebPageStudioQuality.ps1'
        contract = 'ES/Automation/WebPageStudio/Test-ESWebPageStudioContract.ps1'
        accessibility = 'ES/Automation/WebPageStudio/Test-ESWebPageStudioAccessibility.ps1'
        freshness = 'ES/Automation/WebPageStudio/Test-ESWebPageStudioFreshness.ps1'
        acceptanceMatrix = 'ES/AISpace/Public/WebPageStudio/ACCEPTANCE_MATRIX.md'
    }
    sourceFiles = @($files)
    claimsNotProven = @('browser visual regression','axe or Lighthouse runtime','Service Worker installation and offline lifecycle','Unity/PlayMode/Worker runtime','production release acceptance')
    overwritePolicy = 'refuse-existing-output'
}
$parent = Split-Path -Parent $out
if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
[IO.File]::WriteAllText($out, ($package | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
$package | ConvertTo-Json -Depth 12
