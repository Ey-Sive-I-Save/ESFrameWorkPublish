[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [switch]$RequireProvider
)

$ErrorActionPreference = 'Stop'
$strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)

function Add-Check {
    param([System.Collections.Generic.List[object]]$List, [string]$Id,
        [bool]$Passed, [string]$Message, [string]$Value = '')
    [void]$List.Add([ordered]@{
        id = $Id
        status = if ($Passed) { 'passed' } else { 'failed' }
        message = $Message
        value = $Value
    })
}

function Resolve-ProjectRoot {
    param([string]$Candidate)
    if ([string]::IsNullOrWhiteSpace($Candidate)) {
        return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..\..'))
    }
    return [System.IO.Path]::GetFullPath($Candidate)
}

function Resolve-AbsoluteFile {
    param([string]$Value, [string]$Name, [string]$Project)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    if (-not [System.IO.Path]::IsPathRooted($Value)) { return $null }
    $full = [System.IO.Path]::GetFullPath($Value)
    $prefix = $Project.TrimEnd('\') + '\'
    if (-not $full.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) { return $null }
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { return $null }
    return $full
}

function Resolve-ExplicitNode {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value) -or -not [System.IO.Path]::IsPathRooted($Value)) { return $null }
    $full = [System.IO.Path]::GetFullPath($Value)
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { return $null }
    if ([System.IO.Path]::GetFileName($full) -notmatch '(?i)^node(?:\.exe)?$') { return $null }
    return $full
}

function Test-ProjectPath {
    param([string]$Value, [string]$Project)
    if ([string]::IsNullOrWhiteSpace($Value) -or -not [System.IO.Path]::IsPathRooted($Value)) { return $false }
    try {
        $full = [System.IO.Path]::GetFullPath($Value).TrimEnd('\')
        $prefix = [System.IO.Path]::GetFullPath($Project).TrimEnd('\') + '\'
        return $full.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)
    }
    catch { return $false }
}

function Invoke-Probe {
    param([string]$FileName, [string[]]$Arguments, [string]$WorkingDirectory, [int]$TimeoutMs = 5000)
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $FileName
    $psi.WorkingDirectory = $WorkingDirectory
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $quotedArguments = foreach ($argument in $Arguments) {
        '"' + ([string]$argument).Replace('"', '\\"') + '"'
    }
    $psi.Arguments = ($quotedArguments -join ' ')
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $psi
    try {
        if (-not $process.Start()) { throw 'process-start-failed' }
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        while (-not $process.HasExited -and $stopwatch.ElapsedMilliseconds -lt $TimeoutMs) {
            Start-Sleep -Milliseconds 100
        }
        if (-not $process.HasExited) {
            try { $process.Kill($true) } catch {}
            return [ordered]@{ ok = $false; code = 'probe-timeout'; output = '' }
        }
        $stdout = $process.StandardOutput.ReadToEnd()
        $stderr = $process.StandardError.ReadToEnd()
        $output = (($stdout + "`n" + $stderr).Trim())
        if ($output.Length -gt 400) { $output = $output.Substring(0, 400) }
        return [ordered]@{ ok = $process.ExitCode -eq 0; code = [string]$process.ExitCode; output = $output }
    }
    catch {
        return [ordered]@{ ok = $false; code = 'probe-failed'; output = $_.Exception.Message.Substring(0, [Math]::Min(400, $_.Exception.Message.Length)) }
    }
    finally { $process.Dispose() }
}

$root = Resolve-ProjectRoot $ProjectRoot
$workerRoot = [System.IO.Path]::GetFullPath((Join-Path $root 'ES/Automation/Workers/Node/DeepSeekHarness'))
$tempRoot = [System.IO.Path]::GetFullPath((Join-Path $root 'ES/Automation/Temp/DeepSeekHarness'))
$configPath = Join-Path $tempRoot 'runtime.local.json'
$checks = [System.Collections.Generic.List[object]]::new()

Add-Check $checks 'project-root' (Test-Path -LiteralPath (Join-Path $root 'Assets') -PathType Container) 'Unity project root resolved.' $root
Add-Check $checks 'package-json' (Test-Path -LiteralPath (Join-Path $workerRoot 'package.json') -PathType Leaf) 'package.json exists.'
$lockPath = Join-Path $workerRoot 'package-lock.json'
Add-Check $checks 'package-lock' (Test-Path -LiteralPath $lockPath -PathType Leaf) 'package-lock.json is required before claiming frozen dependencies.'

$config = $null
$configValid = $false
if (Test-Path -LiteralPath $configPath -PathType Leaf) {
    try {
        $configText = $strictUtf8.GetString([System.IO.File]::ReadAllBytes($configPath))
        $config = $configText | ConvertFrom-Json
        $configValid = $null -ne $config
    }
    catch {
        Add-Check $checks 'runtime-config' $false 'runtime.local.json is invalid strict UTF-8 JSON.'
        $config = $null
    }
} else {
    Add-Check $checks 'runtime-config' $false 'runtime.local.json is required; run Install-ESDeepSeekHarness.ps1 first.'
}
if ($configValid) {
    $identityValid = ($config.schemaVersion -eq 1 -and
        [string]$config.declaration -eq 'es-deepseek' -and
        [string]$config.providerDeclaration -eq 'es-deepseek')
    Add-Check $checks 'runtime-identity' $identityValid 'runtime.local.json must declare schemaVersion 1 and es-deepseek.'
}

$nodeValue = [Environment]::GetEnvironmentVariable('ES_DEEPSEEK_NODE_PATH')
if ([string]::IsNullOrWhiteSpace($nodeValue)) { $nodeValue = [Environment]::GetEnvironmentVariable('ES_AUTOMATION_NODE_PATH') }
if ([string]::IsNullOrWhiteSpace($nodeValue) -and $config) { $nodeValue = [string]$config.nodePath }
$nodePath = Resolve-ExplicitNode $nodeValue
Add-Check $checks 'node-path' ($null -ne $nodePath) 'An explicit absolute node.exe path is required; PATH is not used.' $(if ($nodePath) { $nodePath } else { '' })

$dshValue = if ($config) { [string]$config.dshExecutable } else { '' }
if ([string]::IsNullOrWhiteSpace($dshValue)) { $dshValue = [Environment]::GetEnvironmentVariable('DSH_EXECUTABLE') }
if ([string]::IsNullOrWhiteSpace($dshValue)) {
    $candidate = Join-Path $workerRoot 'node_modules/.bin/dsh.cmd'
    if (Test-Path -LiteralPath $candidate -PathType Leaf) { $dshValue = $candidate }
}
$dshPath = Resolve-AbsoluteFile $dshValue 'dshExecutable' $root
Add-Check $checks 'dsh-executable' ($null -ne $dshPath) 'DSH CLI entry exists inside the project root.' $(if ($dshPath) { $dshPath } else { '' })

$profile = if ($config -and $config.profile) { [string]$config.profile } else { '' }
Add-Check $checks 'profile' ($configValid -and ($profile -eq 'headless' -or $profile -eq 'sdk')) 'Profile must be managed headless or sdk.' $profile
$dshHome = if ($config) { [string]$config.dshHome } else { '' }
$workspace = if ($config) { [string]$config.workspace } else { '' }
Add-Check $checks 'dsh-home' ($configValid -and (Test-ProjectPath $dshHome $root)) 'DSH_HOME must be an isolated directory under the project.' $dshHome
Add-Check $checks 'workspace' ($configValid -and (Test-ProjectPath $workspace $root)) 'workspace must be an isolated directory under the project.' $workspace

if ($nodePath) {
    $probe = Invoke-Probe $nodePath @('--version') $root
    $versionMatch = [regex]::Match($probe.output, 'v(\d+)(?:\.\d+){1,2}')
    $versionOk = $probe.ok -and $versionMatch.Success -and [int]$versionMatch.Groups[1].Value -ge 22
    Add-Check $checks 'node-probe' $versionOk 'Node version probe must succeed with major version 22 or newer.' $probe.output
}
if ($configValid -and $nodePath -and $dshPath -and $profile -eq 'headless') {
    $dshEntrypoint = Join-Path $workerRoot 'node_modules/@deepseek-ai/dsh/lib/bin.js'
    if ([System.IO.Path]::GetExtension($dshPath) -ieq '.cmd' -and (Test-Path -LiteralPath $dshEntrypoint -PathType Leaf)) {
        $probe = Invoke-Probe $nodePath @($dshEntrypoint, '--profile', 'headless', '--help') $root 30000
    } else {
        $probe = Invoke-Probe $dshPath @('--profile', 'headless', '--help') $root
    }
    Add-Check $checks 'dsh-probe' $probe.ok 'DSH headless CLI probe must succeed.' $probe.output
}

$providerConfigured = -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable('DEEPSEEK_API_KEY'))
if ($RequireProvider) { Add-Check $checks 'provider-credential' $providerConfigured 'A live call requires DEEPSEEK_API_KEY; only presence is reported.' $(if ($providerConfigured) { 'present' } else { 'missing' }) }

