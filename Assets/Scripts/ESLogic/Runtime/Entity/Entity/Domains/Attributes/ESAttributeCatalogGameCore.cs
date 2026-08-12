using System;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Consumer-loaded GameCore root for the Character and Item attribute schemas.
    /// GameCore editor data never crosses this boundary into runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "【ES】/配置/GameCore/属性目录", fileName = "ESAttributeCatalogGameCore")]
    public sealed class ESAttributeCatalogGameCore : ScriptableObject, IGameCoreSO
    {
        [SerializeField] private ESAttributeBakeTable attributeCatalog;
        [SerializeField] private string expectedSchemaHash;

        public ESAttributeBakeTable AttributeCatalog => attributeCatalog;
        public string ExpectedSchemaHash => expectedSchemaHash ?? string.Empty;

        public void SetBakedCatalog(ESAttributeBakeTable catalog)
        {
            attributeCatalog = catalog;
            expectedSchemaHash = catalog != null ? catalog.SchemaHash : string.Empty;
        }

        public void InjectGameCoreTables()
        {
            if (!TryValidate(out string error))
                throw new InvalidOperationException("[ESAttributeCatalog] GameCore injection rejected: " + error);

            ESAttributeRuntimeCatalog.Bind(attributeCatalog, expectedSchemaHash);
        }

        public bool TryValidate(out string error)
        {
            if (attributeCatalog == null)
            {
                error = "属性 Catalog 引用缺失。";
                return false;
            }
            if (!attributeCatalog.TryValidate(out error))
            {
                error = "属性 Catalog 无效：" + error;
                return false;
            }
            if (string.IsNullOrEmpty(expectedSchemaHash))
            {
                error = "属性 SchemaHash 缺失。请从 GameCore 执行 Bake。";
                return false;
            }
            if (!string.Equals(expectedSchemaHash, attributeCatalog.SchemaHash, StringComparison.Ordinal))
            {
                error = "属性 SchemaHash 不匹配。Expected=" + expectedSchemaHash + " Actual=" + attributeCatalog.SchemaHash;
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (attributeCatalog != null && attributeCatalog.TryValidate(out _))
                expectedSchemaHash = attributeCatalog.SchemaHash;
        }
#endif
    }
}
