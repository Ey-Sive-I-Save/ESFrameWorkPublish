Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:ContractsRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Contracts'))
$script:ReferenceSchemaPath = Join-Path $script:ContractsRoot 'es-interaction-binding-ref-v1.schema.json'
$script:ReceiptSchemaPath = Join-Path $script:ContractsRoot 'es-interaction-session-binding-receipt-v1.schema.json'
$script:AuthorityProofSchemaPath = Join-Path $script:ContractsRoot 'es-interaction-session-authority-proof-v1.schema.json'
$script:JsonSchemaLitePath = Join-Path $script:ContractsRoot 'ESJsonSchemaLite.psm1'
Import-Module $script:JsonSchemaLitePath -ErrorAction Stop

function ConvertTo-ESInteractionBindingCanonicalJson {
    [CmdletBinding()]
    param([AllowNull()]$Value)

    if ($null -eq $Value) { return 'null' }
    if ($Value -is [string] -or $Value -is [char]) { return ([string]$Value | ConvertTo-Json -Compress) }
    if ($Value -is [bool]) { return $(if ($Value) { 'true' } else { 'false' }) }
    if ($Value -is [datetime]) { return ($Value.ToUniversalTime().ToString('o') | ConvertTo-Json -Compress) }
    if ($Value -is [Collections.IDictionary]) {
        $parts = foreach ($key in @($Value.Keys | ForEach-Object { [string]$_ } | Sort-Object -CaseSensitive)) {
            '{0}:{1}' -f ($key | ConvertTo-Json -Compress), (ConvertTo-ESInteractionBindingCanonicalJson $Value[$key])
        }
        return '{' + ($parts -join ',') + '}'
    }
    if ($Value -is [pscustomobject]) {
        $parts = foreach ($property in @($Value.PSObject.Properties | Sort-Object Name -CaseSensitive)) {
            '{0}:{1}' -f ($property.Name | ConvertTo-Json -Compress), (ConvertTo-ESInteractionBindingCanonicalJson $property.Value)
        }
        return '{' + ($parts -join ',') + '}'
    }
    if ($Value -is [Collections.IEnumerable] -and -not ($Value -is [string])) {
        return '[' + (@($Value | ForEach-Object { ConvertTo-ESInteractionBindingCanonicalJson $_ }) -join ',') + ']'
    }
    if ($Value -is [IFormattable]) { return $Value.ToString($null, [Globalization.CultureInfo]::InvariantCulture) }
    return ([string]$Value | ConvertTo-Json -Compress)
}

function Get-ESInteractionBindingCanonicalHash {
    [CmdletBinding()]
    param([AllowNull()]$Value)

    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-ESInteractionBindingCanonicalJson $Value))
        return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    } finally { $sha.Dispose() }
}

function Get-ESInteractionAuthorityProofHashInput {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)]$AuthorityProof)

    [ordered]@{
        contractId = [string]$AuthorityProof.contractId
        proofId = [string]$AuthorityProof.proofId
        bindingId = [string]$AuthorityProof.bindingId
        authority = $AuthorityProof.authority
        registry = $AuthorityProof.registry
        acceptance = $AuthorityProof.acceptance
        process = $AuthorityProof.process
        issuedUtc = [string]$AuthorityProof.issuedUtc
    }
}

function Get-ESInteractionBindingReceiptHashInput {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)]$Receipt)

    [ordered]@{
        contractId = [string]$Receipt.contractId
        bindingId = [string]$Receipt.bindingId
        scope = $Receipt.scope
        session = $Receipt.session
        transcript = $Receipt.transcript
        authorityProofHash = [string]$Receipt.authorityProofHash
        issuedUtc = [string]$Receipt.issuedUtc
    }
}

function Assert-ESInteractionBindingSchema {
    param(
        [Parameter(Mandatory=$true)]$Value,
        [Parameter(Mandatory=$true)][string]$SchemaPath,
        [Parameter(Mandatory=$true)][string]$Name
    )
    $errors = @(Test-ESJsonSchemaValue -SchemaPath $SchemaPath -Value $Value)
    if ($errors.Count -gt 0) { throw ("$Name schema validation failed: " + ($errors -join '; ')) }
}

