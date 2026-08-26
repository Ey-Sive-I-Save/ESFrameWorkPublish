[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).ProviderPath
$validator = Join-Path $root '.agents/skills/es-skill-governance/scripts/Test-ESUserDirectedLowRiskPolicy.ps1'
if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) { throw "Validator not found: $validator" }

$cases = New-Object 'System.Collections.Generic.List[object]'
$strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)

function Add-Case([string]$Name, [string]$Expected, [hashtable]$Arguments, [string]$ExpectedReason = '', [string]$ExpectedReview = '') {
    $result = & $validator -ProjectRoot $root -AsObject @Arguments
    $reasonMatched = [string]::IsNullOrEmpty($ExpectedReason) -or @($result.reasons | Where-Object { $_ -like "*$ExpectedReason*" }).Count -gt 0
    $reviewMatched = [string]::IsNullOrEmpty($ExpectedReview) -or @($result.reviewSignals | Where-Object { $_ -like "*$ExpectedReview*" }).Count -gt 0
    [void]$cases.Add([pscustomobject][ordered]@{
        name = $Name
        expected = $Expected
        actual = [string]$result.status
        reasonMatched = $reasonMatched
        reviewMatched = $reviewMatched
        passed = ([string]$result.status -eq $Expected -and $reasonMatched -and $reviewMatched)
    })
}

function Add-TextContractCase(
    [string]$Name,
    [string]$RelativePath,
    [string[]]$RequiredText,
    [string[]]$ForbiddenText = @()
) {
    $path = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Contract file not found: $path" }
    $text = [System.IO.File]::ReadAllText($path, $strictUtf8)
    if ($text.Contains([string][char]0xFFFD)) { throw "Replacement character found in strict UTF-8 contract: $path" }

    $missing = @($RequiredText | Where-Object { -not $text.Contains($_) })
    $forbidden = @($ForbiddenText | Where-Object { $text.Contains($_) })
    $passed = $missing.Count -eq 0 -and $forbidden.Count -eq 0
    [void]$cases.Add([pscustomobject][ordered]@{
        name = $Name
        expected = 'semantic-contract'
        actual = if ($passed) { 'semantic-contract' } else { 'semantic-drift' }
        reasonMatched = $missing.Count -eq 0
        reviewMatched = $forbidden.Count -eq 0
        missingText = $missing
        forbiddenText = $forbidden
        passed = $passed
    })
}

function ConvertFrom-Utf8Base64([string]$Value) {
    return [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($Value))
}

