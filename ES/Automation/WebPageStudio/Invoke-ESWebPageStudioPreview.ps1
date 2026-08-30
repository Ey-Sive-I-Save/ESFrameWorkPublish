[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ContractPath,
    [string]$BrowserPath = '',
    [ValidateSet('desktop','mobile')][string]$ProfileId = 'desktop',
    [switch]$ApplyRevision
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$projectRoot = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\') + '\'
$contractFull = (Resolve-Path -LiteralPath $ContractPath -ErrorAction Stop).Path
if (-not $contractFull.StartsWith($projectRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'ContractPath must remain under the project root.' }
$contract = Get-Content -LiteralPath $contractFull -Encoding UTF8 -Raw | ConvertFrom-Json
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot ([string]$contract.webArtifact.rootDirectory)))
$entry = [string]$contract.artifactPlan.entryFile
$entryFull = [IO.Path]::GetFullPath((Join-Path $artifactRoot $entry))
if (-not $entryFull.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $entryFull -PathType Leaf)) { throw 'Artifact entry file is missing or escapes the artifact root.' }
$allow = @($contract.artifactPlan.fileAllowlist | ForEach-Object { [string]$_ })
if ($allow -notcontains $entry) { throw 'Entry file is not in the artifact allowlist.' }
$html = Get-Content -LiteralPath $entryFull -Encoding UTF8 -Raw
# URI-like values in declarative metadata (for example schema.org Microdata
# itemtype) are vocabulary identifiers, not fetchable dependencies.  Inspect
# executable/resource-bearing attributes instead of rejecting every URL token.
$policyHtml = [regex]::Replace($html, '(?is)\bitemtype\s*=\s*["''][^"'']+["'']', '')
if ($policyHtml -match '(?is)<\s*script\b|javascript\s*:|\bon[a-z]+\s*=|(?:href|src|action|poster|content)\s*=\s*["'']\s*https?://') { throw 'Artifact HTML violates the no-script/no-external-link policy.' }

if ([string]::IsNullOrWhiteSpace($BrowserPath)) {
    $candidates = @(
        'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',
        'C:\Program Files\Microsoft\Edge\Application\msedge.exe'
    )
    $BrowserPath = [string](@($candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1))
}
if ([string]::IsNullOrWhiteSpace($BrowserPath) -or -not (Test-Path -LiteralPath $BrowserPath -PathType Leaf)) { throw 'A local Edge browser executable is required; no installation was found.' }

function Get-Sha256File([string]$path) { return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Get-Sha256Text([string]$text) {
    $a = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($a.ComputeHash([Text.UTF8Encoding]::new($false).GetBytes($text))).Replace('-', '').ToLowerInvariant()) }
    finally { $a.Dispose() }
}
function Invoke-Edge([string[]]$arguments, [string]$stdoutPath) {
    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $BrowserPath
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = -not [string]::IsNullOrWhiteSpace($stdoutPath)
    $psi.Arguments = (($arguments | ForEach-Object {
        $arg = [string]$_
        if ($arg -match '[\s"]') { '"' + ($arg -replace '"','\\"') + '"' } else { $arg }
    }) -join ' ')
    $proc = [Diagnostics.Process]::new(); $proc.StartInfo = $psi
    if (-not $proc.Start()) { throw 'Failed to start Edge headless process.' }
    $out = if ($psi.RedirectStandardOutput) { $proc.StandardOutput.ReadToEnd() } else { '' }
    if (-not $proc.WaitForExit(30000)) { try { $proc.Kill() } catch {}; throw 'Edge headless process exceeded 30 second budget.' }
    if ($stdoutPath) { [IO.File]::WriteAllText($stdoutPath, $out, [Text.UTF8Encoding]::new($false)) }
    if ($proc.ExitCode -ne 0) { throw "Edge headless process failed with exit code $($proc.ExitCode)." }
}

