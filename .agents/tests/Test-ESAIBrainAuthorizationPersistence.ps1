[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$ProjectRoot)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$sourcePath = Join-Path $root "Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs"
$bridgePath = Join-Path $root "Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs"
$testPath = Join-Path $root "Assets/Plugins/ES/1_Design/Tests/ESAutomationAiBridgeTests.cs"
if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "AIBrain coordinator source is missing." }
if (-not (Test-Path -LiteralPath $bridgePath -PathType Leaf)) { throw "AIBrain bridge source is missing." }
if (-not (Test-Path -LiteralPath $testPath -PathType Leaf)) { throw "AIBrain authorization tests are missing." }
$text = [IO.File]::ReadAllText($sourcePath, (New-Object Text.UTF8Encoding($false, $true)))
$bridgeText = [IO.File]::ReadAllText($bridgePath, (New-Object Text.UTF8Encoding($false, $true)))
$testText = [IO.File]::ReadAllText($testPath, (New-Object Text.UTF8Encoding($false, $true)))

function Assert-Contains([string]$value, [string]$needle, [string]$message) {
    if ($value.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw $message }
}

function Assert-NotContains([string]$value, [string]$needle, [string]$message) {
    if ($value.IndexOf($needle, [StringComparison]::Ordinal) -ge 0) { throw $message }
}

Assert-Contains $text "AuthorizationStorePath" "Authorization store path is not bounded."
Assert-Contains $text "AuthorizationStoreSchemaVersion = 3" "Authorization store schema v3 is missing."
Assert-Contains $text "AuthorizationPolicyVersion = 5" "Authorization policy v5 is missing."
Assert-Contains $text "TryOpenAuthorizationLock" "Cross-process authorization lock path is missing."
Assert-Contains $text "FileMode.OpenOrCreate" "Authorization lock file must be permanent."
Assert-Contains $text "FileShare.None" "Authorization lock must exclude concurrent writers."
Assert-Contains $text "TryLoadAuthorizationStore" "Authorization load transaction is missing."
Assert-Contains $text "TryPersistAuthorizationStore" "Authorization persistence transaction is missing."
Assert-Contains $text "ESManagedFileIO.WriteTextAtomic(path" "Authorization persistence must use the managed atomic writer."
Assert-Contains $text "TryRegisterAuthorization(CreateInvocation(snapshot, canonicalPlan, profile)," "Approval must register the rebuilt canonical plan."
Assert-Contains $bridgeText "TryApprovePlan(brainRequest, plan, out string approvalError)" "Bridge must observe authorization registration failure."
Assert-Contains $bridgeText 'plan.blockers.Add(' "Registration failure must produce an explicit blocker."
Assert-Contains $bridgeText '+ approvalError)' "Registration failure must retain the persistence error."
Assert-Contains $bridgeText 'plan.status = "Blocked"' "Registration failure must fail closed."
Assert-Contains $text "public string planHash" "Persisted authorization must include planHash."
Assert-Contains $text "public string bindingHash" "Persisted authorization must include the invocation binding hash."
Assert-Contains $text "public string invocationId" "Persisted authorization must include invocationId."
Assert-Contains $text "public string status = AuthorizationStatusActive" "Persisted authorization must include terminal state."
Assert-Contains $text "public DateTimeOffset expiresAtUtc" "Persisted authorization must include expiry."
Assert-Contains $text "public DateTimeOffset? terminalAtUtc" "Persisted terminal records must retain terminal time."
Assert-Contains $text "TimeSpan.FromMinutes(15)" "Authorization lifetime must remain bounded to 15 minutes."
Assert-Contains $text "store.authorizationPolicyVersion != AuthorizationPolicyVersion" "Stale authorization policy generations must be invalidated."
Assert-Contains $text "authorizationPolicyVersion = AuthorizationPolicyVersion" "Authorization policy version must bind persisted state and invocation hashes."
Assert-Contains $text "if (!File.Exists(path))" "Missing authorization storage must be handled explicitly."
Assert-Contains $text "schemaVersion == 2 && policyVersion == 4" "Policy v4/schema 2 migration boundary is missing."
Assert-Contains $text "store.retiredInvocationIds.Contains(invocation.invocationId" "Legacy Invocation ids must not be silently re-signed."
Assert-Contains $text "public List<string> retiredInvocationIds" "Retired legacy Invocation ids must survive schema migration."
Assert-Contains $text "if (!allowLegacyReinitialization)" "Legacy consumption must fail instead of silently reinitializing the store."
Assert-NotContains $text "static readonly Dictionary<string, AIBrainExecutionAuthorization> Authorizations" "Policy v5 must not retain an authoritative process-local grant cache."
Assert-Contains $text "DefaultLowRiskAuthorizationUses = 20" "User-directed L1 authorization use limit is missing."
Assert-Contains $text "DefaultCandidateAuthorizationUses = 5" "Candidate authorization use limit is missing."
Assert-Contains $text "DefaultHighRiskAuthorizationUses = 1" "High-risk authorization must remain single-use."
Assert-Contains $text "AuthorizationClassCurrentUser" "Current-user authorization class is missing."
Assert-Contains $text "IsLowRiskDirectedPlan(plan)" "Only L1 low-risk plans may receive the directed reuse limit."
Assert-Contains $text 'string.Equals(plan.command.riskLevel, "L2", StringComparison.Ordinal)' "Only L1/L2 candidate plans may receive the candidate reuse limit."
Assert-Contains $text "record.maxUses > 1 && string.IsNullOrWhiteSpace(key)" "Reusable authorization must require a non-empty idempotencyKey."
Assert-Contains $text "record.usedIdempotencyKeys.Contains" "Reusable authorization must reject duplicate idempotency keys."
Assert-Contains $text "record.usedCount++" "Authorization use count must be consumed before dispatch."
Assert-Contains $text "record.usedIdempotencyKeys.Add(key)" "Consumed idempotency keys must be persisted in the store record."
Assert-Contains $text "ComputeAuthorizationBindingHash(invocation," "Use limit, identity, expiry, and invocation fields must bind the authorization hash."
Assert-Contains $text "planHashes.Add(record.planHash)" "Persisted authorization PlanHash values must be unique."
Assert-Contains $text "invocationIds.Add(record.invocationId)" "Persisted authorization InvocationId values must be unique."
Assert-Contains $text "record.usedCount <= record.maxUses" "Persisted use count must be bounded."
Assert-Contains $text "usedKeys.Distinct(StringComparer.OrdinalIgnoreCase)" "Persisted idempotency keys must remain unique."
Assert-Contains $text "AuthorizationStatusExhausted" "Exhausted tombstone state is missing."
Assert-Contains $text "AuthorizationStatusExpired" "Expired tombstone state is missing."
Assert-Contains $text "existingInvocation.status" "Re-approval must inspect the existing Invocation terminal state."
Assert-Contains $text "existingInvocation.planHash, invocation.brainPlanHash" "InvocationId must not be rebound to another PlanHash."
Assert-Contains $text "validTerminalTime" "Persisted terminal timestamps must reject impossible future values."