Add-Case 'explicit-normal-modify' 'allowed' @{ AuthorizedPath = 'AGENTS.md'; Path = 'AGENTS.md'; Operation = 'modify'; ExplicitUserInstruction = $true }
Add-Case 'explicit-control-plane-modify' 'allowed' @{ AuthorizedPath = '.agents/README.md'; Path = '.agents/README.md'; Operation = 'modify'; ExplicitUserInstruction = $true }
Add-Case 'explicit-knowledge-index-modify' 'allowed' @{ AuthorizedPath = 'Documentation/AIKnowledge/KnowledgeIndex.yaml'; Path = 'Documentation/AIKnowledge/KnowledgeIndex.yaml'; Operation = 'modify'; ExplicitUserInstruction = $true }
Add-Case 'missing-user-instruction' 'blocked' @{ AuthorizedPath = 'AGENTS.md'; Path = 'AGENTS.md'; Operation = 'modify' } 'missing-current-explicit-user-instruction'
Add-Case 'declared-inferred-expansion' 'blocked' @{ AuthorizedPath = 'AGENTS.md'; Path = 'AGENTS.md'; Operation = 'modify'; ExplicitUserInstruction = $true; InferredScopeExpansion = $true } 'ai-inferred-scope-expansion'
Add-Case 'target-outside-exact-scope' 'blocked' @{ AuthorizedPath = 'AGENTS.md'; Path = '.agents/README.md'; Operation = 'modify'; ExplicitUserInstruction = $true } 'planned-target-outside-declared-user-scope'
Add-Case 'subtree-target' 'allowed' @{ AuthorizedPath = '.agents'; Path = '.agents/README.md'; Operation = 'modify'; ScopeMode = 'Subtree'; ExplicitUserInstruction = $true }
Add-Case 'delete-not-explicit' 'blocked' @{ AuthorizedPath = 'AGENTS.md'; Path = 'AGENTS.md'; Operation = 'delete'; ExplicitUserInstruction = $true } 'action-specific-operation-not-explicitly-requested'
Add-Case 'delete-explicit' 'allowed' @{ AuthorizedPath = 'AGENTS.md'; Path = 'AGENTS.md'; Operation = 'delete'; ExplicitUserInstruction = $true; ExplicitAction = $true }
Add-Case 'credential-not-explicit' 'blocked' @{ AuthorizedPath = '.env'; Path = '.env'; Operation = 'create'; ExplicitUserInstruction = $true } 'credentials'
Add-Case 'credential-explicit' 'allowed' @{ AuthorizedPath = '.env'; Path = '.env'; Operation = 'create'; ExplicitUserInstruction = $true; ExplicitAction = $true }
Add-Case 'benign-token-source-is-not-credential' 'allowed' @{ AuthorizedPath = 'Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/ValueChange/STRUCT_ESValueChangeToken.cs'; Path = 'Assets/Plugins/ES/0_Stand/BaseDefine_ValueType/ValueChange/STRUCT_ESValueChangeToken.cs'; Operation = 'modify'; ExplicitUserInstruction = $true }
Add-Case 'contained-absolute-input' 'allowed' @{ AuthorizedPath = (Join-Path $root 'AGENTS.md'); Path = (Join-Path $root 'AGENTS.md'); Operation = 'modify'; ExplicitUserInstruction = $true }
Add-Case 'project-root-escape' 'blocked' @{ AuthorizedPath = 'AGENTS.md'; Path = '..\outside.md'; Operation = 'modify'; ExplicitUserInstruction = $true } 'project-root-escape'
Add-Case 'modify-missing-target-is-review' 'allowed' @{ AuthorizedPath = '.agents/tests/__missing-policy-target__.md'; Path = '.agents/tests/__missing-policy-target__.md'; Operation = 'modify'; ExplicitUserInstruction = $true } '' 'modify-target-does-not-yet-exist'
Add-Case 'create-existing-target-is-review' 'allowed' @{ AuthorizedPath = 'AGENTS.md'; Path = 'AGENTS.md'; Operation = 'create'; ExplicitUserInstruction = $true } '' 'create-target-already-exists'

Add-TextContractCase 'local-static-validation-is-not-external-side-effect' 'AGENTS.md' @(
    (ConvertFrom-Utf8Base64 '6aG555uu5pei5pyJ5pys5Zyw6Z2Z5oCB6aqM6K+B5Zmo44CB6Kej5p6Q5Zmo44CB57yW6K+R5Zmo5ZKM5qC85byP5YyW5Zmo5bGe5LqO6LSo6YeP6aqM6K+B')
    (ConvertFrom-Utf8Base64 '5peg572R57ucL+aXoOWuieijhS/ml6DluLjpqbvlia/kvZznlKg=')
)
Add-TextContractCase 'local-static-validation-policy-boundary' '.agents/skills/es-skill-governance/references/user-directed-low-risk-policy.json' @(
    '"classification": "ordinary-quality-validation"'
    '"separateActionRequired": false'
    '"no-unity-or-runtime"'
    '"no-resident-service"'
)

