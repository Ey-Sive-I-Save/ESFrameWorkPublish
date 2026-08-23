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
        public const int ContractVersion = 1;
        public const string KnowledgeIndexPath = "Documentation/AIKnowledge/KnowledgeIndex.yaml";
        public const string ProjectSkillsRoot = ".agents/skills";
        public const string ProjectSkillCatalogPath = ".agents/SKILL_CATALOG.yaml";
        public const string ProjectSkillDiscoveryPolicyPath = ".agents/SKILL_DISCOVERY_POLICY.json";
        private const string SkillGovernanceMetadataFileName = "governance.json";

        private const string AiwarningsRoot = "Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/";
        private const string AiwarningsReadme = AiwarningsRoot + "README.md";
        private const string AiwarningsCurrentStatus = AiwarningsRoot + "当前状态（CurrentStatus）.md";
        private const string AiwarningsRuleIndex = AiwarningsRoot + "规则索引（RuleIndex）.md";
        private const string AiwarningsRouteCatalog = AiwarningsRoot + "AIWarningsRouteCatalog.json";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly object AuthorizationLock = new object();
        private static readonly Dictionary<string, AIBrainExecutionAuthorization> Authorizations =
            new Dictionary<string, AIBrainExecutionAuthorization>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan AuthorizationLifetime = TimeSpan.FromMinutes(1);

        public static bool TryPlan(ESAIBrainRequest request, out ESAIBrainPlan plan, out string error)
        {
            plan = BuildPlan(request);
            error = plan == null ? "AIBrain 未能建立计划。" : plan.FirstBlocker;
            return plan != null && plan.IsRunnable;
        }

        public static ESAIBrainPlan Plan(ESAIBrainRequest request)
        {
            return BuildPlan(request);
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
            try
            {
                if (request.executionSnapshot != null) request.executionSnapshot.Validate();
                if (!string.IsNullOrWhiteSpace(request.idempotencyKey)
                    && (request.idempotencyKey.Length > 160
                        || !Regex.IsMatch(request.idempotencyKey, "^[A-Za-z0-9._:-]+$")))
                    return RejectPlan(out plan, "AIBrain 幂等键格式无效。");
            }
            catch (Exception exception)
            {
                return RejectPlan(out plan, "AIBrain 执行快照无效：" + exception.Message);
            }
            plan = BuildPlan(request);
            if (plan == null)
                return ESAutomationTaskInvocationResult.Rejected("AIBrain 未能建立计划。");
            if (!plan.IsRunnable)
                return ESAutomationTaskInvocationResult.Blocked(
                    "AIBrain 门禁未通过：" + plan.FirstBlocker, plan.planId);
            if (request.executionSnapshot != null)
            {
                if (!string.Equals(request.executionSnapshot.brainPlanHash, plan.planHash,
                    StringComparison.OrdinalIgnoreCase))
                    return ESAutomationTaskInvocationResult.Blocked(
                        "ExecutionSnapshot.brainPlanHash 与当前 AIBrain PlanHash 不一致。", plan.planId);
                if (plan.command != null
                    && !string.Equals(request.executionSnapshot.commandHash, plan.command.contractHash,
                        StringComparison.OrdinalIgnoreCase))
                    return ESAutomationTaskInvocationResult.Blocked(
                        "ExecutionSnapshot.commandHash 与当前 AICommand 合同不一致。", plan.planId);
            }

            if (string.IsNullOrWhiteSpace(request.invocationId)
                || !Guid.TryParseExact(request.invocationId, "N", out _))
            {
                return ESAutomationTaskInvocationResult.Rejected(
                    "AIBrain 执行必须携带稳定的 N 格式 InvocationId，以防止重复副作用。");
            }

            var invocation = new ESAutomationTaskInvocation
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
            };
            RegisterAuthorization(invocation);
            return ESAutomationFacade.RunTask(invocation);
        }

        private static ESAutomationTaskInvocationResult RejectPlan(out ESAIBrainPlan plan, string message)
        {
            plan = null;
            return ESAutomationTaskInvocationResult.Rejected(message);
        }

        internal static bool TryConsumeAuthorization(ESAutomationTaskInvocation invocation,
            out string reason)
        {
            reason = string.Empty;
            if (invocation == null || !invocation.fromAi
                || !ESAutomationWorkerRegistration.IsSha256(invocation.brainPlanHash))
            {
                reason = "AI Automation 调用缺少有效的 AIBrain PlanHash。";
                return false;
            }

            lock (AuthorizationLock)
            {
                PurgeExpiredAuthorizations();
                if (!Authorizations.TryGetValue(invocation.brainPlanHash,
                        out AIBrainExecutionAuthorization authorization))
                {
                    reason = "AIBrain PlanHash 未签发、已过期或已被消费。";
                    return false;
                }
                Authorizations.Remove(invocation.brainPlanHash);
                string invocationHash = ComputeInvocationHash(invocation);
                if (!string.Equals(authorization.invocationHash, invocationHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    reason = "AIBrain 执行许可与当前 Invocation 不一致，已拒绝并消费该许可。";
                    return false;
                }
                return true;
            }
        }

        private static void RegisterAuthorization(ESAutomationTaskInvocation invocation)
        {
            lock (AuthorizationLock)
            {
                PurgeExpiredAuthorizations();
                Authorizations[invocation.brainPlanHash] = new AIBrainExecutionAuthorization
                {
                    invocationHash = ComputeInvocationHash(invocation),
                    expiresAtUtc = DateTimeOffset.UtcNow + AuthorizationLifetime,
                };
            }
        }

        private static void PurgeExpiredAuthorizations()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (string key in Authorizations.Where(pair => pair.Value.expiresAtUtc <= now)
                         .Select(pair => pair.Key).ToArray())
                Authorizations.Remove(key);
        }

        private static string ComputeInvocationHash(ESAutomationTaskInvocation invocation)
        {
            string canonical = JsonConvert.SerializeObject(new
            {
                invocation.invocationId,
                invocation.brainPlanHash,
                invocation.taskId,
                invocation.taskVersion,
                invocation.preset,
                input = invocation.input ?? new JObject(),
                invocation.fromAi,
                invocation.dryRun,
                invocation.actorId,
                invocation.idempotencyKey,
                invocation.executionSnapshot,
            }, Formatting.None);
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>
        /// Explicit, read-only discovery of the production surfaces that AIBrain can route.
        /// Directory enumeration is only performed after an explicit caller request; this
        /// method is never called from domain-load registration.
        /// </summary>
        public static ESAIBrainProductionSurface DescribeProductionSurface(
            IEnumerable<string> requestedRouteKeys = null)
        {
            List<string> routeKeys = NormalizeValues(requestedRouteKeys);
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

            CollectCommands(surface);
            CollectSkills(surface);
            CollectAutomationAndCli(surface);
            surface.mcp.AddRange(ESAutomationAiBridge.CopyMcpCapabilitiesForBrain());

            surface.status = surface.blockers.Count == 0 ? "Ready" : "Partial";
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

        private static string ComputeProductionSurfaceHash(ESAIBrainProductionSurface surface)
        {
            string canonical = JsonConvert.SerializeObject(new
            {
                surface.contractVersion,
                surface.routeKeys,
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

            List<string> routeKeys = NormalizeValues(request.routeKeys);
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
            plan.planHash = ComputePlanHash(plan, request);
            return plan;
        }

        private static string ResolveBlockedStatus(List<string> blockers)
        {
            string text = string.Join("\n", blockers);
            if (text.Contains("AICommand", StringComparison.Ordinal)) return "NoMatchingCommand";
            if (text.Contains("Skill", StringComparison.Ordinal)) return "NoMatchingSkill";
            if (text.Contains("Knowledge", StringComparison.Ordinal)) return "NoKnowledgeRoute";
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

            try
            {
                contract.Validate();
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
                allowAiInvoke = descriptor.allowAiInvoke,
                workerId = contract.worker?.workerId ?? string.Empty,
                workerType = contract.worker?.type ?? string.Empty,
            };
            plan.task.capabilities.AddRange(contract.capabilities ?? new List<string>());
            plan.authority.automation = "ESAutomationFacade";
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

            List<KnowledgeIndexEntry> matched = entries.Where(entry =>
                entry.routeKeys.Any(routeKeys.Contains)).ToList();
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
                        if (!string.Equals(actualHash, sourceRef.sha256, StringComparison.OrdinalIgnoreCase))
                            plan.blockers.Add("Knowledge SourceRef 哈希漂移：" + sourceRef.path);
                    }
                    binding.sourceRefs.Add(sourceRef.path + " (" + actualHash + ")");
                }
                if (actualSourceHashes.Count == sourceRefs.Count
                    && !string.Equals(ComputeStableHashSetHash(actualSourceHashes), entry.contentHash,
                        StringComparison.OrdinalIgnoreCase))
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
                    readingRequiredReads = true;
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
            bool hasSkillUnderstandingRefreshSignal = ContainsAnyIgnoreCase(text,
                "你的理解已经过时", "理解已经过时", "刷新一下技能理解", "刷新技能理解",
                "重新理解当前项目提供的 skill", "重新理解当前项目的 skill",
                "技能增量理解", "技能理解刷新", "技能能力刷新",
                "understanding drift", "skill understanding refresh", "refresh skill understanding",
                "capability refresh", "incremental skill discovery");
            bool hasSnapshotSignal = ContainsAnyIgnoreCase(text,
                "snapshot", "快照", "task read", "read manifest", "读取清单",
                "源文件哈希", "文件哈希", "parser registry", "解析器注册",
                "projectionpacket", "projection cache", "多文件读取", "重复读取",
                "二进制解析", "文件格式");
            bool hasFileContext = ContainsAnyIgnoreCase(text,
                "文件", "读取", "解析", "parser", "projection", "binary", "snapshot",
                "快照", "hash", "哈希");
            bool hasCacheContext = ContainsAnyIgnoreCase(text, "缓存命中", "缓存失效", "缓存漂移")
                && hasFileContext;

            var inferred = new List<string>();
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
            if (hasSnapshotSignal || hasCacheContext)
                inferred.Add("consistency");
            return inferred.Distinct(StringComparer.Ordinal).ToList();
        }

        private static bool ContainsAnyIgnoreCase(string text, params string[] values)
        {
            return (values ?? Array.Empty<string>())
                .Any(value => ContainsIgnoreCase(text, value));
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

        private static string ComputePlanHash(ESAIBrainPlan plan, ESAIBrainRequest request)
        {
            string canonical = JsonConvert.SerializeObject(new
            {
                plan.contractVersion,
                plan.objective,
                plan.routeKeys,
                knowledge = plan.knowledge.Select(item => new { item.knowledgeId, item.contentHash }),
                warnings = plan.warnings.Select(item => new { item.projectPath, item.sha256 }),
                command = plan.command == null ? string.Empty : plan.command.contractHash,
                skills = plan.skills.Select(item => new
                {
                    item.name,
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
                }),
                workflow = plan.workflow == null
                    ? string.Empty : plan.workflow.workflowId + "@" + plan.workflow.contentHash,
                task = plan.task == null ? string.Empty : plan.task.taskId + "@" + plan.task.taskVersion,
                request.invocationId,
                request.preset,
                input = request.input ?? new JObject(),
                request.fromAi,
                request.dryRun,
                request.actorId,
                request.idempotencyKey,
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
            }, Formatting.None);
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
                    .Replace("-", string.Empty).ToLowerInvariant();
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

        private sealed class AIBrainExecutionAuthorization
        {
            public string invocationHash = string.Empty;
            public DateTimeOffset expiresAtUtc;
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
        /// <summary>可选的幂等键和执行快照会参与 AIBrain 计划/授权指纹，防止授权被换绑到另一组输入。</summary>
        public string idempotencyKey = string.Empty;
        public ESAutomationExecutionSnapshot executionSnapshot;
    }

    [Serializable]
    public sealed class ESAIBrainPlan
    {
        public int contractVersion;
        public string planId = string.Empty;
        public string planHash = string.Empty;
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
        public ESAIBrainAuthoritySnapshot authority = new ESAIBrainAuthoritySnapshot();

        [JsonIgnore]
        public bool IsRunnable => string.Equals(status, "Ready", StringComparison.Ordinal)
            && blockers.Count == 0;

        [JsonIgnore]
        public string FirstBlocker => blockers.Count == 0 ? string.Empty : blockers[0];
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
    }

    [Serializable]
    public sealed class ESAIBrainSkillCatalogRecord
    {
        public string family = string.Empty;
        public string registrationState = string.Empty;
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
        public bool allowAiInvoke;
        public string workerId = string.Empty;
        public string workerType = string.Empty;
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
    public sealed class ESAIBrainProductionSurface
    {
        public int contractVersion;
        public string generatedAtUtc = string.Empty;
        public string status = string.Empty;
        public string inventoryHash = string.Empty;
        public readonly List<string> routeKeys = new List<string>();
        public readonly List<string> blockers = new List<string>();
        public readonly List<ESAIBrainEvidenceBinding> warnings = new List<ESAIBrainEvidenceBinding>();
        public readonly List<ESAIBrainKnowledgeBinding> knowledge = new List<ESAIBrainKnowledgeBinding>();
        public readonly List<ESAIBrainCommandBinding> commands = new List<ESAIBrainCommandBinding>();
        public readonly List<ESAIBrainSkillBinding> skills = new List<ESAIBrainSkillBinding>();
        public readonly List<ESAIBrainTaskBinding> tasks = new List<ESAIBrainTaskBinding>();
        public readonly List<ESAIBrainCapabilityBinding> cli = new List<ESAIBrainCapabilityBinding>();
        public readonly List<ESAIBrainCapabilityBinding> mcp = new List<ESAIBrainCapabilityBinding>();
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

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
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
                if (GUILayout.Button("刷新生产力面（Skills / CLI / MCP / Warnings / Knowledge）", GUILayout.Height(26f)))
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
                    EditorGUILayout.LabelField("Skills", productionSurface.skills.Count.ToString());
                    EditorGUILayout.LabelField("CLI", productionSurface.cli.Count.ToString());
                    EditorGUILayout.LabelField("MCP", productionSurface.mcp.Count.ToString());
              