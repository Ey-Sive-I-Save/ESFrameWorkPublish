Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-ESABCDCertificationCanonical($Value) {
    if ($null -eq $Value) { return 'null' }
    if ($Value -is [string] -or $Value -is [char]) { return ([string]$Value | ConvertTo-Json -Compress) }
    if ($Value -is [bool]) { return $(if ($Value) { 'true' } else { 'false' }) }
    if ($Value -is [Collections.IDictionary]) {
        return '{' + ((@($Value.Keys | ForEach-Object { [string]$_ } | Sort-Object) | ForEach-Object { ('{0}:{1}' -f ($_ | ConvertTo-Json -Compress), (ConvertTo-ESABCDCertificationCanonical $Value[$_])) }) -join ',') + '}'
    }
    if ($Value -is [pscustomobject]) {
        return '{' + ((@($Value.PSObject.Properties | Sort-Object Name) | ForEach-Object { ('{0}:{1}' -f ($_.Name | ConvertTo-Json -Compress), (ConvertTo-ESABCDCertificationCanonical $_.Value)) }) -join ',') + '}'
    }
    if ($Value -is [Collections.IEnumerable] -and $Value -isnot [string]) { return '[' + ((@($Value) | ForEach-Object { ConvertTo-ESABCDCertificationCanonical $_ }) -join ',') + ']' }
    return ([string]$Value | ConvertTo-Json -Compress)
}

function Get-ESABCDCertificationHash($Value) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes((ConvertTo-ESABCDCertificationCanonical $Value)))).Replace('-', '').ToLowerInvariant()) }
    finally { $sha.Dispose() }
}

