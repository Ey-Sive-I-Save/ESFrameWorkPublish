using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// DefinitionKey 到相机视图定义的只读索引。索引仅在资产载入或 Inspector 改动后重建，
    /// Director 的热路径不会扫描 List 或执行资源查询。
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "ES", sourceAssembly: "ES_Logic", sourceClassName: "ESCameraProfileCatalog")]
    [CreateAssetMenu(menuName = "【ES】/配置/相机/相机视图定义索引", fileName = "ESCameraViewDefinitionCatalog")]
    public sealed class ESCameraViewDefinitionCatalog : ScriptableObject
    {
        [UnityEngine.Serialization.FormerlySerializedAs("profiles")]
        [SerializeField] private List<ESCameraViewDefinition> definitions = new List<ESCameraViewDefinition>();

        [NonSerialized] private ESKeyCatalog keyCatalog;
        [NonSerialized] private Dictionary<int, ESCameraViewDefinition> byRuntimeKey;
        [NonSerialized] private int catalogIdentity;
        [NonSerialized] private int catalogGeneration;
        [NonSerialized] private bool isValid;
        [NonSerialized] private string buildError;
        private static int nextCatalogIdentity;

        public bool IsValid
        {
            get
            {
                EnsureIndex();
                return isValid;
            }
        }

        public string BuildError
        {
            get
            {
                EnsureIndex();
                return buildError;
            }
        }

        /// <summary>仅稳定 Key 及其声明 Schema 的哈希；不表示 FOV、Rig 或灵敏度等内容载荷版本。</summary>
        public string KeySchemaHash
        {
            get
            {
                EnsureIndex();
                return keyCatalog != null && keyCatalog.IsBuilt ? keyCatalog.SchemaHash : string.Empty;
            }
        }

        public bool TryResolve(ESCameraDefinitionReference reference, out ESCameraDefinitionRuntimeHandle handle)
        {
            handle = default;
            EnsureIndex();
            if (!isValid || !reference.IsConfigured || !keyCatalog.TryGetRuntimeKey(reference.ToStableKey(), out int runtimeKey))
                return false;

            handle = new ESCameraDefinitionRuntimeHandle(catalogIdentity, catalogGeneration, runtimeKey, keyCatalog.SchemaHash);
            return true;
        }

        public bool TryGet(ESCameraDefinitionRuntimeHandle handle, out ESCameraViewDefinition definition)
        {
            definition = null;
            EnsureIndex();
            return isValid
                   && handle.IsValid
                   && handle.catalogIdentity == catalogIdentity
                   && handle.catalogGeneration == catalogGeneration
                   && string.Equals(handle.keySchemaHash, keyCatalog.SchemaHash, StringComparison.Ordinal)
                   && byRuntimeKey.TryGetValue(handle.runtimeKey, out definition)
                   && definition != null;
        }

        /// <summary>SceneBinding 的冷路径配置校验。Definition 仍是唯一身份权威，Rig Catalog
        /// 仅验证其内部依赖，业务侧不会看到 RigKey。</summary>
        public bool TryValidateRigDependencies(ESCameraRigCatalog rigCatalog, out string error)
        {
            EnsureIndex();
            if (!isValid)
            {
                error = buildError ?? "[ESCamera] Definition Catalog 无效。";
                return false;
            }

            if (rigCatalog == null || !rigCatalog.IsValid)
            {
                error = rigCatalog != null ? rigCatalog.BuildError : "[ESCamera] 缺少 Rig Catalog。";
                return false;
            }

            foreach (ESCameraViewDefinition definition in byRuntimeKey.Values)
            {
                if (!rigCatalog.TryGetPrefab(definition.rigKey, out _))
                {
                    error = "[ESCamera] Definition '" + definition.Definition + "' 引用了不存在的 RigKey：" + definition.rigKey;
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private void OnEnable()
        {
            RebuildIndex();
        }

#if UNITY_EDITOR
        /// <summary>明确的内容制作入口；避免编辑器工具窥探或反射 Runtime 私有字段。</summary>
        public void SetDefinitionsForAuthoring(IReadOnlyList<ESCameraViewDefinition> source)
        {
            definitions.Clear();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    if (source[i] != null)
                        definitions.Add(source[i]);
                }
            }

            RebuildIndex();
        }

        /// <summary>编辑器迁移器与 Picker 的受控读取入口。它返回此 Catalog 已验证的内容；
        /// 调用方不得绕过 Catalog 直接扫描孤立 Definition。</summary>
        public bool TryCopyDefinitionsForAuthoring(List<ESCameraViewDefinition> destination, out string error)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            EnsureIndex();
            if (!isValid)
            {
                error = buildError ?? "[ESCamera] Definition Catalog 无效。";
                return false;
            }

            destination.AddRange(definitions);
            error = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            RebuildIndex();
        }
#endif

        private void EnsureIndex()
        {
            if (keyCatalog == null || byRuntimeKey == null)
                RebuildIndex();
        }

        private void RebuildIndex()
        {
            if (catalogIdentity == 0)
                catalogIdentity = ++nextCatalogIdentity;

            catalogGeneration++;
            isValid = false;
            buildError = null;
            keyCatalog = new ESKeyCatalog(name + "/Camera.ViewDefinition", ESCameraDefinitionReference.Scope);
            if (byRuntimeKey == null)
                byRuntimeKey = new Dictionary<int, ESCameraViewDefinition>(definitions != null ? definitions.Count : 0);
            else
                byRuntimeKey.Clear();

            if (definitions == null || definitions.Count == 0)
            {
                buildError = "[ESCamera] Definition Catalog 不允许为空。";
                return;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                ESCameraViewDefinition definition = definitions[i];
                if (definition == null)
                {
                    buildError = "[ESCamera] Definition Catalog 包含空条目，索引构建已拒绝。";
                    return;
                }

                if (!definition.IsValid)
                {
                    buildError = "[ESCamera] Definition '" + definition.name + "' 缺少稳定引用或 RigKey；旧字符串不会作为运行时 fallback。";
                    return;
                }

                keyCatalog.Declare(new ESKeyDeclaration
                {
                    key = definition.Definition.ToStableKey(),
                    kind = ESKeyCatalogKind.Config,
                    valueKind = ESKeyValueKind.Object,
                    storagePolicy = ESKeyStoragePolicy.HotSlot,
                    schemaSignature = "ESCameraViewDefinition/v1",
                    declaredBy = definition.name,
                });
            }

            if (!keyCatalog.TryBuild(out buildError))
            {
                buildError = "[ESCamera] Definition Catalog 构建失败：" + buildError;
                return;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                ESCameraViewDefinition definition = definitions[i];
                if (!keyCatalog.TryGetRuntimeKey(definition.Definition.ToStableKey(), out int runtimeKey)
                    || byRuntimeKey.ContainsKey(runtimeKey))
                {
                    buildError = "[ESCamera] Definition Catalog 出现重复或不可解析的运行时键：" + definition.name;
                    byRuntimeKey.Clear();
                    return;
                }

                byRuntimeKey.Add(runtimeKey, definition);
            }

            isValid = true;
        }
    }
}
