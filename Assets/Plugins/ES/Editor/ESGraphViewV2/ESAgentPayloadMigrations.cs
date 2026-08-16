using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using GraphAsset = global::ES.ESGraphAssetBase;

namespace ES.EditorInternal
{
    internal sealed class ESAgentConstraintPayloadV1
    {
        public int schemaVersion = 1;
        public ESAgentConstraintKind kind;
        public string statement;
        public string rationale;
        public string verification;
    }

    internal static class ESAgentConstraintPayloadMigration
    {
        public static bool TryMigrate(string payloadJson, out ESAgentConstraintPayload payload, out string error)
        {
            payload = null;
            if (!ESAgentAuthoringGraphValidator.TryRead(payloadJson,
                    out ESAgentConstraintPayloadV1 v1, out error))
                return false;
            if (v1.schemaVersion != 1)
            {
                error = "不支持的 Constraint Payload Schema：" + v1.schemaVersion;
                return false;
            }
            payload = new ESAgentConstraintPayload
            {
                kind = v1.kind,
                scope = ESAgentConstraintScope.WholeArtifact,
                combinationMode = ESAgentConstraintCombinationMode.AllOf,
                priority = 50,
                combinationGroup = string.Empty,
                statement = v1.statement,
                rationale = v1.rationale,
                verification = v1.verification
            };
            return ESAgentConstraintContractValidator.TryValidate(payload, out error);
        }
    }

    [Serializable]
    internal sealed class ESAgentAICommandOutputPayloadV1
    {
        public int schemaVersion = 1;
        public string commandName;
        public string targetProjectPath;
        public ESAgentArtifactOperationMode operationMode;
        public string commandType;
        public string defaultWrite;
        public string riskLevel;
        public string purpose;
        public string expectedInputs;
        public string executionOutline;
        public string acceptanceCriteria;
        public string requiredSections;
    }

    [Serializable]
    internal sealed class ESAgentSkillOutputPayloadV1
    {
        public int schemaVersion = 1;
        public string skillName;
        public string targetProjectPath;
        public ESAgentArtifactOperationMode operationMode;
        public string description;
        public string triggerScenarios;
        public string workflow;
        public string nonGoals;
        public string validationSteps;
        public string defaultPrompt;
        public bool includeAgentsMetadata;
        public bool includeReferences;
        public bool includeScripts;
    }

    internal static class ESAgentOutputPayloadMigration
    {
        [Serializable]
        private sealed class SchemaHeader
        {
            public int schemaVersion = 0;
        }

        public static bool TryMigrateAICommand(string payloadJson,
            out ESAgentAICommandOutputPayload payload, out string error)
        {
            payload = null;
            if (!TryReadSchema(payloadJson, out int schemaVersion, out error))
                return false;
            if (schemaVersion == ESAgentAICommandOutputPayload.CurrentSchemaVersion)
            {
                payload = JsonUtility.FromJson<ESAgentAICommandOutputPayload>(payloadJson);
                return ESAgentOutputContractValidator.TryValidate(payload, out error);
            }
            if (schemaVersion != 1)
            {
                error = "不支持的 AICommand Payload Schema：" + schemaVersion;
                return false;
            }

            ESAgentAICommandOutputPayloadV1 v1 = JsonUtility.FromJson<ESAgentAICommandOutputPayloadV1>(payloadJson);
            payload = new ESAgentAICommandOutputPayload
            {
                commandName = v1.commandName,
                targetProjectPath = v1.targetProjectPath,
                operationMode = v1.operationMode,
                commandIntent = ParseCommandIntent(v1.commandType),
                writeAuthorization = ParseWriteAuthorization(v1.defaultWrite),
                riskLevel = ParseRiskLevel(v1.riskLevel),
                failurePolicy = ParseWriteAuthorization(v1.defaultWrite) == ESAgentWriteAuthorization.ScopedWrites
                    ? ESAgentFailurePolicy.RollbackAndReport
                    : ESAgentFailurePolicy.StopAndReport,
                purpose = v1.purpose,
                expectedInputs = v1.expectedInputs,
                allowedWriteScopes = string.IsNullOrWhiteSpace(v1.defaultWrite)
                    ? "未授权写入；需要修改时必须重新取得用户确认。"
                    : v1.defaultWrite,
                executionOutline = v1.executionOutline,
                acceptanceCriteria = v1.acceptanceCriteria,
                requiredSections = v1.requiredSections
            };
            return ESAgentOutputContractValidator.TryValidate(payload, out error);
        }

