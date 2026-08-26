Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:TaskStatuses = @('Active','Suspended','Completed','Cancelled','Blocked','Invalidated')
$script:ContextStatuses = @('Live','Compacting','PartiallyInvalidated','Frozen','Archived','Expired','Quarantined')
$script:CompletionDecisions = @('accepted','rejected','undetermined')
$script:DeliveryAcceptances = @('accepted','pending','rejected')
$script:Hex64Pattern = '^[a-f0-9]{64}$'
$script:SafeIdPattern = '^[A-Za-z0-9][A-Za-z0-9._-]{0,80}$'
$script:EvidenceVerifierRegistryPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Contracts\es-evidence-verifier.registry.json'))
$script:PlatformEvidenceContractPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Contracts\es-platform-evidence-v1.schema.json'))
$script:OutcomeEvaluatorRegistryPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Contracts\es-outcome-evaluator.registry.json'))
$script:EvaluationRecordContractPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Contracts\es-evaluation-record-v1.schema.json'))
$script:JsonSchemaLitePath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Contracts\ESJsonSchemaLite.psm1'))
$script:RoutePlanModulePath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\RoutePlan\ESRoutePlanContract.psm1'))
Import-Module $script:JsonSchemaLitePath -ErrorAction Stop
Import-Module $script:RoutePlanModulePath -ErrorAction Stop

function ConvertTo-ESCanonicalJson {
    param([AllowNull()]$Value)
    if ($null -eq $Value) { return 'null' }
    if ($Value -is [string] -or $Value -is [char]) { return ([string]$Value | ConvertTo-Json -Compress) }
    if ($Value -is [bool]) { if ($Value) { return 'true' } else { return 'false' } }
    if ($Value -is [datetime]) { return ($Value.ToUniversalTime().ToString('o') | ConvertTo-Json -Compress) }
    if ($Value -is [System.Collections.IDictionary]) {
        $parts = foreach ($key in @($Value.Keys | ForEach-Object { [string]$_ } | Sort-Object -CaseSensitive)) {
            '{0}:{1}' -f ($key | ConvertTo-Json -Compress), (ConvertTo-ESCanonicalJson $Value[$key])
        }
        return '{' + ($parts -join ',') + '}'
    }
    if ($Value -is [pscustomobject]) {
        $parts = foreach ($property in @($Value.PSObject.Properties | Sort-Object Name -CaseSensitive)) {
            '{0}:{1}' -f ($property.Name | ConvertTo-Json -Compress), (ConvertTo-ESCanonicalJson $property.Value)
        }
        return '{' + ($parts -join ',') + '}'
    }
    if ($Value -is [System.Collections.IEnumerable] -and -not ($Value -is [string])) {
        $parts = foreach ($item in $Value) { ConvertTo-ESCanonicalJson $item }
        return '[' + ($parts -join ',') + ']'
    }
    if ($Value -is [System.IFormattable]) { return $Value.ToString($null, [Globalization.CultureInfo]::InvariantCulture) }
    return ([string]$Value | ConvertTo-Json -Compress)
}

function Get-ESObjectHash {
    param([AllowNull()]$Value)
    $bytes = [Text.Encoding]::UTF8.GetBytes((ConvertTo-ESCanonicalJson $Value))
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Read-ESStrictJson {
    param([Parameter(Mandatory=$true)][string]$Path)
    try {
        $raw = [Text.UTF8Encoding]::new($false, $true).GetString([IO.File]::ReadAllBytes($Path))
        return $raw | ConvertFrom-Json -ErrorAction Stop
    } catch { throw "Invalid strict UTF-8 JSON: $Path. $($_.Exception.Message)" }
}

function Write-ESCreateOnlyJson {
    param([Parameter(Mandatory=$true)][string]$Path, [Parameter(Mandatory=$true)]$Value)
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    $json = $Value | ConvertTo-Json -Depth 40
    $stream = $null
    $writer = $null
    try {
        $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        $writer = [IO.StreamWriter]::new($stream, [Text.UTF8Encoding]::new($false))
        $writer.Write($json)
        $writer.Flush()
    } finally {
        if ($null -ne $writer) { $writer.Dispose() } elseif ($null -ne $stream) { $stream.Dispose() }
    }
}

function Assert-ESSafeId {
    param([string]$Value, [string]$Name)
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch $script:SafeIdPattern) { throw "$Name is not a safe bounded identifier." }
}

function Assert-ESHash {
    param([string]$Value, [string]$Name)
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -cnotmatch $script:Hex64Pattern) { throw "$Name must be a lowercase SHA-256 hash." }
}

function Assert-ESObjectShape {
    param($Value, [string[]]$Required, [string[]]$Allowed, [string]$Name)
    if ($null -eq $Value -or ($Value -isnot [pscustomobject] -and $Value -isnot [Collections.IDictionary])) { throw "$Name must be an object." }
    $properties = if ($Value -is [Collections.IDictionary]) { @($Value.Keys | ForEach-Object { [string]$_ }) } else { @($Value.PSObject.Properties | ForEach-Object { [string]$_.Name }) }
    foreach ($property in $Required) { if ($properties -cnotcontains $property) { throw "$Name is missing required property: $property" } }
    foreach ($property in $properties) { if ($Allowed -cnotcontains $property) { throw "$Name contains an unsupported property: $property" } }
}

function Get-ESPlatformEvidenceContractSnapshot {
    if (-not (Test-Path -LiteralPath $script:PlatformEvidenceContractPath -PathType Leaf)) { throw 'Platform Evidence contract is missing.' }
    $contract = Read-ESStrictJson $script:PlatformEvidenceContractPath
    $contractId = 'es://automation/contracts/platform-evidence/v1'
    if ([string]$contract.'$id' -cne $contractId -or $null -eq $contract.'$defs'.candidateEvidenceSet -or $null -eq $contract.'$defs'.normalizedEvidenceSet) { throw 'Platform Evidence contract identity or required definitions are invalid.' }
    $contractHash = (Get-FileHash -LiteralPath $script:PlatformEvidenceContractPath -Algorithm SHA256).Hash.ToLowerInvariant()
    [pscustomobject]@{ contractId=$contractId; contractHash=$contractHash; path=$script:PlatformEvidenceContractPath }
}

function Get-ESEvaluationRecordContractSnapshot {
    if (-not (Test-Path -LiteralPath $script:EvaluationRecordContractPath -PathType Leaf)) { throw 'EvaluationRecord contract is missing.' }
    $contract = Read-ESStrictJson $script:EvaluationRecordContractPath
    $contractId = 'es://automation/contracts/evaluation-record/v1'
    if ([string]$contract.'$id' -cne $contractId -or $null -eq $contract.'$defs'.evaluationRequest -or $null -eq $contract.'$defs'.evaluationRecord) { throw 'EvaluationRecord contract identity or required definitions are invalid.' }
    [pscustomobject]@{
        contractId = $contractId
        contractHash = (Get-FileHash -LiteralPath $script:EvaluationRecordContractPath -Algorithm SHA256).Hash.ToLowerInvariant()
        path = $script:EvaluationRecordContractPath
    }
}

function Assert-ESNoReparsePointBelowRoot {
    param([string]$Root, [string]$Target, [string]$Label)
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    $targetFull = [IO.Path]::GetFullPath($Target)
    $prefix = $rootFull + [IO.Path]::DirectorySeparatorChar
    if (-not ($targetFull.Equals($rootFull, [StringComparison]::OrdinalIgnoreCase) -or $targetFull.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase))) {
        throw "$Label escapes ProjectRoot."
    }
    if ($targetFull.Equals($rootFull, [StringComparison]::OrdinalIgnoreCase)) { return }
    $relative = $targetFull.Substring($prefix.Length)
    $current = $rootFull
    foreach ($segment in $relative.Split(@([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar), [StringSplitOptions]::RemoveEmptyEntries)) {
        $current = Join-Path $current $segment
        if (-not (Test-Path -LiteralPath $current)) { break }
        $item = Get-Item -LiteralPath $current -Force
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "$Label cannot traverse a reparse point." }
    }
}

