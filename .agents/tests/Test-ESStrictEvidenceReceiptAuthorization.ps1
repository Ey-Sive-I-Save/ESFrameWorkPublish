[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$utf8 = New-Object Text.UTF8Encoding($false)
$root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path.TrimEnd('\', '/')
$rootPrefix = $root + [IO.Path]::DirectorySeparatorChar
$validator = Join-Path $root '.agents/skills/es-skill-governance/scripts/Test-ESStrictEvidenceReceipt.ps1'
if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) {
    throw "Strict evidence validator not found: $validator"
}

$fixtureRoot = Join-Path $root ('ES/Output/SkillValidationFixtures/es-strict-evidence-receipt-' + [Guid]::NewGuid().ToString('N'))
$fixtureFull = [IO.Path]::GetFullPath($fixtureRoot)
if (-not $fixtureFull.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Fixture path escapes ProjectRoot'
}

$skillPath = Join-Path $fixtureRoot '.agents/skills/es-fixture'
$sourcePath = Join-Path $fixtureRoot 'source.txt'
$evidenceRoot = Join-Path $fixtureRoot 'evidence'
$results = New-Object 'System.Collections.Generic.List[object]'

function Write-Utf8([string]$Path, [string]$Content) {
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    [IO.File]::WriteAllText($Path, $Content, $utf8)
}

function New-Receipt([string]$CaseName, [System.Collections.IDictionary]$AuthorizationFields) {
    $receiptRelative = "evidence/$CaseName.json"
    $receiptPath = Join-Path $fixtureRoot $receiptRelative
    $receipt = [ordered]@{
        skillName = 'es-fixture'
        case = $CaseName
        status = 'passed'
        evidenceLevel = 'S1'
        receiptPath = $receiptRelative
        sourceRefs = @('source.txt')
        sourceRefHashes = [ordered]@{ 'source.txt' = $script:sourceHash }
        toolId = 'es-strict-evidence-receipt-regression'
        unityVersion = 'not-applicable'
        capturedUtc = [DateTime]::UtcNow.ToString('o')
    }
    foreach ($key in $AuthorizationFields.Keys) {
        $receipt[$key] = $AuthorizationFields[$key]
    }
    Write-Utf8 $receiptPath ($receipt | ConvertTo-Json -Depth 8)
    return $receiptPath
}

function Add-Case(
    [string]$Name,
    [bool]$ShouldPass,
    [System.Collections.IDictionary]$AuthorizationFields,
    [string]$ExpectedMessage = ''
) {
    $receiptPath = New-Receipt $Name $AuthorizationFields
    $actual = 'passed'
    $message = ''
    try {
        [void](& $validator -SkillPath $skillPath -EvidencePath $receiptPath -ProjectRoot $fixtureRoot)
    } catch {
        $actual = 'failed'
        $message = [string]$_.Exception.Message
    }

    $expected = if ($ShouldPass) { 'passed' } else { 'failed' }
    $messageMatched = [string]::IsNullOrEmpty($ExpectedMessage) -or $message.Contains($ExpectedMessage)
    [void]$results.Add([pscustomobject][ordered]@{
        name = $Name
        expected = $expected
        actual = $actual
        messageMatched = $messageMatched
        passed = ($actual -eq $expected -and $messageMatched)
        error = $message
    })
}

try {
    New-Item -ItemType Directory -Path $skillPath -Force | Out-Null
    New-Item -ItemType Directory -Path $evidenceRoot -Force | Out-Null
    Write-Utf8 (Join-Path $skillPath 'SKILL.md') "---`nname: es-fixture`ndescription: Receipt fixture.`n---`n"
    Write-Utf8 $sourcePath "strict receipt source`n"
    $script:sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()

    Add-Case 'managed-valid' $true ([ordered]@{
        authorizationKind = 'managed-aibrain'
        planHash = 'a' * 64
    })
    Add-Case 'managed-missing-plan' $false ([ordered]@{
        authorizationKind = 'managed-aibrain'
    }) 'planHash must be a SHA-256 value'
    Add-Case 'managed-invalid-plan' $false ([ordered]@{
        authorizationKind = 'managed-aibrain'
        planHash = 'not-a-hash'
    }) 'planHash must be a SHA-256 value'

    Add-Case 'direct-valid' $true ([ordered]@{
        authorizationKind = 'current-user-direct'
        userInstructionHash = 'b' * 64
        authorizedOperations = @('modify', 'create')
        authorizedPaths = @('.agents/skills/es-fixture', 'evidence/direct-valid.json')
        planHash = 'not-applicable'
    })
    Add-Case 'direct-missing-instruction-hash' $false ([ordered]@{
        authorizationKind = 'current-user-direct'
        authorizedOperations = @('modify')
        authorizedPaths = @('.agents')
    }) 'userInstructionHash must be a SHA-256 value'
    Add-Case 'direct-scalar-operation' $false ([ordered]@{
        authorizationKind = 'current-user-direct'
        userInstructionHash = 'b' * 64
        authorizedOperations = 'modify'
        authorizedPaths = @('.agents')
    }) 'authorizedOperations must be a non-empty JSON string array'
    Add-Case 'direct-empty-operations' $false ([ordered]@{
        authorizationKind = 'current-user-direct'
        userInstructionHash = 'b' * 64
        authorizedOperations = @()
        authorizedPaths = @('.agents')
    }) 'authorizedOperations must be a non-empty JSON string array'
    Add-Case 'direct-non-string-operation' $false ([ordered]@{
        authorizationKind = 'current-user-direct'
        userInstructionHash = 'b' * 64
        authorizedOperations = @('modify', 1)
        authorizedPaths = @('.agents')
    }) 'authorizedOperations must contain only non-empty strings'
    Add-Case 'direct-empty-paths' $false ([ordered]@{
        authorizationKind = 'current-user-direct'
        userInstructionHash = 'b' * 64
        authorizedOperations = @('modify')
        authorizedPaths = @()
    }) 'authorizedPaths must be a non-empty JSON string array'
    Add-Case 'direct-non-string-path' $false ([ordered]@{
        authorizationKind = 'current-user-direct'
        userInstructionHash = 'b' * 64
        authorizedOperations = @('modify')
        authorizedPaths = @('.agents', 1)
    }) 'authorizedPaths must contain only non-empty strings'
    Add-Case 'direct-absolute-path' $false ([ordered]@{
        authorizationKind = 'current-user-direct'
        userInstructionHash = 'b' * 64
        authorizedOperations = @('modify')
        authorizedPaths = @((Join-Path $fixtureRoot '.agents'))
    }) 'authorizedPaths must contain project-relative paths'
    Add-Case 'direct-project-escape' $false ([ordered]@{
        authorizationKind = 'current-user-direct'
        userInstructionHash = 'b' * 64
        authorizedOperations = @('modify')
        authorizedPaths = @('../outside')
    }) 'authorizedPaths escapes ProjectRoot'

    Add-Case 'read-only-valid' $true ([ordered]@{
        authorizationKind = 'read-only'
    })
    Add-Case 'legacy-managed-valid' $true ([ordered]@{
        planHash = 'c' * 64
    })
    Add-Case 'missing-discriminator-and-plan' $false ([ordered]@{}) 'authorizationKind is required unless a legacy SHA-256 planHash is present'
    Add-Case 'unsupported-kind' $false ([ordered]@{
        authorizationKind = 'implicit'
    }) 'Unsupported authorizationKind: implicit'

    $missingReceiptPath = Join-Path $evidenceRoot 'missing.json'
    $missingActual = 'passed'
    $missingMessage = ''
    try {
        [void](& $validator -SkillPath $skillPath -EvidencePath $missingReceiptPath -ProjectRoot $fixtureRoot)
    } catch {
        $missingActual = 'failed'
        $missingMessage = [string]$_.Exception.Message
    }
    [void]$results.Add([pscustomobject][ordered]@{
        name = 'missing-receipt-is-evidence-only'
        expected = 'failed'
        actual = $missingActual
        messageMatched = $missingMessage.Contains('evidence claim is unavailable; project action authority is unchanged')
        passed = ($missingActual -eq 'failed' -and $missingMessage.Contains('evidence claim is unavailable; project action authority is unchanged'))
        error = $missingMessage
    })

    $outsideActual = 'passed'
    $outsideMessage = ''
    try {
        [void](& $validator -SkillPath $skillPath -EvidencePath $validator -ProjectRoot $fixtureRoot)
    } catch {
        $outsideActual = 'failed'
        $outsideMessage = [string]$_.Exception.Message
    }
    [void]$results.Add([pscustomobject][ordered]@{
        name = 'outside-receipt-denied-before-read'
        expected = 'failed'
        actual = $outsideActual
        messageMatched = $outsideMessage.Contains('EvidencePath must identify a receipt inside ProjectRoot')
        passed = ($outsideActual -eq 'failed' -and $outsideMessage.Contains('EvidencePath must identify a receipt inside ProjectRoot'))
        error = $outsideMessage
    })

    $failed = @($results | Where-Object { -not $_.passed })
    $report = [ordered]@{
        schemaVersion = 1
        validator = 'es-strict-evidence-receipt-authorization-regression'
        status = if ($failed.Count -eq 0) { 'static-passed' } else { 'blocked' }
        caseCount = $results.Count
        failedCount = $failed.Count
        cases = $results.ToArray()
        claimsNotProven = @('host authenticity of the current user message', 'Runtime behavior', 'release behavior')
    }
    $report | ConvertTo-Json -Depth 8
    if ($failed.Count -gt 0) { throw "$($failed.Count) strict evidence receipt regression case(s) failed" }
} finally {
    if ($fixtureFull.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $fixtureFull)) {
        Remove-Item -LiteralPath $fixtureFull -Recurse -Force
    }
}
