[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$validator = Join-Path $projectRoot '.agents\skills\es-skill-governance\scripts\Test-ESRuntimeAuthorization.ps1'
$schema = Join-Path $projectRoot 'ES\Automation\Contracts\es-runtime-authorization.schema.json'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('es-runtime-auth-fixture-' + [Guid]::NewGuid().ToString('N'))
$utf8 = New-Object Text.UTF8Encoding($false)
try {
    $contractRelative = 'ES\Automation\Contracts\fixture-task.json'
    $contractPath = Join-Path $temp $contractRelative
    $authorizationRelative = 'ES\Automation\Candidates\fixture-authorization.json'
    $authorizationPath = Join-Path $temp $authorizationRelative
    New-Item -ItemType Directory -Force -Path (Split-Path $contractPath), (Split-Path $authorizationPath), (Join-Path $temp 'ES\Automation\Contracts') | Out-Null
    Copy-Item -LiteralPath $schema -Destination (Join-Path $temp 'ES\Automation\Contracts\es-runtime-authorization.schema.json')
    [IO.File]::WriteAllText($contractPath, '{"schemaVersion":1,"taskId":"fixture","version":1}', $utf8)
    $contractHash = (Get-FileHash -LiteralPath $contractPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $hash = ('a' * 64)
    $now = [DateTimeOffset]::UtcNow
    $base = [ordered]@{
        schemaVersion = 1; taskId = 'fixture'; planHash = $hash; commandId = 'fixture.review'; commandHash = $hash
        taskContractRef = $contractRelative.Replace('\','/'); taskContractHash = $contractHash; targetPaths = @('Assets/Fixture.asset')
        issuedAtUtc = $now.AddMinutes(-1).ToString('O'); expiresAtUtc = $now.AddMinutes(5).ToString('O')
        timeBudgetSeconds = 60; timeoutSeconds = 30; stopCondition = 'stop-on-first-boundary-failure'; oneTime = $true
        developerApproval = 'fixture-approved'
    }
    function Write-Authorization([System.Collections.IDictionary]$value) {
        [IO.File]::WriteAllText($authorizationPath, ($value | ConvertTo-Json -Depth 8), $utf8)
    }
    function Copy-Authorization([System.Collections.IDictionary]$value) {
        $copy=[ordered]@{}
        foreach($entry in $value.GetEnumerator()){$copy[$entry.Key]=$entry.Value}
        return $copy
    }
    function Invoke-Contract([switch]$Consume) {
        try {
            $captured = (& $validator -ProjectRoot $temp -AuthorizationPath $authorizationRelative -Consume:$Consume 2>&1 | Out-String)
            return @{ passed = ($captured -match 'PASS: runtime authorization contract'); output = $captured }
        } catch {
            return @{ passed = $false; output = $_.Exception.Message }
        }
    }
    Write-Authorization $base
    $valid = Invoke-Contract
    if(-not $valid.passed){ throw ('valid runtime authorization fixture was rejected: ' + [string]$valid.output) }

    $expired = Copy-Authorization $base; $expired.expiresAtUtc = $now.AddMinutes(-1).ToString('O'); Write-Authorization $expired
    if((Invoke-Contract).passed){ throw 'expired runtime authorization fixture was accepted' }

    $drifted = Copy-Authorization $base; $drifted.taskContractHash = ('b' * 64); Write-Authorization $drifted
    if((Invoke-Contract).passed){ throw 'contract hash drift fixture was accepted' }

    Write-Authorization $base
    if((Invoke-Contract -Consume).passed){ throw 'read-only validator unexpectedly consumed authorization' }
    Write-Output 'PASS: runtime authorization valid, expiry, source-drift and consume-boundary regression checks'
} finally {
    if(Test-Path -LiteralPath $temp){ Remove-Item -LiteralPath $temp -Recurse -Force }
}
