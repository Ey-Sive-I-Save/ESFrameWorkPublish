[CmdletBinding()]
param([string]$ProjectRoot = (Get-Location).Path)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$root = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\')
if (-not (Test-Path -LiteralPath (Join-Path $root 'ES/Automation/WebPageStudio'))) { throw 'ProjectRoot is not an ESFramework project root.' }

function Find-Tool([string]$name) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $cmd) { return [ordered]@{ name = $name; available = $false; source = ''; version = '' } }
    $version = ''
    try { $version = (& $name --version 2>$null | Select-Object -First 1) } catch { }
    return [ordered]@{ name = $name; available = $true; source = [string]$cmd.Source; version = [string]$version }
}

$moduleCandidates = @(
    (Join-Path $root 'node_modules'),
    (Join-Path $root 'ES/Automation/WebPageStudio/node_modules')
)
$modules = foreach ($candidate in $moduleCandidates) {
    foreach ($name in @('playwright', 'lighthouse', 'axe-core')) {
        $path = Join-Path $candidate $name
        [ordered]@{ name = $name; root = $candidate; available = (Test-Path -LiteralPath $path -PathType Container) }
    }
}
$browserCandidates = @('C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe','C:\Program Files\Microsoft\Edge\Application\msedge.exe')
$browser = @($browserCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1)
$browserEvidence = if ($browser.Count -gt 0) { $bi=Get-Item -LiteralPath $browser[0]; [ordered]@{name='Microsoft Edge';available=$true;executablePath=$bi.FullName;version=$bi.VersionInfo.ProductVersion} } else { [ordered]@{name='Microsoft Edge';available=$false;executablePath='';version=''} }

[ordered]@{
    schemaVersion = 1
    recordType = 'WebPageStudioToolingProbe'
    projectRoot = $root
    tools = @((Find-Tool 'npx'), (Find-Tool 'playwright'), (Find-Tool 'lighthouse'))
    browsers = @($browserEvidence)
    localModules = @($modules)
    network = 'disabled'
    installAttempted = $false
    runtimeStatus = 'runtime-not-run'
    claimsNotProven = @('Playwright multi-browser execution', 'Lighthouse scores', 'axe-core accessibility results')
    nextAction = 'Install or provide an approved local toolchain explicitly, then rerun this probe.'
} | ConvertTo-Json -Depth 8
