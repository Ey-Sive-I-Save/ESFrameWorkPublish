[CmdletBinding()]
param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('es-skill-evidence-contract-' + [Guid]::NewGuid().ToString('N'))
$utf8 = [Text.UTF8Encoding]::new($false)

function Write-Text([string]$Path, [string]$Text) {
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
    [IO.File]::WriteAllText($Path, $Text, $utf8)
}

function Copy-ProjectFile([string]$RelativePath) {
    $source = Join-Path $root $RelativePath
    $target = Join-Path $fixtureRoot $RelativePath
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $target))
    [IO.File]::Copy($source, $target, $true)
}

function Hash([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Invoke-Validator([string]$ReceiptPath, [bool]$ExpectedSuccess, [string]$CaseId) {
    $validator = Join-Path $fixtureRoot '.agents/skills/es-skill-governance/scripts/Test-ESStrictEvidenceReceipt.ps1'
    $skill = Join-Path $fixtureRoot '.agents/skills/es-fixture'
    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $output = & powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $validator `
        -SkillPath $skill -EvidencePath $ReceiptPath -ProjectRoot $fixtureRoot 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorAction
    $passed = $exitCode -eq 0
    if ($passed -ne $ExpectedSuccess) {
        throw "$CaseId expected success=$ExpectedSuccess, actual=$passed. $($output -join [Environment]::NewLine)"
    }
    [pscustomobject]@{ id = $CaseId; status = 'passed' }
}

try {
    foreach ($relative in @(
        'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json',
        'ES/Automation/Contracts/es-skill-evidence-binding-v1.schema.json',
        'ES/Automation/Contracts/ESJsonSchemaLite.psm1',
        '.agents/skills/es-skill-governance/scripts/Test-ESEvidenceContractBindings.ps1',
        '.agents/skills/es-skill-governance/scripts/Test-ESStrictEvidenceReceipt.ps1'
    )) { Copy-ProjectFile $relative }

    $skillRoot = Join-Path $fixtureRoot '.agents/skills/es-fixture'
    Write-Text (Join-Path $skillRoot 'SKILL.md') "---`nname: es-fixture`ndescription: Evidence fixture.`n---`n"
    $entrypoint = @'
[CmdletBinding()]
param([string]$SkillPath,[string]$EvidencePath,[string]$ProjectRoot)
$strict=Join-Path $ProjectRoot '.agents/skills/es-skill-governance/scripts/Test-ESStrictEvidenceReceipt.ps1'
& powershell -NoProfile -File $strict -SkillPath $SkillPath -EvidencePath $EvidencePath -ProjectRoot $ProjectRoot
exit $LASTEXITCODE
'@
    $entrypointPath = Join-Path $skillRoot 'scripts/Test-ESSkillEvidence.ps1'
    Write-Text $entrypointPath $entrypoint
    $contractPath = Join-Path $fixtureRoot 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json'
    $contractHash = Hash $contractPath
    $binding = [ordered]@{
        schemaVersion = 1
        bindingId = 'es.skill-evidence-binding.es-fixture.v1'
        skillName = 'es-fixture'
        contract = [ordered]@{
            id = 'es.skill-evidence-receipt'
            version = '1'
            path = 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json'
            hash = $contractHash
        }
        localContract = [ordered]@{ path = ''; hash = ''; mode = 'central-authoritative' }
        stableEntrypoint = [ordered]@{
            path = 'scripts/Test-ESSkillEvidence.ps1'
            hash = Hash $entrypointPath
            mode = 'central-delegate'
            centralValidatorPath = '.agents/skills/es-skill-governance/scripts/Test-ESStrictEvidenceReceipt.ps1'
        }
        compatibility = [ordered]@{
            legacyReadable = $true
            legacyReceiptBeforeUtc = '2026-08-26T03:45:00Z'
            legacyReceiptAcceptanceEndsUtc = '2026-09-02T03:45:00Z'
            newReceiptBinding = 'required'
            retirementState = 'not-authorized'
        }
    }
    Write-Text (Join-Path $skillRoot 'evidence-contract.binding.json') (($binding | ConvertTo-Json -Depth 8) + "`n")

    $sourcePath = Join-Path $fixtureRoot 'evidence/source.txt'
    Write-Text $sourcePath "fixture-source`n"
    $receiptPath = Join-Path $fixtureRoot 'evidence/receipt.json'
    $receipt = [ordered]@{
        evidenceContractId = 'es.skill-evidence-receipt'
        evidenceContractHash = $contractHash
        skillName = 'es-fixture'
        case = 'canonical'
        status = 'passed'
        evidenceLevel = 'S1'
        receiptPath = 'evidence/receipt.json'
        sourceRefs = @('evidence/source.txt')
        sourceRefHashes = [ordered]@{ 'evidence/source.txt' = Hash $sourcePath }
        toolId = 'fixture-validator'
        unityVersion = 'not-run'
        capturedUtc = [DateTime]::UtcNow.ToString('o')
        authorizationKind = 'read-only'
    }

    $cases = [Collections.Generic.List[object]]::new()
    Write-Text $receiptPath (($receipt | ConvertTo-Json -Depth 8) + "`n")
    [void]$cases.Add((Invoke-Validator $receiptPath $true 'canonical-binding-accepted'))

    $forged = ($receipt | ConvertTo-Json -Depth 8 | ConvertFrom-Json)
    $forged.evidenceContractHash = '0' * 64
    Write-Text $receiptPath (($forged | ConvertTo-Json -Depth 8) + "`n")
    [void]$cases.Add((Invoke-Validator $receiptPath $false 'forged-contract-hash-rejected'))

    $partial = ($receipt | ConvertTo-Json -Depth 8 | ConvertFrom-Json)
    $partial.PSObject.Properties.Remove('evidenceContractHash')
    Write-Text $receiptPath (($partial | ConvertTo-Json -Depth 8) + "`n")
    [void]$cases.Add((Invoke-Validator $receiptPath $false 'partial-contract-binding-rejected'))

    $legacy = ($receipt | ConvertTo-Json -Depth 8 | ConvertFrom-Json)
    $legacy.PSObject.Properties.Remove('evidenceContractId')
    $legacy.PSObject.Properties.Remove('evidenceContractHash')
    $legacy.PSObject.Properties.Remove('authorizationKind')
    $legacy.capturedUtc = '2026-08-26T03:44:00Z'
    $legacy | Add-Member -NotePropertyName planHash -NotePropertyValue ('a' * 64)
    Write-Text $receiptPath (($legacy | ConvertTo-Json -Depth 8) + "`n")
    $legacyWindowOpen = [DateTimeOffset]::UtcNow -le [DateTimeOffset]::Parse('2026-09-02T03:45:00Z')
    [void]$cases.Add((Invoke-Validator $receiptPath $legacyWindowOpen 'legacy-projection-window-enforced'))

    [pscustomobject]@{
        schemaVersion = 1
        validator = 'es-strict-evidence-receipt-contract'
        status = 'passed'
        caseCount = $cases.Count
        passedCount = $cases.Count
        cases = @($cases)
    } | ConvertTo-Json -Depth 6
} finally {
    if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
}
