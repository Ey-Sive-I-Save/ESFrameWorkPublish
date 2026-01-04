using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.Utilities;
using System.IO;
using System.Diagnostics;

#if UNITY_EDITOR
using UnityEditor;
#endif


#if UNITY_EDITOR

#endif


namespace ES
{
    internal static partial class ESStandUtility
    {
        //
        public class SafeEditor
        {
            #region 获取特殊数据
            public static string[] GetAllTags()
            {
#if UNITY_EDITOR
                // 获取Tags
                var tags = UnityEditorInternal.InternalEditorUtility.tags;
                return tags;
#else
                return new string[0];
#endif
            }
            public static Dictionary<int, string> GetAllLayers()
            {

#if UNITY_EDITOR
                Dictionary<int, string> keyValuePairs = new Dictionary<int, string>();
                // 获取Tags管理器
                var layers = UnityEditorInternal.InternalEditorUtility.layers;
                foreach (var i in layers)
                {
                    int mask = LayerMask.GetMask(i);
                    int layer = mask > 0 ? (int)Mathf.Round(Mathf.Log(mask, (2))) : 0;
                    keyValuePairs.TryAdd(layer, i);
                }
                return keyValuePairs;
#else
                return new Dictionary<int, string>();
#endif
            }

            public static void AddTag(string tag)
            {
#if UNITY_EDITOR
                // 获取Tags
                UnityEditorInternal.InternalEditorUtility.AddTag(tag);
#endif
            }

            #endregion

            #region 简单包装
            public static void Wrap_SetDirty(UnityEngine.Object which, bool Refresh = true)
            {
#if UNITY_EDITOR
                if (Refresh)
                {
                    AssetDatabase.Refresh();
                    AssetDatabase.SaveAssets();
                }
                EditorUtility.SetDirty(which);
#endif
            }

            public static bool Wrap_DisplayDialog(string title, string message, string ok = "好的", string cancel = "算了")
            {
#if UNITY_EDITOR

                return EditorUtility.DisplayDialog(title, message, ok, cancel);
#else
                return false;
#endif

            }

            public static string Wrap_OpenSelectorFolderPanel(string targetPath = "Assets", string title = "选择文件夹")
            {
#if UNITY_EDITOR
                return EditorUtility.OpenFolderPanel(title, targetPath, "");
#else
                return targetPath;
#endif
            }

            public static bool Wrap_IsValidFolder(string path, bool IfPlayerRunTime = false)
            {
#if UNITY_EDITOR
                return AssetDatabase.IsValidFolder(path);
#else
                return IfPlayerRunTime;
#endif
            }

            public static void Wrap_CreateFolderDic(string parentPath, string folderName)
            {
#if UNITY_EDITOR
                AssetDatabase.CreateFolder(parentPath, folderName);
#endif
            }
         
            public static void Wrap_SystemCopyBuffer(string content)
            {
                GUIUtility.systemCopyBuffer = content;
            }

            public static string Wrap_GetAssetPath(UnityEngine.Object s)
            {
#if UNITY_EDITOR
                if (s != null)
                {
                    string path = AssetDatabase.GetAssetPath(s);
                    return path;
                }
#endif
                return "";
            }
            #endregion

