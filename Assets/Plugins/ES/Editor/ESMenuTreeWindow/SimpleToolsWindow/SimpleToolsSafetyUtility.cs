using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
    internal static class SimpleToolsSafetyUtility
    {
        public const string QuarantineFolder = "Assets/_ESToolQuarantine";

        public static List<GameObject> CollectTargets(GameObject[] roots, bool includeChildren, bool includeInactive = true)
        {
            if (roots == null || roots.Length == 0)
                return new List<GameObject>();

            var set = new HashSet<GameObject>();
            var result = new List<GameObject>();

            foreach (var root in roots)
            {
                if (root == null)
                    continue;

                if (includeChildren)
                {
                    foreach (var transform in root.GetComponentsInChildren<Transform>(includeInactive))
                        AddUnique(transform.gameObject);
                }
                else
                {
                    AddUnique(root);
                }
            }

            return result;

            void AddUnique(GameObject obj)
            {
                if (obj != null && set.Add(obj))
                    result.Add(obj);
            }
        }

        public static bool EnsureAssetFolder(string assetFolder, out string error)
        {
            error = null;
            assetFolder = NormalizeAssetPath(assetFolder);

            if (!IsAssetPath(assetFolder))
            {
                error = "请选择 Assets 目录下的文件夹。";
                return false;
            }

            if (AssetDatabase.IsValidFolder(assetFolder))
                return true;

            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]) || parts[i] == "." || parts[i] == "..")
                {
                    error = "资源文件夹路径包含无效目录名。";
                    return false;
                }

                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    try
                    {
                        string guid = AssetDatabase.CreateFolder(current, parts[i]);
                        if (string.IsNullOrEmpty(guid) && !AssetDatabase.IsValidFolder(next))
                        {
                            error = "创建资源文件夹失败：" + next;
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        error = "创建资源文件夹失败：" + ex.Message;
                        return false;
                    }
                }
                current = next;
            }

            return true;
        }

        public static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/").TrimEnd('/');
        }

        public static bool IsAssetPath(string path)
        {
            path = NormalizeAssetPath(path);
            if (path != "Assets" && !path.StartsWith("Assets/", StringComparison.Ordinal))
                return false;

            return !path.Split('/').Any(part => part == "..");
        }

        public static string AssetPathToFullPath(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (!IsAssetPath(assetPath))
                throw new ArgumentException("Path must be under Assets: " + assetPath);

            if (assetPath == "Assets")
                return Application.dataPath;

            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)));
            string assetsRoot = Path.GetFullPath(Application.dataPath);
            if (!fullPath.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Path resolved outside Assets: " + assetPath);

            return fullPath.Replace("\\", "/");
        }

        public static string GetUniqueAssetPath(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            return AssetDatabase.GenerateUniqueAssetPath(assetPath);
        }

        public static void RunAssetEditing(Action action)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                action?.Invoke();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
        }

        public static bool MoveAssetToQuarantine(string assetPath, out string newPath, out string error)
        {
            newPath = null;
            error = null;
            assetPath = NormalizeAssetPath(assetPath);

            if (!IsAssetPath(assetPath) || !File.Exists(AssetPathToFullPath(assetPath)))
            {
                error = "资源路径无效或文件不存在。";
                return false;
            }

            if (!EnsureAssetFolder(QuarantineFolder, out error))
                return false;

            string fileName = Path.GetFileName(assetPath);
            newPath = GetUniqueAssetPath($"{QuarantineFolder}/{fileName}");
            error = AssetDatabase.MoveAsset(assetPath, newPath);
            return string.IsNullOrEmpty(error);
        }

        public static string JoinPreview(IEnumerable<string> items, int limit = 12)
        {
            var list = items?.Where(s => !string.IsNullOrEmpty(s)).Take(limit + 1).ToList() ?? new List<string>();
            if (list.Count == 0)
                return "无";

            bool overflow = list.Count > limit;
            if (overflow)
                list.RemoveAt(limit);

            return string.Join("\n", list) + (overflow ? "\n..." : string.Empty);
        }
    }
}
