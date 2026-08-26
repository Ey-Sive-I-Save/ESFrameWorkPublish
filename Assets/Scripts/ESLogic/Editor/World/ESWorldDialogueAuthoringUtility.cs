#if UNITY_EDITOR
using System;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ES
{
    internal readonly struct ESWorldDialogueSaveResult
    {
        public readonly bool success;
        public readonly bool contentChanged;
        public readonly int contentVersion;
        public readonly string contentHash;
        public readonly string error;

        public ESWorldDialogueSaveResult(bool success, bool contentChanged, int contentVersion, string contentHash, string error)
        {
            this.success = success;
            this.contentChanged = contentChanged;
            this.contentVersion = contentVersion;
            this.contentHash = contentHash;
            this.error = error;
        }
    }

    internal static class ESWorldDialogueAuthoringUtility
    {
        public static void MarkChanged(ESWorldDialogueGraphAsset asset)
        {
            if (asset != null) EditorUtility.SetDirty(asset);
        }

        public static ESWorldDialogueSaveResult Save(ESWorldDialogueGraphAsset asset, SerializedObject serializedObject = null)
        {
            if (asset == null) return new ESWorldDialogueSaveResult(false, false, 0, string.Empty, "未绑定对话图资产。");
            try
            {
                serializedObject?.ApplyModifiedProperties();
                ESWorldDialogueGraphDefinition definition = asset.Definition;
                if (definition == null) return new ESWorldDialogueSaveResult(false, false, 0, string.Empty, "对话图资产缺少定义。");
                if (!definition.IsValid(out string error))
                    return new ESWorldDialogueSaveResult(false, false, definition.contentVersion, definition.contentHash, error);

                string nextHash = ComputeContentHash(asset);
                bool changed = !string.Equals(definition.contentHash, nextHash, StringComparison.Ordinal);
                if (changed)
                {
                    definition.contentVersion = Mathf.Max(1, definition.contentVersion) + 1;
                    definition.contentHash = nextHash;
                }
                else if (string.IsNullOrWhiteSpace(definition.contentHash))
                {
                    definition.contentHash = nextHash;
                    changed = true;
                }
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
                return new ESWorldDialogueSaveResult(true, changed, definition.contentVersion, definition.contentHash, null);
            }
            catch (Exception exception)
            {
                ESWorldDialogueGraphDefinition definition = asset.Definition;
                return new ESWorldDialogueSaveResult(false, false, definition == null ? 0 : definition.contentVersion,
                    definition == null ? string.Empty : definition.contentHash, exception.Message);
            }
        }

        private static string ComputeContentHash(ESWorldDialogueGraphAsset asset)
        {
            ESWorldDialogueGraphAsset snapshot = UnityEngine.Object.Instantiate(asset);
            string json;
            try
            {
                snapshot.hideFlags = HideFlags.HideAndDontSave;
                snapshot.Definition.contentHash = string.Empty;
                snapshot.Definition.contentVersion = 0;
                json = EditorJsonUtility.ToJson(snapshot);
            }
            finally
            {
                if (snapshot != null)
                    UnityEngine.Object.DestroyImmediate(snapshot);
            }
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json ?? string.Empty));
                StringBuilder builder = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
                return builder.ToString();
            }
        }

        public static string GetAssetKey(ESWorldDialogueGraphAsset asset)
        {
            return asset == null || asset.Definition == null ? string.Empty : asset.Definition.graphId ?? string.Empty;
        }
    }
}
#endif
