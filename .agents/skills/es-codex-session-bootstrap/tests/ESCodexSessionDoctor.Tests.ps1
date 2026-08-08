$skillRoot = Split-Path -Parent $PSScriptRoot
$scriptsRoot = Join-Path $skillRoot 'scripts'
$doctorScript = Join-Path $scriptsRoot 'Get-ESCodexSessionDoctor.ps1'

Describe 'ES Codex commercial readiness doctor' {
    It 'reports versioned code, environment, state, delivery, and commercial layers without mutation' {
        $stateRoot = Join-Path $TestDrive 'healthy-doctor-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registryPath = Join-Path $stateRoot 'sessions.json'
        $registry = [ordered]@{ schemaVersion = 2; revision = 0; updatedUtc = ''; sessions = @() }
        [IO.File]::WriteAllText($registryPath, ($registry | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
        $before = [Convert]::ToBase64String([IO.File]::ReadAllBytes($registryPath))

        $result = & $doctorScript -SkipUiObservation -SkipReadinessRefresh -StateRoot $stateRoot

        $result.doctorContractVersion | Should Be 1
        $result.productVersion | Should Match '^2\.'
        $result.codeReady | Should Be $true
        $result.environmentReady | Should Be $true
        $result.stateReady | Should Be $true
        $result.cooperativeDeliveryReady | Should Be $true
        $result.commercialBaselineReady | Should Be $true
        $result.fleetOperationalReady | Should Be $false
        $result.managedDirectDeliveryReady | Should Be $false
        $result.code.parserFailureCount | Should Be 0
        $result.registry.applicableRepairCount | Should Be 0
        [Convert]::ToBase64String([IO.File]::ReadAllBytes($registryPath)) | Should Be $before
    }

    It 'returns a stable issue code instead of throwing for an unreadable registry' {
        $stateRoot = Join-Path $TestDrive 'broken-doctor-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        [IO.File]::WriteAllText((Join-Path $stateRoot 'sessions.json'), '{broken', [Text.UTF8Encoding]::new($false))

        $result = & $doctorScript -SkipUiObservation -SkipReadinessRefresh -StateRoot $stateRoot

        $result.stateReady | Should Be $false
        $result.commercialBaselineReady | Should Be $false
        @($result.issues | Where-Object code -eq 'ESCS-STATE-001').Count | Should Be 1
    }

    It 'separates unapplied safe repair work from host delivery limitations' {
        $stateRoot = Join-Path $TestDrive 'repair-doctor-state'
        [void][IO.Directory]::CreateDirectory($stateRoot)
        $registry = [ordered]@{
            schemaVersion = 2
            revision = 1
            updatedUtc = [DateTime]::UtcNow.ToString('o')
            sessions = @([ordered]@{ recordId = '45ca31c3315a5978f40438aab46040d7'; responsibilityKey = 'default' })
        }
        [IO.File]::WriteAllText((Join-Path $stateRoot 'sessions.json'), ($registry | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))

        $result = & $doctorScript -SkipUiObservation -SkipReadinessRefresh -StateRoot $stateRoot

        $result.stateReady | Should Be $false
        $result.registry.applicableRepairCount | Should Be 1
        $repairIssue = @($result.issues | Where-Object code -eq 'ESCS-STATE-004')[0]
        $repairIssue.blocksCommercialBaseline | Should Be $true
        $repairIssue.requiresAuthorization | Should Be $true
        @($result.issues | Where-Object code -eq 'ESCS-HOST-001')[0].blocksCommercialBaseline | Should Be $false
        @($result.issues | Where-Object code -eq 'ESCS-HOST-002')[0].blocksCommercialBaseline | Should Be $false
    }
}