        public static bool TryMigrateSkill(string payloadJson,
            out ESAgentSkillOutputPayload payload, out string error)
        {
            payload = null;
            if (!TryReadSchema(payloadJson, out int schemaVersion, out error))
                return false;
            if (schemaVersion == ESAgentSkillOutputPayload.CurrentSchemaVersion)
            {
                payload = JsonUtility.FromJson<ESAgentSkillOutputPayload>(payloadJson);
                return ESAgentOutputContractValidator.TryValidate(payload, out error);
            }
            if (schemaVersion != 1)
            {
                error = "不支持的 Agent Skill Payload Schema：" + schemaVersion;
                return false;
            }

            ESAgentSkillOutputPayloadV1 v1 = JsonUtility.FromJson<ESAgentSkillOutputPayloadV1>(payloadJson);
            payload = new ESAgentSkillOutputPayload
            {
                skillName = v1.skillName,
                targetProjectPath = v1.targetProjectPath,
                operationMode = v1.operationMode,
                description = v1.description,
                triggerScenarios = v1.triggerScenarios,
                workflow = v1.workflow,
                nonGoals = v1.nonGoals,
                validationSteps = v1.validationSteps,
                defaultPrompt = v1.defaultPrompt,
                includeAgentsMetadata = true,
                includeReferences = v1.includeReferences,
                includeScripts = v1.includeScripts
            };
            return ESAgentOutputContractValidator.TryValidate(payload, out error);
        }

        private static bool TryReadSchema(string payloadJson, out int schemaVersion, out string error)
        {
            schemaVersion = 0;
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                error = "Payload JSON 不能为空。";
                return false;
            }
            try
            {
                SchemaHeader header = JsonUtility.FromJson<SchemaHeader>(payloadJson);
                schemaVersion = header?.schemaVersion ?? 0;
                if (schemaVersion > 0)
                {
                    error = string.Empty;
                    return true;
                }
                error = "Payload SchemaVersion 无效。";
                return false;
            }
            catch (ArgumentException exception)
            {
                error = "Payload JSON 无效：" + exception.Message;
                return false;
            }
        }

        private static ESAgentCommandIntent ParseCommandIntent(string value)
        {
            if ((value ?? string.Empty).Contains("只读")) return ESAgentCommandIntent.ReadOnlyReview;
            if ((value ?? string.Empty).Contains("方案")) return ESAgentCommandIntent.PlanReview;
            if ((value ?? string.Empty).Contains("信息")) return ESAgentCommandIntent.ContextOnly;
            if ((value ?? string.Empty).Contains("交接")) return ESAgentCommandIntent.Handoff;
            return ESAgentCommandIntent.ControlledExecution;
        }

        private static ESAgentWriteAuthorization ParseWriteAuthorization(string value)
        {
            string normalized = value ?? string.Empty;
            if (normalized.StartsWith("否", StringComparison.Ordinal)) return ESAgentWriteAuthorization.NoWrites;
            if (normalized.Contains("用户确认") || normalized.Contains("需确认")
                || normalized.Contains("由用户") || normalized.Contains("由本节点"))
                return ESAgentWriteAuthorization.ConfirmBeforeWrite;
            return normalized.Contains("是") || normalized.Contains("允许")
                ? ESAgentWriteAuthorization.ScopedWrites
                : ESAgentWriteAuthorization.ConfirmBeforeWrite;
        }

