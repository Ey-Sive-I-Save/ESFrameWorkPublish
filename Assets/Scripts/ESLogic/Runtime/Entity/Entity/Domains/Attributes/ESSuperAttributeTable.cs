using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable]
    public sealed class ESSuperFloatAttributeDefinition
    {
        [LabelText("EnumKey")]
        public ushort enumKey;

        [LabelText("Key")]
        public string key;

        [LabelText("存储策略")]
        public ESKeyStoragePolicy storagePolicy = ESKeyStoragePolicy.Sparse;

        [LabelText("显示名")]
        public string displayName;

        [LabelText("覆盖基础值")]
        public bool overrideBaseValue;

        [LabelText("基础值")]
        [ShowIf(nameof(overrideBaseValue))]
        public float baseValue;

        [LabelText("最小值")]
        public float minValue = float.NegativeInfinity;

        [LabelText("最大值")]
        public float maxValue = float.PositiveInfinity;

        [LabelText("公式（当前不支持，必须为空）")]
        public string formula;

        [LabelText("迁移Key")]
        public string migrationKey;

        public string StringKey => key;
    }

    [Serializable]
    public sealed class ESSuperPermitAttributeDefinition
    {
        [LabelText("EnumKey")]
        public ushort enumKey;

        [LabelText("Key")]
        public string key;

        [LabelText("存储策略")]
        public ESKeyStoragePolicy storagePolicy = ESKeyStoragePolicy.Sparse;

        [LabelText("显示名")]
        public string displayName;

        [LabelText("覆盖默认许可")]
        public bool overrideFallbackValue;

        [LabelText("默认许可")]
        [ShowIf(nameof(overrideFallbackValue))]
        public bool fallbackValue = true;

        [LabelText("公式（当前不支持，必须为空）")]
        public string formula;

        [LabelText("迁移Key")]
        public string migrationKey;

        public string StringKey => key;
    }

    /// <summary>
    /// 通用属性定义表。它只管理稳定键、默认值和定义合法性，绝不持有活动 Modifier 或 Token。
    /// <para>具体领域（角色、载具、武器、建筑）各自用枚举 Catalog 将热点键映射到自己的紧凑运行时槽。</para>
    /// <para>活动 Modifier 由具体宿主持有，例如角色由 <see cref="Entity"/> 直接持有；Buff 域只管理 Buff 生命周期。</para>
    /// </summary>
    [Serializable]
    public class ESSuperAttributeTable
    {
        [LabelText("Catalog Scope")]
        public string catalogScope = ESSuperAttributeCatalog.DefaultScope;

        [LabelText("启用")]
        public bool enabled = true;

        [Title("Float 属性")]
        [TableList(AlwaysExpanded = true)]
        public List<ESSuperFloatAttributeDefinition> floatAttributes = new List<ESSuperFloatAttributeDefinition>();

        [Title("Permit 属性")]
        [TableList(AlwaysExpanded = true)]
        public List<ESSuperPermitAttributeDefinition> permitAttributes = new List<ESSuperPermitAttributeDefinition>();

        [NonSerialized] private Dictionary<string, ESSuperFloatAttributeDefinition> floatByKey;
        [NonSerialized] private Dictionary<string, ESSuperPermitAttributeDefinition> permitByKey;
        [NonSerialized] private ESSuperAttributeCatalog compiledCatalog;
        [NonSerialized, ShowInInspector, ReadOnly, LabelText("属性表校验")]
        private string validationError;

        public bool HasValidationError
        {
            get
            {
                return !ValidateDefinitions(out _);
            }
        }

        public string ValidationError
        {
            get
            {
                ValidateDefinitions(out string error);
                return error;
            }
        }

        public void InvalidateCache()
        {
            floatByKey = null;
            permitByKey = null;
            compiledCatalog = null;
            validationError = null;
        }

        /// <summary>Builds the lookup cache and returns whether every configured key is unique and type-safe.</summary>
        public bool ValidateDefinitions(out string error)
        {
            EnsureLookup();
            if (!string.IsNullOrEmpty(validationError))
            {
                error = validationError;
                return false;
            }

            if (!ESSuperAttributeCatalog.TryCreate(
                    string.IsNullOrEmpty(catalogScope) ? ESSuperAttributeCatalog.DefaultScope : catalogScope,
                    floatAttributes,
                    permitAttributes,
                    out _,
                    out error))
                return false;

            return true;
        }

        /// <summary>
        /// Compiles this configuration boundary through the common catalog. Runtime values should
        /// retain the returned RuntimeKey, while configuration/save/network data retains EnumKey
        /// and/or StringKey. The table itself never owns key allocation.
        /// </summary>
        public bool TryBuildCatalog(out ESSuperAttributeCatalog catalog, out string error)
        {
            EnsureLookup();
            if (!string.IsNullOrEmpty(validationError))
            {
                catalog = null;
                error = validationError;
                return false;
            }

            if (compiledCatalog != null)
            {
                catalog = compiledCatalog;
                error = null;
                return true;
            }

            if (!ESSuperAttributeCatalog.TryCreate(
                    string.IsNullOrEmpty(catalogScope) ? ESSuperAttributeCatalog.DefaultScope : catalogScope,
                    floatAttributes,
                    permitAttributes,
                    out catalog,
                    out error))
                return false;

            compiledCatalog = catalog;
            return true;
        }

        /// <summary>通用键解析。热点领域应由各自的枚举 Catalog 在边界处提供稳定 Key。</summary>
        public bool TryResolveFloatBase(string key, float fallbackValue, out float value)
        {
            return TryResolveFloatBase(0, key, fallbackValue, out value);
        }

        public bool TryResolveFloatBase(ushort enumKey, string key, float fallbackValue, out float value)
        {
            value = fallbackValue;
            if (!enabled || (enumKey == 0 && string.IsNullOrEmpty(key))
                || !TryBuildCatalog(out ESSuperAttributeCatalog catalog, out _)
                || !catalog.TryGetRuntimeKey(enumKey, key, out int runtimeKey))
                return false;

            return catalog.TryResolveFloatBase(runtimeKey, fallbackValue, out value);
        }

        /// <summary>通用键解析。热点领域应由各自的枚举 Catalog 在边界处提供稳定 Key。</summary>
        public bool TryResolvePermitFallback(string key, bool fallbackValue, out bool value)
        {
            return TryResolvePermitFallback(0, key, fallbackValue, out value);
        }

        public bool TryResolvePermitFallback(ushort enumKey, string key, bool fallbackValue, out bool value)
        {
            value = fallbackValue;
            if (!enabled || (enumKey == 0 && string.IsNullOrEmpty(key))
                || !TryBuildCatalog(out ESSuperAttributeCatalog catalog, out _)
                || !catalog.TryGetRuntimeKey(enumKey, key, out int runtimeKey))
                return false;

            return catalog.TryResolvePermitFallback(runtimeKey, fallbackValue, out value);
        }

        public bool TryGetRuntimeKey(ushort enumKey, string key, out int runtimeKey)
        {
            runtimeKey = 0;
            return enabled
                   && (enumKey != 0 || !string.IsNullOrEmpty(key))
                   && TryBuildCatalog(out ESSuperAttributeCatalog catalog, out _)
                   && catalog.TryGetRuntimeKey(enumKey, key, out runtimeKey);
        }

        private void EnsureLookup()
        {
            if (floatByKey != null && permitByKey != null)
                return;

            floatByKey = new Dictionary<string, ESSuperFloatAttributeDefinition>(StringComparer.Ordinal);
            permitByKey = new Dictionary<string, ESSuperPermitAttributeDefinition>(StringComparer.Ordinal);
            validationError = null;

            AddFloatDefinitions();
            AddPermitDefinitions();
            ValidateCrossKindKeys();
        }

        private void AddFloatDefinitions()
        {
            if (floatAttributes == null)
                return;

            for (int i = 0; i < floatAttributes.Count; i++)
            {
                ESSuperFloatAttributeDefinition definition = floatAttributes[i];
                if (definition == null)
                    continue;

                if (definition.enumKey == 0 && string.IsNullOrEmpty(definition.key))
                {
                    AddValidationError("Float 属性存在空 EnumKey/StringKey。");
                    continue;
                }

                if (string.IsNullOrEmpty(definition.key))
                    continue;

                if (floatByKey.ContainsKey(definition.key))
                {
                    AddValidationError("Float 属性 Key 重复：" + definition.key);
                    continue;
                }

                floatByKey.Add(definition.key, definition);
            }
        }

        private void AddPermitDefinitions()
        {
            if (permitAttributes == null)
                return;

            for (int i = 0; i < permitAttributes.Count; i++)
            {
                ESSuperPermitAttributeDefinition definition = permitAttributes[i];
                if (definition == null)
                    continue;

                if (definition.enumKey == 0 && string.IsNullOrEmpty(definition.key))
                {
                    AddValidationError("Permit 属性存在空 EnumKey/StringKey。");
                    continue;
                }

                if (string.IsNullOrEmpty(definition.key))
                    continue;

                if (permitByKey.ContainsKey(definition.key))
                {
                    AddValidationError("Permit 属性 Key 重复：" + definition.key);
                    continue;
                }

                permitByKey.Add(definition.key, definition);
            }
        }

        private void ValidateCrossKindKeys()
        {
            foreach (string key in floatByKey.Keys)
            {
                if (permitByKey.ContainsKey(key))
                    AddValidationError("同一 Key 同时声明为 Float 和 Permit：" + key);
            }
        }

        private void AddValidationError(string message)
        {
            validationError = string.IsNullOrEmpty(validationError)
                ? message
                : validationError + "\n" + message;
        }

    }

}