            #region 资产查询
            public static List<T> FindAllSOAssets<T>() where T : class
            {
                List<T> values = new List<T>(3);
#if UNITY_EDITOR
                var all = AssetDatabase.FindAssets("t:ScriptableObject");
                foreach (var i in all)
                {
                    GUID id = default; GUID.TryParse(i, out id);
                    Type type = AssetDatabase.GetMainAssetTypeFromGUID(id);
                    if (typeof(T).IsAssignableFrom(type))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(id);
                        UnityEngine.Object ob = AssetDatabase.LoadAssetAtPath(path, type);
                        if (ob is T t)
                        {
                            values.Add(t);
                        }
                        else
                        {
                            continue;
                        }

                    }
                }
#endif
                return values;
            }
            public static List<T> FindAllSOAssets<T>(Type typeUse) where T : class
            {
                List<T> values = new List<T>(3);
#if UNITY_EDITOR
                if (typeUse == null) { UnityEngine.Debug.LogWarning("查询NULL类型"); return values; }
                var all = AssetDatabase.FindAssets("t:ScriptableObject");
                foreach (var i in all)
                {
                    GUID id = default; GUID.TryParse(i, out id);
                    Type type = AssetDatabase.GetMainAssetTypeFromGUID(id);

                    if (typeUse.IsAssignableFrom(type))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(id);
                        UnityEngine.Object ob = AssetDatabase.LoadAssetAtPath(path, type);
                        if (ob is T t)
                        {
                            values.Add(t);
                        }
                        else
                        {
                            continue;
                        }

                    }
                }
#endif
                return values;
            }
            public static List<T> FindAllSOAssetsQuickly<T>() where T : ScriptableObject
            {
#if UNITY_EDITOR
                var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
                List<T> assets = new List<T>();
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                    if (asset != null)
                    {
                        assets.Add(asset);
                    }
                }
                return assets;
#else
                return  new List<T>();
#endif
            }
            public static T LoadAssetByGUIDString<T>(string s) where T : class
            {
#if UNITY_EDITOR
                GUID id = default;
                GUID.TryParse(s, out id);
                Type type = AssetDatabase.GetMainAssetTypeFromGUID(id);
                if (typeof(T).IsAssignableFrom(type))
                {
                    string path = AssetDatabase.GUIDToAssetPath(id);
                    UnityEngine.Object ob = AssetDatabase.LoadAssetAtPath(path, type);
                    return ob as T;
                }
#endif
                return null;
            }
            public static UnityEngine.Object LoadAssetByGUIDString(string s)
            {
#if UNITY_EDITOR
                GUID id = default;
                GUID.TryParse(s, out id);
                Type type = AssetDatabase.GetMainAssetTypeFromGUID(id);
                if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                {
                    string path = AssetDatabase.GUIDToAssetPath(id);
                    UnityEngine.Object ob = AssetDatabase.LoadAssetAtPath(path, type);
                    return ob;
                }
#endif
                return null;
            }

            public static string GetAssetGUID(UnityEngine.Object uo)
            {
#if UNITY_EDITOR
                string path = AssetDatabase.GetAssetPath(uo);
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (guid != null && !guid.IsNullOrWhitespace()) return guid;
#endif
                return null;
            }





            #endregion

            #region 判定
            public static bool IsObjectAsFolder(UnityEngine.Object ob)
            {
#if UNITY_EDITOR
                if (ob != null)
                {
                    var path = Wrap_GetAssetPath(ob);
                    return AssetDatabase.IsValidFolder(path);
                }
#endif
                return false;
            }


            #endregion

            #region 快捷功能
            public static void Quick_PingAssetByPath(string path)
            {
#if UNITY_EDITOR
                var asset = AssetDatabase.LoadAssetAtPath(path, typeof(UnityEngine.Object));
                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                }
                else
                {
                     UnityEngine.Debug.LogError("未发现资产在路径" + path);
                }
#endif
            }

            public static void Quick_SelectAssetByPath(string path)
            {
#if UNITY_EDITOR
                var asset = AssetDatabase.LoadAssetAtPath(path, typeof(UnityEngine.Object));
                if (asset != null)
                {
                    Selection.activeObject = (asset);
                }
                else
                {
                     UnityEngine.Debug.LogError("未发现资产在路径" + path);
                }
#endif
            }

            public static void Quick_InitAsset<T>() where T : UnityEngine.Object
            {
#if UNITY_EDITOR
                List<T> results = new List<T>();
                string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                    if (asset != null)
                    {
                        results.Add(asset);
                        EditorUtility.SetDirty(asset);
                    }
                }