function Assert-ESABCDCertificationHash([string]$Value, [string]$Name) { if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch '^[a-f0-9]{64}$') { throw "$Name must be a lowercase SHA-256 hash." } }
function Assert-ESABCDCertificationId([string]$Value, [string]$Name) { if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch '^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$') { throw "$Name is invalid." } }
function Get-ESABCDCertificationProperty($Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    if ($Object -is [Collections.IDictionary]) {
        if ($Object.Contains($Name)) { return $Object[$Name] }
        return $null
    }
    if ($null -eq $Object.PSObject) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-ESABCDCurrentGitSnapshot([string]$ProjectRoot) {
    try {
        $head = ((& git -C $ProjectRoot rev-parse HEAD 2>$null) -join '').Trim().ToLowerInvariant()
        if ($LASTEXITCODE -ne 0) { return $null }
        $branch = ((& git -C $ProjectRoot branch --show-current 2>$null) -join '').Trim()
        if ($LASTEXITCODE -ne 0) { return $null }
        $status = ((& git -C $ProjectRoot status --porcelain=v1 --untracked-files=all 2>$null) -join "`n")
        if ($LASTEXITCODE -ne 0) { return $null }
        if ($head -notmatch '^[a-f0-9]{40}$' -or [string]::IsNullOrWhiteSpace($branch)) { return $null }
        [pscustomobject]@{ branch = $branch; head = $head; worktreeHash = Get-ESABCDCertificationHash ([ordered]@{ branch = $branch; head = $head; status = $status }) }
    } catch { return $null }
}

function ConvertTo-ESABCDCertificationProjectRelativePath([string]$ProjectRoot, [string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or [IO.Path]::IsPathRooted($Path) -or $Path -match '(^|[/\\])\.\.([/\\]|$)') {
        throw 'PROJECT_RELATIVE_PATH_REQUIRED'
    }
    $root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path.TrimEnd('\','/')
    $full = [IO.Path]::GetFullPath((Join-Path $root $Path))
    $prefix = $root + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'PROJECT_RELATIVE_PATH_REQUIRED' }
    return $full.Substring($prefix.Length).Replace('\','/')
}

function Get-ESABCDCertificationReceipt([string]$ProjectRoot, [string]$Path) {
    $relative = ConvertTo-ESABCDCertificationProjectRelativePath $ProjectRoot $Path
    if ([IO.Path]::GetExtension($relative).ToLowerInvariant() -ne '.json') { throw 'EVIDENCE_RECEIPT_JSON_REQUIRED' }
    $root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
    $full = Join-Path $root ($relative.Replace('/', [IO.Path]::DirectorySeparatorChar))
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "EVIDENCE_MISSING:$relative" }
    try {
        $raw = [IO.File]::ReadAllText($full, [Text.UTF8Encoding]::new($false, $true))
        $receipt = $raw | ConvertFrom-Json -ErrorAction Stop
    } catch { throw "EVIDENCE_RECEIPT_INVALID:$relative" }
    [pscustomobject]@{ relative = $relative; full = $full; receipt = $receipt }
}

function Assert-ESABCDCertificationReceiptSourceRefs([string]$ProjectRoot, $Receipt, [Collections.Generic.List[string]]$Issues, [string]$EvidencePath) {
    $root = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
    $refs = @(Get-ESABCDCertificationProperty $Receipt 'sourceRefs')
    if ($refs.Count -eq 0) { [void]$Issues.Add("EVIDENCE_SOURCE_REFS_REQUIRED:$EvidencePath"); return }
    $sourceRefHashes = Get-ESABCDCertificationProperty $Receipt 'sourceRefHashes'
    if ($null -eq $sourceRefHashes) { [void]$Issues.Add("EVIDENCE_SOURCE_HASHES_REQUIRED:$EvidencePath"); return }
    foreach ($ref in $refs) {
        try { $relative = ConvertTo-ESABCDCertificationProjectRelativePath $root ([string]$ref) } catch { [void]$Issues.Add("EVIDENCE_SOURCE_PATH_INVALID:$EvidencePath"); continue }
        $full = Join-Path $root ($relative.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { [void]$Issues.Add("EVIDENCE_SOURCE_MISSING:${EvidencePath}:$relative"); continue }
        $hashProperty = $sourceRefHashes.PSObject.Properties[[string]$ref]
        if ($null -eq $hashProperty) { $hashProperty = $sourceRefHashes.PSObject.Properties[$relative] }
        if ($null -eq $hashProperty -or [string]$hashProperty.Value -notmatch '^[a-fA-F0-9]{64}$') { [void]$Issues.Add("EVIDENCE_SOURCE_HASH_MISSING:${EvidencePath}:$relative"); continue }
        $actual = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -cne [string]$hashProperty.Value.ToLowerInvariant()) { [void]$Issues.Add("EVIDENCE_SOURCE_DRIFT:${EvidencePath}:$relative") }
    }
}

function Test-ESABCDCertificationEvidenceRef([string]$ProjectRoot, $EvidenceRef, [string]$Bucket, [Collections.Generic.List[string]]$Issues) {
    $expectedBucketStatus = switch ($Bucket) { 'static' { 'static-passed' } 'runtime' { 'runtime-passed' } 'release' { 'release-passed' } default { throw 'EVIDENCE_BUCKET_INVALID' } }
    $entryPath = [string](Get-ESABCDCertificationProperty $EvidenceRef 'path')
    if ([string]::IsNullOrWhiteSpace($entryPath)) { [void]$Issues.Add("EVIDENCE_PATH_REQUIRED:${Bucket}"); return }
    try { $receiptInfo = Get-ESABCDCertificationReceipt $ProjectRoot $entryPath } catch { [void]$Issues.Add($_.Exception.Message); return }
    $relative = [string]$receiptInfo.relative; $receipt = $receiptInfo.receipt
    $actualHash = (Get-FileHash -LiteralPath $receiptInfo.full -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -cne [string](Get-ESABCDCertificationProperty $EvidenceRef 'sha256')) { [void]$Issues.Add("EVIDENCE_DRIFT:$relative") }
    $centralPath = Join-Path (Resolve-Path -LiteralPath $ProjectRoot).Path 'ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json'
    if (-not (Test-Path -LiteralPath $centralPath -PathType Leaf)) { [void]$Issues.Add('CENTRAL_EVIDENCE_CONTRACT_MISSING'); return }
    $centralHash = (Get-FileHash -LiteralPath $centralPath -Algorithm SHA256).Hash.ToLowerInvariant()
    foreach ($field in @('evidenceContractId','evidenceContractHash','skillName','case','status','evidenceLevel','receiptPath','sourceRefs','sourceRefHashes','toolId','unityVersion','capturedUtc')) {
        $property = $receipt.PSObject.Properties[$field]
        if ($null -eq $property -or $null -eq $property.Value -or ($property.Value -is [string] -and [string]::IsNullOrWhiteSpace([string]$property.Value))) { [void]$Issues.Add("EVIDENCE_RECEIPT_FIELD_MISSING:${relative}:$field") }
    }
    $receiptContractId = Get-ESABCDCertificationProperty $receipt 'evidenceContractId'; $receiptContractHash = Get-ESABCDCertificationProperty $receipt 'evidenceContractHash'; $receiptPath = Get-ESABCDCertificationProperty $receipt 'receiptPath'; $receiptSkillName = Get-ESABCDCertificationProperty $receipt 'skillName'; $receiptCase = Get-ESABCDCertificationProperty $receipt 'case'; $receiptLevel = Get-ESABCDCertificationProperty $receipt 'evidenceLevel'; $receiptStatus = Get-ESABCDCertificationProperty $receipt 'status'; $receiptCapturedUtc = Get-ESABCDCertificationProperty $receipt 'capturedUtc'
    if ([string]$receiptContractId -cne 'es.skill-evidence-receipt' -or [string]$receiptContractHash -cne $centralHash) { [void]$Issues.Add("EVIDENCE_CONTRACT_BINDING_INVALID:$relative") }
    $refReceiptPath = if ($null -ne $EvidenceRef.PSObject.Properties['receiptPath']) { [string]$EvidenceRef.receiptPath } else { '' }
    $refSkillName = if ($null -ne $EvidenceRef.PSObject.Properties['skillName']) { [string]$EvidenceRef.skillName } else { '' }
    $refCase = if ($null -ne $EvidenceRef.PSObject.Properties['case']) { [string]$EvidenceRef.case } elseif ($null -ne $EvidenceRef.PSObject.Properties['caseId']) { [string]$EvidenceRef.caseId } else { '' }
    $refLevel = if ($null -ne $EvidenceRef.PSObject.Properties['evidenceLevel']) { [string]$EvidenceRef.evidenceLevel } else { '' }
    $refStatus = if ($null -ne $EvidenceRef.PSObject.Properties['status']) { [string]$EvidenceRef.status } else { '' }
    if ([string]$receiptPath -ne $relative -or $refReceiptPath -ne $relative) { [void]$Issues.Add("EVIDENCE_RECEIPT_PATH_MISMATCH:$relative") }
    if ($refSkillName -ne [string]$receiptSkillName -or $refCase -ne [string]$receiptCase) { [void]$Issues.Add("EVIDENCE_IDENTITY_MISMATCH:$relative") }
    if ($refLevel -ne [string]$receiptLevel -or [string]$receiptLevel -notmatch '^S[0-6]$') { [void]$Issues.Add("EVIDENCE_LEVEL_INVALID:$relative") }
    if ($refStatus -ne $expectedBucketStatus) { [void]$Issues.Add("EVIDENCE_BUCKET_STATUS_INVALID:${Bucket}:$relative") }
    if ([string]$receiptStatus -cne 'passed') { [void]$Issues.Add("EVIDENCE_RECEIPT_NOT_PASSED:$relative") }
    $axisProperty = switch ($Bucket) { 'static' { 'staticStatus' } 'runtime' { 'runtimeStatus' } 'release' { 'releaseStatus' } }
    $axisStatus = if ($null -ne $receipt.PSObject.Properties[$axisProperty]) { [string]$receipt.PSObject.Properties[$axisProperty].Value } else { $null }
    if ($axisStatus -cne $expectedBucketStatus) { [void]$Issues.Add("EVIDENCE_AXIS_STATUS_INVALID:${Bucket}:$relative") }
    $level = if ([string]$receiptLevel -match '^S([0-6])$') { [int]$Matches[1] } else { -1 }
    $minimumLevel = @{ static = 1; runtime = 2; release = 3 }[$Bucket]
    if ($level -lt $minimumLevel) { [void]$Issues.Add("EVIDENCE_LEVEL_TOO_LOW:${Bucket}:$relative") }
    try {
        $captured = [DateTime]::Parse([string]$receiptCapturedUtc, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime()
        if ($captured -gt [DateTime]::UtcNow.AddMinutes(5)) { [void]$Issues.Add("EVIDENCE_FROM_FUTURE:$relative") }
        if (([DateTime]::UtcNow - $captured).TotalHours -gt 168) { [void]$Issues.Add("EVIDENCE_STALE:$relative") }
    } catch { [void]$Issues.Add("EVIDENCE_TIME_INVALID:$relative") }
    Assert-ESABCDCertificationReceiptSourceRefs $ProjectRoot $receipt $Issues $relative
}

function Get-ESABCDCertificationEvidenceManifest($Assessment) {
    [ordered]@{ profile = [string]$Assessment.profile; requiredCaseIds = @($Assessment.evidence.requiredCaseIds); completedCaseIds = @($Assessment.evidence.completedCaseIds); static = @($Assessment.evidence.static); runtime = @($Assessment.evidence.runtime); release = @($Assessment.evidence.release) }
}

function Get-ESABCDCertificationSignedPayloadHash($Assessment) {
    $copy = [ordered]@{}
    foreach ($p in $Assessment.PSObject.Properties) {
        if ($p.Name -eq 'assessmentHash') { continue }
        if ($p.Name -eq 'verifier') {
            $v = [ordered]@{}
            foreach ($vp in $p.Value.PSObject.Properties) { if ($vp.Name -notin @('signatureStatus','signatureVerificationMethod','signatureRef','signatureHash','signedAtUtc','signedPayloadHash')) { $v[$vp.Name] = $vp.Value } }
            $copy[$p.Name] = $v
        } else { $copy[$p.Name] = $p.Value }
    }
    return Get-ESABCDCertificationHash ([ordered]@{ assessment = $copy; evidenceManifest = Get-ESABCDCertificationEvidenceManifest $Assessment })
}

function Get-ESABCDCertificationHashInput($Assessment) {
    $x = [ordered]@{}
    foreach ($p in $Assessment.PSObject.Properties) { if ($p.Name -ne 'assessmentHash') { $x[$p.Name] = $p.Value } }
    return $x
}

function New-ESABCDCertificationAssessment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Subject,
        [Parameter(Mandatory)][ValidateSet('DesignReview','RuntimeAcceptance','ReleaseAcceptance')][string]$Profile,
        [Parameter(Mandatory)][string]$Branch,
        [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$Head,
        [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$WorktreeHash,
        [Parameter(Mandatory)]$SourceRefs,
        [Parameter(Mandatory)]$Evidence,
        [Parameter(Mandatory)][string[]]$RequiredCaseIds,
        [Parameter(Mandatory)][string[]]$CompletedCaseIds,
        [Parameter(Mandatory)][string]$VerifierId,
        [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$VerifierDefinitionHash,
        [Parameter(Mandatory)][ValidateSet('project-acceptance-owner','independent-auditor','external-certification-body')][string]$VerifierRole,
        [Parameter(Mandatory)][string]$IndependenceProof,
        [string]$SnapshotArtifactPath,
        [ValidatePattern('^[a-f0-9]{64}$')][string]$SnapshotArtifactHash,
        [string]$IssuerRef,
        [ValidatePattern('^[a-f0-9]{64}$')][string]$SignedPayloadHash,
        [ValidateSet('not-requested','pending','verified','failed')][string]$SignatureStatus = 'pending',
        [ValidateSet('none','external-detached-signature')][string]$SignatureVerificationMethod = 'none',
        [string]$SignatureRef,
        [ValidatePattern('^[a-f0-9]{64}$')][string]$SignatureHash,
        [string]$SignedAtUtc
    )
    if ($null -eq $Subject -or [string]::IsNullOrWhiteSpace([string]$Subject.kind) -or [string]::IsNullOrWhiteSpace([string]$Subject.id)) { throw 'SUBJECT_REQUIRED' }
    Assert-ESABCDCertificationId ([string]$Subject.id) 'SubjectId'; Assert-ESABCDCertificationId $VerifierId 'VerifierId'; Assert-ESABCDCertificationHash $VerifierDefinitionHash 'VerifierDefinitionHash'
    if ([string]::IsNullOrWhiteSpace($Branch) -or [string]::IsNullOrWhiteSpace($IndependenceProof)) { throw 'VERIFIER_INDEPENDENCE_PROOF_REQUIRED' }
    if (@($SourceRefs).Count -lt 1 -or @($RequiredCaseIds).Count -lt 1) { throw 'SOURCE_AND_CASES_REQUIRED' }
    foreach ($s in @($SourceRefs)) { if ([string]$s.path -match '(^|[/\\])\.\.([/\\]|$)' -or [IO.Path]::IsPathRooted([string]$s.path)) { throw 'SOURCE_PATH_OUTSIDE_PROJECT' }; Assert-ESABCDCertificationHash ([string]$s.sha256) 'SourceRefHash' }
    $now = if ($SignedAtUtc) { $SignedAtUtc } else { [DateTime]::UtcNow.ToString('o') }
    try { [DateTime]::Parse($now, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind) | Out-Null } catch { throw 'SIGNED_TIME_INVALID' }
    $assessmentSeed = [ordered]@{ subject = $Subject; profile = $Profile; head = $Head.ToLowerInvariant(); worktreeHash = $WorktreeHash.ToLowerInvariant(); sourceRefs = @($SourceRefs); evidence = $Evidence; requiredCaseIds = @($RequiredCaseIds); completedCaseIds = @($CompletedCaseIds); verifierId = $VerifierId; verifierDefinitionHash = $VerifierDefinitionHash.ToLowerInvariant() }
    $assessmentId = 'abcd-ca-' + (Get-ESABCDCertificationHash $assessmentSeed).Substring(0,32)
    $assessment = [ordered]@{
        schemaVersion = 1; contractId = 'es://automation/contracts/abcd/certification-assessment/v1'; recordType = 'ABCDCertificationAssessment'; assessmentId = $assessmentId; subject = $Subject; profile = $Profile; status = 'candidate'; decisionStatus = 'evidence-pending'
        sourceSnapshot = [ordered]@{ branch = $Branch; head = $Head.ToLowerInvariant(); worktreeHash = $WorktreeHash.ToLowerInvariant(); sourceRefs = @($SourceRefs); capturedUtc = [DateTime]::UtcNow.ToString('o') }
        evidence = [ordered]@{ static = @($Evidence.static); runtime = @($Evidence.runtime); release = @($Evidence.release); requiredCaseIds = @($RequiredCaseIds); completedCaseIds = @($CompletedCaseIds) }
        verifier = [ordered]@{ verifierId = $VerifierId; verifierDefinitionHash = $VerifierDefinitionHash.ToLowerInvariant(); role = $VerifierRole; independenceProof = $IndependenceProof; issuerRef = if ($IssuerRef) { $IssuerRef } else { $null }; signedPayloadHash = if ($SignedPayloadHash) { $SignedPayloadHash.ToLowerInvariant() } else { $null }; signatureStatus = $SignatureStatus; signatureVerificationMethod = $SignatureVerificationMethod; signatureRef = if ($SignatureRef) { $SignatureRef } else { $null }; signatureHash = if ($SignatureHash) { $SignatureHash.ToLowerInvariant() } else { $null }; signedAtUtc = if ($SignedAtUtc) { $SignedAtUtc } else { $null } }
        claims = [ordered]@{ proven = @(); notProven = @('runtime-behavior-unproven','external-authority-unproven'); scope = $Profile }
        assessmentHash = $null; nonClaims = @('assessment-is-not-a-certificate','no-automatic-release-or-knowledge-promotion','hashes-are-integrity-evidence-not-signatures')
    }
    if ($SnapshotArtifactPath) { $assessment.sourceSnapshot.snapshotArtifactPath = $SnapshotArtifactPath.Replace('\','/'); $assessment.sourceSnapshot.snapshotArtifactHash = if ($SnapshotArtifactHash) { $SnapshotArtifactHash.ToLowerInvariant() } else { $null } }
    $result = [pscustomobject]$assessment; $result.assessmentHash = Get-ESABCDCertificationHash (Get-ESABCDCertificationHashInput $result); return $result
}

function Test-ESABCDCertificationAssessment {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Assessment,[Parameter(Mandatory)][string]$ProjectRoot,[switch]$RequireIndependent)
    $issues = [Collections.Generic.List[string]]::new()
    if ([string]$Assessment.contractId -cne 'es://automation/contracts/abcd/certification-assessment/v1') { [void]$issues.Add('CONTRACT_MISMATCH') }
    try { Assert-ESABCDCertificationHash ([string]$Assessment.assessmentHash) 'AssessmentHash'; if ((Get-ESABCDCertificationHash (Get-ESABCDCertificationHashInput $Assessment)) -cne [string]$Assessment.assessmentHash) { [void]$issues.Add('ASSESSMENT_HASH_MISMATCH') } } catch { [void]$issues.Add($_.Exception.Message) }
    $root = (Resolve-Path -LiteralPath $ProjectRoot).Path
    $gitSnapshot = Get-ESABCDCurrentGitSnapshot $root
    $snapshot = $Assessment.sourceSnapshot
    if ($null -ne $gitSnapshot) {
        if ([string]$snapshot.branch -cne [string]$gitSnapshot.branch) { [void]$issues.Add('SOURCE_SNAPSHOT_BRANCH_DRIFT') }
        if ([string]$snapshot.head -cne [string]$gitSnapshot.head) { [void]$issues.Add('SOURCE_SNAPSHOT_HEAD_DRIFT') }
        if ([string]$snapshot.worktreeHash -cne [string]$gitSnapshot.worktreeHash) { [void]$issues.Add('SOURCE_SNAPSHOT_WORKTREE_DRIFT') }
    } else { [void]$issues.Add('SOURCE_SNAPSHOT_UNVERIFIABLE') }
    if ($null -ne $snapshot.PSObject.Properties['snapshotArtifactPath'] -and -not [string]::IsNullOrWhiteSpace([string]$snapshot.snapshotArtifactPath)) {
        $artifactPath = [string]$snapshot.snapshotArtifactPath
        if ([IO.Path]::IsPathRooted($artifactPath) -or $artifactPath -match '(^|[/\\])\.\.([/\\]|$)') { [void]$issues.Add('SNAPSHOT_ARTIFACT_PATH_INVALID') }
        else { $artifactFull = Join-Path $root $artifactPath; if (-not (Test-Path -LiteralPath $artifactFull -PathType Leaf)) { [void]$issues.Add('SNAPSHOT_ARTIFACT_MISSING') } elseif ([string]$snapshot.snapshotArtifactHash -notmatch '^[a-f0-9]{64}$' -or (Get-FileHash -LiteralPath $artifactFull -Algorithm SHA256).Hash.ToLowerInvariant() -cne [string]$snapshot.snapshotArtifactHash) { [void]$issues.Add('SNAPSHOT_ARTIFACT_DRIFT') } }
    }
    foreach ($s in @($Assessment.sourceSnapshot.sourceRefs)) {
        $path = [string]$s.path; if ([IO.Path]::IsPathRooted($path) -or $path -match '(^|[/\\])\.\.([/\\]|$)') { [void]$issues.Add("SOURCE_PATH_INVALID:$path"); continue }
        $full = Join-Path $root $path; if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { [void]$issues.Add("SOURCE_MISSING:$path"); continue }
        $actual = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant(); if ($actual -cne [string]$s.sha256) { [void]$issues.Add("SOURCE_DRIFT:$path") }
    }
    $required = @($Assessment.evidence.requiredCaseIds); $completed = @($Assessment.evidence.completedCaseIds); foreach ($c in $required) { if ($completed -notcontains $c) { [void]$issues.Add("CASE_MISSING:$c") } }
    foreach ($bucket in @('static','runtime','release')) {
        foreach ($e in @($Assessment.evidence.$bucket)) {
            Test-ESABCDCertificationEvidenceRef $root $e $bucket $issues
        }
    }
    $evidenceCaseIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($bucket in @('static','runtime','release')) {
        foreach ($e in @($Assessment.evidence.$bucket)) {
            $caseValue = [string](Get-ESABCDCertificationProperty $e 'case')
            if (-not [string]::IsNullOrWhiteSpace($caseValue)) { [void]$evidenceCaseIds.Add($caseValue) }
        }
    }
    foreach ($caseId in $required) {
        if (-not $evidenceCaseIds.Contains([string]$caseId)) { [void]$issues.Add("CASE_EVIDENCE_MISSING:$caseId") }
    }
    $profile = [string]$Assessment.profile; $runtimeCount = @($Assessment.evidence.runtime).Count; $releaseCount = @($Assessment.evidence.release).Count
    if ($profile -in @('RuntimeAcceptance','ReleaseAcceptance') -and $runtimeCount -eq 0) { [void]$issues.Add('RUNTIME_EVIDENCE_REQUIRED') }
    if ($profile -eq 'ReleaseAcceptance' -and $releaseCount -eq 0) { [void]$issues.Add('RELEASE_EVIDENCE_REQUIRED') }
    $role = [string]$Assessment.verifier.role; $sig = [string]$Assessment.verifier.signatureStatus; $sigMethod = [string]$Assessment.verifier.signatureVerificationMethod
    if ($RequireIndependent -and $role -notin @('independent-auditor','external-certification-body')) { [void]$issues.Add('INDEPENDENT_VERIFIER_REQUIRED') }
    $sourcePaths = @($Assessment.sourceSnapshot.sourceRefs | ForEach-Object {
        try { ConvertTo-ESABCDCertificationProjectRelativePath $root ([string]$_.path) } catch { [string]$_.path }
    })
    $evidencePaths = @('static','runtime','release' | ForEach-Object {
        $bucketName = $_
        foreach ($e in @($Assessment.evidence.$bucketName)) {
            try { ConvertTo-ESABCDCertificationProjectRelativePath $root ([string]$e.path) } catch { [string]$e.path }
        }
    })
    $signatureRelative = $null
    $signatureRefValue = [string](Get-ESABCDCertificationProperty $Assessment.verifier 'signatureRef')
    if (-not [string]::IsNullOrWhiteSpace($signatureRefValue)) {
        try { $signatureRelative = ConvertTo-ESABCDCertificationProjectRelativePath $root $signatureRefValue }
        catch { $signatureRelative = $null; [void]$issues.Add('SIGNATURE_PATH_INVALID') }
        if ($signatureRelative -and ($sourcePaths -contains $signatureRelative)) { [void]$issues.Add('SIGNATURE_REF_OVERLAPS_SOURCE') }
        if ($signatureRelative -and ($evidencePaths -contains $signatureRelative)) { [void]$issues.Add('SIGNATURE_REF_OVERLAPS_EVIDENCE') }
    }
    if ($sig -eq 'failed') { [void]$issues.Add('SIGNATURE_FAILED') }
    if ($sig -eq 'verified') {
        # This module does not contain a cryptographic detached-signature verifier.
        # Never promote metadata + a file hash to an external certification.
        [void]$issues.Add('CRYPTOGRAPHIC_SIGNATURE_VERIFICATION_REQUIRED')
        if ($sigMethod -ne 'external-detached-signature') { [void]$issues.Add('EXTERNAL_SIGNATURE_VERIFICATION_REQUIRED') }
        $signatureHashValue = [string](Get-ESABCDCertificationProperty $Assessment.verifier 'signatureHash')
        if ($signatureHashValue -notmatch '^[a-f0-9]{64}$') { [void]$issues.Add('SIGNATURE_HASH_REQUIRED') }
        if ([string]::IsNullOrWhiteSpace($signatureRefValue)) { [void]$issues.Add('SIGNATURE_REF_REQUIRED') }
        elseif ([IO.Path]::IsPathRooted($signatureRefValue) -or $signatureRefValue -match '(^|[/\\])\.\.([/\\]|$)') { [void]$issues.Add('SIGNATURE_PATH_INVALID') }
        elseif ($signatureRelative -and (($sourcePaths -contains $signatureRelative) -or ($evidencePaths -contains $signatureRelative))) { [void]$issues.Add('SIGNATURE_REF_NOT_SIGNATURE_ARTIFACT') }
        else { $sigFull = Join-Path $root $signatureRefValue; if (-not (Test-Path -LiteralPath $sigFull -PathType Leaf)) { [void]$issues.Add('SIGNATURE_ARTIFACT_MISSING') } elseif ((Get-FileHash -LiteralPath $sigFull -Algorithm SHA256).Hash.ToLowerInvariant() -cne $signatureHashValue) { [void]$issues.Add('SIGNATURE_ARTIFACT_DRIFT') } }
        if ([string](Get-ESABCDCertificationProperty $Assessment.verifier 'issuerRef') -notin @('esframework-trust-root-v1','esframework-certification-authority-v1')) { [void]$issues.Add('TRUSTED_ISSUER_REQUIRED') }
        $signedAtValue = [string](Get-ESABCDCertificationProperty $Assessment.verifier 'signedAtUtc')
        try {
            $signedAt = [DateTime]::Parse($signedAtValue, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime()
            if ($signedAt -gt [DateTime]::UtcNow.AddMinutes(5)) { [void]$issues.Add('SIGNATURE_FROM_FUTURE') }
            if (([DateTime]::UtcNow - $signedAt).TotalHours -gt 168) { [void]$issues.Add('SIGNATURE_STALE') }
        } catch { [void]$issues.Add('SIGNATURE_TIME_INVALID') }
        $signedPayloadHashValue = [string](Get-ESABCDCertificationProperty $Assessment.verifier 'signedPayloadHash')
        if ($signedPayloadHashValue -notmatch '^[a-f0-9]{64}$' -or $signedPayloadHashValue -cne (Get-ESABCDCertificationSignedPayloadHash $Assessment)) { [void]$issues.Add('SIGNED_PAYLOAD_COVERAGE_MISMATCH') }
    }
    $independent = $role -in @('independent-auditor','external-certification-body')
    $signatureVerified = $sig -eq 'verified' -and $sigMethod -eq 'external-detached-signature' -and $issues.Count -eq 0 -and -not [string]::IsNullOrWhiteSpace([string]$Assessment.verifier.signedAtUtc)
    $eligible = $issues.Count -eq 0 -and $independent -and $signatureVerified -and (($profile -eq 'DesignReview') -or ($runtimeCount -gt 0)) -and (($profile -ne 'ReleaseAcceptance') -or ($releaseCount -gt 0))
    $status = if ($issues.Count -gt 0) { 'rejected' } elseif (-not $independent -or -not $signatureVerified) { 'conditional' } else { 'eligible' }
    [pscustomobject][ordered]@{ assessmentId = [string]$Assessment.assessmentId; status = $status; eligibleForCertification = $eligible; decisionStatus = if ($eligible) { 'accepted' } elseif ($status -eq 'conditional') { 'conditional' } else { 'evidence-pending' }; issueCount = $issues.Count; issues = @($issues); profile = $profile; verifierRole = $role; signatureStatus = $sig; nonClaims = @('This assessment is not an external certificate.', 'It does not prove Unity, Worker, host or release behavior without corresponding evidence.') }
}

Export-ModuleMember -Function New-ESABCDCertificationAssessment,Test-ESABCDCertificationAssessment,Get-ESABCDCertificationHash,Get-ESABCDCertificationHashInput,Get-ESABCDCurrentGitSnapshot,Get-ESABCDCertificationSignedPayloadHash