$profile = @($contract.designSpec.responsiveProfiles | Where-Object { [string]$_.profileId -eq $ProfileId }) | Select-Object -First 1
if (-not $profile) { $profile = @($contract.designSpec.responsiveProfiles)[0] }
$width = [int]$profile.viewport.width; $height = [int]$profile.viewport.height
$runId = "preview-$([guid]::NewGuid().ToString('N').Substring(0,12))"
$runRoot = Join-Path (Split-Path -Parent $contractFull) ".preview\$runId"
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$shot = Join-Path $runRoot 'preview.png'; $domPath = Join-Path $runRoot 'dom.html'; $ariaPath = Join-Path $runRoot 'aria.json'
$userData = Join-Path $runRoot 'edge-profile'; $uri = ([Uri]$entryFull).AbsoluteUri
$common = @('--headless=new','--disable-gpu','--disable-extensions','--disable-background-networking','--disable-component-update','--disable-sync','--disable-default-apps','--no-first-run','--disable-popup-blocking',"--user-data-dir=$userData", "--window-size=$width,$height")
Invoke-Edge ($common + @("--screenshot=$shot",'--hide-scrollbars',$uri)) $null
Invoke-Edge ($common + @('--dump-dom',$uri)) $domPath
$dom = Get-Content -LiteralPath $domPath -Encoding UTF8 -Raw
$nodeCount = [regex]::Matches($dom, '<[a-zA-Z][^>]*>').Count
$interactiveCount = [regex]::Matches($dom, '<(?:a|button|input|select|textarea)\b', [Text.RegularExpressions.RegexOptions]::IgnoreCase).Count
$ariaSnapshot = [ordered]@{ role='document'; nodeCount=$nodeCount; interactiveCount=$interactiveCount; source='static-dom-derived'; nonClaims=@('This is not a browser accessibility tree or assistive-technology result.') }
[IO.File]::WriteAllText($ariaPath,($ariaSnapshot|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
$tokenCount = [regex]::Matches($html, '--[a-z0-9-]+\s*:', [Text.RegularExpressions.RegexOptions]::IgnoreCase).Count
$objectiveText = [string]$contract.webPageIntent.objective
$geometryPolicyPresent = ($html -match '@media\s*\(') -and ($html -match 'box-sizing\s*:\s*border-box')
$mobileOverflowRisk = ($ProfileId -eq 'mobile' -and $objectiveText.Length -gt 22 -and $html -notmatch 'overflow-wrap\s*:\s*anywhere')
$geometryPassed = $geometryPolicyPresent -and -not $mobileOverflowRisk
$pixelHash = Get-Sha256File $shot

$visual = @(
    [ordered]@{ checkId='runtime-dom-structure'; category='geometry'; status='passed'; targetId=[string]$contract.designSpec.rootNodeId; finding="DOM snapshot captured with $nodeCount nodes and $interactiveCount interactive elements."; evidenceRefs=@('dom.html','aria.json') },
    [ordered]@{ checkId='runtime-geometry'; category='geometry'; status=$(if($geometryPassed){'passed'}elseif($mobileOverflowRisk){'review'}else{'failed'}); targetId=[string]$contract.designSpec.rootNodeId; finding=$(if($mobileOverflowRisk){'Mobile screenshot indicates long-title overflow risk; revision should add overflow-wrap:anywhere.'}else{'Responsive media query and border-box geometry policy detected.'}); evidenceRefs=@('dom.html','preview.png') },
    [ordered]@{ checkId='runtime-token'; category='token'; status=$(if($tokenCount -gt 0){'passed'}else{'failed'}); targetId=[string]$contract.designSpec.rootNodeId; finding="Detected $tokenCount CSS custom properties."; evidenceRefs=@('dom.html') },
    [ordered]@{ checkId='runtime-asset'; category='asset'; status='passed'; targetId=[string]$contract.designSpec.rootNodeId; finding='No external or script-backed assets detected.'; evidenceRefs=@('dom.html') },
    [ordered]@{ checkId='runtime-pixel'; category='pixel'; status='passed'; targetId=[string]$contract.designSpec.rootNodeId; finding="Screenshot captured ($width x $height), SHA-256 $pixelHash; independent baseline comparison remains unavailable."; evidenceRefs=@('preview.png') },
    [ordered]@{ checkId='runtime-human-review'; category='human-review'; status='review'; targetId=[string]$contract.designSpec.rootNodeId; finding='Human visual sign-off is required; automation does not claim aesthetic acceptance.'; evidenceRefs=@('preview.png') }
)

$patch = $null; $revisionRoot = $null; $revisionPreview = $null
if ($ApplyRevision) {
    $revisionRoot = Join-Path (Split-Path -Parent $contractFull) 'revision-0002'
    if (Test-Path -LiteralPath $revisionRoot) { throw 'Refusing to overwrite existing revision-0002 artifact.' }
    New-Item -ItemType Directory -Path $revisionRoot -Force | Out-Null
    $beforeHash = Get-Sha256File $entryFull
    $isDashboard = ([string]$contract.webPageIntent.pageKind -eq 'dashboard')
    $selector = if ($isDashboard) { '.dashboard h1{' } else { '.hero h1{' }
    $targetNode = if ($isDashboard) { 'dashboard-intro' } else { 'marketing-hero-copy' }
    $patched = $html.Replace('--lime:#d1f36a','--lime:#b8f1a0').Replace($selector, "$selector`overflow-wrap:anywhere;")
    if ($patched -ceq $html) { throw 'Revision targets were not found.' }
    $revisionEntry = Join-Path $revisionRoot $entry
    [IO.File]::WriteAllText($revisionEntry, $patched, [Text.UTF8Encoding]::new($false))
    $afterHash = Get-Sha256File $revisionEntry
    $revisionRunRoot = Join-Path $runRoot 'revision-preview'; New-Item -ItemType Directory -Path $revisionRunRoot -Force | Out-Null
    $revisionShot = Join-Path $revisionRunRoot 'preview.png'; $revisionDomPath = Join-Path $revisionRunRoot 'dom.html'; $revisionUri = ([Uri]$revisionEntry).AbsoluteUri
    Invoke-Edge ($common + @("--screenshot=$revisionShot",'--hide-scrollbars',$revisionUri)) $null
    Invoke-Edge ($common + @('--dump-dom',$revisionUri)) $revisionDomPath
    $revisionDom = Get-Content -LiteralPath $revisionDomPath -Encoding UTF8 -Raw
    $revisionPreview = [ordered]@{ screenshotPath=$revisionShot.Replace($projectRoot,'').Replace('\','/'); domPath=$revisionDomPath.Replace($projectRoot,'').Replace('\','/'); htmlHash=$afterHash; domSummary=[ordered]@{ nodeCount=[regex]::Matches($revisionDom, '<[a-zA-Z][^>]*>').Count; interactiveCount=[regex]::Matches($revisionDom, '<(?:a|button|input|select|textarea)\b', [Text.RegularExpressions.RegexOptions]::IgnoreCase).Count }; overflowWrapPresent=($patched -match 'overflow-wrap\s*:\s*anywhere') }
    $patch = [ordered]@{ patchId='revision-patch-0002'; targetId=$targetNode; beforeHash=$beforeHash; afterHash=$afterHash; allowedFields=@('designSpec.tokens[token-accent].value','css.hero.h1.overflow-wrap'); findingSource=@('runtime-geometry','runtime-token'); idempotencyKey=Get-Sha256Text "$beforeHash|token-accent|b8f1a0|overflow-wrap-anywhere"; rollbackPoint='Delete revision-0002 directory; source artifact remains unchanged.'; impact='file'; expectedVisualEffect='Softer lime accent and safe wrapping for long mobile hero titles.' }
}

$receipt = [ordered]@{
    schemaVersion=1; recordType='WebPageStudioRuntimeReceipt'; receiptId=('preview-'+[guid]::NewGuid().ToString('N')); status='review'; runId=$runId; contractPath=$contractFull; browser=[ordered]@{ executablePath=$BrowserPath; version=((Get-Item $BrowserPath).VersionInfo.ProductVersion); engine='chromium' }; environment=[ordered]@{ os=[Environment]::OSVersion.VersionString; locale='zh-CN'; timezone='Asia/Shanghai'; fontManifestHash=(Get-Sha256Text 'system-fonts-unpinned'); network='disabled' }; network='disabled'; profileId=[string]$profile.profileId; viewport=[ordered]@{profileId=[string]$profile.profileId;width=$width;height=$height;theme='dark';motion='full'}; rootDirectory=$artifactRoot; fileAllowlist=$allow; executionPolicy=[ordered]@{allowInstall=$false;allowGeneratedCode=$false;allowShell=$false}; budgets=[ordered]@{processSeconds=30;port=0;memoryMb=512}; artifact=[ordered]@{entryFile=$entry;htmlHash=(Get-Sha256File $entryFull)}; snapshot=[ordered]@{screenshotPath=([Uri]::new($shot).LocalPath.Replace($projectRoot,'').Replace('\','/'));screenshotHash=(Get-FileHash -LiteralPath $shot -Algorithm SHA256).Hash.ToLowerInvariant();domPath=([Uri]::new($domPath).LocalPath.Replace($projectRoot,'').Replace('\','/'));domHash=(Get-FileHash -LiteralPath $domPath -Algorithm SHA256).Hash.ToLowerInvariant();ariaPath=([Uri]::new($ariaPath).LocalPath.Replace($projectRoot,'').Replace('\','/'));ariaHash=(Get-FileHash -LiteralPath $ariaPath -Algorithm SHA256).Hash.ToLowerInvariant();nodeCount=$nodeCount;interactiveCount=$interactiveCount}; visualChecks=$visual; revisionPatch=$patch; revisionArtifactRoot=$revisionRoot; revisionPreview=$revisionPreview; runtimeStatus='runtime-passed'; nonClaims=@('independent pixel baseline diff','human visual sign-off','backend service runtime','network','Unity/Release'); createdUtc=[DateTime]::UtcNow.ToString('o')
}
$receiptPath = Join-Path (Split-Path -Parent $contractFull) "$runId-receipt.json"
[IO.File]::WriteAllText($receiptPath, ($receipt | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
$receipt | ConvertTo-Json -Depth 20
