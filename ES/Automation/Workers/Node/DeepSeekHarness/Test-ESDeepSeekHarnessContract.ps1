[CmdletBinding()]
param([string]$ProjectRoot)

$ErrorActionPreference = 'Stop'
$root = if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..\..'))
} else { [System.IO.Path]::GetFullPath($ProjectRoot) }
$worker = Join-Path $root 'ES/Automation/Workers/Node/DeepSeekHarness'
$cases = [System.Collections.Generic.List[object]]::new()
function Add-Case([string]$Id, [bool]$Passed, [string]$Finding = '') {
    [void]$cases.Add([pscustomobject]@{ case = $Id; status = if ($Passed) { 'passed' } else { 'failed' }; finding = $Finding })
}

foreach ($name in @('package.json','package-lock.json','worker-manifest.json','worker.js','Install-ESDeepSeekHarness.ps1','Test-ESDeepSeekHarness.ps1','Test-ESDeepSeekHarnessUi.ps1','README.md')) {
    Add-Case "file-$name" (Test-Path -LiteralPath (Join-Path $worker $name) -PathType Leaf) "Missing $name"
}
foreach ($name in @('package.json','worker-manifest.json')) {
    try { Get-Content -LiteralPath (Join-Path $worker $name) -Encoding UTF8 -Raw | ConvertFrom-Json | Out-Null; Add-Case "json-$name" $true }
    catch { Add-Case "json-$name" $false $_.Exception.Message }
}
$node = Get-Command node.exe -ErrorAction SilentlyContinue
if ($node -and (Test-Path -LiteralPath (Join-Path $worker 'package-lock.json') -PathType Leaf)) {
    & $node.Source -e "const fs=require('fs');const p=JSON.parse(fs.readFileSync(process.argv[1],'utf8'));const q=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));if(p.version!==q.version||q.packages[''].version!==p.version||q.packages['node_modules/@deepseek-ai/dsh'].version!=='0.1.1-rc.2')process.exit(2);" (Join-Path $worker 'package.json') (Join-Path $worker 'package-lock.json') 2>$null
    Add-Case 'package-lock-consistency' ($LASTEXITCODE -eq 0) 'package-lock.json does not match package.json or the pinned DSH version.'
} else { Add-Case 'package-lock-consistency' $false 'node.exe or package-lock.json is missing.' }
try {
    $manifest = Get-Content -LiteralPath (Join-Path $worker 'worker-manifest.json') -Encoding UTF8 -Raw | ConvertFrom-Json
    Add-Case 'manifest-declaration' ($manifest.declaration -eq 'es-deepseek')
    Add-Case 'manifest-provider-declaration' ($manifest.providerDeclaration -eq 'es-deepseek' -and $manifest.taskId -eq 'es.deepseek.harness')
    Add-Case 'manifest-operations' ((@('dry-run','check-local','headless-prompt') | Where-Object { $_ -notin @($manifest.operations) }).Count -eq 0)
    Add-Case 'manifest-authority' ($manifest.authority -eq 'ESFramework/ESAI' -and $manifest.authorityLevel -eq 'high-contributor-not-final-acceptance')
}
catch { Add-Case 'manifest-semantics' $false $_.Exception.Message }
if ($node) {
    & $node.Source -e "const fs=require('fs');const p=JSON.parse(fs.readFileSync(process.argv[1],'utf8'));const q=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));if(!p.engines||p.engines.node!=='>=22'||!q.packages[''].engines||q.packages[''].engines.node!=='>=22')process.exit(2);" (Join-Path $worker 'package.json') (Join-Path $worker 'package-lock.json') 2>$null
    Add-Case 'node-engine-bound' ($LASTEXITCODE -eq 0) 'package and lock must require Node.js 22 or newer.'
} else { Add-Case 'node-engine-bound' $false 'node.exe is missing.' }
if ($node) {
    & $node.Source --check (Join-Path $worker 'worker.js') 2>$null
    Add-Case 'node-syntax' ($LASTEXITCODE -eq 0) 'worker.js Node syntax check failed.'
} else { Add-Case 'node-syntax' $false 'node.exe is not available for syntax checking.' }
$checkJson = & powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $worker 'Test-ESDeepSeekHarness.ps1') -ProjectRoot $root | Out-String
try {
    $check = $checkJson.Trim() | ConvertFrom-Json
    Add-Case 'local-check-shape' ($check.frameworkId -eq 'deepseek-harness' -and $check.declaration -eq 'es-deepseek' -and $check.status -in @('Connected','NotConnected'))
    Add-Case 'local-check-no-secret' ($checkJson -notmatch 'sk-[A-Za-z0-9]+' -and $checkJson -notmatch 'DEEPSEEK_API_KEY\s*[:=]\s*[^<\r\n]+')
}
catch { Add-Case 'local-check-shape' $false 'The checker did not return valid JSON.' }
$checkerText = Get-Content -LiteralPath (Join-Path $worker 'Test-ESDeepSeekHarness.ps1') -Encoding UTF8 -Raw
$failClosedShape = @(
    '$configValid = $false',
    "Add-Check `$checks 'runtime-config'",
    "Add-Check `$checks 'runtime-identity'",
    'function Test-ProjectPath',
    'configValid -and'
) | Where-Object { $checkerText.Contains($_) }
Add-Case 'local-check-fail-closed' ($failClosedShape.Count -eq 5) 'Local checker must require valid runtime identity and project-contained DSH_HOME/workspace before Connected.'
$uiScript = Join-Path $worker 'Test-ESDeepSeekHarnessUi.ps1'
if (Test-Path -LiteralPath $uiScript -PathType Leaf) {
    $uiJson = & powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $uiScript -ProjectRoot $root | Out-String
    try {
        $ui = $uiJson.Trim() | ConvertFrom-Json
        Add-Case 'ui-static-regression' ($ui.status -eq 'passed' -and $ui.staticStatus -eq 'static-passed' -and $ui.runtimeStatus -eq 'runtime-not-run') 'DSH icon, state labels, role, recovery and registration must remain statically present.'
    }
    catch { Add-Case 'ui-static-regression' $false 'The DSH UI regression checker did not return valid JSON.' }
} else {
    Add-Case 'ui-static-regression' $false 'The DSH UI regression checker is missing.'
}
$failed = @($cases | Where-Object { $_.status -eq 'failed' })
[pscustomobject]@{
    schemaVersion = 1
    validator = 'Test-ESDeepSeekHarnessContract'
    status = if ($failed.Count -eq 0) { 'passed' } else { 'failed' }
    caseCount = $cases.Count
    passedCount = @($cases | Where-Object { $_.status -eq 'passed' }).Count
    failedCount = $failed.Count
    cases = @($cases)
    runtimeStatus = 'runtime-not-run'
    claimsNotProven = @('Provider network call', 'Unity compile/ReloadDomain', 'real headless task completion')
} | ConvertTo-Json -Depth 8
if ($failed.Count -gt 0) { exit 1 }
