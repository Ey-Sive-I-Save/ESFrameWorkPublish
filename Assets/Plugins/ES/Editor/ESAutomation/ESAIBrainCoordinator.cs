using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using ES.EditorInternal;

namespace ES
{
    /// <summary>
    /// AIBrain is the planning and authority-routing boundary for AI work in the Editor.
    /// It does not replace AIWarnings, AICommands, Skills, or Automation; it verifies each
    /// boundary and is the only coordinator allowed to hand a validated plan to the Facade.
    /// </summary>
    public static class ESAIBrainCoordinator
    {
        private const int MaxKnowledgeEntriesPerPlan = 3;
        public const int ContractVersion = 1;
        public const string KnowledgeRankingVersion = "per-route-best-top3-v1";
        public const string KnowledgeIndexPath = "Documentation/AIKnowledge/KnowledgeIndex.yaml";
        public const string ProjectSkillsRoot = ".agents/skills";
        public const string ProjectSkillCatalogPath = ".agents/SKILL_CATALOG.yaml";
        public const string ProjectSkillDiscoveryPolicyPath = ".agents/SKILL_DISCOVERY_POLICY.json";
        public const string RoutePlanContractId = "es://automation/contracts/route-plan/v1";
        public const string RouteStageRegistryPath = "ES/Automation/Contracts/es-route-stage.registry.json";
        public const string AbcModeRegistryPath = "ES/Automation/Contracts/es-ai-abc-mode.registry.json";
        public const string ChineseSkillAliasPath = ".agents/SKILL_ROUTE_ALIASES.zh-CN.json";
        private const string AuthorizationStorePath = "ES/Output/Automation/AIBrain/authorizations.json";
        private const int AuthorizationStoreSchemaVersion = 3;
        private const int MaximumAuthorizationRecords = 4096;
        private const string AuthorizationStatusActive = "Active";
        private const string AuthorizationStatusExhausted = "Exhausted";
        private const string AuthorizationStatusExpired = "Expired";
        private const string AuthorizationClassManaged = "ManagedAIBrain";
        private const string AuthorizationClassCurrentUser = "CurrentUserDirect";
        private const string AuthorizationBudgetLowRisk = "LowRiskDirected";
        private const string AuthorizationBudgetCandidate = "CandidateOnly";
        private const string AuthorizationBudgetHighRisk = "HighRisk";
        private const string SkillGovernanceMetadataFileName = "governance.json";

        private const string AiwarningsRoot = "Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/";
        private const string AiwarningsReadme = AiwarningsRoot + "README.md";
        private const string AiwarningsCurrentStatus = AiwarningsRoot + "当前状态（CurrentStatus）.md";
        private const string AiwarningsRuleIndex = AiwarningsRoot + "规则索引（RuleIndex）.md";
        private const string AiwarningsRouteCatalog = AiwarningsRoot + "AIWarningsRouteCatalog.json";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly object AuthorizationLock = new object();
        private static readonly TimeSpan AuthorizationLifetime = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan TrustedHostProofLifetime = TimeSpan.FromMinutes(5);
        private const int AuthorizationPolicyVersion = 5;
        private const int DefaultLowRiskAuthorizationUses = 20;
        private const int DefaultCandidateAuthorizationUses = 5;
        private const int DefaultHighRiskAuthorizationUses = 1;
        private static Func<DateTimeOffset> authorizationUtcNow = () => DateTimeOffset.UtcNow;
        private static string authorizationStorePathOverride = string.Empty;
        private static int authorizationRecordLimit = MaximumAuthorizationRecords;
        private static readonly object CapabilityDriftLock = new object();
        private static readonly string[] CapabilityMetadataPaths =
        {
            ".agents/SKILL_RESOURCE_INDEX.yaml",
            ".agents/SKILL_CATALOG.yaml",
            ".agents/SKILL_DISCOVERY_POLICY.json",
            ChineseSkillAliasPath,
            "Documentation/AIKnowledge/KnowledgeIndex.yaml",
            "Documentation/AIKnowledge/AIBRAIN_ENTRY.md",
            "Assets/Plugins/ES/AICommands/AICommandCatalog.json",
            RouteStageRegistryPath,
            AbcModeRegistryPath,
        };
        private static string capabilityMetadataFingerprint = string.Empty;
        private static string capabilityDriftTrigger = string.Empty;
        private static DateTimeOffset nextCapabilityPollUtc = DateTimeOffset.MinValue;
        private static int capabilityDriftGeneration;

        public static event System.Action<ESAIBrainCapabilityDriftSignal> CapabilityDriftDetected;

        public static int CapabilityDriftGeneration
        {
            get
            {
                lock (CapabilityDriftLock) return capabilityDriftGeneration;
            }
        }

        public static bool TryPlan(ESAIBrainRequest request, out ESAIBrainPlan plan, out string error)
        {
            plan = Plan(request);
            error = plan == null ? "AIBrain 未能建立计划。" : plan.FirstBlocker;
            return plan != null && plan.IsRunnable;
        }

        public static ESAIBrainPlan Plan(ESAIBrainRequest request)
        {
            if (!TrySnapshotRequest(request, out ESAIBrainRequest snapshot, out string snapshotError))
                return CreateInvalidRequestPlan(request, snapshotError);
            return BuildPlan(snapshot);
        }

        /// <summary>
        /// Execute only a plan that passed every authority gate. No script path, process
        /// argument, or direct asset mutation is accepted by this entry point.
        /// </summary>
        public static ESAutomationTaskInvocationResult Run(ESAIBrainRequest request)
        {
            return Run(request, out _);
        }

        /// <summary>
        /// Builds exactly one plan for an invocation and returns that same plan to the caller.
        /// This prevents a caller from displaying a plan that differs from the one authorized.
        /// </summary>
        public static ESAutomationTaskInvocationResult Run(ESAIBrainRequest request,
            out ESAIBrainPlan plan)
        {
            if (request == null)
            {
                plan = null;
                return ESAutomationTaskInvocationResult.Rejected("AIBrain 执行缺少请求。");
            }
            if (!TrySnapshotRequest(request, out ESAIBrainRequest snapshot, out string snapshotError))
                return RejectPlan(out plan, "AIBrain 请求快照失败：" + snapshotError);
            try
            {
                if (snapshot.executionSnapshot != null) snapshot.executionSnapshot.Validate();
                if (!string.IsNullOrWhiteSpace(snapshot.idempotencyKey)
                    && (snapshot.idempotencyKey.Length > 160
                        || !Regex.IsMatch(snapshot.idempotencyKey, "^[A-Za-z0-9._:-]+$")))
                    return RejectPlan(out plan, "AIBrain 幂等键格式无效。");
            }
            catch (Exception exception)
            {
                return RejectPlan(out plan, "AIBrain 执行快照无效：" + exception.Message);
            }
            plan = BuildPlan(snapshot);
            if (plan == null)
                return ESAutomationTaskInvocationResult.Rejected("AIBrain 未能建立计划。");
            if (!plan.IsRunnable)
                return ESAutomationTaskInvocationResult.Blocked(
                    "AIBrain 门禁未通过：" + plan.FirstBlocker, plan.planId);
            AuthorizationProfile authorizationProfile = AuthorizationProfile.Untrusted();
            if (snapshot.fromAi)
            {
                if (!TryResolveAuthorizationProfile(plan, snapshot, true,
                        out authorizationProfile, out string proofError))
                    return ESAutomationTaskInvocationResult.Blocked(
                        "AIBrain trusted-host proof is invalid: " + proofError, plan.planId);
                if (!ESAutomationWorkerRegistration.IsSha256(snapshot.approvedPlanHash))
                    return ESAutomationTaskInvocationResult.Blocked(
                        "AI runTask requires the approvedPlanHash returned by planTask.", plan.planId);
                if (!string.Equals(snapshot.approvedPlanHash, plan.planHash,
                        StringComparison.OrdinalIgnoreCase))
                    return ESAutomationTaskInvocationResult.Blocked(
                        "approvedPlanHash does not match the current plan; re-plan is required.", plan.planId);
                if (!ValidateExecutionEligibility(plan, authorizationProfile,
                        out string executionEligibilityError))
                    return ESAutomationTaskInvocationResult.Blocked(
                        executionEligibilityError, plan.planId);
            }
            if (snapshot.executionSnapshot != null)
            {
                if (!string.Equals(snapshot.executionSnapshot.brainPlanHash, plan.planHash,
                    StringComparison.OrdinalIgnoreCase))
                    return ESAutomationTaskInvocationResult.Blocked(
                        "ExecutionSnapshot.brainPlanHash 与当前 AIBrain PlanHash 不一致。", plan.planId);
                if (plan.command != null
                    && !string.Equals(snapshot.executionSnapshot.commandHash, plan.command.contractHash,
                        StringComparison.OrdinalIgnoreCase))
                    return ESAutomationTaskInvocationResult.Blocked(
                        "ExecutionSnapshot.commandHash 与当前 AICommand 合同不一致。", plan.planId);
                if (plan.task != null
                    && !string.Equals(snapshot.executionSnapshot.taskContractHash,
                        plan.task.taskContractHash, StringComparison.OrdinalIgnoreCase))
                    return ESAutomationTaskInvocationResult.Blocked(
                        "ExecutionSnapshot.taskContractHash 与当前 TaskContract 不一致。", plan.planId);
            }

            if (string.IsNullOrWhiteSpace(snapshot.invocationId)
                || !Guid.TryParseExact(snapshot.invocationId, "N", out _))
            {
                return ESAutomationTaskInvocationResult.Rejected(
                    "AIBrain 执行必须携带稳定的 N 格式 InvocationId，以防止重复副作用。");
            }

            var invocation = CreateInvocation(snapshot, plan, authorizationProfile);
            return ESAutomationFacade.RunTask(invocation);
        }

        public static bool TryApprovePlan(ESAIBrainRequest request, ESAIBrainPlan plan,
            out string error)
        {
            bool approved = TryApprovePlan(request, plan,
                out ESAIBrainPlan canonicalPlan, out error);
            if (approved && canonicalPlan != null)
            {
                // Preserve the original API while returning the identity actually written to Store.
                plan.planId = canonicalPlan.planId;
                plan.planHash = canonicalPlan.planHash;
            }
            return approved;
        }

