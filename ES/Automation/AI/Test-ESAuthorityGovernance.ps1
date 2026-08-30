[CmdletBinding()]
param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path

function Invoke-JsonValidator([string]$Path) {
    $output = ''
    $exitCode = 0
    try {
        $output = (& powershell -NoProfile -File $Path -ProjectRoot $root 2>&1 | Out-String)
        $exitCode = $LASTEXITCODE
        try { $json = $output | ConvertFrom-Json }
        catch {
            if ($exitCode -eq 0 -and $output.Trim() -eq 'passed') {
                return [pscustomobject][ordered]@{
                    path = $Path.Substring($root.Length).TrimStart('\','/').Replace('\','/')
                    status = 'passed'; validatorStatus = 'passed'; exitCode = 0; issues = @()
                }
            }
            throw
        }
        return [pscustomobject][ordered]@{
            path = $Path.Substring($root.Length).TrimStart('\','/').Replace('\','/')
            status = if ($exitCode -eq 0 -and [string]$json.status -eq 'passed') { 'passed' } else { 'failed' }
            validatorStatus = [string]$json.status
            exitCode = $exitCode
            issues = @($json.issues)
        }
    }
    catch {
        return [pscustomobject][ordered]@{
            path = $Path.Substring($root.Length).TrimStart('\','/').Replace('\','/')
            status = 'failed'
            validatorStatus = 'unparseable'
            exitCode = if ($exitCode -eq 0) { 1 } else { $exitCode }
            issues = @($_.Exception.Message)
        }
    }
}

$validatorPaths = @(
    (Join-Path $root 'ES/Automation/AI/Test-ESAuthorityFacadePolicy.ps1'),
    (Join-Path $root 'ES/Automation/AI/Test-ESAuthorityConsumerCoverage.ps1'),
    (Join-Path $root 'ES/Automation/AI/Test-ESAuthorityDeserialization.ps1'),
    (Join-Path $root 'ES/Automation/AI/Test-ESAuthorityDecisionDataflow.ps1'),
    (Join-Path $root 'ES/Automation/AI/Test-ESAuthorityDecisionPolicy.ps1'),
    (Join-Path $root 'ES/Automation/AI/Test-ESAuthorityDomainCoverage.ps1'),
    (Join-Path $root '.agents/scripts/Test-ESSuperSemantics.ps1')
)
foreach ($path in $validatorPaths) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "AUTHORITY_VALIDATOR_MISSING:$path" }
}

$checks = @($validatorPaths | ForEach-Object { Invoke-JsonValidator $_ })
$failed = @($checks | Where-Object status -ne 'passed')
[ordered]@{
    schemaVersion = 1
    validator = 'Test-ESAuthorityGovernance'
    status = if ($failed.Count) { 'failed' } else { 'passed' }
    checkCount = $checks.Count
    passedCount = @($checks | Where-Object status -eq 'passed').Count
    failedCount = $failed.Count
    checks = $checks
    runtimeStatus = 'runtime-not-run'
    claimsNotProven = @('Unity/PlayMode behavior','host invocation frequency','release behavior')
    capturedUtc = [DateTime]::UtcNow.ToString('o')
} | ConvertTo-Json -Depth 10
if ($failed.Count) { exit 1 }
