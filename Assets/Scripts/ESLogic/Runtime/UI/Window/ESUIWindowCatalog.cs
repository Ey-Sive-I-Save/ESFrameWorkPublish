using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Explicit, root-assigned definition catalog. It builds a local lookup once and rejects
    /// ambiguous enum/string aliases rather than silently choosing a definition.
    /// </summary>
    [CreateAssetMenu(menuName = "【ES】/配置/UI/窗口 Catalog", fileName = "ESUIWindowCatalog")]
    public sealed class ESUIWindowCatalog : ScriptableObject
    {
        [SerializeField] private List<ESUIWindowDefinition> definitions = new List<ESUIWindowDefinition>();

        [NonSerialized] private Dictionary<ESUIWindowId, ESUIWindowDefinition> byBuiltInId;
        [NonSerialized] private Dictionary<string, ESUIWindowDefinition> byStringKey;
        [NonSerialized] private bool indexBuilt;
        [NonSerialized] private string indexError;

        public IReadOnlyList<ESUIWindowDefinition> Definitions => definitions;

        public bool TryGet(ESUIWindowIdentity identity, out ESUIWindowDefinition definition)
        {
            if (!TryBuild(out _))
            {
                definition = null;
                return false;
            }

            ESUIWindowDefinition byId = null;
            if (identity.HasBuiltInId && !byBuiltInId.TryGetValue(identity.BuiltInId, out byId))
            {
                definition = null;
                return false;
            }

            ESUIWindowDefinition byKey = null;
            if (identity.HasStringKey && !byStringKey.TryGetValue(identity.StringKey, out byKey))
            {
                definition = null;
                return false;
            }

            if (byId != null && byKey != null && !ReferenceEquals(byId, byKey))
            {
                definition = null;
                return false;
            }

            definition = byId ?? byKey;
            return definition != null;
        }

        public bool TryBuild(out string error)
        {
            if (indexBuilt)
            {
                error = indexError;
                return string.IsNullOrEmpty(error);
            }

            indexBuilt = true;
            indexError = null;
            byBuiltInId = new Dictionary<ESUIWindowId, ESUIWindowDefinition>(definitions.Count);
            byStringKey = new Dictionary<string, ESUIWindowDefinition>(definitions.Count, StringComparer.Ordinal);

            for (int i = 0; i < definitions.Count; i++)
            {
                ESUIWindowDefinition definition = definitions[i];
                if (definition == null)
                {
                    return SetBuildError("窗口 Catalog 包含空 Definition。", out error);
                }

                if (!definition.TryValidate(out string definitionError))
                {
                    return SetBuildError("窗口 Definition '" + definition.name + "' 无效：" + definitionError, out error);
                }

                if (byStringKey.ContainsKey(definition.StringKey))
                {
                    return SetBuildError("窗口 Catalog 存在重复 StringKey：" + definition.StringKey, out error);
                }

                byStringKey.Add(definition.StringKey, definition);

                if (definition.BuiltInId == ESUIWindowId.None)
                    continue;

                if (byBuiltInId.ContainsKey(definition.BuiltInId))
                {
                    return SetBuildError("窗口 Catalog 存在重复 BuiltInId：" + definition.BuiltInId, out error);
                }

                byBuiltInId.Add(definition.BuiltInId, definition);
            }

            error = null;
            return true;
        }

        private bool SetBuildError(string error, out string outputError)
        {
            indexError = error;
            byBuiltInId.Clear();
            byStringKey.Clear();
            outputError = error;
            return false;
        }

        private void OnValidate()
        {
            indexBuilt = false;
            indexError = null;
            byBuiltInId = null;
            byStringKey = null;
        }
    }
}
