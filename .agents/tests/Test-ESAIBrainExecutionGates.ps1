[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$coordinator = Join-Path $root 'Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs'
$bridge = Join-Path $root 'Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs'
$facade = Join-Path $root 'Assets/Plugins/ES/Editor/ESAutomation/ESAutomationFacade.cs'

function Read-Strict([string]$Path) {
    [IO.File]::ReadAllText($Path, (New-Object Text.UTF8Encoding($false, $true)))
}

function Require-Pattern([string]$Text, [string]$Pattern, [string]$Message) {
    if ($Text -notmatch $Pattern) { throw $Message }
}

$coordinatorText = Read-Strict $coordinator
$bridgeText = Read-Strict $bridge
$facadeText = Read-Strict $facade

Require-Pattern $coordinatorText 'approvedPlanHash' 'Coordinator must bind an approved PlanHash.'
Require-Pattern $coordinatorText 'ESAIBrainPlan canonicalPlan = BuildPlan\(snapshot\)' 'Approval must rebuild a canonical plan from the request snapshot.'
Require-Pattern $coordinatorText 'TrySnapshotRequest' 'Coordinator must snapshot mutable requests before planning or approval.'
Require-Pattern $coordinatorText '\(JObject\)source\.input\.DeepClone\(\)' 'Request input must be deep-cloned.'
Require-Pattern $coordinatorText 'ValidateExecutionEligibility' 'Execution eligibility gate is missing.'
Require-Pattern $coordinatorText 'skill\.reviewRequired' 'NeedsReview must be enforced at execution.'
Require-Pattern $coordinatorText 'runtimeEligibility' 'Runtime eligibility must be enforced at execution.'
Require-Pattern $coordinatorText 'TryBindTrustedHostProof' 'Trusted in-process host proof binding is missing.'
Require-Pattern $coordinatorText 'ComputeTrustedHostRequestHash' 'Trusted-host proof must bind the complete request.'
Require-Pattern $coordinatorText '\[JsonIgnore\][\s\r\n]+internal AIBrainTrustedHostProof trustedHostProof' 'Trusted-host proof must remain internal and non-serializable.'
Require-Pattern $coordinatorText 'Current-user proof requires a bound instruction SHA-256' 'Current-user proof must bind the user instruction hash.'
Require-Pattern $coordinatorText 'NotifyCapabilityDrift' 'Capability drift signal is missing.'
Require-Pattern $coordinatorText 'PollCapabilityDrift' 'Capability metadata polling is missing.'
Require-Pattern $coordinatorText 'route-scoped-compare-and-replan' 'Drift signal must require scoped re-plan.'

Require-Pattern $bridgeText 'approvedPlanHash' 'Bridge must expose approvedPlanHash.'
Require-Pattern $bridgeText 'runTask requires approvedPlanHash' 'Bridge must require approvedPlanHash for runTask.'
Require-Pattern $bridgeText 'new\[\] \{ "skillNames", "dryRun", "approvedPlanHash", "invocationId", "idempotencyKey" \}' 'Bridge optional fields must exclude caller-asserted userDirected.'
Require-Pattern $bridgeText 'TryBindTrustedHostProof\(brainRequest,[\s\r\n]+"es\.automation\.ai-bridge", string\.Empty, false' 'Bridge must bind only its managed-host class after payload validation.'
if ($bridgeText -match 'payload\["userDirected"\]') { throw 'Bridge still trusts a caller-supplied userDirected flag.' }
if ($bridgeText -match 'new\[\].*"userDirected"') { throw 'Bridge allowlist still exposes userDirected.' }
Require-Pattern $bridgeText 'NotifyCapabilityDrift\("queue-update"\)' 'Queue updates must signal capability drift.'
Require-Pattern $bridgeText 'NotifyCapabilityDrift\("session-resume"\)' 'Session resume must signal capability drift.'
Require-Pattern $bridgeText 'PollCapabilityDrift\("catalog-change"\)' 'Metadata changes must be polled.'

$preflightIndex = $facadeText.IndexOf('if (!endpoints.TryGetValue', [StringComparison]::Ordinal)
$playModeIndex = $facadeText.IndexOf('EditorApplication.isPlayingOrWillChangePlaymode', [StringComparison]::Ordinal)
$consumeIndex = $facadeText.IndexOf('TryConsumeAuthorization(invocation', [StringComparison]::Ordinal)
$dispatchIndex = $facadeText.IndexOf('return endpoint.Run(invocation);', [StringComparison]::Ordinal)
if ($preflightIndex -lt 0 -or $playModeIndex -le $preflightIndex -or $consumeIndex -le $playModeIndex -or $dispatchIndex -le $consumeIndex) {
    throw 'Facade must consume authorization only after deterministic preflight and immediately before dispatch.'
}

Write-Output 'PASS: AIBrain execution gates and bounded capability drift signals are structurally present.'
