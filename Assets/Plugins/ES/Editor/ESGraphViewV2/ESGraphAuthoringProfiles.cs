using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES.EditorInternal
{
    public interface IESGraphNodeDefinition
    {
        ESGraphDomainKey Domain { get; }
        ESGraphNodeTypeKey NodeType { get; }
        int CurrentVersion { get; }
        int Priority { get; }
        string MenuPath { get; }
        string DisplayName { get; }
        string Description { get; }
        string BadgeText { get; }
        ESGraphNodeCategory Category { get; }
        ESGraphNodeTheme Theme { get; }
        Color CustomAccentColor { get; }
        IReadOnlyList<ESGraphPortDefinition> Ports { get; }
        string CreateDefaultPayload();
        void ValidateNode(ESGraphAsset asset, ESGraphNodeRecord node, List<ESGraphValidationIssue> issues);
    }

    public interface IESGraphNodeMigrator
    {
        ESGraphDomainKey Domain { get; }
        ESGraphNodeTypeKey NodeType { get; }
        int FromVersion { get; }
        int ToVersion { get; }
        int Priority { get; }
        bool TryMigrate(ESGraphAsset asset, ESGraphNodeRecord node, out string error);
    }

    public sealed class ESStableGraphNodeTemplate : IESGraphNodeDefinition
    {
        public ESGraphDomainKey Domain { get; }
        public ESGraphNodeTypeKey NodeType { get; }
        public int CurrentVersion { get; }
        public int Priority { get; }
        public string MenuPath { get; }
        public string TypeId => NodeType.StableId;
        public string DefaultTitle { get; }
        public string DefaultPayloadJson { get; }
        public string DisplayName => DefaultTitle;
        public string Description { get; }
        public string BadgeText { get; }
        public ESGraphNodeCategory Category { get; }
        public ESGraphNodeTheme Theme { get; }
        public Color CustomAccentColor { get; }
        public IReadOnlyList<ESGraphPortDefinition> Ports { get; }

        public ESStableGraphNodeTemplate(ESGraphDomainKind domainKind, ESGraphBuiltInNodeKind nodeKind,
            string menuPath, string defaultTitle, ESGraphNodeCategory category, ESGraphNodeTheme theme,
            params ESGraphPortDefinition[] ports)
            : this(ESGraphDomainKey.FromKind(domainKind), ESGraphNodeTypeKey.FromKind(nodeKind), menuPath,
                defaultTitle, string.Empty, category, theme, defaultTitle, string.Empty, 1, 0, default, ports)
        {
        }

        public ESStableGraphNodeTemplate(ESGraphDomainKind domainKind, ESGraphBuiltInNodeKind nodeKind,
            string menuPath, string defaultTitle, string defaultPayloadJson,
            ESGraphNodeCategory category, ESGraphNodeTheme theme, params ESGraphPortDefinition[] ports)
            : this(ESGraphDomainKey.FromKind(domainKind), ESGraphNodeTypeKey.FromKind(nodeKind), menuPath,
                defaultTitle, defaultPayloadJson, category, theme, defaultTitle, string.Empty, 1, 0, default, ports)
        {
        }

        public ESStableGraphNodeTemplate(ESGraphDomainKey domain, ESGraphNodeTypeKey nodeType,
            string menuPath, string defaultTitle, string defaultPayloadJson,
            ESGraphNodeCategory category, ESGraphNodeTheme theme, string badgeText,
            string description, int currentVersion, int priority, Color customAccentColor,
            params ESGraphPortDefinition[] ports)
        {
            if (!domain.IsValid)
                throw new ArgumentException("节点定义的领域标识非法。", nameof(domain));
            if (!nodeType.IsValid)
                throw new ArgumentException("节点定义的类型标识非法。", nameof(nodeType));
            if (currentVersion < 1)
                throw new ArgumentOutOfRangeException(nameof(currentVersion), "节点版本必须大于 0。");
            Domain = domain;
            NodeType = nodeType;
            CurrentVersion = currentVersion;
            Priority = priority;
            MenuPath = string.IsNullOrWhiteSpace(menuPath) ? defaultTitle : menuPath.Trim();
            DefaultTitle = string.IsNullOrWhiteSpace(defaultTitle) ? nodeType.StableId : defaultTitle.Trim();
            DefaultPayloadJson = defaultPayloadJson ?? string.Empty;
            Category = category;
            Theme = theme;
            BadgeText = string.IsNullOrWhiteSpace(badgeText) ? DefaultTitle : badgeText.Trim();
            Description = description?.Trim() ?? string.Empty;
            CustomAccentColor = customAccentColor;
            Ports = ports ?? Array.Empty<ESGraphPortDefinition>();
        }

        public string CreateDefaultPayload()
        {
            return DefaultPayloadJson;
        }

        public void ValidateNode(ESGraphAsset asset, ESGraphNodeRecord node,
            List<ESGraphValidationIssue> issues)
        {
        }
    }

    internal static class ESGraphChinesePresentation
    {
        public static string GetDomainKindName(ESGraphDomainKind kind)
        {
            switch (kind)
            {
                case ESGraphDomainKind.Generic: return "通用流程图";
                case ESGraphDomainKind.Story: return "剧情 / 任务与对话";
                case ESGraphDomainKind.BehaviorTree: return "行为树";
                case ESGraphDomainKind.AgentAuthoring: return "智能助手产物编排";
                default: return "扩展领域";
            }
        }

        public static string GetNodeCategoryName(ESGraphNodeCategory category)
        {
            switch (category)
            {
                case ESGraphNodeCategory.Entry: return "入口";
                case ESGraphNodeCategory.Exit: return "出口";
                case ESGraphNodeCategory.Flow: return "流程";
                case ESGraphNodeCategory.Branch: return "分支";
                case ESGraphNodeCategory.Merge: return "汇合";
                case ESGraphNodeCategory.Dialogue: return "对话";
                case ESGraphNodeCategory.Choice: return "选择";
                case ESGraphNodeCategory.Condition: return "条件";
                case ESGraphNodeCategory.Action: return "行为";
                case ESGraphNodeCategory.Composite: return "组合";
                case ESGraphNodeCategory.Decorator: return "装饰";
                case ESGraphNodeCategory.Reference: return "引用";
                case ESGraphNodeCategory.Constraint: return "约束";
                case ESGraphNodeCategory.Output: return "产物输出";
                case ESGraphNodeCategory.Validation: return "验证";
                case ESGraphNodeCategory.Custom: return "扩展分类";
                default: return "通用";
            }
        }

        public static string GetNodeTypeName(string domainId, string typeId)
        {
            if (ESGraphAuthoringRegistry.TryGetNodeDefinition(domainId, typeId, out IESGraphNodeDefinition definition))
                return definition.DisplayName;
            switch (ESGraphNodeTypeCatalog.GetKind(typeId))
            {
                case ESGraphBuiltInNodeKind.GenericFlow: return "流程节点";
                case ESGraphBuiltInNodeKind.GenericSource: return "起点";
                case ESGraphBuiltInNodeKind.GenericSink: return "终点";
                case ESGraphBuiltInNodeKind.GenericBranch: return "分支";
                case ESGraphBuiltInNodeKind.GenericMerge: return "汇合";
                case ESGraphBuiltInNodeKind.StoryStart: return "剧情开始";
                case ESGraphBuiltInNodeKind.StoryDialogue: return "对话";
                case ESGraphBuiltInNodeKind.StoryChoice: return "选择";
                case ESGraphBuiltInNodeKind.StoryCondition: return "条件";
                case ESGraphBuiltInNodeKind.StoryAction: return "行为";
                case ESGraphBuiltInNodeKind.StoryComplete: return "完成";
                case ESGraphBuiltInNodeKind.StoryFail: return "失败";
                case ESGraphBuiltInNodeKind.BehaviorRoot: return "根节点";
                case ESGraphBuiltInNodeKind.BehaviorSequence: return "顺序组合";
                case ESGraphBuiltInNodeKind.BehaviorSelector: return "选择组合";
                case ESGraphBuiltInNodeKind.BehaviorParallel: return "并行组合";
                case ESGraphBuiltInNodeKind.BehaviorDecorator: return "装饰节点";
                case ESGraphBuiltInNodeKind.BehaviorCondition: return "条件节点";
                case ESGraphBuiltInNodeKind.BehaviorAction: return "行为节点";
                case ESGraphBuiltInNodeKind.AgentGoal: return "生成目标";
                case ESGraphBuiltInNodeKind.AgentReference: return "引用资料";
                case ESGraphBuiltInNodeKind.AgentConstraint: return "生成约束";
                case ESGraphBuiltInNodeKind.AgentAICommandOutput: return "生成 AICommand 命令";
                case ESGraphBuiltInNodeKind.AgentSkillOutput: return "生成 Agent Skill 技能";
                case ESGraphBuiltInNodeKind.AgentValidation: return "验证与批准";
                default: return "自定义节点";
            }
        }

        public static string GetPortName(string value)
        {
            if (string.Equals(value, "True", StringComparison.OrdinalIgnoreCase)) return "成立";
            if (string.Equals(value, "False", StringComparison.OrdinalIgnoreCase)) return "不成立";
            return string.IsNullOrWhiteSpace(value) ? "未命名端口" : value;
        }

        public static string GetPortValueTypeName(string valueTypeId)
        {
            ESGraphPortValueKind kind = ESGraphPortValueCatalog.GetKind(valueTypeId);
            if (kind == ESGraphPortValueKind.Custom)
                return string.IsNullOrWhiteSpace(valueTypeId) ? "未分类" : "自定义数据";
            return GetPortValueKindName(kind);
        }

        public static string GetPortValueKindName(ESGraphPortValueKind kind)
        {
            switch (kind)
            {
                case ESGraphPortValueKind.Flow: return "流程";
                case ESGraphPortValueKind.Any: return "任意数据";
                case ESGraphPortValueKind.Boolean: return "布尔值";
                case ESGraphPortValueKind.Number: return "数值";
                case ESGraphPortValueKind.Text: return "文本";
                case ESGraphPortValueKind.Object: return "对象";
                case ESGraphPortValueKind.AgentContext: return "上下文";
                case ESGraphPortValueKind.AgentRequirement: return "需求";
                case ESGraphPortValueKind.AgentArtifact: return "候选产物";
                default: return "自定义数据";
            }
        }

        public static string GetDirectionName(ESGraphPortDirection direction)
        {
            return direction == ESGraphPortDirection.Input ? "输入" : "输出";
        }

        public static string GetCapacityName(ESGraphPortCapacity capacity)
        {
            return capacity == ESGraphPortCapacity.Multi ? "多连接" : "单连接";
        }
    }

    internal static class ESGraphNodeThemePalette
    {
        private static readonly Color Neutral = new Color(0.42f, 0.48f, 0.58f);

        public static Color GetAccentColor(IESGraphNodeDefinition definition)
        {
            if (definition == null)
                return Neutral;
            switch (definition.Theme)
            {
                case ESGraphNodeTheme.Primary: return new Color(0.25f, 0.55f, 0.96f);
                case ESGraphNodeTheme.Entry: return new Color(0.30f, 0.72f, 0.46f);
                case ESGraphNodeTheme.Exit: return new Color(0.82f, 0.38f, 0.38f);
                case ESGraphNodeTheme.Success: return new Color(0.32f, 0.74f, 0.45f);
                case ESGraphNodeTheme.Failure: return new Color(0.86f, 0.34f, 0.36f);
                case ESGraphNodeTheme.Decision: return new Color(0.95f, 0.63f, 0.22f);
                case ESGraphNodeTheme.Merge: return new Color(0.28f, 0.72f, 0.72f);
                case ESGraphNodeTheme.Dialogue: return new Color(0.35f, 0.62f, 0.90f);
                case ESGraphNodeTheme.Composite: return new Color(0.48f, 0.58f, 0.86f);
                case ESGraphNodeTheme.Reference: return new Color(0.28f, 0.75f, 0.72f);
                case ESGraphNodeTheme.Constraint: return new Color(0.95f, 0.63f, 0.22f);
                case ESGraphNodeTheme.CommandOutput: return new Color(0.65f, 0.43f, 0.94f);
                case ESGraphNodeTheme.SkillOutput: return new Color(0.83f, 0.39f, 0.72f);
                case ESGraphNodeTheme.Validation: return new Color(0.35f, 0.78f, 0.43f);
                case ESGraphNodeTheme.Custom:
                    return definition.CustomAccentColor.a > 0f ? definition.CustomAccentColor : Neutral;
                default: return Neutral;
            }
        }
    }

    public interface IESGraphAuthoringProfile
    {
        ESGraphDomainKey Domain { get; }
        string DisplayName { get; }
        string Description { get; }
        int Priority { get; }
        IReadOnlyList<IESGraphNodeDefinition> NodeDefinitions { get; }
        void Validate(ESGraphAsset asset, List<ESGraphValidationIssue> issues);
    }

    /// <summary>
    /// Optional domain policy for templates that intentionally expose a smaller,
    /// task-specific node palette while keeping the same serialized domain.
    /// The policy is evaluated only when the user opens node search.
    /// </summary>
    public interface IESGraphNodeAvailabilityPolicy
    {
        bool IsNodeAvailable(ESGraphAsset asset, IESGraphNodeDefinition definition);
    }

    public interface IESGraphPayloadInspector
    {
        ESGraphDomainKey Domain { get; }
        ESGraphNodeTypeKey NodeType { get; }
        int Priority { get; }
        VisualElement Create(string payloadJson, Action<string> commitPayload);
    }

    public static class ESGraphAuthoringRegistry
    {
        private readonly struct NodeRegistrationKey : IEquatable<NodeRegistrationKey>
        {
            public readonly ESGraphDomainKey Domain;
            public readonly ESGraphNodeTypeKey NodeType;

            public NodeRegistrationKey(ESGraphDomainKey domain, ESGraphNodeTypeKey nodeType)
            {
                Domain = domain;
                NodeType = nodeType;
            }

            public bool Equals(NodeRegistrationKey other)
            {
                return Domain.Equals(other.Domain) && NodeType.Equals(other.NodeType);
            }

            public override bool Equals(object obj)
            {
                return obj is NodeRegistrationKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Domain.GetHashCode() * 397) ^ NodeType.GetHashCode();
                }
            }
        }

        private readonly struct MigrationKey : IEquatable<MigrationKey>
        {
            public readonly ESGraphDomainKey Domain;
            public readonly ESGraphNodeTypeKey NodeType;
            public readonly int FromVersion;

            public MigrationKey(ESGraphDomainKey domain, ESGraphNodeTypeKey nodeType, int fromVersion)
            {
                Domain = domain;
                NodeType = nodeType;
                FromVersion = fromVersion;
            }

            public bool Equals(MigrationKey other)
            {
                return Domain.Equals(other.Domain) && NodeType.Equals(other.NodeType)
                    && FromVersion == other.FromVersion;
            }

            public override bool Equals(object obj)
            {
                return obj is MigrationKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = Domain.GetHashCode();
                    hashCode = (hashCode * 397) ^ NodeType.GetHashCode();
                    return (hashCode * 397) ^ FromVersion;
                }
            }
        }

        private static readonly Dictionary<ESGraphDomainKey, IESGraphAuthoringProfile> Profiles =
            new Dictionary<ESGraphDomainKey, IESGraphAuthoringProfile>();
        private static readonly Dictionary<NodeRegistrationKey, IESGraphNodeDefinition> NodeDefinitions =
            new Dictionary<NodeRegistrationKey, IESGraphNodeDefinition>();
        private static readonly Dictionary<NodeRegistrationKey, string> NodeDefinitionSources =
            new Dictionary<NodeRegistrationKey, string>();
        private static readonly Dictionary<ESGraphDomainKey, IReadOnlyList<IESGraphNodeDefinition>> NodeDefinitionsByDomain =
            new Dictionary<ESGraphDomainKey, IReadOnlyList<IESGraphNodeDefinition>>();
        private static readonly Dictionary<NodeRegistrationKey, IESGraphPayloadInspector> PayloadInspectors =
            new Dictionary<NodeRegistrationKey, IESGraphPayloadInspector>();
        private static readonly Dictionary<NodeRegistrationKey, string> PayloadInspectorSources =
            new Dictionary<NodeRegistrationKey, string>();
        private static readonly Dictionary<MigrationKey, IESGraphNodeMigrator> Migrators =
            new Dictionary<MigrationKey, IESGraphNodeMigrator>();
        private static readonly Dictionary<MigrationKey, string> MigratorSources =
            new Dictionary<MigrationKey, string>();

        static ESGraphAuthoringRegistry()
        {
            DiscoverProfiles();
            RegisterProfileNodeDefinitions();
            DiscoverStandaloneNodeDefinitions();
            DiscoverMigrators();
            DiscoverPayloadInspectors();
            BuildDomainDefinitionIndex();
        }

        public static IReadOnlyList<IESGraphAuthoringProfile> AllProfiles => Profiles.Values
            .OrderBy(profile => profile.DisplayName, StringComparer.Ordinal)
            .ToArray();

        public static bool TryGetProfile(ESGraphDomainKey domain, out IESGraphAuthoringProfile profile)
        {
            return Profiles.TryGetValue(domain, out profile);
        }

        public static bool TryGetProfile(string domainId, out IESGraphAuthoringProfile profile)
        {
            return TryGetProfile(ESGraphDomainKey.Parse(domainId), out profile);
        }

        public static IReadOnlyList<IESGraphNodeDefinition> GetNodeDefinitions(ESGraphDomainKey domain)
        {
            return NodeDefinitionsByDomain.TryGetValue(domain, out IReadOnlyList<IESGraphNodeDefinition> definitions)
                ? definitions
                : Array.Empty<IESGraphNodeDefinition>();
        }

        public static IReadOnlyList<IESGraphNodeDefinition> GetNodeDefinitions(string domainId)
        {
            return GetNodeDefinitions(ESGraphDomainKey.Parse(domainId));
        }

        public static IReadOnlyList<IESGraphNodeDefinition> GetNodeDefinitions(ESGraphAsset asset)
        {
            if (asset == null)
                return Array.Empty<IESGraphNodeDefinition>();

            IReadOnlyList<IESGraphNodeDefinition> definitions = GetNodeDefinitions(asset.DomainKey);
            if (definitions.Count == 0
                || !TryGetProfile(asset.DomainKey, out IESGraphAuthoringProfile profile)
                || !(profile is IESGraphNodeAvailabilityPolicy policy))
                return definitions;

            List<IESGraphNodeDefinition> available = new List<IESGraphNodeDefinition>(definitions.Count);
            for (int i = 0; i < definitions.Count; i++)
            {
                IESGraphNodeDefinition definition = definitions[i];
                if (definition != null && policy.IsNodeAvailable(asset, definition))
                    available.Add(definition);
            }
            return available;
        }

        public static bool TryGetNodeDefinition(ESGraphDomainKey domain, ESGraphNodeTypeKey nodeType,
            out IESGraphNodeDefinition definition)
        {
            return NodeDefinitions.TryGetValue(new NodeRegistrationKey(domain, nodeType), out definition);
        }

        public static bool TryGetNodeDefinition(string domainId, string typeId,
            out IESGraphNodeDefinition definition)
        {
            return TryGetNodeDefinition(ESGraphDomainKey.Parse(domainId), ESGraphNodeTypeKey.Parse(typeId),
                out definition);
        }

        public static List<ESGraphValidationIssue> Validate(ESGraphAsset asset)
        {
            List<ESGraphValidationIssue> issues = asset != null
                ? asset.ValidateGraph()
                : new List<ESGraphValidationIssue>
                {
                    ESGraphValidationIssue.Error("Graph.Asset.Null", "图资产不能为空。")
                };
            if (asset == null)
                return issues;
            if (TryGetProfile(asset.DomainKey, out IESGraphAuthoringProfile profile))
            {
                profile.Validate(asset, issues);
            }
            else if (GetNodeDefinitions(asset.DomainKey).Count == 0)
            {
                issues.Add(ESGraphValidationIssue.Error("Graph.Domain.DefinitionMissing",
                    "当前图领域没有注册编辑方案或节点定义：" + asset.DomainId));
            }
            else
            {
                issues.Add(ESGraphValidationIssue.Warning("Graph.Domain.ProfileMissing",
                    "当前图领域未注册领域方案，将只执行通用模型与节点定义校验：" + asset.DomainId));
            }
            for (int i = 0; i < asset.Nodes.Count; i++)
            {
                ESGraphNodeRecord node = asset.Nodes[i];
                if (node == null)
                    continue;
                if (!TryGetNodeDefinition(asset.DomainKey, node.TypeKey, out IESGraphNodeDefinition definition))
                {
                    issues.Add(ESGraphValidationIssue.Error("Graph.Domain.NodeType",
                        "当前领域没有注册该节点类型：" + node.typeId, node.nodeId));
                    continue;
                }
                if (node.version > definition.CurrentVersion)
                {
                    issues.Add(ESGraphValidationIssue.Error("Graph.Node.Version.Newer",
                        "节点数据版本高于当前定义，无法安全编辑。节点=" + node.version
                        + "，定义=" + definition.CurrentVersion, node.nodeId));
                    continue;
                }
                if (node.version < definition.CurrentVersion)
                {
                    issues.Add(ESGraphValidationIssue.Warning("Graph.Node.Version.Outdated",
                        "节点需要升级。当前=" + node.version + "，目标=" + definition.CurrentVersion,
                        node.nodeId));
                }
                try
                {
                    definition.ValidateNode(asset, node, issues);
                }
                catch (Exception exception)
                {
                    issues.Add(ESGraphValidationIssue.Error("Graph.Node.Validator.Exception",
                        "节点校验器执行失败：" + exception.Message, node.nodeId));
                }
            }
            return issues;
        }

        public static bool TryBake(ESGraphAsset asset, out ESBakedGraphSnapshot snapshot,
            out IESBakedGraphPlan domainPlan, out List<ESGraphValidationIssue> issues)
        {
            snapshot = null;
            domainPlan = null;
            issues = Validate(asset);
            if (HasErrors(issues))
                return false;

            if (!ESGraphSnapshotBaker.TryBake(asset, out snapshot, out List<ESGraphValidationIssue> coreIssues))
            {
                issues.AddRange(coreIssues);
                return false;
            }

            if (!TryGetProfile(asset.DomainKey, out IESGraphAuthoringProfile profile)
                || !(profile is IESGraphAuthoringPlanBaker domainBaker))
                return true;

            if (!domainBaker.TryBakePlan(snapshot, out domainPlan,
                    out IReadOnlyList<ESGraphValidationIssue> domainIssues))
            {
                if (domainIssues != null)
                {
                    for (int i = 0; i < domainIssues.Count; i++)
                        issues.Add(domainIssues[i]);
                }
                return false;
            }

            return true;
        }

        public static bool TryCreatePayloadInspector(ESGraphDomainKey domain, ESGraphNodeTypeKey nodeType,
            string payloadJson,
            Action<string> commitPayload, out VisualElement inspector)
        {
            NodeRegistrationKey key = new NodeRegistrationKey(domain, nodeType);
            if (PayloadInspectors.TryGetValue(key, out IESGraphPayloadInspector provider))
            {
                inspector = provider.Create(payloadJson ?? string.Empty, commitPayload);
                return inspector != null;
            }
            inspector = null;
            return false;
        }

        public static bool TryCreatePayloadInspector(string domainId, string nodeTypeId, string payloadJson,
            Action<string> commitPayload, out VisualElement inspector)
        {
            return TryCreatePayloadInspector(ESGraphDomainKey.Parse(domainId), ESGraphNodeTypeKey.Parse(nodeTypeId),
                payloadJson, commitPayload, out inspector);
        }

        public static bool TryMigrateNode(ESGraphAsset asset, string nodeId, out string error)
        {
            error = null;
            ESGraphNodeRecord node = asset?.FindNode(nodeId);
            if (node == null)
            {
                error = "节点不存在。";
                return false;
            }
            if (!TryGetNodeDefinition(asset.DomainKey, node.TypeKey, out IESGraphNodeDefinition definition))
            {
                error = "当前领域没有注册该节点类型。";
                return false;
            }
            if (node.version > definition.CurrentVersion)
            {
                error = "节点版本高于当前定义，不能降级。";
                return false;
            }

            int guard = 0;
            while (node.version < definition.CurrentVersion && guard++ < 32)
            {
                MigrationKey key = new MigrationKey(asset.DomainKey, node.TypeKey, node.version);
                if (!Migrators.TryGetValue(key, out IESGraphNodeMigrator migrator))
                {
                    error = "缺少节点迁移器：" + node.typeId + " v" + node.version + " → v"
                        + definition.CurrentVersion;
                    return false;
                }
                try
                {
                    if (!migrator.TryMigrate(asset, node, out error))
                        return false;
                }
                catch (Exception exception)
                {
                    error = "节点迁移器执行失败：" + exception.Message;
                    return false;
                }
                node.version = migrator.ToVersion;
            }
            if (node.version != definition.CurrentVersion)
            {
                error = "节点迁移链异常，未到达目标版本。";
                return false;
            }
            return true;
        }

        private static void DiscoverProfiles()
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<IESGraphAuthoringProfile>())
            {
                if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null)
                    continue;
                try
                {
                    IESGraphAuthoringProfile profile = (IESGraphAuthoringProfile)Activator.CreateInstance(type);
                    if (!profile.Domain.IsValid)
                        continue;
                    if (!Profiles.TryGetValue(profile.Domain, out IESGraphAuthoringProfile current))
                    {
                        Profiles[profile.Domain] = profile;
                        continue;
                    }
                    string source = type.FullName ?? type.Name;
                    string currentSource = current.GetType().FullName ?? current.GetType().Name;
                    bool replace = profile.Priority > current.Priority
                        || profile.Priority == current.Priority
                        && string.CompareOrdinal(source, currentSource) < 0;
                    if (replace)
                    {
                        Debug.LogWarning("图领域方案被更高优先级实现替换：" + profile.Domain.StableId
                            + "\n旧来源：" + currentSource + "\n新来源：" + source);
                        Profiles[profile.Domain] = profile;
                    }
                    else
                    {
                        Debug.LogWarning("忽略重复图领域方案：" + profile.Domain.StableId
                            + "\n保留来源：" + currentSource + "\n忽略来源：" + source);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError("图领域编辑方案注册失败：" + type.FullName + "\n" + exception);
                }
            }
        }

        private static void RegisterProfileNodeDefinitions()
        {
            foreach (IESGraphAuthoringProfile profile in Profiles.Values)
            {
                if (profile.NodeDefinitions == null)
                    continue;
                for (int i = 0; i < profile.NodeDefinitions.Count; i++)
                {
                    IESGraphNodeDefinition definition = profile.NodeDefinitions[i];
                    if (definition != null && !definition.Domain.Equals(profile.Domain))
                    {
                        Debug.LogError("图领域方案包含了其他领域的节点定义：" + profile.GetType().FullName
                            + "\n方案领域：" + profile.Domain.StableId + "\n节点领域：" + definition.Domain.StableId);
                        continue;
                    }
                    RegisterNodeDefinition(definition, profile.GetType().FullName);
                }
            }
        }

        private static void DiscoverStandaloneNodeDefinitions()
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<IESGraphNodeDefinition>())
            {
                if (type.IsAbstract || type.IsInterface || type == typeof(ESStableGraphNodeTemplate)
                    || type.GetConstructor(Type.EmptyTypes) == null)
                    continue;
                try
                {
                    var definition = (IESGraphNodeDefinition)Activator.CreateInstance(type);
                    RegisterNodeDefinition(definition, type.FullName);
                }
                catch (Exception exception)
                {
                    Debug.LogError("图节点定义注册失败：" + type.FullName + "\n" + exception);
                }
            }
        }

        private static void RegisterNodeDefinition(IESGraphNodeDefinition definition, string source)
        {
            if (definition == null || !definition.Domain.IsValid || !definition.NodeType.IsValid
                || definition.CurrentVersion < 1)
            {
                Debug.LogError("忽略非法图节点定义：" + (source ?? "<未知来源>"));
                return;
            }
            NodeRegistrationKey key = new NodeRegistrationKey(definition.Domain, definition.NodeType);
            if (NodeDefinitions.TryGetValue(key, out IESGraphNodeDefinition current))
            {
                string currentSource = NodeDefinitionSources[key];
                bool replace = definition.Priority > current.Priority
                    || definition.Priority == current.Priority
                    && string.CompareOrdinal(source, currentSource) < 0;
                if (!replace)
                {
                    Debug.LogWarning("忽略重复图节点定义：" + definition.NodeType.StableId
                        + "\n保留来源：" + currentSource + "\n忽略来源：" + source);
                    return;
                }
                Debug.LogWarning("图节点定义被更高优先级实现替换：" + definition.NodeType.StableId
                    + "\n旧来源：" + currentSource + "\n新来源：" + source);
            }
            NodeDefinitions[key] = definition;
            NodeDefinitionSources[key] = source ?? "<未知来源>";
        }

        private static void BuildDomainDefinitionIndex()
        {
            foreach (IGrouping<ESGraphDomainKey, IESGraphNodeDefinition> group in NodeDefinitions.Values
                         .GroupBy(definition => definition.Domain))
            {
                NodeDefinitionsByDomain[group.Key] = Array.AsReadOnly(group
                    .OrderBy(definition => definition.MenuPath, StringComparer.Ordinal)
                    .ThenBy(definition => definition.DisplayName, StringComparer.Ordinal)
                    .ToArray());
            }
        }

        private static void DiscoverMigrators()
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<IESGraphNodeMigrator>())
            {
                if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null)
                    continue;
                try
                {
                    var migrator = (IESGraphNodeMigrator)Activator.CreateInstance(type);
                    if (!migrator.Domain.IsValid || !migrator.NodeType.IsValid || migrator.FromVersion < 1
                        || migrator.ToVersion <= migrator.FromVersion)
                    {
                        Debug.LogError("忽略非法图节点迁移器：" + type.FullName);
                        continue;
                    }
                    string source = type.FullName ?? type.Name;
                    MigrationKey key = new MigrationKey(migrator.Domain, migrator.NodeType, migrator.FromVersion);
                    if (Migrators.TryGetValue(key, out IESGraphNodeMigrator current))
                    {
                        string currentSource = MigratorSources[key];
                        bool replace = migrator.Priority > current.Priority
                            || migrator.Priority == current.Priority
                            && string.CompareOrdinal(source, currentSource) < 0;
                        if (!replace)
                        {
                            Debug.LogWarning("忽略重复图节点迁移器：" + migrator.NodeType.StableId
                                + " v" + migrator.FromVersion + "\n保留来源：" + currentSource
                                + "\n忽略来源：" + source);
                            continue;
                        }
                        Debug.LogWarning("图节点迁移器被更高优先级实现替换：" + migrator.NodeType.StableId
                            + " v" + migrator.FromVersion + "\n旧来源：" + currentSource + "\n新来源：" + source);
                    }
                    Migrators[key] = migrator;
                    MigratorSources[key] = source;
                }
                catch (Exception exception)
                {
                    Debug.LogError("图节点迁移器注册失败：" + type.FullName + "\n" + exception);
                }
            }
        }

        private static void DiscoverPayloadInspectors()
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<IESGraphPayloadInspector>())
            {
                if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null)
                    continue;
                try
                {
                    IESGraphPayloadInspector inspector = (IESGraphPayloadInspector)Activator.CreateInstance(type);
                    if (!inspector.Domain.IsValid || !inspector.NodeType.IsValid)
                    {
                        Debug.LogError("忽略非法图业务内容编辑器：" + type.FullName);
                        continue;
                    }
                    string source = type.FullName ?? type.Name;
                    NodeRegistrationKey key = new NodeRegistrationKey(inspector.Domain, inspector.NodeType);
                    if (PayloadInspectors.TryGetValue(key, out IESGraphPayloadInspector current))
                    {
                        string currentSource = PayloadInspectorSources[key];
                        bool replace = inspector.Priority > current.Priority
                            || inspector.Priority == current.Priority
                            && string.CompareOrdinal(source, currentSource) < 0;
                        if (!replace)
                        {
                            Debug.LogWarning("忽略重复图业务内容编辑器：" + inspector.NodeType.StableId
                                + "\n保留来源：" + currentSource + "\n忽略来源：" + source);
                            continue;
                        }
                        Debug.LogWarning("图业务内容编辑器被更高优先级实现替换：" + inspector.NodeType.StableId
                            + "\n旧来源：" + currentSource + "\n新来源：" + source);
                    }
                    PayloadInspectors[key] = inspector;
                    PayloadInspectorSources[key] = source;
                }
                catch (Exception exception)
                {
                    Debug.LogError("图业务内容编辑器注册失败：" + type.FullName + "\n" + exception);
                }
            }
        }

        private static bool HasErrors(List<ESGraphValidationIssue> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i] != null && issues[i].severity == ESGraphValidationSeverity.Error)
                    return true;
            }
            return false;
        }
    }

    public abstract class ESGraphAuthoringProfileBase : IESGraphAuthoringProfile
    {
        public abstract ESGraphDomainKey Domain { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }
        public virtual int Priority => 0;
        public IReadOnlyList<IESGraphNodeDefinition> NodeDefinitions { get; }

        protected ESGraphAuthoringProfileBase(params IESGraphNodeDefinition[] definitions)
        {
            NodeDefinitions = definitions ?? Array.Empty<IESGraphNodeDefinition>();
        }

        public virtual void Validate(ESGraphAsset asset, List<ESGraphValidationIssue> issues)
        {
        }

        protected static void RequireExactlyOne(ESGraphAsset asset, List<ESGraphValidationIssue> issues,
            ESGraphBuiltInNodeKind nodeKind, string label)
        {
            RequireExactlyOne(asset, issues, ESGraphNodeTypeKey.FromKind(nodeKind), label);
        }

        protected static void RequireExactlyOne(ESGraphAsset asset, List<ESGraphValidationIssue> issues,
            ESGraphNodeTypeKey nodeType, string label)
        {
            int count = 0;
            for (int i = 0; i < asset.Nodes.Count; i++)
            {
                ESGraphNodeRecord node = asset.Nodes[i];
                if (node != null && node.TypeKey.Equals(nodeType))
                    count++;
            }
            if (count != 1)
                issues.Add(ESGraphValidationIssue.Error("Graph.Domain.RootCount",
                    label + "必须且只能存在一个，当前数量：" + count));
        }

        protected static ESGraphPortDefinition Input(string name = "输入", ESGraphPortCapacity capacity = ESGraphPortCapacity.Single,
            ESGraphPortValueKind valueKind = ESGraphPortValueKind.Flow, string customValueTypeId = null)
        {
            return new ESGraphPortDefinition(name, "flow.input", ESGraphPortDirection.Input, capacity, valueKind,
                customValueTypeId);
        }

        protected static ESGraphPortDefinition Output(string name = "输出", string stableKey = "flow.output",
            ESGraphPortCapacity capacity = ESGraphPortCapacity.Multi,
            ESGraphPortValueKind valueKind = ESGraphPortValueKind.Flow, string customValueTypeId = null)
        {
            return new ESGraphPortDefinition(name, stableKey, ESGraphPortDirection.Output, capacity, valueKind,
                customValueTypeId);
        }
    }

    public sealed class ESGenericGraphAuthoringProfile : ESGraphAuthoringProfileBase
    {
        public override ESGraphDomainKey Domain => ESGraphDomainKey.FromKind(ESGraphDomainKind.Generic);
        public override string DisplayName => "通用流程图";
        public override string Description => "适合快速搭建普通流程；正式业务建议选择对应的领域方案。";

        public ESGenericGraphAuthoringProfile() : base(
            new ESStableGraphNodeTemplate(ESGraphDomainKind.Generic, ESGraphBuiltInNodeKind.GenericFlow,
                "流程/普通节点", "流程节点", ESGraphNodeCategory.Flow, ESGraphNodeTheme.Neutral, Input(), Output()),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.Generic, ESGraphBuiltInNodeKind.GenericSource,
                "流程/起点", "起点", ESGraphNodeCategory.Entry, ESGraphNodeTheme.Entry, Output()),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.Generic, ESGraphBuiltInNodeKind.GenericSink,
                "流程/终点", "终点", ESGraphNodeCategory.Exit, ESGraphNodeTheme.Exit, Input()),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.Generic, ESGraphBuiltInNodeKind.GenericBranch,
                "流程/分支", "分支", ESGraphNodeCategory.Branch, ESGraphNodeTheme.Decision, Input(),
                Output("成立", "flow.true", ESGraphPortCapacity.Single),
                Output("不成立", "flow.false", ESGraphPortCapacity.Single)),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.Generic, ESGraphBuiltInNodeKind.GenericMerge,
                "流程/汇合", "汇合", ESGraphNodeCategory.Merge, ESGraphNodeTheme.Merge,
                Input(capacity: ESGraphPortCapacity.Multi), Output()))
        {
        }
    }

    public sealed class ESStoryGraphAuthoringProfile : ESGraphAuthoringProfileBase
    {
        public override ESGraphDomainKey Domain => ESGraphDomainKey.FromKind(ESGraphDomainKind.Story);
        public override string DisplayName => "剧情 / 任务与对话";
        public override string Description => "用于搭建剧情、任务和对话流程；最终执行仍由剧情记录和运行实例管理。";

        public ESStoryGraphAuthoringProfile() : base(
            new ESStableGraphNodeTemplate(ESGraphDomainKind.Story, ESGraphBuiltInNodeKind.StoryStart,
                "剧情/开始", "开始", ESGraphNodeCategory.Entry, ESGraphNodeTheme.Entry,
                Output(capacity: ESGraphPortCapacity.Single)),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.Story, ESGraphBuiltInNodeKind.StoryDialogue,
                "剧情/对话", "对话", ESGraphNodeCategory.Dialogue, ESGraphNodeTheme.Dialogue, Input(), Output()),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.Story, ESGraphBuiltInNodeKind.StoryChoice,
                "剧情/选择", "选择", ESGraphNodeCategory.Choice, ESGraphNodeTheme.Decision,
                Input(), Output("选项", "flow.option")),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.Story, ESGraphBuiltInNodeKind.StoryCondition,
                "剧情/条件", "条件", ESGraphNodeCategory.Condition, ESGraphNodeTheme.Decision, Input(),
                Output("成立", "flow.true", ESGraphPortCapacity.Single), Output("不成立", "flow.false", ESGraphPortCapacity.Single)),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.Story, ESGraphBuiltInNodeKind.StoryAction,
                "剧情/行为", "行为", ESGraphNodeCategory.Action, ESGraphNodeTheme.Primary, Input(), Output()),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.Story, ESGraphBuiltInNodeKind.StoryComplete,
                "剧情/完成", "完成", ESGraphNodeCategory.Exit, ESGraphNodeTheme.Success, Input()),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.Story, ESGraphBuiltInNodeKind.StoryFail,
                "剧情/失败", "失败", ESGraphNodeCategory.Exit, ESGraphNodeTheme.Failure, Input()))
        {
        }

        public override void Validate(ESGraphAsset asset, List<ESGraphValidationIssue> issues)
        {
            base.Validate(asset, issues);
            RequireExactlyOne(asset, issues, ESGraphBuiltInNodeKind.StoryStart, "剧情开始节点");
        }
    }

    public sealed class ESBehaviorTreeGraphAuthoringProfile : ESGraphAuthoringProfileBase
    {
        public override ESGraphDomainKey Domain => ESGraphDomainKey.FromKind(ESGraphDomainKind.BehaviorTree);
        public override string DisplayName => "行为树调度";
        public override string Description => "行为树作者结构；调度、取消和 Tick 预算由行为树领域实现。";

        public ESBehaviorTreeGraphAuthoringProfile() : base(
            new ESStableGraphNodeTemplate(ESGraphDomainKind.BehaviorTree, ESGraphBuiltInNodeKind.BehaviorRoot,
                "行为树/根节点", "根节点", ESGraphNodeCategory.Entry, ESGraphNodeTheme.Entry,
                Output("子节点", capacity: ESGraphPortCapacity.Single)),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.BehaviorTree, ESGraphBuiltInNodeKind.BehaviorSequence,
                "行为树/顺序组合", "顺序", ESGraphNodeCategory.Composite, ESGraphNodeTheme.Composite, Input(), Output("子节点")),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.BehaviorTree, ESGraphBuiltInNodeKind.BehaviorSelector,
                "行为树/选择组合", "选择", ESGraphNodeCategory.Composite, ESGraphNodeTheme.Composite, Input(), Output("子节点")),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.BehaviorTree, ESGraphBuiltInNodeKind.BehaviorParallel,
                "行为树/并行组合", "并行", ESGraphNodeCategory.Composite, ESGraphNodeTheme.Composite, Input(), Output("子节点")),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.BehaviorTree, ESGraphBuiltInNodeKind.BehaviorDecorator,
                "行为树/装饰节点", "装饰", ESGraphNodeCategory.Decorator, ESGraphNodeTheme.Decision, Input(),
                Output("子节点", capacity: ESGraphPortCapacity.Single)),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.BehaviorTree, ESGraphBuiltInNodeKind.BehaviorCondition,
                "行为树/条件节点", "条件", ESGraphNodeCategory.Condition, ESGraphNodeTheme.Constraint, Input()),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.BehaviorTree, ESGraphBuiltInNodeKind.BehaviorAction,
                "行为树/行为节点", "行为", ESGraphNodeCategory.Action, ESGraphNodeTheme.Primary, Input()))
        {
        }

        public override void Validate(ESGraphAsset asset, List<ESGraphValidationIssue> issues)
        {
            base.Validate(asset, issues);
            RequireExactlyOne(asset, issues, ESGraphBuiltInNodeKind.BehaviorRoot, "行为树根节点");
            if (asset.allowCycles)
                issues.Add(ESGraphValidationIssue.Error("Graph.Behavior.CyclePolicy", "行为树领域禁止开启循环。"));
        }
    }

    public sealed class ESAgentAuthoringGraphProfile : ESGraphAuthoringProfileBase,
        IESGraphNodeAvailabilityPolicy,
        IESGraphAuthoringPlanBaker
    {
        private readonly ESAgentArtifactGenerationBaker baker = new ESAgentArtifactGenerationBaker();

        public override ESGraphDomainKey Domain => ESGraphDomainKey.FromKind(ESGraphDomainKind.AgentAuthoring);
        public override string DisplayName => "智能助手产物编排";
        public override string Description => "编排 AICommand 命令与 Agent Skill 技能的生成要求、候选检查和人工批准；不会进入游戏运行时。";

        public ESAgentAuthoringGraphProfile() : base(
            new ESStableGraphNodeTemplate(ESGraphDomainKind.AgentAuthoring, ESGraphBuiltInNodeKind.AgentGoal,
                "智能助手编排/生成目标", "生成目标",
                JsonUtility.ToJson(new ESAgentGoalPayload()),
                ESGraphNodeCategory.Entry, ESGraphNodeTheme.Primary,
                Output("需求上下文", "agent.context.out", ESGraphPortCapacity.Multi,
                    ESGraphPortValueKind.AgentContext)),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.AgentAuthoring, ESGraphBuiltInNodeKind.AgentReference,
                "智能助手编排/引用资料", "引用资料",
                JsonUtility.ToJson(new ESAgentReferencePayload()),
                ESGraphNodeCategory.Reference, ESGraphNodeTheme.Reference,
                Input("上游上下文", ESGraphPortCapacity.Multi, ESGraphPortValueKind.AgentContext),
                Output("补充上下文", "agent.context.out", ESGraphPortCapacity.Multi,
                    ESGraphPortValueKind.AgentContext)),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.AgentAuthoring, ESGraphBuiltInNodeKind.AgentConstraint,
                "智能助手编排/生成约束", "生成约束",
                JsonUtility.ToJson(new ESAgentConstraintPayload()),
                ESGraphNodeCategory.Constraint, ESGraphNodeTheme.Constraint,
                Input("需求上下文", ESGraphPortCapacity.Multi, ESGraphPortValueKind.AgentContext),
                Output("产物要求", "agent.requirement.out", ESGraphPortCapacity.Multi,
                    ESGraphPortValueKind.AgentRequirement)),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.AgentAuthoring, ESGraphBuiltInNodeKind.AgentAICommandOutput,
                "智能助手编排/产物输出/AI 命令", "生成 AICommand 命令",
                JsonUtility.ToJson(new ESAgentAICommandOutputPayload()),
                ESGraphNodeCategory.Output, ESGraphNodeTheme.CommandOutput,
                Input("产物要求", ESGraphPortCapacity.Multi, ESGraphPortValueKind.AgentRequirement),
                Output("候选产物", "agent.artifact.out", ESGraphPortCapacity.Single,
                    ESGraphPortValueKind.AgentArtifact)),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.AgentAuthoring, ESGraphBuiltInNodeKind.AgentSkillOutput,
                "智能助手编排/产物输出/代理技能", "生成 Agent Skill 技能",
                JsonUtility.ToJson(new ESAgentSkillOutputPayload()),
                ESGraphNodeCategory.Output, ESGraphNodeTheme.SkillOutput,
                Input("产物要求", ESGraphPortCapacity.Multi, ESGraphPortValueKind.AgentRequirement),
                Output("候选产物", "agent.artifact.out", ESGraphPortCapacity.Single,
                    ESGraphPortValueKind.AgentArtifact)),
            new ESStableGraphNodeTemplate(ESGraphDomainKind.AgentAuthoring, ESGraphBuiltInNodeKind.AgentValidation,
                "智能助手编排/验证与批准", "验证与批准",
                JsonUtility.ToJson(new ESAgentValidationPayload()),
                ESGraphNodeCategory.Validation, ESGraphNodeTheme.Validation,
                Input("候选产物", ESGraphPortCapacity.Multi, ESGraphPortValueKind.AgentArtifact)))
        {
        }

        public override void Validate(ESGraphAsset asset, List<ESGraphValidationIssue> issues)
        {
            base.Validate(asset, issues);
            RequireExactlyOne(asset, issues, ESGraphBuiltInNodeKind.AgentGoal, "智能助手编排“生成目标”节点");
            ESAgentAuthoringGraphValidator.Validate(asset, issues);
        }

        public bool TryBakePlan(ESBakedGraphSnapshot source, out IESBakedGraphPlan plan,
            out IReadOnlyList<ESGraphValidationIssue> issues)
        {
            bool success = baker.TryBake(source, out ESAgentArtifactGenerationSpec bakedPlan, out issues);
            plan = bakedPlan;
            return success;
        }

        public bool IsNodeAvailable(ESGraphAsset asset, IESGraphNodeDefinition definition)
        {
            if (asset == null || definition == null)
                return false;

            ESAgentAuthoringPaletteMode mode = ResolvePaletteMode(asset);
            if (mode == ESAgentAuthoringPaletteMode.AICommand
                && definition.NodeType.Kind == ESGraphBuiltInNodeKind.AgentSkillOutput)
                return false;
            if (mode == ESAgentAuthoringPaletteMode.AgentSkill
                && definition.NodeType.Kind == ESGraphBuiltInNodeKind.AgentAICommandOutput)
                return false;
            return true;
        }

        private static ESAgentAuthoringPaletteMode ResolvePaletteMode(ESGraphAsset asset)
        {
            bool hasCommand = false;
            bool hasSkill = false;
            for (int i = 0; i < asset.Nodes.Count; i++)
            {
                ESGraphNodeRecord node = asset.Nodes[i];
                if (node == null)
                    continue;
                hasCommand |= node.BuiltInKind == ESGraphBuiltInNodeKind.AgentAICommandOutput;
                hasSkill |= node.BuiltInKind == ESGraphBuiltInNodeKind.AgentSkillOutput;
            }

            if (hasCommand && !hasSkill)
                return ESAgentAuthoringPaletteMode.AICommand;
            if (hasSkill && !hasCommand)
                return ESAgentAuthoringPaletteMode.AgentSkill;
            if (hasCommand && hasSkill)
                return ESAgentAuthoringPaletteMode.Paired;
            return ESAgentAuthoringPaletteMode.Flexible;
        }

        private enum ESAgentAuthoringPaletteMode : byte
        {
            Flexible,
            AICommand,
            AgentSkill,
            Paired
        }
    }
}