        private static ESAgentRiskLevel ParseRiskLevel(string value)
        {
            string normalized = value ?? string.Empty;
            if (normalized.Contains("L3")) return ESAgentRiskLevel.L3;
            if (normalized.Contains("L2")) return ESAgentRiskLevel.L2;
            return ESAgentRiskLevel.L1;
        }
    }

    public sealed class ESAgentAICommandOutputV1ToV2Migrator : IESGraphNodeMigrator
    {
        public ESGraphDomainKey Domain => ESAgentGraphStableIds.Domain;
        public ESGraphNodeTypeKey NodeType => ESAgentGraphStableIds.Node(ESAgentGraphStableIds.AICommandOutputNode);
        public int FromVersion => 1;
        public int ToVersion => 2;
        public int Priority => 0;

        public bool TryMigrate(GraphAsset asset, ESGraphNodeRecord node, out string error)
        {
            if (asset == null || node == null)
            {
                error = "AICommand 节点迁移上下文为空。";
                return false;
            }
            if (!ESAgentOutputPayloadMigration.TryMigrateAICommand(node.payloadJson,
                    out ESAgentAICommandOutputPayload payload, out error))
                return false;
            return asset.UpdateNode(node.nodeId, node.TypeKey, ToVersion, node.title,
                JsonUtility.ToJson(payload), out error);
        }
    }

    public sealed class ESAgentSkillOutputV1ToV2Migrator : IESGraphNodeMigrator
    {
        public ESGraphDomainKey Domain => ESAgentGraphStableIds.Domain;
        public ESGraphNodeTypeKey NodeType => ESAgentGraphStableIds.Node(ESAgentGraphStableIds.AISkillOutputNode);
        public int FromVersion => 1;
        public int ToVersion => 2;
        public int Priority => 0;

        public bool TryMigrate(GraphAsset asset, ESGraphNodeRecord node, out string error)
        {
            if (asset == null || node == null)
            {
                error = "Agent Skill 节点迁移上下文为空。";
                return false;
            }
            if (!ESAgentOutputPayloadMigration.TryMigrateSkill(node.payloadJson,
                    out ESAgentSkillOutputPayload payload, out error))
                return false;
            return asset.UpdateNode(node.nodeId, node.TypeKey, ToVersion, node.title,
                JsonUtility.ToJson(payload), out error);
        }
    }

    public sealed class ESAgentConstraintV1ToV2Migrator : IESGraphNodeMigrator
    {
        public ESGraphDomainKey Domain => ESAgentGraphStableIds.Domain;
        public ESGraphNodeTypeKey NodeType => ESAgentGraphStableIds.Node(ESAgentGraphStableIds.ConstraintNode);
        public int FromVersion => 1;
        public int ToVersion => ESAgentConstraintPayload.CurrentSchemaVersion;
        public int Priority => 0;

        public bool TryMigrate(GraphAsset asset, ESGraphNodeRecord node, out string error)
        {
            if (asset == null || node == null)
            {
                error = "Constraint 节点迁移上下文为空。";
                return false;
            }
            if (!ESAgentConstraintPayloadMigration.TryMigrate(node.payloadJson,
                    out ESAgentConstraintPayload payload, out error))
                return false;
            return asset.UpdateNode(node.nodeId, node.TypeKey, ToVersion, node.title,
                JsonUtility.ToJson(payload), out error);
        }
    }

    internal static class ESGraphBranchPortCapacityMigration
    {
        public static bool TryMigrate(GraphAsset asset, ESGraphNodeRecord node,
            IEnumerable<string> outputKeys, out string error)
        {
            if (asset == null || node == null)
            {
                error = "分支节点迁移上下文为空。";
                return false;
            }
            HashSet<string> keys = new HashSet<string>(outputKeys ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            bool found = false;
            foreach (ESGraphPortRecord port in node.ports ?? new List<ESGraphPortRecord>())
            {
                if (port == null || port.direction != ESGraphPortDirection.Output
                    || !keys.Contains(port.stableKey))
                    continue;
                port.capacity = ESGraphPortCapacity.Multi;
                found = true;
            }
            if (!found)
            {
                error = "分支节点缺少待迁移的输出端点。";
                return false;
            }
            error = string.Empty;
            return true;
        }
    }

    public sealed class ESGenericBranchV1ToV2Migrator : IESGraphNodeMigrator
    {
        public ESGraphDomainKey Domain => ESGraphDomainKey.FromKind(ESGraphDomainKind.Generic);
        public ESGraphNodeTypeKey NodeType => ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.GenericBranch);
        public int FromVersion => 1;
        public int ToVersion => 2;
        public int Priority => 0;

        public bool TryMigrate(GraphAsset asset, ESGraphNodeRecord node, out string error)
            => ESGraphBranchPortCapacityMigration.TryMigrate(asset, node,
                new[] { "flow.true", "flow.false" }, out error);
    }

    public sealed class ESStoryConditionV1ToV2Migrator : IESGraphNodeMigrator
    {
        public ESGraphDomainKey Domain => ESGraphDomainKey.FromKind(ESGraphDomainKind.Story);
        public ESGraphNodeTypeKey NodeType => ESGraphNodeTypeKey.FromKind(ESGraphBuiltInNodeKind.StoryCondition);
        public int FromVersion => 1;
        public int ToVersion => 2;
        public int Priority => 0;

        public bool TryMigrate(GraphAsset asset, ESGraphNodeRecord node, out string error)
            => ESGraphBranchPortCapacityMigration.TryMigrate(asset, node,
                new[] { "flow.true", "flow.false" }, out error);
    }

    public sealed class ESAgentBranchV1ToV2Migrator : IESGraphNodeMigrator
    {
        public ESGraphDomainKey Domain => ESAgentGraphStableIds.Domain;
        public ESGraphNodeTypeKey NodeType => ESAgentGraphStableIds.Node(ESAgentGraphStableIds.BranchNode);
        public int FromVersion => 1;
        public int ToVersion => 2;
        public int Priority => 0;

        public bool TryMigrate(GraphAsset asset, ESGraphNodeRecord node, out string error)
            => ESGraphBranchPortCapacityMigration.TryMigrate(asset, node,
                new[] { ESAgentGraphStableIds.BranchMatchedPortKey,
                    ESAgentGraphStableIds.BranchDefaultPortKey,
                    ESAgentGraphStableIds.BranchFailurePortKey }, out error);
    }

    internal static class ESAISkillExecutionNodeV1ToV2Migration
    {
        public static bool TryMigrate(GraphAsset asset, ESGraphNodeRecord node,
            string expectedNodeTypeId, out string error)
        {
            if (asset == null || node == null
                || !string.Equals(node.typeId, expectedNodeTypeId, StringComparison.Ordinal))
            {
                error = "AISkill 执行节点迁移上下文或节点类型无效。";
                return false;
            }

            ESGraphPortDefinition[] expectedPorts;
            try
            {
                expectedPorts = ESAISkillExecutionNodeContractV2.CreatePorts(expectedNodeTypeId);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                error = exception.Message;
                return false;
            }

            var currentByKey = new Dictionary<string, ESGraphPortRecord>(StringComparer.Ordinal);
            foreach (ESGraphPortRecord port in node.ports ?? new List<ESGraphPortRecord>())
            {
                if (port == null || string.IsNullOrWhiteSpace(port.stableKey)
                    || !currentByKey.TryAdd(port.stableKey, port))
                {
                    error = "AISkill 执行节点包含空端口或重复 StableKey，不能安全升级："
                        + (node.title ?? node.nodeId);
                    return false;
                }
            }

            var expectedKeys = new HashSet<string>(
                expectedPorts.Select(port => port.stableKey), StringComparer.Ordinal);
            string[] unexpectedKeys = currentByKey.Keys.Where(key => !expectedKeys.Contains(key))
                .OrderBy(key => key, StringComparer.Ordinal).ToArray();
            if (unexpectedKeys.Length > 0)
            {
                error = "AISkill 执行节点包含 v2 合同之外的端口，升级不会自动删除或断开它们："
                    + string.Join(", ", unexpectedKeys);
                return false;
            }

            for (int i = 0; i < expectedPorts.Length; i++)
            {
                ESGraphPortDefinition expected = expectedPorts[i];
                if (currentByKey.TryGetValue(expected.stableKey, out ESGraphPortRecord current))
                {
                    string meaning = ESGraphEndpointRules.ResolveMeaning(expected.meaning,
                        expected.name, expected.stableKey);
                    if (!asset.CanUpdatePort(current.portId, expected.stableKey, meaning,
                            expected.valueTypeId, expected.direction, expected.capacity,
                            expected.aggregation, out error))
                    {
                        error = "端口 " + expected.stableKey + " 无法安全升级：" + error;
                        return false;
                    }
                }
                else if (!asset.CanAddPort(node.nodeId, expected, out error))
                {
                    error = "缺失端口 " + expected.stableKey + " 无法补齐：" + error;
                    return false;
                }
            }

            for (int i = 0; i < expectedPorts.Length; i++)
            {
                ESGraphPortDefinition expected = expectedPorts[i];
                if (currentByKey.TryGetValue(expected.stableKey, out ESGraphPortRecord current))
                {
                    string meaning = ESGraphEndpointRules.ResolveMeaning(expected.meaning,
                        expected.name, expected.stableKey);
                    if (!asset.UpdatePort(current.portId, expected.stableKey, expected.name, meaning,
                            expected.valueTypeId, expected.direction, expected.capacity,
                            expected.aggregation, out error))
                        return false;
                }
                else if (asset.AddPort(node.nodeId, expected, out error) == null)
                {
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }

    public abstract class ESAISkillExecutionNodeV1ToV2Migrator : IESGraphNodeMigrator
    {
        protected abstract string NodeTypeId { get; }

        public ESGraphDomainKey Domain => ESAgentGraphStableIds.Domain;
        public ESGraphNodeTypeKey NodeType => ESAgentGraphStableIds.Node(NodeTypeId);
        public int FromVersion => 1;
        public int ToVersion => ESAISkillExecutionNodeContractV2.Version;
        public int Priority => 0;

        public bool TryMigrate(GraphAsset asset, ESGraphNodeRecord node, out string error)
            => ESAISkillExecutionNodeV1ToV2Migration.TryMigrate(asset, node,
                NodeTypeId, out error);
    }

    public sealed class ESAISkillInputV1ToV2Migrator : ESAISkillExecutionNodeV1ToV2Migrator
    {
        protected override string NodeTypeId => ESAgentGraphStableIds.SkillInputNode;
    }

    public sealed class ESAISkillTaskV1ToV2Migrator : ESAISkillExecutionNodeV1ToV2Migrator
    {
        protected override string NodeTypeId => ESAgentGraphStableIds.SkillTaskNode;
    }

    public sealed class ESAISkillCallV1ToV2Migrator : ESAISkillExecutionNodeV1ToV2Migrator
    {
        protected override string NodeTypeId => ESAgentGraphStableIds.SkillCallNode;
    }

    public sealed class ESAISkillBranchV1ToV2Migrator : ESAISkillExecutionNodeV1ToV2Migrator
    {
        protected override string NodeTypeId => ESAgentGraphStableIds.SkillBranchNode;
    }

    public sealed class ESAISkillForEachV1ToV2Migrator : ESAISkillExecutionNodeV1ToV2Migrator
    {
        protected override string NodeTypeId => ESAgentGraphStableIds.SkillForEachNode;
    }

    public sealed class ESAISkillApprovalV1ToV2Migrator : ESAISkillExecutionNodeV1ToV2Migrator
    {
        protected override string NodeTypeId => ESAgentGraphStableIds.SkillApprovalNode;
    }

    public sealed class ESAISkillFanOutV1ToV2Migrator : ESAISkillExecutionNodeV1ToV2Migrator
    {
        protected override string NodeTypeId => ESAgentGraphStableIds.SkillFanOutNode;
    }

    public sealed class ESAISkillJoinV1ToV2Migrator : ESAISkillExecutionNodeV1ToV2Migrator
    {
        protected override string NodeTypeId => ESAgentGraphStableIds.SkillJoinNode;
    }

    public sealed class ESAISkillOutputV1ToV2Migrator : ESAISkillExecutionNodeV1ToV2Migrator
    {
        protected override string NodeTypeId => ESAgentGraphStableIds.SkillOutputNode;
    }

}
