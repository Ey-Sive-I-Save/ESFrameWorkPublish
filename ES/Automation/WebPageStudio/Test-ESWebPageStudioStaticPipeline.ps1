[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ArtifactDirectory,
    [string]$RequestPath = ''
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\') + '\'
$dir = if([IO.Path]::IsPathRooted($ArtifactDirectory)){[IO.Path]::GetFullPath($ArtifactDirectory)}else{[IO.Path]::GetFullPath((Join-Path (Get-Location) $ArtifactDirectory))}
if (-not $dir.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'ArtifactDirectory must remain under project root.' }
$checks = [Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [bool]$Passed, [string]$Detail) { $checks.Add([pscustomobject]@{ check=$Name; status=if($Passed){'passed'}else{'failed'}; detail=$Detail }) }

$required = @('index.html','web-page-contract.json','contract-validation.json','static-signals.json','deep-design.json','artifact-manifest.json','site.webmanifest','robots.txt','sitemap.xml','icon.svg')
$missing = @($required | Where-Object { -not (Test-Path -LiteralPath (Join-Path $dir $_) -PathType Leaf) })
$requiredDetail = if($missing.Count){'missing: '+($missing -join ', ')}else{'all static artifacts present'}
Add-Check 'required-artifacts' ($missing.Count -eq 0) $requiredDetail
$html = if(Test-Path (Join-Path $dir 'index.html')){Get-Content -Raw -Encoding UTF8 (Join-Path $dir 'index.html')}else{''}
Add-Check 'utf8-and-no-replacement' (-not $html.Contains([char]0xFFFD)) 'index.html decodes as strict UTF-8'
Add-Check 'network-disabled' (($html -notmatch '(?is)(?:src|href|action)=["'']https?://') -and ($html -match '(?is)STATIC\s*/\s*NO NETWORK|network-disabled|data-network=["'']disabled')) 'static HTML has no external resource URL; local JavaScript is allowed'
$contract = $null; try { $contract=Get-Content -Raw -Encoding UTF8 (Join-Path $dir 'web-page-contract.json') | ConvertFrom-Json } catch {}
Add-Check 'contract-parseable' ($null -ne $contract) 'web-page-contract.json is parseable'
$contractValidation=$null; try {$contractValidation=Get-Content -Raw -Encoding UTF8 (Join-Path $dir 'contract-validation.json')|ConvertFrom-Json}catch{}
Add-Check 'contract-validated' ($null -ne $contractValidation -and [string]$contractValidation.status -eq 'passed') 'contract validation receipt is passed'
$signals=$null; try {$signals=Get-Content -Raw -Encoding UTF8 (Join-Path $dir 'static-signals.json')|ConvertFrom-Json}catch{}
Add-Check 'static-signals-validated' ($null -ne $signals -and [string]$signals.status -eq 'passed') 'static signals receipt is passed'
$manifest=$null; try {$manifest=Get-Content -Raw -Encoding UTF8 (Join-Path $dir 'site.webmanifest')|ConvertFrom-Json}catch{}
Add-Check 'manifest-and-discovery' ($null -ne $manifest -and (Test-Path (Join-Path $dir 'robots.txt')) -and (Test-Path (Join-Path $dir 'sitemap.xml'))) 'manifest, robots and sitemap are present'
$artifactManifest=$null;try{$artifactManifest=Get-Content -Raw -Encoding UTF8 (Join-Path $dir 'artifact-manifest.json')|ConvertFrom-Json}catch{}
Add-Check 'ai-artifact-provenance' ($null -ne $artifactManifest -and [string]$artifactManifest.recordType -eq 'WebPageStudioAiArtifactManifest' -and -not [string]::IsNullOrWhiteSpace([string]$artifactManifest.sourceRevisionReceipt) -and [string]$artifactManifest.entryHash -match '^[a-f0-9]{64}$') 'artifact is copied from a hash-pinned AI revision receipt'
$requestOk=$true
if(-not [string]::IsNullOrWhiteSpace($RequestPath)){ $rp=[IO.Path]::GetFullPath((Join-Path (Get-Location) $RequestPath));$requestOk=$rp.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)-and(Test-Path -LiteralPath $rp -PathType Leaf); if($requestOk){try{$r=Get-Content -Raw -Encoding UTF8 $rp|ConvertFrom-Json;$requestOk=(-not [bool]$r.network.enabled)}catch{$requestOk=$false}} }
Add-Check 'request-static-profile' $requestOk 'request is optional; when supplied it must disable network'
$passed=@($checks|Where-Object status -eq 'passed').Count; $failed=$checks.Count-$passed
$result=[ordered]@{schemaVersion=1;recordType='WebPageStudioStaticPipelineReceipt';status=if($failed -eq 0){'passed'}else{'failed'};profile='static-only';artifactDirectory=$dir;checkCount=$checks.Count;passedCount=$passed;failedCount=$failed;checks=@($checks);runtimeStatus='runtime-not-run';releaseStatus='release-not-run';deterministic=$true;nonClaims=@('does-not-prove-browser-rendering','does-not-prove-network-or-backend','does-not-prove-release-acceptance')}
$result|ConvertTo-Json -Depth 10
if($failed -gt 0){exit 1}