$failed = @($checks | Where-Object { $_.status -eq 'failed' })
$status = if ($failed.Count -eq 0) { 'Connected' } else { 'NotConnected' }
$reasonCode = if ($failed.Count -eq 0) {
    ''
} else {
    $candidateReasonCode = [string]$failed[0].id.ToUpperInvariant().Replace('-', '_')
    if ($candidateReasonCode -eq 'PROVIDER_CREDENTIAL') { 'PROVIDER_CREDENTIAL_MISSING' } else { $candidateReasonCode }
}
$nextAction = if ($status -eq 'Connected') {
    if ($providerConfigured) { 'Managed invocation from ESAutomationCenter is available.' } else { 'Local DSH runtime is ready; set DEEPSEEK_API_KEY before live headless calls.' }
} elseif ($reasonCode -eq 'PROVIDER_CREDENTIAL_MISSING') {
    'Set DEEPSEEK_API_KEY in the launching user session, then rerun this checker; never write the key to the repository.'
} elseif ($reasonCode -eq 'DSH_PROBE') {
    'Run Install-ESDeepSeekHarness.ps1 again and inspect the bounded DSH probe output.'
} else {
    'Run Install-ESDeepSeekHarness.ps1 or repair the reasonCode; remain NotConnected until fixed.'
}
[ordered]@{
    schemaVersion = 1
    frameworkId = 'deepseek-harness'
    declaration = 'es-deepseek'
    role = 'external-execution-plane'
    authority = 'ESFramework/ESAI'
    authorityLevel = 'high-contributor-not-final-acceptance'
    status = $status
    reasonCode = $reasonCode
    checks = @($checks)
    providerConfigured = $providerConfigured
    nextAction = $nextAction
    runtimeStatus = 'runtime-not-run'
    nonClaims = @('No Unity/PlayMode/release acceptance claim', 'DSH output is never promoted to ES Accepted', 'API keys are never output')
} | ConvertTo-Json -Depth 8