Add-TextContractCase 'release-task-contract-is-managed-only' '.agents/skills/es-release-acceptance/SKILL.md' @(
    'explicit current-user authorization'
    'A matching TaskContract is additionally required only when the selected execution path is ManagedAIBrain/Worker'
) @(
    'each require explicit authorization and a matching TaskContract.'
)
Add-TextContractCase 'creator-runtime-channel-boundary' '.agents/skills/es-skill-creator/SKILL.md' @(
    'Runtime execution is opt-in only when the current user explicitly requests the Runtime action'
    'When the selected execution path is ManagedAIBrain/Worker'
    'those protocol inputs are not required for direct user work'
) @(
    'Runtime execution is opt-in only after explicit developer approval, an AIBrain plan'
)
Add-TextContractCase 'editor-tooling-direct-vs-managed-channel' '.agents/skills/es-editor-tooling/SKILL.md' @(
    'For direct work, treat the current explicit user request as edit authority.'
    'Select the closest AICommand only when the execution path is `ManagedAIBrain/Worker`'
) @(
    'Confirm whether it authorizes edits.'
)
Add-TextContractCase 'resource-pipeline-direct-vs-managed-channel' '.agents/skills/es-resource-pipeline/SKILL.md' @(
    'For direct work, treat the current explicit user request as edit authority.'
    'Select a matching AICommand only for `ManagedAIBrain/Worker`'
) @(
    'A read-only analysis command does not authorize a pipeline write.'
)
Add-TextContractCase 'module-lifecycle-direct-vs-managed-channel' '.agents/skills/es-module-lifecycle/SKILL.md' @(
    'Direct work follows the current explicit user request and does not require an AICommand.'
    'only a selected `ManagedAIBrain/Worker` channel additionally requires its matching protocol inputs.'
    'validate AICommand and TaskContract inputs only when `ManagedAIBrain/Worker` is selected.'
) @(
    'matching user and AICommand authority'
    'current user and AICommand authority'
)
Add-TextContractCase 'resource-publish-direct-vs-managed-channel' '.agents/skills/es-resource-publish-audit/SKILL.md' @(
    (ConvertFrom-Utf8Base64 '5q2j5byP5a+85Ye65b+F6aG755Sx5b2T5YmN55So5oi35piO56Gu54K55ZCN5bm25YWI5a6M5oiQIGRyeS1ydW7jgII=')
    (ConvertFrom-Utf8Base64 '5Y+q5pyJ6YCJ5oupIGBNYW5hZ2VkQUlCcmFpbi9Xb3JrZXJgIOaXtuaJjemineWkluagoemqjOWvueW6lCBBSUNvbW1hbmQg5ZKMIFRhc2tDb250cmFjdO+8jOWug+S7rOS4jeaYr+S6jOasoeaJueWHhuOAgg==')
) @(
    (ConvertFrom-Utf8Base64 '5q2j5byP5a+85Ye66ZyA5Y2V54usIEFJQ29tbWFuZCDlkowgZHJ5LXJ1buOAgg==')
)
Add-TextContractCase 'module-audit-command-direct-vs-managed-channel' (ConvertFrom-Utf8Base64 'QXNzZXRzL1BsdWdpbnMvRVMvQUlDb21tYW5kcy/mo4Dmn6Vf5qih5Z2X5oiQ54af5bqm5LiO5Y2K5oiQ5ZOB5b2x5ZONX0FJ5ZG95LukLm1k') @(
    (ConvertFrom-Utf8Base64 '5a6e546w5YmN5b+F6aG75Y+W5b6X5b2T5YmN55So5oi355qE5piO56Gu5oyH5Luk44CC')
    (ConvertFrom-Utf8Base64 '5Y+q5pyJ6YCJ5oupIGBNYW5hZ2VkQUlCcmFpbi9Xb3JrZXJgIOaXtu+8jOaJjemineWkluimgeaxguWMuemFjeeahOaJp+ihjOexuyBBSUNvbW1hbmQg5LiOIFRhc2tDb250cmFjdCDkvZzkuLror6XpgJrpgZPnmoTljY/orq7ovpPlhaXjgII=')
) @(
    (ConvertFrom-Utf8Base64 '5a6e546w5YmN5b+F6aG76YeN5paw5Y+W5b6X5b2T5YmN55So5oi35LiO5omn6KGM57G7IEFJQ29tbWFuZCDnmoTmnYPpmZDjgII=')
)
Add-TextContractCase 'skill-creator-project-root-is-portable' '.agents/skills/es-skill-creator/SKILL.md' @(
    '<current-project-root>/.agents/skills'
    '--project-root . --catalog .agents/SKILL_CATALOG.yaml --write'
    '--path ".agents/skills"'
) @(
    'F:/aaProject/ESFrameWorkPublish'
)
Add-TextContractCase 'stable-graph-direct-vs-managed-channel' '.agents/skills/es-stable-graph-authoring/SKILL.md' @(
    '`ManagedProtocolRequiredWhen`: `ManagedAutomation/AIBrain`'
    '`DirectUserAssetWrite`: `ExplicitBoundedOnly`'
) @(
    (ConvertFrom-Utf8Base64 '6K+75Y+WIEdyYXBoIFAw44CBRVNBdXRvbWF0aW9uL1Rhc2tDb250cmFjdOOAgeebruaghyBjb25zdW1lciDlkoznjrDmnIkgYXNzZXTvvJvnoa7orqQgTGVnYWN5IOi3r+W+hOemgeatouaBouWkjeOAgg==')
    (ConvertFrom-Utf8Base64 '5LiN55u05o6l5YaZIEFzc2V0c+OAgeS4jee7lei/hyBBSUNvbW1hbmQvRmFjYWRl44CB5LiN5oqK5Zu+5a2Y5Zyo5a6j56ew5Li65omn6KGM5oiQ5Yqf44CC')
)
Add-TextContractCase 'agent-artifacts-autonomous-vs-user-formal-write' '.agents/skills/es-generate-agent-artifacts/SKILL.md' @(
    'When this Skill runs autonomously from a Graph/Cmd Agent generation request'
    'A current explicit user request may instead name bounded formal targets'
    'run their formal validators, and provide a Diff Review.'
) @(
    'Generate candidates only. Never approve or write directly to the formal AICommand or Agent Skill directories.'
    '- Do not modify `Assets/Plugins/ES/AICommands` or `.agents/skills` directly.'
)
Add-TextContractCase 'stable-graph-topology-and-consumer-contracts' 'Documentation/AIKnowledge/Editor/project-stable-graph-v2/stable-graph-v2-authoring.md' @(
    'ESGraphTopologyAnalyzer'
    'ESBehaviorTreeProgram'
    'ESStoryDefinitionSnapshot'
    'ESStoryDefinitionDataInfo'
    'InvalidEndpointRecordCount'
    'ESStoryGraphAsset'
    'ESBehaviorTreeGraphAsset'
    '`MultiEndpointRule`: `TwoOrMoreValidIndependentEndpointsInSameDirection`'
    '`SingleEndpointMultiConnectionIsMultiEndpoint`: `false`'
    '`BehaviorTreeProgramState`: `ReservedNotImplemented`'
    '`StoryAuthorAuthority`: `ESStoryDefinitionDataInfo`'
    '`StoryGraphIntegrationState`: `TypeExistsNotConnectedToDataInfoSnapshotChain`'
    '`ManagedProtocolRequiredWhen`: `ManagedAutomation/AIBrain`'
    '`CommercialState`: `Graph=Verifying; Story=Verifying; BehaviorTreeProgram=NotImplemented`'
    'ffd47f75089d13023597277357ce63bcd6c05b6e97d2683a7a5e64e33e234649'
    'Assets/Plugins/ES/Editor/ESGraphViewV2/ESGraphAuthoringProfiles.cs'
    'Assets/Scripts/ESLogic/Runtime/Data/For_Info/InfoType/ESStoryDefinitionDataInfo.cs'
    'Assets/Scripts/ESLogic/Runtime/Story/Definitions/ESStoryDefinitionCatalog.cs'
    'Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESStoryModule.cs'
) @(
    (ConvertFrom-Utf8Base64 '5raJ5Y+K5omn6KGMIEdyYXBoIOaXtui9rOivuyBBdXRvbWF0aW9uIOadoeebru+8jOW5tuimgeaxgiBBSUNvbW1hbmTjgIFUYXNrQ29udHJhY3Qg5LiOIFJ1blJlY29yZA==')
    (ConvertFrom-Utf8Base64 '5raI6LS56ICF5b+F6aG755Sf5oiQ6Ieq5bex55qE5LiN5Y+v5Y+Y5Lqn54mp77yM5bm257un57ut6YG15a6I')
)
Add-TextContractCase 'authority-fact-action-separation' 'Documentation/AIKnowledge/entries/authority-and-startup.md' @(
    '`FactAuthority`: `CurrentSourceAndEvidence > AIWarningsP0 > CurrentDomainRules > KnowledgeProjection > ExternalCache`'
    '`ActionAuthority`: `CurrentExplicitUserInstruction`'
    '`ManagedProtocolRequiredWhen`: `ManagedAIBrain/Worker`'
    'AICommand'
    'TaskContract'
)
Add-TextContractCase 'aiwarnings-stale-does-not-revoke-user-authority' 'Documentation/AIKnowledge/entries/aiwarnings-domain-map.md' @(
    '`ActionAuthority`: `CurrentExplicitUserInstruction`'
    '`ManagedProtocolRequiredWhen`: `ManagedAIBrain/Worker`'
    '`SourceDriftEffect`: `KnowledgeAndDependentPlanStale`'
    '`SourceDriftRequiresSecondUserApproval`: `false`'
) @(
    (ConvertFrom-Utf8Base64 '5b+F6aG76YeN5paw5oq95Y+W5bm26YeN5pawIHBsYW5UYXNr')
)
Add-TextContractCase 'managed-authorization-is-bounded-and-nonforgeable' 'Documentation/AIKnowledge/entries/skill-governance-creator.md' @(
    '`AuthorizationLifetimeMinutes`: `15`'
    '`AuthorizationPolicyVersion`: `5`'
    '`AuthorizationStoreSchemaVersion`: `3`'
    '`UserDirectedLowRiskMaxUses`: `20`'
    '`CandidateOnlyL1L2MaxUses`: `5`'
    '`HighRiskMaxUses`: `1`'
    '`ReusableAuthorizationRequiresUniqueNonEmptyIdempotencyKey`: `true`'
    '`ExternalBridgeMayAssertUserDirected`: `false`'
    '`ExhaustedAuthorizationRequiresNewInvocation`: `true`'
)
Add-TextContractCase 'managed-authorization-source-contract' 'Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs' @(
    'private const int AuthorizationPolicyVersion = 5;'
    'private const int AuthorizationStoreSchemaVersion = 3;'
    'private const int DefaultLowRiskAuthorizationUses = 20;'
    'private const int DefaultCandidateAuthorizationUses = 5;'
    'private const int DefaultHighRiskAuthorizationUses = 1;'
    'record.maxUses > 1 && string.IsNullOrWhiteSpace(key)'
    'ComputeAuthorizationBindingHash(invocation,'
    'store.authorizationPolicyVersion != AuthorizationPolicyVersion'
    '!planHashes.Add(record.planHash)'
    '!invocationIds.Add(record.invocationId)'
    'validTerminalTime'
    'AuthorizationStatusExhausted'
)
Add-TextContractCase 'external-bridge-cannot-assert-user-directed' 'Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs' @(
    'new[] { "skillNames", "dryRun", "approvedPlanHash", "invocationId", "idempotencyKey",'
    '"routeProfileId", "goalRevisionPath"'
    '"es.automation.ai-bridge", string.Empty, false'
) @(
    '"idempotencyKey", "userDirected"'
)
Add-TextContractCase 'aiwarnings-unitymcp-user-authority' (ConvertFrom-Utf8Base64 'QXNzZXRzL1BsdWdpbnMvRVMvQUlXYXJuaW5ncy8yMF/mnrbmnoTnjrDnirbvvIhBcmNoaXRlY3R1cmXvvIkv6Leo57O757uf5qC45b+D6K+t5LmJ77yIQ29yZVNlbWFudGljc++8iS9BZ2VudFNraWxsc+S4jkFJQ29tbWFuZHPljY/kvZzovrnnlYxfQUnljY/kvZzorablkYoubWQ=') @(
    (ConvertFrom-Utf8Base64 '5Zy65pmv44CB6LWE5Lqn5ZKM6YWN572u5L+u5pS55b+F6aG755Sx5b2T5YmN55So5oi35piO56Gu54K55ZCN')
    (ConvertFrom-Utf8Base64 '6L+Z5Lqb5Y2P6K6u5LiN5piv56ys5LqM5qyh55So5oi35om55YeG')
) @(
    (ConvertFrom-Utf8Base64 '5Zy65pmv44CB6LWE5Lqn5ZKM6YWN572u5L+u5pS55b+F6aG75pyJ5piO56Gu5ZG95Luk5o6I5p2D')
)
Add-TextContractCase 'feishu-current-user-external-action-authority' '.agents/README.md' @(
    (ConvertFrom-Utf8Base64 '5aSW6YOo5YaZ5b+F6aG755Sx5b2T5YmN55So5oi35piO56Gu54K55ZCN')
    (ConvertFrom-Utf8Base64 '5Y+X566h5omn6KGM5LuN5YWIIERyeVJ1biDlubbmioror6XmjIfku6Tnu5HlrprliLDljZXmrKHliqjkvZw=')
) @(
    (ConvertFrom-Utf8Base64 '5aSW6YOo5YaZ5b+F6aG75YWIIERyeVJ1biDlubbljZXmrKHmjojmnYM=')
)
Add-TextContractCase 'knowledge-command-direct-vs-managed-channel' (ConvertFrom-Utf8Base64 'QXNzZXRzL1BsdWdpbnMvRVMvQUlDb21tYW5kcy/lj5fnrqFBSUtub3dsZWRnZeabtOaWsF9BSeWRveS7pC5tZA==') @(
    (ConvertFrom-Utf8Base64 '5b2T5YmN55So5oi35piO56Gu6KaB5rGC55qE5pyJ55WMIEtub3dsZWRnZSDmnaHnm67jgIFLbm93bGVkZ2VJbmRleOOAgUFJQlJBSU5fRU5UUlkg5oiWIFNvdXJjZVJlZi/ot6/nlLHmipXlvbHkv67mlLnlj6/nm7TmjqXmiafooYw=')
    (ConvertFrom-Utf8Base64 '5Y+q5pyJ6YCJ5oupIEFJQnJhaW4vV29ya2VyIOWPl+euoemAmumBk+aXtg==')
    (ConvertFrom-Utf8Base64 '5rKh5pyJ5b2T5YmN55So5oi35oyH5Luk5pe277yMQUkg6Ieq5Li76Lev5b6E5Y+q6IO96L6T5Ye65YCZ6YCJ5oiW5bu66K6u')
) @(
    (ConvertFrom-Utf8Base64 'S25vd2xlZGdlSW5kZXjjgIFBSUJSQUlOX0VOVFJZ44CBU291cmNlUmVmIOi3r+eUseaIluWFtuS7luadg+Wogee0ouW8leWPmOWMluS7jeW/hemhu+i1sCBBSUJyYWlu')
    (ConvertFrom-Utf8Base64 '5LiN5b6X5omp5aSn6L6T5Ye66IyD5Zu044CB5Lyq6YCg6K+B5o2u44CB5Yig6Zmk5Yay56qB5p2h55uu44CB5YaZ5YWl56eY5a+G5oiW57uV6L+HIEFJQnJhaW4vRmFjYWRl44CC')
)

$failed = @($cases | Where-Object { -not $_.passed })
$report = [ordered]@{
    schemaVersion = 1
    validator = 'es-user-directed-action-authority-regression'
    status = if ($failed.Count -eq 0) { 'static-passed' } else { 'blocked' }
    caseCount = $cases.Count
    failedCount = $failed.Count
    cases = $cases.ToArray()
    claimsNotProven = @('host authenticity of the current user message', 'Runtime behavior', 'network or release behavior')
}
$report | ConvertTo-Json -Depth 8
if ($failed.Count -gt 0) { exit 1 }
