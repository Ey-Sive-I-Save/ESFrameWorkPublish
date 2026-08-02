using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Editor-only optional-system adapter. Implementations turn one authored source into a
    /// versioned, immutable Plan snapshot; they must not retain runtime state.
    /// </summary>
    public interface IESResourcePlanBakeExtension
    {
        string ProviderId { get; }
        int SchemaVersion { get; }
        bool CanBake(ScriptableObject source);
        ESResourcePlanBakedExtensionEntry Bake(ESResourcePlanInfo plan, ESResourcePlanExtensionSourceEntry source);
    }

    /// <summary>
    /// Explicit registry keeps optional integrations out of ES core assemblies. Registration is
    /// performed by the integration's Editor assembly initializer, where duplicate ProviderId is
    /// a hard error rather than a last-writer-wins ambiguity.
    /// </summary>
    public static class ESResourcePlanBakeExtensions
    {
        private static readonly Dictionary<string, IESResourcePlanBakeExtension> Extensions
            = new Dictionary<string, IESResourcePlanBakeExtension>(StringComparer.Ordinal);
        private static bool discovered;

        public static void Register(IESResourcePlanBakeExtension extension)
        {
            if (extension == null) throw new ArgumentNullException(nameof(extension));
            string providerId = extension.ProviderId?.Trim();
            if (string.IsNullOrEmpty(providerId)) throw new InvalidOperationException("ResourcePlan Bake extension ProviderId 不能为空。");
            if (extension.SchemaVersion <= 0) throw new InvalidOperationException("ResourcePlan Bake extension SchemaVersion 必须大于 0：" + providerId);
            if (Extensions.ContainsKey(providerId)) throw new InvalidOperationException("ResourcePlan Bake extension ProviderId 重复：" + providerId);
            Extensions.Add(providerId, extension);
        }

        internal static IESResourcePlanBakeExtension Resolve(ScriptableObject source)
        {
            EnsureDiscovered();
            IESResourcePlanBakeExtension match = null;
            foreach (IESResourcePlanBakeExtension extension in Extensions.Values)
            {
                if (!extension.CanBake(source)) continue;
                if (match != null) throw new InvalidOperationException("同一扩展配置来源被多个 ResourcePlan Bake extension 声明：" + source.name);
                match = extension;
            }
            return match;
        }

        private static void EnsureDiscovered()
        {
            if (discovered) return;
            discovered = true;
            foreach (Type type in TypeCache.GetTypesDerivedFrom<IESResourcePlanBakeExtension>())
            {
                if (type == null || type.IsAbstract || type.ContainsGenericParameters) continue;
                if (type.GetConstructor(Type.EmptyTypes) == null)
                    throw new InvalidOperationException("ResourcePlan Bake extension 必须提供无参构造函数：" + type.FullName);
                Register((IESResourcePlanBakeExtension)Activator.CreateInstance(type));
            }
        }
    }
}