#endif
            }
            #endregion

            #region 资产创建
            public static T CreateSOAsset<T>(string savePath, string name) where T : UnityEngine.ScriptableObject
            {
                var ins = ScriptableObject.CreateInstance<T>();
                ins.name = name._ToValidIdentName();
                if (ins != null)
                {
#if UNITY_EDITOR
                    AssetDatabase.CreateAsset(ins, savePath + "/" + ins.name + ".asset");
                    AssetDatabase.Refresh();
                    AssetDatabase.SaveAssets();
#endif
                    return ins;
                }
                return null;
            }
            public static T CreateSOAsset<T>(string folderPath, string assetName, bool appendRandomIfNotChangedDefaultName = false, bool hasChange = false, Action<T> AfterCreate = null) where T : ScriptableObject
            {
#if UNITY_EDITOR

                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                     UnityEngine.Debug.LogError($"Invalid folder path_: {folderPath}");
                    return null;
                }
                T asset = ScriptableObject.CreateInstance<T>();
                asset.name = assetName._ToValidIdentName() + (appendRandomIfNotChangedDefaultName && !hasChange ? UnityEngine.Random.Range(0, 9999).ToString() : "");
                string path = $"{folderPath}/{asset.name}.asset";

                AssetDatabase.CreateAsset(asset, path);
                AfterCreate?.Invoke(asset);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return asset;
#else
                return null;
#endif
            }
            public static ScriptableObject CreateSOAsset(Type type, string folderPath, string assetName, bool appendRandomIfNotChangedDefaultName = false, bool hasChange = false, Action<ScriptableObject> afterCreate = null)
            {
#if UNITY_EDITOR
                if (type == null) return null;
                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                     UnityEngine.Debug.LogError($"Invalid folder path_: {folderPath}");
                    return null;
                }
                ScriptableObject asset = ScriptableObject.CreateInstance(type);
                asset.name = assetName._ToValidIdentName() + (appendRandomIfNotChangedDefaultName && !hasChange ? UnityEngine.Random.Range(0, 99999).ToString() : "");
                string path = $"{folderPath}/{asset.name}.asset";

                AssetDatabase.CreateAsset(asset, path);
                afterCreate?.Invoke(asset);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return asset;
#else
                return null;
