using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>One AssetConfigKey leaf discovered while expanding a ResourcePlan.</summary>
    internal readonly struct ESResourcePlanSerializedDependency
    {
        public readonly UnityEngine.Object Root;
        public readonly string PropertyPath;
        public readonly string TraversalPath;
        public readonly int Depth;
        public readonly ESAssetReferKind Kind;
        public readonly int EnumKey;
        public readonly string StringKey;
        public readonly string Guid;
        public readonly long LocalFileId;

        public ESResourcePlanSerializedDependency(
            UnityEngine.Object root,
            string propertyPath,
            string traversalPath,
            int depth,
            ESAssetReferKind kind,
            int enumKey,
            string stringKey,
            string guid,
            long localFileId)
        {
            Root = root;
            PropertyPath = propertyPath;
            TraversalPath = traversalPath ?? string.Empty;
            Depth = depth;
            Kind = kind;
            EnumKey = enumKey;
            StringKey = stringKey ?? string.Empty;
            Guid = guid ?? string.Empty;
            LocalFileId = localFileId;
        }

        public string Source => !string.IsNullOrEmpty(TraversalPath)
            ? TraversalPath + " -> " + (Root != null ? Root.name : "<missing>") + "." + PropertyPath
            : (Root != null ? Root.name : "<missing>") + "." + PropertyPath;

        public string EffectiveKey => Kind + "/" + ESConfigKeyMatch.Describe(EnumKey, StringKey);
    }

    /// <summary>
    /// ResourcePlan-specific rules layered over ESSerializedDependencyExpander. All ConfigKey
    /// and GameCore knowledge stays here, outside the generic traversal hot path.
    /// </summary>
    internal static class ESResourcePlanDependencyCollector
    {
        public const int DefaultMaxGameCoreDepth = 4;

        private sealed class RuleContext
        {
            public readonly Dictionary<Type, IReadOnlyList<ESGameCoreDefinitionLocator.Candidate>> CandidatesByEnumType
                = new Dictionary<Type, IReadOnlyList<ESGameCoreDefinitionLocator.Candidate>>();
        }

        private static readonly Dictionary<string, Type> GameCoreEnumTypeBySerializedType = BuildGameCoreEnumTypeMap();
        private static readonly ESSerializedDependencyRuleSet<RuleContext, ESResourcePlanSerializedDependency> Rules
            = BuildRules();
        private static readonly ESSerializedDependencyPipeline<RuleContext, ESResourcePlanSerializedDependency> Pipeline
            = new ESSerializedDependencyPipeline<RuleContext, ESResourcePlanSerializedDependency>(
                Rules,
                new ESSerializedDependencyOptions
                {
                    MaxDepth = DefaultMaxGameCoreDepth,
                    ResultCapacity = 32,
                    NodeCapacity = 16
                });

        public static List<ESResourcePlanSerializedDependency> Expand(
            IEnumerable<ScriptableObject> roots,
            int maxGameCoreDepth = DefaultMaxGameCoreDepth)
        {
            var context = new RuleContext();
            if (maxGameCoreDepth == DefaultMaxGameCoreDepth)
                return Pipeline.Expand(roots, context);

            return ESSerializedDependencyExpander.Expand(roots, Rules, context, new ESSerializedDependencyOptions
            {
                MaxDepth = maxGameCoreDepth,
                ResultCapacity = 32,
                NodeCapacity = 16
            });
        }

        private static ESSerializedDependencyRuleSet<RuleContext, ESResourcePlanSerializedDependency> BuildRules()
        {
            ESSerializedDependencyRuleSet<RuleContext, ESResourcePlanSerializedDependency>.Builder rules
                = ESSerializedDependencyRuleSet<RuleContext, ESResourcePlanSerializedDependency>.CreateBuilder(24);

            AddAssetRule<ESAssetReferPrefabConfigKey>(rules, ESAssetReferKind.Prefab);
            AddAssetRule<ESAssetReferSpriteConfigKey>(rules, ESAssetReferKind.Sprite);
            AddAssetRule<ESAssetReferAudioClipConfigKey>(rules, ESAssetReferKind.AudioClip);
            AddAssetRule<ESAssetReferAnimationClipConfigKey>(rules, ESAssetReferKind.AnimationClip);
            AddAssetRule<ESAssetReferAnimatorControllerConfigKey>(rules, ESAssetReferKind.AnimatorController);
            AddAssetRule<ESAssetReferMaterialConfigKey>(rules, ESAssetReferKind.Material);
            AddAssetRule<ESAssetReferMeshConfigKey>(rules, ESAssetReferKind.Mesh);
            AddAssetRule<ESAssetReferTextureConfigKey>(rules, ESAssetReferKind.Texture);
            AddAssetRule<ESAssetReferTexture2DConfigKey>(rules, ESAssetReferKind.Texture2D);
            AddAssetRule<ESAssetReferSpriteAtlasConfigKey>(rules, ESAssetReferKind.SpriteAtlas);
            AddAssetRule<ESAssetReferAvatarConfigKey>(rules, ESAssetReferKind.Avatar);
            AddAssetRule<ESAssetReferPlayableAssetConfigKey>(rules, ESAssetReferKind.PlayableAsset);
            AddAssetRule<ESAssetReferScriptableObjectConfigKey>(rules, ESAssetReferKind.ScriptableObject);
            AddAssetRule<ESAssetReferTimelineAssetConfigKey>(rules, ESAssetReferKind.TimelineAsset);
            AddAssetRule<ESAssetReferVideoClipConfigKey>(rules, ESAssetReferKind.VideoClip);
            AddAssetRule<ESAssetReferTerrainDataConfigKey>(rules, ESAssetReferKind.TerrainData);
            AddAssetRule<ESAssetReferRawConfigKey>(rules, ESAssetReferKind.Raw);

            foreach (KeyValuePair<string, Type> pair in GameCoreEnumTypeBySerializedType)
            {
                Type enumType = pair.Value;
                rules.Add(pair.Key, (context, root, property, depth, traversalPath) =>
                    CollectGameCoreDependency(context, enumType, root, property));
            }

            return rules.Build();
        }

        private static void AddAssetRule<TKey>(
            ESSerializedDependencyRuleSet<RuleContext, ESResourcePlanSerializedDependency>.Builder rules,
            ESAssetReferKind kind)
        {
            rules.Add<TKey>((context, root, property, depth, traversalPath) =>
                CollectAssetDependency(kind, root, property, depth, traversalPath));
        }

        private static ESSerializedDependencyVisit<ESResourcePlanSerializedDependency> CollectAssetDependency(
            ESAssetReferKind kind,
            ScriptableObject root,
            SerializedProperty property,
            int depth,
            string traversalPath)
        {
            SerializedProperty enumKey = property.FindPropertyRelative("enumKey");
            SerializedProperty stringKey = property.FindPropertyRelative("stringKey");
            int enumValue = enumKey != null ? enumKey.intValue : 0;
            string stringValue = stringKey != null ? stringKey.stringValue : string.Empty;
            if (!ESConfigKeyMatch.IsConfigured(enumValue, stringValue))
                return ESSerializedDependencyVisit<ESResourcePlanSerializedDependency>.Consume();

            SerializedProperty guid = property.FindPropertyRelative("guid");
            SerializedProperty fileId = property.FindPropertyRelative("localFileId");
            return ESSerializedDependencyVisit<ESResourcePlanSerializedDependency>.Emit(
                new ESResourcePlanSerializedDependency(
                    root,
                    property.propertyPath,
                    traversalPath,
                    depth,
                    kind,
                    enumValue,
                    stringValue,
                    guid != null ? guid.stringValue : string.Empty,
                    fileId != null ? fileId.longValue : 0L));
        }

        private static ESSerializedDependencyVisit<ESResourcePlanSerializedDependency> CollectGameCoreDependency(
            RuleContext context,
            Type enumType,
            ScriptableObject owner,
            SerializedProperty property)
        {
            SerializedProperty enumKey = property.FindPropertyRelative("enumKey");
            SerializedProperty stringKey = property.FindPropertyRelative("stringKey");
            int enumValue = enumKey != null ? enumKey.intValue : 0;
            string stringValue = stringKey != null ? stringKey.stringValue : string.Empty;
            if (!ESConfigKeyMatch.IsConfigured(enumValue, stringValue))
                return ESSerializedDependencyVisit<ESResourcePlanSerializedDependency>.Consume();

            if (!context.CandidatesByEnumType.TryGetValue(enumType, out IReadOnlyList<ESGameCoreDefinitionLocator.Candidate> candidates))
            {
                candidates = ESGameCoreDefinitionLocator.GetCandidates(enumType);
                context.CandidatesByEnumType.Add(enumType, candidates);
            }

            ESGameCoreDefinitionLocator.Candidate match = null;
            int matchCount = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                ESGameCoreDefinitionLocator.Candidate candidate = candidates[i];
                if (!ESConfigKeyMatch.Matches(enumValue, stringValue, candidate.enumKey, candidate.stringKey))
                    continue;
                match = candidate;
                matchCount++;
            }

            string source = owner.name + "." + property.propertyPath;
            string describedKey = enumType.Name + "/" + ESConfigKeyMatch.Describe(enumValue, stringValue);
            if (matchCount == 0)
                throw new InvalidOperationException("[ESRes][Dependency] GameCore Key 未解析到定义资产：" + source + " -> " + describedKey);
            if (matchCount > 1)
                throw new InvalidOperationException("[ESRes][Dependency] GameCore Key 对应多个定义资产：" + source + " -> " + describedKey);

            SerializedProperty cachedGuid = property.FindPropertyRelative("definitionGuid");
            SerializedProperty cachedLocalFileId = property.FindPropertyRelative("definitionLocalFileId");
            if (cachedGuid != null && !string.IsNullOrEmpty(cachedGuid.stringValue)
                && (!string.Equals(cachedGuid.stringValue, match.guid, StringComparison.Ordinal)
                    || (cachedLocalFileId != null && cachedLocalFileId.longValue != match.localFileId)))
                throw new InvalidOperationException(
                    "[ESRes][Dependency] GameCore Key 的编辑器定位缓存已过期：" + source
                    + "，Key 解析=" + match.guid + ":" + match.localFileId
                    + "，缓存=" + cachedGuid.stringValue + ":" + (cachedLocalFileId != null ? cachedLocalFileId.longValue : 0L));

            return ESSerializedDependencyVisit<ESResourcePlanSerializedDependency>.Traverse(
                match.asset,
                source + " [" + describedKey + "]");
        }

        private static Dictionary<string, Type> BuildGameCoreEnumTypeMap()
        {
            var result = new Dictionary<string, Type>(StringComparer.Ordinal);
            TypeCache.TypeCollection keyTypes = TypeCache.GetTypesDerivedFrom<IESConfigKey>();
            for (int i = 0; i < keyTypes.Count; i++)
            {
                Type concreteType = keyTypes[i];
                if (concreteType == null || concreteType.IsAbstract || concreteType.ContainsGenericParameters)
                    continue;

                Type current = concreteType;
                while (current != null)
                {
                    if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(ESGameCoreConfigKey<>))
                    {
                        Type enumType = current.GetGenericArguments()[0];
                        if (result.TryGetValue(concreteType.Name, out Type existing) && existing != enumType)
                            throw new InvalidOperationException(
                                "[ESRes][Dependency] GameCore ConfigKey 序列化类型名称冲突：" + concreteType.Name);
                        result[concreteType.Name] = enumType;
                        break;
                    }
                    current = current.BaseType;
                }
            }
            return result;
        }

        public static string ComputeFingerprint(IEnumerable<ESResourcePlanSerializedDependency> dependencies)
        {
            var text = new StringBuilder(2048);
            foreach (ESResourcePlanSerializedDependency item in dependencies)
                text.Append(item.Root != null
                        ? ESSerializedDependencyExpander.GetStableObjectIdentity(item.Root)
                        : string.Empty).Append('|')
                    .Append(item.Depth).Append('|').Append(item.TraversalPath).Append('|')
                    .Append(item.PropertyPath).Append('|').Append(item.Kind).Append('|').Append(item.EnumKey).Append('|')
                    .Append(item.StringKey).Append('|').Append(item.Guid).Append('|').Append(item.LocalFileId).Append('\n');
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", string.Empty);
        }
    }
}