function New-ESInteractionAuthorityProofDocument {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$ProofId,
        [Parameter(Mandatory=$true)][string]$BindingId,
        [Parameter(Mandatory=$true)]$Authority,
        [Parameter(Mandatory=$true)]$Registry,
        [Parameter(Mandatory=$true)]$Acceptance,
        [Parameter(Mandatory=$true)]$Process,
        [Parameter(Mandatory=$true)][string]$IssuedUtc
    )

    $core = [ordered]@{
        contractId = 'es://automation/contracts/interaction-session-authority-proof/v1'
        proofId = $ProofId
        bindingId = $BindingId
        authority = $Authority
        registry = $Registry
        acceptance = $Acceptance
        process = $Process
        issuedUtc = $IssuedUtc
    }
    $document = [pscustomobject]([ordered]@{
        contractId = $core.contractId
        proofId = $core.proofId
        bindingId = $core.bindingId
        authority = $core.authority
        registry = $core.registry
        acceptance = $core.acceptance
        process = $core.process
        issuedUtc = $core.issuedUtc
        proofHash = Get-ESInteractionBindingCanonicalHash $core
    })
    Assert-ESInteractionBindingSchema $document $script:AuthorityProofSchemaPath 'AuthorityProof'
    return $document
}

function New-ESInteractionSessionBindingReceiptDocument {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$BindingId,
        [Parameter(Mandatory=$true)]$Scope,
        [Parameter(Mandatory=$true)]$Session,
        [Parameter(Mandatory=$true)]$Transcript,
        [Parameter(Mandatory=$true)][string]$AuthorityProofHash,
        [Parameter(Mandatory=$true)][string]$IssuedUtc
    )

    $core = [ordered]@{
        contractId = 'es://automation/contracts/interaction-session-binding-receipt/v1'
        bindingId = $BindingId
        scope = $Scope
        session = $Session
        transcript = $Transcript
        authorityProofHash = $AuthorityProofHash
        issuedUtc = $IssuedUtc
    }
    $document = [pscustomobject]([ordered]@{
        contractId = $core.contractId
        bindingId = $core.bindingId
        scope = $core.scope
        session = $core.session
        transcript = $core.transcript
        authorityProofHash = $core.authorityProofHash
        issuedUtc = $core.issuedUtc
        bindingHash = Get-ESInteractionBindingCanonicalHash $core
    })
    Assert-ESInteractionBindingSchema $document $script:ReceiptSchemaPath 'InteractionSessionBindingReceipt'
    return $document
}

function New-ESInteractionBindingReference {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)]$Receipt)

    $document = [pscustomobject][ordered]@{
        bindingId = [string]$Receipt.bindingId
        bindingHash = [string]$Receipt.bindingHash
    }
    Assert-ESInteractionBindingSchema $document $script:ReferenceSchemaPath 'InteractionBindingRef'
    return $document
}

function Test-ESInteractionBindingRelativePath {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path) -or $Path.Contains('\') -or [IO.Path]::IsPathRooted($Path) -or
        $Path -match '(^|/)\.\.?(/|$)' -or $Path -match '[:*?]') { return $false }
    return $true
}

function New-ESInteractionBindingValidationResult {
    param(
        [bool]$Valid,
        [string]$ReasonCode,
        [string]$Object,
        [string]$Field,
        [AllowNull()][string]$Effect,
        [string]$Outcome,
        [string]$Recovery,
        [string[]]$Evidence
    )
    [pscustomobject][ordered]@{
        valid = $Valid
        reasonCode = $ReasonCode
        object = $Object
        field = $Field
        profile = 'interaction-observation'
        scope = 'task-object'
        effect = $Effect
        outcome = $Outcome
        evidence = @($Evidence)
        recovery = $Recovery
        productionRouteIntegrated = $false
        globalP0Integrated = $false
    }
}

