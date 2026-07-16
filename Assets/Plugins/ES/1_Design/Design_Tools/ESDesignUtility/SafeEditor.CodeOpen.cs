using System;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace ES
{
    public static partial class ESDesignUtility
    {
        public static partial class SafeEditor
        {
            public static bool OpenCodeAtLine(string assetOrFullPath, int line, int column = 1)
            {
#if UNITY_EDITOR
                return OpenCodeAtLine_Editor(assetOrFullPath, line, column);
#else
                return false;
#endif
            }

            public static bool OpenFileAtLine(string assetOrFullPath, int line, int column = 1)
            {
                return OpenCodeAtLine(assetOrFullPath, line, column);
            }

#if UNITY_EDITOR
            public static bool OpenMonoScriptAtLine(MonoScript script, int line, int column = 1)
            {
                if (script == null)
                {
                    Debug.LogWarning("打开代码失败：MonoScript 为空。");
                    return false;
                }

                return OpenCodeAtLine(AssetDatabase.GetAssetPath(script), line, column);
            }

            public static bool OpenTypeAtLine(Type type, int line = 1, int column = 1)
            {
                if (type == null)
                {
                    Debug.LogWarning("打开代码失败：Type 为空。");
                    return false;
                }

                MonoScript script = FindMonoScript(type);
                if (script == null)
                {
                    Debug.LogWarning($"打开代码失败：没有找到类型 {type.FullName} 对应的脚本。");
                    return false;
                }

                return OpenMonoScriptAtLine(script, line, column);
            }

            private static bool OpenCodeAtLine_Editor(string assetOrFullPath, int line, int column)
            {
                if (string.IsNullOrWhiteSpace(assetOrFullPath))
                {
                    Debug.LogWarning("打开代码失败：路径为空。");
                    return false;
                }

                line = Mathf.Max(1, line);
                column = Mathf.Max(1, column);

                string normalizedPath = NormalizeCodePath(assetOrFullPath);
                string assetPath = ToCodeAssetPath(normalizedPath);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    if (asset != null)
                    {
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                        return AssetDatabase.OpenAsset(asset, line);
                    }
                }

                string fullPath = ToCodeFullPath(normalizedPath);
                if (!File.Exists(fullPath))
                {
                    Debug.LogWarning($"打开代码失败：文件不存在。路径：{assetOrFullPath}");
                    return false;
                }

                InternalEditorUtility.OpenFileAtLineExternal(fullPath, line);
                return true;
            }

            private static MonoScript FindMonoScript(Type type)
            {
                MonoScript[] runtimeScripts = MonoImporter.GetAllRuntimeMonoScripts();
                for (int i = 0; i < runtimeScripts.Length; i++)
                {
                    MonoScript script = runtimeScripts[i];
                    if (script != null && script.GetClass() == type)
                    {
                        return script;
                    }
                }

                string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript");
                for (int i = 0; i < scriptGuids.Length; i++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(scriptGuids[i]);
                    MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                    if (script != null && script.GetClass() == type)
                    {
                        return script;
                    }
                }

                return null;
            }

            private static string NormalizeCodePath(string path)
            {
                return path.Replace('\\', '/').Trim();
            }

            private static string ToCodeAssetPath(string normalizedPath)
            {
                if (normalizedPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                    string.Equals(normalizedPath, "Assets", StringComparison.Ordinal))
                {
                    return normalizedPath;
                }

                string projectRoot = GetCodeProjectRoot();
                string fullPath = ToCodeFullPath(normalizedPath);
                if (!fullPath.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return fullPath.Substring(projectRoot.Length + 1);
            }

            private static string ToCodeFullPath(string normalizedPath)
            {
                if (Path.IsPathRooted(normalizedPath))
                {
                    return NormalizeCodePath(Path.GetFullPath(normalizedPath));
                }

                return NormalizeCodePath(Path.GetFullPath(Path.Combine(GetCodeProjectRoot(), normalizedPath)));
            }

            private static string GetCodeProjectRoot()
            {
                string dataPath = NormalizeCodePath(Application.dataPath);
                return Path.GetDirectoryName(dataPath)?.Replace('\\', '/') ?? dataPath;
            }
#endif
        }
    }
}
