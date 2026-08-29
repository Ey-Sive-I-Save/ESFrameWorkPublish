[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ContractPath,
    [string]$ReportPath = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$root = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\') + '\'
$full = (Resolve-Path -LiteralPath $ContractPath -ErrorAction Stop).Path
if (-not $full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'ContractPath must remain under the project root.' }

Import-Module (Join-Path (Get-Location) 'ES/Automation/Contracts/ESJsonSchemaLite.psm1') -Force
$contract = Get-Content -Raw -Encoding UTF8 -LiteralPath $full | ConvertFrom-Json -ErrorAction Stop
$schemaPath = Join-Path (Get-Location) 'ES/Automation/Contracts/es-web-page-generation-v1.schema.json'
$schemaErrors = @(Test-ESJsonSchemaValue -SchemaPath $schemaPath -Value $contract)
$cases = [System.Collections.Generic.List[object]]::new()

function Add-Case([string]$name, [bool]$ok, [string]$detail) {
    $cases.Add([ordered]@{ case = $name; status = if ($ok) { 'passed' } else { 'failed' }; detail = $detail })
}
function Get-Sha256([string]$text) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($algorithm.ComputeHash([Text.UTF8Encoding]::new($false).GetBytes($text))).Replace('-', '').ToLowerInvariant()) }
    finally { $algorithm.Dispose() }
}
function Same([object]$a, [object]$b) { return ([string]$a -ceq [string]$b) }

Add-Case 'schema' ($schemaErrors.Count -eq 0) $(if ($schemaErrors.Count) { $schemaErrors -join '; ' } else { 'contract matches generation schema' })

