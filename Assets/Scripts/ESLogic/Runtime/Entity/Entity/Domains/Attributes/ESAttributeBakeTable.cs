using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Read-only GameCore bake output for the Character and Item attribute schemas.
    /// It stores stable definitions only. Dense RuntimeKey values are rebuilt per process by
    /// <see cref="ESSuperAttributeCatalog"/> and never enter this asset.
    /// </summary>
    [CreateAssetMenu(menuName = "【ES】/配置/GameCore/属性烘焙表", fileName = "ESAttributeBakeTable")]
    public sealed class ESAttributeBakeTable : ScriptableObject
    {
        public const string CharacterScope = "Attribute.Character";
        public const string ItemScope = "Attribute.Item";

        [SerializeField, ReadOnly, LabelText("角色 Float（由 GameCore 生成）")]
        private List<ESSuperFloatAttributeDefinition> characterFloatAttributes = new List<ESSuperFloatAttributeDefinition>();

        [SerializeField, ReadOnly, LabelText("角色 Permit（由 GameCore 生成）")]
        private List<ESSuperPermitAttributeDefinition> characterPermitAttributes = new List<ESSuperPermitAttributeDefinition>();

        [SerializeField, ReadOnly, LabelText("物品 Float（由 GameCore 生成）")]
        private List<ESSuperFloatAttributeDefinition> itemFloatAttributes = new List<ESSuperFloatAttributeDefinition>();

        [SerializeField, ReadOnly, LabelText("物品 Permit（由 GameCore 生成）")]
        private List<ESSuperPermitAttributeDefinition> itemPermitAttributes = new List<ESSuperPermitAttributeDefinition>();

        [SerializeField, ReadOnly, LabelText("Schema Hash")]
        private string schemaHash;

        [NonSerialized] private ESSuperAttributeCatalog characterCatalog;
        [NonSerialized] private ESSuperAttributeCatalog itemCatalog;
        [NonSerialized] private string runtimeError;

        public IReadOnlyList<ESSuperFloatAttributeDefinition> CharacterFloatAttributes => characterFloatAttributes;
        public IReadOnlyList<ESSuperPermitAttributeDefinition> CharacterPermitAttributes => characterPermitAttributes;
        public IReadOnlyList<ESSuperFloatAttributeDefinition> ItemFloatAttributes => itemFloatAttributes;
        public IReadOnlyList<ESSuperPermitAttributeDefinition> ItemPermitAttributes => itemPermitAttributes;
        public string SchemaHash => schemaHash ?? string.Empty;

        /// <summary>
        /// Validates the generated payload and confirms that its persisted handshake was produced
        /// from exactly these definitions.
        /// </summary>
        public bool TryValidate(out string error)
        {
            if (!TryBuildCatalogs(out _, out _, out error))
                return false;

            string actualSchemaHash = CalculateSchemaHash(characterCatalog.SchemaHash, itemCatalog.SchemaHash);
            if (string.IsNullOrEmpty(schemaHash))
            {
                error = "属性 Bake 表缺少 SchemaHash。请从 GameCore 执行 Bake。";
                return false;
            }

            if (!string.Equals(schemaHash, actualSchemaHash, StringComparison.Ordinal))
            {
                error = "属性 Bake 表 SchemaHash 不匹配。Expected=" + schemaHash + " Actual=" + actualSchemaHash;
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Builds process-local Catalogs from this immutable bake output. The returned Catalogs
        /// share no runtime allocation with GameCore editor configuration.
        /// </summary>
        public bool TryBuildCatalogs(
            out ESSuperAttributeCatalog character,
            out ESSuperAttributeCatalog item,
            out string error)
        {
            if (characterCatalog != null && itemCatalog != null)
            {
                character = characterCatalog;
                item = itemCatalog;
                error = runtimeError;
                return string.IsNullOrEmpty(error);
            }

            if (!ESSuperAttributeCatalog.TryCreate(
                    CharacterScope,
                    characterFloatAttributes,
                    characterPermitAttributes,
                    out character,
                    out error))
            {
                runtimeError = "角色属性无效：" + error;
                character = null;
                item = null;
                return false;
            }

            if (!ESSuperAttributeCatalog.TryCreate(
                    ItemScope,
                    itemFloatAttributes,
                    itemPermitAttributes,
                    out item,
                    out error))
            {
                runtimeError = "物品属性无效：" + error;
                character = null;
                item = null;
                return false;
            }

            characterCatalog = character;
            itemCatalog = item;
            runtimeError = null;
            error = null;
            return true;
        }

        /// <summary>
        /// The only write entry used by the GameCore Bake command. Validation happens against
        /// temporary Catalogs before this asset is touched, so a rejected Bake preserves the old
        /// payload and SchemaHash.
        /// </summary>
        public bool TryReplaceFrom(
            ESSuperAttributeTable characterSource,
            ESSuperAttributeTable itemSource,
            out string error)
        {
            if (!TryBuildSourceCatalogs(characterSource, itemSource, out ESSuperAttributeCatalog character, out ESSuperAttributeCatalog item, out error))
                return false;

            List<ESSuperFloatAttributeDefinition> nextCharacterFloat = CloneFloats(characterSource.floatAttributes);
            List<ESSuperPermitAttributeDefinition> nextCharacterPermit = ClonePermits(characterSource.permitAttributes);
            List<ESSuperFloatAttributeDefinition> nextItemFloat = CloneFloats(itemSource.floatAttributes);
            List<ESSuperPermitAttributeDefinition> nextItemPermit = ClonePermits(itemSource.permitAttributes);
            string nextSchemaHash = CalculateSchemaHash(character.SchemaHash, item.SchemaHash);

            characterFloatAttributes = nextCharacterFloat;
            characterPermitAttributes = nextCharacterPermit;
            itemFloatAttributes = nextItemFloat;
            itemPermitAttributes = nextItemPermit;
            schemaHash = nextSchemaHash;
            characterCatalog = character;
            itemCatalog = item;
            runtimeError = null;
            error = null;
            return true;
        }

        public static bool TryValidateSources(
            ESSuperAttributeTable characterSource,
            ESSuperAttributeTable itemSource,
            out string error)
        {
            return TryBuildSourceCatalogs(characterSource, itemSource, out _, out _, out error);
        }

        private static bool TryBuildSourceCatalogs(
            ESSuperAttributeTable characterSource,
            ESSuperAttributeTable itemSource,
            out ESSuperAttributeCatalog character,
            out ESSuperAttributeCatalog item,
            out string error)
        {
            character = null;
            item = null;
            if (characterSource == null)
            {
                error = "缺少角色属性表。";
                return false;
            }
            if (itemSource == null)
            {
                error = "缺少物品属性表。";
                return false;
            }
            if (!string.Equals(characterSource.catalogScope, CharacterScope, StringComparison.Ordinal))
            {
                error = "角色属性表 Scope 必须为 " + CharacterScope + "。";
                return false;
            }
            if (!string.Equals(itemSource.catalogScope, ItemScope, StringComparison.Ordinal))
            {
                error = "物品属性表 Scope 必须为 " + ItemScope + "。";
                return false;
            }
            if (!characterSource.enabled || !itemSource.enabled)
            {
                error = "角色与物品属性表必须启用，不能 Bake 一个半可用的 Schema。";
                return false;
            }
            if (!TryValidateFixedApiUsage(characterSource, itemSource, out error))
                return false;
            if (!ESSuperAttributeCatalog.TryCreate(
                    CharacterScope,
                    characterSource.floatAttributes,
                    characterSource.permitAttributes,
                    out character,
                    out error))
            {
                error = "角色属性表无效：" + error;
                return false;
            }
            if (!ESSuperAttributeCatalog.TryCreate(
                    ItemScope,
                    itemSource.floatAttributes,
                    itemSource.permitAttributes,
                    out item,
                    out error))
            {
                error = "物品属性表无效：" + error;
                character = null;
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryValidateFixedApiUsage(
            ESSuperAttributeTable characterSource,
            ESSuperAttributeTable itemSource,
            out string error)
        {
            if (!TryValidateCharacterFixedApiUsage(characterSource.floatAttributes, "Float", out error)
                || !TryValidateCharacterFixedApiUsage(characterSource.permitAttributes, "Permit", out error)
                || !TryRejectItemFixedApiUsage(itemSource.floatAttributes, "Float", out error)
                || !TryRejectItemFixedApiUsage(itemSource.permitAttributes, "Permit", out error))
                return false;

            error = null;
            return true;
        }

        private static bool TryValidateCharacterFixedApiUsage(
            List<ESSuperFloatAttributeDefinition> definitions,
            string kind,
            out string error)
        {
            if (definitions != null)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    ESSuperFloatAttributeDefinition definition = definitions[i];
                    if (definition == null || string.IsNullOrEmpty(definition.fixedApiName))
                        continue;
                    if (string.IsNullOrWhiteSpace(definition.fixedApiName))
                    {
                        error = "角色 " + kind + " definition[" + i + "] 的 fixedApiName 不能只包含空白。";
                        return false;
                    }
                    if (definition.storagePolicy != ESKeyStoragePolicy.HotSlot)
                    {
                        error = "角色 " + kind + " definition[" + i + "] 仅 HotSlot 可配置 fixedApiName。";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        private static bool TryValidateCharacterFixedApiUsage(
            List<ESSuperPermitAttributeDefinition> definitions,
            string kind,
            out string error)
        {
            if (definitions != null)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    ESSuperPermitAttributeDefinition definition = definitions[i];
                    if (definition == null || string.IsNullOrEmpty(definition.fixedApiName))
                        continue;
                    if (string.IsNullOrWhiteSpace(definition.fixedApiName))
                    {
                        error = "角色 " + kind + " definition[" + i + "] 的 fixedApiName 不能只包含空白。";
                        return false;
                    }
                    if (definition.storagePolicy != ESKeyStoragePolicy.HotSlot)
                    {
                        error = "角色 " + kind + " definition[" + i + "] 仅 HotSlot 可配置 fixedApiName。";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        private static bool TryRejectItemFixedApiUsage(
            List<ESSuperFloatAttributeDefinition> definitions,
            string kind,
            out string error)
        {
            if (definitions != null)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    ESSuperFloatAttributeDefinition definition = definitions[i];
                    if (definition != null && !string.IsNullOrEmpty(definition.fixedApiName))
                    {
                        error = "物品 " + kind + " definition[" + i + "] 不支持 fixedApiName。"
                                + "Item 统一使用 Catalog HotSlot/Sparse，不生成角色固定 API。";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        private static bool TryRejectItemFixedApiUsage(
            List<ESSuperPermitAttributeDefinition> definitions,
            string kind,
            out string error)
        {
            if (definitions != null)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    ESSuperPermitAttributeDefinition definition = definitions[i];
                    if (definition != null && !string.IsNullOrEmpty(definition.fixedApiName))
                    {
                        error = "物品 " + kind + " definition[" + i + "] 不支持 fixedApiName。"
                                + "Item 统一使用 Catalog HotSlot/Sparse，不生成角色固定 API。";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        private static string CalculateSchemaHash(string characterSchemaHash, string itemSchemaHash)
        {
            ulong hash = 14695981039346656037UL;
            hash = AppendHash(hash, "ESAttributeBakeTable/v1");
            hash = AppendHash(hash, CharacterScope);
            hash = AppendHash(hash, characterSchemaHash);
            hash = AppendHash(hash, ItemScope);
            hash = AppendHash(hash, itemSchemaHash);
            return hash.ToString("X16");
        }

        // ESKeyHash is intentionally internal to ES_Design. The bake asset lives in ES_Logic,
        // so it uses the same FNV-1a wire format locally instead of widening a cross-assembly API.
        private static ulong AppendHash(ulong hash, string value)
        {
            const ulong prime = 1099511628211UL;
            if (value == null)
                return (hash ^ 0) * prime;

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                hash = (hash ^ (byte)character) * prime;
                hash = (hash ^ (byte)(character >> 8)) * prime;
            }

            return (hash ^ 0xFF) * prime;
        }

        private static List<ESSuperFloatAttributeDefinition> CloneFloats(List<ESSuperFloatAttributeDefinition> source)
        {
            int count = source != null ? source.Count : 0;
            List<ESSuperFloatAttributeDefinition> clone = new List<ESSuperFloatAttributeDefinition>(count);
            for (int i = 0; i < count; i++)
            {
                ESSuperFloatAttributeDefinition value = source[i];
                if (value == null)
                    continue;

                clone.Add(new ESSuperFloatAttributeDefinition
                {
                    enumKey = value.enumKey,
                    key = value.key,
                    storagePolicy = value.storagePolicy,
                    fixedApiName = value.fixedApiName,
                    displayName = value.displayName,
                    overrideBaseValue = value.overrideBaseValue,
                    baseValue = value.baseValue,
                    minValue = value.minValue,
                    maxValue = value.maxValue,
                    formula = value.formula,
                    migrationKey = value.migrationKey
                });
            }

            return clone;
        }

        private static List<ESSuperPermitAttributeDefinition> ClonePermits(List<ESSuperPermitAttributeDefinition> source)
        {
            int count = source != null ? source.Count : 0;
            List<ESSuperPermitAttributeDefinition> clone = new List<ESSuperPermitAttributeDefinition>(count);
            for (int i = 0; i < count; i++)
            {
                ESSuperPermitAttributeDefinition value = source[i];
                if (value == null)
                    continue;

                clone.Add(new ESSuperPermitAttributeDefinition
                {
                    enumKey = value.enumKey,
                    key = value.key,
                    storagePolicy = value.storagePolicy,
                    fixedApiName = value.fixedApiName,
                    displayName = value.displayName,
                    overrideFallbackValue = value.overrideFallbackValue,
                    fallbackValue = value.fallbackValue,
                    formula = value.formula,
                    migrationKey = value.migrationKey
                });
            }

            return clone;
        }

        private void OnEnable()
        {
            characterCatalog = null;
            itemCatalog = null;
            runtimeError = null;
        }
    }
}
