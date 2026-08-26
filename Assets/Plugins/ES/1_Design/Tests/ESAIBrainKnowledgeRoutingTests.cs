using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace ES.Tests
{
    public sealed class ESAIBrainKnowledgeRoutingTests
    {
        private const string RouteProbeRegistryPath =
            "Documentation/AIKnowledge/RouteProbeRegistry.json";

        [Test]
        public void RegisteredRouteProbeRunner_ExecutesRegistryAndPasses()
        {
            ESAIBrainRouteProbeReport report = ESAIBrainRouteProbeRunner.Run();

            Assert.That(report.status, Is.EqualTo("Passed"), report.error);
            Assert.That(report.results.Count, Is.GreaterThanOrEqualTo(10));
            Assert.That(report.results, Has.All.Matches<ESAIBrainRouteProbeResult>(item => item.passed));
            Assert.That(report.evidenceBoundary, Is.EqualTo("static-routing-only"));
        }

        [Test]
        public void ProductionSurface_RegistersRouteProbeDiagnostic()
        {
            ESAIBrainProductionSurface surface = ESAIBrainCoordinator.DescribeProductionSurface(
                new[] { "knowledge-quality", "route-probe" });

            ESAIBrainCapabilityBinding diagnostic = surface.diagnostics.Single(item =>
                string.Equals(item.id, "diagnostic.knowledge-route-probes", StringComparison.Ordinal));
            Assert.That(diagnostic.status, Is.EqualTo("Registered"));
            Assert.That(diagnostic.capabilities, Does.Contain("runKnowledgeRouteProbes"));
            Assert.That(diagnostic.capabilities, Does.Contain("static-routing-only"));
        }

        [Test]
        public void FailureTelemetry_IsBoundedAndDoesNotRetainRawDetail()
        {
            ESAIBrainFailureTelemetry.ClearForTests();
            for (int index = 0; index < 300; index++)
                ESAIBrainFailureTelemetry.Record("NoKnowledgeRoute", "test", "secret-" + index, index.ToString());

            ESAIBrainFailureTelemetrySnapshot snapshot = ESAIBrainFailureTelemetry.Snapshot();

            Assert.That(snapshot.capacity, Is.EqualTo(256));
            Assert.That(snapshot.retainedEventCount, Is.EqualTo(256));
            Assert.That(snapshot.recent.Count, Is.EqualTo(32));
            Assert.That(snapshot.recent, Has.All.Matches<ESAIBrainFailureEvent>(item =>
                item.detailHash.Length == 64 && !item.detailHash.Contains("secret")));
            Assert.That(snapshot.counts.Single().count, Is.EqualTo(256));
        }

        [Test]
        public void CompletionDecision_DowngradedClaim_IsObservable()
        {
            ESAIBrainFailureTelemetry.ClearForTests();
            var decision = new ESAutomationCompletionDecision
            {
                runId = Guid.NewGuid().ToString("N"),
                accepted = true,
                evidenceStatus = "missing",
                runtimeStatus = "runtime-not-run",
            };

            decision.RefreshDecisionSemantics();
            ESAIBrainFailureTelemetrySnapshot snapshot = ESAIBrainFailureTelemetry.Snapshot();

            Assert.That(decision.accepted, Is.False);
            Assert.That(snapshot.counts.Any(item => item.category == "ClaimDowngraded"), Is.True);
        }

        [Test]
        public void Plan_MissingTask_IsNotMisclassifiedAsNoMatchingCommand()
        {
            ESAIBrainPlan plan = ESAIBrainCoordinator.Plan(new ESAIBrainRequest
            {
                objective = "验证缺失 TaskContract 的失败分类",
                routeKeys = new List<string> { "aibrain" },
                invocationId = Guid.NewGuid().ToString("N"),
            });

            Assert.That(plan.status, Is.EqualTo("PlanTaskUnavailable"));
        }
        [Test]
        public void Plan_RouteProbeRegistry_MatchesFixedCrossDomainExpectations()
        {
            JObject registry = LoadAndValidateRouteProbeRegistry();

            foreach (JObject probe in registry["probes"].OfType<JObject>())
            {
                string probeId = probe.Value<string>("probeId");
                string objective = probe.Value<string>("objective");
                string[] explicitRouteKeys = ReadStrings(probe["explicitRouteKeys"]);
                string[] expectedRouteKeys = ReadStrings(probe["expectedRouteKeys"]);
                JObject[] expectedKnowledge = probe["expectedKnowledgeTop3"].OfType<JObject>().ToArray();
                string[] forbiddenKnowledgeIds = ReadStrings(probe["forbiddenKnowledgeIds"]);
                bool zeroHitAllowed = probe.Value<bool>("zeroHitAllowed");
                int repeatCount = probe.Value<int>("repeatCount");

                for (int attempt = 0; attempt < repeatCount; attempt++)
                {
                    ESAIBrainPlan plan = PlanObjective(objective, explicitRouteKeys);
                    string context = probeId + " attempt " + (attempt + 1);

                    CollectionAssert.AreEqual(expectedRouteKeys, plan.routeKeys, context + " routeKeys");
                    CollectionAssert.AreEqual(
                        expectedKnowledge.Select(item => item.Value<string>("knowledgeId")),
                        plan.knowledge.Select(item => item.knowledgeId),
                        context + " Top-3");

                    foreach (JObject expectation in expectedKnowledge)
                    {
                        string knowledgeId = expectation.Value<string>("knowledgeId");
                        ESAIBrainKnowledgeBinding binding = plan.knowledge.Single(
                            item => string.Equals(item.knowledgeId, knowledgeId, StringComparison.Ordinal));
                        CollectionAssert.AreEqual(
                            ReadStrings(expectation["requiredReads"]),
                            binding.requiredReads,
                            context + " requiredReads " + knowledgeId);
                    }

                    foreach (string forbiddenKnowledgeId in forbiddenKnowledgeIds)
                    {
                        Assert.That(plan.knowledge.Select(item => item.knowledgeId),
                            Does.Not.Contain(forbiddenKnowledgeId),
                            context + " forbidden " + forbiddenKnowledgeId);
                    }

                    if (zeroHitAllowed)
                    {
                        Assert.That(plan.knowledge, Is.Empty, context);
                        Assert.That(plan.blockers,
                            Does.Contain("没有 Knowledge 条目匹配当前 routeKeys。"), context);
                    }
                    else
                    {
                        Assert.That(plan.knowledge.Count, Is.InRange(1, 3), context);
                    }
                }
            }
        }

        [Test]
        public void RouteProbeRegistry_RejectsUnknownRankingVersion()
        {
            JObject registry = LoadAndValidateRouteProbeRegistry();
            registry["rankingVersion"] = "future-ranking-v99";

            Assert.Throws<InvalidDataException>(() => ValidateRouteProbeRegistry(registry));
        }

        [Test]
        public void Plan_KnowledgeRoute_SelectsAtMostThreeDeterministically()
        {
            ESAIBrainPlan first = Plan("knowledge", "editor", "prefab", "serialization");
            ESAIBrainPlan second = Plan("knowledge", "editor", "prefab", "serialization");

            Assert.That(first.knowledge.Count, Is.InRange(1, 3));
            CollectionAssert.AreEqual(
                first.knowledge.Select(item => item.knowledgeId),
                second.knowledge.Select(item => item.knowledgeId));
        }

        [Test]
        public void Plan_PerformanceRoute_PrefersSpecializedKnowledge()
        {
            ESAIBrainPlan plan = Plan(
                "performance",
                "runtime-hot-container",
                "container-warmup",
                "steady-state-gc",
                "capacity-growth");

            Assert.That(plan.knowledge.Count, Is.InRange(1, 3));
            Assert.That(plan.knowledge[0].knowledgeId,
                Is.EqualTo("es.engineering.hot-path-container-performance-evidence.v1"));
        }

        [TestCase("Graph 边重连后 edge.order 和 EdgeId 应如何保持", "edge-order", "es.project.stable-graph-v2.v1")]
        [TestCase("Stable Graph 多步编辑失败后如何 Undo 回滚", "graph-undo", "es.project.stable-graph-v2.v1")]
        [TestCase("把旧 NodeRunner 迁移到 Stable Graph V2，何时必须停止", "graph-migration", "es.project.stable-graph-v2.v1")]
        [TestCase("Graph Bake Snapshot 的内容签名为什么在边顺序变化后失效", "graph-bake", "es.project.stable-graph-v2.v1")]
        [TestCase("Story Definition Snapshot 能否替换 DataInfo 作者权威", "story", "es.project.stable-graph-v2.v1")]
        [TestCase("Graph RunRecord 损坏后能否按目录猜测恢复", "automation-run-record", "es.project.automation-aibrain-graph.v1")]
        [TestCase("跨 Domain 粘贴节点时遇到未知端口怎么办", "graph-migration", "es.project.stable-graph-v2.v1")]
        [TestCase("Legacy GraphView 和 NodeRunnerSO 能否恢复使用", "legacy-graph", "es.project.stable-graph-v2.v1")]
        public void Plan_ObjectiveInference_SelectsExpectedDomainKnowledge(
            string objective, string expectedRouteKey, string expectedKnowledgeId)
        {
            ESAIBrainPlan plan = PlanObjective(objective);

            Assert.That(plan.routeKeys, Does.Contain(expectedRouteKey), objective);
            Assert.That(plan.knowledge.Select(item => item.knowledgeId),
                Does.Contain(expectedKnowledgeId), objective);
            if (objective.IndexOf("Snapshot", StringComparison.OrdinalIgnoreCase) >= 0)
                Assert.That(plan.knowledge.Select(item => item.knowledgeId),
                    Does.Not.Contain("es.engineering.task-read-snapshot.v1"), objective);
            if (objective.IndexOf("RunRecord", StringComparison.OrdinalIgnoreCase) >= 0)
                Assert.That(plan.knowledge.Select(item => item.knowledgeId),
                    Does.Not.Contain("es.engineering.hot-path-container-performance-evidence.v1"), objective);
            if (objective.IndexOf("Legacy", StringComparison.OrdinalIgnoreCase) >= 0)
                Assert.That(plan.knowledge.Select(item => item.knowledgeId),
                    Does.Not.Contain("es.project.automation-aibrain-graph.v1"), objective);
        }

        [TestCase("为什么第二次进入 Play 后静态事件会重复触发？", "es.unity.lifecycle-domain-reload.v1")]
        [TestCase("关闭 Domain Reload 后静态单例应该在哪里重置？", "es.unity.lifecycle-domain-reload.v1")]
        [TestCase("关闭 Scene Reload 时 OnDisable 和 OnDestroy 还会不会调用？", "es.unity.lifecycle-domain-reload.v1")]
        [TestCase("同一 MonoBehaviour 类型的多个实例能靠 Script Execution Order 排序吗？", "es.unity.lifecycle-domain-reload.v1")]
        [TestCase("RuntimeInitializeOnLoadMethod 之间能配置先后顺序吗？", "es.unity.lifecycle-domain-reload.v1")]
        [TestCase("Unity 编译后怎样证明 Domain Reload 已经完成？", "es.unity.compile-player-il2cpp-evidence.v1")]
        [TestCase("Enter Play Mode 的结果能不能证明 Player 启动也正确？", "es.unity.compile-player-il2cpp-evidence.v1")]
        [TestCase("ExecuteAlways 脚本能直接套用普通 MonoBehaviour 生命周期规则吗？", "es.unity.execute-always-prefab-stage.v1")]
        public void Plan_UnityLifecycleNaturalLanguageProbe_SelectsExpectedKnowledgeFirst(
            string objective, string expectedKnowledgeId)
        {
            ESAIBrainPlan plan = PlanObjective(objective);

            Assert.That(plan.routeKeys, Is.Not.Empty, objective);
            Assert.That(plan.knowledge.Count, Is.InRange(1, 3), objective);
            Assert.That(plan.knowledge[0].knowledgeId, Is.EqualTo(expectedKnowledgeId), objective);
            Assert.That(plan.knowledge.Select(item => item.knowledgeId),
                Does.Not.Contain("es.function-area.lifecycle.v1"), objective);
            Assert.That(plan.knowledge.Select(item => item.knowledgeId),
                Does.Not.Contain("es.unity.serialization-prefab-identity.v1"), objective);
            Assert.That(plan.knowledge.Select(item => item.knowledgeId),
                Does.Not.Contain("es.project.runtime-lifecycle-pool-arbitration.v1"), objective);
            Assert.That(plan.knowledge.Select(item => item.knowledgeId),
                Does.Not.Contain("es.project.scene-release-evidence.v1"), objective);
            Assert.That(plan.knowledge.Select(item => item.knowledgeId),
                Does.Not.Contain("es.function-area.release.v1"), objective);
        }

        [Test]
        public void Plan_PlayerStartup_DoesNotInferMonoBehaviourLifecycle()
        {
            ESAIBrainPlan plan = PlanObjective("Verify Unity Player startup evidence");

            Assert.That(plan.routeKeys, Does.Not.Contain("monobehaviour"));
            Assert.That(plan.routeKeys, Does.Not.Contain("lifecycle"));
            Assert.That(plan.knowledge[0].knowledgeId,
                Is.EqualTo("es.unity.compile-player-il2cpp-evidence.v1"));
        }

        [TestCase("ExecuteAlways 的 OnEnable 在编辑态能直接跑游戏初始化吗？", "execute-always")]
        [TestCase("ExecuteAlways 脚本为什么在 Prefab Stage 中改坏了预制体？", "prefab-stage")]
        [TestCase("Prefab Mode 中只检查 Application.isPlaying 安全吗？", "application-is-playing")]
        [TestCase("Application.IsPlaying(gameObject) 和 Application.isPlaying 有什么边界？", "playing-world")]
        [TestCase("ExecuteInEditMode 组件进入 Prefab Mode 时应该怎么办？", "execute-in-edit-mode")]
        [TestCase("Prefab Stage 的 isolation 和 in context 能当普通场景吗？", "prefab-stage")]
        [TestCase("ExecuteAlways 的 Update 为什么没有每个编辑器帧都调用？", "edit-mode")]
        [TestCase("ExecuteAlways 能否在编辑态直接修改共享 Material？", "execute-always")]
        [TestCase("Prefab Auto Save 开启时脚本自动改值怎么恢复？", "prefab-auto-save")]
        [TestCase("预制体模式下的对象是否属于 playing world？", "playing-world")]
        public void Plan_ExecuteAlwaysPrefabStageNaturalLanguageProbe_SelectsCanonicalKnowledge(
            string objective, string expectedRouteKey)
        {
            ESAIBrainPlan plan = PlanObjective(objective);

            Assert.That(plan.routeKeys, Does.Contain(expectedRouteKey), objective);
            Assert.That(plan.knowledge.Count, Is.InRange(1, 3), objective);
            Assert.That(plan.knowledge[0].knowledgeId,
                Is.EqualTo("es.unity.execute-always-prefab-stage.v1"), objective);
            Assert.That(plan.knowledge.Select(item => item.knowledgeId),
                Does.Not.Contain("es.function-area.lifecycle.v1"), objective);
            Assert.That(plan.knowledge.Select(item => item.knowledgeId),
                Does.Not.Contain("es.function-area.release.v1"), objective);
        }

        [TestCase("审计测试场景里的 Prefab Override", "es.unity.editor.project-scene-builder-authority.v1", true)]
        [TestCase("检查测试场景 Fixture 布局与稳定身份", "es.unity.editor.project-scene-builder-authority.v1", true)]
        [TestCase("为测试场景备份清单校验文件哈希", "es.unity.editor.project-scene-builder-authority.v1", true)]
        [TestCase("执行 Scene Guide 的场景验收", "es.project.scene-release-evidence.v1", true)]
        [TestCase("检查测试场景的目标平台 Profiler 证据", "es.project.scene-release-evidence.v1", true)]
        [TestCase("生成响应式 UI Prefab 和 Fixture Scene", "es.project.ui-automation-authoring.v1", true)]
        public void Plan_SceneAndUiNaturalLanguageProbe_SelectsExpectedKnowledge(
            string objective, string expectedKnowledgeId, bool expectedFirst)
        {
            ESAIBrainPlan first = PlanObjective(objective);
            ESAIBrainPlan second = PlanObjective(objective);

            Assert.That(first.routeKeys, Is.Not.Empty, objective);
            Assert.That(first.knowledge.Count, Is.InRange(1, 3), objective);
            Assert.That(first.knowledge.Select(item => item.knowledgeId),
                Does.Contain(expectedKnowledgeId), objective);
            CollectionAssert.AreEqual(
                first.knowledge.Select(item => item.knowledgeId),
                second.knowledge.Select(item => item.knowledgeId), objective);
            if (expectedFirst)
                Assert.That(first.knowledge[0].knowledgeId,
                    Is.EqualTo(expectedKnowledgeId), objective);
        }

        [Test]
        public void Plan_SceneFixtureLayout_DoesNotPullUiAutomation()
        {
            ESAIBrainPlan plan = PlanObjective("检查车辆测试场景 Fixture 布局");

            Assert.That(plan.knowledge.Select(item => item.knowledgeId),
                Does.Contain("es.unity.editor.project-scene-builder-authority.v1"));
            Assert.That(plan.knowledge.Select(item => item.knowledgeId),
                Does.Not.Contain("es.project.ui-automation-authoring.v1"));
        }

        [Test]
        public void Plan_UiFixtureLayout_DoesNotPullSceneBuilder()
        {
            ESAIBrainPlan plan = PlanObjective("生成 UI Fixture Scene 并检查响应式布局");

            Assert.That(plan.knowledge.Select(item => item.knowledgeId),
                Does.Contain("es.project.ui-automation-authoring.v1"));
            Assert.That(plan.knowledge.Select(item => item.knowledgeId),
                Does.Not.Contain("es.unity.editor.project-scene-builder-authority.v1"));
        }

        [Test]
        public void Plan_UiGraphicRaycaster_DoesNotInferPhysicsKnowledge()
        {
            ESAIBrainPlan plan = PlanObjective("检查 UI GraphicRaycaster 和 EventSystem 的点击命中");

            Assert.That(plan.routeKeys, Does.Contain("ui-automation"));
            Assert.That(plan.routeKeys, Does.Not.Contain("physics-3d"));
            Assert.That(plan.routeKeys, Does.Not.Contain("physics-query"));
            Assert.That(plan.knowledge.Select(item => item.knowledgeId),
                Does.Not.Contain("es.unity.physics-motion-authority.v1"));
        }

        [Test]
        public void Plan_RoutePlanRegisteredChain_IsDeterministicAndReadOnly()
        {
            ESAIBrainRequest request = new ESAIBrainRequest
            {
                objective = "create validate and replay one governed Skill",
                routeKeys = new List<string> { "skill", "validation", "static-replay" },
                skillNames = new List<string>
                {
                    "es-skill-creator", "es-skill-validator", "es-static-deep-replay",
                },
                invocationId = Guid.NewGuid().ToString("N"),
                routeProfileId = "governance",
            };

            ESAIBrainPlan first = ESAIBrainCoordinator.Plan(request);
            request.invocationId = Guid.NewGuid().ToString("N");
            ESAIBrainPlan second = ESAIBrainCoordinator.Plan(request);

            Assert.That(first.routePlan, Is.Not.Null);
            Assert.That(first.routePlan.executionEnabled, Is.False);
            Assert.That(first.routePlan.compatibility.productionRouteIntegrated, Is.False);
            Assert.That(first.routePlan.compatibility.globalP0Integrated, Is.False);
            Assert.That(first.routePlan.status, Is.EqualTo("EvidencePending"));
            Assert.That(first.routePlan.issues.Select(item => item.reasonCode),
                Does.Contain("ROUTE.GOAL_REVISION_REQUIRED"));
            CollectionAssert.AreEqual(
                new[] { "es-skill-creator", "es-skill-validator", "es-static-deep-replay" },
                first.routePlan.stages.Select(item => item.skillName));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 },
                first.routePlan.stages.Select(item => item.depth));
            Assert.That(first.routePlan.stages, Has.All.Matches<ESAIBrainRouteStage>(item =>
                item.executionStatus == "not-executed"));
            Assert.That(first.routePlan.routePlanHash, Is.EqualTo(second.routePlan.routePlanHash));
            Assert.That(first.routePlan.routePlanId, Is.EqualTo(second.routePlan.routePlanId));
            Assert.That(first.routePlan.compatibility.legacyPlanStatus, Is.EqualTo(first.status));
        }

        [Test]
        public void Plan_RoutePlanSnapshot_BindsCurrentHeadRegistryAndSourceSet()
        {
            ESAIBrainPlan plan = ESAIBrainCoordinator.Plan(new ESAIBrainRequest
            {
                objective = "author one governed Skill",
                routeKeys = new List<string> { "skill", "creator" },
                skillNames = new List<string> { "es-skill-creator" },
                invocationId = Guid.NewGuid().ToString("N"),
            });

            string registryPath = Path.Combine(ESCommandPalettePathPolicy.ProjectRoot,
                ESAIBrainCoordinator.RouteStageRegistryPath.Replace('/', Path.DirectorySeparatorChar));
            string registryHash;
            using (SHA256 sha = SHA256.Create())
                registryHash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(registryPath)))
                    .Replace("-", string.Empty).ToLowerInvariant();

            Assert.That(plan.routePlan.snapshot.head, Does.Match("^[a-f0-9]{40}$"));
            Assert.That(plan.routePlan.snapshot.registryHash, Is.EqualTo(registryHash));
            Assert.That(plan.routePlan.snapshot.sourceRefsHash, Does.Match("^[a-f0-9]{64}$"));
            Assert.That(plan.routePlan.snapshot.sourceRefs.Any(item =>
                item.projectPath == ESAIBrainCoordinator.RouteStageRegistryPath
                && item.sha256 == registryHash), Is.True);
        }

        [Test]
        public void Plan_RoutePlanUnregisteredStage_DoesNotChangeLegacyRouteStatus()
        {
            ESAIBrainPlan plan = ESAIBrainCoordinator.Plan(new ESAIBrainRequest
            {
                objective = "review AIBrain route authoring",
                routeKeys = new List<string> { "aibrain", "route" },
                skillNames = new List<string> { "es-aibrain-route-authoring" },
                invocationId = Guid.NewGuid().ToString("N"),
            });

            Assert.That(plan.routePlan.status, Is.EqualTo("NeedsRegistration"));
            Assert.That(plan.routePlan.issues.Select(item => item.reasonCode),
                Does.Contain("ROUTE.UNREGISTERED_STAGE"));
            Assert.That(plan.routePlan.compatibility.legacyPlanStatus, Is.EqualTo(plan.status));
            Assert.That(plan.routePlan.compatibility.projectionOnly, Is.True);
            Assert.That(plan.routePlan.executionEnabled, Is.False);
        }

        private static ESAIBrainPlan Plan(params string[] routeKeys)
        {
            return ESAIBrainCoordinator.Plan(new ESAIBrainRequest
            {
                objective = "验证 AIKnowledge 最小路由",
                routeKeys = routeKeys.ToList(),
                invocationId = Guid.NewGuid().ToString("N"),
            });
        }

        private static ESAIBrainPlan PlanObjective(string objective, params string[] routeKeys)
        {
            return ESAIBrainCoordinator.Plan(new ESAIBrainRequest
            {
                objective = objective,
                routeKeys = (routeKeys ?? Array.Empty<string>()).ToList(),
                invocationId = Guid.NewGuid().ToString("N"),
            });
        }

        private static JObject LoadAndValidateRouteProbeRegistry()
        {
            string fullPath = Path.Combine(
                ESCommandPalettePathPolicy.ProjectRoot,
                RouteProbeRegistryPath.Replace('/', Path.DirectorySeparatorChar));
            string text = File.ReadAllText(fullPath, new UTF8Encoding(false, true));
            JObject registry = JObject.Parse(text);
            ValidateRouteProbeRegistry(registry);
            return registry;
        }

        private static void ValidateRouteProbeRegistry(JObject registry)
        {
            if (registry == null) throw new InvalidDataException("Route probe registry is missing.");
            if (registry.Value<int?>("schemaVersion") != 1)
                throw new InvalidDataException("Unsupported route probe schemaVersion.");
            if (!string.Equals(registry.Value<string>("registryId"),
                    "esframework-knowledge-route-probes", StringComparison.Ordinal))
                throw new InvalidDataException("Unexpected route probe registryId.");
            if (!string.Equals(registry.Value<string>("lifecycleState"),
                    "operational-static", StringComparison.Ordinal))
                throw new InvalidDataException("Route probe registry is not operational-static.");
            if (!string.Equals(registry.Value<string>("ownerKnowledgeId"),
                    "es.knowledge.routing-quality.v1", StringComparison.Ordinal))
                throw new InvalidDataException("Unexpected route probe canonical owner.");
            if (!string.Equals(registry.Value<string>("rankingVersion"),
                    ESAIBrainCoordinator.KnowledgeRankingVersion, StringComparison.Ordinal))
                throw new InvalidDataException("Unsupported route probe rankingVersion.");
            if (!string.Equals(registry.Value<string>("knowledgeIndexPath"),
                    "Documentation/AIKnowledge/KnowledgeIndex.yaml", StringComparison.Ordinal))
                throw new InvalidDataException("Unexpected KnowledgeIndex path.");

            JObject consumers = registry["consumers"] as JObject
                ?? throw new InvalidDataException("Route probe consumers are missing.");
            if (!string.Equals(consumers.Value<string>("cliValidator"),
                    "Documentation/AIKnowledge/tools/Test-ESKnowledgeRouteProbeRegistry.ps1",
                    StringComparison.Ordinal)
                || !string.Equals(consumers.Value<string>("unityTestSource"),
                    "Assets/Plugins/ES/1_Design/Tests/ESAIBrainKnowledgeRoutingTests.cs",
                    StringComparison.Ordinal)
                || !string.Equals(consumers.Value<string>("unityTestMethod"),
                    nameof(Plan_RouteProbeRegistry_MatchesFixedCrossDomainExpectations),
                    StringComparison.Ordinal)
                || !string.Equals(consumers.Value<string>("bridgeOperation"),
                    "runKnowledgeRouteProbes", StringComparison.Ordinal)
                || !string.Equals(consumers.Value<string>("productionSurfaceId"),
                    "diagnostic.knowledge-route-probes", StringComparison.Ordinal))
                throw new InvalidDataException("Route probe consumer registration is invalid.");

            JObject[] probes = (registry["probes"] as JArray ?? new JArray())
                .OfType<JObject>().ToArray();
            if (probes.Length < 10) throw new InvalidDataException("At least 10 route probes are required.");
            string[] probeIds = probes.Select(item => item.Value<string>("probeId") ?? string.Empty).ToArray();
            if (probeIds.Any(string.IsNullOrWhiteSpace)
                || probeIds.Distinct(StringComparer.Ordinal).Count() != probeIds.Length)
                throw new InvalidDataException("Route probeId values must be non-empty and unique.");

            foreach (JObject probe in probes)
            {
                string probeId = probe.Value<string>("probeId") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(probe.Value<string>("objective")))
                    throw new InvalidDataException(probeId + " objective is missing.");
                if (!string.Equals(probe.Value<string>("evidenceBoundary"),
                        "static-routing-only", StringComparison.Ordinal))
                    throw new InvalidDataException(probeId + " evidenceBoundary is invalid.");
                int repeatCount = probe.Value<int?>("repeatCount") ?? 0;
                if (repeatCount < 2 || repeatCount > 5)
                    throw new InvalidDataException(probeId + " repeatCount is outside 2..5.");

                ReadStrings(probe["explicitRouteKeys"]);
                string[] expectedRouteKeys = ReadStrings(probe["expectedRouteKeys"]);
                string[] expectedKnowledgeIds = (probe["expectedKnowledgeTop3"] as JArray ?? new JArray())
                    .OfType<JObject>().Select(item => item.Value<string>("knowledgeId") ?? string.Empty).ToArray();
                string[] forbiddenKnowledgeIds = ReadStrings(probe["forbiddenKnowledgeIds"]);
                bool zeroHitAllowed = probe.Value<bool?>("zeroHitAllowed") ?? false;
                if (expectedKnowledgeIds.Length > 3
                    || expectedKnowledgeIds.Any(string.IsNullOrWhiteSpace)
                    || expectedKnowledgeIds.Distinct(StringComparer.Ordinal).Count() != expectedKnowledgeIds.Length)
                    throw new InvalidDataException(probeId + " expected Top-3 is invalid.");
                if (expectedKnowledgeIds.Intersect(forbiddenKnowledgeIds, StringComparer.Ordinal).Any())
                    throw new InvalidDataException(probeId + " expected and forbidden Knowledge overlap.");
                if (zeroHitAllowed != (expectedKnowledgeIds.Length == 0))
                    throw new InvalidDataException(probeId + " zero-hit contract is inconsistent.");
                if (!zeroHitAllowed && expectedRouteKeys.Length == 0)
                    throw new InvalidDataException(probeId + " expected routeKeys are missing.");

                foreach (JObject expectation in (probe["expectedKnowledgeTop3"] as JArray ?? new JArray())
                             .OfType<JObject>())
                {
                    string[] requiredReads = ReadStrings(expectation["requiredReads"]);
                    if (requiredReads.Length == 0)
                        throw new InvalidDataException(probeId + " requiredReads are missing.");
                    if (requiredReads.Any(Path.IsPathRooted)
                        || requiredReads.Any(path => path.Split('/', '\\').Contains("..")))
                        throw new InvalidDataException(probeId + " requiredReads must be project-relative.");
                }
            }
        }

        private static string[] ReadStrings(JToken token)
        {
            if (!(token is JArray array)) return Array.Empty<string>();
            string[] values = array.Values<string>().ToArray();
            if (values.Any(string.IsNullOrWhiteSpace)
                || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                throw new InvalidDataException("Registry string arrays must be non-empty sets.");
            return values;
        }
    }
}