        public static bool TryApprovePlan(ESAIBrainRequest request, ESAIBrainPlan plan,
            out ESAIBrainPlan approvedCanonicalPlan, out string error)
        {
            approvedCanonicalPlan = null;
            error = string.Empty;
            if (request == null || plan == null)
            {
                error = "AIBrain 授权缺少请求或计划。";
                return false;
            }
            string expectedPlanHash = plan.planHash ?? string.Empty;
            if (!ESAutomationWorkerRegistration.IsSha256(expectedPlanHash))
            {
                error = "待批准计划缺少有效 PlanHash。";
                return false;
            }
            if (!TrySnapshotRequest(request, out ESAIBrainRequest snapshot, out error)) return false;
            if (string.IsNullOrWhiteSpace(snapshot.invocationId)
                || !Guid.TryParseExact(snapshot.invocationId, "N", out _))
            {
                error = "计划必须携带稳定的 N 格式 InvocationId。";
                return false;
            }
            if (!snapshot.fromAi)
            {
                error = "AIBrain 持久授权只签发给受管 AI 调用。";
                return false;
            }
            ESAIBrainPlan canonicalPlan = BuildPlan(snapshot);
            approvedCanonicalPlan = canonicalPlan;
            if (canonicalPlan == null || !canonicalPlan.IsRunnable)
            {
                error = "只有重新构建后仍可运行的 canonical 计划才能签发授权："
                    + (canonicalPlan?.FirstBlocker ?? "计划为空");
                return false;
            }
            if (!string.Equals(expectedPlanHash, canonicalPlan.planHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                error = "待批准 PlanHash 与当前 canonical 计划不一致；必须重新规划。";
                return false;
            }
            if (!TryResolveAuthorizationProfile(canonicalPlan, snapshot, true,
                    out AuthorizationProfile profile, out error)) return false;
            if (!ValidateExecutionEligibility(canonicalPlan, profile, out error)) return false;
            return TryRegisterAuthorization(CreateInvocation(snapshot, canonicalPlan, profile),
                canonicalPlan, profile, out error);
        }

        private static ESAutomationTaskInvocation CreateInvocation(ESAIBrainRequest request,
            ESAIBrainPlan plan, AuthorizationProfile profile)
        {
            return new ESAutomationTaskInvocation
            {
                invocationId = request.invocationId,
                brainPlanHash = plan.planHash,
                taskId = request.taskId,
                taskVersion = request.taskVersion,
                preset = request.preset ?? string.Empty,
                input = request.input == null ? new JObject() : (JObject)request.input.DeepClone(),
                fromAi = request.fromAi,
                dryRun = request.dryRun,
                actorId = string.IsNullOrWhiteSpace(request.actorId) ? "aibrain" : request.actorId,
                idempotencyKey = request.idempotencyKey ?? string.Empty,
                executionSnapshot = request.executionSnapshot,
                authorizationClass = profile.authorizationClass,
                authorizationBudgetClass = profile.budgetClass,
                authorizationHostId = profile.hostId,
                userInstructionHash = profile.instructionHash,
            };
        }

        private static bool ValidateExecutionEligibility(ESAIBrainPlan plan,
            AuthorizationProfile profile, out string error)
        {
            error = string.Empty;
            bool lowRiskDirected = profile != null
                && string.Equals(profile.authorizationClass, AuthorizationClassCurrentUser,
                    StringComparison.Ordinal)
                && IsLowRiskDirectedPlan(plan);
            bool explicitUserRuntime = profile != null
                && string.Equals(profile.authorizationClass, AuthorizationClassCurrentUser,
                    StringComparison.Ordinal)
                && ESAutomationWorkerRegistration.IsSha256(profile.instructionHash)
                && IsExplicitUserRuntimePlan(plan);
            bool bridgeUiMaterializer = profile != null
                && string.Equals(profile.authorizationClass, AuthorizationClassManaged,
                    StringComparison.Ordinal)
                && ESAutomationWorkerRegistration.IsSha256(profile.instructionHash)
                && IsExplicitUserRuntimePlan(plan);
            foreach (ESAIBrainSkillBinding skill in plan.skills)
            {
                if (skill.reviewRequired && !lowRiskDirected && !explicitUserRuntime
                    && !bridgeUiMaterializer)
                {
                    error = "Skill is still NeedsReview and cannot enter an AI execution plan: " + skill.name;
                    return false;
                }
                if (!lowRiskDirected && !explicitUserRuntime && !bridgeUiMaterializer
                    && !string.Equals(skill.runtimeEligibility, "authorized-only",
                        StringComparison.Ordinal))
                {
                    error = "Skill has no runtime acceptance eligibility: " + skill.name
                        + " (" + skill.runtimeEligibility + ")";
                    return false;
                }
            }
            return true;
        }

        private static bool IsExplicitUserRuntimePlan(ESAIBrainPlan plan)
        {
            if (plan == null || plan.command == null || plan.task == null)
                return false;
            if (!string.Equals(plan.command.id, "ui.materialize-screen", StringComparison.Ordinal)
                || !string.Equals(plan.command.riskLevel, "L2", StringComparison.Ordinal)
                || !string.Equals(plan.command.writeMode, "scoped-write", StringComparison.Ordinal))
                return false;
            if (!string.Equals(plan.task.taskId, "es.ui.materialize-screen", StringComparison.Ordinal)
                || plan.task.taskVersion != 1
                || !string.Equals(plan.task.workerType, "Other", StringComparison.Ordinal)
                || !plan.task.workerEnabled
                || !plan.task.allowAiInvoke)
                return false;
            return plan.task.capabilities != null
                && plan.task.capabilities.Contains("MaterializeUI");
        }

        private static bool IsExplicitUiMaterializerRequest(ESAIBrainRequest request)
        {
            return request != null
                && string.Equals(request.commandId, "ui.materialize-screen", StringComparison.Ordinal)
                && string.Equals(request.taskId, "es.ui.materialize-screen", StringComparison.Ordinal)
                && request.taskVersion == 1;
        }

        internal static bool TryBindTrustedHostProof(ESAIBrainRequest request,
            string hostId, string userInstructionHash, bool userDirected, out string error)
        {
            error = string.Empty;
            if (request == null)
            {
                error = "Trusted-host proof requires a managed AI request.";
                return false;
            }
            // A failed rebind must never leave a previously valid proof attached.
            request.trustedHostProof = null;
            if (!request.fromAi)
            {
                error = "Trusted-host proof requires a managed AI request.";
                return false;
            }
            string normalizedHost = hostId?.Trim() ?? string.Empty;
            if (!Regex.IsMatch(normalizedHost, "^[A-Za-z0-9._:-]{1,128}$"))
            {
                error = "Trusted host id is invalid.";
                return false;
            }
            string normalizedInstructionHash = userInstructionHash?.Trim().ToLowerInvariant()
                ?? string.Empty;
            if (userDirected && !ESAutomationWorkerRegistration.IsSha256(normalizedInstructionHash))
            {
                error = "Current-user proof requires a bound instruction SHA-256.";
                return false;
            }
            if (!userDirected
                && string.Equals(normalizedHost, "es.automation.ai-bridge", StringComparison.Ordinal)
                && IsExplicitUiMaterializerRequest(request))
            {
                // The local Bridge is gated by IsUserAuthorized before this proof is
                // created. Bind the exact request internally; external JSON cannot
                // assert a current-user flag or supply its own instruction hash.
                normalizedInstructionHash = ComputeTrustedHostRequestHash(request);
            }
            if (!string.IsNullOrWhiteSpace(normalizedInstructionHash)
                && !ESAutomationWorkerRegistration.IsSha256(normalizedInstructionHash))
            {
                error = "Trusted-host instruction hash is invalid.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(request.invocationId)
                || !Guid.TryParseExact(request.invocationId, "N", out _))
            {
                error = "Trusted-host proof requires a stable N format InvocationId.";
                return false;
            }

            DateTimeOffset issuedAtUtc = AuthorizationUtcNow;
            request.trustedHostProof = new AIBrainTrustedHostProof(
                normalizedHost,
                userDirected ? AuthorizationClassCurrentUser : AuthorizationClassManaged,
                normalizedInstructionHash,
                request.invocationId,
                request.actorId ?? string.Empty,
                ComputeTrustedHostRequestHash(request),
                issuedAtUtc,
                issuedAtUtc + TrustedHostProofLifetime);
            return true;
        }

        private static bool TryResolveAuthorizationProfile(ESAIBrainPlan plan,
            ESAIBrainRequest request, bool requireTrustedProof,
            out AuthorizationProfile profile, out string error)
        {
            profile = AuthorizationProfile.Untrusted();
            if (!TryValidateTrustedHostProof(request, out AIBrainTrustedHostProof proof,
                    out error))
                return !requireTrustedProof;

            profile = CreateAuthorizationProfile(plan, proof.authorizationClass,
                proof.hostId, proof.instructionHash);
            return true;
        }

        private static AuthorizationProfile CreateAuthorizationProfile(ESAIBrainPlan plan,
            string authorizationClass, string hostId, string instructionHash)
        {
            string budgetClass;
            int maxUses;
            if (plan != null && plan.command != null
                && string.Equals(plan.command.writeMode, "candidate-only", StringComparison.Ordinal)
                && (string.Equals(plan.command.riskLevel, "L1", StringComparison.Ordinal)
                    || string.Equals(plan.command.riskLevel, "L2", StringComparison.Ordinal)))
            {
                budgetClass = AuthorizationBudgetCandidate;
                maxUses = DefaultCandidateAuthorizationUses;
            }
            else if (string.Equals(authorizationClass, AuthorizationClassCurrentUser,
                         StringComparison.Ordinal) && IsLowRiskDirectedPlan(plan))
            {
                budgetClass = AuthorizationBudgetLowRisk;
                maxUses = DefaultLowRiskAuthorizationUses;
            }
            else
            {
                budgetClass = AuthorizationBudgetHighRisk;
                maxUses = DefaultHighRiskAuthorizationUses;
            }
            return new AuthorizationProfile(authorizationClass, budgetClass,
                hostId, instructionHash, maxUses);
        }

        private static bool TryValidateTrustedHostProof(ESAIBrainRequest request,
            out AIBrainTrustedHostProof proof, out string error)
        {
            proof = request?.trustedHostProof;
            error = string.Empty;
            if (request == null || proof == null)
            {
                error = "Trusted-host proof is missing.";
                return false;
            }
            DateTimeOffset now = AuthorizationUtcNow;
            if (proof.issuedAtUtc > now || proof.expiresAtUtc <= now
                || proof.expiresAtUtc - proof.issuedAtUtc > TrustedHostProofLifetime)
            {
                error = "Trusted-host proof is expired or outside its lifetime.";
                return false;
            }
            if (!string.Equals(proof.invocationId, request.invocationId, StringComparison.Ordinal)
                || !string.Equals(proof.actorId, request.actorId ?? string.Empty,
                    StringComparison.Ordinal))
            {
                error = "Trusted-host proof identity does not match the request.";
                return false;
            }
            if (!Regex.IsMatch(proof.hostId ?? string.Empty, "^[A-Za-z0-9._:-]{1,128}$")
                || (!string.IsNullOrWhiteSpace(proof.instructionHash)
                    && !ESAutomationWorkerRegistration.IsSha256(proof.instructionHash)))
            {
                error = "Trusted-host proof host or instruction hash is invalid.";
                return false;
            }
            if (!ESAutomationWorkerRegistration.IsSha256(proof.requestHash)
                || !string.Equals(proof.requestHash, ComputeTrustedHostRequestHash(request),
                    StringComparison.OrdinalIgnoreCase))
            {
                error = "Trusted-host proof request binding has drifted.";
                return false;
            }
            if (!string.Equals(proof.authorizationClass, AuthorizationClassManaged,
                    StringComparison.Ordinal)
                && !string.Equals(proof.authorizationClass, AuthorizationClassCurrentUser,
                    StringComparison.Ordinal))
            {
                error = "Trusted-host proof authorization class is invalid.";
                return false;
            }
            if (string.Equals(proof.authorizationClass, AuthorizationClassCurrentUser,
                    StringComparison.Ordinal)
                && !ESAutomationWorkerRegistration.IsSha256(proof.instructionHash))
            {
                error = "Current-user proof is not bound to an instruction hash.";
                return false;
            }
            return true;
        }

        private static bool TrySnapshotRequest(ESAIBrainRequest source,
            out ESAIBrainRequest snapshot, out string error)
        {
            snapshot = null;
            error = string.Empty;
            if (source == null) return true;
            try
            {
                snapshot = new ESAIBrainRequest
                {
                    objective = source.objective ?? string.Empty,
                    routeKeys = new List<string>(source.routeKeys ?? new List<string>()),
                    commandId = source.commandId ?? string.Empty,
                    skillNames = new List<string>(source.skillNames ?? new List<string>()),
                    workflow = source.workflow == null ? null : new ESAIBrainWorkflowAuthority
                    {
                        workflowId = source.workflow.workflowId ?? string.Empty,
                        contentHash = source.workflow.contentHash ?? string.Empty,
                        sourceAssetGuid = source.workflow.sourceAssetGuid ?? string.Empty,
                    },
                    taskId = source.taskId ?? string.Empty,
                    taskVersion = source.taskVersion,
                    preset = source.preset ?? string.Empty,
                    input = source.input == null ? new JObject() : (JObject)source.input.DeepClone(),
                    fromAi = source.fromAi,
                    dryRun = source.dryRun,
                    actorId = source.actorId ?? string.Empty,
                    invocationId = source.invocationId ?? string.Empty,
                    approvedPlanHash = source.approvedPlanHash ?? string.Empty,
                    idempotencyKey = source.idempotencyKey ?? string.Empty,
                    userDirectedRuntime = source.userDirectedRuntime,
                    userInstructionHash = source.userInstructionHash ?? string.Empty,
                    executionSnapshot = SnapshotExecutionSnapshot(source.executionSnapshot),
                    trustedHostProof = source.trustedHostProof,
                };
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static ESAutomationExecutionSnapshot SnapshotExecutionSnapshot(
            ESAutomationExecutionSnapshot source)
        {
            return source == null ? null : new ESAutomationExecutionSnapshot
            {
                snapshotId = source.snapshotId ?? string.Empty,
                inputManifestHash = source.inputManifestHash ?? string.Empty,
                sourceHash = source.sourceHash ?? string.Empty,
                taskContractHash = source.taskContractHash ?? string.Empty,
                commandHash = source.commandHash ?? string.Empty,
                brainPlanHash = source.brainPlanHash ?? string.Empty,
            };
        }

        private static ESAIBrainPlan CreateInvalidRequestPlan(ESAIBrainRequest request, string error)
        {
            var plan = new ESAIBrainPlan
            {
                contractVersion = ContractVersion,
                planId = Guid.NewGuid().ToString("N"),
                status = "InvalidRequest",
                objective = request?.objective ?? string.Empty,
                invocationId = request?.invocationId ?? string.Empty,
            };
            plan.blockers.Add("AIBrain 请求快照失败：" + error);
            return plan;
        }

        private static bool IsLowRiskDirectedPlan(ESAIBrainPlan plan)
        {
            if (plan == null || plan.command == null
                || !string.Equals(plan.command.riskLevel, "L1", StringComparison.Ordinal)) return false;
            if (!string.Equals(plan.command.writeMode, "read-only", StringComparison.Ordinal)
                && !string.Equals(plan.command.writeMode, "documentation-write", StringComparison.Ordinal)
                && !string.Equals(plan.command.writeMode, "candidate-only", StringComparison.Ordinal)) return false;
            if (plan.task != null && !string.Equals(plan.task.workerType, "DotNet", StringComparison.Ordinal)
                && !string.Equals(plan.task.workerType, "Other", StringComparison.Ordinal)) return false;
            return true;
        }

        private static ESAutomationTaskInvocationResult RejectPlan(out ESAIBrainPlan plan, string message)
        {
            plan = null;
            return ESAutomationTaskInvocationResult.Rejected(message);
        }

        internal static bool TryValidateAuthorization(ESAutomationTaskInvocation invocation,
            out string reason)
        {
            return TryAccessAuthorization(invocation, false, out reason);
        }

        internal static bool TryConsumeAuthorization(ESAutomationTaskInvocation invocation,
            out string reason)
        {
            return TryAccessAuthorization(invocation, true, out reason);
        }

        private static bool TryAccessAuthorization(ESAutomationTaskInvocation invocation,
            bool consume, out string reason)
        {
            reason = string.Empty;
            if (invocation == null || !invocation.fromAi
                || !ESAutomationWorkerRegistration.IsSha256(invocation.brainPlanHash)
                || string.IsNullOrWhiteSpace(invocation.invocationId)
                || !Guid.TryParseExact(invocation.invocationId, "N", out _))
            {
                reason = "AI Automation 调用缺少有效的 AIBrain PlanHash 或 InvocationId。";
                return false;
            }

            lock (AuthorizationLock)
            {
                if (!TryOpenAuthorizationLock(out string storePath,
                        out FileStream storeLock, out string lockError))
                {
                    reason = "AIBrain 执行许可事务锁不可用：" + lockError;
                    return false;
                }
                using (storeLock)
                {
                    var transaction = new AuthorizationStoreTransaction(AuthorizationUtcNow);
                    DateTimeOffset transactionUtc = transaction.UtcNow;
                    if (!TryLoadAuthorizationStore(storePath, false, transactionUtc,
                            out AIBrainAuthorizationStore store, out string loadError))
                    {
                        reason = "AIBrain 执行许可存储不可用：" + loadError;
                        return false;
                    }
                    if (TransitionExpiredAuthorizations(store, transactionUtc)
                        && !transaction.TryPersistAuthorizationStore(storePath, store,
                            out string expiryError))
                    {
                        reason = "AIBrain 过期许可无法持久化为 tombstone：" + expiryError;
                        return false;
                    }

                    AIBrainAuthorizationRecord record = store.entries.FirstOrDefault(item =>
                        string.Equals(item.planHash, invocation.brainPlanHash,
                            StringComparison.OrdinalIgnoreCase));
                    if (record == null)
                    {
                        reason = "AIBrain PlanHash 未签发或不属于当前策略代际。";
                        return false;
                    }
                    if (!string.Equals(record.invocationId, invocation.invocationId,
                            StringComparison.Ordinal))
                    {
                        reason = "AIBrain PlanHash 与 InvocationId 不一致。";
                        return false;
                    }
                    if (!string.Equals(record.status, AuthorizationStatusActive,
                            StringComparison.Ordinal))
                    {
                        reason = "AIBrain 执行许可已进入终态：" + record.status + "。";
                        return false;
                    }
                    string bindingHash = ComputeAuthorizationBindingHash(invocation,
                        record.authorizationClass, record.budgetClass, record.hostId,
                        record.instructionHash, record.maxUses, record.issuedAtUtc,
                        record.expiresAtUtc);
                    if (!string.Equals(record.bindingHash, bindingHash,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        reason = "AIBrain 执行许可与当前 Invocation 不一致，已拒绝。"
                            + " [stored=" + record.bindingHash + "; computed=" + bindingHash + "]";
                        return false;
                    }
                    string key = invocation.idempotencyKey ?? string.Empty;
                    if (record.maxUses > 1 && string.IsNullOrWhiteSpace(key))
                    {
                        reason = "可复用的 AIBrain 执行许可要求每次调用提供非空 idempotencyKey。";
                        return false;
                    }
                    if (!string.IsNullOrWhiteSpace(key)
                        && (key.Length > 160 || !Regex.IsMatch(key, "^[A-Za-z0-9._:-]+$")))
                    {
                        reason = "AIBrain idempotencyKey 格式无效。";
                        return false;
                    }
                    if (!string.IsNullOrWhiteSpace(key)
                        && record.usedIdempotencyKeys.Contains(key,
                            StringComparer.OrdinalIgnoreCase))
                    {
                        reason = "重复的 idempotencyKey 已拒绝，避免重复副作用。";
                        return false;
                    }
                    if (record.usedCount >= record.maxUses)
                    {
                        reason = "AIBrain 执行许可已达到最大复用次数。";
                        return false;
                    }
                    if (!consume) return true;

                    record.usedCount++;
                    if (!string.IsNullOrWhiteSpace(key)) record.usedIdempotencyKeys.Add(key);
                    if (record.usedCount == record.maxUses)
                    {
                        record.status = AuthorizationStatusExhausted;
                        record.terminalAtUtc = transactionUtc;
                    }
                    store.revision++;
                    if (!transaction.TryPersistAuthorizationStore(storePath, store, out string consumeError))
                    {
                        reason = "AIBrain 执行许可消费记录无法持久化：" + consumeError;
                        return false;
                    }
                    return true;
                }
            }
        }

        private static bool TryRegisterAuthorization(ESAutomationTaskInvocation invocation,
            ESAIBrainPlan plan, AuthorizationProfile profile, out string error)
        {
            error = string.Empty;
            if (invocation == null || plan == null || profile == null
                || !ESAutomationWorkerRegistration.IsSha256(plan.planHash)
                || !string.Equals(invocation.brainPlanHash, plan.planHash,
                    StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(invocation.invocationId)
                || !Guid.TryParseExact(invocation.invocationId, "N", out _)
                || string.IsNullOrWhiteSpace(plan.planId)
                || !Guid.TryParseExact(plan.planId, "N", out _)
                || !Regex.IsMatch(invocation.actorId ?? string.Empty,
                    "^[A-Za-z0-9._:-]{1,128}$")
                || !IsValidAuthorizationProfile(profile))
            {
                error = "AIBrain 授权注册输入无效。";
                return false;
            }
            lock (AuthorizationLock)
            {
                if (!TryOpenAuthorizationLock(out string storePath,
                        out FileStream storeLock, out error)) return false;
                using (storeLock)
                {
                    var transaction = new AuthorizationStoreTransaction(AuthorizationUtcNow);
                    DateTimeOffset transactionUtc = transaction.UtcNow;
                    if (!TryLoadAuthorizationStore(storePath, true, transactionUtc,
                            out AIBrainAuthorizationStore store, out error)) return false;
                    if (store.retiredInvocationIds.Contains(invocation.invocationId,
                            StringComparer.Ordinal))
                    {
                        error = "旧策略 Invocation 不得在 Policy v5 中重签；必须使用新的 Invocation。";
                        return false;
                    }
                    if (TransitionExpiredAuthorizations(store, transactionUtc)
                        && !transaction.TryPersistAuthorizationStore(storePath, store,
                            out string expiryError))
                    {
                        error = "AIBrain 过期许可无法持久化为 tombstone：" + expiryError;
                        return false;
                    }

                    AIBrainAuthorizationRecord existingInvocation = store.entries.FirstOrDefault(item =>
                        string.Equals(item.invocationId, invocation.invocationId,
                            StringComparison.Ordinal));
                    if (existingInvocation != null)
                    {
                        if (!string.Equals(existingInvocation.planHash, invocation.brainPlanHash,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            error = "InvocationId 已绑定另一 PlanHash，禁止换绑重签。";
                            return false;
                        }
                        if (!string.Equals(existingInvocation.status, AuthorizationStatusActive,
                                StringComparison.Ordinal))
                        {
                            error = "InvocationId 已进入 " + existingInvocation.status
                                + " 终态，禁止重签。";
                            return false;
                        }
                        string existingBinding = ComputeAuthorizationBindingHash(invocation,
                            existingInvocation.authorizationClass, existingInvocation.budgetClass,
                            existingInvocation.hostId, existingInvocation.instructionHash,
                            existingInvocation.maxUses, existingInvocation.issuedAtUtc,
                            existingInvocation.expiresAtUtc);
                        if (!string.Equals(existingInvocation.bindingHash, existingBinding,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            error = "现有 Invocation 授权与当前 canonical 请求不一致。";
                            return false;
                        }
                        return true;
                    }
                    if (store.entries.Any(item => string.Equals(item.planHash,
                            invocation.brainPlanHash, StringComparison.OrdinalIgnoreCase)))
                    {
                        error = "PlanHash 已绑定另一 InvocationId。";
                        return false;
                    }
                    if (store.entries.Count >= authorizationRecordLimit)
                    {
                        error = "AIBrain 授权存储已达到容量上限；现有 Active 许可仍可消费，"
                            + "新 Invocation 必须等待显式维护。";
                        return false;
                    }

                    DateTimeOffset issuedAtUtc = transactionUtc;
                    DateTimeOffset expiresAtUtc = issuedAtUtc + AuthorizationLifetime;
                    store.entries.Add(new AIBrainAuthorizationRecord
                    {
                        planHash = invocation.brainPlanHash,
                        bindingHash = ComputeAuthorizationBindingHash(invocation,
                            profile.authorizationClass, profile.budgetClass, profile.hostId,
                            profile.instructionHash, profile.maxUses, issuedAtUtc, expiresAtUtc),
                        planId = plan.planId ?? string.Empty,
                        invocationId = invocation.invocationId,
                        actorId = invocation.actorId ?? string.Empty,
                        authorizationClass = profile.authorizationClass,
                        budgetClass = profile.budgetClass,
                        hostId = profile.hostId,
                        instructionHash = profile.instructionHash,
                        status = AuthorizationStatusActive,
                        issuedAtUtc = issuedAtUtc,
                        expiresAtUtc = expiresAtUtc,
                        terminalAtUtc = null,
                        maxUses = profile.maxUses,
                        usedCount = 0,
                        usedIdempotencyKeys = new List<string>(),
                    });
                    store.revision++;
                    return transaction.TryPersistAuthorizationStore(storePath, store, out error);
                }
            }
        }

        private static DateTimeOffset AuthorizationUtcNow => authorizationUtcNow();

        private static string CurrentAuthorizationStorePath =>
            string.IsNullOrWhiteSpace(authorizationStorePathOverride)
                ? AuthorizationStorePath : authorizationStorePathOverride;

        private static bool TryOpenAuthorizationLock(out string storePath,
            out FileStream storeLock, out string error)
        {
            storePath = string.Empty;
            storeLock = null;
            error = string.Empty;
            string relativeStorePath = CurrentAuthorizationStorePath.Replace('\\', '/');
            if (!TryResolveProjectPath(relativeStorePath, out storePath, out error)) return false;
            if (!TryResolveProjectPath(relativeStorePath + ".lock", out string lockPath, out error))
                return false;
            try
            {
                string parent = Path.GetDirectoryName(storePath);
                if (string.IsNullOrWhiteSpace(parent))
                    throw new InvalidDataException("授权存储目录无效。");
                ESManagedFileIO.EnsurePath(storePath, false, ESAutomationPathPolicy.ProjectRoot);
                ESManagedFileIO.EnsurePath(lockPath, false, ESAutomationPathPolicy.ProjectRoot);
                Directory.CreateDirectory(parent);
                // Re-check after creation so a concurrent reparse-point swap fails closed.
                ESManagedFileIO.EnsurePath(storePath, false, ESAutomationPathPolicy.ProjectRoot);
                ESManagedFileIO.EnsurePath(lockPath, false, ESAutomationPathPolicy.ProjectRoot);
                // The lock file is permanent. Deleting it after release would allow two writers
                // to hold different file identities and would defeat cross-process exclusion.
                storeLock = new FileStream(lockPath, FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None);
                return true;
            }
            catch (Exception exception)
            {
                storeLock?.Dispose();
                storeLock = null;
                error = exception.Message;
                return false;
            }
        }

        private static bool TryLoadAuthorizationStore(string path,
            bool allowLegacyReinitialization, DateTimeOffset validationUtc,
            out AIBrainAuthorizationStore store, out string error)
        {
            store = null;
            error = string.Empty;
            if (!File.Exists(path))
            {
                store = CreateEmptyAuthorizationStore();
                return true;
            }
            if (!TryReadTextAndHash(path, out string text, out _, out error)) return false;
            try
            {
                JObject document = JObject.Parse(text);
                int? schemaVersion = document.Value<int?>("schemaVersion");
                int? policyVersion = document.Value<int?>("authorizationPolicyVersion");
                if (schemaVersion == 2 && policyVersion == 4)
                {
                    if (!allowLegacyReinitialization)
                    {
                        error = "Policy v4/schema 2 授权已 stale，必须使用新 Invocation 重新规划。";
                        return false;
                    }
                    JArray legacyEntries = document["entries"] as JArray
                        ?? throw new InvalidDataException("旧授权存储缺少 entries。");
                    if (legacyEntries.Count > authorizationRecordLimit)
                        throw new InvalidDataException("旧授权存储超过迁移容量上限。");
                    var retiredLegacyInvocations = new HashSet<string>(StringComparer.Ordinal);
                    foreach (JToken legacyToken in legacyEntries)
                    {
                        if (!(legacyToken is JObject legacyRecord))
                            throw new InvalidDataException("旧授权存储 entries 包含非对象记录。");
                        string invocationId = legacyRecord.Value<string>("invocationId")
                            ?? string.Empty;
                        string planHash = legacyRecord.Value<string>("planHash") ?? string.Empty;
                        if (!Guid.TryParseExact(invocationId, "N", out _)
                            || !ESAutomationWorkerRegistration.IsSha256(planHash))
                            throw new InvalidDataException("旧授权存储包含无效 Invocation 或 PlanHash。");
                        if (!retiredLegacyInvocations.Add(invocationId))
                            throw new InvalidDataException("旧授权存储包含重复 InvocationId。");
                    }
                    store = CreateEmptyAuthorizationStore();
                    store.retiredInvocationIds = retiredLegacyInvocations
                        .OrderBy(item => item, StringComparer.Ordinal).ToList();
                    return true;
                }
                if (schemaVersion != AuthorizationStoreSchemaVersion
                    || policyVersion != AuthorizationPolicyVersion)
                {
                    error = "授权存储 schema/policy 代际无效且不能自动覆盖。";
                    return false;
                }
                store = document.ToObject<AIBrainAuthorizationStore>();
                return ValidateAuthorizationStore(store, validationUtc, out error);
            }
            catch (Exception exception)
            {
                error = "授权存储 JSON 无法安全加载：" + exception.Message;
                return false;
            }
        }

        private static AIBrainAuthorizationStore CreateEmptyAuthorizationStore()
        {
            return new AIBrainAuthorizationStore
            {
                schemaVersion = AuthorizationStoreSchemaVersion,
                authorizationPolicyVersion = AuthorizationPolicyVersion,
                revision = 0,
                retiredInvocationIds = new List<string>(),
                entries = new List<AIBrainAuthorizationRecord>(),
            };
        }

        private static bool ValidateAuthorizationStore(AIBrainAuthorizationStore store,
            DateTimeOffset validationUtc, out string error)
        {
            error = string.Empty;
            if (store == null || store.schemaVersion != AuthorizationStoreSchemaVersion
                || store.authorizationPolicyVersion != AuthorizationPolicyVersion
                || store.revision < 0 || store.entries == null
                || store.retiredInvocationIds == null
                || store.entries.Count > authorizationRecordLimit
                || store.retiredInvocationIds.Count > authorizationRecordLimit)
            {
                error = "授权存储头、revision 或容量无效。";
                return false;
            }
            var planHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var invocationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (AIBrainAuthorizationRecord record in store.entries)
            {
                if (!ValidateAuthorizationRecord(record, validationUtc, planHashes, invocationIds,
                        out error)) return false;
            }
            if (store.retiredInvocationIds.Any(item => string.IsNullOrWhiteSpace(item)
                    || !Guid.TryParseExact(item, "N", out _))
                || store.retiredInvocationIds.Distinct(StringComparer.Ordinal).Count()
                    != store.retiredInvocationIds.Count
                || store.retiredInvocationIds.Any(invocationIds.Contains))
            {
                error = "授权存储包含无效、重复或仍处于活动记录的退役 InvocationId。";
                return false;
            }
            store.retiredInvocationIds = store.retiredInvocationIds
                .OrderBy(item => item, StringComparer.Ordinal).ToList();
            return true;
        }

        private static bool ValidateAuthorizationRecord(AIBrainAuthorizationRecord record,
            DateTimeOffset now, HashSet<string> planHashes, HashSet<string> invocationIds,
            out string error)
        {
            error = string.Empty;
            if (record == null
                || !ESAutomationWorkerRegistration.IsSha256(record.planHash)
                || !ESAutomationWorkerRegistration.IsSha256(record.bindingHash)
                || !planHashes.Add(record.planHash)
                || string.IsNullOrWhiteSpace(record.invocationId)
                || !Guid.TryParseExact(record.invocationId, "N", out _)
                || !invocationIds.Add(record.invocationId)
                || string.IsNullOrWhiteSpace(record.planId)
                || !Guid.TryParseExact(record.planId, "N", out _)
                || !Regex.IsMatch(record.actorId ?? string.Empty, "^[A-Za-z0-9._:-]{1,128}$")
                || !Regex.IsMatch(record.hostId ?? string.Empty, "^[A-Za-z0-9._:-]{1,128}$"))
            {
                error = "授权存储包含无效或重复身份。";
                return false;
            }
            var profile = new AuthorizationProfile(record.authorizationClass,
                record.budgetClass, record.hostId, record.instructionHash, record.maxUses);
            if (!IsValidAuthorizationProfile(profile)
                || record.issuedAtUtc == default
                || record.expiresAtUtc <= record.issuedAtUtc
                || record.expiresAtUtc - record.issuedAtUtc > AuthorizationLifetime
                || record.issuedAtUtc > now + TimeSpan.FromMinutes(1))
            {
                error = "授权存储包含无效策略分类或 TTL。";
                return false;
            }
            List<string> usedKeys = record.usedIdempotencyKeys ?? new List<string>();
            bool validKeys = usedKeys.All(key => !string.IsNullOrWhiteSpace(key)
                    && key.Length <= 160 && Regex.IsMatch(key, "^[A-Za-z0-9._:-]+$"))
                && usedKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count() == usedKeys.Count;
            bool validUsage = record.usedCount >= 0 && record.usedCount <= record.maxUses
                && (record.maxUses == 1 ? usedKeys.Count <= record.usedCount
                    : usedKeys.Count == record.usedCount);
            if (!validKeys || !validUsage)
            {
                error = "授权存储包含无效使用计数或幂等键。";
                return false;
            }
            record.usedIdempotencyKeys = usedKeys;
            bool validTerminalTime = !record.terminalAtUtc.HasValue
                || (record.terminalAtUtc.Value <= now
                    && record.terminalAtUtc.Value <= record.expiresAtUtc);
            bool validState =
                validTerminalTime &&
                ((string.Equals(record.status, AuthorizationStatusActive,
                     StringComparison.Ordinal)
                  && record.usedCount < record.maxUses
                  && !record.terminalAtUtc.HasValue)
                || (string.Equals(record.status, AuthorizationStatusExhausted,
                        StringComparison.Ordinal)
                    && record.usedCount == record.maxUses
                    && record.terminalAtUtc.HasValue
                    && record.terminalAtUtc.Value >= record.issuedAtUtc
                    && record.terminalAtUtc.Value <= record.expiresAtUtc)
                || (string.Equals(record.status, AuthorizationStatusExpired,
                        StringComparison.Ordinal)
                    && record.usedCount < record.maxUses
                    && record.terminalAtUtc.HasValue
                    && record.terminalAtUtc.Value == record.expiresAtUtc));
            if (!validState)
            {
                error = "授权存储包含无效 Active/Exhausted/Expired 状态。";
                return false;
            }
            return true;
        }

        private static bool IsValidAuthorizationProfile(AuthorizationProfile profile)
        {
            if (profile == null
                || (!string.Equals(profile.authorizationClass, AuthorizationClassManaged,
                        StringComparison.Ordinal)
                    && !string.Equals(profile.authorizationClass, AuthorizationClassCurrentUser,
                        StringComparison.Ordinal))
                || !Regex.IsMatch(profile.hostId ?? string.Empty,
                    "^[A-Za-z0-9._:-]{1,128}$")) return false;
            if (string.Equals(profile.authorizationClass, AuthorizationClassCurrentUser,
                    StringComparison.Ordinal)
                && !ESAutomationWorkerRegistration.IsSha256(profile.instructionHash)) return false;
            if (!string.IsNullOrWhiteSpace(profile.instructionHash)
                && !ESAutomationWorkerRegistration.IsSha256(profile.instructionHash)) return false;
            return string.Equals(profile.budgetClass, AuthorizationBudgetLowRisk,
                       StringComparison.Ordinal) && profile.maxUses == DefaultLowRiskAuthorizationUses
                || string.Equals(profile.budgetClass, AuthorizationBudgetCandidate,
                       StringComparison.Ordinal) && profile.maxUses == DefaultCandidateAuthorizationUses
                || string.Equals(profile.budgetClass, AuthorizationBudgetHighRisk,
                       StringComparison.Ordinal) && profile.maxUses == DefaultHighRiskAuthorizationUses;
        }

        private static bool TransitionExpiredAuthorizations(AIBrainAuthorizationStore store,
            DateTimeOffset now)
        {
            bool changed = false;
            foreach (AIBrainAuthorizationRecord record in store.entries)
            {
                if (!string.Equals(record.status, AuthorizationStatusActive,
                        StringComparison.Ordinal) || record.expiresAtUtc > now) continue;
                record.status = AuthorizationStatusExpired;
                record.terminalAtUtc = record.expiresAtUtc;
                changed = true;
            }
            if (changed) store.revision++;
            return changed;
        }

        private static bool TryPersistAuthorizationStore(string path,
            AIBrainAuthorizationStore store, DateTimeOffset validationUtc, out string error)
        {
            error = string.Empty;
            try
            {
                store.entries = store.entries
                    .OrderBy(item => item.planHash, StringComparer.OrdinalIgnoreCase).ToList();
                if (!ValidateAuthorizationStore(store, validationUtc,
                        out string validationError))
                {
                    error = "授权存储写前校验失败：" + validationError;
                    return false;
                }
                ESManagedFileIO.WriteTextAtomic(path,
                    JsonConvert.SerializeObject(store, Formatting.None), StrictUtf8,
                    ESAutomationPathPolicy.ProjectRoot);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static string ComputeAuthorizationBindingHash(ESAutomationTaskInvocation invocation,
            string authorizationClass, string budgetClass, string hostId,
            string instructionHash, int maxUses, DateTimeOffset issuedAtUtc,
            DateTimeOffset expiresAtUtc)
        {
            return ComputeCanonicalSha256(JToken.FromObject(new
            {
                authorizationPolicyVersion = AuthorizationPolicyVersion,
                authorizationClass,
                budgetClass,
                hostId,
                instructionHash,
                invocationAuthorizationClass = invocation.authorizationClass,
                invocationBudgetClass = invocation.authorizationBudgetClass,
                invocationHostId = invocation.authorizationHostId,
                invocationInstructionHash = invocation.userInstructionHash,
                maxUses,
                // Bind the instant, not its serialized offset representation. Unity's
                // Json.NET settings may normalize DateTimeOffset offsets on reload;
                // UTC ticks keep registration and consumption canonical across a
                // process/domain reload while preserving the exact expiry window.
                issuedAtUtcTicks = issuedAtUtc.UtcTicks,
                expiresAtUtcTicks = expiresAtUtc.UtcTicks,
                invocation.invocationId,
                invocation.brainPlanHash,
                invocation.taskId,
                invocation.taskVersion,
                invocation.preset,
                input = invocation.input ?? new JObject(),
                invocation.fromAi,
                invocation.dryRun,
                invocation.actorId,
                invocation.executionSnapshot,
            }));
        }

#if UNITY_INCLUDE_TESTS
        internal static IDisposable Internal_BeginAuthorizationTestScope(
            string projectRelativeStorePath, DateTimeOffset utcNow, int recordLimit = 64)
        {
            if (string.IsNullOrWhiteSpace(projectRelativeStorePath)
                || Path.IsPathRooted(projectRelativeStorePath)
                || recordLimit < 1 || recordLimit > MaximumAuthorizationRecords)
                throw new ArgumentException("Authorization test scope is outside its bounded test root.");
            if (!TryResolveProjectPath(projectRelativeStorePath,
                    out string normalizedStorePath, out string pathError))
                throw new ArgumentException(pathError);
            if (!TryResolveProjectPath("ES/Output/Automation/AIBrain/Tests",
                    out string normalizedTestsRoot, out string rootError))
                throw new ArgumentException(rootError);
            if (string.Equals(normalizedStorePath, normalizedTestsRoot,
                    StringComparison.OrdinalIgnoreCase)
                || !IsSameOrChildPath(normalizedTestsRoot, normalizedStorePath))
                throw new ArgumentException("Authorization test scope is outside its bounded test root.");

            lock (AuthorizationLock)
            {
                string previousPath = authorizationStorePathOverride;
                Func<DateTimeOffset> previousClock = authorizationUtcNow;
                int previousLimit = authorizationRecordLimit;
                authorizationStorePathOverride = ToProjectRelative(normalizedStorePath);
                authorizationUtcNow = () => utcNow;
                authorizationRecordLimit = recordLimit;
                return new AuthorizationTestScope(previousPath, previousClock, previousLimit);
            }
        }

        internal static void Internal_SetAuthorizationUtcNowForTests(DateTimeOffset utcNow)
        {
            lock (AuthorizationLock) authorizationUtcNow = () => utcNow;
        }

        internal static void Internal_SetAuthorizationUtcNowProviderForTests(
            Func<DateTimeOffset> utcNowProvider)
        {
            if (utcNowProvider == null) throw new ArgumentNullException(nameof(utcNowProvider));
            lock (AuthorizationLock) authorizationUtcNow = utcNowProvider;
        }

        internal static string Internal_AuthorizationStorePathForTests()
        {
            if (!TryResolveProjectPath(CurrentAuthorizationStorePath,
                    out string path, out string error)) throw new InvalidOperationException(error);
            return path;
        }

        internal static string Internal_AuthorizationLockPathForTests()
        {
            if (!TryResolveProjectPath(CurrentAuthorizationStorePath + ".lock",
                    out string path, out string error)) throw new InvalidOperationException(error);
            return path;
        }

        internal static bool Internal_TryRegisterAuthorizationForTests(
            ESAutomationTaskInvocation invocation, ESAIBrainPlan plan,
            bool userDirected, out string error)
        {
            string instructionHash = userDirected ? new string('a', 64) : string.Empty;
            AuthorizationProfile profile = CreateAuthorizationProfile(plan,
                userDirected ? AuthorizationClassCurrentUser : AuthorizationClassManaged,
                userDirected ? "es.tests.current-user" : "es.tests.managed",
                instructionHash);
            invocation.authorizationClass = profile.authorizationClass;
            invocation.authorizationBudgetClass = profile.budgetClass;
            invocation.authorizationHostId = profile.hostId;
            invocation.userInstructionHash = profile.instructionHash;
            return TryRegisterAuthorization(invocation, plan, profile, out error);
        }

        internal static bool Internal_ValidateTrustedHostProofForTests(
            ESAIBrainRequest request, out string error)
        {
            return TryValidateTrustedHostProof(request, out _, out error);
        }

        internal static void Internal_ResetAuthorizationCacheForTests()
        {
            // Policy v5 intentionally has no authoritative process-local grant cache.
        }

        private sealed class AuthorizationTestScope : IDisposable
        {
            private readonly string previousPath;
            private readonly Func<DateTimeOffset> previousClock;
            private readonly int previousLimit;
            private bool disposed;

            public AuthorizationTestScope(string previousPath,
                Func<DateTimeOffset> previousClock, int previousLimit)
            {
                this.previousPath = previousPath;
                this.previousClock = previousClock;
                this.previousLimit = previousLimit;
            }

            public void Dispose()
            {
                lock (AuthorizationLock)
                {
                    if (disposed) return;
                    authorizationStorePathOverride = previousPath;
                    authorizationUtcNow = previousClock;
                    authorizationRecordLimit = previousLimit;
                    disposed = true;
                }
            }
        }
#endif

        /// <summary>
        /// Emit a bounded, read-only capability drift signal. The signal contains
        /// metadata hashes only; it never grants authority or loads Skill documents.
        /// Callers must perform route-scoped refresh and re-plan after a bound change.
        /// </summary>
        public static void NotifyCapabilityDrift(string trigger)
        {
            string normalizedTrigger = string.IsNullOrWhiteSpace(trigger)
                ? "unknown" : trigger.Trim();
            string fingerprint = ComputeCapabilityMetadataFingerprint();
            ESAIBrainCapabilityDriftSignal signal;
            lock (CapabilityDriftLock)
            {
                capabilityDriftGeneration++;
                capabilityMetadataFingerprint = fingerprint;
                capabilityDriftTrigger = normalizedTrigger;
                signal = new ESAIBrainCapabilityDriftSignal
                {
                    generation = capabilityDriftGeneration,
                    trigger = normalizedTrigger,
                    metadataFingerprint = fingerprint,
                    nextAction = "route-scoped-compare-and-replan",
                };
            }
            System.Action<ESAIBrainCapabilityDriftSignal> handler = CapabilityDriftDetected;
            if (handler == null) return;
            try
            {
                handler(signal);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Poll metadata at most once per second. This is intentionally cheaper than
        /// reading Skill content and only emits when the metadata fingerprint changes.
        /// </summary>
        public static bool PollCapabilityDrift(string trigger = "catalog-change")
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            lock (CapabilityDriftLock)
            {
                if (now < nextCapabilityPollUtc) return false;
                nextCapabilityPollUtc = now.AddSeconds(1);
            }

            string fingerprint = ComputeCapabilityMetadataFingerprint();
            lock (CapabilityDriftLock)
            {
                if (string.IsNullOrWhiteSpace(capabilityMetadataFingerprint))
                {
                    capabilityMetadataFingerprint = fingerprint;
                    capabilityDriftTrigger = trigger ?? string.Empty;
                    return false;
                }
                if (string.Equals(capabilityMetadataFingerprint, fingerprint,
                        StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            NotifyCapabilityDrift(string.IsNullOrWhiteSpace(trigger) ? "metadata-change" : trigger);
            return true;
        }

        private static string ComputeCapabilityMetadataFingerprint()
        {
            var canonical = new StringBuilder();
            foreach (string relativePath in CapabilityMetadataPaths)
                AppendCapabilityMetadataHash(canonical, relativePath);

            if (TryResolveProjectPath(ProjectSkillsRoot, out string skillsRoot, out _)
                && Directory.Exists(skillsRoot))
            {
                foreach (string governancePath in Directory.GetFiles(
                    skillsRoot, SkillGovernanceMetadataFileName, SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    string relativePath = ToProjectRelative(governancePath);
                    AppendCapabilityMetadataHash(canonical, relativePath);
                }
            }

            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(
                        StrictUtf8.GetBytes(canonical.ToString())))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void AppendCapabilityMetadataHash(StringBuilder canonical, string relativePath)
        {
            string normalized = (relativePath ?? string.Empty).Replace('\\', '/');
            if (!TryResolveProjectPath(normalized, out string fullPath, out string pathError)
                || !File.Exists(fullPath))
            {
                canonical.Append(normalized).Append("|missing|").Append(pathError ?? string.Empty).Append('\n');
                return;
            }
            if (!TryReadTextAndHash(fullPath, out _, out string hash, out string error))
            {
                canonical.Append(normalized).Append("|unreadable|").Append(error ?? string.Empty).Append('\n');
                return;
            }
            canonical.Append(normalized).Append('|').Append(hash).Append('\n');
        }

        /// <summary>
        /// Explicit, read-only discovery of the production surfaces that AIBrain can route.
        /// Directory enumeration is only performed after an explicit caller request; this
        /// method is never called from domain-load registration.
        /// </summary>
        public static ESAIBrainProductionSurface DescribeProductionSurface(
            IEnumerable<string> requestedRouteKeys = null)
        {
            List<string> routeKeys = NormalizeDiscoveryRouteKeys(requestedRouteKeys);
            if (routeKeys.Count == 0)
                routeKeys.AddRange(DefaultDiscoveryRouteKeys);

            var surface = new ESAIBrainProductionSurface
            {
                contractVersion = ContractVersion,
                generatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            };
            surface.routeKeys.AddRange(routeKeys);

            var probe = new ESAIBrainPlan
            {
                contractVersion = ContractVersion,
                objective = "AIBrain 生产力面发现",
                authority = new ESAIBrainAuthoritySnapshot(),
            };
            TryReadAiwarnings(probe, probe.objective, routeKeys, out _);
            TryReadKnowledge(probe, routeKeys, out _);
            surface.warnings.AddRange(probe.warnings);
            surface.knowledge.AddRange(probe.knowledge);
            surface.blockers.AddRange(probe.blockers);

            CollectAbcModes(surface);
            CollectCommands(surface);
            CollectSkills(surface);
            CollectAutomationAndCli(surface);
            CollectDiagnostics(surface);
            surface.mcp.AddRange(ESAutomationAiBridge.CopyMcpCapabilitiesForBrain());

            surface.status = surface.blockers.Count == 0 ? "Ready" : "Partial";
            surface.failureTelemetry = ESAIBrainFailureTelemetry.Snapshot();
            surface.inventoryHash = ComputeProductionSurfaceHash(surface);
            return surface;
        }

        private static readonly string[] DefaultDiscoveryRouteKeys =
        {
            "aibrain", "orchestration", "task-routing", "evidence",
            "startup", "authority", "aiwarnings", "context",
            "skill", "routing", "validation", "iteration",
            "task", "read", "snapshot", "consistency", "parser", "projection", "binary",
            "feishu", "lark", "external-adapter", "dry-run",
        };

        private static readonly string[] RequiredAbcModeIds =
        {
            "ABCD.Dynamic", "ABCC.Core", "ABCP.Part",
        };

        private static List<string> NormalizeDiscoveryRouteKeys(IEnumerable<string> values)
        {
            List<string> routeKeys = NormalizeValues(values);
            if (routeKeys.Any(IsAbcdDynamicRouteAlias)
                && !routeKeys.Contains("agent-mechanism-replication", StringComparer.Ordinal))
                routeKeys.Add("agent-mechanism-replication");
            return routeKeys;
        }

        private static bool IsAbcdDynamicRouteAlias(string value)
        {
            switch (value)
            {
                case "abcd":
                case "abcd.dynamic":
                case "abcd dynamic":
                case "abcd动态":
                case "abcd动态体系":
                case "abcd动态协作":
                case "es动态协作体":
                case "es 动态协作体":
                case "动态协作体":
                case "动态协作接管":
                case "abcd接管":
                    return true;
                default:
                    return false;
            }
        }

        private static void CollectAbcModes(ESAIBrainProductionSurface surface)
        {
            if (!TryResolveProjectPath(AbcModeRegistryPath, out string path, out string pathError))
            {
                surface.blockers.Add("ABC 模式注册表路径无效：" + pathError);
                return;
            }
            if (!TryReadTextAndHash(path, out string text, out string registryHash, out string readError))
            {
                surface.blockers.Add("ABC 模式注册表无法严格读取：" + readError);
                return;
            }

            try
            {
                JObject root = JObject.Parse(text);
                if (root.Value<int?>("schemaVersion") != 1
                    || !string.Equals(root.Value<string>("registryId"),
                        "es-ai-abc-mode-registry", StringComparison.Ordinal))
                    throw new InvalidDataException("ABC 模式注册表身份或版本无效。");

                JObject namingAuthority = root["namingAuthority"] as JObject
                    ?? throw new InvalidDataException("ABC 模式命名权威缺失。");
                string authorityId = namingAuthority.Value<string>("authorityId") ?? string.Empty;
                string authorityVersion = namingAuthority.Value<string>("version") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(authorityId)
                    || string.IsNullOrWhiteSpace(authorityVersion))
                    throw new InvalidDataException("ABC 模式命名权威身份不完整。");
                JObject modeNames = namingAuthority["modeNames"] as JObject
                    ?? throw new InvalidDataException("ABC 模式命名映射缺失。");
                JArray modes = root["modes"] as JArray
                    ?? throw new InvalidDataException("ABC 模式列表缺失。");
                string[] modeIds = modes.OfType<JObject>()
                    .Select(item => item.Value<string>("modeId") ?? string.Empty)
                    .ToArray();
                if (modes.Count != modeIds.Length || modeIds.Length == 0
                    || modeIds.Any(string.IsNullOrWhiteSpace)
                    || modeIds.Distinct(StringComparer.Ordinal).Count() != modeIds.Length)
                    throw new InvalidDataException("ABC 模式 ID 必须非空且唯一。");
                if (modeIds.Length != RequiredAbcModeIds.Length
                    || RequiredAbcModeIds.Any(required => !modeIds.Contains(required, StringComparer.Ordinal)))
                    throw new InvalidDataException("ABC 模式注册表必须完整声明 ABCD.Dynamic、ABCC.Core 和 ABCP.Part。");

                var bindings = new List<ESAIBrainModeBinding>();
                foreach (JObject mode in modes.OfType<JObject>())
                {
                    string modeId = mode.Value<string>("modeId") ?? string.Empty;
                    if (!Regex.IsMatch(modeId, "^[A-Za-z][A-Za-z0-9._-]{1,80}$"))
                        throw new InvalidDataException("ABC 模式 ID 格式无效：" + modeId);
                    JObject names = modeNames[modeId] as JObject
                        ?? throw new InvalidDataException("ABC 模式缺少命名权威：" + modeId);
                    JArray coverage = mode["capabilityCoverage"] as JArray
                        ?? throw new InvalidDataException("ABC 模式缺少能力覆盖声明：" + modeId);
                    List<string> capabilities = coverage.Values<string>()
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                    if (capabilities.Count == 0)
                        throw new InvalidDataException("ABC 模式能力覆盖不能为空：" + modeId);

                    bindings.Add(new ESAIBrainModeBinding
                    {
                        modeId = modeId,
                        authorityId = authorityId,
                        authorityVersion = authorityVersion,
                        displayName = mode.Value<string>("displayName") ?? string.Empty,
                        englishName = names.Value<string>("english") ?? string.Empty,
                        chineseName = names.Value<string>("chinese") ?? string.Empty,
                        shortName = names.Value<string>("shortName") ?? string.Empty,
                        suffix = names.Value<string>("suffix") ?? string.Empty,
                        independent = mode.Value<bool?>("independent") ?? false,
                        orchestration = mode.Value<string>("orchestration") ?? string.Empty,
                        dependsOnCore = mode.Value<bool?>("dependsOnCore") ?? false,
                        fallback = mode.Value<string>("fallback") ?? string.Empty,
                        contractRef = mode.Value<string>("contractRef") ?? string.Empty,
                        registryHash = registryHash,
                        capabilityCoverage = capabilities,
                    });
                }

                ESAIBrainModeBinding dynamicMode = bindings.Single(item =>
                    string.Equals(item.modeId, "ABCD.Dynamic", StringComparison.Ordinal));
                ESAIBrainModeBinding coreMode = bindings.Single(item =>
                    string.Equals(item.modeId, "ABCC.Core", StringComparison.Ordinal));
                JObject parityRule = root["parityRule"] as JObject;
                if (parityRule != null && parityRule.Value<bool?>("coreMustCoverDynamic") == true
                    && dynamicMode.capabilityCoverage.Any(capability =>
                        !coreMode.capabilityCoverage.Contains(capability, StringComparer.Ordinal)))
                    throw new InvalidDataException("ABCC.Core 能力覆盖未闭合 ABCD.Dynamic。");

                surface.modes.AddRange(bindings);
            }
            catch (Exception exception)
            {
                surface.blockers.Add("ABC 模式注册表校验失败：" + exception.Message);
            }
        }

        private static void CollectCommands(ESAIBrainProductionSurface surface)
        {
            if (!ESCommandPalettePathPolicy.TryReadAICommandCatalog(
                    out List<ESAICommandCatalogEntry> entries, out string catalogHash,
                    out string catalogError))
            {
                surface.blockers.Add("AICommand 目录不可用：" + catalogError);
                return;
            }

            foreach (ESAICommandCatalogEntry entry in entries.OrderBy(item => item.id, StringComparer.Ordinal))
            {
                surface.commands.Add(new ESAIBrainCommandBinding
                {
                    id = entry.id,
                    path = entry.path,
                    role = entry.role,
                    riskLevel = entry.riskLevel,
                    writeMode = entry.writeMode,
                    catalogHash = catalogHash,
                    contractHash = string.Empty,
                    reference = "catalog:" + catalogHash,
                });
            }
        }

        private static void CollectSkills(ESAIBrainProductionSurface surface)
        {
            if (!TryResolveProjectPath(ProjectSkillsRoot, out string root, out string rootError))
            {
                surface.blockers.Add("Project Skill 根目录无效：" + rootError);
                return;
            }
            if (!Directory.Exists(root))
            {
                surface.blockers.Add("Project Skill 根目录不存在：" + ProjectSkillsRoot);
                return;
            }

            try
            {
                foreach (string skillRoot in Directory.EnumerateDirectories(root).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    string name = new DirectoryInfo(skillRoot).Name.ToLowerInvariant();
                    if (!Regex.IsMatch(name, "^[a-z0-9][a-z0-9-]*$")) continue;
                    string skillFile = Path.Combine(skillRoot, "SKILL.md");
                    string metadataFile = Path.Combine(skillRoot, "agents", "openai.yaml");
                    if (!File.Exists(skillFile) || !File.Exists(metadataFile))
                    {
                        surface.blockers.Add("项目 Skill 不完整：" + name);
                        continue;
                    }
                    string skillError = string.Empty;
                    string metadataError = string.Empty;
                    if (!TryReadTextAndHash(skillFile, out string skillText, out string skillHash, out skillError)
                        || !TryReadTextAndHash(metadataFile, out string metadataText, out string metadataHash, out metadataError))
                    {
                        surface.blockers.Add("项目 Skill 无法严格读取：" + name + "；"
                            + (string.IsNullOrWhiteSpace(skillError) ? metadataError : skillError));
                        continue;
                    }
                    if (!TryValidateSkillContract(name, skillText, metadataText, out string skillContractError))
                    {
                        surface.blockers.Add("项目 Skill 合同无效：" + name + "；" + skillContractError);
                        continue;
                    }
                    if (!TryReadSkillGovernanceMetadata(skillRoot, name,
                            out ESAIBrainSkillGovernanceMetadata governance,
                            out string governanceHash, out string governanceError))
                    {
                        surface.blockers.Add("项目 Skill 治理元数据无效：" + name + "；" + governanceError);
                        continue;
                    }
                    if (!TryReadSkillCatalogRecord(name, skillHash, governanceHash,
                        out ESAIBrainSkillCatalogRecord catalog, out string catalogError))
                    {
                        surface.blockers.Add("项目 Skill Catalog 注册无效：" + name + "；" + catalogError);
                        continue;
                    }
                    if (!TryResolveSkillEligibility(governance.maturity, governance.delivery,
                            catalog.registrationState, out SkillEligibility eligibility,
                            out string eligibilityError))
                    {
                        surface.blockers.Add("项目 Skill 发现策略无效：" + name + "；" + eligibilityError);
                        continue;
                    }
                    surface.skills.Add(new ESAIBrainSkillBinding
                    {
                        name = name,
                        skillPath = ToProjectRelative(skillRoot),
                        skillHash = skillHash,
                        metadataHash = metadataHash,
                        governanceHash = governanceHash,
                        tier = governance?.tier ?? string.Empty,
                        maturity = governance?.maturity ?? string.Empty,
                        delivery = governance?.delivery ?? string.Empty,
                        evidenceLevel = governance?.evidenceLevel ?? string.Empty,
                        riskClass = governance?.riskClass ?? string.Empty,
                        authorityClass = governance?.authorityClass ?? string.Empty,
                        owner = governance?.owner ?? string.Empty,
                        acceptanceOwner = governance?.acceptanceOwner ?? string.Empty,
                        requiresBrainPlan = governance?.requiresBrainPlan ?? false,
                        allowDirectExecution = governance?.allowDirectExecution ?? false,
                        writePolicy = governance?.writePolicy ?? string.Empty,
                        family = catalog.family,
                        registrationState = catalog.registrationState,
                        discoveryState = eligibility.discoveryState,
                        planEligibility = eligibility.planEligibility,
                        runtimeEligibility = eligibility.runtimeEligibility,
                        reviewRequired = eligibility.reviewRequired,
                    });
                }
            }
            catch (Exception exception)
            {
                surface.blockers.Add("Project Skill 目录读取失败：" + exception.Message);
            }
        }

        private static void CollectAutomationAndCli(ESAIBrainProductionSurface surface)
        {
            List<ESAutomationTaskDescriptor> descriptors;
            try
            {
                descriptors = ESAutomationFacade.CopyDescriptors();
            }
            catch (Exception exception)
            {
                surface.blockers.Add("Automation Facade 目录不可用：" + exception.Message);
                return;
            }

            foreach (ESAutomationTaskDescriptor descriptor in descriptors.OrderBy(item => item.taskId, StringComparer.Ordinal))
            {
                if (!ESAutomationTaskRegistry.TryGet(descriptor.taskId, descriptor.taskVersion,
                        out ESAutomationTaskContract contract))
                {
                    surface.blockers.Add("Automation Task 缺少 TaskContract："
                        + descriptor.taskId + "@" + descriptor.taskVersion);
                    continue;
                }
                try
                {
                    contract.Validate();
                }
                catch (Exception exception)
                {
                    surface.blockers.Add("Automation TaskContract 校验失败："
                        + descriptor.taskId + "；" + exception.Message);
                    continue;
                }

                var task = new ESAIBrainTaskBinding
                {
                    taskId = descriptor.taskId,
                    taskVersion = descriptor.taskVersion,
                    displayName = descriptor.displayName,
                    summary = descriptor.summary,
                    category = descriptor.category,
                    inputSchemaHash = descriptor.inputSchemaHash,
                    allowAiInvoke = descriptor.allowAiInvoke,
                    workerId = contract.worker?.workerId ?? string.Empty,
                    workerType = contract.worker?.type ?? string.Empty,
                };
                task.capabilities.AddRange(contract.capabilities ?? new List<string>());
                surface.tasks.Add(task);

                if (contract.worker == null || contract.worker.type == "DotNet"
                    || descriptor.taskId.StartsWith("es.agent.", StringComparison.Ordinal))
                    continue;
                bool adapterRegistered = ESAutomationProcessRunner.IsAdapterRegistered(
                    contract.worker.type, contract.worker.workerId);
                surface.cli.Add(new ESAIBrainCapabilityBinding
                {
                    id = "cli." + descriptor.taskId,
                    kind = "CLI",
                    status = adapterRegistered ? "Registered" : "Unavailable",
                    displayName = descriptor.displayName,
                    summary = descriptor.summary,
                    authority = "ESAutomationFacade / TaskContract",
                    workerType = contract.worker.type,
                    workerId = contract.worker.workerId,
                    requiresUserAuthorization = descriptor.allowAiInvoke,
                    capabilities = new List<string>(contract.capabilities ?? new List<string>()),
                });
            }
        }

        private static void CollectDiagnostics(ESAIBrainProductionSurface surface)
        {
            string registryPath = Path.Combine(ESCommandPalettePathPolicy.ProjectRoot,
                ESAIBrainRouteProbeRunner.RegistryPath.Replace('/', Path.DirectorySeparatorChar));
            bool registered = File.Exists(registryPath);
            surface.diagnostics.Add(new ESAIBrainCapabilityBinding
            {
                id = "diagnostic.knowledge-route-probes",
                kind = "ReadOnlyDiagnostic",
                status = registered ? "Registered" : "Unavailable",
                displayName = "AIKnowledge route probes",
                summary = "Runs the registered static route-probe dataset through the current AIBrain planner.",
                authority = "AIBrain / AIKnowledge route-probe registry",
                requiresUserAuthorization = true,
                capabilities = new List<string>
                {
                    "runKnowledgeRouteProbes", "route-probe", "knowledge-quality", "static-routing-only"
                },
            });
            if (!registered)
                surface.blockers.Add("AIKnowledge route-probe registry is unavailable: "
                    + ESAIBrainRouteProbeRunner.RegistryPath);
            surface.diagnostics.Add(new ESAIBrainCapabilityBinding
            {
                id = "diagnostic.ai-failure-telemetry",
                kind = "ReadOnlyDiagnostic",
                status = "Registered",
                displayName = "AI failure telemetry",
                summary = "Returns bounded, detail-hashed AIBrain and completion-gate failures for this Editor process.",
                authority = "AIBrain reliability runtime",
                requiresUserAuthorization = true,
                capabilities = new List<string> { "getFailureTelemetry", "failure-classification", "claim-downgrade" },
            });
        }

        private static string ComputeProductionSurfaceHash(ESAIBrainProductionSurface surface)
        {
            string canonical = JsonConvert.SerializeObject(new
            {
                surface.contractVersion,
                surface.routeKeys,
                modes = surface.modes.Select(item => item.modeId + "@" + item.registryHash
                    + "@" + item.authorityId + "@" + item.authorityVersion + "@" + item.displayName
                    + "@" + item.englishName + "@" + item.chineseName
                    + "@" + item.shortName + "@" + item.suffix + "@" + item.independent
                    + "@" + item.orchestration + "@" + item.dependsOnCore + "@" + item.fallback
                    + "@" + item.contractRef + "@" + string.Join(",", item.capabilityCoverage)),
                warnings = surface.warnings.Select(item => item.projectPath + "@" + item.sha256),
                knowledge = surface.knowledge.Select(item => item.knowledgeId + "@" + item.contentHash),
                commands = surface.commands.Select(item => item.id + "@" + item.contractHash),
                skills = surface.skills.Select(item => item.name + "@" + item.skillHash + "@" + item.metadataHash
                    + "@" + item.governanceHash + "@" + item.tier + "@" + item.maturity
                    + "@" + item.delivery + "@" + item.evidenceLevel + "@" + item.riskClass
                    + "@" + item.family + "@" + item.registrationState
                    + "@" + item.discoveryState + "@" + item.planEligibility
                    + "@" + item.runtimeEligibility + "@" + item.reviewRequired),
                tasks = surface.tasks.Select(item => item.taskId + "@" + item.taskVersion + "@" + item.workerId),
                cli = surface.cli.Select(item => item.id + "@" + item.status),
                diagnostics = surface.diagnostics.Select(item => item.id + "@" + item.status),
                mcp = surface.mcp.Select(item => item.id + "@" + item.status),
            }, Formatting.None);
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(StrictUtf8.GetBytes(canonical)))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static ESAIBrainPlan BuildPlan(ESAIBrainRequest request)
        {
            var plan = new ESAIBrainPlan
            {
                contractVersion = ContractVersion,
                planId = Guid.NewGuid().ToString("N"),
                status = "Blocked",
                objective = request?.objective ?? string.Empty,
                invocationId = request?.invocationId ?? string.Empty,
                authority = new ESAIBrainAuthoritySnapshot(),
            };

            if (request == null)
            {
                plan.blockers.Add("缺少 AIBrain 请求。");
                plan.status = "InvalidRequest";
                return plan;
            }

            string objective = request.objective?.Trim() ?? string.Empty;
            if (objective.Length == 0)
                plan.blockers.Add("目标不能为空。");

            List<string> routeKeys = NormalizeDiscoveryRouteKeys(request.routeKeys);
            foreach (string inferredRouteKey in InferObjectiveRouteKeys(objective))
            {
                if (!routeKeys.Contains(inferredRouteKey, StringComparer.Ordinal))
                    routeKeys.Add(inferredRouteKey);
            }
            if (routeKeys.Count == 0)
                plan.blockers.Add("无法从目标推导 routeKeys；请补充任务领域或明确 routeKeys。");
            plan.routeKeys.AddRange(routeKeys);

            if (!TryReadAiwarnings(plan, objective, routeKeys, out string warningError)
                && !string.IsNullOrWhiteSpace(warningError))
                plan.blockers.Add(warningError);

            if (!TryReadKnowledge(plan, routeKeys, out string knowledgeError)
                && !string.IsNullOrWhiteSpace(knowledgeError))
                plan.blockers.Add(knowledgeError);

            ValidateCommand(plan, request.commandId);
            List<string> selectedSkills = NormalizeValues(request.skillNames);
            if (selectedSkills.Count == 0)
            {
                selectedSkills = plan.knowledge.SelectMany(item => item.relatedSkills ?? new List<string>())
                    .Select(item => item?.Trim().ToLowerInvariant() ?? string.Empty)
                    .Where(IsCompleteProjectSkill)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }
            ValidateSkills(plan, selectedSkills, request.workflow);
            ValidateTask(plan, request);

            if (string.IsNullOrWhiteSpace(request.invocationId)
                || !Guid.TryParseExact(request.invocationId, "N", out _))
                plan.blockers.Add("执行必须携带稳定的 N 格式 InvocationId。");

            plan.status = plan.blockers.Count == 0 ? "Ready" : ResolveBlockedStatus(plan.blockers);
            plan.routePlan = BuildReadOnlyRoutePlan(plan, request);
            plan.planHash = ComputePlanHash(plan, request);
            return plan;
        }

        private static ESAIBrainRoutePlan BuildReadOnlyRoutePlan(
            ESAIBrainPlan legacyPlan, ESAIBrainRequest request)
        {
            var routePlan = new ESAIBrainRoutePlan
            {
                schemaVersion = 1,
                contractId = RoutePlanContractId,
                profile = string.IsNullOrWhiteSpace(request.routeProfileId)
                    ? "governance" : request.routeProfileId.Trim().ToLowerInvariant(),
                scope = "task-object",
                status = "EvidencePending",
                routeState = "core",
                evidenceState = "pending",
                effect = "claim-cap",
                executionEnabled = false,
                compatibility = new ESAIBrainRouteCompatibility
                {
                    legacyPlanStatus = legacyPlan.status,
                    projectionOnly = true,
                    productionRouteIntegrated = false,
                    globalP0Integrated = false,
                    executionAuthority = "none",
                },
            };
            routePlan.routeKeys.AddRange((legacyPlan.routeKeys ?? new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal));
            AddRouteStopConditions(routePlan);

            bool hardBlocked = false;
            bool needsRegistration = false;
            bool evidencePending = false;
            if (!Regex.IsMatch(routePlan.profile, "^[a-z0-9][a-z0-9._:-]{0,127}$"))
            {
                AddRouteIssue(routePlan, "route-plan", "profile", "ROUTE.PROFILE_INVALID",
                    "profile must be one registered exact token", "hard-block",
                    "Use an exact registered RoutePlan profile.");
                hardBlocked = true;
            }

            var sourceRefs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (TryBindGoalRevision(request.goalRevisionPath, routePlan,
                    out ESAIBrainGoalRevisionBinding goalRevision, out string goalError))
            {
                routePlan.goalRevision = goalRevision;
                AddRouteSourceRef(sourceRefs, goalRevision.projectPath, goalRevision.artifactHash,
                    routePlan, ref hardBlocked);
            }
            else if (string.IsNullOrWhiteSpace(request.goalRevisionPath))
            {
                AddRouteIssue(routePlan, "goal-revision", "goalRevisionPath",
                    "ROUTE.GOAL_REVISION_REQUIRED",
                    "a composable RoutePlan must bind one frozen GoalRevision",
                    "claim-cap", "Create or select a frozen GoalRevision and re-plan.");
                evidencePending = true;
            }
            else
            {
                AddRouteIssue(routePlan, "goal-revision", "goalRevisionPath",
                    "ROUTE.GOAL_REVISION_INVALID", goalError, "hard-block",
                    "Repair the referenced GoalRevision or provide its current immutable path.");
                hardBlocked = true;
            }

            RouteStageRegistry registry = null;
            string registryHash = string.Empty;
            if (!TryReadRouteStageRegistry(out registry, out registryHash, out string registryError))
            {
                AddRouteIssue(routePlan, "route-stage-registry", "registry",
                    "ROUTE.REGISTRY_INVALID", registryError, "hard-block",
                    "Repair the central Route Stage Registry and re-plan.");
                hardBlocked = true;
            }
            else
            {
                AddRouteSourceRef(sourceRefs, RouteStageRegistryPath, registryHash,
                    routePlan, ref hardBlocked);
                BuildRegisteredRouteStages(legacyPlan, routePlan, registry,
                    ref hardBlocked, ref needsRegistration, ref evidencePending);
            }

            AddCurrentRouteSourceRefs(legacyPlan, routePlan, sourceRefs, ref hardBlocked);
            BuildRouteSnapshot(routePlan, sourceRefs, registryHash, ref hardBlocked);

            routePlan.maxDepth = routePlan.stages.Count == 0
                ? 0 : routePlan.stages.Max(stage => stage.depth);
            if (hardBlocked)
            {
                routePlan.status = "Blocked";
                routePlan.routeState = "blocked";
                routePlan.evidenceState = "partial";
                routePlan.effect = "hard-block";
            }
            else if (needsRegistration)
            {
                routePlan.status = "NeedsRegistration";
                routePlan.routeState = "blocked";
                routePlan.evidenceState = "pending";
                routePlan.effect = "hard-block";
            }
            else if (routePlan.stages.Count == 0)
            {
                routePlan.status = legacyPlan.workflow == null ? "EvidencePending" : "NotApplicable";
                routePlan.routeState = "core";
                routePlan.evidenceState = "pending";
                routePlan.effect = legacyPlan.workflow == null ? "claim-cap" : "review";
            }
            else if (evidencePending)
            {
                routePlan.status = "EvidencePending";
                routePlan.routeState = routePlan.maxDepth == 0 ? "core" : "extension";
                routePlan.evidenceState = "pending";
                routePlan.effect = "claim-cap";
            }
            else
            {
                routePlan.status = "Ready";
                routePlan.routeState = routePlan.maxDepth == 0 ? "core" : "extension";
                routePlan.evidenceState = "closed";
                routePlan.effect = "review";
            }

            BuildRouteShadowIntegration(routePlan, legacyPlan.status);
            routePlan.routePlanHash = ComputeRoutePlanHash(routePlan);
            routePlan.routePlanId = "route-" + routePlan.routePlanHash.Substring(0, 32);
            return routePlan;
        }

        private static void BuildRouteShadowIntegration(
            ESAIBrainRoutePlan routePlan, string legacyPlanStatus)
        {
            const string selectedProfile = "governance";
            const string selectedScope = "task-object";
            bool eligible = string.Equals(routePlan.profile, selectedProfile,
                                StringComparison.Ordinal)
                            && string.Equals(routePlan.scope, selectedScope,
                                StringComparison.Ordinal);
            string decisionHash = eligible
                ? ComputeRouteShadowDecisionHash(routePlan, legacyPlanStatus)
                : null;
            string decisionId = decisionHash == null
                ? null : "route-decision-" + decisionHash.Substring(0, 32);
            routePlan.shadowIntegration = new ESAIBrainRouteShadowIntegration
            {
                contractId = "es://automation/contracts/route-plan-shadow-candidate/v1",
                mode = "read-only-shadow",
                algorithmId = "route-shadow-canonical-v1",
                selectedProfile = selectedProfile,
                selectedScope = selectedScope,
                candidateStatus = eligible ? "candidate-emitted" : "not-selected",
                decisionHash = decisionHash,
                decisionId = decisionId,
                legacyPlanStatusBefore = legacyPlanStatus,
                legacyPlanStatusAfter = legacyPlanStatus,
                stateChanged = false,
                verificationRequired = true,
                rollbackState = "available",
                rollbackAction = "discard-shadow-candidate",
                productionRouteIntegrated = false,
                globalP0Integrated = false,
            };
            routePlan.shadowIntegration.observationCodes.AddRange(eligible
                ? new[]
                {
                    "SHADOW.SCOPED_MATCH",
                    "SHADOW.ROLLBACK_AVAILABLE",
                    "SHADOW.NO_PRODUCTION_TAKEOVER",
                }
                : new[]
                {
                    "SHADOW.PROFILE_SCOPE_NOT_SELECTED",
                    "SHADOW.ROLLBACK_AVAILABLE",
                    "SHADOW.NO_PRODUCTION_TAKEOVER",
                });
        }

        private static string ComputeRouteShadowDecisionHash(
            ESAIBrainRoutePlan routePlan, string legacyPlanStatus)
        {
            return ComputeCanonicalSha256(JToken.FromObject(new
            {
                contractId = "es://automation/contracts/route-plan-shadow-candidate/v1",
                mode = "read-only-shadow",
                algorithmId = "route-shadow-canonical-v1",
                profile = routePlan.profile,
                scope = routePlan.scope,
                legacyPlanStatus,
                routePlan.status,
                routePlan.routeState,
                routePlan.evidenceState,
                routePlan.effect,
                routePlan.routeKeys,
                routePlan.goalRevision,
                routePlan.stages,
                routePlan.maxDepth,
                routePlan.issues,
                routePlan.snapshot,
            }));
        }

        private static void AddRouteStopConditions(ESAIBrainRoutePlan routePlan)
        {
            routePlan.stopConditions.Add(new ESAIBrainRouteStopCondition
            {
                code = "ROUTE.UNREGISTERED_STAGE",
                predicate = "a selected Skill has no unique stage contract for the exact profile and route",
                trigger = "before accepting the composed RoutePlan",
                outcome = "hard-block",
                recovery = "register one exact stage contract; do not infer an order",
            });
            routePlan.stopConditions.Add(new ESAIBrainRouteStopCondition
            {
                code = "ROUTE.DEPTH_LIMIT",
                predicate = "the next dependency level exceeds the registered depth budget",
                trigger = "before adding the next stage",
                outcome = "stop-next-read",
                recovery = "reduce the plan or register a depth-2 reason for the exact profile and route",
            });
            routePlan.stopConditions.Add(new ESAIBrainRouteStopCondition
            {
                code = "ROUTE.DEPENDENCY_INVALID",
                predicate = "a required token is missing, duplicated, or cyclic",
                trigger = "while topologically ordering registered stages",
                outcome = "hard-block",
                recovery = "repair stage requires/produces and re-plan from the same GoalRevision",
            });
        }

        private static void AddRouteIssue(ESAIBrainRoutePlan routePlan, string targetObject,
            string field, string reasonCode, string predicate, string effect, string recovery)
        {
            routePlan.issues.Add(new ESAIBrainRouteIssue
            {
                targetObject = targetObject,
                field = field,
                profile = routePlan.profile,
                scope = routePlan.scope,
                reasonCode = reasonCode,
                predicate = string.IsNullOrWhiteSpace(predicate) ? reasonCode : predicate,
                effect = effect,
                recovery = recovery,
            });
        }

        private static bool TryBindGoalRevision(string projectPath, ESAIBrainRoutePlan routePlan,
            out ESAIBrainGoalRevisionBinding binding, out string error)
        {
            binding = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                error = "GoalRevision path is missing.";
                return false;
            }
            if (!TryResolveProjectPath(projectPath, out string fullPath, out error)) return false;
            if (!File.Exists(fullPath))
            {
                error = "GoalRevision file does not exist.";
                return false;
            }
            if (!TryReadTextAndHash(fullPath, out string text, out string artifactHash, out error))
                return false;
            try
            {
                JObject goal = JObject.Parse(text);
                string[] required =
                {
                    "schemaVersion", "goalId", "goalRevision", "scope", "acceptanceIntent",
                    "status", "budget", "parentGoalRef", "revisionHash",
                };
                if (!HasExactProperties(goal, required))
                    throw new InvalidDataException("GoalRevision fields are not the exact V1 contract.");
                if (goal.Value<int?>("schemaVersion") != 1
                    || !string.Equals(goal.Value<string>("status"), "frozen", StringComparison.Ordinal))
                    throw new InvalidDataException("GoalRevision must be schemaVersion 1 and frozen.");
                string goalId = goal.Value<string>("goalId") ?? string.Empty;
                string revision = goal.Value<string>("goalRevision") ?? string.Empty;
                string revisionHash = (goal.Value<string>("revisionHash") ?? string.Empty).ToLowerInvariant();
                if (!Regex.IsMatch(goalId, "^[A-Za-z0-9][A-Za-z0-9._-]{0,80}$")
                    || !Regex.IsMatch(revision, "^r[1-9][0-9]{0,8}$")
                    || !ESAutomationWorkerRegistration.IsSha256(revisionHash)
                    || !(goal["scope"] is JArray scope) || scope.Count == 0
                    || !(goal["budget"] is JObject))
                    throw new InvalidDataException("GoalRevision identity, scope, budget, or hash is invalid.");
                if (scope.Any(item => item.Type != JTokenType.String
                        || string.IsNullOrWhiteSpace(item.Value<string>()))
                    || scope.Values<string>().Distinct(StringComparer.OrdinalIgnoreCase).Count() != scope.Count)
                    throw new InvalidDataException("GoalRevision scope must be a non-empty unique string set.");
                var core = new JObject
                {
                    ["schemaVersion"] = 1,
                    ["goalId"] = goalId,
                    ["goalRevision"] = revision,
                    ["scope"] = scope.DeepClone(),
                    ["acceptanceIntent"] = goal["acceptanceIntent"]?.DeepClone() ?? JValue.CreateNull(),
                    ["status"] = "frozen",
                    ["budget"] = goal["budget"].DeepClone(),
                    ["parentGoalRef"] = goal["parentGoalRef"]?.DeepClone() ?? JValue.CreateNull(),
                };
                if (!string.Equals(ComputeCanonicalSha256(core), revisionHash,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("GoalRevision hash mismatch.");
                binding = new ESAIBrainGoalRevisionBinding
                {
                    goalId = goalId,
                    goalRevision = revision,
                    revisionHash = revisionHash,
                    projectPath = ToProjectRelative(fullPath),
                    artifactHash = artifactHash,
                };
                routePlan.budget = (JObject)goal["budget"].DeepClone();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool TryReadRouteStageRegistry(out RouteStageRegistry registry,
            out string registryHash, out string error)
        {
            registry = null;
            registryHash = string.Empty;
            error = string.Empty;
            if (!TryResolveProjectPath(RouteStageRegistryPath, out string fullPath, out error)
                || !TryReadTextAndHash(fullPath, out string text, out registryHash, out error))
                return false;
            try
            {
                JObject root = JObject.Parse(text);
                if (!HasExactProperties(root, "schemaVersion", "registryId", "normalizationVersion",
                        "defaultMaxDepth", "maxDepth", "externalInputs", "depthAuthorizations", "stages")
                    || root.Value<int?>("schemaVersion") != 1
                    || !string.Equals(root.Value<string>("registryId"),
                        "esframework-route-stage-registry", StringComparison.Ordinal)
                    || !string.Equals(root.Value<string>("normalizationVersion"),
                        "route-stage-canonical-v1", StringComparison.Ordinal)
                    || root.Value<int?>("defaultMaxDepth") != 1
                    || root.Value<int?>("maxDepth") != 2)
                    throw new InvalidDataException("Route Stage Registry header is invalid.");

                registry = new RouteStageRegistry
                {
                    defaultMaxDepth = 1,
                    maxDepth = 2,
                    externalInputs = ReadRouteStringSet(root["externalInputs"], "externalInputs"),
                };
                if (!registry.externalInputs.Contains("goal-revision", StringComparer.Ordinal))
                    throw new InvalidDataException("Route Stage Registry must declare goal-revision as an external input.");

                if (!(root["depthAuthorizations"] is JArray authorizations))
                    throw new InvalidDataException("depthAuthorizations must be an array.");
                foreach (JObject item in authorizations.OfType<JObject>())
                {
                    if (!HasExactProperties(item, "reasonCode", "authorizesDepth", "profiles", "routeKeys"))
                        throw new InvalidDataException("A depth authorization has unsupported fields.");
                    var authorization = new RouteDepthAuthorization
                    {
                        reasonCode = item.Value<string>("reasonCode") ?? string.Empty,
                        authorizesDepth = item.Value<int?>("authorizesDepth") ?? 0,
                        profiles = ReadRouteStringSet(item["profiles"], "depthAuthorization.profiles"),
                        routeKeys = ReadRouteStringSet(item["routeKeys"], "depthAuthorization.routeKeys"),
                    };
                    if (!Regex.IsMatch(authorization.reasonCode,
                            "^ROUTE\\.DEPTH_2\\.[A-Z0-9_]{1,80}$")
                        || authorization.authorizesDepth != 2
                        || authorization.profiles.Count == 0 || authorization.routeKeys.Count == 0)
                        throw new InvalidDataException("A depth authorization is invalid.");
                    registry.depthAuthorizations.Add(authorization);
                }
                if (registry.depthAuthorizations.Select(item => item.reasonCode)
                    .Distinct(StringComparer.Ordinal).Count() != registry.depthAuthorizations.Count)
                    throw new InvalidDataException("Depth reason codes must be unique.");

                if (!(root["stages"] is JArray stages))
                    throw new InvalidDataException("stages must be an array.");
                foreach (JObject item in stages.OfType<JObject>())
                {
                    if (!HasExactProperties(item, "stageContractId", "skillName", "profiles",
                            "routeKeys", "requires", "produces", "failureConditions", "depthReasonCode"))
                        throw new InvalidDataException("A route stage has unsupported fields.");
                    var stage = new RouteStageDefinition
                    {
                        stageContractId = item.Value<string>("stageContractId") ?? string.Empty,
                        skillName = item.Value<string>("skillName") ?? string.Empty,
                        profiles = ReadRouteStringSet(item["profiles"], "stage.profiles"),
                        routeKeys = ReadRouteStringSet(item["routeKeys"], "stage.routeKeys"),
                        requires = ReadRouteStringSet(item["requires"], "stage.requires"),
                        produces = ReadRouteStringSet(item["produces"], "stage.produces"),
                        failureConditions = ReadRouteStringSet(item["failureConditions"],
                            "stage.failureConditions"),
                        depthReasonCode = item.Value<string>("depthReasonCode") ?? string.Empty,
                    };
                    if (!Regex.IsMatch(stage.stageContractId, "^[A-Za-z0-9][A-Za-z0-9._:-]{0,159}$")
                        || !Regex.IsMatch(stage.skillName, "^[a-z0-9][a-z0-9-]{0,80}$")
                        || stage.profiles.Count == 0 || stage.routeKeys.Count == 0
                        || stage.produces.Count == 0 || stage.failureConditions.Count == 0
                        || !string.IsNullOrEmpty(stage.depthReasonCode)
                        && !Regex.IsMatch(stage.depthReasonCode,
                            "^ROUTE\\.DEPTH_2\\.[A-Z0-9_]{1,80}$"))
                        throw new InvalidDataException("A route stage contract is invalid.");
                    registry.stages.Add(stage);
                }
                if (registry.stages.Count == 0
                    || registry.stages.Select(item => item.stageContractId)
                        .Distinct(StringComparer.Ordinal).Count() != registry.stages.Count)
                    throw new InvalidDataException("Route stage contracts must be non-empty and uniquely identified.");
                return true;
            }
            catch (Exception exception)
            {
                registry = null;
                error = exception.Message;
                return false;
            }
        }

        private static List<string> ReadRouteStringSet(JToken value, string field)
        {
            if (!(value is JArray array)) throw new InvalidDataException(field + " must be an array.");
            var result = new List<string>();
            foreach (JToken token in array)
            {
                string item = token.Type == JTokenType.String ? token.Value<string>() : string.Empty;
                if (!Regex.IsMatch(item ?? string.Empty, "^[a-z0-9][a-z0-9._:-]{0,127}$"))
                    throw new InvalidDataException(field + " contains an invalid token.");
                result.Add(item);
            }
            if (result.Distinct(StringComparer.Ordinal).Count() != result.Count)
                throw new InvalidDataException(field + " must be unique.");
            return result;
        }

        private static bool HasExactProperties(JObject value, params string[] expected)
        {
            if (value == null) return false;
            var actual = new HashSet<string>(value.Properties().Select(item => item.Name),
                StringComparer.Ordinal);
            return actual.SetEquals(expected ?? Array.Empty<string>());
        }

        private static void BuildRegisteredRouteStages(ESAIBrainPlan legacyPlan,
            ESAIBrainRoutePlan routePlan, RouteStageRegistry registry,
            ref bool hardBlocked, ref bool needsRegistration, ref bool evidencePending)
        {
            var routeKeys = new HashSet<string>(routePlan.routeKeys, StringComparer.Ordinal);
            var selected = new List<RouteStageDefinition>();
            foreach (ESAIBrainSkillBinding skill in legacyPlan.skills.OrderBy(item => item.name,
                         StringComparer.Ordinal))
            {
                List<RouteStageDefinition> matches = registry.stages.Where(item =>
                        string.Equals(item.skillName, skill.name, StringComparison.Ordinal)
                        && item.profiles.Contains(routePlan.profile, StringComparer.Ordinal)
                        && item.routeKeys.Any(routeKeys.Contains))
                    .ToList();
                if (matches.Count != 1)
                {
                    AddRouteIssue(routePlan, skill.name, "stageContractId",
                        matches.Count == 0 ? "ROUTE.UNREGISTERED_STAGE" : "ROUTE.AMBIGUOUS_STAGE",
                        matches.Count == 0
                            ? "no exact stage contract matches the selected Skill, profile, and route"
                            : "multiple stage contracts match the selected Skill, profile, and route",
                        "hard-block", "Register exactly one stage contract for this Skill/profile/route.");
                    needsRegistration = true;
                    continue;
                }
                selected.Add(matches[0]);
            }
            if (selected.Count == 0) return;

            var producers = new Dictionary<string, RouteStageDefinition>(StringComparer.Ordinal);
            foreach (RouteStageDefinition stage in selected)
            {
                foreach (string output in stage.produces)
                {
                    if (producers.TryGetValue(output, out RouteStageDefinition prior))
                    {
                        AddRouteIssue(routePlan, stage.skillName, "produces",
                            "ROUTE.DUPLICATE_PRODUCT",
                            output + " is produced by both " + prior.skillName + " and " + stage.skillName,
                            "hard-block", "Assign one canonical producer for the route token.");
                        hardBlocked = true;
                    }
                    else producers.Add(output, stage);
                }
            }

            var dependencies = selected.ToDictionary(item => item,
                _ => new HashSet<RouteStageDefinition>());
            foreach (RouteStageDefinition stage in selected)
            {
                foreach (string input in stage.requires)
                {
                    if (registry.externalInputs.Contains(input, StringComparer.Ordinal))
                    {
                        if (string.Equals(input, "goal-revision", StringComparison.Ordinal)
                            && routePlan.goalRevision == null)
                            evidencePending = true;
                        continue;
                    }
                    if (!producers.TryGetValue(input, out RouteStageDefinition producer))
                    {
                        AddRouteIssue(routePlan, stage.skillName, "requires",
                            "ROUTE.MISSING_INPUT", "no selected stage produces " + input,
                            "hard-block", "Select or register the stage that produces the required token.");
                        hardBlocked = true;
                        continue;
                    }
                    dependencies[stage].Add(producer);
                }
            }

            var ordered = new List<RouteStageDefinition>();
            var depths = new Dictionary<RouteStageDefinition, int>();
            var remaining = new HashSet<RouteStageDefinition>(selected);
            while (remaining.Count > 0)
            {
                List<RouteStageDefinition> ready = remaining
                    .Where(item => dependencies[item].All(ordered.Contains))
                    .OrderBy(item => item.stageContractId, StringComparer.Ordinal).ToList();
                if (ready.Count == 0)
                {
                    AddRouteIssue(routePlan, "route-plan", "stages",
                        "ROUTE.DEPENDENCY_CYCLE", "stage dependencies contain a cycle",
                        "hard-block", "Break the cycle in the central stage contracts.");
                    hardBlocked = true;
                    break;
                }
                foreach (RouteStageDefinition stage in ready)
                {
                    int depth = dependencies[stage].Count == 0
                        ? 0 : dependencies[stage].Max(item => depths[item]) + 1;
                    depths[stage] = depth;
                    ordered.Add(stage);
                    remaining.Remove(stage);
                }
            }

            int sequence = 0;
            foreach (RouteStageDefinition stage in ordered)
            {
                int depth = depths[stage];
                if (depth > registry.maxDepth)
                {
                    AddRouteIssue(routePlan, stage.skillName, "depth",
                        "ROUTE.DEPTH_LIMIT", "stage depth " + depth + " exceeds registry maxDepth",
                        "stop-next-read", "Reduce dependencies before adding this stage.");
                    hardBlocked = true;
                    continue;
                }
                if (depth > registry.defaultMaxDepth)
                {
                    RouteDepthAuthorization authorization = registry.depthAuthorizations.FirstOrDefault(item =>
                        string.Equals(item.reasonCode, stage.depthReasonCode, StringComparison.Ordinal)
                        && item.authorizesDepth == depth
                        && item.profiles.Contains(routePlan.profile, StringComparer.Ordinal)
                        && item.routeKeys.Any(routeKeys.Contains));
                    if (authorization == null)
                    {
                        AddRouteIssue(routePlan, stage.skillName, "depthReasonCode",
                            "ROUTE.DEPTH_REASON_UNAUTHORIZED",
                            "depth 2 requires a registered reason for the exact profile and route",
                            "hard-block", "Register or select the allowed depth-2 reason.");
                        hardBlocked = true;
                        continue;
                    }
                }
                else if (!string.IsNullOrEmpty(stage.depthReasonCode))
                {
                    AddRouteIssue(routePlan, stage.skillName, "depthReasonCode",
                        "ROUTE.DEPTH_REASON_MISAPPLIED",
                        "a depth-2 reason cannot authorize a shallower stage",
                        "hard-block", "Remove the reason or repair the dependency graph.");
                    hardBlocked = true;
                    continue;
                }

                sequence++;
                var output = new ESAIBrainRouteStage
                {
                    stageId = "stage-" + sequence.ToString("00") + "-" + stage.skillName,
                    stageContractId = stage.stageContractId,
                    skillName = stage.skillName,
                    depth = depth,
                    depthReasonCode = stage.depthReasonCode,
                    executionStatus = "not-executed",
                };
                output.requires.AddRange(stage.requires.OrderBy(item => item, StringComparer.Ordinal));
                output.produces.AddRange(stage.produces.OrderBy(item => item, StringComparer.Ordinal));
                output.failureConditions.AddRange(stage.failureConditions
                    .OrderBy(item => item, StringComparer.Ordinal));
                routePlan.stages.Add(output);
            }
        }

        private static void AddCurrentRouteSourceRefs(ESAIBrainPlan legacyPlan,
            ESAIBrainRoutePlan routePlan, Dictionary<string, string> sourceRefs, ref bool hardBlocked)
        {
            AddRouteSourceRef(sourceRefs, KnowledgeIndexPath, legacyPlan.knowledgeIndexHash,
                routePlan, ref hardBlocked);
            AddCurrentRouteSourceRef(ProjectSkillCatalogPath, sourceRefs, routePlan, ref hardBlocked);
            AddCurrentRouteSourceRef(ProjectSkillDiscoveryPolicyPath, sourceRefs, routePlan,
                ref hardBlocked);
            AddCurrentRouteSourceRef(".agents/SKILL_RESOURCE_INDEX.yaml", sourceRefs, routePlan,
                ref hardBlocked);
            foreach (ESAIBrainEvidenceBinding item in legacyPlan.warnings.Concat(legacyPlan.evidence))
                AddRouteSourceRef(sourceRefs, item.projectPath, item.sha256, routePlan, ref hardBlocked);
            foreach (ESAIBrainSkillBinding skill in legacyPlan.skills)
            {
                AddRouteSourceRef(sourceRefs, skill.skillPath.TrimEnd('/') + "/SKILL.md",
                    skill.skillHash, routePlan, ref hardBlocked);
                AddRouteSourceRef(sourceRefs, skill.skillPath.TrimEnd('/') + "/agents/openai.yaml",
                    skill.metadataHash, routePlan, ref hardBlocked);
                AddRouteSourceRef(sourceRefs, skill.skillPath.TrimEnd('/') + "/governance.json",
                    skill.governanceHash, routePlan, ref hardBlocked);
            }
            if (legacyPlan.command != null)
                AddRouteSourceRef(sourceRefs, legacyPlan.command.path,
                    legacyPlan.command.contractHash, routePlan, ref hardBlocked);
        }

        private static void AddCurrentRouteSourceRef(string projectPath,
            Dictionary<string, string> sourceRefs, ESAIBrainRoutePlan routePlan, ref bool hardBlocked)
        {
            string readError = string.Empty;
            string hash = string.Empty;
            if (!TryResolveProjectPath(projectPath, out string fullPath, out string pathError)
                || !TryReadTextAndHash(fullPath, out _, out hash, out readError))
            {
                AddRouteIssue(routePlan, "route-snapshot", "sourceRefs",
                    "ROUTE.SOURCE_REF_UNAVAILABLE",
                    string.IsNullOrWhiteSpace(pathError) ? readError : pathError,
                    "hard-block", "Restore the current source and re-plan.");
                hardBlocked = true;
                return;
            }
            AddRouteSourceRef(sourceRefs, projectPath, hash, routePlan, ref hardBlocked);
        }

        private static void AddRouteSourceRef(Dictionary<string, string> sourceRefs,
            string projectPath, string hash, ESAIBrainRoutePlan routePlan, ref bool hardBlocked)
        {
            string normalizedPath = (projectPath ?? string.Empty).Replace('\\', '/').Trim();
            string normalizedHash = (hash ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedPath) || Path.IsPathRooted(normalizedPath)
                || normalizedPath.Split('/').Contains("..")
                || !ESAutomationWorkerRegistration.IsSha256(normalizedHash))
            {
                AddRouteIssue(routePlan, "route-snapshot", "sourceRefs",
                    "ROUTE.SOURCE_REF_INVALID", "SourceRef path or SHA-256 is invalid",
                    "hard-block", "Use a current project-relative SourceRef and SHA-256.");
                hardBlocked = true;
                return;
            }
            if (sourceRefs.TryGetValue(normalizedPath, out string existing)
                && !string.Equals(existing, normalizedHash, StringComparison.OrdinalIgnoreCase))
            {
                AddRouteIssue(routePlan, "route-snapshot", "sourceRefs",
                    "ROUTE.SOURCE_REF_CONFLICT", normalizedPath + " has conflicting hashes",
                    "hard-block", "Re-read the source once and rebuild the plan snapshot.");
                hardBlocked = true;
                return;
            }
            sourceRefs[normalizedPath] = normalizedHash;
        }

        private static void BuildRouteSnapshot(ESAIBrainRoutePlan routePlan,
            Dictionary<string, string> sourceRefs, string registryHash, ref bool hardBlocked)
        {
            string head = ESAutomationSourceState.GetCurrentGitCommit();
            if (!Regex.IsMatch(head ?? string.Empty, "^[a-f0-9]{40}$"))
            {
                AddRouteIssue(routePlan, "route-snapshot", "head", "ROUTE.HEAD_UNAVAILABLE",
                    "Git HEAD is not a current 40-character commit SHA",
                    "hard-block", "Restore a readable Git HEAD and re-plan.");
                hardBlocked = true;
            }
            routePlan.snapshot = new ESAIBrainRouteSnapshot
            {
                head = Regex.IsMatch(head ?? string.Empty, "^[a-f0-9]{40}$") ? head : null,
                registryHash = ESAutomationWorkerRegistration.IsSha256(registryHash ?? string.Empty)
                    ? registryHash.ToLowerInvariant() : null,
                coverage = new ESAIBrainRouteSnapshotCoverage
                {
                    normalizationVersion = "route-plan-canonical-v1",
                },
            };
            routePlan.snapshot.coverage.includes.AddRange(new[]
            {
                "goal-revision-artifact",
                "knowledge-index",
                "route-stage-registry",
                "selected-skill-contracts",
                "selected-warning-and-command-bindings",
            });
            foreach (KeyValuePair<string, string> item in sourceRefs
                         .OrderBy(item => item.Key, StringComparer.Ordinal))
                routePlan.snapshot.sourceRefs.Add(new ESAIBrainRouteSourceRef
                    { projectPath = item.Key, sha256 = item.Value });
            routePlan.snapshot.sourceRefsHash = ComputeCanonicalSha256(JToken.FromObject(
                routePlan.snapshot.sourceRefs.Select(item => new { item.projectPath, item.sha256 })));
        }

        private static string ComputeRoutePlanHash(ESAIBrainRoutePlan routePlan)
        {
            return ComputeCanonicalSha256(JToken.FromObject(new
            {
                routePlan.schemaVersion,
                routePlan.contractId,
                routePlan.status,
                routePlan.routeState,
                routePlan.evidenceState,
                routePlan.effect,
                routePlan.profile,
                routePlan.scope,
                routePlan.routeKeys,
                routePlan.goalRevision,
                routePlan.stages,
                routePlan.maxDepth,
                routePlan.budget,
                routePlan.stopConditions,
                routePlan.issues,
                routePlan.snapshot,
                routePlan.shadowIntegration,
                routePlan.compatibility,
                routePlan.executionEnabled,
            }));
        }

        private static string ResolveBlockedStatus(List<string> blockers)
        {
            string text = string.Join("\n", blockers);
            if (text.IndexOf("Automation Facade", StringComparison.Ordinal) >= 0
                || text.IndexOf("TaskContract", StringComparison.Ordinal) >= 0
                || text.IndexOf("已注册的 Automation", StringComparison.Ordinal) >= 0)
                return "PlanTaskUnavailable";
            if (text.IndexOf("SourceRef", StringComparison.Ordinal) >= 0
                && text.IndexOf("漂移", StringComparison.Ordinal) >= 0)
                return "SourceHashDrift";
            if (text.IndexOf("AICommand", StringComparison.Ordinal) >= 0) return "NoMatchingCommand";
            if (text.IndexOf("Skill", StringComparison.Ordinal) >= 0) return "NoMatchingSkill";
            if (text.IndexOf("Knowledge", StringComparison.Ordinal) >= 0) return "NoKnowledgeRoute";
            return "Blocked";
        }

        private static void ValidateCommand(ESAIBrainPlan plan, string commandId)
        {
            if (string.IsNullOrWhiteSpace(commandId))
            {
                plan.blockers.Add("未选择 AICommand；AIBrain 不得借用无关合同扩大权限。");
                return;
            }

            if (!ESCommandPalettePathPolicy.TryReadAICommandCatalog(
                    out List<ESAICommandCatalogEntry> entries, out string catalogHash,
                    out string catalogError))
            {
                plan.blockers.Add("AICommand 目录不可用：" + catalogError);
                return;
            }

            ESAICommandCatalogEntry selected = entries.FirstOrDefault(
                item => string.Equals(item.id, commandId, StringComparison.Ordinal));
            if (selected == null)
            {
                plan.blockers.Add("未找到 AICommand：" + commandId);
                return;
            }

            if (!ESCommandPalettePathPolicy.TryReadAICommandContract(
                    selected.path, out _, out string commandHash, out string commandError))
            {
                plan.blockers.Add("AICommand 正文不可用：" + commandError);
                return;
            }

            if (!ESCommandPalettePathPolicy.TryCreateAICommandReference(
                    selected.id, selected.path, catalogHash, commandHash,
                    out ESAICommandCatalogEntry verified, out string reference,
                    out string referenceError))
            {
                plan.blockers.Add("AICommand 验签失败：" + referenceError);
                return;
            }

            plan.command = new ESAIBrainCommandBinding
            {
                id = verified.id,
                path = verified.path,
                role = verified.role,
                riskLevel = verified.riskLevel,
                writeMode = verified.writeMode,
                catalogHash = catalogHash,
                contractHash = commandHash,
                reference = reference,
            };
            plan.authority.command = "AICommand:" + verified.id;
        }

        private static void ValidateSkills(ESAIBrainPlan plan, IEnumerable<string> requestedSkills,
            ESAIBrainWorkflowAuthority workflow)
        {
            List<string> skills = NormalizeValues(requestedSkills);
            bool hasWorkflow = ValidateWorkflow(plan, workflow);
            if (skills.Count == 0 && !hasWorkflow)
                plan.blockers.Add("未选择项目 Skill 或已烘焙 Graph Workflow；AIBrain 不得把 Knowledge 摘要当作执行合同。");

            foreach (string skill in skills)
            {
                if (!Regex.IsMatch(skill, "^[a-z0-9][a-z0-9-]*$"))
                {
                    plan.blockers.Add("Skill 名称不安全：" + skill);
                    continue;
                }

                if (!TryResolveProjectPath(ProjectSkillsRoot + "/" + skill, out string root,
                        out string skillPathError))
                {
                    plan.blockers.Add("Skill 路径无效：" + skill + "；" + skillPathError);
                    continue;
                }
                string skillFile = Path.Combine(root, "SKILL.md");
                string metadataFile = Path.Combine(root, "agents", "openai.yaml");
                if (!File.Exists(skillFile) || !File.Exists(metadataFile))
                {
                    plan.blockers.Add("项目 Skill 不完整或不存在：" + skill);
                    continue;
                }

                string skillError = string.Empty;
                string metadataError = string.Empty;
                if (!TryReadTextAndHash(skillFile, out string skillText, out string skillHash, out skillError)
                    || !TryReadTextAndHash(metadataFile, out string metadataText, out string metadataHash,
                        out metadataError))
                {
                    plan.blockers.Add("Skill 无法严格读取：" + skill + "；"
                        + (string.IsNullOrWhiteSpace(skillError) ? metadataError : skillError));
                    continue;
                }
                if (!TryValidateSkillContract(skill, skillText, metadataText, out string skillContractError))
                {
                    plan.blockers.Add("项目 Skill 合同无效：" + skill + "；" + skillContractError);
                    continue;
                }
                if (!TryReadSkillGovernanceMetadata(root, skill,
                        out ESAIBrainSkillGovernanceMetadata governance,
                        out string governanceHash, out string governanceError))
                {
                    plan.blockers.Add("项目 Skill 治理元数据无效：" + skill + "；" + governanceError);
                    continue;
                }
                if (!TryReadSkillCatalogRecord(skill, skillHash, governanceHash,
                        out ESAIBrainSkillCatalogRecord catalog, out string catalogError))
                {
                    plan.blockers.Add("项目 Skill 未通过 Catalog 注册门禁：" + skill + "；" + catalogError);
                    continue;
                }
                if (!TryResolveSkillEligibility(governance.maturity, governance.delivery,
                        catalog.registrationState, out SkillEligibility eligibility,
                        out string eligibilityError))
                {
                    plan.blockers.Add("Skill 发现策略无效：" + skill + "；" + eligibilityError);
                    continue;
                }
                if (string.Equals(eligibility.planEligibility, "none", StringComparison.Ordinal))
                {
                    plan.blockers.Add("Skill 当前生命周期不可用于计划：" + skill
                        + "（" + eligibility.discoveryState + ").");
                    continue;
                }
                if (string.Equals(eligibility.planEligibility, "advisory-only", StringComparison.Ordinal)
                    && (plan.command == null
                        || !string.Equals(plan.command.writeMode, "read-only", StringComparison.OrdinalIgnoreCase)))
                {
                    plan.blockers.Add("候选 Skill 只能用于只读建议，不能绑定写入或运行任务：" + skill);
                    continue;
                }

                plan.skills.Add(new ESAIBrainSkillBinding
                {
                    name = skill,
                    skillPath = ToProjectRelative(root),
                    skillHash = skillHash,
                    metadataHash = metadataHash,
                    governanceHash = governanceHash,
                    tier = governance?.tier ?? string.Empty,
                    maturity = governance?.maturity ?? string.Empty,
                    delivery = governance?.delivery ?? string.Empty,
                    evidenceLevel = governance?.evidenceLevel ?? string.Empty,
                    riskClass = governance?.riskClass ?? string.Empty,
                    authorityClass = governance?.authorityClass ?? string.Empty,
                    owner = governance?.owner ?? string.Empty,
                    acceptanceOwner = governance?.acceptanceOwner ?? string.Empty,
                    requiresBrainPlan = governance?.requiresBrainPlan ?? false,
                    allowDirectExecution = governance?.allowDirectExecution ?? false,
                    writePolicy = governance?.writePolicy ?? string.Empty,
                    family = catalog.family,
                    registrationState = catalog.registrationState,
                    discoveryState = eligibility.discoveryState,
                    planEligibility = eligibility.planEligibility,
                    runtimeEligibility = eligibility.runtimeEligibility,
                    reviewRequired = eligibility.reviewRequired,
                });
            }

            if (plan.skills.Count == 0 && skills.Count > 0)
                plan.blockers.Add("没有任何 Skill 通过项目目录和 UTF-8 校验。");
            plan.authority.skill = hasWorkflow && plan.skills.Count > 0
                ? "ProjectSkills + BakedGraphWorkflow"
                : hasWorkflow ? "BakedGraphWorkflow" : "ProjectSkills";
        }

        private static bool ValidateWorkflow(ESAIBrainPlan plan, ESAIBrainWorkflowAuthority workflow)
        {
            if (workflow == null || string.IsNullOrWhiteSpace(workflow.workflowId)
                && string.IsNullOrWhiteSpace(workflow.contentHash)
                && string.IsNullOrWhiteSpace(workflow.sourceAssetGuid))
                return false;
            if (string.IsNullOrWhiteSpace(workflow.workflowId)
                || !Regex.IsMatch(workflow.workflowId, "^[A-Za-z0-9._:-]{1,160}$"))
            {
                plan.blockers.Add("Graph Workflow 缺少安全的稳定 ID。");
                return false;
            }
            if (!ESAutomationWorkerRegistration.IsSha256(workflow.contentHash))
            {
                plan.blockers.Add("Graph Workflow 必须携带 64 位内容指纹。");
                return false;
            }
            if (string.IsNullOrWhiteSpace(workflow.sourceAssetGuid)
                || !Regex.IsMatch(workflow.sourceAssetGuid, "^[a-fA-F0-9]{32}$"))
            {
                plan.blockers.Add("Graph Workflow 必须携带当前 Graph 的 SourceAssetGuid。");
                return false;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(workflow.sourceAssetGuid);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                plan.blockers.Add("Graph Workflow SourceAssetGuid 未解析到当前项目资产。");
                return false;
            }

            ESGraphAssetBase graphAsset = AssetDatabase.LoadAssetAtPath<ESGraphAssetBase>(assetPath);
            if (graphAsset == null)
            {
                plan.blockers.Add("Graph Workflow SourceAssetGuid 未解析到 Stable Graph 资产：" + assetPath);
                return false;
            }

            try
            {
                if (!ESGraphAuthoringRegistry.TryBake(graphAsset,
                        out ESBakedGraphSnapshot snapshot, out IESBakedGraphPlan bakedPlan,
                        out List<ESGraphValidationIssue> issues)
                    || snapshot == null || bakedPlan == null)
                {
                    string detail = string.Join("；", (issues ?? new List<ESGraphValidationIssue>())
                        .Where(issue => issue != null)
                        .Take(3)
                        .Select(issue => issue.code + ":" + issue.message));
                    plan.blockers.Add("Graph Workflow 当前 Graph 无法完成只读 Bake。"
                        + (string.IsNullOrWhiteSpace(detail) ? string.Empty : " " + detail));
                    return false;
                }
                if (!string.Equals(snapshot.GraphId, workflow.workflowId, StringComparison.Ordinal)
                    || !string.Equals(snapshot.ContentSignature, workflow.contentHash,
                        StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(bakedPlan.SourceContentSignature, workflow.contentHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    plan.blockers.Add("Graph Workflow 身份或内容指纹与当前 Graph Bake 结果不一致。");
                    return false;
                }
            }
            catch (Exception exception)
            {
                plan.blockers.Add("Graph Workflow 只读 Bake 失败：" + exception.Message);
                return false;
            }
            plan.workflow = new ESAIBrainWorkflowAuthority
            {
                workflowId = workflow.workflowId,
                contentHash = workflow.contentHash.ToLowerInvariant(),
                sourceAssetGuid = workflow.sourceAssetGuid ?? string.Empty,
            };
            return true;
        }

        private static void ValidateTask(ESAIBrainPlan plan, ESAIBrainRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.taskId) || request.taskVersion < 1)
            {
                plan.blockers.Add("未指定已注册的 Automation TaskContract。");
                return;
            }

            if (!ESAutomationFacade.TryGetDescriptor(request.taskId, request.taskVersion,
                    out ESAutomationTaskDescriptor descriptor))
            {
                plan.blockers.Add("Automation Facade 未注册任务："
                    + request.taskId + "@" + request.taskVersion);
                return;
            }

            if (!ESAutomationTaskRegistry.TryGet(request.taskId, request.taskVersion,
                    out ESAutomationTaskContract contract))
            {
                plan.blockers.Add("任务缺少受信 TaskContract："
                    + request.taskId + "@" + request.taskVersion);
                return;
            }

            string taskContractHash;
            string descriptorHash;
            try
            {
                descriptor.Validate();
                contract.Validate();
                taskContractHash = contract.ComputeStableHash();
                descriptorHash = ComputeCanonicalSha256(JToken.FromObject(descriptor));
            }
            catch (Exception exception)
            {
                plan.blockers.Add("TaskContract 校验失败：" + exception.Message);
                return;
            }

            if (contract.worker == null || !contract.worker.enabled)
                plan.blockers.Add("TaskContract 的 Worker 未被 C# Editor 启用。");
            if (request.fromAi && !descriptor.allowAiInvoke)
                plan.blockers.Add("任务未声明 allowAiInvoke，AIBrain 拒绝 AI 直接调用：" + request.taskId);

            plan.task = new ESAIBrainTaskBinding
            {
                taskId = descriptor.taskId,
                taskVersion = descriptor.taskVersion,
                displayName = descriptor.displayName,
                category = descriptor.category,
                summary = descriptor.summary,
                inputSchemaHash = descriptor.inputSchemaHash,
                descriptorHash = descriptorHash,
                taskContractHash = taskContractHash,
                allowAiInvoke = descriptor.allowAiInvoke,
                allowInPlayMode = descriptor.allowInPlayMode,
                workerId = contract.worker?.workerId ?? string.Empty,
                workerType = contract.worker?.type ?? string.Empty,
                workerVersion = contract.worker?.version ?? string.Empty,
                workerEntrypointHash = contract.worker?.entrypointHash ?? string.Empty,
                workerEnabled = contract.worker?.enabled ?? false,
            };
            plan.task.capabilities.AddRange(contract.capabilities ?? new List<string>());
            plan.authority.automation = "ESAutomationFacade";
            ValidateFeishuCommandTaskBinding(plan);
        }

        private static void ValidateFeishuCommandTaskBinding(ESAIBrainPlan plan)
        {
            string taskId = plan.task?.taskId ?? string.Empty;
            string expectedCommandId;
            switch (taskId)
            {
                case "es.feishu.read":
                    expectedCommandId = "feishu.read";
                    break;
                case "es.feishu.task.monitor":
                    expectedCommandId = "feishu.task.monitor";
                    break;
                case "es.feishu.task.dispatch":
                case "es.feishu.task.transition":
                    expectedCommandId = "feishu.task.mutate";
                    break;
                case "es.feishu.identity.claim":
                    expectedCommandId = "feishu.identity.manage";
                    break;
                case "es.feishu.message.send":
                    expectedCommandId = "feishu.message.send";
                    break;
                case "es.task-context.evaluate":
                    expectedCommandId = "task.context-runtime.mutate";
                    break;
                default:
                    return;
            }

            if (plan.command == null
                || !string.Equals(plan.command.id, expectedCommandId, StringComparison.Ordinal))
            {
                plan.blockers.Add("飞书 TaskContract 必须绑定精确 AICommand："
                    + taskId + " -> " + expectedCommandId + "。");
                return;
            }
            if ((taskId == "es.feishu.task.dispatch" || taskId == "es.feishu.task.transition")
                && (!string.Equals(plan.command.writeMode, "external-run", StringComparison.Ordinal)
                    || !string.Equals(plan.command.riskLevel, "L3", StringComparison.Ordinal)))
                plan.blockers.Add("飞书外部写 AICommand 必须声明 L3/external-run，"
                    + "并由 TaskContract 的 ExternalWrite 能力单独收紧。"
                    + " 当前为 " + plan.command.riskLevel + "/" + plan.command.writeMode + "。");
            if (taskId == "es.feishu.identity.claim"
                && (!string.Equals(plan.command.writeMode, "scoped-write", StringComparison.Ordinal)
                    || !string.Equals(plan.command.riskLevel, "L2", StringComparison.Ordinal)))
                plan.blockers.Add("飞书本地身份 AICommand 必须声明 L2/scoped-write。"
                    + " 当前为 " + plan.command.riskLevel + "/" + plan.command.writeMode + "。");
            if (taskId == "es.feishu.message.send"
                && (!string.Equals(plan.command.writeMode, "external-run", StringComparison.Ordinal)
                    || !string.Equals(plan.command.riskLevel, "L3", StringComparison.Ordinal)))
                plan.blockers.Add("飞书消息发送 AICommand 必须声明 L3/external-run。"
                    + " 当前为 " + plan.command.riskLevel + "/" + plan.command.writeMode + "。");
        }

        private static bool TryReadAiwarnings(ESAIBrainPlan plan, string objective,
            IReadOnlyCollection<string> routeKeys, out string error)
        {
            error = string.Empty;
            var paths = new HashSet<string>(StringComparer.Ordinal)
            {
                AiwarningsReadme,
                AiwarningsCurrentStatus,
                AiwarningsRuleIndex,
            };

            ApplyAiwarningsMigrationSafetyGate(plan, objective, routeKeys, false);

            if (!TryResolveProjectPath(AiwarningsRouteCatalog, out string catalogPath,
                    out string catalogPathError))
            {
                error = "AIWarnings 路由目录路径无效：" + catalogPathError;
                return false;
            }
            string catalogError = string.Empty;
            if (File.Exists(catalogPath)
                && TryReadTextAndHash(catalogPath, out string catalogText, out string catalogHash,
                    out catalogError))
            {
                plan.authority.warningsCatalogHash = catalogHash;
                try
                {
                    JObject document = JObject.Parse(catalogText);
                    foreach (JToken route in document["routes"] as JArray ?? new JArray())
                    {
                        string routeId = route.Value<string>("id") ?? string.Empty;
                        bool matched = routeKeys.Contains(routeId, StringComparer.Ordinal)
                            || (route["match"] as JArray ?? new JArray()).Values<string>()
                                .Any(match => ContainsIgnoreCase(objective, match));
                        if (!matched) continue;
                        if (route.Value<bool?>("requiresExplicitSourcePreservation") == true)
                            ApplyAiwarningsMigrationSafetyGate(plan, objective, routeKeys, true);
                        string state = route.Value<string>("state") ?? string.Empty;
                        if (string.Equals(state, "reserved", StringComparison.Ordinal))
                        {
                            plan.blockers.Add("AIWarnings 保留路由尚未实现，禁止宣称已接入：" + routeId);
                        }
                        foreach (string mustRead in (route["mustRead"] as JArray ?? new JArray()).Values<string>())
                            paths.Add(mustRead);
                    }
                }
                catch (Exception exception)
                {
                    error = "AIWarnings 路由目录 JSON 无法解析：" + exception.Message;
                }
            }
            else if (File.Exists(catalogPath))
            {
                error = "AIWarnings 路由目录无法严格读取：" + catalogError;
            }

            foreach (string projectPath in paths.OrderBy(item => item, StringComparer.Ordinal))
            {
                if (!TryResolveProjectPath(projectPath, out string fullPath, out string pathError))
                {
                    plan.blockers.Add("AIWarnings 路径无效：" + projectPath + "；" + pathError);
                    continue;
                }
                if (!TryReadTextAndHash(fullPath, out _, out string hash, out string readError))
                {
                    plan.blockers.Add("AIWarnings 读取失败：" + projectPath + "；" + readError);
                    continue;
                }
                plan.warnings.Add(new ESAIBrainEvidenceBinding { projectPath = projectPath, sha256 = hash });
            }

            plan.authority.warnings = "AIWarnings P0/规则索引";
            return string.IsNullOrWhiteSpace(error);
        }

        private static bool TryReadKnowledge(ESAIBrainPlan plan,
            IReadOnlyCollection<string> routeKeys, out string error)
        {
            error = string.Empty;
            if (!TryResolveProjectPath(KnowledgeIndexPath, out string indexPath, out string indexPathError))
            {
                error = "AIKnowledge 索引路径无效：" + indexPathError;
                plan.blockers.Add(error);
                return false;
            }
            if (!TryReadTextAndHash(indexPath, out string text, out string indexHash, out error))
            {
                plan.blockers.Add("AIKnowledge 索引不可用：" + error);
                return false;
            }
            plan.knowledgeIndexHash = indexHash;

            if (!TryParseKnowledgeIndex(text, out List<KnowledgeIndexEntry> entries, out error))
            {
                plan.blockers.Add("AIKnowledge 索引解析失败：" + error);
                return false;
            }

            var normalizedRouteKeys = new HashSet<string>(routeKeys, StringComparer.Ordinal);
            var candidates = entries
                .Select(entry => new
                {
                    Entry = entry,
                    MatchedKeyCount = entry.routeKeys.Count(normalizedRouteKeys.Contains),
                })
                .Where(candidate => candidate.MatchedKeyCount > 0)
                .ToList();
            var bestMatchedKeyCountByRoute = normalizedRouteKeys.ToDictionary(
                routeKey => routeKey,
                routeKey => candidates
                    .Where(candidate => candidate.Entry.routeKeys.Contains(routeKey))
                    .Select(candidate => candidate.MatchedKeyCount)
                    .DefaultIfEmpty(0)
                    .Max(),
                StringComparer.Ordinal);
            List<KnowledgeIndexEntry> matched = candidates
                .Where(candidate => candidate.Entry.routeKeys.Any(routeKey =>
                    normalizedRouteKeys.Contains(routeKey)
                    && candidate.MatchedKeyCount == bestMatchedKeyCountByRoute[routeKey]))
                .OrderByDescending(candidate => candidate.MatchedKeyCount)
                .ThenByDescending(candidate =>
                    (double)candidate.MatchedKeyCount / candidate.Entry.routeKeys.Count)
                .ThenBy(candidate => candidate.Entry.knowledgeId, StringComparer.Ordinal)
                .Take(MaxKnowledgeEntriesPerPlan)
                .Select(candidate => candidate.Entry)
                .ToList();
            if (matched.Count == 0)
            {
                plan.blockers.Add("没有 Knowledge 条目匹配当前 routeKeys。");
                return false;
            }

            foreach (KnowledgeIndexEntry entry in matched)
            {
                string entryProjectPath = "Documentation/AIKnowledge/" + entry.file.TrimStart('/', '\\');
                if (!TryResolveProjectPath(entryProjectPath, out string entryPath, out string entryPathError))
                {
                    plan.blockers.Add("Knowledge 条目路径无效：" + entry.file + "；" + entryPathError);
                    continue;
                }
                if (!TryReadTextAndHash(entryPath, out string entryText, out _,
                        out string entryError))
                {
                    plan.blockers.Add("Knowledge 条目读取失败：" + entry.file + "；" + entryError);
                    continue;
                }
                if (!TryReadDeclaredContentHash(entryText, out string declaredContentHash)
                    || !string.Equals(declaredContentHash, entry.contentHash, StringComparison.OrdinalIgnoreCase))
                {
                    plan.blockers.Add("Knowledge 条目与索引的 ContentHash 不一致：" + entry.knowledgeId);
                    continue;
                }

                var binding = new ESAIBrainKnowledgeBinding
                {
                    knowledgeId = entry.knowledgeId,
                    file = entry.file,
                    topic = entry.topic,
                    contentHash = entry.contentHash,
                };
                binding.routeKeys.AddRange(entry.routeKeys);
                binding.relatedSkills.AddRange(entry.relatedSkills);
                binding.requiredReads.AddRange(entry.requiredReads);

                List<KnowledgeSourceReference> sourceRefs = ParseSourceRefs(entryText).ToList();
                if (sourceRefs.Count == 0)
                {
                    plan.blockers.Add("Knowledge 条目缺少 SourceRefs：" + entry.knowledgeId);
                }
                var actualSourceHashes = new List<string>(sourceRefs.Count);
                var normalizedSourceHashes = new List<string>(sourceRefs.Count);
                foreach (KnowledgeSourceReference sourceRef in sourceRefs)
                {
                    if (!TryResolveProjectPath(sourceRef.path, out string sourceFullPath, out string sourcePathError))
                    {
                        plan.blockers.Add("Knowledge SourceRef 路径无效：" + sourceRef.path + "；" + sourcePathError);
                        continue;
                    }
                    if (!TryReadTextAndHash(sourceFullPath, out _, out string actualHash, out string sourceError))
                    {
                        plan.blockers.Add("Knowledge SourceRef 读取失败：" + sourceRef.path + "；" + sourceError);
                    }
                    else
                    {
                        actualSourceHashes.Add(actualHash);
                        bool normalizedHashAvailable = TryReadNormalizedTextHash(sourceFullPath,
                            out string normalizedHash, out _);
                        if (normalizedHashAvailable) normalizedSourceHashes.Add(normalizedHash);
                        if (!string.Equals(actualHash, sourceRef.sha256, StringComparison.OrdinalIgnoreCase)
                            && (!normalizedHashAvailable
                                || !string.Equals(normalizedHash, sourceRef.sha256,
                                    StringComparison.OrdinalIgnoreCase)))
                            plan.blockers.Add("Knowledge SourceRef 哈希漂移：" + sourceRef.path);
                    }
                    binding.sourceRefs.Add(sourceRef.path + " (" + actualHash + ")");
                }
                if (actualSourceHashes.Count == sourceRefs.Count
                    && normalizedSourceHashes.Count == sourceRefs.Count
                    && !MatchesKnowledgeSourceSetHash(entry.contentHash, sourceRefs,
                        actualSourceHashes, normalizedSourceHashes))
                    plan.blockers.Add("Knowledge SourceRef 集合 ContentHash 不匹配：" + entry.knowledgeId);

                foreach (string requiredRead in entry.requiredReads)
                {
                    if (!TryResolveProjectPath(requiredRead, out string requiredFullPath,
                            out string requiredPathError))
                    {
                        plan.blockers.Add("Knowledge RequiredRead 路径无效：" + requiredRead + "；" + requiredPathError);
                        continue;
                    }
                    if (!TryReadTextAndHash(requiredFullPath, out _, out string requiredHash, out string requiredError))
                    {
                        plan.blockers.Add("Knowledge RequiredRead 读取失败：" + requiredRead + "；" + requiredError);
                    }
                    else
                    {
                        plan.evidence.Add(new ESAIBrainEvidenceBinding
                        {
                            projectPath = requiredRead,
                            sha256 = requiredHash,
                        });
                    }
                }
                plan.knowledge.Add(binding);
            }

            plan.authority.knowledge = "AIKnowledge 定向索引（不拥有源事实）";
            return plan.knowledge.Count > 0;
        }

        private static IEnumerable<KnowledgeSourceReference> ParseSourceRefs(string text)
        {
            var pattern = new Regex(
                "^- `(?<path>[^`]+)` \\(`(?<hash>[a-fA-F0-9]{64})`\\)$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);
            foreach (string raw in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                string line = raw.Trim();
                Match match = pattern.Match(line);
                if (match.Success)
                {
                    yield return new KnowledgeSourceReference
                    {
                        path = match.Groups["path"].Value,
                        sha256 = match.Groups["hash"].Value.ToLowerInvariant(),
                    };
                }
            }
        }

        private static bool TryReadDeclaredContentHash(string text, out string contentHash)
        {
            Match match = Regex.Match(text ?? string.Empty,
                "(?m)^`ContentHash`:\\s*`(?<hash>[a-fA-F0-9]{64})`\\s*$",
                RegexOptions.CultureInvariant);
            contentHash = match.Success ? match.Groups["hash"].Value.ToLowerInvariant() : string.Empty;
            return match.Success;
        }

        private static bool TryParseKnowledgeIndex(string text, out List<KnowledgeIndexEntry> entries,
            out string error)
        {
            entries = new List<KnowledgeIndexEntry>();
            error = string.Empty;
            int schemaVersion = 0;
            KnowledgeIndexEntry current = null;
            bool readingRequiredReads = false;
            foreach (string raw in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                string trimmed = raw.Trim();
                if (trimmed.StartsWith("schemaVersion:", StringComparison.Ordinal))
                {
                    int.TryParse(trimmed.Substring("schemaVersion:".Length).Trim(), out schemaVersion);
                    continue;
                }
                if (trimmed.StartsWith("- knowledgeId:", StringComparison.Ordinal))
                {
                    if (current != null) entries.Add(current);
                    current = new KnowledgeIndexEntry
                    {
                        knowledgeId = Unquote(trimmed.Substring("- knowledgeId:".Length).Trim()),
                    };
                    readingRequiredReads = false;
                    continue;
                }
                if (current == null) continue;
                if (trimmed.StartsWith("file:", StringComparison.Ordinal))
                {
                    current.file = Unquote(trimmed.Substring(5).Trim());
                    readingRequiredReads = false;
                }
                else if (trimmed.StartsWith("topic:", StringComparison.Ordinal))
                {
                    current.topic = Unquote(trimmed.Substring(6).Trim());
                    readingRequiredReads = false;
                }
                else if (trimmed.StartsWith("routeKeys:", StringComparison.Ordinal))
                {
                    current.routeKeys.AddRange(ParseInlineList(trimmed.Substring(10).Trim()));
                    readingRequiredReads = false;
                }
                else if (trimmed.StartsWith("relatedSkills:", StringComparison.Ordinal))
                {
                    current.relatedSkills.AddRange(ParseInlineList(trimmed.Substring(14).Trim()));
                    readingRequiredReads = false;
                }
                else if (trimmed.StartsWith("requiredReads:", StringComparison.Ordinal))
                {
                    string inlineReads = trimmed.Substring("requiredReads:".Length).Trim();
                    if (inlineReads.Length > 0)
                        current.requiredReads.AddRange(ParseInlineList(inlineReads));
                    readingRequiredReads = inlineReads.Length == 0;
                }
                else if (readingRequiredReads && trimmed.StartsWith("- ", StringComparison.Ordinal))
                {
                    current.requiredReads.Add(Unquote(trimmed.Substring(2).Trim()));
                }
                else if (trimmed.StartsWith("contentHash:", StringComparison.Ordinal))
                {
                    current.contentHash = Unquote(trimmed.Substring(12).Trim());
                    readingRequiredReads = false;
                }
                else if (trimmed.Length > 0 && !raw.StartsWith(" ", StringComparison.Ordinal))
                {
                    readingRequiredReads = false;
                }
            }
            if (current != null) entries.Add(current);
            if (schemaVersion != 1)
            {
                error = "schemaVersion 必须为 1。";
                return false;
            }
            foreach (KnowledgeIndexEntry entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.knowledgeId) || string.IsNullOrWhiteSpace(entry.file)
                    || !Regex.IsMatch(entry.contentHash ?? string.Empty, "^[a-fA-F0-9]{64}$")
                    || entry.routeKeys.Count == 0)
                {
                    error = "Knowledge 条目缺少稳定 ID、文件、RouteKeys 或 ContentHash。";
                    return false;
                }
            }
            return entries.Count > 0;
        }

        private static List<string> ParseInlineList(string value)
        {
            string content = value.Trim();
            if (content.StartsWith("[", StringComparison.Ordinal) && content.EndsWith("]", StringComparison.Ordinal))
                content = content.Substring(1, content.Length - 2);
            return content.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => Unquote(item.Trim())).Where(item => item.Length > 0).ToList();
        }

        private static string Unquote(string value)
        {
            string result = value?.Trim() ?? string.Empty;
            if (result.Length >= 2 && ((result[0] == '\'' && result[result.Length - 1] == '\'')
                || (result[0] == '"' && result[result.Length - 1] == '"')))
                return result.Substring(1, result.Length - 2);
            return result;
        }

        private static List<string> NormalizeValues(IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>())
                .Select(value => value?.Trim().ToLowerInvariant() ?? string.Empty)
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        // Foundation routes are inferred from task shape so callers do not need to know
        // internal Skill names. Refresh intent is deliberately mapped to dedicated
        // routes so it cannot broaden discovery into the generic Skill portfolio.
        private static List<string> InferObjectiveRouteKeys(string objective)
        {
            string text = objective ?? string.Empty;
            bool hasAbcdDynamicModeSignal = ContainsAnyIgnoreCase(text,
                "ABCD", "ABCD.Dynamic", "ABCD Dynamic", "ABCD动态", "ABCD动态体系",
                "ABCD动态协作", "ES动态协作体", "ES 动态协作体", "动态协作体",
                "动态协作接管", "ABCD接管");
            bool hasSkillUnderstandingRefreshSignal = ContainsAnyIgnoreCase(text,
                "你的理解已经过时", "理解已经过时", "刷新一下技能理解", "刷新技能理解",
                "重新理解当前项目提供的 skill", "重新理解当前项目的 skill",
                "技能增量理解", "技能理解刷新", "技能能力刷新",
                "understanding drift", "skill understanding refresh", "refresh skill understanding",
                "capability refresh", "incremental skill discovery");
            bool hasSnapshotWord = ContainsAnyIgnoreCase(text, "snapshot", "快照");
            bool hasExplicitReadConsistencySignal = ContainsAnyIgnoreCase(text,
                "task read", "read manifest", "读取清单", "源文件哈希", "文件哈希",
                "parser registry", "解析器注册", "projectionpacket", "projection cache",
                "多文件读取", "重复读取", "二进制解析", "文件格式");
            bool hasFileContext = ContainsAnyIgnoreCase(text,
                "文件", "读取", "解析", "parser", "projection", "binary", "hash", "哈希",
                "manifest", "清单");
            bool hasCacheContext = ContainsAnyIgnoreCase(text, "缓存命中", "缓存失效", "缓存漂移")
                && hasFileContext;
            bool hasReadConsistencyContext = hasExplicitReadConsistencySignal
                || (hasSnapshotWord && hasFileContext) || hasCacheContext;
            bool hasStorySnapshotContext = ContainsAnyIgnoreCase(text,
                "story definition snapshot", "esstorydefinitionsnapshot", "剧情定义快照");
            bool hasStableGraphContext = ContainsAnyIgnoreCase(text,
                "stable graph", "stablegraph", "stable-graph", "esgraph", "graphview",
                "noderunner", "edge.order", "edgeid", "graph bake", "graph snapshot",
                "图资产", "图节点", "图边", "跨 domain 粘贴", "跨域粘贴", "未知端口")
                || hasStorySnapshotContext;
            bool hasGraphIdentity = hasStableGraphContext && ContainsAnyIgnoreCase(text,
                "稳定身份", "身份", "graphid", "nodeid", "portid", "edgeid", "新增节点");
            bool hasGraphUndo = hasStableGraphContext && ContainsAnyIgnoreCase(text,
                "undo", "redo", "回滚", "原子编辑", "多步编辑");
            bool hasGraphMigration = hasStableGraphContext && ContainsAnyIgnoreCase(text,
                "迁移", "migration", "schema", "跨 domain", "跨域", "未知端口");
            bool hasEdgeOrder = hasStableGraphContext && ContainsAnyIgnoreCase(text,
                "edge.order", "边顺序", "调序", "重连");
            bool hasGraphSnapshot = hasStableGraphContext && (hasSnapshotWord
                || ContainsAnyIgnoreCase(text, "内容签名", "contentsignature"));
            bool hasGraphBake = hasStableGraphContext && ContainsAnyIgnoreCase(text,
                "bake", "烘焙", "内容签名", "缓存失效");
            bool hasLegacyGraph = hasStableGraphContext && ContainsAnyIgnoreCase(text,
                "legacy", "旧 graph", "旧graph", "noderunner", "恢复使用", "恢复旧");
            bool hasExecutionGraphContext = ContainsAnyIgnoreCase(text, "graph", "执行图", "工作流图")
                && ContainsAnyIgnoreCase(text, "taskcontract", "skillcall", "runrecord",
                    "fanout", "join", "aiskill", "执行", "恢复执行", "执行恢复");
            bool hasTaskContractContext = hasExecutionGraphContext
                && ContainsAnyIgnoreCase(text, "taskcontract", "任务合同");
            bool hasRunRecordContext = hasExecutionGraphContext
                && ContainsAnyIgnoreCase(text, "runrecord", "运行记录");
            bool hasFeishuContext = ContainsAnyIgnoreCase(text, "飞书", "feishu", "lark");
            bool hasTaskMonitor = hasFeishuContext && ContainsAnyIgnoreCase(text,
                "监控", "追踪", "查看任务", "读取任务", "任务清单", "task monitor", "task list");
            bool hasTaskDispatch = hasFeishuContext && ContainsAnyIgnoreCase(text,
                "派发", "分发", "创建任务", "创建清单", "虚拟团队", "dispatch", "virtual team");
            bool hasTaskTransition = hasFeishuContext && ContainsAnyIgnoreCase(text,
                "推进", "进度", "完成任务", "重新打开", "成员", "提醒", "transition", "progress");
            bool hasIdentityClaim = hasFeishuContext && ContainsAnyIgnoreCase(text,
                "角色", "认领", "机器人", "个人身份", "配置引导", "接入引导",
                "identity claim", "role claim", "bot ownership", "onboarding");
            bool hasMessageSend = hasFeishuContext && ContainsAnyIgnoreCase(text,
                "发送消息", "消息通知", "通知角色", "单人消息", "message send", "notify role");
            bool hasEditorWindowContext = ContainsAnyIgnoreCase(text,
                "editorwindow", "editor window", "编辑器窗口");
            bool hasExecuteAlwaysSignal = ContainsAnyIgnoreCase(text,
                "executealways", "execute always");
            bool hasExecuteInEditMode = ContainsAnyIgnoreCase(text,
                "executeineditmode", "execute in edit mode");
            bool hasPrefabStageContext = ContainsAnyIgnoreCase(text,
                "prefab stage", "prefabstage", "prefab mode", "prefabmode",
                "prefab auto save", "预制体模式", "预制体阶段", "预制体自动保存");
            bool hasApplicationIsPlayingObject = ContainsAnyIgnoreCase(text,
                "application.isplaying", "application isplaying", "playing world", "播放世界");
            bool hasEditModeExecution = hasExecuteAlwaysSignal || hasExecuteInEditMode
                || ContainsAnyIgnoreCase(text,
                    "编辑态执行", "编辑模式执行", "edit mode 回调", "edit mode lifecycle");
            bool hasPrefabAutoSave = hasPrefabStageContext
                && ContainsAnyIgnoreCase(text, "auto save", "autosave", "自动保存");
            bool hasUnityLifecycleContext = hasEditorWindowContext || hasEditModeExecution
                || hasPrefabStageContext || hasApplicationIsPlayingObject
                || ContainsAnyIgnoreCase(text,
                "unity", "monobehaviour", "gameobject", "awake", "onenable", "ondisable",
                "ondestroy", "runtimeinitializeonloadmethod",
                "script execution order", "defaultexecutionorder", "domain reload",
                "reloaddomain", "scene reload", "enter play mode", "play mode options",
                "进入 play", "第二次进入 play", "域重载", "场景重载", "执行顺序");
            bool hasStartCallback = Regex.IsMatch(text,
                "(?<![A-Za-z0-9_])Start(?![A-Za-z0-9_])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            bool hasMonoBehaviourLifecycle = hasUnityLifecycleContext && !hasEditModeExecution
                && (ContainsAnyIgnoreCase(text,
                    "monobehaviour", "awake", "onenable", "ondisable", "ondestroy",
                    "生命周期") || hasStartCallback);
            bool hasStaticState = hasUnityLifecycleContext && ContainsAnyIgnoreCase(text,
                "static", "静态事件", "静态单例", "重复订阅", "第二次进入 play");
            bool hasDomainReload = hasUnityLifecycleContext && ContainsAnyIgnoreCase(text,
                "domain reload", "reloaddomain", "域重载", "第二次进入 play");
            bool hasSceneReload = hasUnityLifecycleContext && ContainsAnyIgnoreCase(text,
                "scene reload", "场景重载");
            bool hasEnterPlayMode = hasUnityLifecycleContext && ContainsAnyIgnoreCase(text,
                "enter play mode", "play mode options", "进入 play", "第二次进入 play",
                "关闭 domain reload", "关闭 scene reload");
            bool hasScriptExecutionOrder = hasUnityLifecycleContext && ContainsAnyIgnoreCase(text,
                "script execution order", "defaultexecutionorder",
                "runtimeinitializeonloadmethod", "执行顺序");
            bool hasUnityCompile = hasUnityLifecycleContext && ContainsAnyIgnoreCase(text,
                "编译", "compile", "compilation");
            bool hasPlayerEvidence = hasUnityLifecycleContext
                && ContainsAnyIgnoreCase(text, "player", "播放器")
                && ContainsAnyIgnoreCase(text, "证明", "证据", "验证", "evidence", "verify");
            bool hasExplicitKccContext = ContainsAnyIgnoreCase(text,
                "kcc", "kinematic character controller", "kinematiccharactercontroller", "角色 kcc");
            bool hasEntityCharacterControllerContext = ContainsAnyIgnoreCase(text,
                    "character controller", "charactercontroller")
                && ContainsAnyIgnoreCase(text, "entity", "角色", "玩家");
            bool hasKccMotionContext = hasExplicitKccContext || hasEntityCharacterControllerContext
                || ContainsAnyIgnoreCase(text, "角色移动", "玩家移动");
            bool hasVehicleMotionContext = ContainsAnyIgnoreCase(text,
                    "vehicle", "载具", "车辆", "骑乘", "mount", "driver", "驾驶")
                && ContainsAnyIgnoreCase(text,
                    "运动", "移动", "motion", "rigidbody", "kcc", "fixedupdate", "fixed update", "物理");
            bool hasRigidbodyContext = ContainsAnyIgnoreCase(text, "rigidbody", "刚体");
            bool hasFixedUpdateContext = ContainsAnyIgnoreCase(text,
                "fixedupdate", "fixed update", "固定更新", "固定步", "物理步");
            bool hasUiRaycastContext = ContainsAnyIgnoreCase(text,
                "graphicraycaster", "graphic raycaster", "ui raycast", "eventsystem", "pointereventdata");
            bool hasPhysicsQueryContext = !hasUiRaycastContext && ContainsAnyIgnoreCase(text,
                "physics query", "physics.query", "物理查询", "碰撞查询",
                "raycast", "ray cast", "射线检测", "射线查询",
                "spherecast", "sphere cast", "capsulecast", "capsule cast", "boxcast", "box cast",
                "球形投射", "胶囊投射", "盒体投射",
                "overlapsphere", "overlapbox", "overlapcapsule", "physics.overlap",
                "物理重叠", "重叠检测", "重叠查询");
            bool hasColliderContext = ContainsAnyIgnoreCase(text, "collider", "碰撞体");
            bool hasExplicitPhysicsTriggerContext = ContainsAnyIgnoreCase(text,
                "querytriggerinteraction", "querieshittriggers", "ontrigger", "istrigger", "is trigger",
                "physics trigger", "物理触发器", "碰撞触发器");
            bool hasTriggerWord = ContainsAnyIgnoreCase(text, "trigger", "触发器");
            bool hasTriggerContext = hasExplicitPhysicsTriggerContext
                || (hasTriggerWord && (hasColliderContext || hasPhysicsQueryContext));
            bool hasPhysicsContext = ContainsAnyIgnoreCase(text,
                    "physics", "物理", "collision", "oncollision", "碰撞检测", "碰撞回调", "碰撞矩阵", "发生碰撞")
                || hasRigidbodyContext || hasFixedUpdateContext || hasPhysicsQueryContext
                || hasColliderContext || hasExplicitPhysicsTriggerContext;
            bool hasLayerMaskContext = hasPhysicsContext && ContainsAnyIgnoreCase(text,
                "layermask", "layer mask", "层掩码", "物理层", "~0", "全层");
            bool hasQueryTriggerContext = hasPhysicsQueryContext && hasTriggerContext;
            bool hasTransformSyncContext = hasPhysicsContext && ContainsAnyIgnoreCase(text,
                "synctransforms", "sync transforms", "autosynctransforms", "transform 同步", "transform同步",
                "立即查询", "立刻查询", "旧位置");
            bool hasInterpolationContext = hasRigidbodyContext && ContainsAnyIgnoreCase(text,
                "interpolation", "interpolate", "extrapolate", "插值", "抖动", "jitter");
            bool hasSingleWriterContext = hasPhysicsContext && (ContainsAnyIgnoreCase(text,
                    "单写入者", "single writer", "第二写入者", "争写")
                || (hasRigidbodyContext && ContainsAnyIgnoreCase(text,
                    "transform", "moveposition", "moverotation", "写位置", "写旋转", "直接改")));
            bool hasKccGrounding = hasKccMotionContext && ContainsAnyIgnoreCase(text,
                "grounding", "grounded", "接地", "地面", "斜坡");
            bool hasKccMovingPlatform = hasKccMotionContext && ContainsAnyIgnoreCase(text,
                "moving platform", "移动平台");
            bool hasKccTeleport = hasKccMotionContext && ContainsAnyIgnoreCase(text,
                "teleport", "传送", "瞬移");
            bool hasKccMotionInfluence = hasKccMotionContext && ContainsAnyIgnoreCase(text,
                "motion influence", "运动影响", "击退", "冲量", "外力");
            bool hasKccVelocity = hasKccMotionContext && ContainsAnyIgnoreCase(text,
                "velocity", "速度");
            bool hasMountContext = hasVehicleMotionContext && ContainsAnyIgnoreCase(text,
                "mount", "骑乘", "上车", "下车", "座位");
            bool hasDriverContext = hasVehicleMotionContext && ContainsAnyIgnoreCase(text,
                "driver", "驾驶", "驾驶输入");
            bool hasUiToken = Regex.IsMatch(text,
                "(?i)(^|[^a-z0-9])ui([^a-z0-9]|$)", RegexOptions.CultureInvariant);
            bool hasScreenSpecContext = ContainsAnyIgnoreCase(text,
                "screen spec", "screenspec", "screen-spec", "屏幕规格");
            bool hasHudUi = ContainsAnyIgnoreCase(text,
                "hud ui", "hud 界面", "hud界面", "战斗 hud", "战斗hud");
            bool hasInventoryUi = ContainsAnyIgnoreCase(text,
                "inventory ui", "inventory screen", "collection ui", "背包界面", "背包 ui", "背包ui",
                "仓库界面", "图鉴界面", "配装界面", "装备界面");
            bool hasShopUi = ContainsAnyIgnoreCase(text,
                "shop ui", "shop screen", "store ui", "商店界面", "商店 ui", "商店ui", "商城界面");
            bool hasDialogueUi = ContainsAnyIgnoreCase(text,
                "dialogue ui", "dialogue screen", "conversation ui", "对话界面", "对话 ui", "对话ui", "剧情对话");
            bool hasMapUi = ContainsAnyIgnoreCase(text,
                "map ui", "map screen", "world map", "地图界面", "地图 ui", "地图ui", "世界地图");
            bool hasProgressionUi = ContainsAnyIgnoreCase(text,
                "progression ui", "skill tree ui", "quest ui", "技能树", "任务界面", "任务页", "成长界面");
            bool hasResultUi = ContainsAnyIgnoreCase(text,
                "result ui", "result screen", "results screen", "结算界面", "结果页", "奖励页");
            bool hasSettingsUi = ContainsAnyIgnoreCase(text,
                "settings ui", "settings screen", "设置界面", "设置页");
            bool hasGameUiScreenFamilyContext = hasHudUi || hasInventoryUi || hasShopUi || hasDialogueUi
                || hasMapUi || hasProgressionUi || hasResultUi || hasSettingsUi
                || ContainsAnyIgnoreCase(text, "game ui screen family", "screen family", "屏幕族", "主菜单界面");
            bool hasUiVisualDesignSubject = hasUiToken || hasScreenSpecContext || hasGameUiScreenFamilyContext
                || ContainsAnyIgnoreCase(text, "用户界面", "游戏界面", "界面预制体");
            bool hasUiIntentContract = ContainsAnyIgnoreCase(text,
                "intent spec", "intentspec", "intent-spec", "player intent", "player goal",
                "玩家目标", "界面目标", "交互目标", "主动作", "primary action");
            bool hasRegisteredUiAction = ContainsAnyIgnoreCase(text,
                "browse", "inspect", "select", "compare", "equip", "confirm", "cancel", "navigate",
                "filter", "sort", "claim", "configure", "track", "respond", "resume", "retry", "dismiss",
                "浏览", "查看", "检查", "选择", "比较", "装备", "确认", "取消", "导航", "筛选", "排序",
                "领取", "配置", "追踪", "回应", "继续", "重试", "关闭");
            bool hasUiActionDomain = hasUiVisualDesignSubject || ContainsAnyIgnoreCase(text,
                "背包", "仓库", "图鉴", "商店", "商城", "地图", "技能树", "任务", "对话", "设置",
                "菜单", "inventory", "collection", "shop", "store", "map", "quest", "dialogue", "settings");
            bool hasUiGoalFraming = ContainsAnyIgnoreCase(text,
                "玩家", "玩家目标", "用户想", "用户要", "界面目标", "ui 计划", "ui计划", "界面计划",
                "交互计划", "player wants", "player needs", "player goal", "ui plan", "screen plan");
            bool hasUiPlayerGoal = hasUiIntentContract
                || (hasRegisteredUiAction && hasUiActionDomain && hasUiGoalFraming);
            bool hasUiIntentClarification = hasUiPlayerGoal && ContainsAnyIgnoreCase(text,
                "澄清", "不明确", "缺少信息", "需要补充", "ambiguous", "clarify", "needs-clarification", "blocked");
            bool hasUiBusinessBridge = hasUiPlayerGoal && ContainsAnyIgnoreCase(text,
                "business bridge", "businessbridge", "业务桥接", "业务接入");
            bool hasUiColorDesign = hasUiVisualDesignSubject && ContainsAnyIgnoreCase(text,
                "ui color", "ui colour", "color palette", "配色", "颜色", "色彩", "品牌色");
            bool hasUiTypographyDesign = hasUiVisualDesignSubject && ContainsAnyIgnoreCase(text,
                "ui font", "typography", "字体", "字号", "字重", "fallback font");
            bool hasUiSpacingDesign = hasUiVisualDesignSubject && ContainsAnyIgnoreCase(text,
                "spacing token", "ui spacing", "间距", "留白");
            bool hasUiHierarchyDesign = hasUiVisualDesignSubject && ContainsAnyIgnoreCase(text,
                "visual hierarchy", "视觉层级", "信息层级");
            bool hasUiDensityDesign = hasUiVisualDesignSubject && ContainsAnyIgnoreCase(text,
                "information density", "信息密度", "compact ui", "紧凑布局");
            bool hasUiRarityDesign = hasUiVisualDesignSubject && ContainsAnyIgnoreCase(text,
                "rarity", "稀有度");
            bool hasUiMaterialDesign = hasUiVisualDesignSubject && ContainsAnyIgnoreCase(text,
                "ui material", "界面材质", "ui 材质", "ui材质");
            bool hasUiDesignToken = hasUiVisualDesignSubject && ContainsAnyIgnoreCase(text,
                "design token", "design-token", "ui token", "设计令牌", "视觉 token", "视觉token");
            bool hasUiVisualDesignContext = hasUiColorDesign || hasUiTypographyDesign || hasUiSpacingDesign
                || hasUiHierarchyDesign || hasUiDensityDesign || hasUiRarityDesign || hasUiMaterialDesign
                || hasUiDesignToken;
            bool hasUiContext = hasUiToken || hasScreenSpecContext || ContainsAnyIgnoreCase(text,
                "用户界面", "游戏界面", "界面预制体", "界面按钮", "界面参考图", "界面素材", "界面焦点",
                "界面导航", "界面文本", "界面长文本", "界面本地化", "本地化界面", "canvas", "hud")
                || hasGameUiScreenFamilyContext || hasUiVisualDesignContext || hasUiPlayerGoal;
            bool hasUiReferenceEvidence = hasUiContext && ContainsAnyIgnoreCase(text,
                "design evidence", "designevidence", "参考图", "reference image", "reference screenshot",
                "设计稿来源", "来源区域", "source region", "视觉观察", "观察与假设", "vision review",
                "reference provenance");
            bool hasUiAssetManifest = hasUiContext && ContainsAnyIgnoreCase(text,
                "assetmanifest", "asset manifest", "ui asset manifest", "素材清单", "素材 provenance",
                "素材来源", "素材许可证", "asset license", "spriteatlas", "sprite atlas", "图集归属",
                "crop policy", "裁剪策略", "9-slice", "九宫格", "asset resolver", "素材解析器");
            bool hasUiBehaviorSpec = hasUiContext && ContainsAnyIgnoreCase(text,
                "behaviorspec", "behavior spec", "behavior binding", "ui binding", "interaction.intent",
                "交互 intent", "交互绑定", "焦点导航", "focus navigation", "selectable navigation",
                "input modality", "输入模态", "键鼠焦点", "手柄焦点", "ui input module");
            bool hasUiTextResilience = hasUiContext && ContainsAnyIgnoreCase(text,
                "ui localization", "ui 本地化", "界面本地化", "long-content", "long content", "长文本",
                "text wrapping", "文字换行", "文本换行", "glyph coverage", "字形覆盖", "font fallback",
                "fallback font", "字体 fallback", "fallback 字体", "字体与 fallback", "字体和 fallback",
                "字体及 fallback", "字体回退", "字体降级",
                "bidi", "rtl", "双向文本", "从右到左", "line breaking", "行分断");
            bool hasUiMaterializer = hasUiContext && ContainsAnyIgnoreCase(text,
                "materializer", "物化器", "物化 ui", "物化 prefab", "物化预制体");
            bool hasSceneContext = !hasUiContext && ContainsAnyIgnoreCase(text,
                "scene", "场景", "测试关卡");
            bool hasSceneBuilder = hasSceneContext && ContainsAnyIgnoreCase(text,
                "builder", "构建器", "重建", "构建", "生成场景", "build scene");
            bool hasPrefabOverride = hasSceneContext
                && ContainsAnyIgnoreCase(text, "prefab", "预制体")
                && ContainsAnyIgnoreCase(text, "override", "覆盖");
            bool hasSceneFixture = hasSceneContext
                && ContainsAnyIgnoreCase(text, "fixture", "夹具");
            bool hasSceneLayout = hasSceneContext
                && ContainsAnyIgnoreCase(text, "layout", "布局", "位置", "锚点");
            bool hasSceneBackup = hasSceneContext
                && ContainsAnyIgnoreCase(text, "backup", "备份", "回滚副本");
            bool hasBackupManifest = hasSceneBackup
                && ContainsAnyIgnoreCase(text, "manifest", "清单", "哈希", "hash");
            bool hasSceneGuide = hasSceneContext
                && ContainsAnyIgnoreCase(text, "scene guide", "guide", "导视", "诊断");
            bool hasSceneAcceptance = hasSceneContext && ContainsAnyIgnoreCase(text,
                "验收", "acceptance", "验证", "validation", "检查");
            bool hasSceneRelease = hasSceneContext
                && ContainsAnyIgnoreCase(text, "发布", "release");
            bool hasSceneReceipt = hasSceneContext
                && ContainsAnyIgnoreCase(text, "receipt", "回执", "运行记录");
            bool hasSceneProfiler = hasSceneContext
                && ContainsAnyIgnoreCase(text, "profiler", "性能采样");
            bool hasSceneEvidence = hasSceneContext && ContainsAnyIgnoreCase(text,
                "证据", "evidence", "receipt", "回执", "profiler", "验收", "acceptance");
            bool hasUiPrefab = hasUiContext
                && ContainsAnyIgnoreCase(text, "prefab", "预制体");
            bool hasUiFixture = hasUiContext
                && ContainsAnyIgnoreCase(text, "fixture", "夹具", "fixture scene");
            bool hasUiLayout = hasUiContext
                && ContainsAnyIgnoreCase(text, "layout", "布局", "anchor", "锚点");
            bool hasResponsiveUi = hasUiContext && ContainsAnyIgnoreCase(text,
                "responsive", "响应式", "safe area", "安全区", "分辨率", "adaptive");
            bool hasUiVisualQa = hasUiContext && ContainsAnyIgnoreCase(text,
                "visual", "视觉", "截图", "screenshot", "gpu", "像素");
            bool hasUiVisualEvidence = hasUiVisualQa && ContainsAnyIgnoreCase(text,
                "evidence", "证据", "png", "snapshot", "快照", "capture", "捕获", "验收", "acceptance");
            bool hasUiAssetFallback = hasUiContext && ContainsAnyIgnoreCase(text,
                "asset fallback", "素材 fallback", "素材降级", "素材缺失", "占位素材");
            bool hasManagedAllocationQuestion = ContainsAnyIgnoreCase(text,
                "托管分配", "managed allocation", "gc alloc", "gc allocation",
                "会不会 gc", "是否 gc", "产生 gc", "分配误报", "allocation false positive");
            bool hasManagedAllocationSyntax = ContainsAnyIgnoreCase(text,
                "boxing", "装箱", "closure", "闭包", "lambda", "delegate", "委托",
                "foreach", "iterator", "迭代器", "yield", "async", "await", "linq");
            bool hasAllocationConcern = ContainsAnyIgnoreCase(text,
                "分配", "alloc", "gc", "垃圾回收", "0 gc", "零 gc", "零gc", "热路径");
            bool hasManagedAllocationContext = hasManagedAllocationQuestion
                || (hasManagedAllocationSyntax && hasAllocationConcern);
            bool hasMemoryCapacityContext = ContainsAnyIgnoreCase(text,
                "内存预算", "memory budget", "驻留内存", "resident memory", "retained memory",
                "容量预算", "capacity budget", "高水位", "high-water", "high water",
                "池大小", "pool size", "缓存大小", "cache size", "池化成本", "gc tradeoff",
                "memory profiler")
                || (ContainsAnyIgnoreCase(text, "对象池", "pool", "缓存", "cache")
                    && ContainsAnyIgnoreCase(text,
                        "容量", "大小", "内存", "驻留", "保留", "峰值", "trim", "缩容"));

            var inferred = new List<string>();
            if (hasAbcdDynamicModeSignal)
            {
                inferred.AddRange(new[]
                {
                    "agent-mechanism-replication"
                });
            }
            if (hasSkillUnderstandingRefreshSignal)
            {
                inferred.AddRange(new[]
                {
                    "understanding-drift",
                    "skill-understanding-refresh",
                    "capability-refresh",
                    "incremental-discovery"
                });
            }
            if (hasReadConsistencyContext)
                inferred.Add("consistency");
            if (hasStableGraphContext)
                inferred.AddRange(new[] { "graph", "stable-graph-v2" });
            if (hasGraphIdentity) inferred.Add("graph-identity");
            if (hasGraphUndo)
                inferred.AddRange(new[] { "graph-undo", "rollback" });
            if (hasGraphMigration) inferred.Add("graph-migration");
            if (hasEdgeOrder) inferred.Add("edge-order");
            if (hasGraphSnapshot) inferred.Add("graph-snapshot");
            if (hasGraphBake) inferred.Add("graph-bake");
            if (hasLegacyGraph) inferred.Add("legacy-graph");
            if (hasStorySnapshotContext) inferred.Add("story");
            if (hasExecutionGraphContext) inferred.Add("agent-execution-graph");
            if (hasTaskContractContext) inferred.Add("task-contract");
            if (hasRunRecordContext) inferred.Add("automation-run-record");
            if (hasFeishuContext)
                inferred.AddRange(new[] { "feishu", "lark", "external-adapter" });
            if (hasTaskMonitor) inferred.Add("task-monitor");
            if (hasTaskDispatch)
                inferred.AddRange(new[] { "task-dispatch", "virtual-team" });
            if (hasTaskTransition) inferred.Add("task-transition");
            if (hasIdentityClaim)
                inferred.AddRange(new[] { "identity-claim", "bot-ownership", "onboarding" });
            if (hasMessageSend)
                inferred.AddRange(new[] { "message-send", "notification" });
            if (hasUnityLifecycleContext) inferred.Add("unity");
            if (hasMonoBehaviourLifecycle)
                inferred.AddRange(new[] { "monobehaviour", "lifecycle" });
            if (hasStaticState) inferred.Add("static-state");
            if (hasDomainReload) inferred.Add("domain-reload");
            if (hasSceneReload) inferred.Add("scene-reload");
            if (hasEnterPlayMode) inferred.Add("enter-play-mode");
            if (hasScriptExecutionOrder) inferred.Add("script-execution-order");
            if (hasExecuteAlwaysSignal) inferred.Add("execute-always");
            if (hasExecuteInEditMode) inferred.Add("execute-in-edit-mode");
            if (hasEditModeExecution) inferred.Add("edit-mode");
            if (hasPrefabStageContext)
                inferred.AddRange(new[] { "prefab-stage", "prefab-mode" });
            if (hasPrefabAutoSave) inferred.Add("prefab-auto-save");
            if (hasApplicationIsPlayingObject)
                inferred.AddRange(new[] { "application-is-playing", "playing-world" });
            if (hasEditorWindowContext)
                inferred.AddRange(new[] { "editor", "editor-window", "owner-lifecycle" });
            if (hasUnityCompile) inferred.Add("compile");
            if (hasPlayerEvidence)
                inferred.AddRange(new[] { "player", "evidence" });
            if (hasKccMotionContext)
                inferred.AddRange(new[] { "entity", "motion", "kcc", "character-controller" });
            if (hasKccGrounding) inferred.Add("grounding");
            if (hasKccMovingPlatform) inferred.Add("moving-platform");
            if (hasKccTeleport) inferred.Add("teleport");
            if (hasKccMotionInfluence) inferred.Add("motion-influence");
            if (hasKccVelocity) inferred.Add("velocity");
            if (hasVehicleMotionContext)
                inferred.AddRange(new[] { "vehicle", "motion" });
            if (hasMountContext)
                inferred.AddRange(new[] { "mount", "rider" });
            if (hasDriverContext)
                inferred.AddRange(new[] { "driver", "input" });
            if (hasPhysicsContext)
                inferred.AddRange(new[] { "unity", "physics-3d" });
            if (hasFixedUpdateContext)
                inferred.AddRange(new[] { "fixed-update", "fixed-step" });
            if (hasRigidbodyContext) inferred.Add("rigidbody");
            if (hasRigidbodyContext && ContainsAnyIgnoreCase(text, "kinematic", "运动学刚体"))
                inferred.Add("kinematic-rigidbody");
            if (hasColliderContext) inferred.Add("collider");
            if (hasTriggerContext) inferred.Add("trigger");
            if (hasPhysicsQueryContext) inferred.Add("physics-query");
            if (hasPhysicsQueryContext && ContainsAnyIgnoreCase(text,
                    "raycast", "ray cast", "射线检测", "射线查询"))
                inferred.Add("raycast");
            if (hasPhysicsQueryContext && ContainsAnyIgnoreCase(text,
                    "spherecast", "sphere cast", "capsulecast", "capsule cast", "boxcast", "box cast",
                    "球形投射", "胶囊投射", "盒体投射"))
                inferred.Add("cast");
            if (hasPhysicsQueryContext && ContainsAnyIgnoreCase(text,
                    "overlapsphere", "overlapbox", "overlapcapsule", "physics.overlap",
                    "物理重叠", "重叠检测", "重叠查询"))
                inferred.Add("overlap");
            if (hasLayerMaskContext) inferred.Add("layer-mask");
            if (hasQueryTriggerContext) inferred.Add("query-trigger");
            if (hasTransformSyncContext) inferred.Add("transform-sync");
            if (hasInterpolationContext) inferred.Add("interpolation");
            if (hasSingleWriterContext) inferred.Add("single-writer");
            if (hasSceneBuilder) inferred.Add("scene-builder");
            if (hasPrefabOverride) inferred.Add("prefab-override");
            if (hasSceneFixture) inferred.Add("scene-fixture");
            if (hasSceneLayout) inferred.Add("scene-layout");
            if (hasSceneBackup) inferred.Add("scene-backup");
            if (hasBackupManifest) inferred.Add("backup-manifest");
            if (hasSceneGuide) inferred.Add("scene-guide");
            if (hasSceneGuide || hasSceneAcceptance || hasSceneRelease
                || hasSceneReceipt || hasSceneProfiler)
                inferred.Add("scene-validation");
            if (hasSceneAcceptance) inferred.Add("acceptance");
            if (hasSceneRelease) inferred.Add("release");
            if (hasSceneEvidence) inferred.Add("evidence");
            if (hasSceneReceipt) inferred.Add("receipt");
            if (hasSceneProfiler) inferred.Add("profiler");
            if (hasUiContext) inferred.Add("ui-automation");
            if (hasUiPlayerGoal)
                inferred.AddRange(new[] { "player-intent", "player-goal", "intent-spec", "primary-action" });
            if (hasUiIntentClarification) inferred.Add("ui-intent-clarification");
            if (hasUiBusinessBridge) inferred.Add("business-bridge");
            if (hasScreenSpecContext) inferred.Add("screen-spec-v3");
            if (hasUiPrefab) inferred.Add("ui-prefab");
            if (hasUiFixture) inferred.Add("ui-fixture-scene");
            if (hasUiPrefab || hasUiFixture || hasUiMaterializer) inferred.Add("materializer");
            if (hasUiLayout) inferred.Add("ui-layout");
            if (hasResponsiveUi) inferred.AddRange(new[] { "responsive", "ui-responsive" });
            if (hasUiVisualQa) inferred.Add("visual-qa");
            if (hasUiVisualEvidence) inferred.Add("visual-evidence");
            if (hasUiAssetFallback) inferred.Add("asset-fallback");
            if (hasUiReferenceEvidence)
                inferred.AddRange(new[] { "ui-reference-evidence", "design-evidence", "reference-image",
                    "reference-provenance", "source-region", "vision-review", "observation-assumption" });
            if (hasUiAssetManifest)
                inferred.AddRange(new[] { "ui-asset-manifest", "asset-manifest", "asset-provenance",
                    "asset-license", "asset-fallback", "sprite-atlas", "crop-policy", "asset-resolver" });
            if (hasUiBehaviorSpec)
                inferred.AddRange(new[] { "ui-behavior-spec", "behavior-spec", "ui-binding",
                    "ui-interaction-intent", "ui-focus", "ui-navigation", "input-modality", "input-system-ui" });
            if (hasUiTextResilience)
                inferred.AddRange(new[] { "ui-text-resilience", "ui-localization", "long-content",
                    "text-wrapping", "bidi", "rtl", "glyph-coverage", "font-fallback", "line-breaking" });
            if (hasGameUiScreenFamilyContext)
                inferred.AddRange(new[] { "game-ui-screen-family", "commercial-ui", "ui-information-architecture" });
            if (hasHudUi) inferred.Add("hud-ui");
            if (hasInventoryUi) inferred.Add("inventory-ui");
            if (hasShopUi) inferred.Add("shop-ui");
            if (hasDialogueUi) inferred.Add("dialogue-ui");
            if (hasMapUi) inferred.Add("map-ui");
            if (hasProgressionUi) inferred.Add("progression-ui");
            if (hasResultUi) inferred.Add("result-ui");
            if (hasSettingsUi) inferred.Add("settings-ui");
            if (hasUiVisualDesignContext)
                inferred.AddRange(new[] { "ui-visual-design", "visual-design" });
            if (hasUiDesignToken) inferred.Add("design-token");
            if (hasUiColorDesign) inferred.Add("color-role");
            if (hasUiTypographyDesign) inferred.Add("typography-role");
            if (hasUiSpacingDesign) inferred.Add("spacing-token");
            if (hasUiHierarchyDesign) inferred.Add("visual-hierarchy");
            if (hasUiDensityDesign) inferred.Add("information-density");
            if (hasUiRarityDesign) inferred.Add("rarity-visual");
            if (hasUiMaterialDesign) inferred.Add("ui-material");
            if (hasManagedAllocationContext)
                inferred.AddRange(new[] { "performance", "managed-allocation", "allocation-static-audit" });
            if (hasManagedAllocationContext && ContainsAnyIgnoreCase(text, "boxing", "装箱"))
                inferred.Add("boxing");
            if (hasManagedAllocationContext && ContainsAnyIgnoreCase(text, "closure", "闭包", "lambda"))
                inferred.Add("closure");
            if (hasManagedAllocationContext && ContainsAnyIgnoreCase(text, "delegate", "委托"))
                inferred.Add("delegate");
            if (hasManagedAllocationContext && ContainsAnyIgnoreCase(text, "foreach"))
                inferred.Add("foreach");
            if (hasManagedAllocationContext && ContainsAnyIgnoreCase(text, "iterator", "迭代器"))
                inferred.Add("iterator");
            if (hasManagedAllocationContext && ContainsAnyIgnoreCase(text, "yield"))
                inferred.Add("yield");
            if (hasManagedAllocationContext && ContainsAnyIgnoreCase(text, "async", "await"))
                inferred.Add("async");
            if (hasManagedAllocationContext && ContainsAnyIgnoreCase(text, "linq"))
                inferred.Add("linq");
            if (hasManagedAllocationContext && ContainsAnyIgnoreCase(text, "误报", "false positive"))
                inferred.Add("false-positive");
            if (hasMemoryCapacityContext)
                inferred.AddRange(new[] { "performance", "memory-budget", "capacity-budget" });
            if (hasMemoryCapacityContext && ContainsAnyIgnoreCase(text,
                    "驻留", "resident memory", "retained memory", "保留"))
                inferred.Add("resident-memory");
            if (hasMemoryCapacityContext && ContainsAnyIgnoreCase(text,
                    "高水位", "high-water", "high water", "峰值"))
                inferred.Add("high-water-mark");
            if (hasMemoryCapacityContext && ContainsAnyIgnoreCase(text, "对象池", "pool", "池大小"))
                inferred.AddRange(new[] { "pool", "pool-size" });
            if (hasMemoryCapacityContext && ContainsAnyIgnoreCase(text, "缓存", "cache", "缓存大小"))
                inferred.Add("cache-size");
            if (hasMemoryCapacityContext && ContainsAnyIgnoreCase(text, "trim", "缩容"))
                inferred.Add("trim");
            if (hasMemoryCapacityContext && ContainsAnyIgnoreCase(text, "memory profiler"))
                inferred.Add("memory-profiler");
            if (hasMemoryCapacityContext && ContainsAnyIgnoreCase(text,
                    "池化成本", "gc tradeoff", "内存换 gc", "内存换gc"))
                inferred.Add("gc-tradeoff");
            return inferred.Distinct(StringComparer.Ordinal).ToList();
        }

        private static bool ContainsAnyIgnoreCase(string text, params string[] values)
        {
            return (values ?? Array.Empty<string>())
                .Any(value => ContainsIgnoreCase(text, value));
        }

        private static void ApplyAiwarningsMigrationSafetyGate(ESAIBrainPlan plan,
            string objective, IReadOnlyCollection<string> routeKeys,
            bool matchedPreservingRoute)
        {
            // This is a plan-only gate: it never moves, deletes, renames, or overwrites files.
            bool hasWarningKnowledgeTarget = ContainsAnyIgnoreCase(objective,
                    "警告", "warning", "warnings")
                && ContainsAnyIgnoreCase(objective, "知识库", "Knowledge", "AIKnowledge");
            bool hasAiwarningsTarget = ContainsAnyIgnoreCase(objective, "AIWarnings", "AIWarning")
                || hasWarningKnowledgeTarget
                || (routeKeys?.Contains("es.aiwarnings.full-coverage", StringComparer.Ordinal) ?? false)
                || matchedPreservingRoute;
            if (!hasAiwarningsTarget)
                return;

            bool hasTransferIntent = matchedPreservingRoute
                || (routeKeys?.Contains("es.aiwarnings.full-coverage", StringComparer.Ordinal) ?? false)
                || ContainsAnyIgnoreCase(objective,
                    "迁移", "迁入", "迁出", "迁往", "转移", "转入", "搬迁",
                    "移动", "移到", "移走", "搬到", "挪到", "导入", "导出", "抽离",
                    "migration", "transfer", "move", "relocate", "projection", "投影", "聚合", "外置");
            if (!hasTransferIntent)
                return;

            bool hasUnnegatedDestructiveAction = ContainsUnnegatedAnyIgnoreCase(objective,
                "移动", "移到", "移走", "搬到", "挪到", "转移", "搬迁", "迁出",
                "删除", "清空", "清除", "移除", "抹掉", "擦除", "销毁", "覆盖", "替换", "重写", "改写",
                "move", "relocate", "delete", "remove", "erase", "purge", "destroy", "overwrite", "replace", "rewrite");
            if (hasUnnegatedDestructiveAction)
            {
                AddAiwarningsMigrationBlocker(plan, "[AIWARNINGS.MIGRATION_DESTRUCTIVE_ACTION_DENIED] "
                    + "AIWarnings 到 AIKnowledge 的迁移意图包含未被否定的移动、删除、覆盖或替换操作；"
                    + "必须改为保留源 Warning 的非破坏性投影，并重新提交明确目标。");
                return;
            }

            bool hasSourceRetention = ContainsAnyIgnoreCase(objective,
                "保留全部 AIWarnings", "保留 AIWarnings", "保留原 Warning", "保留源 Warning",
                "保留全部警告", "保留原警告", "保留源警告",
                "保留原文件", "保留原路径", "保留权威", "源文件不变", "原始文件不变", "留存",
                "权威性", "preserve source", "preserve source warnings", "source remains unchanged",
                "do not alter source", "keep source");
            bool hasNoDelete = ContainsAnyIgnoreCase(objective, "不删除", "禁止删除");
            bool hasNoMove = ContainsAnyIgnoreCase(objective, "不移动", "禁止移动");
            bool hasNoRename = ContainsAnyIgnoreCase(objective, "不重命名", "禁止重命名");
            bool hasNoOverwrite = ContainsAnyIgnoreCase(objective, "不覆盖", "禁止覆盖");
            bool hasDestructiveProhibition = ContainsAnyIgnoreCase(objective,
                "源文件不变", "保持源不变", "不可变源", "非破坏性", "non-destructive", "immutable source")
                || (hasNoDelete && hasNoMove && hasNoRename && hasNoOverwrite);
            if (!hasSourceRetention || !hasDestructiveProhibition)
            {
                AddAiwarningsMigrationBlocker(plan, "[AIWARNINGS.MIGRATION_SOURCE_PRESERVATION_REQUIRED] "
                    + "AIWarnings 到 AIKnowledge 的迁移必须明确保留原 Warning、原路径和权威性，"
                    + "并禁止删除、移动、重命名或覆盖源文件。");
            }
        }

        private static void AddAiwarningsMigrationBlocker(ESAIBrainPlan plan, string blocker)
        {
            if (plan == null || string.IsNullOrWhiteSpace(blocker)
                || plan.blockers.Any(item => string.Equals(item, blocker, StringComparison.Ordinal)))
                return;
            plan.blockers.Add(blocker);
        }

        private static bool ContainsUnnegatedAnyIgnoreCase(string text, params string[] values)
        {
            string source = text ?? string.Empty;
            string[] negations =
            {
                "禁止", "不得", "不允许", "不应", "不要", "不能", "严禁", "避免",
                "不删除", "不移动", "不重命名", "不覆盖", "非破坏性", "源文件不变", "保持源不变",
                "not", "do not", "must not", "without", "non-destructive", "preserve", "keep",
            };

            foreach (string value in values ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                int offset = 0;
                while (offset < source.Length)
                {
                    int index = source.IndexOf(value, offset, StringComparison.OrdinalIgnoreCase);
                    if (index < 0)
                        break;

                    int prefixStart = Math.Max(0, index - 16);
                    string prefix = source.Substring(prefixStart, index - prefixStart);
                    if (!ContainsAnyIgnoreCase(prefix, negations))
                        return true;

                    offset = index + Math.Max(1, value.Length);
                }
            }

            return false;
        }

        private static bool IsCompleteProjectSkill(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !Regex.IsMatch(name, "^[a-z0-9][a-z0-9-]*$"))
                return false;
            if (!TryResolveProjectPath(ProjectSkillsRoot + "/" + name, out string root, out _))
                return false;
            return File.Exists(Path.Combine(root, "SKILL.md"))
                && File.Exists(Path.Combine(root, "agents", "openai.yaml"));
        }

        private static bool TryValidateSkillContract(string skillName, string skillText,
            string metadataText, out string error)
        {
            error = string.Empty;
            Match frontmatter = Regex.Match(skillText ?? string.Empty,
                "(?ms)^---\\s*\\r?\\nname:\\s*(?<name>[a-z0-9-]+)\\s*\\r?\\ndescription:\\s*(?<description>[^\\r\\n]+)\\s*\\r?\\n---\\s*$",
                RegexOptions.CultureInvariant);
            if (!frontmatter.Success
                || !string.Equals(frontmatter.Groups["name"].Value, skillName, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(frontmatter.Groups["description"].Value))
            {
                error = "SKILL.md frontmatter 必须声明与目录一致的 name 和非空 description。";
                return false;
            }
            if ((skillText ?? string.Empty).IndexOf("[TODO", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                error = "SKILL.md 仍包含初始化模板 TODO，不能作为正式能力。";
                return false;
            }

            string metadata = metadataText ?? string.Empty;
            string[] requiredFields = { "display_name", "short_description", "default_prompt" };
            foreach (string field in requiredFields)
            {
                if (!Regex.IsMatch(metadata, "(?m)^\\s*" + field + "\\s*:\\s*['\"`]?[^\\r\\n]+",
                        RegexOptions.CultureInvariant))
                {
                    error = "agents/openai.yaml 缺少非空字段：" + field;
                    return false;
                }
            }
            return true;
        }

        private static bool TryReadSkillCatalogRecord(string skillName, string skillHash,
            string governanceHash, out ESAIBrainSkillCatalogRecord record, out string error)
        {
            record = null;
            error = string.Empty;
            if (!TryResolveProjectPath(ProjectSkillCatalogPath, out string catalogPath, out string pathError)
                || !File.Exists(catalogPath))
            {
                error = "Skill Catalog 不存在：" + (string.IsNullOrWhiteSpace(pathError)
                    ? ProjectSkillCatalogPath : pathError);
                return false;
            }
            if (!TryReadTextAndHash(catalogPath, out string catalogText, out _, out error))
                return false;

            string[] lines = (catalogText ?? string.Empty).Replace("\r\n", "\n")
                .Replace('\r', '\n').Split('\n');
            int matchCount = 0;
            string block = string.Empty;
            for (int i = 0; i < lines.Length; i++)
            {
                Match header = Regex.Match(lines[i] ?? string.Empty,
                    "^  (?<name>[a-z0-9][a-z0-9-]*):\\s*$", RegexOptions.CultureInvariant);
                if (!header.Success || !string.Equals(header.Groups["name"].Value,
                        skillName, StringComparison.OrdinalIgnoreCase))
                    continue;
                matchCount++;
                int end = i + 1;
                while (end < lines.Length && !Regex.IsMatch(lines[end] ?? string.Empty,
                        "^  [a-z0-9][a-z0-9-]*:\\s*$", RegexOptions.CultureInvariant)) end++;
                block = string.Join("\n", lines.Skip(i).Take(end - i));
            }
            if (matchCount != 1)
            {
                error = matchCount == 0 ? "Catalog 缺少唯一记录。" : "Catalog 存在重复记录。";
                return false;
            }

            string declaredSkillHash = ReadCatalogScalar(block, "skillHash");
            string declaredGovernanceHash = ReadCatalogScalar(block, "governanceHash");
            string family = ReadCatalogScalar(block, "family");
            string registrationState = ReadCatalogScalar(block, "registrationState");
            if (!Regex.IsMatch(declaredSkillHash ?? string.Empty, "^[a-fA-F0-9]{64}$")
                || !string.Equals(declaredSkillHash, skillHash, StringComparison.OrdinalIgnoreCase))
            {
                error = "Catalog skillHash 与当前 SKILL.md 不一致。";
                return false;
            }
            if (!Regex.IsMatch(declaredGovernanceHash ?? string.Empty, "^[a-fA-F0-9]{64}$")
                || !string.Equals(declaredGovernanceHash, governanceHash, StringComparison.OrdinalIgnoreCase))
            {
                error = "Catalog governanceHash 与当前 governance.json 不一致。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(family) || string.IsNullOrWhiteSpace(registrationState))
            {
                error = "Catalog 记录缺少 family 或 registrationState。";
                return false;
            }
            record = new ESAIBrainSkillCatalogRecord
            {
                family = family,
                registrationState = registrationState,
            };
            return true;
        }

        private static string ReadCatalogScalar(string block, string key)
        {
            Match match = Regex.Match(block ?? string.Empty,
                "(?m)^\\s+" + Regex.Escape(key) + ":\\s*(?<value>[^\\r\\n]+)",
                RegexOptions.CultureInvariant);
            return match.Success ? match.Groups["value"].Value.Trim().Trim('"', '\'') : string.Empty;
        }

        private static bool TryResolveSkillEligibility(string maturity, string delivery,
            string registrationState, out SkillEligibility eligibility, out string error)
        {
            eligibility = null;
            error = string.Empty;
            if (!TryResolveProjectPath(ProjectSkillDiscoveryPolicyPath, out string policyPath,
                    out string pathError) || !File.Exists(policyPath))
            {
                error = string.IsNullOrWhiteSpace(pathError)
                    ? "发现策略文件不存在：" + ProjectSkillDiscoveryPolicyPath : pathError;
                return false;
            }

            JObject policy;
            try
            {
                policy = JObject.Parse(File.ReadAllText(policyPath, StrictUtf8));
            }
            catch (Exception exception)
            {
                error = "发现策略不是合法 JSON：" + exception.Message;
                return false;
            }

            if (policy.Value<int?>("schemaVersion") != 1)
            {
                error = "发现策略 schemaVersion 必须为 1。";
                return false;
            }

            JObject states = policy["states"] as JObject;
            JObject state = states == null ? null : states[maturity] as JObject;
            if (state == null)
            {
                error = "maturity 未在发现策略中注册：" + maturity;
                return false;
            }

            string discoveryState = state.Value<string>("discoveryState")?.Trim() ?? string.Empty;
            string planEligibility = state.Value<string>("planEligibility")?.Trim() ?? string.Empty;
            string runtimeEligibility = state.Value<string>("runtimeEligibility")?.Trim() ?? string.Empty;
            JObject deliveryOverrides = policy["deliveryOverrides"] as JObject;
            JObject deliveryOverride = deliveryOverrides == null ? null : deliveryOverrides[delivery] as JObject;
            if (deliveryOverride != null)
            {
                discoveryState = deliveryOverride.Value<string>("discoveryState")?.Trim() ?? discoveryState;
                planEligibility = deliveryOverride.Value<string>("planEligibility")?.Trim() ?? planEligibility;
                runtimeEligibility = deliveryOverride.Value<string>("runtimeEligibility")?.Trim() ?? runtimeEligibility;
            }

            JObject registrationOverrides = policy["registrationOverrides"] as JObject;
            JObject registrationOverride = registrationOverrides == null
                ? null : registrationOverrides[registrationState] as JObject;
            bool reviewRequired = registrationOverride == null
                || registrationOverride.Value<bool?>("reviewRequired") != false;
            if (string.IsNullOrWhiteSpace(discoveryState)
                || string.IsNullOrWhiteSpace(planEligibility)
                || string.IsNullOrWhiteSpace(runtimeEligibility))
            {
                error = "发现策略结果缺少 discoveryState、planEligibility 或 runtimeEligibility。";
                return false;
            }

            eligibility = new SkillEligibility
            {
                discoveryState = discoveryState,
                planEligibility = planEligibility,
                runtimeEligibility = runtimeEligibility,
                reviewRequired = reviewRequired,
            };
            return true;
        }

        private static bool TryReadSkillGovernanceMetadata(string skillRoot, string skillName,
            out ESAIBrainSkillGovernanceMetadata metadata, out string governanceHash,
            out string error)
        {
            metadata = null;
            governanceHash = string.Empty;
            error = string.Empty;
            string path = Path.Combine(skillRoot, SkillGovernanceMetadataFileName);
            if (!File.Exists(path))
                return true;

            if (!TryReadTextAndHash(path, out string text, out governanceHash, out error))
                return false;

            JObject json;
            try
            {
                json = JObject.Parse(text);
            }
            catch (Exception exception)
            {
                error = "governance.json 不是合法 JSON：" + exception.Message;
                return false;
            }

            int? schemaVersion = json.Value<int?>("schemaVersion");
            if (schemaVersion != 1)
            {
                error = "schemaVersion 必须为 1。";
                return false;
            }

            string declaredName = json.Value<string>("skillName")?.Trim() ?? string.Empty;
            if (!string.Equals(declaredName, skillName, StringComparison.Ordinal))
            {
                error = "skillName 与 Skill 目录不一致。";
                return false;
            }

            string tier = json.Value<string>("tier")?.Trim() ?? string.Empty;
            string maturity = json.Value<string>("maturity")?.Trim() ?? string.Empty;
            string delivery = json.Value<string>("delivery")?.Trim() ?? string.Empty;
            string evidenceLevel = json.Value<string>("evidenceLevel")?.Trim() ?? string.Empty;
            string riskClass = json.Value<string>("riskClass")?.Trim() ?? string.Empty;
            string authorityClass = json.Value<string>("authorityClass")?.Trim() ?? string.Empty;
            string owner = json.Value<string>("owner")?.Trim() ?? string.Empty;
            string acceptanceOwner = json.Value<string>("acceptanceOwner")?.Trim() ?? string.Empty;
            string executionMode = json.Value<string>("executionMode")?.Trim() ?? string.Empty;
            string writePolicy = json.Value<string>("writePolicy")?.Trim() ?? string.Empty;
            bool? requiresBrainPlan = json.Value<bool?>("requiresBrainPlan");
            bool? allowDirectExecution = json.Value<bool?>("allowDirectExecution");

            if (!IsOneOf(tier, "SmallTool", "Workflow", "Engineering")
                || !IsOneOf(maturity, "Proposed", "Scaffolded", "Implementing", "Integrating",
                    "Verifying", "Stable", "Deprecated", "Archived")
                || !IsOneOf(delivery, "Designed", "Implemented-Unverified", "Blocked", "Failed",
                    "Accepted", "Released")
                || !Regex.IsMatch(evidenceLevel, "^S[0-6]$", RegexOptions.CultureInvariant)
                || !Regex.IsMatch(riskClass, "^[a-z0-9][a-z0-9-]*$", RegexOptions.CultureInvariant)
                || !IsOneOf(authorityClass, "standard", "core-governed", "project-gate")
                || string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(acceptanceOwner)
                || !Regex.IsMatch(executionMode, "^[a-z0-9][a-z0-9-]*$", RegexOptions.CultureInvariant)
                || !Regex.IsMatch(writePolicy, "^[a-z0-9][a-z0-9-]*$", RegexOptions.CultureInvariant)
                || !requiresBrainPlan.HasValue || !allowDirectExecution.HasValue)
            {
                error = "治理元数据的状态、风险、执行模式或布尔字段无效。";
                return false;
            }
            if (allowDirectExecution.Value)
            {
                error = "项目 Skill 不允许声明 allowDirectExecution=true。";
                return false;
            }
            if (!requiresBrainPlan.Value && !string.Equals(authorityClass, "standard", StringComparison.Ordinal))
            {
                error = "core-governed/project-gate Skill 必须要求 AIBrain 计划。";
                return false;
            }
            if (string.Equals(authorityClass, "project-gate", StringComparison.Ordinal)
                && !Regex.IsMatch(evidenceLevel, "^S[2-6]$", RegexOptions.CultureInvariant))
            {
                error = "project-gate Skill 至少需要 S2 治理证据。";
                return false;
            }

            metadata = new ESAIBrainSkillGovernanceMetadata
            {
                schemaVersion = schemaVersion.Value,
                skillName = declaredName,
                tier = tier,
                maturity = maturity,
                delivery = delivery,
                evidenceLevel = evidenceLevel,
                riskClass = riskClass,
                authorityClass = authorityClass,
                owner = owner,
                acceptanceOwner = acceptanceOwner,
                executionMode = executionMode,
                requiresBrainPlan = requiresBrainPlan.Value,
                allowDirectExecution = allowDirectExecution.Value,
                writePolicy = writePolicy,
            };
            return true;
        }

        private static bool IsOneOf(string value, params string[] allowed)
        {
            return allowed != null && allowed.Any(item => string.Equals(item, value, StringComparison.Ordinal));
        }

        private static bool ContainsIgnoreCase(string text, string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && (text ?? string.Empty).IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ResolveProjectPath(string projectRelativePath)
        {
            if (string.IsNullOrWhiteSpace(projectRelativePath) || Path.IsPathRooted(projectRelativePath))
                throw new InvalidDataException("只允许项目相对路径：" + projectRelativePath);
            string normalized = projectRelativePath.Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            string root = ESCommandPalettePathPolicy.ProjectRoot.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            string full = Path.GetFullPath(Path.Combine(root, normalized));
            if (!IsSameOrChildPath(root, full))
                throw new InvalidDataException("项目路径越界：" + projectRelativePath);
            return full;
        }

        private static bool TryResolveProjectPath(string projectRelativePath, out string fullPath,
            out string error)
        {
            fullPath = string.Empty;
            error = string.Empty;
            try
            {
                fullPath = ResolveProjectPath(projectRelativePath);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool IsSameOrChildPath(string root, string candidate)
        {
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            return string.Equals(normalizedRoot, candidate, StringComparison.OrdinalIgnoreCase)
                || candidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static string ToProjectRelative(string fullPath)
        {
            string root = ESCommandPalettePathPolicy.ProjectRoot.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(root.Length).Replace('\\', '/') : fullPath;
        }

        private static bool TryReadTextAndHash(string path, out string text, out string hash, out string error)
        {
            text = string.Empty;
            hash = string.Empty;
            error = string.Empty;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                text = StrictUtf8.GetString(bytes);
                using (SHA256 sha = SHA256.Create())
                    hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static string ComputeStableHashSetHash(IEnumerable<string> hashes)
        {
            string canonical = string.Join("\n", (hashes ?? Enumerable.Empty<string>())
                .Where(hash => !string.IsNullOrWhiteSpace(hash))
                .Select(hash => hash.Trim().ToLowerInvariant())
                .OrderBy(hash => hash, StringComparer.Ordinal));
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(StrictUtf8.GetBytes(canonical)))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string ComputeConcatenatedHashSetHash(IEnumerable<string> hashes)
        {
            string canonical = string.Concat((hashes ?? Enumerable.Empty<string>())
                .Where(hash => !string.IsNullOrWhiteSpace(hash))
                .Select(hash => hash.Trim().ToLowerInvariant())
                .OrderBy(hash => hash, StringComparer.Ordinal));
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(StrictUtf8.GetBytes(canonical)))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static bool MatchesKnowledgeSourceSetHash(string expected,
            IEnumerable<KnowledgeSourceReference> sourceRefs,
            IEnumerable<string> rawHashes, IEnumerable<string> normalizedHashes)
        {
            if (string.IsNullOrWhiteSpace(expected)) return false;
            string normalizedExpected = expected.Trim().ToLowerInvariant();
            string[] candidates =
            {
                ComputeKnowledgeSourceSetHash(sourceRefs),
                // ContentHash is the compatibility hash over the declared,
                // validated SourceRef hashes. This is also the effective hash
                // when a text source is represented by its normalized form.
                ComputeConcatenatedHashSetHash((sourceRefs ?? Enumerable.Empty<KnowledgeSourceReference>())
                    .Select(sourceRef => sourceRef?.sha256 ?? string.Empty)),
                ComputeStableHashSetHash(rawHashes),
                ComputeConcatenatedHashSetHash(rawHashes),
                ComputeStableHashSetHash(normalizedHashes),
                ComputeConcatenatedHashSetHash(normalizedHashes),
            };
            return candidates.Any(candidate => string.Equals(candidate, normalizedExpected,
                StringComparison.OrdinalIgnoreCase));
        }

        // Knowledge refresh plans use a path-bound canonical record so that two
        // different SourceRef sets cannot accidentally share the same content hash.
        private static string ComputeKnowledgeSourceSetHash(
            IEnumerable<KnowledgeSourceReference> sourceRefs)
        {
            string[] records = (sourceRefs ?? Enumerable.Empty<KnowledgeSourceReference>())
                .Select(sourceRef => CanonicalSourceSetRecord(
                    sourceRef?.path ?? string.Empty, sourceRef?.sha256 ?? string.Empty))
                .OrderBy(record => record, StringComparer.Ordinal)
                .ToArray();
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(
                    StrictUtf8.GetBytes(string.Join("\n", records))))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string CanonicalSourceSetRecord(string path, string hash)
        {
            return CanonicalSourceSetField("source-set") + "|"
                + CanonicalSourceSetField(path) + "|"
                + CanonicalSourceSetField(hash);
        }

        private static string CanonicalSourceSetField(string value)
        {
            string text = value ?? string.Empty;
            return text.Length + ":" + text;
        }

        private static bool TryReadNormalizedTextHash(string path, out string hash, out string error)
        {
            hash = string.Empty;
            error = string.Empty;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                string text = StrictUtf8.GetString(bytes)
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n");
                using (SHA256 sha = SHA256.Create())
                    hash = BitConverter.ToString(sha.ComputeHash(StrictUtf8.GetBytes(text)))
                        .Replace("-", string.Empty).ToLowerInvariant();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static string ComputePlanHash(ESAIBrainPlan plan, ESAIBrainRequest request)
        {
            TryResolveAuthorizationProfile(plan, request, false,
                out AuthorizationProfile authorizationProfile, out _);
            return ComputeCanonicalSha256(JToken.FromObject(new
            {
                authorizationPolicyVersion = AuthorizationPolicyVersion,
                plan.contractVersion,
                plan.status,
                plan.objective,
                plan.knowledgeIndexHash,
                plan.routeKeys,
                blockers = plan.blockers.OrderBy(item => item, StringComparer.Ordinal),
                knowledge = plan.knowledge.OrderBy(item => item.knowledgeId, StringComparer.Ordinal)
                    .Select(item => new
                    {
                        item.knowledgeId,
                        item.file,
                        item.topic,
                        item.contentHash,
                        routeKeys = item.routeKeys.OrderBy(value => value, StringComparer.Ordinal),
                        relatedSkills = item.relatedSkills.OrderBy(value => value, StringComparer.Ordinal),
                        requiredReads = item.requiredReads.OrderBy(value => value, StringComparer.Ordinal),
                        sourceRefs = item.sourceRefs.OrderBy(value => value, StringComparer.Ordinal),
                    }),
                warnings = plan.warnings.OrderBy(item => item.projectPath, StringComparer.Ordinal)
                    .ThenBy(item => item.sha256, StringComparer.Ordinal)
                    .Select(item => new { item.projectPath, item.sha256 }),
                evidence = plan.evidence.OrderBy(item => item.projectPath, StringComparer.Ordinal)
                    .ThenBy(item => item.sha256, StringComparer.Ordinal)
                    .Select(item => new { item.projectPath, item.sha256 }),
                command = plan.command == null ? null : new
                {
                    plan.command.id,
                    plan.command.path,
                    plan.command.role,
                    plan.command.riskLevel,
                    plan.command.writeMode,
                    plan.command.catalogHash,
                    plan.command.contractHash,
                    plan.command.reference,
                },
                skills = plan.skills.OrderBy(item => item.name, StringComparer.Ordinal).Select(item => new
                {
                    item.name,
                    item.skillPath,
                    item.skillHash,
                    item.metadataHash,
                    item.governanceHash,
                    item.tier,
                    item.maturity,
                    item.delivery,
                    item.evidenceLevel,
                    item.riskClass,
                    item.authorityClass,
                    item.owner,
                    item.acceptanceOwner,
                    item.requiresBrainPlan,
                    item.allowDirectExecution,
                    item.writePolicy,
                    item.family,
                    item.registrationState,
                    item.discoveryState,
                    item.planEligibility,
                    item.runtimeEligibility,
                    item.reviewRequired,
                }),
                workflow = plan.workflow == null ? null : new
                {
                    plan.workflow.workflowId,
                    plan.workflow.contentHash,
                    plan.workflow.sourceAssetGuid,
                },
                task = plan.task == null ? null : new
                {
                    plan.task.taskId,
                    plan.task.taskVersion,
                    plan.task.displayName,
                    plan.task.category,
                    plan.task.summary,
                    plan.task.inputSchemaHash,
                    plan.task.descriptorHash,
                    plan.task.taskContractHash,
                    plan.task.allowAiInvoke,
                    plan.task.allowInPlayMode,
                    plan.task.workerId,
                    plan.task.workerType,
                    plan.task.workerVersion,
                    plan.task.workerEntrypointHash,
                    plan.task.workerEnabled,
                    capabilities = plan.task.capabilities.OrderBy(value => value, StringComparer.Ordinal),
                },
                routePlan = plan.routePlan,
                plan.authority,
                authorization = new
                {
                    authorizationProfile.authorizationClass,
                    authorizationProfile.budgetClass,
                    authorizationProfile.hostId,
                    authorizationProfile.instructionHash,
                    authorizationProfile.maxUses,
                },
                request.invocationId,
                request.preset,
                input = request.input ?? new JObject(),
                request.fromAi,
                request.dryRun,
                request.actorId,
                request.routeProfileId,
                request.goalRevisionPath,
                executionSnapshot = request.executionSnapshot == null ? null : new
                {
                    request.executionSnapshot.snapshotId,
                    request.executionSnapshot.inputManifestHash,
                    request.executionSnapshot.sourceHash,
                    request.executionSnapshot.taskContractHash,
                    request.executionSnapshot.commandHash,
                    // The plan hash is written into the snapshot after planning;
                    // including it here would create a self-referential hash.
                    brainPlanHash = string.Empty,
                },
            }));
        }

        private static string ComputeTrustedHostRequestHash(ESAIBrainRequest request)
        {
            return ComputeCanonicalSha256(JToken.FromObject(new
            {
                authorizationPolicyVersion = AuthorizationPolicyVersion,
                request.objective,
                routeKeys = request.routeKeys ?? new List<string>(),
                request.commandId,
                skillNames = request.skillNames ?? new List<string>(),
                workflow = request.workflow == null ? null : new
                {
                    request.workflow.workflowId,
                    request.workflow.contentHash,
                    request.workflow.sourceAssetGuid,
                },
                request.taskId,
                request.taskVersion,
                request.preset,
                input = request.input ?? new JObject(),
                request.fromAi,
                request.dryRun,
                request.actorId,
                request.invocationId,
                request.routeProfileId,
                request.goalRevisionPath,
                request.userDirectedRuntime,
                request.userInstructionHash,
                executionSnapshot = request.executionSnapshot == null ? null : new
                {
                    request.executionSnapshot.snapshotId,
                    request.executionSnapshot.inputManifestHash,
                    request.executionSnapshot.sourceHash,
                    request.executionSnapshot.taskContractHash,
                    request.executionSnapshot.commandHash,
                    brainPlanHash = string.Empty,
                },
            }));
        }

        private static string ComputeCanonicalSha256(JToken value)
        {
            string canonical = CanonicalizeToken(value ?? JValue.CreateNull())
                .ToString(Formatting.None);
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(StrictUtf8.GetBytes(canonical)))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static JToken CanonicalizeToken(JToken token)
        {
            if (token is JObject obj)
            {
                var result = new JObject();
                foreach (JProperty property in obj.Properties()
                             .OrderBy(item => item.Name, StringComparer.Ordinal))
                    result.Add(property.Name, CanonicalizeToken(property.Value));
                return result;
            }
            if (token is JArray array)
            {
                var result = new JArray();
                foreach (JToken item in array) result.Add(CanonicalizeToken(item));
                return result;
            }
            return token.DeepClone();
        }

        private sealed class KnowledgeIndexEntry
        {
            public string knowledgeId = string.Empty;
            public string file = string.Empty;
            public string topic = string.Empty;
            public string contentHash = string.Empty;
            public readonly List<string> routeKeys = new List<string>();
            public readonly List<string> relatedSkills = new List<string>();
            public readonly List<string> requiredReads = new List<string>();
        }

        private sealed class KnowledgeSourceReference
        {
            public string path = string.Empty;
            public string sha256 = string.Empty;
        }

        private sealed class AuthorizationProfile
        {
            public readonly string authorizationClass;
            public readonly string budgetClass;
            public readonly string hostId;
            public readonly string instructionHash;
            public readonly int maxUses;

            public AuthorizationProfile(string authorizationClass, string budgetClass,
                string hostId, string instructionHash, int maxUses)
            {
                this.authorizationClass = authorizationClass ?? string.Empty;
                this.budgetClass = budgetClass ?? string.Empty;
                this.hostId = hostId ?? string.Empty;
                this.instructionHash = instructionHash ?? string.Empty;
                this.maxUses = maxUses;
            }

            public static AuthorizationProfile Untrusted()
            {
                return new AuthorizationProfile("Untrusted", AuthorizationBudgetHighRisk,
                    string.Empty, string.Empty, DefaultHighRiskAuthorizationUses);
            }
        }

        private sealed class AIBrainAuthorizationStore
        {
            public int schemaVersion = AuthorizationStoreSchemaVersion;
            public int authorizationPolicyVersion;
            public long revision;
            public List<string> retiredInvocationIds = new List<string>();
            public List<AIBrainAuthorizationRecord> entries = new List<AIBrainAuthorizationRecord>();
        }

        private sealed class AuthorizationStoreTransaction
        {
            public AuthorizationStoreTransaction(DateTimeOffset utcNow)
            {
                UtcNow = utcNow;
            }

            public DateTimeOffset UtcNow { get; }

            public bool TryPersistAuthorizationStore(string path,
                AIBrainAuthorizationStore store, out string error)
            {
                return ESAIBrainCoordinator.TryPersistAuthorizationStore(
                    path, store, UtcNow, out error);
            }
        }

        private sealed class AIBrainAuthorizationRecord
        {
            public string planHash = string.Empty;
            public string bindingHash = string.Empty;
            public string planId = string.Empty;
            public string invocationId = string.Empty;
            public string actorId = string.Empty;
            public string authorizationClass = string.Empty;
            public string budgetClass = string.Empty;
            public string hostId = string.Empty;
            public string instructionHash = string.Empty;
            public string status = AuthorizationStatusActive;
            public DateTimeOffset issuedAtUtc;
            public DateTimeOffset expiresAtUtc;
            public DateTimeOffset? terminalAtUtc;
            public int maxUses = 1;
            public int usedCount;
            public List<string> usedIdempotencyKeys = new List<string>();
        }

        private sealed class RouteStageRegistry
        {
            public int defaultMaxDepth;
            public int maxDepth;
            public List<string> externalInputs = new List<string>();
            public List<RouteDepthAuthorization> depthAuthorizations =
                new List<RouteDepthAuthorization>();
            public List<RouteStageDefinition> stages = new List<RouteStageDefinition>();
        }

        private sealed class RouteDepthAuthorization
        {
            public string reasonCode = string.Empty;
            public int authorizesDepth;
            public List<string> profiles = new List<string>();
            public List<string> routeKeys = new List<string>();
        }

        private sealed class RouteStageDefinition
        {
            public string stageContractId = string.Empty;
            public string skillName = string.Empty;
            public List<string> profiles = new List<string>();
            public List<string> routeKeys = new List<string>();
            public List<string> requires = new List<string>();
            public List<string> produces = new List<string>();
            public List<string> failureConditions = new List<string>();
            public string depthReasonCode = string.Empty;
        }
    }

    [Serializable]
    public sealed class ESAIBrainRequest
    {
        public string objective = string.Empty;
        public List<string> routeKeys = new List<string>();
        public string commandId = string.Empty;
        public List<string> skillNames = new List<string>();
        public ESAIBrainWorkflowAuthority workflow;
        public string taskId = string.Empty;
        public int taskVersion;
        public string preset = string.Empty;
        public JObject input = new JObject();
        public bool fromAi = true;
        public bool dryRun;
        public string actorId = string.Empty;
        public string invocationId = string.Empty;
        public string routeProfileId = "governance";
        public string goalRevisionPath = string.Empty;
        /// <summary>
        /// Immutable plan hash returned by planTask. External AI execution must submit
        /// it again; a changed plan is stale and cannot be authorized.
        /// </summary>
        public string approvedPlanHash = string.Empty;
        /// <summary>幂等键用于本次执行去重；执行快照参与授权绑定，防止授权被换绑到另一组输入。</summary>
        public string idempotencyKey = string.Empty;
        /// <summary>
        /// Explicit current-user runtime authorization. This is not a blanket bypass:
        /// the coordinator accepts it only for the fixed UI materializer plan and
        /// requires the caller to bind a SHA-256 of the current user instruction.
        /// </summary>
        public bool userDirectedRuntime;
        public string userInstructionHash = string.Empty;
        public ESAutomationExecutionSnapshot executionSnapshot;
        [JsonIgnore]
        internal AIBrainTrustedHostProof trustedHostProof;
    }

    internal sealed class AIBrainTrustedHostProof
    {
        internal readonly string hostId;
        internal readonly string authorizationClass;
        internal readonly string instructionHash;
        internal readonly string invocationId;
        internal readonly string actorId;
        internal readonly string requestHash;
        internal readonly DateTimeOffset issuedAtUtc;
        internal readonly DateTimeOffset expiresAtUtc;

        internal AIBrainTrustedHostProof(string hostId, string authorizationClass,
            string instructionHash, string invocationId, string actorId,
            string requestHash, DateTimeOffset issuedAtUtc, DateTimeOffset expiresAtUtc)
        {
            this.hostId = hostId ?? string.Empty;
            this.authorizationClass = authorizationClass ?? string.Empty;
            this.instructionHash = instructionHash ?? string.Empty;
            this.invocationId = invocationId ?? string.Empty;
            this.actorId = actorId ?? string.Empty;
            this.requestHash = requestHash ?? string.Empty;
            this.issuedAtUtc = issuedAtUtc;
            this.expiresAtUtc = expiresAtUtc;
        }
    }

    [Serializable]
    public sealed class ESAIBrainPlan
    {
        public int contractVersion;
        public string planId = string.Empty;
        public string planHash = string.Empty;
        public string invocationId = string.Empty;
        public string status = string.Empty;
        public string objective = string.Empty;
        public string knowledgeIndexHash = string.Empty;
        public readonly List<string> routeKeys = new List<string>();
        public readonly List<string> blockers = new List<string>();
        public readonly List<ESAIBrainKnowledgeBinding> knowledge = new List<ESAIBrainKnowledgeBinding>();
        public readonly List<ESAIBrainEvidenceBinding> warnings = new List<ESAIBrainEvidenceBinding>();
        public readonly List<ESAIBrainEvidenceBinding> evidence = new List<ESAIBrainEvidenceBinding>();
        public readonly List<ESAIBrainSkillBinding> skills = new List<ESAIBrainSkillBinding>();
        public ESAIBrainWorkflowAuthority workflow;
        public ESAIBrainCommandBinding command;
        public ESAIBrainTaskBinding task;
        public ESAIBrainRoutePlan routePlan;
        public ESAIBrainAuthoritySnapshot authority = new ESAIBrainAuthoritySnapshot();

        [JsonIgnore]
        public bool IsRunnable => string.Equals(status, "Ready", StringComparison.Ordinal)
            && blockers.Count == 0;

        [JsonIgnore]
        public string FirstBlocker => blockers.Count == 0 ? string.Empty : blockers[0];
    }

    [Serializable]
    public sealed class ESAIBrainRoutePlan
    {
        public int schemaVersion;
        public string contractId = string.Empty;
        public string routePlanId = string.Empty;
        public string routePlanHash = string.Empty;
        public string status = string.Empty;
        public string routeState = string.Empty;
        public string evidenceState = string.Empty;
        public string effect = string.Empty;
        public string profile = string.Empty;
        public string scope = string.Empty;
        public readonly List<string> routeKeys = new List<string>();
        public ESAIBrainGoalRevisionBinding goalRevision;
        public readonly List<ESAIBrainRouteStage> stages = new List<ESAIBrainRouteStage>();
        public int maxDepth;
        public JObject budget = new JObject();
        public readonly List<ESAIBrainRouteStopCondition> stopConditions =
            new List<ESAIBrainRouteStopCondition>();
        public readonly List<ESAIBrainRouteIssue> issues = new List<ESAIBrainRouteIssue>();
        public ESAIBrainRouteSnapshot snapshot = new ESAIBrainRouteSnapshot();
        public ESAIBrainRouteShadowIntegration shadowIntegration =
            new ESAIBrainRouteShadowIntegration();
        public ESAIBrainRouteCompatibility compatibility = new ESAIBrainRouteCompatibility();
        public bool executionEnabled;
    }

    [Serializable]
    public sealed class ESAIBrainRouteShadowIntegration
    {
        public string contractId = string.Empty;
        public string mode = string.Empty;
        public string algorithmId = string.Empty;
        public string selectedProfile = string.Empty;
        public string selectedScope = string.Empty;
        public string candidateStatus = string.Empty;
        public string decisionHash;
        public string decisionId;
        public string legacyPlanStatusBefore = string.Empty;
        public string legacyPlanStatusAfter = string.Empty;
        public bool stateChanged;
        public bool verificationRequired;
        public string rollbackState = string.Empty;
        public string rollbackAction = string.Empty;
        public bool productionRouteIntegrated;
        public bool globalP0Integrated;
        public readonly List<string> observationCodes = new List<string>();
    }

    [Serializable]
    public sealed class ESAIBrainGoalRevisionBinding
    {
        public string goalId = string.Empty;
        public string goalRevision = string.Empty;
        public string revisionHash = string.Empty;
        public string projectPath = string.Empty;
        public string artifactHash = string.Empty;
    }

    [Serializable]
    public sealed class ESAIBrainRouteStage
    {
        public string stageId = string.Empty;
        public string stageContractId = string.Empty;
        public string skillName = string.Empty;
        public int depth;
        public readonly List<string> requires = new List<string>();
        public readonly List<string> produces = new List<string>();
        public readonly List<string> failureConditions = new List<string>();
        public string depthReasonCode = string.Empty;
        public string executionStatus = "not-executed";
    }

    [Serializable]
    public sealed class ESAIBrainRouteStopCondition
    {
        public string code = string.Empty;
        public string predicate = string.Empty;
        public string trigger = string.Empty;
        public string outcome = string.Empty;
        public readonly List<string> evidence = new List<string>();
        public string recovery = string.Empty;
    }

    [Serializable]
    public sealed class ESAIBrainRouteIssue
    {
        [JsonProperty("object")]
        public string targetObject = string.Empty;
        public string field = string.Empty;
        public string profile = string.Empty;
        public string scope = string.Empty;
        public string reasonCode = string.Empty;
        public string predicate = string.Empty;
        public string effect = string.Empty;
        public string recovery = string.Empty;
    }

    [Serializable]
    public sealed class ESAIBrainRouteSourceRef
    {
        public string projectPath = string.Empty;
        public string sha256 = string.Empty;
    }

    [Serializable]
    public sealed class ESAIBrainRouteSnapshot
    {
        public string head = string.Empty;
        public readonly List<ESAIBrainRouteSourceRef> sourceRefs =
            new List<ESAIBrainRouteSourceRef>();
        public string sourceRefsHash = string.Empty;
        public string registryHash = string.Empty;
        public ESAIBrainRouteSnapshotCoverage coverage = new ESAIBrainRouteSnapshotCoverage();
    }

    [Serializable]
    public sealed class ESAIBrainRouteSnapshotCoverage
    {
        public string normalizationVersion = string.Empty;
        public readonly List<string> includes = new List<string>();
    }

    [Serializable]
    public sealed class ESAIBrainRouteCompatibility
    {
        public string legacyPlanStatus = string.Empty;
        public bool projectionOnly;
        public bool productionRouteIntegrated;
        public bool globalP0Integrated;
        public string executionAuthority = string.Empty;
    }

    [Serializable]
    public sealed class ESAIBrainAuthoritySnapshot
    {
        public string warnings = string.Empty;
        public string warningsCatalogHash = string.Empty;
        public string command = string.Empty;
        public string knowledge = string.Empty;
        public string skill = string.Empty;
        public string automation = string.Empty;
        public readonly string[] precedence =
        {
            "当前源码与真实证据",
            "AIWarnings P0",
            "AICommand",
            "AIBrain 路由",
            "Project Skill",
            "AIKnowledge 摘要",
        };
    }

    [Serializable]
    public sealed class ESAIBrainKnowledgeBinding
    {
        public string knowledgeId = string.Empty;
        public string file = string.Empty;
        public string topic = string.Empty;
        public string contentHash = string.Empty;
        public List<string> routeKeys = new List<string>();
        public List<string> relatedSkills = new List<string>();
        public List<string> requiredReads = new List<string>();
        public readonly List<string> sourceRefs = new List<string>();
    }

    [Serializable]
    public sealed class ESAIBrainEvidenceBinding
    {
        public string projectPath = string.Empty;
        public string sha256 = string.Empty;
    }

    [Serializable]
    public sealed class ESAIBrainCommandBinding
    {
        public string id = string.Empty;
        public string path = string.Empty;
        public string role = string.Empty;
        public string riskLevel = string.Empty;
        public string writeMode = string.Empty;
        public string catalogHash = string.Empty;
        public string contractHash = string.Empty;
        public string reference = string.Empty;
    }

    [Serializable]
    public sealed class ESAIBrainSkillBinding
    {
        public string name = string.Empty;
        public string skillPath = string.Empty;
        public string skillHash = string.Empty;
        public string metadataHash = string.Empty;
        public string governanceHash = string.Empty;
        public string tier = string.Empty;
        public string maturity = string.Empty;
        public string delivery = string.Empty;
        public string evidenceLevel = string.Empty;
        public string riskClass = string.Empty;
        public string authorityClass = string.Empty;
        public string owner = string.Empty;
        public string acceptanceOwner = string.Empty;
        public bool requiresBrainPlan;
        public bool allowDirectExecution;
        public string writePolicy = string.Empty;
        public string family = string.Empty;
        public string registrationState = string.Empty;
        public string discoveryState = string.Empty;
        public string planEligibility = string.Empty;
        public string runtimeEligibility = string.Empty;
        public bool reviewRequired;
    }

    [Serializable]
    public sealed class ESAIBrainSkillCatalogRecord
    {
        public string family = string.Empty;
        public string registrationState = string.Empty;
    }

    [Serializable]
    public sealed class ESAIBrainModeBinding
    {
        public string modeId = string.Empty;
        public string authorityId = string.Empty;
        public string authorityVersion = string.Empty;
        public string displayName = string.Empty;
        public string englishName = string.Empty;
        public string chineseName = string.Empty;
        public string shortName = string.Empty;
        public string suffix = string.Empty;
        public bool independent;
        public string orchestration = string.Empty;
        public bool dependsOnCore;
        public string fallback = string.Empty;
        public string contractRef = string.Empty;
        public string registryHash = string.Empty;
        public List<string> capabilityCoverage = new List<string>();
    }

    internal sealed class SkillEligibility
    {
        public string discoveryState = string.Empty;
        public string planEligibility = string.Empty;
        public string runtimeEligibility = string.Empty;
        public bool reviewRequired;
    }

    [Serializable]
    public sealed class ESAIBrainSkillGovernanceMetadata
    {
        public int schemaVersion;
        public string skillName = string.Empty;
        public string tier = string.Empty;
        public string maturity = string.Empty;
        public string delivery = string.Empty;
        public string evidenceLevel = string.Empty;
        public string riskClass = string.Empty;
        public string authorityClass = string.Empty;
        public string owner = string.Empty;
        public string acceptanceOwner = string.Empty;
        public string executionMode = string.Empty;
        public bool requiresBrainPlan;
        public bool allowDirectExecution;
        public string writePolicy = string.Empty;
    }

    [Serializable]
    public sealed class ESAIBrainWorkflowAuthority
    {
        public string workflowId = string.Empty;
        public string contentHash = string.Empty;
        public string sourceAssetGuid = string.Empty;
    }

    [Serializable]
    public sealed class ESAIBrainTaskBinding
    {
        public string taskId = string.Empty;
        public int taskVersion;
        public string displayName = string.Empty;
        public string category = string.Empty;
        public string summary = string.Empty;
        public string inputSchemaHash = string.Empty;
        public string descriptorHash = string.Empty;
        public string taskContractHash = string.Empty;
        public bool allowAiInvoke;
        public bool allowInPlayMode;
        public string workerId = string.Empty;
        public string workerType = string.Empty;
        public string workerVersion = string.Empty;
        public string workerEntrypointHash = string.Empty;
        public bool workerEnabled;
        public List<string> capabilities = new List<string>();
    }

    [Serializable]
    public sealed class ESAIBrainCapabilityBinding
    {
        public string id = string.Empty;
        public string kind = string.Empty;
        public string status = string.Empty;
        public string displayName = string.Empty;
        public string summary = string.Empty;
        public string authority = string.Empty;
        public string workerType = string.Empty;
        public string workerId = string.Empty;
        public bool requiresUserAuthorization;
        public bool requiresApproval;
        public List<string> capabilities = new List<string>();
    }

    [Serializable]
    public sealed class ESAIBrainCapabilityDriftSignal
    {
        public int generation;
        public string trigger = string.Empty;
        public string metadataFingerprint = string.Empty;
        public string nextAction = string.Empty;
    }

    [Serializable]
    public sealed class ESAIBrainProductionSurface
    {
        public int contractVersion;
        public string generatedAtUtc = string.Empty;
        public string status = string.Empty;
        public string inventoryHash = string.Empty;
        public readonly List<string> routeKeys = new List<string>();
        public readonly List<ESAIBrainModeBinding> modes = new List<ESAIBrainModeBinding>();
        public readonly List<string> blockers = new List<string>();
        public readonly List<ESAIBrainEvidenceBinding> warnings = new List<ESAIBrainEvidenceBinding>();
        public readonly List<ESAIBrainKnowledgeBinding> knowledge = new List<ESAIBrainKnowledgeBinding>();
        public readonly List<ESAIBrainCommandBinding> commands = new List<ESAIBrainCommandBinding>();
        public readonly List<ESAIBrainSkillBinding> skills = new List<ESAIBrainSkillBinding>();
        public readonly List<ESAIBrainTaskBinding> tasks = new List<ESAIBrainTaskBinding>();
        public readonly List<ESAIBrainCapabilityBinding> cli = new List<ESAIBrainCapabilityBinding>();
        public readonly List<ESAIBrainCapabilityBinding> diagnostics = new List<ESAIBrainCapabilityBinding>();
        public readonly List<ESAIBrainCapabilityBinding> mcp = new List<ESAIBrainCapabilityBinding>();
        public ESAIBrainFailureTelemetrySnapshot failureTelemetry = new ESAIBrainFailureTelemetrySnapshot();
    }

    /// <summary>
    /// Explicit user-facing plan inspection entry. It never scans or executes on open;
    /// the user must submit a concrete request before AIBrain reads routed evidence.
    /// </summary>
    public sealed class ESAIBrainWindow : ESSinglePageIMGUIWindow<ESAIBrainWindow>
    {
        private string objective = string.Empty;
        private string routeKeys = "aibrain,orchestration";
        private string commandId = "project-entry.info";
        private string skillName = "es-editor-tooling";
        private string taskId = string.Empty;
        private int taskVersion = 1;
        private ESAIBrainPlan latestPlan;
        private ESAIBrainProductionSurface productionSurface;
        private Vector2 scrollPosition;
        private bool capabilityRefreshPending;
        private ESAIBrainCapabilityDriftSignal lastCapabilityDrift;

        [MenuItem(MenuItemPathDefine.AUTOMATION_CENTER_PATH + "打开 AIBrain")]
        private static void Open() => OpenWindow();

        public override GUIContent ESWindow_GetWindowGUIContent()
            => new GUIContent("ES AIBrain", "建立并检查 AI 权威路由计划");

        public override string ESWindow_PresentationShortTitle => "AIBrain";
        protected override string ESWindow_Subtitle => "AI 权威路由与执行计划";
        protected override Vector2 ESWindow_MinSize => new Vector2(700f, 560f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(900f, 720f);
        protected override string ESWindow_PageStableId => "automation.aibrain";
        protected override string ESWindow_PageTitle => "AIBrain";
        protected override string ESWindow_PageKeywords => "AIBrain Knowledge AIWarnings AICommand Skill Automation";

        protected override void ESWindow_OnHostEnable()
        {
            base.ESWindow_OnHostEnable();
            maxSize = new Vector2(1400f, 1000f);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ESAIBrainCoordinator.CapabilityDriftDetected += OnCapabilityDriftDetected;
        }

        protected override void OnDisable()
        {
            ESAIBrainCoordinator.CapabilityDriftDetected -= OnCapabilityDriftDetected;
            base.OnDisable();
        }

        private void OnCapabilityDriftDetected(ESAIBrainCapabilityDriftSignal signal)
        {
            lastCapabilityDrift = signal;
            capabilityRefreshPending = true;
            Repaint();
        }

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
            if (capabilityRefreshPending)
            {
                capabilityRefreshPending = false;
                context.SetStatus("Capability metadata changed; route-scoped refresh required",
                    ESMenuTreePageStatus.Warning);
            }
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            try
            {
                EditorGUILayout.HelpBox(
                    "AIBrain 第一阶段聚焦 Skills、CLI、MCP、AIWarnings 与 AIKnowledge 的发现、核对、计划和受控执行；Graph 仅作为可选实验适配。",
                    MessageType.Info);
                objective = EditorGUILayout.TextField("目标", objective);
                routeKeys = EditorGUILayout.TextField("RouteKeys（逗号分隔）", routeKeys);
                commandId = EditorGUILayout.TextField("AICommand", commandId);
                skillName = EditorGUILayout.TextField("Project Skill", skillName);
                taskId = EditorGUILayout.TextField("Automation Task（可空）", taskId);
                taskVersion = EditorGUILayout.IntField("Task Version", taskVersion);
                if (lastCapabilityDrift != null)
                {
                    EditorGUILayout.HelpBox(
                        "Capability drift: " + lastCapabilityDrift.trigger
                        + " / generation " + lastCapabilityDrift.generation
                        + ". Existing plans are stale until route-scoped comparison and re-plan.",
                        MessageType.Warning);
                }
                if (GUILayout.Button("刷新生产力面（ABC 模式 / Skills / CLI / MCP / Warnings / Knowledge）", GUILayout.Height(26f)))
                {
                    productionSurface = ESAIBrainCoordinator.DescribeProductionSurface(Split(routeKeys));
                    context.SetStatus("生产力面已刷新：" + productionSurface.status,
                        productionSurface.blockers.Count == 0
                            ? ESMenuTreePageStatus.Ready : ESMenuTreePageStatus.Warning);
                }
                if (GUILayout.Button("建立只读计划", GUILayout.Height(26f)))
                {
                    latestPlan = ESAIBrainCoordinator.Plan(new ESAIBrainRequest
                    {
                        objective = objective,
                        routeKeys = Split(routeKeys),
                        commandId = commandId,
                        skillNames = Split(skillName),
                        taskId = taskId,
                        taskVersion = taskVersion,
                        fromAi = false,
                        invocationId = Guid.NewGuid().ToString("N"),
                        actorId = Environment.UserName,
                    });
                    context.SetStatus(latestPlan.status,
                        latestPlan.IsRunnable ? ESMenuTreePageStatus.Ready : ESMenuTreePageStatus.Warning);
                }

                if (productionSurface != null)
                {
                    EditorGUILayout.Space(8f);
                    EditorGUILayout.LabelField("生产力面", productionSurface.status, EditorStyles.boldLabel);
                    DrawSelectableValue("InventoryHash", productionSurface.inventoryHash);
                    EditorGUILayout.LabelField("ABC 模式", productionSurface.modes.Count.ToString());
                    EditorGUILayout.LabelField("Skills", productionSurface.skills.Count.ToString());
                    EditorGUILayout.LabelField("CLI", productionSurface.cli.Count.ToString());
                    EditorGUILayout.LabelField("MCP", productionSurface.mcp.Count.ToString());
                    EditorGUILayout.LabelField("Knowledge", productionSurface.knowledge.Count.ToString());
                    EditorGUILayout.LabelField("AIWarnings", productionSurface.warnings.Count.ToString());
                    EditorGUILayout.LabelField("AICommands", productionSurface.commands.Count.ToString());
                    if (productionSurface.blockers.Count > 0)
                        EditorGUILayout.HelpBox(string.Join("\n", productionSurface.blockers.Take(5)), MessageType.Warning);
                }

                if (latestPlan != null)
                {
                    EditorGUILayout.Space(8f);
                    EditorGUILayout.LabelField("计划状态", latestPlan.status, EditorStyles.boldLabel);
                    DrawSelectableValue("PlanId", latestPlan.planId);
                    DrawSelectableValue("PlanHash", latestPlan.planHash);
                    EditorGUILayout.LabelField("Knowledge", latestPlan.knowledge.Count.ToString());
                    EditorGUILayout.LabelField("Warnings", latestPlan.warnings.Count.ToString());
                    EditorGUILayout.LabelField("Skills", latestPlan.skills.Count.ToString());
                    if (latestPlan.blockers.Count > 0)
                        EditorGUILayout.HelpBox(string.Join("\n", latestPlan.blockers), MessageType.Warning);
                    else
                        EditorGUILayout.HelpBox("计划已通过静态权威门禁；执行仍须由明确的用户动作触发。", MessageType.None);
                }
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawSelectableValue(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            try
            {
                EditorGUILayout.PrefixLabel(label);
                EditorGUILayout.SelectableLabel(value ?? string.Empty,
                    EditorStyles.textField, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight));
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }
        }

        private static List<string> Split(string value)
        {
            return (value ?? string.Empty).Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim()).Where(item => item.Length > 0).ToList();
        }
    }

    public static class ESAIBrainFailureTelemetry
    {
        private const int Capacity = 256;
        private static readonly object Sync = new object();
        private static readonly Queue<ESAIBrainFailureEvent> Events = new Queue<ESAIBrainFailureEvent>();

        public static void Record(string category, string stage, string detail, string correlationId = "")
        {
            if (string.IsNullOrWhiteSpace(category)) return;
            var item = new ESAIBrainFailureEvent
            {
                category = category.Trim(),
                stage = string.IsNullOrWhiteSpace(stage) ? "unknown" : stage.Trim(),
                occurredAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                correlationId = NormalizeIdentifier(correlationId),
                detailHash = Hash(detail ?? string.Empty),
            };
            lock (Sync)
            {
                while (Events.Count >= Capacity) Events.Dequeue();
                Events.Enqueue(item);
            }
        }

        public static void RecordPlan(ESAIBrainPlan plan, string stage)
        {
            if (plan == null)
            {
                Record("PlanTaskUnavailable", stage, "plan:null");
                return;
            }
            if (plan.IsRunnable) return;
            Record(Classify(plan), stage, string.Join("\n", plan.blockers), plan.planId);
        }

        public static ESAIBrainFailureTelemetrySnapshot Snapshot()
        {
            ESAIBrainFailureEvent[] copy;
            lock (Sync) copy = Events.ToArray();
            var snapshot = new ESAIBrainFailureTelemetrySnapshot
            {
                generatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                capacity = Capacity,
                retainedEventCount = copy.Length,
            };
            snapshot.counts.AddRange(copy.GroupBy(item => item.category, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new ESAIBrainFailureCount
                {
                    category = group.Key,
                    count = group.Count(),
                    lastOccurredAtUtc = group.Last().occurredAtUtc,
                }));
            snapshot.recent.AddRange(copy.Skip(Math.Max(0, copy.Length - 32)));
            return snapshot;
        }

        internal static void ClearForTests()
        {
            lock (Sync) Events.Clear();
        }

        private static string Classify(ESAIBrainPlan plan)
        {
            string blockers = string.Join("\n", plan.blockers ?? new List<string>());
            if (blockers.IndexOf("SourceRef", StringComparison.OrdinalIgnoreCase) >= 0
                && blockers.IndexOf("漂移", StringComparison.OrdinalIgnoreCase) >= 0)
                return "SourceHashDrift";
            if (string.Equals(plan.status, "PlanTaskUnavailable", StringComparison.Ordinal))
                return "PlanTaskUnavailable";
            if (string.Equals(plan.status, "NoKnowledgeRoute", StringComparison.Ordinal))
                return "NoKnowledgeRoute";
            if (string.Equals(plan.status, "NoMatchingCommand", StringComparison.Ordinal))
                return "NoMatchingCommand";
            return "PlanBlocked";
        }

        private static string NormalizeIdentifier(string value)
        {
            value = value ?? string.Empty;
            return value.Length <= 128 ? value : value.Substring(0, 128);
        }

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    public static class ESAIBrainRouteProbeRunner
    {
        public const string RegistryPath = "Documentation/AIKnowledge/RouteProbeRegistry.json";

        public static ESAIBrainRouteProbeReport Run()
        {
            var report = new ESAIBrainRouteProbeReport
            {
                generatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                registryPath = RegistryPath,
                evidenceBoundary = "static-routing-only",
            };
            try
            {
                string fullPath = Path.Combine(ESCommandPalettePathPolicy.ProjectRoot,
                    RegistryPath.Replace('/', Path.DirectorySeparatorChar));
                string text = File.ReadAllText(fullPath, new UTF8Encoding(false, true));
                report.registryHash = Hash(text);
                JObject registry = JObject.Parse(text);
                ValidateRegistry(registry);
                report.rankingVersion = registry.Value<string>("rankingVersion") ?? string.Empty;
                foreach (JObject probe in registry["probes"].OfType<JObject>())
                    report.results.Add(RunProbe(probe));
                report.status = report.results.All(item => item.passed) ? "Passed" : "Failed";
            }
            catch (Exception exception)
            {
                report.status = "Blocked";
                report.error = exception.Message;
            }
            if (!string.Equals(report.status, "Passed", StringComparison.Ordinal))
                ESAIBrainFailureTelemetry.Record("WrongKnowledgeRoute", "route-probe",
                    JsonConvert.SerializeObject(report, Formatting.None), report.registryHash);
            return report;
        }

        private static ESAIBrainRouteProbeResult RunProbe(JObject probe)
        {
            string probeId = probe.Value<string>("probeId") ?? string.Empty;
            string[] expectedRoutes = ReadStrings(probe["expectedRouteKeys"]);
            JObject[] expectedKnowledge = (probe["expectedKnowledgeTop3"] as JArray ?? new JArray())
                .OfType<JObject>().ToArray();
            string[] forbidden = ReadStrings(probe["forbiddenKnowledgeIds"]);
            int repeatCount = probe.Value<int>("repeatCount");
            var result = new ESAIBrainRouteProbeResult { probeId = probeId, passed = true };
            for (int attempt = 0; attempt < repeatCount; attempt++)
            {
                ESAIBrainPlan plan = ESAIBrainCoordinator.Plan(new ESAIBrainRequest
                {
                    objective = probe.Value<string>("objective") ?? string.Empty,
                    routeKeys = ReadStrings(probe["explicitRouteKeys"]).ToList(),
                    invocationId = Guid.NewGuid().ToString("N"),
                    fromAi = false,
                });
                string[] actualKnowledge = plan.knowledge.Select(item => item.knowledgeId).ToArray();
                if (attempt == 0)
                {
                    result.actualRouteKeys.AddRange(plan.routeKeys);
                    result.actualKnowledgeTop3.AddRange(actualKnowledge);
                }
                if (!expectedRoutes.SequenceEqual(plan.routeKeys, StringComparer.Ordinal)
                    || !expectedKnowledge.Select(item => item.Value<string>("knowledgeId"))
                        .SequenceEqual(actualKnowledge, StringComparer.Ordinal)
                    || forbidden.Intersect(actualKnowledge, StringComparer.Ordinal).Any())
                    result.passed = false;
                foreach (JObject expectation in expectedKnowledge)
                {
                    ESAIBrainKnowledgeBinding binding = plan.knowledge.FirstOrDefault(item =>
                        string.Equals(item.knowledgeId, expectation.Value<string>("knowledgeId"), StringComparison.Ordinal));
                    string[] expectedReads = ReadStrings(expectation["requiredReads"]);
                    if (binding == null || !expectedReads.SequenceEqual(binding.requiredReads, StringComparer.Ordinal))
                    {
                        result.passed = false;
                        result.requiredReadsMismatch = true;
                        if (binding != null && binding.requiredReads.Except(expectedReads, StringComparer.Ordinal).Any())
                            result.requiredReadsOverflow = true;
                    }
                }
            }
            if (!result.passed)
                ESAIBrainFailureTelemetry.Record(result.requiredReadsOverflow
                    ? "RequiredReadOverflow" : "WrongKnowledgeRoute", "route-probe", probeId, probeId);
            return result;
        }

        private static void ValidateRegistry(JObject registry)
        {
            if (registry == null || registry.Value<int?>("schemaVersion") != 1)
                throw new InvalidDataException("Unsupported route probe schemaVersion.");
            if (!string.Equals(registry.Value<string>("rankingVersion"),
                    ESAIBrainCoordinator.KnowledgeRankingVersion, StringComparison.Ordinal))
                throw new InvalidDataException("Unsupported route probe rankingVersion.");
            if (!string.Equals(registry.Value<string>("lifecycleState"), "operational-static",
                    StringComparison.Ordinal)
                || !string.Equals(registry.Value<string>("ownerKnowledgeId"),
                    "es.knowledge.routing-quality.v1", StringComparison.Ordinal))
                throw new InvalidDataException("Route probe registration metadata is invalid.");
            JObject consumers = registry["consumers"] as JObject
                ?? throw new InvalidDataException("Route probe consumers are missing.");
            if (!string.Equals(consumers.Value<string>("bridgeOperation"),
                    "runKnowledgeRouteProbes", StringComparison.Ordinal)
                || !string.Equals(consumers.Value<string>("productionSurfaceId"),
                    "diagnostic.knowledge-route-probes", StringComparison.Ordinal))
                throw new InvalidDataException("Route probe production consumer registration is invalid.");
            JObject[] probes = (registry["probes"] as JArray ?? new JArray()).OfType<JObject>().ToArray();
            if (probes.Length < 10) throw new InvalidDataException("At least 10 route probes are required.");
            string[] ids = probes.Select(item => item.Value<string>("probeId") ?? string.Empty).ToArray();
            if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new InvalidDataException("Route probe ids must be non-empty and unique.");
        }

        private static string[] ReadStrings(JToken token)
        {
            string[] values = (token as JArray ?? new JArray()).Values<string>().ToArray();
            if (values.Any(string.IsNullOrWhiteSpace)
                || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                throw new InvalidDataException("Route probe arrays must contain unique non-empty strings.");
            return values;
        }

        private static string Hash(string text)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text)))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    [Serializable] public sealed class ESAIBrainFailureEvent { public string category = ""; public string stage = ""; public string occurredAtUtc = ""; public string correlationId = ""; public string detailHash = ""; }
    [Serializable] public sealed class ESAIBrainFailureCount { public string category = ""; public int count; public string lastOccurredAtUtc = ""; }
    [Serializable] public sealed class ESAIBrainFailureTelemetrySnapshot { public string generatedAtUtc = ""; public int capacity; public int retainedEventCount; public readonly List<ESAIBrainFailureCount> counts = new List<ESAIBrainFailureCount>(); public readonly List<ESAIBrainFailureEvent> recent = new List<ESAIBrainFailureEvent>(); }
    [Serializable] public sealed class ESAIBrainRouteProbeResult { public string probeId = ""; public bool passed; public bool requiredReadsMismatch; public bool requiredReadsOverflow; public readonly List<string> actualRouteKeys = new List<string>(); public readonly List<string> actualKnowledgeTop3 = new List<string>(); }
    [Serializable] public sealed class ESAIBrainRouteProbeReport { public string generatedAtUtc = ""; public string registryPath = ""; public string registryHash = ""; public string rankingVersion = ""; public string evidenceBoundary = ""; public string status = "Blocked"; public string error = ""; public readonly List<ESAIBrainRouteProbeResult> results = new List<ESAIBrainRouteProbeResult>(); }
}
