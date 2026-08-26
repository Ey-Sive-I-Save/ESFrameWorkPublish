$skillRoot = Split-Path -Parent $PSScriptRoot
$probe = Join-Path $skillRoot 'scripts/Invoke-ESGovernanceRouteProbe.ps1'
$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $skillRoot))
function Write-ProbeFixture($Value, [string]$Name) {
    $path = Join-Path $TestDrive $Name
    [IO.File]::WriteAllText($path, ($Value | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false)); return $path
}
function New-ProbeInput {
    [pscustomobject][ordered]@{
        decisionId = 'probe-fixture'; object = 'task-1'; field = 'evidence'; profile = 'StaticDevelopment'; scope = 'task:read-only'; scopeKind = 'task-object'
        routeState = 'core'; evidenceState = 'runtime-not-run'; effect = 'claim-cap'; reasonCode = 'RUNTIME.NOT_RUN'; routeDepth = 0; depthReasonCode = $null
    }
}
Describe 'Read-only governance route probe' {
    It 'reads exact registries and leaves production integration disabled' {
        $path = Write-ProbeFixture (New-ProbeInput) 'runtime.json'; $before = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        $result = & $probe -ProjectRoot $projectRoot -DecisionPath $path
        $after = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        $result.probeStatus | Should Be 'ReadOnly'; $result.decisionStatus | Should Be 'Accepted'; $result.productionRouteIntegrated | Should Be $false; $result.globalP0Integrated | Should Be $false; @($result.registriesRead).Count | Should Be 6; $result.decision.stateChanged | Should Be $false; $before | Should Be $after
    }
    It 'reports a registered H/I scoped transition without global takeover' {
        $input = New-ProbeInput; $input.reasonCode = 'EVIDENCE.RECEIPT_MISSING'; $input.profile = 'HIAcceptance'; $input.scope = 'context:HIAcceptance'; $input.scopeKind = 'context-object'; $input.routeState = 'blocked'; $input.evidenceState = 'pending'; $input.effect = 'hard-block'; $input | Add-Member -MemberType NoteProperty -Name effectOverride -Value ([pscustomobject]@{ fromEffect = 'claim-cap'; toEffect = 'hard-block'; profile = 'HIAcceptance' })
        $result = & $probe -ProjectRoot $projectRoot -DecisionPath (Write-ProbeFixture $input 'hi-acceptance.json')
        $result.decisionStatus | Should Be 'Accepted'; $result.decision.stateChanged | Should Be $true; $result.decision.changedBy | Should Be 'EVIDENCE.RECEIPT_MISSING.HI_ACCEPTANCE'; $result.productionRouteIntegrated | Should Be $false
    }
    It 'rejects forged deviation, optional stale global projection, and bad depth two' {
        $deviation = New-ProbeInput; $deviation.routeState = 'blocked'; $deviation.evidenceState = 'runtime-not-run'; $deviation.effect = 'hard-block'
        (& $probe -ProjectRoot $projectRoot -DecisionPath (Write-ProbeFixture $deviation 'unregistered-deviation.json')).decisionStatus | Should Be 'Rejected'
        $stale = New-ProbeInput; $stale.reasonCode = 'SOURCE.STALE_OPTIONAL'; $stale.evidenceState = 'stale'; $stale.effect = 'review'; $stale.profile = 'ProjectAcceptance'; $stale.scope = 'project:global'; $stale.scopeKind = 'project-global'
        (& $probe -ProjectRoot $projectRoot -DecisionPath (Write-ProbeFixture $stale 'stale-global.json')).decisionStatus | Should Be 'Rejected'
        $depth = New-ProbeInput; $depth.reasonCode = 'ROUTE.DEPTH_2_AUTHORIZED'; $depth.profile = 'StaticDevelopment'; $depth.routeState = 'extension'; $depth.evidenceState = 'pending'; $depth.effect = 'review'; $depth.routeDepth = 2; $depth.depthReasonCode = 'ROUTE.DEPTH_2_AUTHORIZED'
        (& $probe -ProjectRoot $projectRoot -DecisionPath (Write-ProbeFixture $depth 'depth-denied.json')).decisionStatus | Should Be 'Rejected'
        $override = New-ProbeInput; $override.reasonCode = 'EVIDENCE.RECEIPT_MISSING'; $override.profile = 'HIAcceptance'; $override.scope = 'context:HIAcceptance'; $override.scopeKind = 'context-object'; $override.routeState = 'blocked'; $override.evidenceState = 'pending'; $override.effect = 'hard-block'; $override | Add-Member -MemberType NoteProperty -Name effectOverride -Value ([pscustomobject]@{ fromEffect = 'hard-block'; toEffect = 'review'; profile = 'HIAcceptance' })
        (& $probe -ProjectRoot $projectRoot -DecisionPath (Write-ProbeFixture $override 'unregistered-override.json')).decisionStatus | Should Be 'Rejected'
    }
}
