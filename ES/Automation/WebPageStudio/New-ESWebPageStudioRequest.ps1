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
    [switch]$EnableNetwork,
    [string]$ApiBase = '',
    [string[]]$Allowlist = @(),
    [ValidateRange(1, 300)][int]$TimeoutSeconds = 10,
    [string]$PublicBaseUrl = '',
    [ValidateSet('always','hourly','daily','weekly','monthly','yearly','never')][string]$SitemapChangeFreq = 'weekly',
    [ValidateRange(0.0,1.0)][double]$SitemapPriority = 0.8,
    [string[]]$SitemapPaths = @('./'),
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

if ($EnableNetwork -and [string]::IsNullOrWhiteSpace($ApiBase)) { throw 'EnableNetwork requires ApiBase.' }
if ($EnableNetwork -and $Allowlist.Count -eq 0) { throw 'EnableNetwork requires at least one Allowlist entry.' }
if (-not $EnableNetwork -and ($ApiBase -or $Allowlist.Count -gt 0)) { throw 'ApiBase and Allowlist require EnableNetwork.' }
if ($PublicBaseUrl -and (($PublicBaseUrl -notmatch '^https://[^\s/]+(?:/[^\s]*)?/?$') -or $PublicBaseUrl.Contains('?') -or $PublicBaseUrl.Contains('#'))) { throw 'PublicBaseUrl must be an absolute HTTPS origin/path without query or fragment.' }
if ($SitemapPaths.Count -eq 0 -or @($SitemapPaths | Where-Object { [string]::IsNullOrWhiteSpace($_) -or [IO.Path]::IsPathRooted($_) -or $_ -match '(^|[/\\])\.\.([/\\]|$)' -or $_ -match '[?#]' }).Count -gt 0) { throw 'SitemapPaths must contain one or more project-relative paths without traversal, query, or fragment.' }

$slug = (($Objective.ToLowerInvariant() -replace '[^a-z0-9]+', '-') -replace '(^-|-$)', '')
if ([string]::IsNullOrWhiteSpace($slug)) {
    $hashAlgorithm = [Security.Cryptography.SHA256]::Create()
    try { $digest = $hashAlgorithm.ComputeHash([Text.UTF8Encoding]::new($false).GetBytes($Objective)) } finally { $hashAlgorithm.Dispose() }
    $suffix = ([BitConverter]::ToString($digest).Replace('-', '').ToLowerInvariant()).Substring(0, 8)
    $slug = "web-page-$suffix"
}
$request = [ordered]@{
    schemaVersion = 1
    recordType = 'WebPageStudioRequest'
    requestId = [guid]::NewGuid().ToString('N')
    pageKind = $PageKind
    objective = $Objective
    audience = $Audience
    primaryAction = $PrimaryAction
    language = $Language
    visualDirection = [ordered]@{ style = $VisualStyle; motion = $MotionLevel; density = $LayoutDensity; referencePaths = @() }
    responsiveProfiles = @(
        [ordered]@{ id = 'desktop'; width = 1440; height = 900 },
        [ordered]@{ id = 'mobile'; width = 390; height = 844 }
    )
    states = @('default', 'loading', 'error')
    backend = [ordered]@{ mode = if ($EnableNetwork) { 'user-authorized-service' } else { 'mock-contract-only' }; apiBase = $ApiBase }
    network = [ordered]@{ enabled = [bool]$EnableNetwork; allowlist = @($Allowlist); timeoutSeconds = $TimeoutSeconds }
    publicBaseUrl = $PublicBaseUrl.TrimEnd('/')
    sitemap = [ordered]@{ changefreq = $SitemapChangeFreq; priority = $SitemapPriority; paths = @($SitemapPaths) }
    output = [ordered]@{ format = 'static-html-css'; entryFile = 'index.html'; outputDirectory = "ES/Output/WebPageStudio/$slug/" }
    acceptance = [ordered]@{ requirePreview = $false; requireVisualReview = $true; requireRuntime = [bool]$EnableNetwork }
    nonClaims = @('No preview or network process was started by this request compiler.')
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path (Get-Location) "ES/Output/WebPageStudio/requests/$slug-$([guid]::NewGuid().ToString('N')).json"
}
$fullOutput = [IO.Path]::GetFullPath($OutputPath)
$root = [IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\') + '\'
if (-not $fullOutput.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputPath must remain under the project root.' }
if (Test-Path -LiteralPath $fullOutput) { throw "Refusing to overwrite existing request: $fullOutput" }
$parent = Split-Path -Parent $fullOutput
if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
[IO.File]::WriteAllText($fullOutput, ($request | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
$request | ConvertTo-Json -Depth 8