#endif
            }
            #endregion

            #region 更新
            public static List<string> Quick_System_GetFilePaths_AlwaysSafe(string folder, string patten = "*")
            {
                List<string> paths = new List<string>();
                string assetsPath = Application.dataPath._KeepBeforeByLast("/Assets");
                string path = System.IO.Path.Combine(assetsPath,folder).Replace('\\', '/');
                
                string[] allFiles = Directory.GetFiles(path, patten, SearchOption.AllDirectories);

                foreach (string file in allFiles)
                {
                    string relativePath = "Assets" + file.Replace(Application.dataPath, "").Replace('\\', '/');
                    paths.Add(relativePath);
                }
                return paths;
            }
            public static void Quick_System_DeleteAllFilesInFolder_Always(string fullPath)
            {
                if (Directory.Exists(fullPath))
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(fullPath);

                    // 删除所有文件，但跳过.meta文件
                    foreach (FileInfo file in dirInfo.GetFiles())
                    {
                        if (file.Extension != ".meta")
                        {
                            file.Delete();
                        }
                    }

                    UnityEngine.Debug.Log("文件删除完毕（已跳过.meta文件）。");

#if UNITY_EDITOR
                    UnityEditor.AssetDatabase.Refresh();
#endif
                }
                else
                {
                    UnityEngine.Debug.LogWarning("目录不存在，无需删除。");
                }
            }
            public static void Quick_OpenInSystemFolder(string folderPath, bool FromAssets=true)
            {
#if UNITY_EDITOR
                if (string.IsNullOrEmpty(folderPath) || !System.IO.Directory.Exists(folderPath))
                {
                    UnityEngine.Debug.LogError("目录不存在或路径无效: " + folderPath);
                    return;
                }

                // 根据不同平台执行命令
                if (Application.platform == RuntimePlatform.WindowsEditor ||
                    Application.platform == RuntimePlatform.WindowsPlayer)
                {
                    // Windows: 使用explorer.exe
                    // 确保路径使用反斜杠，并对路径加引号以处理空格
                    folderPath = folderPath.Replace("/", "\\");
                    Process.Start("explorer.exe", "\"" + folderPath + "\"");
                }
                else if (Application.platform == RuntimePlatform.OSXEditor ||
                         Application.platform == RuntimePlatform.OSXPlayer)
                {
                    // macOS: 使用open命令
                    Process.Start("open", "\"" + folderPath + "\"");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("当前平台不支持直接打开文件夹。");
                }
#endif
            }

            public static (bool Success, string Message) Quick_System_CreateDirectory(string fullPath)
            {
                // 参数校验
                if (string.IsNullOrWhiteSpace(fullPath))
                {
                    return (false, "提供的路径为空或无效。");
                }

                try
                {
                    // 检查目录是否已存在
                    if (Directory.Exists(fullPath))
                    {
                        return (true, $"目录已存在：{fullPath}");
                    }

                    // 创建目录
                    DirectoryInfo directoryInfo = Directory.CreateDirectory(fullPath);
                    string resultMessage = $"目录创建成功：{fullPath}";

                    return (true, resultMessage);
                }
                catch (UnauthorizedAccessException ex)
                {
                    return (false, $"权限不足，无法创建目录 '{fullPath}'。详细信息：{ex.Message}");
                }
                catch (PathTooLongException ex)
                {
                    return (false, $"路径 '{fullPath}' 过长。详细信息：{ex.Message}");
                }
                catch (IOException ex) // 此异常类包含 DirectoryNotFoundException 等
                {
                    return (false, $"创建目录时发生I/O错误：{ex.Message}");
                }
                catch (Exception ex) // 捕获其他未预期的异常
                {
                    return (false, $"创建目录 '{fullPath}' 时发生未预期的错误：{ex.Message}");
                }
            }

            public static bool Quick_CreateFolderByFullPath(string fullPath)
            {
#if UNITY_EDITOR
                // 参数检查
                if (string.IsNullOrEmpty(fullPath) || !fullPath.StartsWith("Assets"))
                {
                     UnityEngine.Debug.LogError("路径无效！必须是以 'Assets' 开头的有效路径。");
                    return false;
                }

                // 检查文件夹是否已存在
                if (AssetDatabase.IsValidFolder(fullPath))
                {
                    
                    return true;
                }
                 UnityEngine.Debug.Log("尝试创建");
                // 从完整路径中提取父文件夹路径和要创建的新文件夹名称
                string parentFolder = Path.GetDirectoryName(fullPath);
                string newFolderName = Path.GetFileName(fullPath);

                // 检查父目录是否存在，如果不存在，则递归创建父目录
                if (!AssetDatabase.IsValidFolder(parentFolder))
                {
                    // 递归调用自身来创建父目录
                    if (!Quick_CreateFolderByFullPath(parentFolder))
                    {
                        return false; // 如果父目录创建失败，则直接返回
                    }
                }

                // 创建最终的目标文件夹
                string resultGuid = AssetDatabase.CreateFolder(parentFolder, newFolderName);
                if (!string.IsNullOrEmpty(resultGuid))
                {
                    AssetDatabase.Refresh(); // 刷新数据库使新文件夹立即可见
                     UnityEngine.Debug.Log($"文件夹创建成功：{fullPath}");
                    return true;
                }
                else
                {
                     UnityEngine.Debug.LogError($"文件夹创建失败：{fullPath}");
                    return false;
                }
#else
                return false;
#endif
            }

#endregion
        }
    }
}