if ($schemaErrors.Count -eq 0) {
    $identity = $contract.identity
    $preview = $contract.previewRun
    $snapshot = $contract.previewSnapshot
    $designJson = $contract.designSpec | ConvertTo-Json -Depth 40 -Compress
    $computedSpecHash = Get-Sha256 $designJson

    Add-Case 'identity-profile-binding' ((Same $identity.profileId $preview.profileId) -and (Same $identity.profileId $snapshot.profileId)) 'identity.profileId binds previewRun and previewSnapshot'
    Add-Case 'identity-state-binding' ((Same $identity.stateId $preview.stateId) -and (Same $identity.stateId $snapshot.stateId)) 'identity.stateId binds previewRun and previewSnapshot'
    Add-Case 'spec-hash-binding' ((Same $identity.specHash $preview.specHash) -and (Same $identity.specHash $snapshot.specHash) -and (Same $identity.specHash $computedSpecHash)) 'identity.specHash binds designSpec and preview records'
    Add-Case 'input-hash-binding' ((Same $identity.inputHash $contract.evidence.inputHash) -and @($contract.designSpec.sourceMap | Where-Object { -not (Same $_.sourceHash $identity.inputHash) }).Count -eq 0) 'inputHash binds evidence and sourceMap'
    Add-Case 'preview-snapshot-run-binding' (Same $preview.runId $snapshot.runId) 'previewSnapshot.runId matches previewRun.runId'

    $nodes = @($contract.designSpec.nodes)
    $nodeIds = @($nodes | ForEach-Object { [string]$_.nodeId })
    $tokenIds = @($contract.designSpec.tokens | ForEach-Object { [string]$_.tokenId })
    $componentIds = @($contract.designSpec.components | ForEach-Object { [string]$_.componentId })
    $sourceIds = @($contract.designSpec.sourceMap | ForEach-Object { [string]$_.sourceId })
    $duplicateNodeIds = @($nodeIds | Group-Object | Where-Object Count -gt 1)
    Add-Case 'design-id-uniqueness' ($duplicateNodeIds.Count -eq 0 -and @($tokenIds | Sort-Object -Unique).Count -eq $tokenIds.Count -and @($componentIds | Sort-Object -Unique).Count -eq $componentIds.Count -and @($sourceIds | Sort-Object -Unique).Count -eq $sourceIds.Count) 'design node, token, component, and source IDs are unique'
    $rootNode = @($nodes | Where-Object { [string]$_.nodeId -eq [string]$contract.designSpec.rootNodeId })
    $parentErrors = @($nodes | Where-Object { $null -ne $_.parentId -and $nodeIds -notcontains [string]$_.parentId })
    $childErrors = [System.Collections.Generic.List[string]]::new()
    foreach ($node in $nodes) {
        foreach ($childId in @($node.children)) {
            $childNode = @($nodes | Where-Object { [string]$_.nodeId -eq [string]$childId })
            if ($childNode.Count -ne 1 -or [string]$childNode[0].parentId -cne [string]$node.nodeId) { $childErrors.Add([string]$childId) }
        }
    }
    Add-Case 'design-tree-closure' ($rootNode.Count -eq 1 -and $null -eq $rootNode[0].parentId -and $parentErrors.Count -eq 0 -and $childErrors.Count -eq 0) 'root, parent, and child node references are closed'
    $sourceRefErrors = @($nodes | ForEach-Object { @($_.sourceRefs | Where-Object { $sourceIds -notcontains [string]$_ }) })
    $tokenRefErrors = @($nodes | ForEach-Object { @($_.tokenRefs | Where-Object { $tokenIds -notcontains [string]$_ }) })
    $mapTargetErrors = @($contract.designSpec.sourceMap | Where-Object { $nodeIds -notcontains [string]$_.targetNodeId })
    Add-Case 'design-reference-closure' ($sourceRefErrors.Count -eq 0 -and $tokenRefErrors.Count -eq 0 -and $mapTargetErrors.Count -eq 0) 'node source/token refs and source map targets resolve'
    Add-Case 'profile-state-closure' (@($contract.designSpec.responsiveProfiles | Where-Object { [string]$_.profileId -eq [string]$identity.profileId }).Count -eq 1 -and @($contract.designSpec.states | Where-Object { [string]$_.stateId -eq [string]$identity.stateId }).Count -eq 1) 'identity profile and state exist in designSpec'
    $visualTargetErrors = @($contract.visualChecks | Where-Object { $nodeIds -notcontains [string]$_.targetId })
    $revisionTargetErrors = @($contract.revisionPatches | Where-Object { $nodeIds -notcontains [string]$_.targetId })
    $revisionCountOk = @($contract.revisionPatches).Count -le 1
    Add-Case 'finding-target-closure' ($visualTargetErrors.Count -eq 0 -and $revisionTargetErrors.Count -eq 0 -and $revisionCountOk) 'visual checks and at most one revision patch target known nodes'

    $entry = [string]$contract.artifactPlan.entryFile
    $outputs = @($contract.artifactPlan.outputFiles | ForEach-Object { [string]$_ })
    $allowlist = @($contract.artifactPlan.fileAllowlist | ForEach-Object { [string]$_ })
    $artifactFiles = @($contract.webArtifact.files | ForEach-Object { [string]$_ })
    Add-Case 'artifact-entry-binding' ($outputs -contains $entry -and $allowlist -contains $entry -and $artifactFiles -contains $entry) 'entryFile is present in output and file allowlists'
    Add-Case 'artifact-allowlist-binding' (@($outputs | Where-Object { $allowlist -notcontains $_ }).Count -eq 0 -and @($artifactFiles | Where-Object { $allowlist -notcontains $_ }).Count -eq 0) 'all artifact files are allowlisted'
    Add-Case 'preview-allowlist-binding' ((@($preview.fileAllowlist | ForEach-Object { [string]$_ }) | Where-Object { $allowlist -notcontains $_ }).Count -eq 0) 'preview fileAllowlist is bounded by artifact allowlist'
    Add-Case 'static-output-policy' ([string]$contract.webPageIntent.allowedOutput -eq 'static-html-css' -and [string]$contract.artifactPlan.dependencyPolicy -in @('no-dependencies', 'allowlisted-local-only')) 'static output policy is explicit'
    Add-Case 'network-boundary' ([string]$preview.network -eq 'disabled' -and [bool]$preview.executionPolicy.allowInstall -eq $false -and [bool]$preview.executionPolicy.allowGeneratedCode -eq $false -and [bool]$preview.executionPolicy.allowShell -eq $false) 'preview cannot install, generate code, shell, or access network'
    $runtimeNotRun = [string]$contract.evidence.runtimeStatus -eq 'runtime-not-run'
    $artifactHashKeys = @($contract.webArtifact.contentHashes.PSObject.Properties | ForEach-Object { $_.Name })
    $artifactHashCoverage = $artifactFiles.Count -eq 0 -or (@($artifactFiles | Where-Object { $artifactHashKeys -notcontains $_ }).Count -eq 0)
    Add-Case 'artifact-hash-coverage' ($artifactHashCoverage -or ($runtimeNotRun -and $artifactHashKeys.Count -eq 0)) 'generated artifacts are hashed, or explicitly pending before runtime'

    $artifactContentOk = $true
    $artifactAccessibilityOk = $true
    $artifactHashDrift = $false
    if ($artifactHashKeys.Count -gt 0) {
        foreach ($file in $artifactFiles) {
            $relativeRoot = [string]$contract.webArtifact.rootDirectory
            $artifactPath = [IO.Path]::GetFullPath((Join-Path (Get-Location) (Join-Path $relativeRoot $file)))
            if (-not $artifactPath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
                $artifactContentOk = $false
                continue
            }
            $actualHash = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
            if (-not (Same $actualHash ([string]$contract.webArtifact.contentHashes.PSObject.Properties[$file].Value))) { $artifactHashDrift = $true }
            if ([IO.Path]::GetExtension($file).ToLowerInvariant() -eq '.html') {
                $html = Get-Content -LiteralPath $artifactPath -Encoding UTF8 -Raw
                $securityHtml = [regex]::Replace($html, '(?is)<(?:meta|link)\b[^>]*(?:canonical|alternate|og:url|og:image|twitter:image)[^>]*>', '')
                # schema.org Microdata itemtype is a declarative vocabulary reference, not a fetched resource.
                $securityHtml = $securityHtml -replace '(?is)\sitemtype=["'']https://schema\.org/WebPage["'']', ''
                $securityBlocked = $securityHtml -match '(?is)<\s*script\b|javascript\s*:|\bon[a-z]+\s*=|https?://'
                $structureMissing = $html -notmatch '(?is)<!doctype\s+html|<\s*style\b|<\s*body\b'
                if ($securityBlocked -or $structureMissing) { $artifactContentOk = $false }
                $hasLandmark = $html -match '(?is)<main\b[^>]*\bid=["'']main-content["'']'
                $hasSkipLink = $html -match '(?is)<a\b[^>]*class=["'']skip-link["''][^>]*href=["'']#main-content["'']'
                $hasFocusPolicy = $html -match '(?is)focus-visible'
                $hasReducedMotion = $html -match '(?is)prefers-reduced-motion'
                $hasSeoMetadata = $html -match '(?is)<meta\b[^>]*name=["'']description["''][^>]*content=["''][^"'']+["'']' -and $html -match '(?is)<meta\b[^>]*name=["'']generator["'']' -and $html -match '(?is)<meta\b[^>]*name=["'']referrer["'']'
                $hasThemeMetadata = $html -match '(?is)<meta\b[^>]*name=["'']color-scheme["'']' -and $html -match '(?is)<meta\b[^>]*name=["'']theme-color["'']'
                $hasPrintFallback = $html -match '(?is)@media\s+print' -and $html -match '(?is)forced-colors:active'
                $hasLocaleBinding = $html -match '(?is)data-locale=["''](?:en|zh-CN|ar)["'']'
                $hasStateCoverage = ([regex]::Matches($html, '(?is)class=["'']state-card(?:\s|["''])').Count -ge 3) -and $html -match '(?is)<[^>]+role=["'']alert["'']'
                $forms = [regex]::Matches($html, '(?is)<form\b[^>]*>(.*?)</form>')
                $formLabelsOk = $true
                foreach ($form in $forms) {
                    foreach ($input in [regex]::Matches($form.Groups[1].Value, '(?is)<input\b[^>]*\bid=["'']([^"'']+)["'']')) {
                        if ($form.Groups[1].Value -notmatch ('(?is)<label\b[^>]*\bfor=["'']' + [regex]::Escape($input.Groups[1].Value) + '["'']')) { $formLabelsOk = $false }
                    }
                }
                if (-not ($html -match '(?is)<html\b[^>]*\blang=["''][^"'']+["'']' -and $html -match '(?is)<html\b[^>]*\bdir=["''](?:ltr|rtl)["'']' -and $hasLocaleBinding -and $hasLandmark -and $hasSkipLink -and $hasFocusPolicy -and $hasReducedMotion -and $formLabelsOk -and $hasPrintFallback -and $hasStateCoverage)) { $artifactAccessibilityOk = $false }
                if (-not ($hasSeoMetadata -and $hasThemeMetadata)) { $artifactContentOk = $false }
            }
        }
    }
    Add-Case 'artifact-content-security' ($artifactContentOk -and -not $artifactHashDrift) 'allowlisted artifacts stay under the project root, hashes match, and HTML has no scripts/external links/event handlers'
    Add-Case 'artifact-accessibility-baseline' $artifactAccessibilityOk 'HTML declares language, main landmark, skip link, visible focus policy, reduced-motion and forced-colors policies, print fallback, and labels all form controls'

    $zeroHash = ('0' * 64)
    $checksNotRun = @($contract.visualChecks | Where-Object { [string]$_.status -ne 'not-run' }).Count -eq 0
    $statusBoundary = ([string]$contract.status -eq 'designed' -and [string]$contract.evidence.acceptanceStatus -eq 'designed') -or ([string]$contract.status -eq 'implemented-unverified' -and [string]$contract.evidence.acceptanceStatus -eq 'static-verified')
    Add-Case 'runtime-boundary' ($runtimeNotRun -and $checksNotRun -and $statusBoundary) 'runtime-not-run contracts keep visual checks unexecuted'
    $hasArtifactHash = $artifactHashKeys.Count -gt 0
    $artifactEntryHash = if ($hasArtifactHash) { [string]$contract.webArtifact.contentHashes.PSObject.Properties[$entry].Value } else { '' }
    $snapshotBoundary = if ($hasArtifactHash) { (Same $snapshot.htmlHash $artifactEntryHash) } else { (Same $snapshot.htmlHash $zeroHash) }
    Add-Case 'placeholder-snapshot' ($snapshotBoundary -and ([string]$snapshot.screenshotPath -like 'pending/*')) 'unexecuted preview uses a pending screenshot and truthful HTML hash'
    $knownReceiptHashes = @($contract.webArtifact.contentHashes.PSObject.Properties | ForEach-Object { [string]$_.Value }) + @([string]$contract.evidence.outputHash)
    $receiptOutputBinding = @($contract.evidence.receipts | Where-Object { $knownReceiptHashes -notcontains ([string]$_.receiptHash) }).Count -eq 0
    $outputHashBinding = if ($hasArtifactHash) { (Same $contract.evidence.outputHash $artifactEntryHash) } else { (Same $contract.evidence.outputHash $identity.specHash) }
    Add-Case 'receipt-output-binding' ($receiptOutputBinding -and $outputHashBinding) 'receipt and output hashes bind to the spec or artifact according to status'
}

$failed = @($cases | Where-Object { $_.status -eq 'failed' })
$report = [ordered]@{
    schemaVersion = 1
    validator = 'Test-ESWebPageStudioContract'
    status = if ($failed.Count) { 'failed' } else { 'passed' }
    contractPath = $full
    caseCount = $cases.Count
    passedCount = @($cases | Where-Object { $_.status -eq 'passed' }).Count
    failedCount = $failed.Count
    cases = @($cases)
    staticStatus = if ($failed.Count) { 'static-failed' } else { 'static-passed' }
    runtimeStatus = 'runtime-not-run'
    claimsNotProven = @('browser rendering and screenshots', 'backend service behavior', 'network availability', 'Unity/Worker runtime', 'release acceptance')
}
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path (Split-Path -Parent $full) 'contract-validation.json'
}
$reportFull = [IO.Path]::GetFullPath($ReportPath)
if (-not $reportFull.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'ReportPath must remain under the project root.' }
[IO.File]::WriteAllText($reportFull, ($report | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
$report | ConvertTo-Json -Depth 20
if ($failed.Count) { exit 1 }
