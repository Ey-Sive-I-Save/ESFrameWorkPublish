using System;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Consumer-loaded runtime root for the project's single Tag Catalog.
    /// It binds the Catalog only after the resource provider has loaded this GameCore asset.
    /// </summary>
    [CreateAssetMenu(menuName = "【ES】/配置/GameCore/标签目录", fileName = "ESTagCatalogGameCore")]
    public sealed class ESTagCatalogGameCore : ScriptableObject, IGameCoreSO
    {
        [SerializeField] private ESTagBakeTable tagCatalog;
        [SerializeField] private string expectedSchemaHash;

        public ESTagBakeTable TagCatalog => tagCatalog;
        public string ExpectedSchemaHash => expectedSchemaHash ?? string.Empty;

        public void SetBakedCatalog(ESTagBakeTable catalog)
        {
            tagCatalog = catalog;
            expectedSchemaHash = catalog != null ? catalog.SchemaHash : string.Empty;
        }

        public void InjectGameCoreTables()
        {
            if (!TryValidate(out string error))
                throw new InvalidOperationException("[ESTagCatalog] GameCore injection rejected: " + error);

            ESTagRuntimeCatalog.Bind(tagCatalog, expectedSchemaHash);
        }

        public bool TryValidate(out string error)
        {
            if (tagCatalog == null)
            {
                error = "Tag Catalog reference is missing.";
                return false;
            }

            if (!tagCatalog.TryValidate(out error))
            {
                error = "Tag Catalog is invalid: " + error;
                return false;
            }

            if (string.IsNullOrEmpty(expectedSchemaHash))
            {
                error = "Expected SchemaHash is missing. Bake and save the Tag Catalog before release.";
                return false;
            }

            if (!string.Equals(expectedSchemaHash, tagCatalog.SchemaHash, StringComparison.Ordinal))
            {
                error = "SchemaHash mismatch. Expected=" + expectedSchemaHash + " Actual=" + tagCatalog.SchemaHash;
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (tagCatalog != null && tagCatalog.TryValidate(out _))
                expectedSchemaHash = tagCatalog.SchemaHash;
        }
#endif
    }
}
