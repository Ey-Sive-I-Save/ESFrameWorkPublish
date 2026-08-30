using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ES
{
    /// <summary>
    /// Bounded provider adapter for ABC generation. It only stages/sends a prompt to
    /// the existing controlled Codex window; it never writes project files and never
    /// owns ABCD final authority.
    /// </summary>
    public static class ESABCModelProviderAdapter
    {
        private const int MaximumObjectiveCharacters = 16000;
        private const int MaximumContextCharacters = 32000;
        private const int MaximumResponseCharacters = 128000;
        private const int MaximumTotalProposedChangeCharacters = 64000;
        private const int MaximumTotalProposedChangeFiles = 32;

        public static bool TryBuildGenerationPrompt(
            string objective,
            string generationMode,
            string acceptanceProfile,
            string context,
            out string prompt,
            out string error)
        {
            prompt = string.Empty;
            error = string.Empty;
            string normalizedObjective = objective?.Trim() ?? string.Empty;
            string normalizedMode = generationMode?.Trim().ToLowerInvariant() ?? string.Empty;
            string normalizedProfile = acceptanceProfile?.Trim().ToLowerInvariant() ?? string.Empty;
            string normalizedContext = context?.Trim() ?? string.Empty;
            if (normalizedObjective.Length == 0 || normalizedObjective.Length > MaximumObjectiveCharacters)
            {
                error = "ABC Provider objective 不能为空且不得超过 16000 字符。";
                return false;
            }
            if (!IsGenerationMode(normalizedMode))
            {
                error = "ABC Provider generationMode 无效。";
                return false;
            }
            if (!IsAcceptanceProfile(normalizedProfile))
            {
                error = "ABC Provider acceptanceProfile 无效。";
                return false;
            }
            if (normalizedContext.Length > MaximumContextCharacters)
            {
                error = "ABC Provider context 不得超过 32000 字符。";
                return false;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("你是 ES ABC 方案生成 Provider。只生成候选方案，不执行写入、运行、发布或删除。");
            builder.Append("generationMode=").AppendLine(normalizedMode);
            builder.Append("acceptanceProfile=").AppendLine(normalizedProfile);
            builder.AppendLine(ModeInstruction(normalizedMode));
            builder.AppendLine("Shared pipeline is mandatory for creative-divergence and engineering: AB fast seed -> ABCD expand -> ABCD audit -> playability backpressure -> finalization. Preserve stage evidence fields seedDraft, expansionSet, auditFindings, playabilityBackpressure, and finalDecision for every candidate.");
            builder.AppendLine("For creative-divergence, bold novelty is valid only when the candidate names at least two deleted default anchors, one novel mechanism, a player-readable plausibility rationale, and a counterplay invariant; include surpriseScore>=70 and plausibilityScore>=60. Never use impossible power without causal, resource, timing, or counterplay reasons.");
            builder.AppendLine("Playability is not full-system comprehension: provide firstUseAffordance, partialUnderstandingPath, and masteryDepth separately. onboardingBurden is observational only and never an automatic failure; firstPayoffSeconds<=10 and firstInputCount<=3 prove an immediate playable affordance, while mastery may be layered later.");
            builder.AppendLine("创新完整性是硬门槛：必须保留 requested identity、role 和 requested form factor，只改变 mechanism；把刀改成枪属于 form-factor substitution，必须拒绝。每个分叉都必须给出 concretePlayerScenario、inputSequence、visibleFeedback、acceptabilityRationale、concretenessScore>=70、acceptabilityScore>=60。连续执行至少12轮、每轮2-4个分叉，输出 iterationTrace；每轮先发散，再以玩家可接受度筛选，再深化，禁止把抽象、极端或逆天程度当作创新分数。");
            builder.AppendLine("Self-critique loop is mandatory: generate all visible branches -> score the mode focus -> repair/amplify the weakest dimension -> rank only after the repair pass. Return the self-critique summary per candidate; do not silently discard a weak branch.");
            // ABC_GENERATION_NO_SILENT_HIDING: every candidate remains visible for collaborator review.
            // Mode-specific quality focus: creative feel, engineering depth, stable project closure.
            builder.AppendLine("amplificationChain format: core action -> linked mechanic -> visible payoff -> recovery/follow-up choice; provide at least four stages, and keep stages distinct.");
            builder.AppendLine("候选必须全部显式列出；不得静默隐藏。每个候选必须回答：够不够爽、是否丝滑、表现是否强、上限是否高、机制是否有深度/突破/复用/寿命、是否接纳项目且形成安全完整闭环。必须提供 modeScores（17项0-100）、amplificationChain、selfCritique 和 selfCritiquePasses；selfCritique 必须使用 weakest=...; repair=...; ranking=... 三个结构标记，并至少执行 2 轮自评补强。不要只给孤立技能名。创意模式必须给出首10秒爽点和复杂度减法；工程模式必须为每个复杂状态给出所有者、恢复路径和回归信号。");
            builder.AppendLine("请只输出一个 JSON 对象：{\"schemaVersion\":1,\"contractId\":\"es://automation/contracts/ai-abc/generation-response/v1\",\"generationMode\":\"" + normalizedMode + "\",\"status\":\"candidate\",\"candidates\":[...] }。禁止 Markdown 包装、accepted 字段或省略候选。");
            builder.AppendLine("只有最终排序选中的候选允许附 proposedChanges；未选候选只返回机制说明，不要重复携带代码。总 proposedChanges 不超过 32 个文件/64000 字符，整体回执不超过 128000 字符；不要输出绝对路径。");
            builder.AppendLine("最终审计门禁只在生成完成后生效；本阶段只按当前模式的目标生成与排序，不用其他模式的词汇提前压平方案。");
            builder.AppendLine("最终输出只代表 candidate，不代表 design accepted、Unity accepted 或 runtime accepted。");
            builder.AppendLine("用户目标：");
            builder.AppendLine(normalizedObjective);
            if (normalizedContext.Length > 0)
            {
                builder.AppendLine("已有上下文：");
                builder.AppendLine(normalizedContext);
            }
            prompt = builder.ToString();
            return true;
        }

        public static ESCmdAgentPromptDispatchResult DispatchGenerationPrompt(
            string objective,
            string generationMode,
            string acceptanceProfile,
            string context,
            string correlationId = "",
            int timeoutSeconds = 0)
        {
            if (!TryBuildGenerationPrompt(objective, generationMode, acceptanceProfile,
                    context, out string prompt, out string error))
                return new ESCmdAgentPromptDispatchResult(ESCmdAgentPromptDispatchState.Rejected, error);
            return ESCmdAgentWindow.OpenAndSendPromptWithReceipt(prompt, correlationId, timeoutSeconds);
        }

        /// <summary>
        /// Completes the host-side generation handoff: consume only the exact managed
        /// operation receipt returned by DispatchGenerationPrompt, then normalize it into
        /// a candidate envelope. No sibling receipt, mutable source path, audit decision,
        /// or apply authority is inferred here.
        /// </summary>
        public static bool TryConsumeGenerationDispatch(
            ESCmdAgentPromptDispatchResult dispatch,
            string expectedGenerationMode,
            out ESABCGenerationResponseEnvelope envelope,
            out string error)
        {
            envelope = null;
            error = string.Empty;
            if (!dispatch.Accepted)
            {
                error = "ABC Provider dispatch 未进入 Sent 状态，不能消费生成回执。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(dispatch.OperationDirectory)
                || !Path.IsPathRooted(dispatch.OperationDirectory))
            {
                error = "ABC Provider dispatch 缺少绝对 OperationDirectory。";
                return false;
            }
            if (!TryReadProviderResponseReceipt(dispatch.OperationDirectory, out string response, out error))
                return false;
            return TryParseGenerationResponse(response, expectedGenerationMode, out envelope, out error);
        }

        /// <summary>
        /// Parses a provider response into an explicit candidate envelope. This is deliberately
        /// candidate-only: it cannot apply files, approve a design, or grant runtime authority.
        /// The provider may return a JSON object directly or a single fenced JSON block.
        /// </summary>
        public static bool TryParseGenerationResponse(
            string response,
            string expectedGenerationMode,
            out ESABCGenerationResponseEnvelope envelope,
            out string error)
        {
            envelope = null;
            error = string.Empty;
            string text = response?.Trim() ?? string.Empty;
            if (text.Length == 0 || text.Length > MaximumResponseCharacters)
            {
                error = "ABC Provider 回执为空或超过 128000 字符。";
                return false;
            }
            string json = ExtractJson(text);
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "ABC Provider 回执不是可解析的 JSON 候选包。";
                return false;
            }
            try
            {
                JObject root = JObject.Parse(json);
                if (root.Value<int?>("schemaVersion") != 1
                    || !string.Equals(root.Value<string>("contractId"),
                        "es://automation/contracts/ai-abc/generation-response/v1", StringComparison.Ordinal))
                {
                    error = "Provider 回执缺少匹配的 generation-response/v1 契约身份。";
                    return false;
                }
                string mode = root.Value<string>("generationMode")?.Trim().ToLowerInvariant() ?? string.Empty;
                string expected = expectedGenerationMode?.Trim().ToLowerInvariant() ?? string.Empty;
                if (!IsGenerationMode(mode) || !string.Equals(mode, expected, StringComparison.Ordinal))
                {
                    error = "generationMode 与请求不一致。";
                    return false;
                }
                if (!string.Equals(root.Value<string>("status"), "candidate", StringComparison.Ordinal))
                {
                    error = "Provider 回执必须声明 status=candidate。";
                    return false;
                }
                JArray items = root["candidates"] as JArray;
                if (items == null || items.Count == 0)
                {
                    error = "候选包不能为空。";
                    return false;
                }
                int minimum = mode == "creative-divergence" ? 7 : 5;
                int maximum = mode == "stable" ? 8 : mode == "engineering" ? 12 : 16;
                if (items.Count < minimum || items.Count > maximum)
                {
                    error = "候选数量不符合当前 generationMode 的发散预算。";
                    return false;
                }
                var seen = new HashSet<string>(StringComparer.Ordinal);
            var candidates = new List<ESABCGenerationCandidate>();
            int totalProposedChangeCharacters = 0;
            int totalProposedChangeFiles = 0;
                foreach (JToken token in items)
                {
                    JObject item = token as JObject;
                    string id = item?.Value<string>("candidateId")?.Trim() ?? string.Empty;
                    if (item == null || id.Length == 0 || !seen.Add(id))
                    {
                        error = "候选 candidateId 缺失或重复。";
                        return false;
                    }
                    // ABC_RESPONSE_REJECT_HIDDEN: hidden candidates are never accepted.
                    if (item.Value<bool?>("hidden") == true)
                    {
                        error = "候选不得以 hidden=true 静默隐藏。";
                        return false;
                    }
                    // ABC_RESPONSE_REJECT_ACCEPTED: provider cannot grant final authority.
                    if (string.Equals(item.Value<string>("status"), "accepted", StringComparison.OrdinalIgnoreCase))
                    {
                        error = "Provider 不能直接声明 accepted。";
                        return false;
                    }
                    string mechanism = item.Value<string>("mechanism")?.Trim() ?? string.Empty;
                    if (mechanism.Length == 0)
                    {
                        error = "候选缺少 mechanism。";
                        return false;
                    }
                    string playerValue = item.Value<string>("playerValue")?.Trim() ?? string.Empty;
                    int? playerDelightScore = item.Value<int?>("playerDelightScore");
                    string amplificationChain = item.Value<string>("amplificationChain")?.Trim() ?? string.Empty;
                    string selfCritique = item.Value<string>("selfCritique")?.Trim() ?? string.Empty;
                    int? selfCritiquePasses = item.Value<int?>("selfCritiquePasses");
                    string seedDraft = item.Value<string>("seedDraft")?.Trim() ?? string.Empty;
                    string expansionSet = item.Value<string>("expansionSet")?.Trim() ?? string.Empty;
                    string auditFindings = item.Value<string>("auditFindings")?.Trim() ?? string.Empty;
                    string playabilityBackpressure = item.Value<string>("playabilityBackpressure")?.Trim() ?? string.Empty;
                    string finalDecision = item.Value<string>("finalDecision")?.Trim() ?? string.Empty;
                    var deletedAnchors = item["deletedAnchors"] as JArray;
                    string novelMechanism = item.Value<string>("novelMechanism")?.Trim() ?? string.Empty;
                    string plausibilityRationale = item.Value<string>("plausibilityRationale")?.Trim() ?? string.Empty;
                    string counterplayInvariant = item.Value<string>("counterplayInvariant")?.Trim() ?? string.Empty;
                    int? surpriseScore = item.Value<int?>("surpriseScore");
                    int? plausibilityScore = item.Value<int?>("plausibilityScore");
                    string firstUseAffordance = item.Value<string>("firstUseAffordance")?.Trim() ?? string.Empty;
                    string partialUnderstandingPath = item.Value<string>("partialUnderstandingPath")?.Trim() ?? string.Empty;
                    string masteryDepth = item.Value<string>("masteryDepth")?.Trim() ?? string.Empty;
                    int? onboardingBurden = item.Value<int?>("onboardingBurden");
                    double? firstPayoffSeconds = item.Value<double?>("firstPayoffSeconds");
                    int? firstInputCount = item.Value<int?>("firstInputCount");
                    string preservedIdentity = item.Value<string>("preservedIdentity")?.Trim() ?? string.Empty;
                    string preservedRole = item.Value<string>("preservedRole")?.Trim() ?? string.Empty;
                    string requestedFormFactor = item.Value<string>("requestedFormFactor")?.Trim() ?? string.Empty;
                    bool? formFactorPreserved = item.Value<bool?>("formFactorPreserved");
                    string mechanismDelta = item.Value<string>("mechanismDelta")?.Trim() ?? string.Empty;
                    string concretePlayerScenario = item.Value<string>("concretePlayerScenario")?.Trim() ?? string.Empty;
                    string inputSequence = item.Value<string>("inputSequence")?.Trim() ?? string.Empty;
                    string visibleFeedback = item.Value<string>("visibleFeedback")?.Trim() ?? string.Empty;
                    string acceptabilityRationale = item.Value<string>("acceptabilityRationale")?.Trim() ?? string.Empty;
                    int? concretenessScore = item.Value<int?>("concretenessScore");
                    int? acceptabilityScore = item.Value<int?>("acceptabilityScore");
                    JArray iterationTrace = item["iterationTrace"] as JArray;
                    var parsedIterationTrace = new List<ESABCInnovationRound>();
                    bool iterationTraceValid = iterationTrace != null && iterationTrace.Count >= 12;
                    if (iterationTraceValid)
                    {
                        var roundIds = new HashSet<int>();
                        foreach (JToken roundToken in iterationTrace)
                        {
                            JObject round = roundToken as JObject;
                            int? roundId = round?.Value<int?>("roundId");
                            string parentCandidateId = round?.Value<string>("parentCandidateId")?.Trim() ?? string.Empty;
                            string branchId = round?.Value<string>("branchId")?.Trim() ?? string.Empty;
                            string branchReason = round?.Value<string>("branchReason")?.Trim() ?? string.Empty;
                            string concreteChange = round?.Value<string>("concreteChange")?.Trim() ?? string.Empty;
                            int? playerAcceptability = round?.Value<int?>("playerAcceptability");
                            string keepOrDiscardReason = round?.Value<string>("keepOrDiscardReason")?.Trim() ?? string.Empty;
                            string decision = round?.Value<string>("decision")?.Trim().ToLowerInvariant() ?? string.Empty;
                            if (round == null || !roundId.HasValue || roundId.Value < 1 || !roundIds.Add(roundId.Value)
                                || parentCandidateId.Length == 0 || branchId.Length == 0 || branchReason.Length == 0
                                || concreteChange.Length == 0 || !playerAcceptability.HasValue || playerAcceptability.Value < 0 || playerAcceptability.Value > 100
                                || keepOrDiscardReason.Length == 0 || (decision != "keep" && decision != "discard"))
                            {
                                iterationTraceValid = false;
                                break;
                            }
                            parsedIterationTrace.Add(new ESABCInnovationRound
                            {
                                RoundId = roundId.Value,
                                ParentCandidateId = parentCandidateId,
                                BranchId = branchId,
                                BranchReason = branchReason,
                                ConcreteChange = concreteChange,
                                PlayerAcceptability = playerAcceptability.Value,
                                KeepOrDiscardReason = keepOrDiscardReason,
                                Decision = decision
                            });
                        }
                        if (iterationTraceValid && !roundIds.Contains(1)) iterationTraceValid = false;
                    }
                    JObject modeScores = item["modeScores"] as JObject;
                    string risks = item.Value<string>("risks")?.Trim() ?? string.Empty;
                    string counterplay = item.Value<string>("counterplay")?.Trim() ?? string.Empty;
                    string validationMetric = item.Value<string>("validationMetric")?.Trim() ?? string.Empty;
                    string[] scoreNames = { "delight", "smoothness", "presentation", "skillCeiling", "joyLoop", "first10sMoment", "expressionCeiling", "noveltyDelta", "counterplayClarity", "depth", "breakthrough", "reusability", "longevity", "projectFit", "completeness", "safety", "closure" };
                    var parsedScores = new Dictionary<string, int>(StringComparer.Ordinal);
                    if (modeScores != null)
                    {
                        foreach (string scoreName in scoreNames)
                        {
                            int? score = modeScores.Value<int?>(scoreName);
                            if (!score.HasValue || score.Value < 0 || score.Value > 100)
                            {
                                error = "候选 modeScores 必须完整提供 17 项 0-100 评分。";
                                return false;
                            }
                            parsedScores[scoreName] = score.Value;
                        }
                    }
                    int amplificationStages = amplificationChain.Split(new[] { "→", "->" }, StringSplitOptions.None).Length;
                    bool selfCritiqueShapeValid = selfCritique.IndexOf("weakest", StringComparison.OrdinalIgnoreCase) >= 0
                        && selfCritique.IndexOf("repair", StringComparison.OrdinalIgnoreCase) >= 0
                        && selfCritique.IndexOf("ranking", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool creativeNoveltyValid = mode != "creative-divergence" || (deletedAnchors != null && deletedAnchors.Count >= 2 && novelMechanism.Length > 0 && plausibilityRationale.Length > 0 && counterplayInvariant.Length > 0 && surpriseScore.HasValue && surpriseScore.Value >= 70 && plausibilityScore.HasValue && plausibilityScore.Value >= 60);
                    bool integrityValid = preservedIdentity.Length > 0 && preservedRole.Length > 0 && requestedFormFactor.Length > 0 && formFactorPreserved == true && mechanismDelta.Length > 0 && concretePlayerScenario.Length > 0 && inputSequence.Length > 0 && visibleFeedback.Length > 0 && acceptabilityRationale.Length > 0 && concretenessScore.HasValue && concretenessScore.Value >= 70 && concretenessScore.Value <= 100 && acceptabilityScore.HasValue && acceptabilityScore.Value >= 60 && acceptabilityScore.Value <= 100;
                    if (playerValue.Length == 0 || !playerDelightScore.HasValue || playerDelightScore.Value < 0 || playerDelightScore.Value > 100 || amplificationChain.Length < 32 || amplificationStages < 4 || selfCritique.Length < 32 || !selfCritiqueShapeValid || !selfCritiquePasses.HasValue || selfCritiquePasses.Value < 2 || selfCritiquePasses.Value > 4 || seedDraft.Length == 0 || expansionSet.Length == 0 || auditFindings.Length == 0 || playabilityBackpressure.Length == 0 || finalDecision.Length == 0 || deletedAnchors == null || deletedAnchors.Count == 0 || novelMechanism.Length == 0 || plausibilityRationale.Length == 0 || counterplayInvariant.Length == 0 || !surpriseScore.HasValue || surpriseScore.Value < 0 || surpriseScore.Value > 100 || !plausibilityScore.HasValue || plausibilityScore.Value < 0 || plausibilityScore.Value > 100 || !creativeNoveltyValid || !integrityValid || !iterationTraceValid || firstUseAffordance.Length == 0 || partialUnderstandingPath.Length == 0 || masteryDepth.Length == 0 || !onboardingBurden.HasValue || onboardingBurden.Value < 0 || onboardingBurden.Value > 100 || !firstPayoffSeconds.HasValue || firstPayoffSeconds.Value < 0 || firstPayoffSeconds.Value > 10 || !firstInputCount.HasValue || firstInputCount.Value < 1 || firstInputCount.Value > 8 || modeScores == null || risks.Length == 0 || counterplay.Length == 0 || validationMetric.Length == 0)
                    {
                        error = "候选必须补齐五阶段证据与大胆但有依据证据；创意模式还必须满足至少两个删除锚点、惊喜分>=70、合理性分>=60。";
                        return false;
                    }
                    string[] focusNames = mode == "creative-divergence"
                        ? new[] { "delight", "smoothness", "presentation", "skillCeiling", "joyLoop", "first10sMoment", "expressionCeiling", "noveltyDelta", "counterplayClarity" }
                        : mode == "engineering"
                            ? new[] { "depth", "breakthrough", "reusability", "longevity", "counterplayClarity" }
                            : new[] { "projectFit", "completeness", "safety", "closure", "reusability" };
                    int minimumFocusScore = focusNames.Select(name => parsedScores[name]).Min();
                    int focusScore = (int)Math.Round(focusNames.Select(name => parsedScores[name]).Average());
                    int focusScoreSpread = focusNames.Select(name => parsedScores[name]).Max() - minimumFocusScore;
                    if (focusScoreSpread <= 2)
                    {
                        error = "候选模式焦点评分区分度不足（最大最小差值必须大于 2），必须体现真实取舍。";
                        return false;
                    }
                    var proposedChanges = new List<ESABCGenerationChange>();
                    JArray changes = item["proposedChanges"] as JArray;
                    if (changes != null)
                    {
                        if (changes.Count > 32)
                        {
                            error = "候选 proposedChanges 单候选最多 32 个文件。";
                            return false;
                        }
                        var changePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (JToken changeToken in changes)
                        {
                            JObject change = changeToken as JObject;
                            string path = change?.Value<string>("path")?.Trim() ?? string.Empty;
                            string changeId = change?.Value<string>("changeId")?.Trim() ?? string.Empty;
                            string afterContent = change?.Value<string>("afterContent") ?? string.Empty;
                            string[] pathSegments = path.Replace('\\', '/').Split('/');
                            bool hasTraversal = pathSegments.Any(segment => string.Equals(segment, "..", StringComparison.Ordinal));
                            if (change == null || path.Length == 0 || Path.IsPathRooted(path)
                                || hasTraversal
                                || changeId.Length == 0 || !changePaths.Add(path))
                            {
                                error = "候选 proposedChanges 必须使用项目相对路径和非空 changeId。";
                                return false;
                            }
                            if (afterContent.Length > 2000000)
                            {
                                error = "候选 proposedChanges 单文件内容超过 2000000 字符。";
                                return false;
                            }
                            totalProposedChangeFiles++;
                            totalProposedChangeCharacters += afterContent.Length;
                            if (totalProposedChangeFiles > MaximumTotalProposedChangeFiles)
                            {
                                error = "候选 proposedChanges 总文件数超过 32。";
                                return false;
                            }
                            if (totalProposedChangeCharacters > MaximumTotalProposedChangeCharacters)
                            {
                                error = "候选 proposedChanges 总字符数超过 64000。";
                                return false;
                            }
                            proposedChanges.Add(new ESABCGenerationChange
                            {
                                Path = path,
                                ChangeId = changeId,
                                AfterContent = afterContent
                            });
                        }
                    }
                    candidates.Add(new ESABCGenerationCandidate
                    {
                        CandidateId = id,
                        Mechanism = mechanism,
                        PlayerValue = playerValue,
                        PlayerDelightScore = playerDelightScore.Value,
                        ModeScores = parsedScores,
                        AmplificationChain = amplificationChain,
                        SelfCritique = selfCritique,
                        SelfCritiquePasses = selfCritiquePasses.Value,
                        SeedDraft = seedDraft,
                        ExpansionSet = expansionSet,
                        AuditFindings = auditFindings,
                        PlayabilityBackpressure = playabilityBackpressure,
                        FinalDecision = finalDecision,
                        DeletedAnchors = deletedAnchors?.Values<string>().ToList() ?? new List<string>(),
                        NovelMechanism = novelMechanism,
                        PlausibilityRationale = plausibilityRationale,
                        CounterplayInvariant = counterplayInvariant,
                        SurpriseScore = surpriseScore.Value,
                        PlausibilityScore = plausibilityScore.Value,
                        FirstUseAffordance = firstUseAffordance,
                        PartialUnderstandingPath = partialUnderstandingPath,
                        MasteryDepth = masteryDepth,
                        OnboardingBurden = onboardingBurden.Value,
                        FirstPayoffSeconds = firstPayoffSeconds.Value,
                        FirstInputCount = firstInputCount.Value,
                        PreservedIdentity = preservedIdentity,
                        PreservedRole = preservedRole,
                        RequestedFormFactor = requestedFormFactor,
                        FormFactorPreserved = formFactorPreserved.Value,
                        MechanismDelta = mechanismDelta,
                        ConcretePlayerScenario = concretePlayerScenario,
                        InputSequence = inputSequence,
                        VisibleFeedback = visibleFeedback,
                        AcceptabilityRationale = acceptabilityRationale,
                        ConcretenessScore = concretenessScore.Value,
                        AcceptabilityScore = acceptabilityScore.Value,
                        IterationTrace = parsedIterationTrace,
                        LineageRoot = id,
                        LineageDepth = parsedIterationTrace.Count,
                        FocusScore = focusScore,
                        MinimumFocusScore = minimumFocusScore,
                        QualityStatus = minimumFocusScore < 60 ? "needs-deepening" : "mode-ready",
                        Risks = risks,
                        Counterplay = counterplay,
                        ValidationMetric = validationMetric,
                        ProposedChanges = proposedChanges
                    });
                }
                string canonical = JsonConvert.SerializeObject(new { generationMode = mode, status = "candidate", candidates }, Formatting.None);
                envelope = new ESABCGenerationResponseEnvelope
                {
                    GenerationMode = mode,
                    Status = "candidate",
                    Candidates = candidates,
                    CandidateSetHash = Sha256(canonical),
                    AuditDeferred = true,
                    FinalAuthority = "ABCD-audit-only"
                };
                return true;
            }
            catch (JsonException)
            {
                error = "ABC Provider 回执 JSON 无效。";
                return false;
            }
        }

        /// <summary>
        /// Reads one exact managed operation receipt. It never scans sibling operations and never
        /// accepts a mutable source path. The caller still has to invoke ABCD audit before apply.
        /// </summary>
        public static bool TryReadProviderResponseReceipt(
            string operationDirectory,
            out string response,
            out string error)
        {
            response = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(operationDirectory) || !Path.IsPathRooted(operationDirectory))
            {
                error = "Provider 回执目录必须是绝对路径。";
                return false;
            }
            string directory;
            try { directory = Path.GetFullPath(operationDirectory); }
            catch (Exception) { error = "Provider 回执目录无效。"; return false; }
            string receiptPath = Path.Combine(directory, "result.json");
            if (!File.Exists(receiptPath))
            {
                error = "未找到精确受管操作 result.json。";
                return false;
            }
            try
            {
                string json = File.ReadAllText(receiptPath, new UTF8Encoding(false, true));
                JObject root = JObject.Parse(json);
                response = FirstString(root, "response", "output", "aggregated_output",
                    "result.output", "result.response", "message.text", "message.content");
                if (string.IsNullOrWhiteSpace(response))
                {
                    error = "受管回执存在，但没有可解析的 Provider 文本。";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                error = "受管 Provider 回执不可读取：" + exception.GetBaseException().Message;
                return false;
            }
        }

        private static string ExtractJson(string response)
        {
            if (response.StartsWith("{") && response.EndsWith("}")) return response;
            int start = response.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (start < 0) start = response.IndexOf("```", StringComparison.Ordinal);
            if (start < 0) return string.Empty;
            start = response.IndexOf('{', start);
            int end = response.LastIndexOf('}');
            return start >= 0 && end > start ? response.Substring(start, end - start + 1) : string.Empty;
        }

        private static string FirstString(JObject root, params string[] paths)
        {
            foreach (string path in paths ?? Array.Empty<string>())
            {
                JToken token = root.SelectToken(path);
                if (token == null || token.Type == JTokenType.Null) continue;
                string value = token.Type == JTokenType.String ? token.Value<string>() : token.ToString(Formatting.None);
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
            return string.Empty;
        }

        private static string Sha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(b => b.ToString("x2")));
        }

        private static bool IsGenerationMode(string value)
            => value == "creative-divergence" || value == "engineering" || value == "stable";

        private static bool IsAcceptanceProfile(string value)
            => value == "shallow-fast" || value == "full-depth" || value == "core-high-risk";

        private static string ModeInstruction(string mode)
        {
            switch (mode)
            {
                case "creative-divergence":
                    return "创新型：至少发散 7 个真正不同的方向。先写玩家幻想、前10秒名场面、输入节奏、连续反馈和高手表达，再补最小风险说明。必须反复放大一个动作：一按键要能串联多个机制、视觉高潮和下一步选择；优先爽感、丝滑、表现力、上限和新鲜感，风险只标记不提前隐藏。推荐权重：joyLoop 30%、first10sMoment 25%、expressionCeiling 20%、noveltyDelta 15%、counterplayClarity 10%。";
                case "engineering":
                    return "工程型：至少发散 5 个方向。每个方向都要证明真实机制深度、突破点、可复用接口、长生命周期、状态/所有权/确定性、峰值预算、恢复和反制；把一个核心机制向系统深处放大，而不是堆更多功能。推荐权重：depth 25%、breakthrough 25%、reusability 20%、longevity 15%、counterplayClarity 15%。";
                default:
                    return "稳定型：至少发散 5 个方向。每个方向都要说明如何理解并接纳当前项目、覆盖输入到结果的完整闭环、兼容/回滚/证据/安全边界，以及如何批量复用产生丰富内容；稳定不是缩水，而是可持续交付。推荐权重：projectFit 25%、completeness 25%、safety 20%、closure 20%、reusability 10%。";
            }
        }
    }

    [Serializable]
    public sealed class ESABCGenerationCandidate
    {
        public string CandidateId;
        public string Mechanism;
        public string PlayerValue;
        public int PlayerDelightScore;
        public Dictionary<string, int> ModeScores = new Dictionary<string, int>();
        public string AmplificationChain;
        public string SelfCritique;
        public int SelfCritiquePasses;
        public string SeedDraft;
        public string ExpansionSet;
        public string AuditFindings;
        public string PlayabilityBackpressure;
        public string FinalDecision;
        public List<string> DeletedAnchors = new List<string>();
        public string NovelMechanism;
        public string PlausibilityRationale;
        public string CounterplayInvariant;
        public int SurpriseScore;
        public int PlausibilityScore;
        public string FirstUseAffordance;
        public string PartialUnderstandingPath;
        public string MasteryDepth;
        public int OnboardingBurden;
        public double FirstPayoffSeconds;
        public int FirstInputCount;
        public string PreservedIdentity;
        public string PreservedRole;
        public string RequestedFormFactor;
        public bool FormFactorPreserved;
        public string MechanismDelta;
        public string ConcretePlayerScenario;
        public string InputSequence;
        public string VisibleFeedback;
        public string AcceptabilityRationale;
        public int ConcretenessScore;
        public int AcceptabilityScore;
        public List<ESABCInnovationRound> IterationTrace = new List<ESABCInnovationRound>();
        public string LineageRoot;
        public int LineageDepth;
        public int FocusScore;
        public int MinimumFocusScore;
        public string QualityStatus;
        public string Risks;
        public string Counterplay;
        public string ValidationMetric;
        public List<ESABCGenerationChange> ProposedChanges = new List<ESABCGenerationChange>();
    }

    [Serializable]
    public sealed class ESABCGenerationChange
    {
        public string Path;
        public string ChangeId;
        public string AfterContent;
    }

    [Serializable]
    public sealed class ESABCInnovationRound
    {
        public int RoundId;
        public string ParentCandidateId;
        public string BranchId;
        public string BranchReason;
        public string ConcreteChange;
        public int PlayerAcceptability;
        public string KeepOrDiscardReason;
        public string Decision;
    }

    [Serializable]
    public sealed class ESABCGenerationResponseEnvelope
    {
        public string GenerationMode;
        public string Status;
        public List<ESABCGenerationCandidate> Candidates;
        public string CandidateSetHash;
        public bool AuditDeferred;
        public string FinalAuthority;
    }
}