function Test-ESInteractionSessionBindingContract {
    [CmdletBinding()]
    param(
        $Reference,
        $Receipt,
        $AuthorityProof,
        [Parameter(Mandatory=$true)][string]$ExpectedTaskId,
        [Parameter(Mandatory=$true)][string]$ExpectedGoalRevisionHash,
        [Parameter(Mandatory=$true)][string]$ExpectedRoutePlanHash,
        [Parameter(Mandatory=$true)][string]$ExpectedProjectRootHash,
        [Parameter(Mandatory=$true)][string]$ExpectedLaunchTokenHash,
        [Parameter(Mandatory=$true)][int]$ExpectedPid,
        [Parameter(Mandatory=$true)][string]$ExpectedProcessStartUtc,
        [Parameter(Mandatory=$true)][string]$ExpectedAncestorChainHash
    )

    if ($null -eq $Reference -or $null -eq $Receipt -or $null -eq $AuthorityProof) {
        return New-ESInteractionBindingValidationResult $false 'INTERACTION_BINDING.EVIDENCE_MISSING' 'InteractionSessionBinding' 'artifact' 'claim-cap' 'evidence-pending' 'Issue a platform binding before closing interaction-observation evidence.' @()
    }

    foreach ($entry in @(
        [pscustomobject]@{Name='InteractionBindingRef';Value=$Reference;Schema=$script:ReferenceSchemaPath},
        [pscustomobject]@{Name='InteractionSessionBindingReceipt';Value=$Receipt;Schema=$script:ReceiptSchemaPath},
        [pscustomobject]@{Name='AuthorityProof';Value=$AuthorityProof;Schema=$script:AuthorityProofSchemaPath}
    )) {
        $errors = @(Test-ESJsonSchemaValue -SchemaPath $entry.Schema -Value $entry.Value)
        if ($errors.Count -gt 0) {
            return New-ESInteractionBindingValidationResult $false 'INTERACTION_BINDING.CONTRACT_INVALID' $entry.Name 'schema' 'hard-block' 'reject-binding-request' 'Correct only the invalid binding artifact and retry the scoped request.' @($errors)
        }
    }

    foreach ($pathEntry in @(
        [pscustomobject]@{Object='AuthorityProof.registry';Field='relativePath';Value=[string]$AuthorityProof.registry.relativePath},
        [pscustomobject]@{Object='AuthorityProof.registry.record';Field='transcriptRelativePath';Value=[string]$AuthorityProof.registry.record.transcriptRelativePath},
        [pscustomobject]@{Object='AuthorityProof.acceptance';Field='relativePath';Value=[string]$AuthorityProof.acceptance.relativePath},
        [pscustomobject]@{Object='InteractionSessionBindingReceipt.transcript';Field='relativePath';Value=[string]$Receipt.transcript.relativePath}
    )) {
        if (-not (Test-ESInteractionBindingRelativePath $pathEntry.Value)) {
            return New-ESInteractionBindingValidationResult $false 'INTERACTION_BINDING.PATH_NOT_NORMALIZED' $pathEntry.Object $pathEntry.Field 'hard-block' 'reject-binding-request' 'Use a normalized path relative to the trusted authority root.' @($pathEntry.Value)
        }
    }

    $expectedProofHash = Get-ESInteractionBindingCanonicalHash (Get-ESInteractionAuthorityProofHashInput $AuthorityProof)
    if ([string]$AuthorityProof.proofHash -cne $expectedProofHash) {
        return New-ESInteractionBindingValidationResult $false 'INTERACTION_BINDING.PROOF_HASH_MISMATCH' 'AuthorityProof' 'proofHash' 'hard-block' 'reject-binding-request' 'Reissue AuthorityProof from current platform evidence.' @($expectedProofHash)
    }
    $expectedBindingHash = Get-ESInteractionBindingCanonicalHash (Get-ESInteractionBindingReceiptHashInput $Receipt)
    if ([string]$Receipt.bindingHash -cne $expectedBindingHash -or [string]$Reference.bindingHash -cne $expectedBindingHash) {
        return New-ESInteractionBindingValidationResult $false 'INTERACTION_BINDING.RECEIPT_HASH_MISMATCH' 'InteractionSessionBindingReceipt' 'bindingHash' 'hard-block' 'reject-binding-request' 'Reissue the scoped Binding Receipt and its two-field reference.' @($expectedBindingHash)
    }
    if ([string]$Reference.bindingId -cne [string]$Receipt.bindingId -or [string]$Receipt.bindingId -cne [string]$AuthorityProof.bindingId) {
        return New-ESInteractionBindingValidationResult $false 'INTERACTION_BINDING.IDENTITY_MISMATCH' 'InteractionSessionBinding' 'bindingId' 'hard-block' 'reject-binding-request' 'Use artifacts issued for the same bindingId.' @()
    }
    if ([string]$Receipt.authorityProofHash -cne [string]$AuthorityProof.proofHash) {
        return New-ESInteractionBindingValidationResult $false 'INTERACTION_BINDING.AUTHORITY_HASH_MISMATCH' 'InteractionSessionBindingReceipt' 'authorityProofHash' 'hard-block' 'reject-binding-request' 'Bind the Receipt to the exact current AuthorityProof hash.' @()
    }
    $expectedProcessStart = [datetimeoffset]::Parse($ExpectedProcessStartUtc, [Globalization.CultureInfo]::InvariantCulture)
    $proofProcessStart = [datetimeoffset]::Parse([string]$AuthorityProof.process.processStartUtc, [Globalization.CultureInfo]::InvariantCulture)
    if ([string]$AuthorityProof.authority.projectRootHash -cne $ExpectedProjectRootHash -or
        [string]$AuthorityProof.authority.launchTokenHash -cne $ExpectedLaunchTokenHash -or
        [int]$AuthorityProof.process.pid -ne $ExpectedPid -or
        $proofProcessStart -ne $expectedProcessStart -or
        [string]$AuthorityProof.process.ancestorChainHash -cne $ExpectedAncestorChainHash) {
        return New-ESInteractionBindingValidationResult $false 'INTERACTION_BINDING.AUTHORITY_CONTEXT_MISMATCH' 'AuthorityProof' 'authority/process' 'hard-block' 'reject-binding-request' 'Resolve authority again from the current project and process identity.' @()
    }
    if ([string]$Receipt.scope.taskId -cne $ExpectedTaskId -or
        [string]$Receipt.scope.goalRevisionHash -cne $ExpectedGoalRevisionHash -or
        [string]$Receipt.scope.routePlanHash -cne $ExpectedRoutePlanHash) {
        return New-ESInteractionBindingValidationResult $false 'INTERACTION_BINDING.SCOPE_MISMATCH' 'InteractionSessionBindingReceipt' 'scope' 'hard-block' 'reject-binding-request' 'Issue a new binding for the current Task, GoalRevision, and RoutePlan.' @()
    }
    if ([string]$Receipt.session.recordId -cne [string]$AuthorityProof.registry.record.recordId -or
        [string]$Receipt.session.sessionId -cne [string]$AuthorityProof.registry.record.sessionId -or
        [string]$Receipt.transcript.relativePath -cne [string]$AuthorityProof.registry.record.transcriptRelativePath) {
        return New-ESInteractionBindingValidationResult $false 'INTERACTION_BINDING.SESSION_MISMATCH' 'InteractionSessionBindingReceipt' 'session' 'hard-block' 'reject-binding-request' 'Resolve and bind one unique accepted session record.' @()
    }
    if ([int64]$Receipt.transcript.taskStartByteOffset -gt [int64]$Receipt.transcript.snapshotLength) {
        return New-ESInteractionBindingValidationResult $false 'INTERACTION_BINDING.TRANSCRIPT_RANGE_INVALID' 'InteractionSessionBindingReceipt.transcript' 'taskStartByteOffset' 'hard-block' 'reject-binding-request' 'Reissue the transcript snapshot with an in-range task byte offset.' @()
    }
    if ([datetimeoffset]::Parse([string]$AuthorityProof.issuedUtc, [Globalization.CultureInfo]::InvariantCulture) -gt
        [datetimeoffset]::Parse([string]$Receipt.issuedUtc, [Globalization.CultureInfo]::InvariantCulture)) {
        return New-ESInteractionBindingValidationResult $false 'INTERACTION_BINDING.ISSUANCE_ORDER_INVALID' 'InteractionSessionBindingReceipt' 'issuedUtc' 'hard-block' 'reject-binding-request' 'Issue the Binding Receipt after its AuthorityProof.' @()
    }

    return New-ESInteractionBindingValidationResult $true 'INTERACTION_BINDING.VALID' 'InteractionSessionBinding' 'bindingHash' $null 'binding-verified' 'No recovery is required.' @([string]$Receipt.bindingHash, [string]$AuthorityProof.proofHash)
}

Export-ModuleMember -Function ConvertTo-ESInteractionBindingCanonicalJson,Get-ESInteractionBindingCanonicalHash,Get-ESInteractionAuthorityProofHashInput,Get-ESInteractionBindingReceiptHashInput,New-ESInteractionAuthorityProofDocument,New-ESInteractionSessionBindingReceiptDocument,New-ESInteractionBindingReference,Test-ESInteractionSessionBindingContract
