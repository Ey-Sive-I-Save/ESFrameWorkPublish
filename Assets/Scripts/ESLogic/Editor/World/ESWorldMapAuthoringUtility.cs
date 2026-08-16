#if UNITY_EDITOR
using System;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ES
{
    internal readonly struct ESWorldMapSaveResult
    {
        public readonly bool success;
        public readonly bool contentChanged;
        public readonly int contentVersion;
        public readonly string contentHash;
        public readonly string error;

        public ESWorldMapSaveResult(bool success, bool contentChanged, int contentVersion, string contentHash, string error)
        {
            this.success = success;
            this.contentChanged = contentChanged;
            this.contentVersion = contentVersion;
            this.contentHash = contentHash;
            this.error = error;
        }
    }

    /// <summary>地图作者态唯一保存入口：签名、版本、Dirty 与磁盘落盘在同一提交边界完成。</summary>
    internal static class ESWorldMapAuthoringUtility
    {
        public static void MarkChanged(ESWorldMapAsset asset)
        {
            if (asset != null) EditorUtility.SetDirty(asset);
        }

        public static ESWorldMapSaveResult Save(ESWorldMapAsset asset, SerializedObject serializedObject = null)
        {
            if (asset == null) return new ESWorldMapSaveResult(false, false, 0, string.Empty, "未绑定地图资产。");
            try
            {
                serializedObject?.ApplyModifiedProperties();
                ESWorldMapDefinition definition = asset.Definition;
                if (definition == null) return new ESWorldMapSaveResult(false, false, 0, string.Empty, "地图资产缺少定义。");

                string nextHash = ComputeContentHash(asset);
                bool changed = !string.Equals(definition.contentHash, nextHash, StringComparison.Ordinal);
                if (changed)
                {
                    definition.contentVersion = Mathf.Max(1, definition.contentVersion) + 1;
                    definition.contentHash = nextHash;
                }
                else if (string.IsNullOrWhiteSpace(definition.contentHash))
                {
                    definition.contentVersion = Mathf.Max(1, definition.contentVersion);
                    definition.contentHash = nextHash;
                    changed = true;
                }

                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
                return new ESWorldMapSaveResult(true, changed, definition.contentVersion, definition.contentHash, null);
            }
            catch (Exception exception)
            {
                return new ESWorldMapSaveResult(false, false, asset.Definition == null ? 0 : asset.Definition.contentVersion,
                    asset.Definition == null ? string.Empty : asset.Definition.contentHash, exception.Message);
            }
        }

        private static string ComputeContentHash(ESWorldMapAsset asset)
        {
            ESWorldMapAsset snapshot = UnityEngine.Object.Instantiate(asset);
            snapshot.hideFlags = HideFlags.HideAndDontSave;
            snapshot.Definition.contentHash = string.Empty;
            snapshot.Definition.contentVersion = 0;
            string json = EditorJsonUtility.ToJson(snapshot);
            UnityEngine.Object.DestroyImmediate(snapshot);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json ?? string.Empty));
                StringBuilder builder = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
#endif
