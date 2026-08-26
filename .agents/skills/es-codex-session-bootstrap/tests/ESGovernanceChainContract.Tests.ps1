$skillRoot = Split-Path -Parent $PSScriptRoot
$validator = Join-Path $skillRoot 'scripts/Test-ESGovernanceChainContract.ps1'
$contract = Join-Path $skillRoot 'references/governance-chain-plan.contract.json'
$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $skillRoot))

Describe 'ES P0 governance contract completeness' {
    It 'accepts the canonical contract only after all static validation overrides are present' {
        $result = & $validator -ProjectRoot $projectRoot -ContractPath $contract
        $result.decisionStatus | Should Be 'Accepted'
        $result.p0Status | Should Be 'passed'
        $result.blockCount | Should Be 12
        $result.atomicStepCount | Should Be 76
        $result.staticValidationOverrides | Should Be 12
        $result.runtimeStatus | Should Be 'runtime-not-run'
    }

    It 'hard-blocks a contract with static validation removed' {
        $fixture = Join-Path $TestDrive 'missing-static.json'
        $json = Get-Content -Raw -Encoding UTF8 $contract | ConvertFrom-Json
        $json.PSObject.Properties.Remove('staticValidation')
        [IO.File]::WriteAllText($fixture, ($json | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
        { & $validator -ProjectRoot $projectRoot -ContractPath $fixture } | Should Throw
    }

    It 'hard-blocks a block with a missing required validation field' {
        $fixture = Join-Path $TestDrive 'missing-keyword.json'
        $json = Get-Content -Raw -Encoding UTF8 $contract | ConvertFrom-Json
        $json.staticValidation.blockOverrides.A.PSObject.Properties.Remove('validationKeywords')
        [IO.File]::WriteAllText($fixture, ($json | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
        { & $validator -ProjectRoot $projectRoot -ContractPath $fixture } | Should Throw
    }

    It 'hard-blocks silent removal of one canonical static keyword' {
        $fixture = Join-Path $TestDrive 'removed-keyword.json'
        $json = Get-Content -Raw -Encoding UTF8 $contract | ConvertFrom-Json
        $json.staticValidation.blockOverrides.A.validationKeywords = @('入口清单','范围边界','UTF-8')
        [IO.File]::WriteAllText($fixture, ($json | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
        { & $validator -ProjectRoot $projectRoot -ContractPath $fixture } | Should Throw
    }

    It 'hard-blocks a missing authority reference and non-candidate A-D write' {
        $missingRef = Join-Path $TestDrive 'missing-ref.json'
        $json = Get-Content -Raw -Encoding UTF8 $contract | ConvertFrom-Json
        $json.staticValidation.keywordAuthorityRefs = @('does-not-exist.md')
        [IO.File]::WriteAllText($missingRef, ($json | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
        { & $validator -ProjectRoot $projectRoot -ContractPath $missingRef } | Should Throw

        $badWrite = Join-Path $TestDrive 'bad-write.json'
        $json = Get-Content -Raw -Encoding UTF8 $contract | ConvertFrom-Json
        $json.blocks[0].allowedWrites = @('source-edit')
        [IO.File]::WriteAllText($badWrite, ($json | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
        { & $validator -ProjectRoot $projectRoot -ContractPath $badWrite } | Should Throw
    }

    It 'hard-blocks a score policy that can override a hard block' {
        $fixture = Join-Path $TestDrive 'score-override.json'
        $json = Get-Content -Raw -Encoding UTF8 $contract | ConvertFrom-Json
        $json.staticValidation.blockOverrides.K.validationKeywords = @('score-cap','evidence-quality','calibration')
        [IO.File]::WriteAllText($fixture, ($json | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
        { & $validator -ProjectRoot $projectRoot -ContractPath $fixture } | Should Throw
    }
}
