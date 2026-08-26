using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>第五步远端发布的非敏感项目配置。访问密钥只允许由 Provider 的凭据源在执行时读取。</summary>
    public sealed class ESAssetReleaseUploadSettings : ScriptableObject
    {
        internal const string AssetPath = "Assets/ESNormalAssets/Data/GlobalData/AssetSettings/ESAssetReleaseUploadSettings.asset";

        public ESAssetReleaseUploadTarget target = new ESAssetReleaseUploadTarget();

        internal static ESAssetReleaseUploadSettings Load()
        {
            return AssetDatabase.LoadAssetAtPath<ESAssetReleaseUploadSettings>(AssetPath);
        }

        internal static ESAssetReleaseUploadSettings Create()
        {
            ESAssetReleaseUploadSettings existing = Load();
            if (existing != null) return existing;
            string folder = Path.GetDirectoryName(AssetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder)) throw new InvalidOperationException("远端发布设置路径无效。");
            Directory.CreateDirectory(folder);
            var settings = CreateInstance<ESAssetReleaseUploadSettings>();
            try
            {
                AssetDatabase.CreateAsset(settings, AssetPath);
            }
            catch
            {
                if (settings != null && !EditorUtility.IsPersistent(settings))
                    DestroyImmediate(settings);
                throw;
            }
            Undo.RegisterCreatedObjectUndo(settings, "创建远端发布配置");
            AssetDatabase.SaveAssetIfDirty(settings);
            return settings;
        }
    }
}