$openStart = $text.IndexOf("private static bool TryOpenAuthorizationLock", [StringComparison]::Ordinal)
$loadStart = $text.IndexOf("private static bool TryLoadAuthorizationStore", [StringComparison]::Ordinal)
if ($openStart -lt 0 -or $loadStart -le $openStart) { throw "Could not isolate authorization lock implementation." }
$openBlock = $text.Substring($openStart, $loadStart - $openStart)
$firstPathCheck = $openBlock.IndexOf("ESManagedFileIO.EnsurePath(storePath", [StringComparison]::Ordinal)
$createDirectory = $openBlock.IndexOf("Directory.CreateDirectory(parent)", [StringComparison]::Ordinal)
$lastPathCheck = $openBlock.LastIndexOf("ESManagedFileIO.EnsurePath(storePath", [StringComparison]::Ordinal)
if ($firstPathCheck -lt 0 -or $createDirectory -lt 0 -or $firstPathCheck -gt $createDirectory -or $lastPathCheck -le $createDirectory) {
    throw "Authorization directory must be checked before creation and rechecked after creation."
}

$consumeStart = $text.IndexOf("internal static bool TryConsumeAuthorization", [StringComparison]::Ordinal)
$registerStart = $text.IndexOf("private static bool TryRegisterAuthorization", [StringComparison]::Ordinal)
if ($consumeStart -lt 0 -or $registerStart -le $consumeStart) { throw "Could not isolate authorization consumption transaction." }
$consumeBlock = $text.Substring($consumeStart, $registerStart - $consumeStart)
$lockIndex = $consumeBlock.IndexOf("TryOpenAuthorizationLock", [StringComparison]::Ordinal)
$loadIndex = $consumeBlock.IndexOf("TryLoadAuthorizationStore", [StringComparison]::Ordinal)
$incrementIndex = $consumeBlock.IndexOf("record.usedCount++", [StringComparison]::Ordinal)
$persistIndex = $consumeBlock.IndexOf("TryPersistAuthorizationStore(storePath, store, out string consumeError)", [StringComparison]::Ordinal)
$successIndex = $consumeBlock.LastIndexOf("return true;", [StringComparison]::Ordinal)
if ($lockIndex -lt 0 -or $loadIndex -le $lockIndex -or $incrementIndex -le $loadIndex -or $persistIndex -le $incrementIndex -or $successIndex -le $persistIndex) {
    throw "Authorization consumption must lock, reload, mutate, persist, then report success in that order."
}

$recordStart = $text.IndexOf("private sealed class AIBrainAuthorizationRecord", [StringComparison]::Ordinal)
$requestStart = $text.IndexOf("public sealed class ESAIBrainRequest", [StringComparison]::Ordinal)
if ($recordStart -lt 0 -or $requestStart -le $recordStart) { throw "Could not isolate persisted authorization record." }
$recordBlock = $text.Substring($recordStart, $requestStart - $recordStart)
Assert-NotContains $recordBlock "JObject input" "Persisted authorization records must not store task input payloads."

Assert-Contains $testText "AIBrainAuthorizationPolicy_EnforcesTwentyFiveOneBudgetsAndTombstones" "Exact 20/5/1 behavior test is missing."
Assert-Contains $testText "AIBrainAuthorizationPolicy_LockFailureDoesNotConsumeIdempotencyKey" "Lock failure recovery test is missing."
Assert-Contains $testText "AIBrainAuthorizationPolicy_FacadePreflightFailureDoesNotConsumeGrant" "Facade preflight must not consume a grant."
Assert-Contains $testText "AIBrainAuthorizationPolicy_ExpiredInvocationCannotBeResignedOrRebound" "Expiry and rebind test is missing."
Assert-Contains $testText "AIBrainAuthorizationPolicy_LegacyAndCorruptStoresFailClosed" "Legacy and corrupt-store test is missing."
Assert-Contains $testText "AIBrainTrustedHostProof_IsInternalBoundAndExpiring" "Trusted-host proof test is missing."

Write-Output ([string]::Concat("PASS: AIBrain Policy v5 persistence is locked, atomic, tombstoned, bounded, and fail-closed."))
