[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$NodePath
)

$ErrorActionPreference = 'Stop'
$root = if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..\..'))
} else { [System.IO.Path]::GetFullPath($ProjectRoot) }
$workerRoot = Join-Path $root 'ES/Automation/Workers/Node/DeepSeekHarness'
$tempRoot = Join-Path $root 'ES/Automation/Temp/DeepSeekHarness'
$runtimeWorkspace = Join-Path $tempRoot 'workspace'
$dshHome = Join-Path $tempRoot 'dsh-home'

if ([string]::IsNullOrWhiteSpace($NodePath)) { $NodePath = [Environment]::GetEnvironmentVariable('ES_DEEPSEEK_NODE_PATH') }
if ([string]::IsNullOrWhiteSpace($NodePath)) { $NodePath = [Environment]::GetEnvironmentVariable('ES_AUTOMATION_NODE_PATH') }
if ([string]::IsNullOrWhiteSpace($NodePath)) {
    $nodeCommand = Get-Command node.exe -ErrorAction SilentlyContinue
    if ($nodeCommand) { $NodePath = $nodeCommand.Source }
}
if ([string]::IsNullOrWhiteSpace($NodePath) -or -not [System.IO.Path]::IsPathRooted($NodePath)) {
    throw 'node.exe was not found. Pass -NodePath <absolute path>; the installer does not write PATH.'
}
$NodePath = [System.IO.Path]::GetFullPath($NodePath)
if (-not (Test-Path -LiteralPath $NodePath -PathType Leaf)) { throw "node.exe does not exist: $NodePath" }
$nodeVersionText = (& $NodePath --version 2>$null | Out-String).Trim()
$nodeVersionMatch = [regex]::Match($nodeVersionText, 'v(\d+)(?:\.\d+){1,2}')
if (-not $nodeVersionMatch.Success -or [int]$nodeVersionMatch.Groups[1].Value -lt 22) {
    throw "Node.js 22 or newer is required; found '$nodeVersionText'."
}

$npmPath = Join-Path (Split-Path -Parent $NodePath) 'npm.cmd'
if (-not (Test-Path -LiteralPath $npmPath -PathType Leaf)) {
    $npmCommand = Get-Command npm.cmd -ErrorAction SilentlyContinue
    if ($npmCommand) { $npmPath = $npmCommand.Source }
}
if (-not (Test-Path -LiteralPath $npmPath -PathType Leaf)) { throw 'npm.cmd paired with node.exe was not found.' }
if (-not (Test-Path -LiteralPath (Join-Path $workerRoot 'package.json') -PathType Leaf)) { throw 'DSH Worker package.json is missing.' }

& $npmPath install --ignore-scripts --no-audit --no-fund --prefix $workerRoot
if ($LASTEXITCODE -ne 0) { throw "npm install failed with exit code $LASTEXITCODE." }

$dshPath = Join-Path $workerRoot 'node_modules/.bin/dsh.cmd'
if (-not (Test-Path -LiteralPath $dshPath -PathType Leaf)) { throw 'npm install completed but node_modules/.bin/dsh.cmd was not found.' }
[System.IO.Directory]::CreateDirectory($runtimeWorkspace) | Out-Null
[System.IO.Directory]::CreateDirectory($dshHome) | Out-Null
$configPath = Join-Path $tempRoot 'runtime.local.json'
$config = [ordered]@{
    schemaVersion = 1
    declaration = 'es-deepseek'
    providerDeclaration = 'es-deepseek'
    nodePath = $NodePath
    dshExecutable = [System.IO.Path]::GetFullPath($dshPath)
    profile = 'headless'
    dshHome = [System.IO.Path]::GetFullPath($dshHome)
    workspace = [System.IO.Path]::GetFullPath($runtimeWorkspace)
    configuredAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
}
$json = $config | ConvertTo-Json -Depth 5
$temporary = "$configPath.$([Guid]::NewGuid().ToString('N')).tmp"
[System.IO.File]::WriteAllText($temporary, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
if ([System.IO.File]::Exists($configPath)) {
    [System.IO.File]::Replace($temporary, $configPath, $null)
} else {
    [System.IO.File]::Move($temporary, $configPath)
}

Write-Output ($config | ConvertTo-Json -Depth 5)
Write-Output 'Next: set DEEPSEEK_API_KEY, then validate the DSH link in Unity ES/Automation and Development/Automation Center.'
