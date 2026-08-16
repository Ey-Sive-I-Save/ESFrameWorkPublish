#if UNITY_EDITOR
using System;
using System.Collections.Generic;
namespace ES
{
/// <summary>Marks an Editor-only handler for one generated Agent artifact kind.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ESAgentArtifactAttribute : Attribute
    {
        public string StableId { get; }

        public ESAgentArtifactAttribute(string stableId)
        {
            if (!ESGraphStableIdUtility.IsValid(stableId))
                throw new ArgumentException("Agent 产物稳定标识非法。", nameof(stableId));
            StableId = stableId;
        }
    }

    /// <summary>Domain-reload-safe metadata registry populated by ES AssemblyStream.</summary>
    public static class ESAgentArtifactTypeRegistry
    {
        private static readonly Dictionary<string, Type> Types =
            new Dictionary<string, Type>(StringComparer.Ordinal);

        public static bool TryGet(string stableId, out Type type)
        {
            return Types.TryGetValue(stableId ?? string.Empty, out type);
        }

        public static IReadOnlyList<KeyValuePair<string, Type>> CopyEntries()
        {
            var result = new List<KeyValuePair<string, Type>>(Types);
            result.Sort((left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));
            return result;
        }

        internal static void Register(ESAgentArtifactAttribute attribute, Type type)
        {
            if (attribute == null || type == null || type.IsAbstract || type.IsInterface)
                return;
            if (Types.TryGetValue(attribute.StableId, out Type existing))
            {
                if (existing == type)
                    return;
                throw new InvalidOperationException("Agent 产物稳定标识重复：" + attribute.StableId
                    + "，类型 " + existing.FullName + " 与 " + type.FullName + " 冲突。");
            }
            Types.Add(attribute.StableId, type);
        }
    }

    /// <summary>Lightweight, idempotent Agent artifact metadata registration.</summary>
    public sealed class ESAgentArtifactAttributeRegister
        : EditorRegister_FOR_ClassAttribute<ESAgentArtifactAttribute>
    {
        public override void Handle(ESAgentArtifactAttribute attribute, Type type)
        {
            ESAgentArtifactTypeRegistry.Register(attribute, type);
        }
    }
}
#endif