function Resolve-ESRuntimePaths {
    param([string]$ProjectRoot, [string]$StoreRoot, [string]$TaskId)
    $project = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path.TrimEnd('\','/')
    if ([IO.Path]::IsPathRooted($StoreRoot) -or $StoreRoot -match '(^|[\/])\.\.([\/]|$)' -or $StoreRoot -match '[*?]') {
        throw 'StoreRoot must be a bounded project-relative path.'
    }
    $store = [IO.Path]::GetFullPath((Join-Path $project $StoreRoot))
    if (-not ($store.Equals($project, [StringComparison]::OrdinalIgnoreCase) -or $store.StartsWith($project + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))) {
        throw 'StoreRoot escapes ProjectRoot.'
    }
    Assert-ESNoReparsePointBelowRoot $project $store 'StoreRoot'
    $taskRoot = if ([string]::IsNullOrWhiteSpace($TaskId)) { $null } else { Join-Path $store $TaskId }
    $eventsRoot = if ($taskRoot) { Join-Path $taskRoot 'events' } else { $null }
    $receiptsRoot = if ($taskRoot) { Join-Path $taskRoot 'receipts' } else { $null }
    $evaluationsRoot = if ($taskRoot) { Join-Path $taskRoot 'evaluations' } else { $null }
    if ($taskRoot) {
        Assert-ESNoReparsePointBelowRoot $project $taskRoot 'TaskRoot'
        Assert-ESNoReparsePointBelowRoot $project $eventsRoot 'EventsRoot'
        Assert-ESNoReparsePointBelowRoot $project $receiptsRoot 'ReceiptsRoot'
        Assert-ESNoReparsePointBelowRoot $project $evaluationsRoot 'EvaluationsRoot'
    }
    [pscustomobject]@{ ProjectRoot=$project; StoreRoot=$store; TaskRoot=$taskRoot; EventsRoot=$eventsRoot; ReceiptsRoot=$receiptsRoot; EvaluationsRoot=$evaluationsRoot }
}

function Resolve-ESProjectFile {
    param([string]$ProjectRoot, [string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path) -or [IO.Path]::IsPathRooted($Path) -or $Path -match '(^|[\/])\.\.([\/]|$)' -or $Path -match '[*?]') {
        throw "Source path must be project-relative and bounded: $Path"
    }
    $full = [IO.Path]::GetFullPath((Join-Path $ProjectRoot ($Path.Replace('/', [IO.Path]::DirectorySeparatorChar))))
    if (-not $full.StartsWith($ProjectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Source path escapes ProjectRoot: $Path" }
    Assert-ESNoReparsePointBelowRoot $ProjectRoot $full 'Source path'
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "Source file is missing: $Path" }
    $item = Get-Item -LiteralPath $full -Force
    if ($item.LinkType) { throw "Source file cannot be a reparse point: $Path" }
    [pscustomobject]@{ Full=$full; Relative=$full.Substring($ProjectRoot.Length).TrimStart('\','/').Replace('\','/') }
}

function Resolve-ESProjectItem {
    param([string]$ProjectRoot,[string]$Path)
    if([string]::IsNullOrWhiteSpace($Path)-or[IO.Path]::IsPathRooted($Path)-or$Path-match'(^|[\/])\.\.([\/]|$)'-or$Path-match'[*?]'){throw "Project item path must be project-relative and bounded: $Path"}
    $full=[IO.Path]::GetFullPath((Join-Path $ProjectRoot ($Path.Replace('/',[IO.Path]::DirectorySeparatorChar))))
    if(-not$full.StartsWith($ProjectRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw "Project item path escapes ProjectRoot: $Path"}
    Assert-ESNoReparsePointBelowRoot $ProjectRoot $full 'Project item path'
    if(-not(Test-Path -LiteralPath $full)){throw "Project item is missing: $Path"}
    $item=Get-Item -LiteralPath $full -Force
    if($item.LinkType-or($item.Attributes-band[IO.FileAttributes]::ReparsePoint)-ne0){throw "Project item cannot be a reparse point: $Path"}
    [pscustomobject]@{Full=$full;Relative=$full.Substring($ProjectRoot.Length).TrimStart('\','/').Replace('\','/');IsFile=[bool](-not$item.PSIsContainer)}
}

function Get-ESFileObservation {
    param([string]$FullPath, [string]$RelativePath)
    $item = Get-Item -LiteralPath $FullPath -Force
    [ordered]@{
        path = $RelativePath
        length = [int64]$item.Length
        sha256 = (Get-FileHash -LiteralPath $FullPath -Algorithm SHA256).Hash.ToLowerInvariant()
        verifiedUtc = [DateTime]::UtcNow.ToString('o')
    }
}

function Get-ESEvidenceVerifierRegistrySnapshot {
    $registry = Read-ESStrictJson $script:EvidenceVerifierRegistryPath
    $projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..')).TrimEnd('\','/')
    $rootFields=@('$schema','$id','schemaVersion','registryId','verifiers')
    Assert-ESObjectShape $registry $rootFields $rootFields 'Evidence verifier registry'
    if ([string]$registry.'$schema' -cne 'https://json-schema.org/draft/2020-12/schema' -or [string]$registry.'$id' -cne 'es://automation/contracts/evidence-verifier-registry/v1' -or [int]$registry.schemaVersion -ne 1 -or [string]$registry.registryId -cne 'es.task-context.evidence-verifiers.v1') { throw 'Evidence verifier registry identity is invalid.' }
    if ($registry.verifiers -isnot [Array] -or @($registry.verifiers).Count -eq 0) { throw 'Evidence verifier registry must contain a non-empty verifier array.' }
    $definitionFields=@('verifierId','authority','artifactFormat','claimIdPattern','requiredArtifactFields','observationFields','outcomePolicy')
    $optionalDefinitionFields=@('implementationPath','implementationHash','maxSourceFiles','maxSourceBytes','maxExecutionSeconds','maxOutputChars','contractPath','contractHash','maxTranscriptBytes','maxSliceLines','maxSliceChars')
    $allowedDefinitionFields=@($definitionFields+$optionalDefinitionFields)
    $ids = @($registry.verifiers | ForEach-Object {
        $verifierDefinition=$_
        Assert-ESObjectShape $_ $definitionFields $allowedDefinitionFields 'Evidence verifier definition'
        Assert-ESSafeId ([string]$_.verifierId) 'VerifierId';Assert-ESSafeId ([string]$_.authority) 'VerifierAuthority'
        if ([string]$_.artifactFormat -cne 'json') { throw "Evidence verifier contract is unsupported: $($_.verifierId)" }
        $policy=[string]$_.outcomePolicy
        if($policy -notin @('failed-if-any-hash-mismatch;unverified-if-empty;passed-if-all-match','failed-if-static-replay-blocked;unverified-if-runner-unavailable;passed-if-static-replay-passed','passed-if-task-slice-verified;unverified-if-followup-missing')){throw "Evidence verifier outcome policy is unsupported: $($_.verifierId)"}
        if ($_.requiredArtifactFields -isnot [Array] -or $_.observationFields -isnot [Array] -or @($_.requiredArtifactFields).Count -eq 0 -or @($_.observationFields).Count -eq 0) { throw "Evidence verifier field lists are invalid: $($_.verifierId)" }
        foreach($fieldList in @(@($_.requiredArtifactFields),@($_.observationFields))){
            foreach($field in $fieldList){if([string]$field -cnotmatch '^[A-Za-z][A-Za-z0-9]{0,63}$'){throw "Evidence verifier field name is invalid: $field"}}
            if(@($fieldList|Sort-Object -Unique).Count-ne@($fieldList).Count){throw "Evidence verifier field list contains duplicates: $($_.verifierId)"}
        }
        $pattern=[string]$_.claimIdPattern
        if(-not($pattern.StartsWith('^',[StringComparison]::Ordinal)-and$pattern.EndsWith('$',[StringComparison]::Ordinal))){throw "Evidence verifier claimIdPattern must be fully anchored: $($_.verifierId)"}
        try{[void][regex]::new($pattern,[Text.RegularExpressions.RegexOptions]::CultureInvariant)}catch{throw "Evidence verifier claimIdPattern is invalid: $($_.verifierId)"}
        if([string]$_.verifierId-ceq'platform.static-replay-v1'){
            foreach($field in @('implementationPath','implementationHash','maxSourceFiles','maxSourceBytes','maxExecutionSeconds','maxOutputChars')){if($null-eq$_.PSObject.Properties[$field]){throw "Static replay verifier field is missing: $field"}}
            Assert-ESHash ([string]$_.implementationHash) 'StaticReplayImplementationHash'
            $implementation=Resolve-ESProjectFile $projectRoot ([string]$_.implementationPath)
            $actualImplementationHash=(Get-FileHash -LiteralPath $implementation.Full -Algorithm SHA256).Hash.ToLowerInvariant()
            if($actualImplementationHash-cne[string]$_.implementationHash){throw 'Static replay verifier implementation hash drifted.'}
            if([int]$_.maxSourceFiles-lt1-or[int]$_.maxSourceFiles-gt4096-or[int64]$_.maxSourceBytes-lt1-or[int64]$_.maxSourceBytes-gt536870912-or[int]$_.maxExecutionSeconds-lt1-or[int]$_.maxExecutionSeconds-gt900-or[int]$_.maxOutputChars-lt4096-or[int]$_.maxOutputChars-gt16777216){throw 'Static replay verifier budget is invalid.'}
        }elseif([string]$_.verifierId-ceq'platform.codex-transcript-slice-v1'){
            foreach($field in @('implementationPath','implementationHash','contractPath','contractHash','maxTranscriptBytes','maxSliceLines','maxSliceChars')){if($null-eq$_.PSObject.Properties[$field]){throw "Transcript slice verifier field is missing: $field"}}
            Assert-ESHash ([string]$_.implementationHash) 'TranscriptVerifierImplementationHash';Assert-ESHash ([string]$_.contractHash) 'TranscriptVerifierContractHash'
            $implementation=Resolve-ESProjectFile $projectRoot ([string]$_.implementationPath)
            $contract=Resolve-ESProjectFile $projectRoot ([string]$_.contractPath)
            if((Get-FileHash -LiteralPath $implementation.Full -Algorithm SHA256).Hash.ToLowerInvariant()-cne[string]$_.implementationHash){throw 'Transcript slice verifier implementation hash drifted.'}
            if((Get-FileHash -LiteralPath $contract.Full -Algorithm SHA256).Hash.ToLowerInvariant()-cne[string]$_.contractHash){throw 'Transcript slice verifier contract hash drifted.'}
            if([int64]$_.maxTranscriptBytes-lt1024-or[int64]$_.maxTranscriptBytes-gt104857600-or[int]$_.maxSliceLines-lt2-or[int]$_.maxSliceLines-gt4096-or[int]$_.maxSliceChars-lt1024-or[int]$_.maxSliceChars-gt16777216){throw 'Transcript slice verifier budget is invalid.'}
        }elseif(@($optionalDefinitionFields|Where-Object{$null-ne$verifierDefinition.PSObject.Properties[$_]}).Count){throw "Built-in evidence verifier cannot declare external implementation fields: $($_.verifierId)"}
        [string]$_.verifierId
    })
    if (@($ids | Sort-Object -Unique).Count -ne $ids.Count) { throw 'Evidence verifier registry contains duplicate verifierId values.' }
    return [pscustomobject]@{ registry=$registry; registryHash=(Get-ESObjectHash $registry) }
}

function Get-ESEvidenceVerifierDefinition {
    param([string]$VerifierId)
    $snapshot = Get-ESEvidenceVerifierRegistrySnapshot
    $definition = @($snapshot.registry.verifiers | Where-Object { [string]$_.verifierId -ceq $VerifierId })
    if ($definition.Count -ne 1) { throw "Evidence verifier is not registered exactly once: $VerifierId" }
    return [pscustomobject]@{ definition=$definition[0]; definitionHash=(Get-ESObjectHash $definition[0]); registryHash=$snapshot.registryHash }
}

function Get-ESOutcomeEvaluatorRegistrySnapshot {
    if (-not (Test-Path -LiteralPath $script:OutcomeEvaluatorRegistryPath -PathType Leaf)) { throw 'Outcome evaluator registry is missing.' }
    $registry = Read-ESStrictJson $script:OutcomeEvaluatorRegistryPath
    $rootFields = @('$schema','$id','schemaVersion','registryId','evaluators')
    Assert-ESObjectShape $registry $rootFields $rootFields 'Outcome evaluator registry'
    if ([string]$registry.'$schema' -cne 'https://json-schema.org/draft/2020-12/schema' -or [string]$registry.'$id' -cne 'es://automation/contracts/outcome-evaluator-registry/v1' -or [int]$registry.schemaVersion -ne 1 -or [string]$registry.registryId -cne 'es.task-context.outcome-evaluators.v1') { throw 'Outcome evaluator registry identity is invalid.' }
    if ($registry.evaluators -isnot [Array] -or @($registry.evaluators).Count -eq 0) { throw 'Outcome evaluator registry must contain a non-empty evaluator array.' }
    $definitionFields = @('evaluatorId','authority','algorithmVersion','profileIdPattern','decisionPolicy','scopeType','recordContractId','acceptedRequires')
    $ids = @($registry.evaluators | ForEach-Object {
        Assert-ESObjectShape $_ $definitionFields $definitionFields 'Outcome evaluator definition'
        Assert-ESSafeId ([string]$_.evaluatorId) 'OutcomeEvaluatorId'
        if ([string]$_.authority -cne 'TaskContextRuntime' -or [string]$_.algorithmVersion -cne 'task-context-outcome-v1' -or [string]$_.decisionPolicy -cne 'rejected-if-authoritative-required-failure;undetermined-if-gap-or-drift;accepted-if-all-required-verified' -or [string]$_.scopeType -cne 'task-object' -or [string]$_.recordContractId -cne 'es://automation/contracts/evaluation-record/v1') { throw "Outcome evaluator contract is unsupported: $($_.evaluatorId)" }
        $pattern = [string]$_.profileIdPattern
        if (-not ($pattern.StartsWith('^',[StringComparison]::Ordinal) -and $pattern.EndsWith('$',[StringComparison]::Ordinal))) { throw "Outcome evaluator profileIdPattern must be fully anchored: $($_.evaluatorId)" }
        try { [void][regex]::new($pattern,[Text.RegularExpressions.RegexOptions]::CultureInvariant) } catch { throw "Outcome evaluator profileIdPattern is invalid: $($_.evaluatorId)" }
        if ($_.acceptedRequires -isnot [Array] -or @($_.acceptedRequires).Count -eq 0 -or @($_.acceptedRequires | Sort-Object -Unique).Count -ne @($_.acceptedRequires).Count) { throw "Outcome evaluator acceptedRequires is invalid: $($_.evaluatorId)" }
        foreach ($requirement in @($_.acceptedRequires)) { if ([string]::IsNullOrWhiteSpace([string]$requirement)) { throw "Outcome evaluator acceptedRequires contains an empty value: $($_.evaluatorId)" } }
        [string]$_.evaluatorId
    })
    if (@($ids | Sort-Object -Unique).Count -ne $ids.Count) { throw 'Outcome evaluator registry contains duplicate evaluatorId values.' }
    [pscustomobject]@{ registry=$registry; registryHash=(Get-ESObjectHash $registry) }
}

function Get-ESOutcomeEvaluatorDefinition {
    param([string]$EvaluatorId)
    $snapshot = Get-ESOutcomeEvaluatorRegistrySnapshot
    $definition = @($snapshot.registry.evaluators | Where-Object { [string]$_.evaluatorId -ceq $EvaluatorId })
    if ($definition.Count -ne 1) { throw "Outcome evaluator is not registered exactly once: $EvaluatorId" }
    [pscustomobject]@{ definition=$definition[0]; definitionHash=(Get-ESObjectHash $definition[0]); registryHash=$snapshot.registryHash }
}

function Invoke-ESStaticReplayEvidenceVerifier {
    param(
        [string]$ProjectRoot,
        $Artifact,
        $Definition,
        [string]$VerifierDefinitionHash,
        [string[]]$ExpectedSourcePaths,
        [string]$ArtifactHash,
        [string]$ArtifactPath
    )
    $skillName=[string]$Artifact.skillName
    if($skillName-cnotmatch'^es-[a-z0-9]+(?:-[a-z0-9]+)*$'){throw 'Static replay evidence skillName is invalid.'}
    $expectedManifestPath='.agents/skills/'+$skillName+'/static-replay.manifest.json'
    if([string]$Artifact.manifestPath-cne$expectedManifestPath){throw 'Static replay evidence manifestPath is not canonical for skillName.'}
    $manifestItem=Resolve-ESProjectFile $ProjectRoot $expectedManifestPath
    $manifest=Read-ESStrictJson $manifestItem.Full
    if([string]$manifest.skillName -cne $skillName -or $manifest.sourceRoots -isnot [Array] -or @($manifest.sourceRoots).Count -eq 0){throw 'Static replay manifest identity or sourceRoots are invalid.'}

    $projectedFiles=[Collections.Generic.List[object]]::new()
    foreach($sourceRoot in @($manifest.sourceRoots)){
        $item=Resolve-ESProjectItem $ProjectRoot ([string]$sourceRoot)
        if($item.IsFile){[void]$projectedFiles.Add((Get-Item -LiteralPath $item.Full -Force))}
        else{foreach($file in @(Get-ChildItem -LiteralPath $item.Full -Recurse -File|Where-Object{$_.FullName -notmatch '[\\/]__pycache__[\\/]' -and $_.Extension -notin @('.pyc','.pyo')})){Assert-ESNoReparsePointBelowRoot $ProjectRoot $file.FullName 'Static replay source';if($file.LinkType -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Static replay source cannot be a reparse point.'};[void]$projectedFiles.Add($file)}}
    }
    foreach($authorityPath in @('ES/Automation/Contracts/es-skill-evidence-receipt-v1.schema.json','.agents/skills/es-static-deep-replay/references/specialized-acceptance-registry.json')){
        $authorityFull=[IO.Path]::GetFullPath((Join-Path $ProjectRoot $authorityPath.Replace('/',[IO.Path]::DirectorySeparatorChar)))
        if(Test-Path -LiteralPath $authorityFull -PathType Leaf){[void]$projectedFiles.Add((Get-Item -LiteralPath $authorityFull -Force))}
    }
    $projected=@($projectedFiles|Sort-Object FullName -Unique|ForEach-Object{[pscustomobject]@{path=$_.FullName.Substring($ProjectRoot.Length).TrimStart('\','/').Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant();length=[int64]$_.Length}})
    if($projected.Count -gt [int]$Definition.maxSourceFiles){throw 'Static replay evidence exceeds maxSourceFiles before verifier execution.'}
    $totalBytes=[int64](($projected|Measure-Object -Property length -Sum).Sum)
    if($totalBytes -gt [int64]$Definition.maxSourceBytes){throw 'Static replay evidence exceeds maxSourceBytes before verifier execution.'}
    foreach($source in $projected){if(@($ExpectedSourcePaths|Where-Object{[string]$_ -ceq [string]$source.path}).Count -ne 1){throw "Static replay source is outside the verified sourceScope: $($source.path)"}}

    $implementation=Resolve-ESProjectFile $ProjectRoot ([string]$Definition.implementationPath)
    $actualImplementationHash=(Get-FileHash -LiteralPath $implementation.Full -Algorithm SHA256).Hash.ToLowerInvariant()
    if($actualImplementationHash-cne[string]$Definition.implementationHash){throw 'Static replay verifier implementation hash drifted before execution.'}
    $scratchRelative='ES/Output/TaskContextRuntime/VerifierScratch/static-replay-'+[Guid]::NewGuid().ToString('N')+'.json'
    $scratchFull=[IO.Path]::GetFullPath((Join-Path $ProjectRoot $scratchRelative.Replace('/',[IO.Path]::DirectorySeparatorChar)))
    $scratchRoot=Split-Path -Parent $scratchFull
    $report=$null
    $process=$null
    try{
        if(-not(Test-Path -LiteralPath $scratchRoot)){New-Item -ItemType Directory -Path $scratchRoot -Force|Out-Null}
        Assert-ESNoReparsePointBelowRoot $ProjectRoot $scratchRoot 'Static replay verifier scratch'
        $powershell=Join-Path $PSHOME 'powershell.exe'
        if(-not(Test-Path -LiteralPath $powershell -PathType Leaf)){throw 'Static replay verifier PowerShell host is unavailable.'}
        $implementationLiteral="'"+$implementation.Full.Replace("'","''")+"'"
        $projectRootLiteral="'"+$ProjectRoot.Replace("'","''")+"'"
        $manifestLiteral="'"+$expectedManifestPath.Replace("'","''")+"'"
        $reportLiteral="'"+$scratchRelative.Replace("'","''")+"'"
        $encodedCommand=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes("& $implementationLiteral -ProjectRoot $projectRootLiteral -ManifestPath $manifestLiteral -ReportPath $reportLiteral"))
        $startInfo=[Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName=$powershell
        $startInfo.Arguments='-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand '+$encodedCommand
        $startInfo.UseShellExecute=$false
        $startInfo.CreateNoWindow=$true
        $startInfo.RedirectStandardOutput=$true
        $startInfo.RedirectStandardError=$true
        $startInfo.StandardOutputEncoding=[Text.UTF8Encoding]::new($false)
        $startInfo.StandardErrorEncoding=[Text.UTF8Encoding]::new($false)
        $process=[Diagnostics.Process]::new()
        $process.StartInfo=$startInfo
        if(-not$process.Start()){throw 'Static replay verifier process did not start.'}
        $stdoutTask=$process.StandardOutput.ReadToEndAsync()
        $stderrTask=$process.StandardError.ReadToEndAsync()
        $timeoutMs=[int]$Definition.maxExecutionSeconds*1000
        if(-not$process.WaitForExit($timeoutMs)){
            $terminated=$false
            $taskkill=Join-Path $env:SystemRoot 'System32\taskkill.exe'
            if(Test-Path -LiteralPath $taskkill -PathType Leaf){try{& $taskkill /PID $process.Id /T /F|Out-Null;$terminated=$process.WaitForExit(5000)}catch{$terminated=$false}}
            if(-not$terminated){try{$process.Kill();$terminated=$process.WaitForExit(5000)}catch{$terminated=$false}}
            if(-not$terminated){throw 'Static replay verifier timed out and process termination was not confirmed.'}
            throw 'Static replay verifier timed out.'
        }
        $process.WaitForExit()
        if(-not$stdoutTask.Wait(5000)-or-not$stderrTask.Wait(5000)){throw 'Static replay verifier output drain did not complete.'}
        $runnerOutputText=[string]$stdoutTask.Result
        $runnerErrorText=[string]$stderrTask.Result
        if(($runnerOutputText.Length+$runnerErrorText.Length)-gt[int]$Definition.maxOutputChars){throw 'Static replay verifier output exceeded maxOutputChars.'}
        $runnerOutput=@(($runnerOutputText+"`n"+$runnerErrorText)-split'\r?\n'|Where-Object{$_})
        $runnerExitCode=$process.ExitCode
        if($runnerExitCode -notin @(0,1) -or -not (Test-Path -LiteralPath $scratchFull -PathType Leaf)){throw ('Static replay verifier did not produce a bounded report. ExitCode='+$runnerExitCode+' Output='+(@($runnerOutput|Select-Object -Last 3)-join' | '))}
        $report=Read-ESStrictJson $scratchFull
    }finally{
        if($null-ne$process){if(-not$process.HasExited){try{$process.Kill();[void]$process.WaitForExit(5000)}catch{}};$process.Dispose()}
        if(Test-Path -LiteralPath $scratchFull -PathType Leaf){Remove-Item -LiteralPath $scratchFull -Force -ErrorAction Stop}
        if((Test-Path -LiteralPath $scratchRoot -PathType Container) -and @(Get-ChildItem -LiteralPath $scratchRoot -Force).Count -eq 0){Remove-Item -LiteralPath $scratchRoot -Force -ErrorAction Stop}
    }
    if([string]$report.skillName-cne$skillName-or[string]$report.case-cne'StaticDeepReplay'-or[string]$report.profile-cne'StaticReview'){throw 'Static replay verifier report identity is invalid.'}
    $reportRefs=@($report.sourceRefs|ForEach-Object{[string]$_})
    $projectedRefs=@($projected|ForEach-Object{[string]$_.path})
    if(($reportRefs-join'|')-cne($projectedRefs-join'|')){throw 'Static replay verifier source projection differs from the preflight snapshot.'}
    foreach($source in $projected){$property=$report.sourceRefHashes.PSObject.Properties[[string]$source.path];if($null-eq$property-or[string]$property.Value-cne[string]$source.sha256){throw "Static replay verifier source hash mismatch: $($source.path)"}}
    $derivedOutcome=if([string]$report.status -ceq 'passed' -and [string]$report.staticStatus -ceq 'static-passed' -and @($report.issues).Count -eq 0){'passed'}elseif([string]$report.status -in @('blocked','failed') -or [string]$report.staticStatus -ceq 'static-blocked'){'failed'}else{'unverified'}
    $evidenceHash=Get-ESObjectHash ([ordered]@{verifierDefinitionHash=$VerifierDefinitionHash;planHash=[string]$report.planHash;sourceRefHashes=$report.sourceRefHashes;status=[string]$report.status;staticStatus=[string]$report.staticStatus;cases=@($report.cases);customCheckResults=@($report.customCheckResults);specializedAcceptance=$report.specializedAcceptance})
    [pscustomobject][ordered]@{outcome=$derivedOutcome;evidenceHash=$evidenceHash;artifactHash=$ArtifactHash;verifierId=[string]$Definition.verifierId;verifierDefinitionHash=$VerifierDefinitionHash;verificationStatus='verified';artifactPath=$ArtifactPath}
}

function ConvertTo-ESVerifiedArtifact {
    param([string]$ProjectRoot, [string]$ArtifactPath, [string]$ClaimId, [string]$CandidateOutcome, [string]$CandidateEvidenceHash, [string]$ExpectedSourceScopeHash, [string[]]$ExpectedSourcePaths, [string]$VerifierId, [string]$ExpectedVerifierDefinitionHash, $ExpectedTask)
    $verifierSnapshot = Get-ESEvidenceVerifierDefinition $VerifierId
    if ([string]$verifierSnapshot.definitionHash -cne $ExpectedVerifierDefinitionHash) { throw "Evidence verifier definition drifted: $VerifierId" }
    $definition = $verifierSnapshot.definition
    if([string]$definition.verifierId -notin @('platform.file-hash-manifest-v1','platform.static-replay-v1','platform.codex-transcript-slice-v1')){throw "Unsupported evidence verifier contract: $VerifierId"}
    if ([string]::IsNullOrWhiteSpace([string]$definition.claimIdPattern) -or $ClaimId -cnotmatch [string]$definition.claimIdPattern) { throw "Evidence verifier does not support claimId: $ClaimId" }
    $resolved = Resolve-ESProjectFile $ProjectRoot $ArtifactPath
    $artifactHash = (Get-FileHash -LiteralPath $resolved.Full -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-ESHash $CandidateEvidenceHash 'CandidateEvidenceHash'
    if ($CandidateEvidenceHash -cne $artifactHash) { throw "Evidence artifact hash mismatch: $ArtifactPath" }
    $artifact = Read-ESStrictJson $resolved.Full
    if ([int]$artifact.schemaVersion -ne 1 -or [string]$artifact.claimId -cne $ClaimId) { throw "Evidence artifact identity mismatch: $ArtifactPath" }
    if ([string]$artifact.sourceScopeHash -cne $ExpectedSourceScopeHash) { throw "Evidence artifact sourceScope mismatch: $ArtifactPath" }
    $allowedArtifactFields = @($definition.requiredArtifactFields | ForEach-Object { [string]$_ })
    foreach ($field in $allowedArtifactFields) { if ($null -eq $artifact.PSObject.Properties[$field]) { throw "Evidence artifact field is missing: $field" } }
    foreach ($property in $artifact.PSObject.Properties) { if ($allowedArtifactFields -cnotcontains [string]$property.Name) { throw "Evidence artifact field is unsupported: $($property.Name)" } }
    if([string]$definition.verifierId-ceq'platform.static-replay-v1'){
        $verified=Invoke-ESStaticReplayEvidenceVerifier $ProjectRoot $artifact $definition ([string]$verifierSnapshot.definitionHash) $ExpectedSourcePaths $artifactHash $resolved.Relative
        if([string]$verified.outcome-cne$CandidateOutcome){throw "Candidate outcome does not match platform-derived artifact outcome: $ArtifactPath"}
        return $verified
    }
    if([string]$definition.verifierId-ceq'platform.codex-transcript-slice-v1'){
        if($null-eq$ExpectedTask){throw 'Transcript slice verifier requires a frozen task binding.'}
        $contract=Resolve-ESProjectFile $ProjectRoot ([string]$definition.contractPath)
        $schemaErrors=@(Test-ESJsonSchemaValue -SchemaPath $contract.Full -Value $artifact)
        if($schemaErrors.Count){throw ('Task transcript slice schema validation failed: '+($schemaErrors-join'; '))}
        $implementation=Resolve-ESProjectFile $ProjectRoot ([string]$definition.implementationPath)
        $modules=@(Import-Module $implementation.Full -Force -PassThru -ErrorAction Stop)
        try{
            $module=@($modules|Where-Object{[IO.Path]::GetFullPath([string]$_.Path)-ceq[IO.Path]::GetFullPath($implementation.Full)})|Select-Object -Last 1
            if($null-eq$module){throw 'Transcript slice verifier implementation did not load.'}
            $observation=&$module{param($value,$task,$maxBytes,$maxLines,$maxChars)Get-ESTaskTranscriptCorrectionObservation -Artifact $value -ExpectedTask $task -MaxTranscriptBytes $maxBytes -MaxSliceLines $maxLines -MaxSliceChars $maxChars} $artifact $ExpectedTask ([int64]$definition.maxTranscriptBytes) ([int]$definition.maxSliceLines) ([int]$definition.maxSliceChars)
        }finally{foreach($loaded in $modules){Remove-Module $loaded -Force -ErrorAction SilentlyContinue}}
        $derivedOutcome='passed'
        if($derivedOutcome-cne$CandidateOutcome){throw "Candidate outcome does not match platform-derived artifact outcome: $ArtifactPath"}
        return[pscustomobject][ordered]@{outcome=$derivedOutcome;evidenceHash=Get-ESObjectHash $observation;artifactHash=$artifactHash;verifierId=[string]$definition.verifierId;verifierDefinitionHash=[string]$verifierSnapshot.definitionHash;verificationStatus='verified';artifactPath=$resolved.Relative;observation=$observation}
    }
    if ($artifact.observations -isnot [Array]) { throw 'Evidence artifact observations must be an array.' }
    $failed = $false
    $observationPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $allowedObservationFields = @($definition.observationFields | ForEach-Object { [string]$_ })
    foreach ($observation in @($artifact.observations)) {
        if ($null -eq $observation -or $observation -isnot [pscustomobject]) { throw 'Evidence observations must be objects.' }
        foreach ($field in $allowedObservationFields) { if ($null -eq $observation.PSObject.Properties[$field]) { throw "Evidence observation field is missing: $field" } }
        foreach ($property in $observation.PSObject.Properties) { if ($allowedObservationFields -cnotcontains [string]$property.Name) { throw "Evidence observation field is unsupported: $($property.Name)" } }
        Assert-ESHash ([string]$observation.expectedSha256) 'Evidence observation expectedSha256'
        $source = Resolve-ESProjectFile $ProjectRoot ([string]$observation.path)
        if (@($ExpectedSourcePaths | Where-Object { [string]$_ -ceq [string]$source.Relative }).Count -ne 1) { throw "Evidence observation is outside the verified sourceScope: $($source.Relative)" }
        if (-not $observationPaths.Add([string]$source.Relative)) { throw "Evidence observation path is duplicated: $($source.Relative)" }
        $actualHash = (Get-FileHash -LiteralPath $source.Full -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -cne [string]$observation.expectedSha256) { $failed = $true }
    }
    $derivedOutcome = if ($failed) { 'failed' } elseif (@($artifact.observations).Count -eq 0) { 'unverified' } else { 'passed' }
    if ($derivedOutcome -cne $CandidateOutcome) { throw "Candidate outcome does not match platform-derived artifact outcome: $ArtifactPath" }
    [pscustomobject][ordered]@{
        outcome = $derivedOutcome
        evidenceHash = $artifactHash
        artifactHash = $artifactHash
        verifierId = [string]$definition.verifierId
        verifierDefinitionHash = [string]$verifierSnapshot.definitionHash
        verificationStatus = 'verified'
        artifactPath = $resolved.Relative
    }
}

function Test-ESEvidenceArtifacts {
    param([string]$ProjectRoot, $EvidenceSet, [string]$ExpectedSourceScopeHash, [string[]]$ExpectedSourcePaths, [string[]]$RequiredClaimIds, $ExpectedTask)
    $drift = @()
    foreach ($item in @($EvidenceSet.items)) {
        if ([string]::IsNullOrWhiteSpace([string]$item.artifactPath)) { continue }
        try {
            $verified = ConvertTo-ESVerifiedArtifact $ProjectRoot ([string]$item.artifactPath) ([string]$item.claimId) ([string]$item.candidateOutcome) ([string]$item.candidateEvidenceHash) $ExpectedSourceScopeHash $ExpectedSourcePaths ([string]$item.verifierId) ([string]$item.verifierDefinitionHash) $ExpectedTask
            if ([string]$verified.artifactHash -cne [string]$item.artifactHash -or [string]$verified.evidenceHash -cne [string]$item.evidenceHash -or [string]$verified.outcome -cne [string]$item.outcome) { $drift += [string]$item.artifactPath }
        } catch { if($RequiredClaimIds-ccontains[string]$item.claimId){$drift += [string]$item.artifactPath} }
    }
    return $drift
}

function Copy-ESObject {
    param($Value)
    if ($null -eq $Value) { return $null }
    return ($Value | ConvertTo-Json -Depth 40 -Compress | ConvertFrom-Json)
}

function Get-ESEventHashInput {
    param($Event)
    [ordered]@{
        schemaVersion = [int]$Event.schemaVersion
        eventId = [string]$Event.eventId
        eventType = [string]$Event.eventType
        occurredUtc = [string]$Event.occurredUtc
        previousEventHash = if ($null -eq $Event.previousEventHash) { $null } else { [string]$Event.previousEventHash }
        state = $Event.state
        metadata = if ($null -eq $Event.PSObject.Properties['metadata']) { [ordered]@{} } else { $Event.metadata }
    }
}

function Get-ESReceiptHashInput {
    param($Receipt)
    $input=[ordered]@{
        schemaVersion = [int]$Receipt.schemaVersion
        receiptId = [string]$Receipt.receiptId
        taskId = [string]$Receipt.taskId
        taskRevision = [int]$Receipt.taskRevision
        contextVersion = [int]$Receipt.contextVersion
        planHash = [string]$Receipt.planHash
        goalRevisionHash = [string]$Receipt.goalRevisionHash
        acceptanceProfileHash = [string]$Receipt.acceptanceProfileHash
    }
    if($null-ne$Receipt.PSObject.Properties['routePlanId']-or$null-ne$Receipt.PSObject.Properties['routePlanPath']-or$null-ne$Receipt.PSObject.Properties['routePlanHash']-or$null-ne$Receipt.PSObject.Properties['routePlanArtifactHash']-or$null-ne$Receipt.PSObject.Properties['routePlanSnapshotHash']){
        $input.routePlanId=[string]$Receipt.routePlanId
        $input.routePlanPath=[string]$Receipt.routePlanPath
        $input.routePlanHash=[string]$Receipt.routePlanHash
        $input.routePlanArtifactHash=[string]$Receipt.routePlanArtifactHash
        $input.routePlanSnapshotHash=[string]$Receipt.routePlanSnapshotHash
    }
    if($null-ne$Receipt.PSObject.Properties['evidenceContractId']-or$null-ne$Receipt.PSObject.Properties['evidenceContractHash']){
        $input.evidenceContractId=[string]$Receipt.evidenceContractId
        $input.evidenceContractHash=[string]$Receipt.evidenceContractHash
    }
    if($null-ne$Receipt.PSObject.Properties['evaluationId']-or$null-ne$Receipt.PSObject.Properties['evaluationRecordPath']-or$null-ne$Receipt.PSObject.Properties['evaluationRecordHash']){
        $input.evaluationId=[string]$Receipt.evaluationId
        $input.evaluationRecordPath=[string]$Receipt.evaluationRecordPath
        $input.evaluationRecordHash=[string]$Receipt.evaluationRecordHash
    }
    $input.evidenceSetHash=[string]$Receipt.evidenceSetHash
    $input.verifiedSourceScopeHash=[string]$Receipt.verifiedSourceScopeHash
    $input.completionDecision=[string]$Receipt.completionDecision
    $input.issuedUtc=[string]$Receipt.issuedUtc
    $input
}

function Get-ESEvaluationRecordHashInput {
    param($Record)
    [ordered]@{
        schemaVersion = [int]$Record.schemaVersion
        contractId = [string]$Record.contractId
        contractHash = [string]$Record.contractHash
        recordType = [string]$Record.recordType
        evaluationId = [string]$Record.evaluationId
        purpose = [string]$Record.purpose
        requestHash = [string]$Record.requestHash
        evaluatorId = [string]$Record.evaluatorId
        evaluatorDefinitionHash = [string]$Record.evaluatorDefinitionHash
        taskId = [string]$Record.taskId
        taskRevision = [int]$Record.taskRevision
        contextVersion = [int]$Record.contextVersion
        planHash = [string]$Record.planHash
        goalRevisionHash = [string]$Record.goalRevisionHash
        acceptanceProfileId = [string]$Record.acceptanceProfileId
        acceptanceProfileHash = [string]$Record.acceptanceProfileHash
        evidenceContractId = [string]$Record.evidenceContractId
        evidenceContractHash = [string]$Record.evidenceContractHash
        evidenceSetHash = if($null-eq$Record.evidenceSetHash){$null}else{[string]$Record.evidenceSetHash}
        verifiedSourceScopeHash = if($null-eq$Record.verifiedSourceScopeHash){$null}else{[string]$Record.verifiedSourceScopeHash}
        evaluatedUtc = [string]$Record.evaluatedUtc
        decision = [string]$Record.decision
        decisionScope = [string]$Record.decisionScope
        evidenceState = [string]$Record.evidenceState
        inputSnapshotHash = [string]$Record.inputSnapshotHash
        trajectoryRecord = $Record.trajectoryRecord
        outcomeAssertions = @($Record.outcomeAssertions)
        failureRecords = @($Record.failureRecords)
        influenceRefs = @($Record.influenceRefs)
        evidenceRefs = @($Record.evidenceRefs)
        nonClaims = @($Record.nonClaims)
    }
}

function Assert-ESGoalRevisionContract {
    param($Goal)
    if ($null -eq $Goal -or $Goal -isnot [pscustomobject]) { throw 'GoalRevision must be a JSON object.' }

    $requiredProperties = @('schemaVersion','goalId','goalRevision','scope','acceptanceIntent','status','budget','parentGoalRef','revisionHash')
    $actualProperties = @($Goal.PSObject.Properties | ForEach-Object { [string]$_.Name })
    foreach ($name in $requiredProperties) {
        if ($actualProperties -cnotcontains $name) { throw "GoalRevision is missing required property: $name" }
    }
    foreach ($name in $actualProperties) {
        if ($requiredProperties -cnotcontains $name) { throw "GoalRevision contains an unsupported property: $name" }
    }

    if ($Goal.schemaVersion -isnot [int] -and $Goal.schemaVersion -isnot [long]) { throw 'GoalRevision schemaVersion must be an integer.' }
    if ([int]$Goal.schemaVersion -ne 1 -or [string]$Goal.status -cne 'frozen') { throw 'GoalRevision must be schemaVersion 1 and frozen.' }
    Assert-ESSafeId ([string]$Goal.goalId) 'GoalId'
    if ($Goal.goalRevision -isnot [string] -or [string]$Goal.goalRevision -notmatch '^r[1-9][0-9]{0,8}$') { throw 'GoalRevision must use the frozen rN format.' }
    if ($Goal.scope -isnot [Array] -or @($Goal.scope).Count -eq 0) { throw 'GoalRevision scope must be a non-empty array.' }
    $scopeKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($scopeEntry in @($Goal.scope)) {
        if ($scopeEntry -isnot [string] -or [string]::IsNullOrWhiteSpace([string]$scopeEntry)) { throw 'GoalRevision scope entries must be non-empty strings.' }
        if (-not $scopeKeys.Add([string]$scopeEntry)) { throw 'GoalRevision scope must be unique.' }
    }
    if ($Goal.acceptanceIntent -isnot [string] -and $Goal.acceptanceIntent -isnot [pscustomobject] -and $Goal.acceptanceIntent -isnot [Collections.IDictionary]) { throw 'GoalRevision acceptanceIntent must be a string or object.' }
    if ($Goal.budget -isnot [pscustomobject] -and $Goal.budget -isnot [Collections.IDictionary]) { throw 'GoalRevision budget must be an object.' }
    if ($null -ne $Goal.parentGoalRef -and $Goal.parentGoalRef -isnot [string]) { throw 'GoalRevision parentGoalRef must be a string or null.' }
    Assert-ESHash ([string]$Goal.revisionHash) 'GoalRevisionHash'
}

function Resolve-ESGoalRevision {
    param([string]$ProjectRoot, [string]$GoalRevisionPath)
    $resolved = Resolve-ESProjectFile $ProjectRoot $GoalRevisionPath
    $goal = Read-ESStrictJson $resolved.Full
    Assert-ESGoalRevisionContract $goal
    $core = [ordered]@{schemaVersion=1;goalId=[string]$goal.goalId;goalRevision=[string]$goal.goalRevision;scope=@($goal.scope | ForEach-Object {[string]$_});acceptanceIntent=$goal.acceptanceIntent;status='frozen';budget=$goal.budget;parentGoalRef=if($null -eq $goal.parentGoalRef){$null}else{[string]$goal.parentGoalRef}}
    if ((Get-ESObjectHash $core) -cne [string]$goal.revisionHash) { throw 'GoalRevision hash mismatch.' }
    [pscustomobject][ordered]@{ goalId=[string]$goal.goalId; goalRevision=[string]$goal.goalRevision; goalRevisionHash=[string]$goal.revisionHash; artifactHash=(Get-FileHash -LiteralPath $resolved.Full -Algorithm SHA256).Hash.ToLowerInvariant(); path=$resolved.Relative; scope=@($core.scope); acceptanceIntent=$core.acceptanceIntent; budget=$core.budget; parentGoalRef=$core.parentGoalRef }
}

function Resolve-ESRoutePlan {
    param([string]$ProjectRoot, [string]$RoutePlanPath, $ExpectedGoal)
    Resolve-ESRoutePlanArtifact -ProjectRoot $ProjectRoot -RoutePlanPath $RoutePlanPath -ExpectedGoal $ExpectedGoal -RequireReady
}

function New-ESGoalRevision {
    [CmdletBinding()]
    param(
        [string]$ProjectRoot='.', [string]$StoreRoot='ES/Output/TaskContextRuntime',
        [Parameter(Mandatory=$true)][string]$GoalId, [Parameter(Mandatory=$true)][string]$GoalRevision,
        [Parameter(Mandatory=$true)][string[]]$Scope, [Parameter(Mandatory=$true)]$AcceptanceIntent,
        [Parameter(Mandatory=$true)]$Budget, [string]$ParentGoalRef
    )
    Assert-ESSafeId $GoalId 'GoalId'
    if ($GoalRevision -notmatch '^r[1-9][0-9]{0,8}$') { throw 'GoalRevision must use the frozen rN format.' }
    $root=(Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
    $paths=Resolve-ESRuntimePaths $root $StoreRoot ''
    $relative="goals/$GoalId/$GoalRevision.json"
    $projectRelative=($StoreRoot.TrimEnd('\','/') + '/' + $relative).Replace('\\','/')
    $full=Join-Path $paths.StoreRoot ($relative.Replace('/',[IO.Path]::DirectorySeparatorChar))
    $core=[ordered]@{schemaVersion=1;goalId=$GoalId;goalRevision=$GoalRevision;scope=@($Scope);acceptanceIntent=$AcceptanceIntent;status='frozen';budget=$Budget;parentGoalRef=if([string]::IsNullOrWhiteSpace($ParentGoalRef)){$null}else{$ParentGoalRef}}
    $payload=[ordered]@{};foreach($key in $core.Keys){$payload[$key]=$core[$key]};$payload.revisionHash=Get-ESObjectHash $core
    Assert-ESGoalRevisionContract ([pscustomobject]$payload)
    if (Test-Path -LiteralPath $full -PathType Leaf) {
        $existing=Resolve-ESGoalRevision $root $projectRelative
        if ($existing.goalRevisionHash -cne [string]$payload.revisionHash) { throw 'GoalRevision already exists with different content.' }
        return $existing
    }
    Write-ESCreateOnlyJson $full $payload
    return (Resolve-ESGoalRevision $root $projectRelative)
}

function Assert-ESEventTransition {
    param($Previous, $Current, [string]$EventType, $Metadata)
    if ($null -eq $Previous) {
        if ($EventType -ne 'Created' -or [int]$Current.taskRevision -ne 1 -or [int]$Current.contextVersion -ne 1 -or [string]$Current.taskStatus -ne 'Active' -or [string]$Current.contextStatus -ne 'Live') { throw 'Invalid initial event.' }
        return
    }
    if ([int]$Current.taskRevision -ne ([int]$Previous.taskRevision + 1)) { throw 'TaskRevision is not contiguous.' }
    if ([int]$Current.contextVersion -lt [int]$Previous.contextVersion -or [int]$Current.contextVersion -gt ([int]$Previous.contextVersion + 1)) { throw 'ContextVersion transition is invalid.' }
    switch ($EventType) {
        'SourceScopeVerified' { if ($Current.taskStatus -ne $Previous.taskStatus -or $Current.contextStatus -ne $Previous.contextStatus -or [int]$Current.contextVersion -ne [int]$Previous.contextVersion) { throw 'SourceScopeVerified changed lifecycle state.' } }
        'EvidenceSubmitted' { if ($Current.taskStatus -ne $Previous.taskStatus -or $Current.contextStatus -ne $Previous.contextStatus -or [int]$Current.contextVersion -ne [int]$Previous.contextVersion) { throw 'EvidenceSubmitted changed lifecycle state.' } }
        'CompletionAccepted' { if ($Previous.taskStatus -ne 'Active' -or $Previous.contextStatus -ne 'Live' -or $Current.taskStatus -ne 'Completed' -or $Current.contextStatus -ne 'Frozen' -or $Current.completionDecision -ne 'accepted' -or $Current.deliveryAcceptance -ne 'pending' -or [int]$Current.contextVersion -ne ([int]$Previous.contextVersion + 1)) { throw 'Accepted completion transition is invalid.' } }
        'CompletionRejected' { if ($Previous.taskStatus -ne 'Active' -or $Current.taskStatus -ne 'Blocked' -or $Current.contextStatus -ne $Previous.contextStatus -or $Current.completionDecision -ne 'rejected') { throw 'Rejected completion transition is invalid.' } }
        'CompletionUndetermined' { if ($Previous.taskStatus -ne 'Active' -or $Current.taskStatus -ne 'Active' -or $Current.completionDecision -ne 'undetermined') { throw 'Undetermined completion transition is invalid.' } }
        'DeliveryAcceptanceChanged' { if ($Previous.taskStatus -ne 'Completed' -or $Previous.contextStatus -ne 'Frozen' -or $Previous.completionDecision -ne 'accepted' -or $Previous.deliveryAcceptance -ne 'pending' -or $Current.taskStatus -ne 'Completed' -or $Current.contextStatus -ne 'Frozen' -or $Current.completionDecision -ne 'accepted' -or @('accepted','rejected') -notcontains [string]$Current.deliveryAcceptance) { throw 'Delivery acceptance transition is invalid.' } }
        'Reopened' { if ($Previous.taskStatus -ne 'Completed' -or $Previous.contextStatus -ne 'Frozen' -or $Current.taskStatus -ne 'Active' -or $Current.contextStatus -ne 'Live' -or $Current.completionDecision -ne 'undetermined' -or [int]$Current.contextVersion -ne ([int]$Previous.contextVersion + 1)) { throw 'Reopen transition is invalid.' } }
        'Suspended' { if ($Previous.taskStatus -ne 'Active' -or $Current.taskStatus -ne 'Suspended') { throw 'Suspend transition is invalid.' } }
        'Resumed' { if (@('Suspended','Blocked') -notcontains [string]$Previous.taskStatus -or $Current.taskStatus -ne 'Active') { throw 'Resume transition is invalid.' } }
        'Cancelled' { if (@('Active','Suspended','Blocked') -notcontains [string]$Previous.taskStatus -or $Current.taskStatus -ne 'Cancelled' -or $Current.contextStatus -ne 'Frozen') { throw 'Cancel transition is invalid.' } }
        'Invalidated' { if (@('Active','Suspended','Blocked') -notcontains [string]$Previous.taskStatus -or $Current.taskStatus -ne 'Invalidated' -or $Current.contextStatus -ne 'PartiallyInvalidated') { throw 'Invalidate transition is invalid.' } }
        'CompactionStarted' { if ($Previous.contextStatus -ne 'Live' -or $Current.contextStatus -ne 'Compacting') { throw 'Compaction start transition is invalid.' } }
        'CompactionEnded' { if ($Previous.contextStatus -ne 'Compacting' -or $Current.contextStatus -ne 'Live') { throw 'Compaction end transition is invalid.' } }
        'SourceDriftDetected' { if (@('Live','Compacting') -notcontains [string]$Previous.contextStatus -or $Current.contextStatus -ne 'PartiallyInvalidated') { throw 'Source drift transition is invalid.' } }
        'SourceDriftResolved' { if ($Previous.contextStatus -ne 'PartiallyInvalidated' -or $Current.contextStatus -ne 'Live') { throw 'Source drift recovery transition is invalid.' } }
        'Archived' { if ($Previous.taskStatus -ne 'Completed' -or $Previous.contextStatus -ne 'Frozen' -or $Current.contextStatus -ne 'Archived') { throw 'Archive transition is invalid.' } }
        'Expired' { if ($Previous.contextStatus -eq 'Quarantined' -or $Current.contextStatus -ne 'Expired') { throw 'Expire transition is invalid.' } }
        'Quarantined' { if ($Previous.contextStatus -eq 'Quarantined' -or $Current.contextStatus -ne 'Quarantined' -or [string]$Metadata.recoveryContextStatus -cne [string]$Previous.contextStatus) { throw 'Quarantine transition is invalid.' } }
        'Recovered' { if ($Previous.contextStatus -ne 'Quarantined' -or $Current.contextStatus -eq 'Quarantined' -or $script:ContextStatuses -notcontains [string]$Current.contextStatus -or [string]$Metadata.recoveryContextStatus -cne [string]$Current.contextStatus) { throw 'Recovery transition is invalid.' } }
        default { throw "Unknown event type: $EventType" }
    }
}

function Read-ESEventChain {
    param($Paths, [switch]$VerifyReceipts)
    if (-not (Test-Path -LiteralPath $Paths.EventsRoot -PathType Container)) { throw 'Task event log is missing.' }
    $files = @(Get-ChildItem -LiteralPath $Paths.EventsRoot -File -Filter '*.json' | Sort-Object Name)
    if ($files.Count -eq 0) { throw 'Task event log is empty.' }
    $events = @()
    $previousEvent = $null
    $previousState = $null
    for ($index = 0; $index -lt $files.Count; $index++) {
        $expectedPrefix = '{0:D10}-' -f ($index + 1)
        if (-not $files[$index].Name.StartsWith($expectedPrefix, [StringComparison]::Ordinal)) { throw 'Event log contains a revision gap or duplicate.' }
        $event = Read-ESStrictJson $files[$index].FullName
        $actualHash = Get-ESObjectHash (Get-ESEventHashInput $event)
        if ([string]$event.eventHash -cne $actualHash) { throw "Event hash mismatch: $($files[$index].Name)" }
        $expectedPrevious = if ($null -eq $previousEvent) { $null } else { [string]$previousEvent.eventHash }
        if ([string]$event.previousEventHash -ne [string]$expectedPrevious) { throw "Event chain mismatch: $($files[$index].Name)" }
        Assert-ESEventTransition $previousState $event.state ([string]$event.eventType) $event.metadata
        if ($event.state.taskId -ne $Paths.TaskRoot.Substring($Paths.StoreRoot.Length).TrimStart('\','/')) { throw 'Event TaskId does not match its store path.' }
        if ($VerifyReceipts -and $event.eventType -eq 'CompletionAccepted') { Test-ESBoundReceipt -Paths $Paths -State $event.state | Out-Null }
        if ($VerifyReceipts -and $null -ne $event.metadata.PSObject.Properties['evaluationRecordPath']) {
            $evaluation=Test-ESBoundEvaluationRecord -Paths $Paths -RelativePath ([string]$event.metadata.evaluationRecordPath) -ExpectedHash ([string]$event.metadata.evaluationRecordHash)
            if([string]$evaluation.evaluationId-cne[string]$event.metadata.evaluationId-or[string]$evaluation.taskId-cne[string]$event.state.taskId-or[string]$evaluation.decision-cne[string]$event.metadata.decision){throw 'Event EvaluationRecord binding mismatch.'}
        }
        $events += $event
        $previousEvent = $event
        $previousState = $event.state
    }
    return ,$events
}

function Test-ESBoundReceipt {
    param($Paths, $State)
    if ($null -eq $State.completionReceipt) { throw 'Accepted state is missing its receipt binding.' }
    $relative = [string]$State.completionReceipt.path
    if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[\/])\.\.([\/]|$)') { throw 'Receipt path is not bounded.' }
    $full = [IO.Path]::GetFullPath((Join-Path $Paths.StoreRoot $relative))
    if (-not $full.StartsWith($Paths.StoreRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $full -PathType Leaf)) { throw 'Bound receipt is missing.' }
    Assert-ESNoReparsePointBelowRoot $Paths.ProjectRoot $full 'Receipt path'
    $receiptItem = Get-Item -LiteralPath $full -Force
    if ($receiptItem.LinkType -or ($receiptItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Bound receipt cannot be a reparse point.' }
    $receipt = Read-ESStrictJson $full
    $actual = Get-ESObjectHash (Get-ESReceiptHashInput $receipt)
    if ([string]$receipt.receiptHash -cne $actual -or [string]$State.completionReceipt.receiptHash -cne $actual) { throw 'Completion receipt hash mismatch.' }
    $bindings=@(@('taskId',$State.taskId),@('taskRevision',$State.taskRevision),@('contextVersion',$State.contextVersion),@('planHash',$State.planHash),@('goalRevisionHash',$State.goalRevisionHash),@('acceptanceProfileHash',$State.acceptanceProfile.profileHash))
    if($null-ne$State.PSObject.Properties['routePlan']-and$null-ne$State.routePlan){$bindings+=@(@('routePlanId',$State.routePlan.routePlanId),@('routePlanPath',$State.routePlan.routePlanPath),@('routePlanHash',$State.routePlan.routePlanHash),@('routePlanArtifactHash',$State.routePlan.artifactHash),@('routePlanSnapshotHash',$State.routePlan.snapshotHash))}
    if($null-ne$State.acceptanceProfile.PSObject.Properties['evidenceContractId']-or$null-ne$State.acceptanceProfile.PSObject.Properties['evidenceContractHash']){$bindings+=@(@('evidenceContractId',$State.acceptanceProfile.evidenceContractId),@('evidenceContractHash',$State.acceptanceProfile.evidenceContractHash))}
    $bindings+=@(@('evidenceSetHash',$State.evidenceSet.evidenceSetHash),@('verifiedSourceScopeHash',$State.verifiedSourceScopeHash),@('completionDecision','accepted'))
    foreach ($binding in $bindings) {
        if ([string]$receipt.($binding[0]) -cne [string]$binding[1]) { throw "Completion receipt binding mismatch: $($binding[0])" }
    }
    if ($null -ne $receipt.PSObject.Properties['evaluationRecordPath']) {
        $evaluation = Test-ESBoundEvaluationRecord -Paths $Paths -RelativePath ([string]$receipt.evaluationRecordPath) -ExpectedHash ([string]$receipt.evaluationRecordHash)
        if ([string]$evaluation.evaluationId -cne [string]$receipt.evaluationId -or [string]$evaluation.taskId -cne [string]$State.taskId -or [string]$evaluation.decision -cne 'accepted' -or [string]$evaluation.purpose -cne 'completion') { throw 'Completion receipt evaluation binding mismatch.' }
    }
    return $receipt
}

function Test-ESBoundEvaluationRecord {
    param($Paths, [string]$RelativePath, [string]$ExpectedHash)
    Assert-ESHash $ExpectedHash 'EvaluationRecordHash'
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath) -or $RelativePath -match '(^|[\/])\.\.([\/]|$)') { throw 'EvaluationRecord path is not bounded.' }
    $full = [IO.Path]::GetFullPath((Join-Path $Paths.StoreRoot ($RelativePath.Replace('/',[IO.Path]::DirectorySeparatorChar))))
    if (-not $full.StartsWith($Paths.StoreRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $full -PathType Leaf)) { throw 'Bound EvaluationRecord is missing.' }
    Assert-ESNoReparsePointBelowRoot $Paths.ProjectRoot $full 'EvaluationRecord path'
    $item = Get-Item -LiteralPath $full -Force
    if ($item.LinkType -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Bound EvaluationRecord cannot be a reparse point.' }
    $record = Read-ESStrictJson $full
    if ([int]$record.schemaVersion -ne 1 -or [string]$record.contractId -cne 'es://automation/contracts/evaluation-record/v1' -or [string]$record.recordType -cne 'EvaluationRecord') { throw 'EvaluationRecord identity is invalid.' }
    $actual = Get-ESObjectHash (Get-ESEvaluationRecordHashInput $record)
    if ([string]$record.recordHash -cne $actual -or $ExpectedHash -cne $actual) { throw 'EvaluationRecord hash mismatch.' }
    return $record
}

function Invoke-ESTaskMutex {
    param([string]$TaskId, [scriptblock]$Body)
    $nameHash = Get-ESObjectHash $TaskId
    $mutex = [Threading.Mutex]::new($false, 'ESFramework_TaskContext_' + $nameHash)
    $acquired = $false
    try {
        $acquired = $mutex.WaitOne([TimeSpan]::FromSeconds(15))
        if (-not $acquired) { throw 'Another TaskContextRuntime operation is active for this TaskId.' }
        & $Body
    } finally {
        if ($acquired) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
}

function Assert-ESCas {
    param($State, [int]$ExpectedTaskRevision, [int]$ExpectedContextVersion)
    if ([int]$State.taskRevision -ne $ExpectedTaskRevision -or [int]$State.contextVersion -ne $ExpectedContextVersion) {
        throw "CAS conflict: current TaskRevision=$($State.taskRevision), ContextVersion=$($State.contextVersion)."
    }
}

function Find-ESIdempotentEvent {
    param($Events, [string]$IdempotencyKey, [string]$OperationHash)
    if ([string]::IsNullOrWhiteSpace($IdempotencyKey)) { return $null }
    foreach ($event in $Events) {
        if ($null -ne $event.PSObject.Properties['metadata'] -and [string]$event.metadata.idempotencyKey -ceq $IdempotencyKey) {
            if ([string]$event.metadata.operationHash -cne $OperationHash) { throw 'IdempotencyKey is already bound to a different operation.' }
            return $event
        }
    }
    return $null
}

function Write-ESEvent {
    param($Paths, $PreviousEvent, $State, [string]$EventType, [string]$IdempotencyKey, $Metadata)
    $stateCopy = Copy-ESObject $State
    if (-not [string]::IsNullOrWhiteSpace($IdempotencyKey)) { $stateCopy.idempotencyKeys = @($stateCopy.idempotencyKeys) + $IdempotencyKey }
    $meta = [ordered]@{ idempotencyKey=$IdempotencyKey }
    if ($null -ne $Metadata) {
        foreach ($property in $Metadata.PSObject.Properties) { $meta[$property.Name] = $property.Value }
    }
    $base = [ordered]@{
        schemaVersion = 1
        eventId = [Guid]::NewGuid().ToString('N')
        eventType = $EventType
        occurredUtc = [DateTime]::UtcNow.ToString('o')
        previousEventHash = if ($null -eq $PreviousEvent) { $null } else { [string]$PreviousEvent.eventHash }
        state = $stateCopy
        metadata = $meta
    }
    $event = [ordered]@{}
    foreach ($key in $base.Keys) { $event[$key] = $base[$key] }
    $event['eventHash'] = Get-ESObjectHash $base
    Assert-ESEventTransition $(if($null -eq $PreviousEvent){$null}else{$PreviousEvent.state}) $stateCopy $EventType $meta
    $name = '{0:D10}-{1}.json' -f [int]$stateCopy.taskRevision, $base.eventId
    Write-ESCreateOnlyJson (Join-Path $Paths.EventsRoot $name) $event
    return [pscustomobject]$event
}

function New-ESTaskContextTask {
    [CmdletBinding()]
    param(
        [string]$ProjectRoot='.', [string]$StoreRoot='ES/Output/TaskContextRuntime',
        [Parameter(Mandatory=$true)][string]$TaskId, [string]$PlanHash,
        [Parameter(Mandatory=$true)][string]$RoutePlanPath, [Parameter(Mandatory=$true)][string]$GoalRevisionPath,
        [Parameter(Mandatory=$true)][string]$AcceptanceProfileId, [Parameter(Mandatory=$true)][string]$OutcomeEvaluatorId,
        [string[]]$RequiredClaim=@(), $RequiredClaimVerifier=$null,
        [string[]]$OptionalClaim=@(), $OptionalClaimVerifier=$null,
        [string]$InteractionSessionId,
        [int]$MaxEvidenceAgeHours=24, [switch]$AllowUnverifiedClaims,
        [string[]]$RequestedSourceScope=@(), [Parameter(Mandatory=$true)][string]$IdempotencyKey
    )
    Assert-ESSafeId $TaskId 'TaskId'; Assert-ESSafeId $AcceptanceProfileId 'AcceptanceProfileId'; Assert-ESSafeId $OutcomeEvaluatorId 'OutcomeEvaluatorId'; Assert-ESSafeId $IdempotencyKey 'IdempotencyKey'
    if (-not [string]::IsNullOrWhiteSpace($PlanHash)) { Assert-ESHash $PlanHash 'PlanHash' }
    if ($MaxEvidenceAgeHours -lt 1 -or $MaxEvidenceAgeHours -gt 8760) { throw 'MaxEvidenceAgeHours is outside 1..8760.' }
    $claims = @($RequiredClaim | ForEach-Object { Assert-ESSafeId $_ 'RequiredClaim'; $_ } | Sort-Object -Unique)
    $optionalClaims=@($OptionalClaim|ForEach-Object{Assert-ESSafeId $_ 'OptionalClaim';$_}|Sort-Object -Unique)
    if(@($claims|Where-Object{$optionalClaims-ccontains$_}).Count){throw 'A claim cannot be both required and optional.'}
    $registrySnapshot = Get-ESEvidenceVerifierRegistrySnapshot
    $outcomeEvaluatorSnapshot = Get-ESOutcomeEvaluatorDefinition $OutcomeEvaluatorId
    if ($AcceptanceProfileId -cnotmatch [string]$outcomeEvaluatorSnapshot.definition.profileIdPattern) { throw 'Outcome evaluator does not support the AcceptanceProfileId.' }
    $evidenceContractSnapshot = Get-ESPlatformEvidenceContractSnapshot
    $evaluationContractSnapshot = Get-ESEvaluationRecordContractSnapshot
    $requiredVerifiers = @()
    if ($claims.Count -gt 0 -and $null -eq $RequiredClaimVerifier) { throw 'Every required claim needs an explicit verifier binding.' }
    foreach ($claim in $claims) {
        $binding = if ($RequiredClaimVerifier -is [Collections.IDictionary]) { $RequiredClaimVerifier[$claim] } else { $property=$RequiredClaimVerifier.PSObject.Properties[$claim]; if($null-ne$property){$property.Value}else{$null} }
        if ([string]::IsNullOrWhiteSpace([string]$binding)) { throw "Required verifier binding is missing for claim: $claim" }
        $verifierId = [string]$binding
        Assert-ESSafeId $verifierId 'RequiredClaimVerifierId'
        $verifierSnapshot = Get-ESEvidenceVerifierDefinition $verifierId
        if ([string]::IsNullOrWhiteSpace([string]$verifierSnapshot.definition.claimIdPattern) -or $claim -cnotmatch [string]$verifierSnapshot.definition.claimIdPattern) { throw "Evidence verifier does not support required claim: $claim" }
        $requiredVerifiers += [ordered]@{claimId=$claim;verifierId=$verifierId;verifierDefinitionHash=[string]$verifierSnapshot.definitionHash}
    }
    $optionalVerifiers=@()
    if($optionalClaims.Count-gt0-and$null-eq$OptionalClaimVerifier){throw 'Every optional claim needs an explicit verifier binding.'}
    foreach($claim in $optionalClaims){
        $binding=if($OptionalClaimVerifier-is[Collections.IDictionary]){$OptionalClaimVerifier[$claim]}else{$property=$OptionalClaimVerifier.PSObject.Properties[$claim];if($null-ne$property){$property.Value}else{$null}}
        if([string]::IsNullOrWhiteSpace([string]$binding)){throw "Optional verifier binding is missing for claim: $claim"}
        $verifierId=[string]$binding;Assert-ESSafeId $verifierId 'OptionalClaimVerifierId'
        $verifierSnapshot=Get-ESEvidenceVerifierDefinition $verifierId
        if($claim-cnotmatch[string]$verifierSnapshot.definition.claimIdPattern){throw "Evidence verifier does not support optional claim: $claim"}
        $optionalVerifiers+=[ordered]@{claimId=$claim;verifierId=$verifierId;verifierDefinitionHash=[string]$verifierSnapshot.definitionHash}
    }
    $interactionSessionIdNormalized=if([string]::IsNullOrWhiteSpace($InteractionSessionId)){$null}else{$InteractionSessionId.ToLowerInvariant()}
    if($null-ne$interactionSessionIdNormalized-and$interactionSessionIdNormalized-cnotmatch'^[a-f0-9-]{16,64}$'){throw 'InteractionSessionId is invalid.'}
    if(@($optionalVerifiers|Where-Object{[string]$_.verifierId-ceq'platform.codex-transcript-slice-v1'}).Count-gt0-and$null-eq$interactionSessionIdNormalized){throw 'Transcript observation claims require a frozen InteractionSessionId.'}
    $paths = Resolve-ESRuntimePaths $ProjectRoot $StoreRoot $TaskId
    $goal = Resolve-ESGoalRevision $paths.ProjectRoot $GoalRevisionPath
    $routePlan = Resolve-ESRoutePlan $paths.ProjectRoot $RoutePlanPath $goal
    if (-not [string]::IsNullOrWhiteSpace($PlanHash) -and $PlanHash -cne [string]$routePlan.routePlanHash) { throw 'PlanHash must equal the platform-verified RoutePlan hash.' }
    $operationHash = Get-ESObjectHash ([ordered]@{operation='Create';taskId=$TaskId;planHash=[string]$routePlan.routePlanHash;routePlanId=[string]$routePlan.routePlanId;routePlanPath=[string]$routePlan.routePlanPath;routePlanArtifactHash=[string]$routePlan.routePlanArtifactHash;routePlanSnapshotHash=[string]$routePlan.snapshotHash;goalRevisionHash=$goal.goalRevisionHash;acceptanceProfileId=$AcceptanceProfileId;outcomeEvaluatorId=$OutcomeEvaluatorId;outcomeEvaluatorDefinitionHash=[string]$outcomeEvaluatorSnapshot.definitionHash;outcomeEvaluatorRegistryHash=[string]$outcomeEvaluatorSnapshot.registryHash;evaluationContractId=[string]$evaluationContractSnapshot.contractId;evaluationContractHash=[string]$evaluationContractSnapshot.contractHash;requiredClaims=$claims;requiredVerifiers=$requiredVerifiers;optionalClaims=$optionalClaims;optionalVerifiers=$optionalVerifiers;interactionSessionId=$interactionSessionIdNormalized;verifierRegistryHash=[string]$registrySnapshot.registryHash;evidenceContractId=[string]$evidenceContractSnapshot.contractId;evidenceContractHash=[string]$evidenceContractSnapshot.contractHash;maxEvidenceAgeHours=$MaxEvidenceAgeHours;allowUnverifiedClaims=[bool]$AllowUnverifiedClaims;requestedSourceScope=@($RequestedSourceScope);idempotencyKey=$IdempotencyKey})
    Invoke-ESTaskMutex $TaskId {
        if (Test-Path -LiteralPath $paths.TaskRoot) {
            $existing = Read-ESEventChain $paths -VerifyReceipts
            $match = Find-ESIdempotentEvent $existing $IdempotencyKey $operationHash
            if ($null -ne $match -and $match.eventType -eq 'Created') { return $match.state }
            throw 'TaskId already exists.'
        }
        $resolved = @()
        foreach ($sourcePath in @($RequestedSourceScope)) { $resolved += (Resolve-ESProjectFile $paths.ProjectRoot ([string]$sourcePath)).Relative }
        if (@($resolved | Sort-Object -Unique).Count -ne @($resolved).Count) { throw 'RequestedSourceScope contains duplicate paths.' }
        $profileCore = [ordered]@{ profileId=$AcceptanceProfileId; outcomeEvaluatorId=$OutcomeEvaluatorId; outcomeEvaluatorDefinitionHash=[string]$outcomeEvaluatorSnapshot.definitionHash; outcomeEvaluatorRegistryHash=[string]$outcomeEvaluatorSnapshot.registryHash; evaluationContractId=[string]$evaluationContractSnapshot.contractId; evaluationContractHash=[string]$evaluationContractSnapshot.contractHash; requiredClaims=$claims; requiredVerifiers=$requiredVerifiers; optionalClaims=$optionalClaims; optionalVerifiers=$optionalVerifiers; verifierRegistryHash=[string]$registrySnapshot.registryHash; evidenceContractId=[string]$evidenceContractSnapshot.contractId; evidenceContractHash=[string]$evidenceContractSnapshot.contractHash; maxEvidenceAgeHours=$MaxEvidenceAgeHours; allowUnverifiedClaims=[bool]$AllowUnverifiedClaims; frozen=$true }
        $profile = [ordered]@{}
        foreach ($key in $profileCore.Keys) { $profile[$key] = $profileCore[$key] }
        $profile['profileHash'] = Get-ESObjectHash $profileCore
        $createdUtc=[DateTime]::UtcNow.ToString('o')
        $state = [pscustomobject][ordered]@{
            taskId=$TaskId; planHash=[string]$routePlan.routePlanHash
            routePlan=[pscustomobject][ordered]@{routePlanId=[string]$routePlan.routePlanId;routePlanPath=[string]$routePlan.routePlanPath;routePlanHash=[string]$routePlan.routePlanHash;artifactHash=[string]$routePlan.routePlanArtifactHash;snapshotHash=[string]$routePlan.snapshotHash;profile=[string]$routePlan.profile;routeState=[string]$routePlan.routeState;routeKeys=@($routePlan.routeKeys);head=[string]$routePlan.head;sourceRefsHash=[string]$routePlan.sourceRefsHash;registryHash=[string]$routePlan.registryHash}
            goalId=$goal.goalId; goalRevision=$goal.goalRevision; goalRevisionHash=$goal.goalRevisionHash; goalRevisionPath=$goal.path; createdUtc=$createdUtc; interactionSessionId=$interactionSessionIdNormalized; taskRevision=1; contextVersion=1
            taskStatus='Active'; contextStatus='Live'; completionDecision='undetermined'; deliveryAcceptance='pending'
            acceptanceProfile=$profile; requestedSourceScope=@($RequestedSourceScope); resolvedSourceScope=$resolved
            verifiedSourceScope=@(); verifiedSourceScopeHash=$null; evidenceSet=$null; completionReceipt=$null; idempotencyKeys=@()
        }
        $event = Write-ESEvent $paths $null $state 'Created' $IdempotencyKey ([pscustomobject]@{ operationHash=$operationHash; sourceScopeCount=@($resolved).Count })
        return $event.state
    }
}

function Get-ESTaskContextState {
    [CmdletBinding()]
    param([string]$ProjectRoot='.', [string]$StoreRoot='ES/Output/TaskContextRuntime', [Parameter(Mandatory=$true)][string]$TaskId, [switch]$VerifyIntegrity)
    Assert-ESSafeId $TaskId 'TaskId'
    $paths = Resolve-ESRuntimePaths $ProjectRoot $StoreRoot $TaskId
    $events = Read-ESEventChain $paths -VerifyReceipts:$VerifyIntegrity
    return $events[-1].state
}

function Confirm-ESTaskSourceScope {
    [CmdletBinding()]
    param(
        [string]$ProjectRoot='.', [string]$StoreRoot='ES/Output/TaskContextRuntime', [Parameter(Mandatory=$true)][string]$TaskId,
        [Parameter(Mandatory=$true)][int]$ExpectedTaskRevision, [Parameter(Mandatory=$true)][int]$ExpectedContextVersion,
        [Parameter(Mandatory=$true)][string]$IdempotencyKey
    )
    Assert-ESSafeId $TaskId 'TaskId'; Assert-ESSafeId $IdempotencyKey 'IdempotencyKey'
    $paths = Resolve-ESRuntimePaths $ProjectRoot $StoreRoot $TaskId
    $operationHash = Get-ESObjectHash ([ordered]@{operation='VerifySources';taskId=$TaskId;expectedTaskRevision=$ExpectedTaskRevision;expectedContextVersion=$ExpectedContextVersion;idempotencyKey=$IdempotencyKey})
    Invoke-ESTaskMutex $TaskId {
        $events = Read-ESEventChain $paths -VerifyReceipts
        $duplicate = Find-ESIdempotentEvent $events $IdempotencyKey $operationHash
        if ($null -ne $duplicate) { return $duplicate.state }
        $previous = $events[-1]; Assert-ESCas $previous.state $ExpectedTaskRevision $ExpectedContextVersion
        if ($previous.state.taskStatus -ne 'Active' -or @('Live','PartiallyInvalidated') -notcontains [string]$previous.state.contextStatus) { throw 'Source scope can be verified only for an Active Live or PartiallyInvalidated task.' }
        $observed = @()
        foreach ($sourcePath in @($previous.state.resolvedSourceScope)) {
            $resolved = Resolve-ESProjectFile $paths.ProjectRoot ([string]$sourcePath)
            $observed += [pscustomobject](Get-ESFileObservation $resolved.Full $resolved.Relative)
        }
        $state = Copy-ESObject $previous.state
        $state.taskRevision = [int]$state.taskRevision + 1
        $state.verifiedSourceScope = $observed
        $state.verifiedSourceScopeHash = Get-ESObjectHash @($observed | ForEach-Object { [ordered]@{path=$_.path;length=[int64]$_.length;sha256=$_.sha256} })
        $eventType = if ($state.contextStatus -eq 'PartiallyInvalidated') { $state.contextStatus='Live'; $state.contextVersion=[int]$state.contextVersion+1; 'SourceDriftResolved' } else { 'SourceScopeVerified' }
        $event = Write-ESEvent $paths $previous $state $eventType $IdempotencyKey ([pscustomobject]@{ operationHash=$operationHash; verifiedSourceScopeHash=$state.verifiedSourceScopeHash })
        return $event.state
    }
}

function ConvertTo-ESEvidenceSet {
    param($InputObject, $State, [string]$TaskId, [string]$ProjectRoot)
    $contractSnapshot = Get-ESPlatformEvidenceContractSnapshot
    if ([string]$State.acceptanceProfile.evidenceContractId -cne [string]$contractSnapshot.contractId -or [string]$State.acceptanceProfile.evidenceContractHash -cne [string]$contractSnapshot.contractHash) { throw 'Platform Evidence contract drifted from the frozen AcceptanceProfile.' }
    $canonical = $null -ne $InputObject.PSObject.Properties['contractId'] -or $null -ne $InputObject.PSObject.Properties['contractHash'] -or $null -ne $InputObject.PSObject.Properties['recordType']
    $setFields = @('schemaVersion','taskId','evidenceSetId','capturedUtc','items','contradictions','sourceDrift','unverifiedClaims')
    if ($canonical) {
        $setFields = @('schemaVersion','contractId','contractHash','recordType') + $setFields[1..($setFields.Count-1)]
        Assert-ESObjectShape $InputObject $setFields $setFields 'CandidateEvidenceSet'
        if ([string]$InputObject.contractId -cne [string]$contractSnapshot.contractId) { throw 'CandidateEvidenceSet contractId does not match the platform contract.' }
        Assert-ESHash ([string]$InputObject.contractHash) 'CandidateEvidenceSet contractHash'
        if ([string]$InputObject.contractHash -cne [string]$contractSnapshot.contractHash) { throw 'CandidateEvidenceSet contractHash does not match the platform contract.' }
        if ([string]$InputObject.recordType -cne 'CandidateEvidenceSet') { throw 'CandidateEvidenceSet recordType is invalid.' }
    } else {
        Assert-ESObjectShape $InputObject $setFields $setFields 'LegacyCandidateEvidenceSet'
    }
    if ($InputObject.schemaVersion -isnot [int] -and $InputObject.schemaVersion -isnot [long]) { throw 'EvidenceSet schemaVersion must be an integer.' }
    if ([int]$InputObject.schemaVersion -ne 1) { throw 'EvidenceSet schemaVersion must be 1.' }
    if ([string]$InputObject.taskId -cne $TaskId) { throw 'EvidenceSet taskId does not match the target task.' }
    Assert-ESSafeId ([string]$InputObject.evidenceSetId) 'EvidenceSetId'
    foreach ($arrayField in @('items','contradictions','sourceDrift','unverifiedClaims')) { if ($InputObject.$arrayField -isnot [Array]) { throw "EvidenceSet $arrayField must be an array." } }
    $capturedUtc = [datetime]::Parse([string]$InputObject.capturedUtc, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime().ToString('o')
    $items = @()
    $claimIds = @()
    foreach ($item in @($InputObject.items)) {
        $itemFields = if ($canonical) { @('claimId','candidateOutcome','capturedUtc','sourceScopeHash','candidateEvidenceHash','candidateProducerType','artifactPath') } else { @('claimId','outcome','capturedUtc','sourceScopeHash','evidenceHash','producerType','artifactPath') }
        Assert-ESObjectShape $item $itemFields $itemFields $(if($canonical){'CandidateEvidence'}else{'LegacyCandidateEvidence'})
        $candidateOutcome = if ($canonical) { [string]$item.candidateOutcome } else { [string]$item.outcome }
        $candidateEvidenceHash = if ($canonical) { [string]$item.candidateEvidenceHash } else { [string]$item.evidenceHash }
        $candidateProducerType = if ($canonical) { [string]$item.candidateProducerType } else { [string]$item.producerType }
        Assert-ESSafeId ([string]$item.claimId) 'Evidence claimId'; Assert-ESHash $candidateEvidenceHash 'CandidateEvidenceHash'
        if (@('passed','failed','unverified') -cnotcontains $candidateOutcome) { throw 'CandidateEvidence outcome is invalid.' }
        if ([string]$item.sourceScopeHash -cne [string]$State.verifiedSourceScopeHash) { throw 'Evidence item is not bound to the current verified sourceScope.' }
        $itemUtc = [datetime]::Parse([string]$item.capturedUtc, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime().ToString('o')
        if (@('platform','adapter','skill','worker','human') -cnotcontains $candidateProducerType) { throw 'CandidateEvidence producerType is invalid.' }
        if ($null -ne $item.artifactPath -and $item.artifactPath -isnot [string]) { throw 'CandidateEvidence artifactPath must be a string or null.' }
        $artifactPath = if ($null -eq $item.artifactPath) { $null } else { [string]$item.artifactPath }
        $normalizedOutcome = 'unverified'
        $normalizedHash = Get-ESObjectHash ([ordered]@{claimId=[string]$item.claimId;candidateOutcome=$candidateOutcome;candidateEvidenceHash=$candidateEvidenceHash;candidateProducerType=$candidateProducerType;capturedUtc=$itemUtc;sourceScopeHash=[string]$item.sourceScopeHash})
        $normalizedProducerType = 'unverified'
        $artifactHash = $null
        $verifierId = $null
        $verifierDefinitionHash = $null
        $verificationStatus = 'unverified'
        $allVerifierBindings = @($State.acceptanceProfile.requiredVerifiers) + @($State.acceptanceProfile.optionalVerifiers)
        $verifierBinding = @($allVerifierBindings | Where-Object { [string]$_.claimId -ceq [string]$item.claimId }) | Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($artifactPath) -and $null -ne $verifierBinding) {
            $expectedTask=[pscustomobject][ordered]@{taskId=[string]$State.taskId;goalRevisionHash=[string]$State.goalRevisionHash;taskRevision=[int]$State.taskRevision;contextVersion=[int]$State.contextVersion;sessionId=if($null-eq$State.interactionSessionId){$null}else{[string]$State.interactionSessionId};createdUtc=[string]$State.createdUtc}
            $verified = ConvertTo-ESVerifiedArtifact $ProjectRoot $artifactPath ([string]$item.claimId) $candidateOutcome $candidateEvidenceHash ([string]$State.verifiedSourceScopeHash) @($State.verifiedSourceScope | ForEach-Object { [string]$_.path }) ([string]$verifierBinding.verifierId) ([string]$verifierBinding.verifierDefinitionHash) $expectedTask
            $normalizedOutcome = [string]$verified.outcome
            $normalizedHash = [string]$verified.evidenceHash
            $artifactHash = [string]$verified.artifactHash
            $verifierId = [string]$verified.verifierId
            $verifierDefinitionHash = [string]$verified.verifierDefinitionHash
            $verificationStatus = [string]$verified.verificationStatus
            $normalizedProducerType = 'platform'
            $artifactPath = [string]$verified.artifactPath
        }
        $items += [ordered]@{
            claimId=[string]$item.claimId; outcome=$normalizedOutcome; candidateOutcome=$candidateOutcome; capturedUtc=$itemUtc
            sourceScopeHash=[string]$item.sourceScopeHash; evidenceHash=$normalizedHash; candidateEvidenceHash=$candidateEvidenceHash
            producerType=$normalizedProducerType; candidateProducerType=$candidateProducerType
            verificationStatus=$verificationStatus; verifierId=$verifierId; verifierDefinitionHash=$verifierDefinitionHash; artifactHash=$artifactHash; artifactPath=$artifactPath
        }
        $claimIds += [string]$item.claimId
    }
    if (@($claimIds | Sort-Object -Unique).Count -ne @($claimIds).Count) { throw 'EvidenceSet contains duplicate claimId values.' }
    $contradictions = @($InputObject.contradictions | ForEach-Object {
        Assert-ESObjectShape $_ @('critical','description') @('critical','description') 'Evidence contradiction'
        if ($_.critical -isnot [bool] -or [string]::IsNullOrWhiteSpace([string]$_.description)) { throw 'Evidence contradiction fields are invalid.' }
        [ordered]@{ critical=[bool]$_.critical; description=[string]$_.description }
    })
    $sourceDrift = @($InputObject.sourceDrift | ForEach-Object {
        Assert-ESObjectShape $_ @('path','expectedHash','actualHash','resolved') @('path','expectedHash','actualHash','resolved') 'Evidence sourceDrift'
        if ([string]::IsNullOrWhiteSpace([string]$_.path) -or $_.resolved -isnot [bool]) { throw 'Evidence sourceDrift fields are invalid.' }
        Assert-ESHash ([string]$_.expectedHash) 'SourceDrift expectedHash'
        if ($null -ne $_.actualHash) { Assert-ESHash ([string]$_.actualHash) 'SourceDrift actualHash' }
        [ordered]@{ path=[string]$_.path; expectedHash=[string]$_.expectedHash; actualHash=if($null -eq $_.actualHash){$null}else{[string]$_.actualHash}; resolved=[bool]$_.resolved }
    })
    $unverified = @($InputObject.unverifiedClaims | ForEach-Object { Assert-ESSafeId ([string]$_) 'UnverifiedClaim'; [string]$_ } | Sort-Object -Unique)
    $core = [ordered]@{ schemaVersion=1; contractId=[string]$contractSnapshot.contractId; contractHash=[string]$contractSnapshot.contractHash; recordType='EvidenceSet'; inputContractMode=if($canonical){'canonical-v1'}else{'legacy-task-context-v1'}; taskId=$TaskId; evidenceSetId=[string]$InputObject.evidenceSetId; capturedUtc=$capturedUtc; items=$items; contradictions=$contradictions; sourceDrift=$sourceDrift; unverifiedClaims=$unverified }
    $result = [ordered]@{}
    foreach ($key in $core.Keys) { $result[$key] = $core[$key] }
    $result['evidenceSetHash'] = Get-ESObjectHash $core
    return [pscustomobject]$result
}

function Submit-ESTaskEvidenceSet {
    [CmdletBinding()]
    param(
        [string]$ProjectRoot='.', [string]$StoreRoot='ES/Output/TaskContextRuntime', [Parameter(Mandatory=$true)][string]$TaskId,
        [Parameter(Mandatory=$true)][string]$EvidenceSetPath,
        [Parameter(Mandatory=$true)][int]$ExpectedTaskRevision, [Parameter(Mandatory=$true)][int]$ExpectedContextVersion,
        [Parameter(Mandatory=$true)][string]$IdempotencyKey
    )
    Assert-ESSafeId $TaskId 'TaskId'; Assert-ESSafeId $IdempotencyKey 'IdempotencyKey'
    $paths = Resolve-ESRuntimePaths $ProjectRoot $StoreRoot $TaskId
    $evidenceFull = Resolve-ESProjectFile $paths.ProjectRoot $EvidenceSetPath
    $inputObject = Read-ESStrictJson $evidenceFull.Full
    $operationHash = Get-ESObjectHash ([ordered]@{operation='SubmitEvidence';taskId=$TaskId;evidenceInputHash=(Get-ESObjectHash $inputObject);expectedTaskRevision=$ExpectedTaskRevision;expectedContextVersion=$ExpectedContextVersion;idempotencyKey=$IdempotencyKey})
    Invoke-ESTaskMutex $TaskId {
        $events = Read-ESEventChain $paths -VerifyReceipts
        $duplicate = Find-ESIdempotentEvent $events $IdempotencyKey $operationHash
        if ($null -ne $duplicate) { return $duplicate.state }
        $previous = $events[-1]; Assert-ESCas $previous.state $ExpectedTaskRevision $ExpectedContextVersion
        if ($previous.state.taskStatus -ne 'Active' -or $previous.state.contextStatus -ne 'Live') { throw 'Evidence can be submitted only for an Active+Live task.' }
        if ([string]::IsNullOrWhiteSpace([string]$previous.state.verifiedSourceScopeHash)) { throw 'Evidence requires a verified sourceScope.' }
        $evidence = ConvertTo-ESEvidenceSet $inputObject $previous.state $TaskId $paths.ProjectRoot
        $state = Copy-ESObject $previous.state; $state.taskRevision=[int]$state.taskRevision+1; $state.evidenceSet=$evidence
        $event = Write-ESEvent $paths $previous $state 'EvidenceSubmitted' $IdempotencyKey ([pscustomobject]@{ operationHash=$operationHash; evidenceSetHash=$evidence.evidenceSetHash; sourcePath=$evidenceFull.Relative })
        return $event.state
    }
}

function Test-ESCurrentSourceScope {
    param($Paths, $State)
    $drift = @()
    foreach ($expected in @($State.verifiedSourceScope)) {
        $actualHash = $null
        try { $resolved = Resolve-ESProjectFile $Paths.ProjectRoot ([string]$expected.path); $actualHash=(Get-FileHash -LiteralPath $resolved.Full -Algorithm SHA256).Hash.ToLowerInvariant() } catch { }
        if ([string]$expected.sha256 -cne [string]$actualHash) { $drift += [ordered]@{ path=[string]$expected.path; expectedHash=[string]$expected.sha256; actualHash=$actualHash; resolved=$false } }
    }
    return ,$drift
}

function Get-ESCompletionEvaluation {
    param([string]$ProjectRoot, $State, $SourceDrift, [datetime]$EvaluationUtc=[DateTime]::UtcNow)
    $reasons = @()
    $rejected = $false
    if ($null -eq $State.PSObject.Properties['routePlan'] -or $null -eq $State.routePlan -or
        [string]::IsNullOrWhiteSpace([string]$State.routePlan.routePlanId) -or
        [string]$State.routePlan.routePlanHash -cnotmatch $script:Hex64Pattern -or
        [string]$State.planHash -cne [string]$State.routePlan.routePlanHash) {
        $reasons += 'RoutePlanBindingMissing'
    } else {
        try {
            $boundGoal = [pscustomobject]@{goalId=[string]$State.goalId;goalRevision=[string]$State.goalRevision;goalRevisionHash=[string]$State.goalRevisionHash;artifactHash=(Get-FileHash -LiteralPath (Resolve-ESProjectFile $ProjectRoot ([string]$State.goalRevisionPath)).Full -Algorithm SHA256).Hash.ToLowerInvariant();path=[string]$State.goalRevisionPath}
            $currentRoutePlan = Resolve-ESRoutePlan $ProjectRoot ([string]$State.routePlan.routePlanPath) $boundGoal
            $routeBindings = @(
                @('routePlanId',$currentRoutePlan.routePlanId), @('routePlanHash',$currentRoutePlan.routePlanHash),
                @('artifactHash',$currentRoutePlan.routePlanArtifactHash), @('snapshotHash',$currentRoutePlan.snapshotHash),
                @('profile',$currentRoutePlan.profile), @('routeState',$currentRoutePlan.routeState), @('head',$currentRoutePlan.head),
                @('sourceRefsHash',$currentRoutePlan.sourceRefsHash), @('registryHash',$currentRoutePlan.registryHash)
            )
            foreach ($binding in $routeBindings) {
                if ([string]$State.routePlan.($binding[0]) -cne [string]$binding[1]) { $reasons += 'RoutePlanDrift'; break }
            }
            if (-not ($reasons -contains 'RoutePlanDrift') -and
                (Get-ESObjectHash @($State.routePlan.routeKeys)) -cne (Get-ESObjectHash @($currentRoutePlan.routeKeys))) {
                $reasons += 'RoutePlanDrift'
            }
        } catch { $reasons += 'RoutePlanDrift' }
    }
    if ([string]::IsNullOrWhiteSpace([string]$State.goalId) -or [string]::IsNullOrWhiteSpace([string]$State.goalRevision) -or [string]$State.goalRevisionHash -notmatch $script:Hex64Pattern) { $reasons += 'GoalRevisionBindingMissing' }
    try {
        $currentGoal = Resolve-ESGoalRevision $ProjectRoot ([string]$State.goalRevisionPath)
        if ([string]$currentGoal.goalId -cne [string]$State.goalId -or [string]$currentGoal.goalRevision -cne [string]$State.goalRevision -or [string]$currentGoal.goalRevisionHash -cne [string]$State.goalRevisionHash) { $reasons += 'GoalRevisionDrift' }
    } catch { $reasons += 'GoalRevisionDrift' }
    if (-not [bool]$State.acceptanceProfile.frozen) { $reasons += 'AcceptanceProfileNotFrozen' }
    try {
        $currentEvaluationContract = Get-ESEvaluationRecordContractSnapshot
        if ([string]$State.acceptanceProfile.evaluationContractId -cne [string]$currentEvaluationContract.contractId -or [string]$State.acceptanceProfile.evaluationContractHash -cne [string]$currentEvaluationContract.contractHash) { $reasons += 'EvaluationContractDrift' }
    } catch { $reasons += 'EvaluationContractDrift' }
    try {
        $currentOutcomeEvaluator = Get-ESOutcomeEvaluatorDefinition ([string]$State.acceptanceProfile.outcomeEvaluatorId)
        if ([string]$currentOutcomeEvaluator.definitionHash -cne [string]$State.acceptanceProfile.outcomeEvaluatorDefinitionHash -or [string]$currentOutcomeEvaluator.definition.scopeType -cne 'task-object' -or [string]$State.acceptanceProfile.profileId -cnotmatch [string]$currentOutcomeEvaluator.definition.profileIdPattern) { $reasons += 'OutcomeEvaluatorDefinitionDrift' }
    } catch { $reasons += 'OutcomeEvaluatorDefinitionDrift' }
    try {
        $currentEvidenceContract = Get-ESPlatformEvidenceContractSnapshot
        if ([string]$State.acceptanceProfile.evidenceContractId -cne [string]$currentEvidenceContract.contractId -or [string]$State.acceptanceProfile.evidenceContractHash -cne [string]$currentEvidenceContract.contractHash) { $reasons += 'EvidenceContractDrift' }
    } catch { $reasons += 'EvidenceContractDrift' }
    if ([string]::IsNullOrWhiteSpace([string]$State.verifiedSourceScopeHash) -or @($State.verifiedSourceScope).Count -eq 0) { $reasons += 'VerifiedSourceScopeMissing' }
    if ($null -eq $State.evidenceSet) { $reasons += 'EvidenceSetMissing' }
    elseif ([string]$State.evidenceSet.contractId -cne [string]$State.acceptanceProfile.evidenceContractId -or [string]$State.evidenceSet.contractHash -cne [string]$State.acceptanceProfile.evidenceContractHash) { $reasons += 'EvidenceContractBindingMismatch' }
    $declaredDrift = if ($null -eq $State.evidenceSet) { @() } else { @($State.evidenceSet.sourceDrift | Where-Object { -not [bool]$_.resolved }) }
    $contradictions = if ($null -eq $State.evidenceSet) { @() } else { @($State.evidenceSet.contradictions | Where-Object { [bool]$_.critical }) }
    if (@($SourceDrift).Count -gt 0 -or @($declaredDrift).Count -gt 0) { $reasons += 'UnresolvedSourceDrift' }
    if ($null -ne $State.evidenceSet) {
        $expectedTask=[pscustomobject][ordered]@{taskId=[string]$State.taskId;goalRevisionHash=[string]$State.goalRevisionHash;taskRevision=[int]$State.taskRevision;contextVersion=[int]$State.contextVersion;sessionId=if($null-eq$State.interactionSessionId){$null}else{[string]$State.interactionSessionId};createdUtc=[string]$State.createdUtc}
        $artifactDrift = @(Test-ESEvidenceArtifacts $ProjectRoot $State.evidenceSet ([string]$State.verifiedSourceScopeHash) @($State.verifiedSourceScope | ForEach-Object { [string]$_.path }) @($State.acceptanceProfile.requiredClaims) $expectedTask)
        if ($artifactDrift.Count -gt 0) { $reasons += 'EvidenceArtifactDrift' }
    }
    if (@($contradictions).Count -gt 0) { $reasons += 'CriticalContradiction'; $rejected=$true }
    $required = @($State.acceptanceProfile.requiredClaims)
    foreach ($claim in $required) {
        $requiredVerifier = @($State.acceptanceProfile.requiredVerifiers | Where-Object { [string]$_.claimId -ceq $claim }) | Select-Object -First 1
        if($null-ne$requiredVerifier){
            try{$currentVerifier=Get-ESEvidenceVerifierDefinition ([string]$requiredVerifier.verifierId);if([string]$currentVerifier.definitionHash-cne[string]$requiredVerifier.verifierDefinitionHash){$reasons+="VerifierDefinitionDrift:$claim"}}
            catch{$reasons+="VerifierDefinitionDrift:$claim"}
        }
        $item = if ($null -eq $State.evidenceSet) { $null } else { @($State.evidenceSet.items | Where-Object { $_.claimId -ceq $claim }) | Select-Object -First 1 }
        if ($null -eq $item) { $reasons += "RequiredClaimMissing:$claim"; continue }
        if ($item.outcome -eq 'failed') { $reasons += "RequiredClaimFailed:$claim"; $rejected=$true; continue }
        if ($item.outcome -ne 'passed') { $reasons += "RequiredClaimUnverified:$claim"; continue }
        if ($null -eq $requiredVerifier -or [string]$item.verificationStatus -ne 'verified' -or [string]$item.verifierId -cne [string]$requiredVerifier.verifierId -or [string]$item.verifierDefinitionHash -cne [string]$requiredVerifier.verifierDefinitionHash) { $reasons += "RequiredClaimVerifierMismatch:$claim"; continue }
        $captured = [datetime]::Parse([string]$item.capturedUtc, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime()
        if (($EvaluationUtc.ToUniversalTime() - $captured).TotalHours -gt [int]$State.acceptanceProfile.maxEvidenceAgeHours -or $captured -gt $EvaluationUtc.ToUniversalTime().AddMinutes(5)) { $reasons += "RequiredClaimStale:$claim" }
        if ([string]$item.sourceScopeHash -cne [string]$State.verifiedSourceScopeHash) { $reasons += "RequiredClaimSourceMismatch:$claim" }
    }
    $allUnverified = if ($null -eq $State.evidenceSet) { @() } else { @($State.evidenceSet.unverifiedClaims) }
    $violatingUnverified = @($allUnverified | Where-Object { $required -contains $_ })
    if (@($violatingUnverified).Count -gt 0 -or (-not [bool]$State.acceptanceProfile.allowUnverifiedClaims -and @($allUnverified).Count -gt 0)) { $reasons += 'UnverifiedClaimsOutsideAcceptance' }
    [pscustomobject]@{ decision=if($rejected){'rejected'}elseif(@($reasons).Count -gt 0){'undetermined'}else{'accepted'}; reasons=@($reasons) }
}

function Get-ESEvaluationEvidenceState {
    param([string]$Decision, [string[]]$Reasons)
    if ($Decision -in @('accepted','rejected')) { return 'closed' }
    if (@($Reasons | Where-Object { $_ -match 'Drift|Stale|SourceMismatch' }).Count -gt 0) { return 'stale' }
    if (@($Reasons | Where-Object { $_ -match 'Missing' }).Count -gt 0) { return 'pending' }
    return 'partial'
}

function Get-ESEvaluationFailureRecord {
    param([string]$Reason, $State, [string]$Decision, [int]$Index)
    $parts = [regex]::Split($Reason,':',2)
    $base = $parts[0]
    $claim = if ($parts.Count -gt 1) { $parts[1] } else { $null }
    $descriptor = switch ($base) {
        'RoutePlanBindingMissing' { @('EVAL.ROUTE_PLAN_BINDING_MISSING','route-plan','routePlanHash','missing','event-chain','Create the task from a platform-verified RoutePlan artifact.') }
        'RoutePlanDrift' { @('EVAL.ROUTE_PLAN_DRIFT','route-plan','artifactHash','drift','route-plan-artifact','Re-plan and create a new task revision from the current immutable RoutePlan.') }
        'GoalRevisionBindingMissing' { @('EVAL.GOAL_REVISION_BINDING_MISSING','goal-revision','revisionHash','missing','event-chain','Restore the frozen GoalRevision binding and evaluate again.') }
        'GoalRevisionDrift' { @('EVAL.GOAL_REVISION_DRIFT','goal-revision','revisionHash','drift','event-chain','Restore or create the intended immutable GoalRevision, then create a new task revision.') }
        'AcceptanceProfileNotFrozen' { @('EVAL.ACCEPTANCE_PROFILE_NOT_FROZEN','acceptance-profile','frozen','policy','event-chain','Create a task with a frozen AcceptanceProfile.') }
        'EvaluationContractDrift' { @('EVAL.EVALUATION_CONTRACT_DRIFT','evaluation-contract','contractHash','drift','event-chain','Re-plan against the current EvaluationRecord contract.') }
        'OutcomeEvaluatorDefinitionDrift' { @('EVAL.OUTCOME_EVALUATOR_DRIFT','outcome-evaluator','definitionHash','drift','event-chain','Create a new task revision bound to the current registered evaluator definition.') }
        'EvidenceContractDrift' { @('EVAL.EVIDENCE_CONTRACT_DRIFT','evidence-contract','contractHash','drift','event-chain','Re-plan against the current Evidence contract.') }
        'VerifiedSourceScopeMissing' { @('EVAL.SOURCE_SCOPE_MISSING','source-scope','verifiedSourceScopeHash','missing','event-chain','Verify the task sourceScope before evaluation.') }
        'EvidenceSetMissing' { @('EVAL.EVIDENCE_SET_MISSING','evidence-set','evidenceSetHash','missing','event-chain','Submit CandidateEvidence and let the platform normalize it.') }
        'EvidenceContractBindingMismatch' { @('EVAL.EVIDENCE_CONTRACT_BINDING_MISMATCH','evidence-set','contractHash','drift','event-chain','Resubmit evidence through the frozen central contract.') }
        'UnresolvedSourceDrift' { @('EVAL.SOURCE_DRIFT','source-scope','sha256','drift','event-chain','Reverify sources and resubmit evidence.') }
        'EvidenceArtifactDrift' { @('EVAL.ARTIFACT_DRIFT','evidence-artifact','artifactHash','drift','event-chain','Regenerate and resubmit the candidate artifact.') }
        'CriticalContradiction' { @('EVAL.CRITICAL_CONTRADICTION','evidence-set','contradictions','contradiction','event-chain','Resolve the authoritative contradiction before completion.') }
        'VerifierDefinitionDrift' { @('EVAL.VERIFIER_DEFINITION_DRIFT',"claim:$claim",'verifierDefinitionHash','drift','event-chain','Create a new task revision bound to the current verifier definition.') }
        'RequiredClaimMissing' { @('EVAL.REQUIRED_CLAIM_MISSING',"claim:$claim",'claimId','missing','event-chain','Submit platform-verifiable evidence for the required claim.') }
        'RequiredClaimFailed' { @('EVAL.REQUIRED_CLAIM_FAILED',"claim:$claim",'outcome','required-failure','event-chain','Correct the underlying artifact or goal result, then resubmit evidence.') }
        'RequiredClaimUnverified' { @('EVAL.REQUIRED_CLAIM_UNVERIFIED',"claim:$claim",'verificationStatus','missing','event-chain','Use the registered verifier and resubmit a re-readable artifact.') }
        'RequiredClaimVerifierMismatch' { @('EVAL.REQUIRED_CLAIM_VERIFIER_MISMATCH',"claim:$claim",'verifierId','policy','event-chain','Resubmit through the verifier frozen by the AcceptanceProfile.') }
        'RequiredClaimStale' { @('EVAL.REQUIRED_CLAIM_STALE',"claim:$claim",'capturedUtc','stale','event-chain','Capture fresh evidence and evaluate again.') }
        'RequiredClaimSourceMismatch' { @('EVAL.REQUIRED_CLAIM_SOURCE_MISMATCH',"claim:$claim",'sourceScopeHash','drift','event-chain','Reverify sources and resubmit evidence for the current sourceScope.') }
        'UnverifiedClaimsOutsideAcceptance' { @('EVAL.UNVERIFIED_CLAIMS_OUTSIDE_ACCEPTANCE','evidence-set','unverifiedClaims','policy','event-chain','Verify or remove claims outside the frozen acceptance allowance.') }
        default { @('EVAL.UNCLASSIFIED_REASON','task-evaluation','reasons','policy','event-chain','Register a stable reason mapping before relying on this evaluation.') }
    }
    $failureSeed = Get-ESObjectHash ([ordered]@{taskId=[string]$State.taskId;taskRevision=[int]$State.taskRevision;reason=$Reason;index=$Index})
    [ordered]@{
        recordType = 'FailureRecord'
        failureId = 'failure-' + $failureSeed.Substring(0,16)
        code = $descriptor[0]
        object = $descriptor[1]
        field = $descriptor[2]
        profile = [string]$State.acceptanceProfile.profileId
        scope = 'task-object'
        classification = $descriptor[3]
        completionImpact = if($Decision -eq 'rejected'){'task-completion-block'}else{'claim-cap'}
        predicate = $Reason
        evidenceRefs = @($descriptor[4])
        recovery = $descriptor[5]
    }
}

function Get-ESEvaluationEvidenceRefs {
    param($Paths, $State, $Events)
    $refs = @([ordered]@{refId='event-chain';kind='event-chain';identity=[string]$Events[-1].eventId;hash=[string]$Events[-1].eventHash;path=$null})
    if ($null -ne $State.PSObject.Properties['routePlan'] -and $null -ne $State.routePlan) {
        $actualRoutePlanHash = $null
        try { $routePlanArtifact=Resolve-ESProjectFile $Paths.ProjectRoot ([string]$State.routePlan.routePlanPath);$actualRoutePlanHash=(Get-FileHash -LiteralPath $routePlanArtifact.Full -Algorithm SHA256).Hash.ToLowerInvariant() } catch { }
        $refs += [ordered]@{refId='route-plan-artifact';kind='artifact';identity=[string]$State.routePlan.routePlanId;hash=$actualRoutePlanHash;path=[string]$State.routePlan.routePlanPath}
    }
    foreach ($source in @($State.verifiedSourceScope)) {
        $actualHash = $null
        try { $resolved=Resolve-ESProjectFile $Paths.ProjectRoot ([string]$source.path);$actualHash=(Get-FileHash -LiteralPath $resolved.Full -Algorithm SHA256).Hash.ToLowerInvariant() } catch { }
        $refHash=Get-ESObjectHash ([string]$source.path)
        $refs += [ordered]@{refId='source-'+$refHash.Substring(0,16);kind='source';identity=[string]$source.path;hash=$actualHash;path=[string]$source.path}
    }
    if ($null -ne $State.evidenceSet) {
        $refs += [ordered]@{refId='evidence-set';kind='evidence-set';identity=[string]$State.evidenceSet.evidenceSetId;hash=[string]$State.evidenceSet.evidenceSetHash;path=$null}
        foreach ($item in @($State.evidenceSet.items | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.artifactPath) })) {
            $actualHash = $null
            try { $resolved=Resolve-ESProjectFile $Paths.ProjectRoot ([string]$item.artifactPath);$actualHash=(Get-FileHash -LiteralPath $resolved.Full -Algorithm SHA256).Hash.ToLowerInvariant() } catch { }
            $refHash=Get-ESObjectHash ([ordered]@{claimId=[string]$item.claimId;path=[string]$item.artifactPath})
            $refs += [ordered]@{refId='artifact-'+$refHash.Substring(0,16);kind='artifact';identity=[string]$item.claimId;hash=$actualHash;path=[string]$item.artifactPath}
        }
    }
    if ($null -ne $State.completionReceipt) {
        $refs += [ordered]@{refId='completion-receipt';kind='completion-receipt';identity=[string]$State.taskId;hash=[string]$State.completionReceipt.receiptHash;path=[string]$State.completionReceipt.path}
    }
    return $refs
}

function Get-ESEvaluationInfluenceRefs {
    param($State)
    $refs = @(
        [ordered]@{refId='goal-revision';kind='goal-revision';identity=([string]$State.goalId+':'+[string]$State.goalRevision);hash=[string]$State.goalRevisionHash;path=[string]$State.goalRevisionPath},
        [ordered]@{refId='acceptance-profile';kind='acceptance-profile';identity=[string]$State.acceptanceProfile.profileId;hash=[string]$State.acceptanceProfile.profileHash;path=$null},
        [ordered]@{refId='evidence-contract';kind='evidence-contract';identity=[string]$State.acceptanceProfile.evidenceContractId;hash=[string]$State.acceptanceProfile.evidenceContractHash;path=$null},
        [ordered]@{refId='outcome-evaluator';kind='outcome-evaluator';identity=[string]$State.acceptanceProfile.outcomeEvaluatorId;hash=[string]$State.acceptanceProfile.outcomeEvaluatorDefinitionHash;path=$null},
        [ordered]@{refId='route-plan';kind='route-plan';identity=[string]$State.routePlan.routePlanId;hash=[string]$State.routePlan.routePlanHash;path=[string]$State.routePlan.routePlanPath}
    )
    if (-not [string]::IsNullOrWhiteSpace([string]$State.verifiedSourceScopeHash)) { $refs += [ordered]@{refId='source-scope';kind='source-scope';identity=[string]$State.taskId;hash=[string]$State.verifiedSourceScopeHash;path=$null} }
    foreach ($binding in @(@($State.acceptanceProfile.requiredVerifiers)+@($State.acceptanceProfile.optionalVerifiers))) {
        $refHash=Get-ESObjectHash ([string]$binding.claimId)
        $refs += [ordered]@{refId='verifier-'+$refHash.Substring(0,16);kind='evidence-verifier';identity=([string]$binding.claimId+':'+[string]$binding.verifierId);hash=[string]$binding.verifierDefinitionHash;path=$null}
    }
    return $refs
}

function New-ESEvaluationRecordCore {
    param($Paths, $Events, $State, [ValidateSet('advisory','completion')][string]$Purpose, [string]$RequestHash, [string]$EvaluationId)
    Assert-ESHash $RequestHash 'EvaluationRequestHash'
    if ($EvaluationId -notmatch '^[a-f0-9]{32}$') { throw 'EvaluationId must be a lowercase 32-character hash identity.' }
    $contract = Get-ESEvaluationRecordContractSnapshot
    $evaluator = Get-ESOutcomeEvaluatorDefinition ([string]$State.acceptanceProfile.outcomeEvaluatorId)
    $evaluatedUtc = [DateTime]::UtcNow
    $drift = Test-ESCurrentSourceScope $Paths $State
    $evaluation = Get-ESCompletionEvaluation $Paths.ProjectRoot $State $drift $evaluatedUtc
    $evidenceRefs = @(Get-ESEvaluationEvidenceRefs $Paths $State $Events)
    $influenceRefs = @(Get-ESEvaluationInfluenceRefs $State)
    $trajectory = [ordered]@{
        recordType='TrajectoryRecord';eventCount=@($Events).Count;firstEventId=[string]$Events[0].eventId;lastEventId=[string]$Events[-1].eventId
        lastEventHash=[string]$Events[-1].eventHash;eventTypes=@($Events | ForEach-Object {[string]$_.eventType});taskStatus=[string]$State.taskStatus;contextStatus=[string]$State.contextStatus
    }
    $snapshot = [ordered]@{
        evaluatedUtc=$evaluatedUtc.ToUniversalTime().ToString('o');taskId=[string]$State.taskId;taskRevision=[int]$State.taskRevision;contextVersion=[int]$State.contextVersion
        planHash=[string]$State.planHash;routePlanArtifactHash=[string]$State.routePlan.artifactHash;routePlanSnapshotHash=[string]$State.routePlan.snapshotHash;goalRevisionHash=[string]$State.goalRevisionHash;acceptanceProfileHash=[string]$State.acceptanceProfile.profileHash
        evidenceSetHash=if($null-eq$State.evidenceSet){$null}else{[string]$State.evidenceSet.evidenceSetHash};verifiedSourceScopeHash=if($null-eq$State.verifiedSourceScopeHash){$null}else{[string]$State.verifiedSourceScopeHash}
        evaluatorDefinitionHash=[string]$evaluator.definitionHash;trajectory=$trajectory;influenceRefs=$influenceRefs;evidenceRefs=$evidenceRefs
    }
    $inputSnapshotHash = Get-ESObjectHash $snapshot
    $assertions = @()
    foreach ($claim in @($State.acceptanceProfile.requiredClaims)) {
        $item = if($null-eq$State.evidenceSet){$null}else{@($State.evidenceSet.items|Where-Object{[string]$_.claimId-ceq[string]$claim})|Select-Object -First 1}
        $binding=@($State.acceptanceProfile.requiredVerifiers|Where-Object{[string]$_.claimId-ceq[string]$claim})|Select-Object -First 1
        $assertions += [ordered]@{recordType='OutcomeAssertion';assertionId='claim-'+(Get-ESObjectHash ([string]$claim)).Substring(0,16);claimId=[string]$claim;verifierId=if($null-eq$binding){$null}else{[string]$binding.verifierId};outcome=if($null-eq$item){'unverified'}else{[string]$item.outcome};evidenceHash=if($null-eq$item){$null}else{[string]$item.evidenceHash};sourceScopeHash=if($null-eq$item){$null}else{[string]$item.sourceScopeHash}}
    }
    foreach($claim in @($State.acceptanceProfile.optionalClaims)){
        $item=if($null-eq$State.evidenceSet){$null}else{@($State.evidenceSet.items|Where-Object{[string]$_.claimId-ceq[string]$claim})|Select-Object -First 1}
        $binding=@($State.acceptanceProfile.optionalVerifiers|Where-Object{[string]$_.claimId-ceq[string]$claim})|Select-Object -First 1
        $assertions += [ordered]@{recordType='OutcomeAssertion';assertionId='claim-'+(Get-ESObjectHash ([string]$claim)).Substring(0,16);claimId=[string]$claim;verifierId=if($null-eq$binding){$null}else{[string]$binding.verifierId};outcome=if($null-eq$item){'unverified'}else{[string]$item.outcome};evidenceHash=if($null-eq$item){$null}else{[string]$item.evidenceHash};sourceScopeHash=if($null-eq$item){$null}else{[string]$item.sourceScopeHash}}
    }
    $assertions += [ordered]@{recordType='OutcomeAssertion';assertionId='task-completion';claimId='task-completion';verifierId=[string]$State.acceptanceProfile.outcomeEvaluatorId;outcome=if($evaluation.decision-eq'accepted'){'passed'}elseif($evaluation.decision-eq'rejected'){'failed'}else{'unverified'};evidenceHash=if($null-eq$State.evidenceSet){$null}else{[string]$State.evidenceSet.evidenceSetHash};sourceScopeHash=if($null-eq$State.verifiedSourceScopeHash){$null}else{[string]$State.verifiedSourceScopeHash}}
    $failures = @()
    for ($index=0;$index-lt@($evaluation.reasons).Count;$index++) { $failures += Get-ESEvaluationFailureRecord ([string]$evaluation.reasons[$index]) $State ([string]$evaluation.decision) $index }
    $evidenceRefIds=@($evidenceRefs|ForEach-Object{[string]$_.refId})
    $influenceRefIds=@($influenceRefs|ForEach-Object{[string]$_.refId})
    $assertionIds=@($assertions|ForEach-Object{[string]$_.assertionId})
    if(@($evidenceRefIds|Sort-Object -Unique).Count-ne$evidenceRefIds.Count-or@($influenceRefIds|Sort-Object -Unique).Count-ne$influenceRefIds.Count-or@($assertionIds|Sort-Object -Unique).Count-ne$assertionIds.Count){throw 'EvaluationRecord contains duplicate stable identities.'}
    foreach($failure in $failures){if([string]$failure.scope-cne'task-object'){throw 'Evaluation failure scope expanded beyond task-object.'};foreach($refId in @($failure.evidenceRefs)){if($evidenceRefIds-cnotcontains[string]$refId){throw "Evaluation failure references unknown evidence: $refId"}}}
    if($evaluation.decision-eq'accepted'-and$failures.Count-ne0){throw 'Accepted evaluation cannot contain failure records.'}
    if($evaluation.decision-ne'accepted'-and$failures.Count-eq0){throw 'Non-accepted evaluation must contain a failure record.'}
    $base = [ordered]@{
        schemaVersion=1;contractId=[string]$contract.contractId;contractHash=[string]$contract.contractHash;recordType='EvaluationRecord';evaluationId=$EvaluationId;purpose=$Purpose;requestHash=$RequestHash
        evaluatorId=[string]$State.acceptanceProfile.outcomeEvaluatorId;evaluatorDefinitionHash=[string]$evaluator.definitionHash;taskId=[string]$State.taskId;taskRevision=[int]$State.taskRevision;contextVersion=[int]$State.contextVersion
        planHash=[string]$State.planHash;goalRevisionHash=[string]$State.goalRevisionHash;acceptanceProfileId=[string]$State.acceptanceProfile.profileId;acceptanceProfileHash=[string]$State.acceptanceProfile.profileHash
        evidenceContractId=[string]$State.acceptanceProfile.evidenceContractId;evidenceContractHash=[string]$State.acceptanceProfile.evidenceContractHash;evidenceSetHash=if($null-eq$State.evidenceSet){$null}else{[string]$State.evidenceSet.evidenceSetHash}
        verifiedSourceScopeHash=if($null-eq$State.verifiedSourceScopeHash){$null}else{[string]$State.verifiedSourceScopeHash};evaluatedUtc=$snapshot.evaluatedUtc;decision=[string]$evaluation.decision;decisionScope='task-object'
        evidenceState=Get-ESEvaluationEvidenceState ([string]$evaluation.decision) @($evaluation.reasons);inputSnapshotHash=$inputSnapshotHash;trajectoryRecord=$trajectory;outcomeAssertions=$assertions;failureRecords=$failures
        influenceRefs=$influenceRefs;evidenceRefs=$evidenceRefs;nonClaims=@('Automation Accepted is not TaskContext completion','Static evidence does not prove Runtime or Release','This EvaluationRecord does not mutate task or context lifecycle')
    }
    $record=[ordered]@{};foreach($key in $base.Keys){$record[$key]=$base[$key]};$record.recordHash=Get-ESObjectHash $base
    $relative="$($State.taskId)/evaluations/$EvaluationId.json"
    $full=Join-Path $Paths.StoreRoot ($relative.Replace('/',[IO.Path]::DirectorySeparatorChar))
    if(Test-Path -LiteralPath $full -PathType Leaf){
        $existing=Test-ESBoundEvaluationRecord $Paths $relative ([string](Read-ESStrictJson $full).recordHash)
        if([string]$existing.requestHash-cne$RequestHash-or[string]$existing.purpose-cne$Purpose){throw 'Evaluation idempotency identity is already bound to a different request.'}
        return [pscustomobject]@{record=$existing;path=$relative}
    }
    Write-ESCreateOnlyJson $full $record
    $verified=Test-ESBoundEvaluationRecord $Paths $relative ([string]$record.recordHash)
    return [pscustomobject]@{record=$verified;path=$relative}
}

function New-ESTaskEvaluationRecord {
    [CmdletBinding()]
    param(
        [string]$ProjectRoot='.',[string]$StoreRoot='ES/Output/TaskContextRuntime',[Parameter(Mandatory=$true)][string]$TaskId,
        [Parameter(Mandatory=$true)][string]$ContractId,[Parameter(Mandatory=$true)][string]$ContractHash,
        [Parameter(Mandatory=$true)][int]$ExpectedTaskRevision,[Parameter(Mandatory=$true)][int]$ExpectedContextVersion,
        [Parameter(Mandatory=$true)][string]$IdempotencyKey
    )
    Assert-ESSafeId $TaskId 'TaskId';Assert-ESSafeId $IdempotencyKey 'IdempotencyKey';Assert-ESHash $ContractHash 'EvaluationContractHash'
    $contract=Get-ESEvaluationRecordContractSnapshot
    if($ContractId-cne[string]$contract.contractId-or$ContractHash-cne[string]$contract.contractHash){throw 'EvaluationRequest contract binding does not match the platform contract.'}
    $paths=Resolve-ESRuntimePaths $ProjectRoot $StoreRoot $TaskId
    $requestHash=Get-ESObjectHash ([ordered]@{operation='Evaluate';contractId=$ContractId;contractHash=$ContractHash;taskId=$TaskId;expectedTaskRevision=$ExpectedTaskRevision;expectedContextVersion=$ExpectedContextVersion;idempotencyKey=$IdempotencyKey})
    $evaluationId=(Get-ESObjectHash ([ordered]@{taskId=$TaskId;purpose='advisory';idempotencyKey=$IdempotencyKey})).Substring(0,32)
    Invoke-ESTaskMutex $TaskId {
        $events=Read-ESEventChain $paths -VerifyReceipts
        $state=$events[-1].state;Assert-ESCas $state $ExpectedTaskRevision $ExpectedContextVersion
        return (New-ESEvaluationRecordCore $paths $events $state 'advisory' $requestHash $evaluationId).record
    }
}

function Complete-ESTaskContextTask {
    [CmdletBinding()]
    param(
        [string]$ProjectRoot='.', [string]$StoreRoot='ES/Output/TaskContextRuntime', [Parameter(Mandatory=$true)][string]$TaskId,
        [Parameter(Mandatory=$true)][int]$ExpectedTaskRevision, [Parameter(Mandatory=$true)][int]$ExpectedContextVersion,
        [Parameter(Mandatory=$true)][string]$IdempotencyKey
    )
    Assert-ESSafeId $TaskId 'TaskId'; Assert-ESSafeId $IdempotencyKey 'IdempotencyKey'
    $paths = Resolve-ESRuntimePaths $ProjectRoot $StoreRoot $TaskId
    $operationHash = Get-ESObjectHash ([ordered]@{operation='Complete';taskId=$TaskId;expectedTaskRevision=$ExpectedTaskRevision;expectedContextVersion=$ExpectedContextVersion;idempotencyKey=$IdempotencyKey})
    Invoke-ESTaskMutex $TaskId {
        $events=Read-ESEventChain $paths -VerifyReceipts
        $duplicate=Find-ESIdempotentEvent $events $IdempotencyKey $operationHash
        if($null -ne $duplicate){return $duplicate.state}
        $previous=$events[-1];Assert-ESCas $previous.state $ExpectedTaskRevision $ExpectedContextVersion
        if($previous.state.taskStatus -ne 'Active' -or $previous.state.contextStatus -ne 'Live'){throw 'Completion evaluation requires Active+Live.'}
        $evaluationId=[Guid]::NewGuid().ToString('N')
        $evaluationResult=New-ESEvaluationRecordCore $paths $events $previous.state 'completion' $operationHash $evaluationId
        $evaluation=$evaluationResult.record
        $drift=Test-ESCurrentSourceScope $paths $previous.state
        $state=Copy-ESObject $previous.state;$state.taskRevision=[int]$state.taskRevision+1;$state.completionDecision=$evaluation.decision
        $eventType='CompletionUndetermined'
        if($evaluation.decision -eq 'accepted'){
            $state.taskStatus='Completed';$state.contextStatus='Frozen';$state.contextVersion=[int]$state.contextVersion+1;$state.deliveryAcceptance='pending'
            $receiptBase=[ordered]@{schemaVersion=1;receiptId=[Guid]::NewGuid().ToString('N');taskId=$TaskId;taskRevision=[int]$state.taskRevision;contextVersion=[int]$state.contextVersion;planHash=[string]$state.planHash;routePlanId=[string]$state.routePlan.routePlanId;routePlanPath=[string]$state.routePlan.routePlanPath;routePlanHash=[string]$state.routePlan.routePlanHash;routePlanArtifactHash=[string]$state.routePlan.artifactHash;routePlanSnapshotHash=[string]$state.routePlan.snapshotHash;goalRevisionHash=[string]$state.goalRevisionHash;acceptanceProfileHash=[string]$state.acceptanceProfile.profileHash;evidenceContractId=[string]$state.acceptanceProfile.evidenceContractId;evidenceContractHash=[string]$state.acceptanceProfile.evidenceContractHash;evaluationId=[string]$evaluation.evaluationId;evaluationRecordPath=[string]$evaluationResult.path;evaluationRecordHash=[string]$evaluation.recordHash;evidenceSetHash=[string]$state.evidenceSet.evidenceSetHash;verifiedSourceScopeHash=[string]$state.verifiedSourceScopeHash;completionDecision='accepted';issuedUtc=[DateTime]::UtcNow.ToString('o')}
            $receipt=[ordered]@{};foreach($key in $receiptBase.Keys){$receipt[$key]=$receiptBase[$key]};$receipt['receiptHash']=Get-ESObjectHash $receiptBase
            $receiptRelative="$TaskId/receipts/$($receiptBase.receiptId).json";$receiptFull=Join-Path $paths.StoreRoot ($receiptRelative.Replace('/',[IO.Path]::DirectorySeparatorChar))
            Write-ESCreateOnlyJson $receiptFull $receipt
            $state.completionReceipt=[pscustomobject][ordered]@{path=$receiptRelative;receiptHash=$receipt.receiptHash}
            $eventType='CompletionAccepted'
        }elseif($evaluation.decision -eq 'rejected'){$state.taskStatus='Blocked';$eventType='CompletionRejected'}
        elseif(@($drift).Count -gt 0){$state.contextStatus='PartiallyInvalidated';$state.contextVersion=[int]$state.contextVersion+1;$eventType='SourceDriftDetected'}
        $event=Write-ESEvent $paths $previous $state $eventType $IdempotencyKey ([pscustomobject]@{operationHash=$operationHash;decision=$evaluation.decision;reasons=@($evaluation.failureRecords|ForEach-Object{[string]$_.predicate});sourceDrift=@($drift);evaluationId=[string]$evaluation.evaluationId;evaluationRecordPath=[string]$evaluationResult.path;evaluationRecordHash=[string]$evaluation.recordHash})
        if($evaluation.decision -eq 'accepted'){Test-ESBoundReceipt $paths $event.state|Out-Null}
        return $event.state
    }
}

function Set-ESTaskDeliveryAcceptance {
    [CmdletBinding()]
    param(
        [string]$ProjectRoot='.', [string]$StoreRoot='ES/Output/TaskContextRuntime', [Parameter(Mandatory=$true)][string]$TaskId,
        [ValidateSet('accepted','rejected')][string]$DeliveryAcceptance,
        [Parameter(Mandatory=$true)][int]$ExpectedTaskRevision, [Parameter(Mandatory=$true)][int]$ExpectedContextVersion,
        [Parameter(Mandatory=$true)][string]$IdempotencyKey
    )
    Assert-ESSafeId $TaskId 'TaskId';Assert-ESSafeId $IdempotencyKey 'IdempotencyKey'
    $paths=Resolve-ESRuntimePaths $ProjectRoot $StoreRoot $TaskId
    $operationHash=Get-ESObjectHash ([ordered]@{operation='SetDelivery';taskId=$TaskId;deliveryAcceptance=$DeliveryAcceptance;expectedTaskRevision=$ExpectedTaskRevision;expectedContextVersion=$ExpectedContextVersion;idempotencyKey=$IdempotencyKey})
    Invoke-ESTaskMutex $TaskId {
        $events=Read-ESEventChain $paths -VerifyReceipts;$duplicate=Find-ESIdempotentEvent $events $IdempotencyKey $operationHash;if($null-ne$duplicate){return $duplicate.state}
        $previous=$events[-1];Assert-ESCas $previous.state $ExpectedTaskRevision $ExpectedContextVersion
        if($previous.state.taskStatus-ne'Completed'-or$previous.state.contextStatus-ne'Frozen'-or$previous.state.completionDecision-ne'accepted'){throw 'Delivery acceptance requires an accepted Completed+Frozen task.'}
        if($previous.state.deliveryAcceptance-ne'pending'){throw 'Delivery acceptance is final; reopen or create a follow-up task for further delivery feedback.'}
        $state=Copy-ESObject $previous.state;$state.taskRevision=[int]$state.taskRevision+1;$state.deliveryAcceptance=$DeliveryAcceptance
        $event=Write-ESEvent $paths $previous $state 'DeliveryAcceptanceChanged' $IdempotencyKey ([pscustomobject]@{operationHash=$operationHash;deliveryAcceptance=$DeliveryAcceptance})
        return $event.state
    }
}

function Invoke-ESTaskContextTransition {
    [CmdletBinding()]
    param(
        [string]$ProjectRoot='.', [string]$StoreRoot='ES/Output/TaskContextRuntime', [Parameter(Mandatory=$true)][string]$TaskId,
        [ValidateSet('Suspend','Resume','Cancel','Invalidate','BeginCompaction','EndCompaction','Archive','Expire','Quarantine','Recover','Reopen')][string]$Transition,
        [Parameter(Mandatory=$true)][int]$ExpectedTaskRevision,[Parameter(Mandatory=$true)][int]$ExpectedContextVersion,
        [Parameter(Mandatory=$true)][string]$IdempotencyKey
    )
    Assert-ESSafeId $TaskId 'TaskId';Assert-ESSafeId $IdempotencyKey 'IdempotencyKey'
    $paths=Resolve-ESRuntimePaths $ProjectRoot $StoreRoot $TaskId
    $operationHash=Get-ESObjectHash ([ordered]@{operation='Transition';taskId=$TaskId;transition=$Transition;expectedTaskRevision=$ExpectedTaskRevision;expectedContextVersion=$ExpectedContextVersion;idempotencyKey=$IdempotencyKey})
    Invoke-ESTaskMutex $TaskId {
        $events=Read-ESEventChain $paths -VerifyReceipts;$duplicate=Find-ESIdempotentEvent $events $IdempotencyKey $operationHash;if($null-ne$duplicate){return $duplicate.state}
        $previous=$events[-1];Assert-ESCas $previous.state $ExpectedTaskRevision $ExpectedContextVersion;$state=Copy-ESObject $previous.state;$state.taskRevision=[int]$state.taskRevision+1;$eventType=$null
        switch($Transition){
            'Suspend'{$state.taskStatus='Suspended';$eventType='Suspended'}
            'Resume'{$state.taskStatus='Active';$eventType='Resumed'}
            'Cancel'{$state.taskStatus='Cancelled';$state.contextStatus='Frozen';$state.contextVersion=[int]$state.contextVersion+1;$eventType='Cancelled'}
            'Invalidate'{$state.taskStatus='Invalidated';$state.contextStatus='PartiallyInvalidated';$state.contextVersion=[int]$state.contextVersion+1;$eventType='Invalidated'}
            'BeginCompaction'{$state.contextStatus='Compacting';$state.contextVersion=[int]$state.contextVersion+1;$eventType='CompactionStarted'}
            'EndCompaction'{$state.contextStatus='Live';$state.contextVersion=[int]$state.contextVersion+1;$eventType='CompactionEnded'}
            'Archive'{$state.contextStatus='Archived';$state.contextVersion=[int]$state.contextVersion+1;$eventType='Archived'}
            'Expire'{$state.contextStatus='Expired';$state.contextVersion=[int]$state.contextVersion+1;$eventType='Expired'}
            'Quarantine'{$recoveryContext=[string]$state.contextStatus;$state.contextStatus='Quarantined';$state.contextVersion=[int]$state.contextVersion+1;$eventType='Quarantined'}
            'Recover'{$recoveryContext=if($null-ne$previous.metadata.PSObject.Properties['recoveryContextStatus']){[string]$previous.metadata.recoveryContextStatus}elseif($state.taskStatus-eq'Completed'){'Frozen'}else{'Live'};if($recoveryContext-eq'Quarantined'-or$script:ContextStatuses-notcontains$recoveryContext){throw 'Quarantined context has no compatible recovery target.'};$state.contextStatus=$recoveryContext;$state.contextVersion=[int]$state.contextVersion+1;$eventType='Recovered'}
            'Reopen'{$state.taskStatus='Active';$state.contextStatus='Live';$state.contextVersion=[int]$state.contextVersion+1;$state.completionDecision='undetermined';$state.deliveryAcceptance='pending';$state.evidenceSet=$null;$state.completionReceipt=$null;$eventType='Reopened'}
        }
        $event=Write-ESEvent $paths $previous $state $eventType $IdempotencyKey ([pscustomobject]@{operationHash=$operationHash;transition=$Transition;recoveryContextStatus=if($Transition-in@('Quarantine','Recover')){$recoveryContext}else{$null}})
        return $event.state
    }
}

function Get-ESTaskCommercialObservation {
    [CmdletBinding()]
    param(
        [string]$ProjectRoot='.',
        [string]$StoreRoot='ES/Output/TaskContextRuntime',
        [Parameter(Mandatory=$true)][string]$TaskId
    )
    Assert-ESSafeId $TaskId 'TaskId'
    $paths=Resolve-ESRuntimePaths $ProjectRoot $StoreRoot $TaskId
    $events=@(Read-ESEventChain $paths -VerifyReceipts)
    if($events.Count-eq1-and$events[0]-is[Array]){$events=@($events[0])}
    $state=$events[-1].state
    $contract=Get-ESEvaluationRecordContractSnapshot
    $evaluationRecords=@()
    $evaluationFiles=@(if(Test-Path -LiteralPath $paths.EvaluationsRoot -PathType Container){Get-ChildItem -LiteralPath $paths.EvaluationsRoot -File -Filter '*.json'}else{@()})
    foreach($file in $evaluationFiles){
        $raw=Read-ESStrictJson $file.FullName
        $relative="$TaskId/evaluations/$($file.Name)"
        $record=Test-ESBoundEvaluationRecord $paths $relative ([string]$raw.recordHash)
        if([string]$record.taskId-cne$TaskId){throw 'Commercial observation EvaluationRecord task identity mismatch.'}
        if([string]$record.contractHash-cne[string]$contract.contractHash){throw 'Commercial observation EvaluationRecord contract drift.'}
        if([string]$record.decisionScope-cne'task-object'){throw 'Commercial observation EvaluationRecord scope expanded beyond task-object.'}
        $evaluationRecords+=$record
    }
    function ConvertTo-ESCommercialTimestamp([AllowNull()]$Value,[string]$Name){
        try{return [DateTimeOffset]::Parse([string]$Value,[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::RoundtripKind)}
        catch{throw "Commercial observation $Name timestamp is invalid: '$Value'."}
    }
    $evaluationRecords=@($evaluationRecords|Sort-Object @{Expression={ConvertTo-ESCommercialTimestamp $_.evaluatedUtc 'EvaluationRecord.evaluatedUtc'}},@{Expression={[string]$_.evaluationId}})
    $latest=if($evaluationRecords.Count){$evaluationRecords[-1]}else{$null}
    $firstEventUtc=ConvertTo-ESCommercialTimestamp $events[0].occurredUtc 'first event occurredUtc'
    $lastObservedUtc=if($null-ne$latest){ConvertTo-ESCommercialTimestamp $latest.evaluatedUtc 'latest EvaluationRecord.evaluatedUtc'}else{ConvertTo-ESCommercialTimestamp $events[-1].occurredUtc 'last event occurredUtc'}
    $latencyMs=if($null-ne$latest){[long][Math]::Max(0,[Math]::Round(($lastObservedUtc-$firstEventUtc).TotalMilliseconds,0,[MidpointRounding]::AwayFromZero))}else{$null}
    $hardViolationCodes=@($evaluationRecords|ForEach-Object{@($_.failureRecords)}|Where-Object{[string]$_.completionImpact-ceq'task-completion-block'}|ForEach-Object{[string]$_.code}|Sort-Object -Unique)
    $priorNonAccepted=@(if($evaluationRecords.Count-gt1){$evaluationRecords[0..($evaluationRecords.Count-2)]|Where-Object{[string]$_.decision-cne'accepted'}}else{@()})
    $recoveryEligible=$priorNonAccepted.Count-gt0
    $recoveryObserved=$recoveryEligible -and $null -ne $latest -and [string]$latest.decision -ceq 'accepted'
    $regressionAssertions=@(if($null-ne$latest){@($latest.outcomeAssertions|Where-Object{[string]$_.verifierId-ceq'platform.static-replay-v1'-and[string]$_.claimId-cmatch'^regression(?:[._-]|$)'})}else{@()})
    $regressionFailureCount=@($regressionAssertions|Where-Object{[string]$_.outcome-ceq'failed'}).Count
    $regressionObserved=$regressionAssertions.Count-gt0-and@($regressionAssertions|Where-Object{[string]$_.outcome-ceq'unverified'}).Count-eq0
    $regressionPassed=$regressionObserved-and$regressionFailureCount-eq0
    $correctionObservationClosed=$false
    $humanCorrectionObserved=$null
    $correctionCount=$null
    $correctionObservationHashes=@()
    $correctionItems=@(if($null-ne$state.evidenceSet){@($state.evidenceSet.items|Where-Object{[string]$_.verifierId-ceq'platform.codex-transcript-slice-v1'-and[string]$_.claimId-cmatch'^interaction-correction(?:[._-]|$)'})}else{@()})
    if($correctionItems.Count-gt0){
        $verifiedCorrectionObservations=@()
        $correctionVerificationFailed=$false
        $expectedTask=[pscustomobject][ordered]@{taskId=[string]$state.taskId;goalRevisionHash=[string]$state.goalRevisionHash;taskRevision=[int]$state.taskRevision;contextVersion=[int]$state.contextVersion;sessionId=if($null-eq$state.interactionSessionId){$null}else{[string]$state.interactionSessionId};createdUtc=[string]$state.createdUtc;allowHistoricalRevision=$true}
        foreach($item in $correctionItems){
            try{
                $verified=ConvertTo-ESVerifiedArtifact $paths.ProjectRoot ([string]$item.artifactPath) ([string]$item.claimId) ([string]$item.candidateOutcome) ([string]$item.candidateEvidenceHash) ([string]$state.verifiedSourceScopeHash) @($state.verifiedSourceScope|ForEach-Object{[string]$_.path}) ([string]$item.verifierId) ([string]$item.verifierDefinitionHash) $expectedTask
                if([string]$verified.artifactHash-cne[string]$item.artifactHash-or[string]$verified.evidenceHash-cne[string]$item.evidenceHash-or[string]$verified.outcome-cne[string]$item.outcome-or$null-eq$verified.observation){throw 'Correction observation evidence binding drifted.'}
                $verifiedCorrectionObservations+=$verified.observation
                $correctionObservationHashes+=[string]$verified.evidenceHash
            }catch{$correctionVerificationFailed=$true;break}
        }
        if(-not$correctionVerificationFailed-and$verifiedCorrectionObservations.Count-eq$correctionItems.Count){
            $correctionObservationClosed=$true
            $humanCorrectionObserved=@($verifiedCorrectionObservations|Where-Object{[bool]$_.correctionObserved}).Count-gt0
            $correctionCount=[int](($verifiedCorrectionObservations|Measure-Object -Property correctionCount -Sum).Sum)
        }
    }
    $sourceSnapshot=[ordered]@{
        taskId=$TaskId
        eventHashes=@($events|ForEach-Object{[string]$_.eventHash})
        evaluationHashes=@($evaluationRecords|ForEach-Object{[string]$_.recordHash})
        correctionObservationHashes=$correctionObservationHashes
        correctionObservationClosed=$correctionObservationClosed
        taskRevision=[int]$state.taskRevision
        contextVersion=[int]$state.contextVersion
    }
    $base=[ordered]@{
        schemaVersion=1
        recordType='CommercialTaskObservation'
        scope='task-object'
        taskId=$TaskId
        goalRevisionHash=[string]$state.goalRevisionHash
        profileId=[string]$state.acceptanceProfile.profileId
        taskRevision=[int]$state.taskRevision
        contextVersion=[int]$state.contextVersion
        taskStatus=[string]$state.taskStatus
        contextStatus=[string]$state.contextStatus
        latestDecision=if($null-eq$latest){$null}else{[string]$latest.decision}
        latestEvaluationId=if($null-eq$latest){$null}else{[string]$latest.evaluationId}
        evaluationCount=$evaluationRecords.Count
        firstEventUtc=$firstEventUtc.ToUniversalTime().ToString('o')
        lastObservedUtc=$lastObservedUtc.ToUniversalTime().ToString('o')
        evaluationLatencyMs=$latencyMs
        hardViolationObserved=$hardViolationCodes.Count -gt 0
        hardViolationCodes=$hardViolationCodes
        recoveryEligible=$recoveryEligible
        recoveryObserved=$recoveryObserved
        regressionObserved=$regressionObserved
        regressionPassed=$regressionPassed
        regressionClaimCount=$regressionAssertions.Count
        regressionFailureCount=$regressionFailureCount
        correctionObservationClosed=$correctionObservationClosed
        humanCorrectionObserved=$humanCorrectionObserved
        correctionCount=$correctionCount
        sourceSnapshotHash=Get-ESObjectHash $sourceSnapshot
    }
    $result=[ordered]@{};foreach($key in $base.Keys){$result[$key]=$base[$key]};$result.observationHash=Get-ESObjectHash $base
    return [pscustomobject]$result
}

function Test-ESTaskContextIntegrity {
    [CmdletBinding()]
    param([string]$ProjectRoot='.',[string]$StoreRoot='ES/Output/TaskContextRuntime',[Parameter(Mandatory=$true)][string]$TaskId)
    Assert-ESSafeId $TaskId 'TaskId';$paths=Resolve-ESRuntimePaths $ProjectRoot $StoreRoot $TaskId
    try{
        $events=Read-ESEventChain $paths -VerifyReceipts
        $bound=@($events|Where-Object eventType -eq 'CompletionAccepted'|ForEach-Object{$_.state.completionReceipt.path})
        $boundEvaluations=@($events|Where-Object{$null-ne$_.metadata.PSObject.Properties['evaluationRecordPath']}|ForEach-Object{[string]$_.metadata.evaluationRecordPath})
        $allReceipts=@(if(Test-Path -LiteralPath $paths.ReceiptsRoot){Get-ChildItem -LiteralPath $paths.ReceiptsRoot -File -Filter '*.json'})
        $orphanCount=@($allReceipts|Where-Object{("$TaskId/receipts/$($_.Name)") -notin $bound}).Count
        $allEvaluations=@(if(Test-Path -LiteralPath $paths.EvaluationsRoot){Get-ChildItem -LiteralPath $paths.EvaluationsRoot -File -Filter '*.json'})
        $orphanCompletionEvaluationCount=0
        foreach($file in $allEvaluations){$record=Read-ESStrictJson $file.FullName;$actual=Get-ESObjectHash (Get-ESEvaluationRecordHashInput $record);if([string]$record.recordHash-cne$actual){throw "EvaluationRecord hash mismatch: $($file.Name)"};if([string]$record.purpose-ceq'completion'-and("$TaskId/evaluations/$($file.Name)") -notin $boundEvaluations){$orphanCompletionEvaluationCount++}}
        [pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESTaskContextIntegrity';status='passed';taskId=$TaskId;eventCount=@($events).Count;currentTaskRevision=[int]$events[-1].state.taskRevision;currentContextVersion=[int]$events[-1].state.contextVersion;orphanReceiptCount=$orphanCount;orphanReceiptsAuthoritative=$false;evaluationRecordCount=$allEvaluations.Count;orphanCompletionEvaluationCount=$orphanCompletionEvaluationCount;orphanCompletionEvaluationsAuthoritative=$false;runtimeStatus='runtime-not-run';claimsNotProven=@('Unity Runtime','Worker Runtime','Release acceptance')}
    }catch{
        [pscustomobject][ordered]@{schemaVersion=1;validator='Test-ESTaskContextIntegrity';status='failed';taskId=$TaskId;finding=$_.Exception.Message;runtimeStatus='runtime-not-run';claimsNotProven=@('Event chain integrity','Runtime behavior')}
    }
}

Export-ModuleMember -Function New-ESGoalRevision,Resolve-ESGoalRevision,New-ESTaskContextTask,Get-ESTaskContextState,Confirm-ESTaskSourceScope,Submit-ESTaskEvidenceSet,New-ESTaskEvaluationRecord,Complete-ESTaskContextTask,Set-ESTaskDeliveryAcceptance,Invoke-ESTaskContextTransition,Get-ESTaskCommercialObservation,Test-ESTaskContextIntegrity
