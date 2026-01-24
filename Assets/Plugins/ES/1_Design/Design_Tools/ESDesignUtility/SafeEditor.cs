using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.Utilities;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace ES
{
    public static partial class ESDesignUtility
    {
        //SafeEditor提供了一系列已经被#if UnityEditor包裹的安全编辑器功能，可以直接在任何地方使用并且不需要额外处理
        public static class SafeEditor
        {
            private static string _NormalizeAssetPath(string path)
            {
                if (string.IsNullOrEmpty(path)) return path;
                return path.Replace('\\', '/');
            }

            #region 获取特殊数据
            /// <summary>
            /// 获得全部标签
            /// </summary>
            /// <returns></returns>
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
            /// <summary>
            /// 获得全部层级字典表(layer->name)
            /// </summary>
            /// <returns></returns>
            public static Dictionary<int, string> GetAllLayers()
            {

#if UNITY_EDITOR
                Dictionary<int, string> keyValuePairs = new Dictionary<int, string>();
                // 获取Tags管理器
                var layers = UnityEditorInternal.InternalEditorUtility.layers;
                foreach (var i in layers)
                {
                    int layer = LayerMask.NameToLayer(i);
                    if (layer < 0) continue;
                    keyValuePairs.TryAdd(layer, i);
                }
                return keyValuePairs;
#else
                return new Dictionary<int, string>();
#endif
            }
            /// <summary>
            /// 添加标签
            /// </summary>
            /// <param name="tag"></param>
            public static void AddTag(string tag)
            {
#if UNITY_EDITOR
                // 获取Tags
                UnityEditorInternal.InternalEditorUtility.AddTag(tag);
#endif
            }
            #endregion

            #region 简单包装
            /// <summary>
            /// SetDirty 脏标记对象
            /// </summary>
            /// <param name="which"></param>
            /// <param name="saveAndRefresh">兼容旧版：为 true 时同时执行 SaveAssets + Refresh</param>
            public static void Wrap_SetDirty(UnityEngine.Object which, bool saveAndRefresh = true)
            {
#if UNITY_EDITOR
                Wrap_SetDirty(which, saveAssets: saveAndRefresh, refresh: saveAndRefresh);
#endif
            }

            /// <summary>
            /// SetDirty 脏标记对象（商业级语义版：显式控制 SaveAssets/Refresh）
            /// </summary>
            /// <param name="which"></param>
            /// <param name="saveAssets">是否调用 AssetDatabase.SaveAssets()</param>
            /// <param name="refresh">是否调用 AssetDatabase.Refresh()</param>
            public static void Wrap_SetDirty(UnityEngine.Object which, bool saveAssets, bool refresh)
            {
#if UNITY_EDITOR
                if (which == null) return;

                EditorUtility.SetDirty(which);
                if (saveAssets) AssetDatabase.SaveAssets();
                if (refresh) AssetDatabase.Refresh();
#endif
            }
            /// <summary>
            /// 显示对话框
            /// </summary>
            /// <param name="title">标题</param>
            /// <param name="message">信息内容</param>
            /// <param name="ok">确定</param>
            /// <param name="cancel">取消</param>
            /// <returns></returns>
            public static bool Wrap_DisplayDialog(string title, string message, string ok = "好的", string cancel = "算了")
            {
#if UNITY_EDITOR

                return EditorUtility.DisplayDialog(title, message, ok, cancel);
#else
                return false;
#endif

            }
            /// <summary>
            /// 打开选择文件夹窗口(返回路径)
            /// </summary>
            /// <param name="targetPath">默认目标路径</param>
            /// <param name="title">标题</param>
            /// <returns>(返回文件夹路径)</returns>
            public static string Wrap_OpenSelectorFolderPanel(string targetPath = "Assets", string title = "选择文件夹")
            {
#if UNITY_EDITOR
                return EditorUtility.OpenFolderPanel(title, targetPath, "");
#else
                return targetPath;
#endif
            }
            /// <summary>
            /// 是有效的文件夹
            /// </summary>
            /// <param name="path">文件夹路径</param>
            /// <param name="IfPlayerRuntime">如果在运行时(无法实际判断)，决定返回？？</param>
            /// <returns></returns>
            public static bool Wrap_IsValidFolder(string path, bool IfPlayerRuntime = false)
            {
#if UNITY_EDITOR
                return AssetDatabase.IsValidFolder(path);
#else
                return IfPlayerRuntime;
#endif
            }
            /// <summary>
            /// 创建文件夹
            /// </summary>
            /// <param name="parentPath">父级路径</param>
            /// <param name="folderName">文件夹名字</param>
            public static void Wrap_CreateFolderDic(string parentPath, string folderName)
            {
#if UNITY_EDITOR
                AssetDatabase.CreateFolder(parentPath, folderName);
#endif
            }
            /// <summary>
            /// 把内容放在系统粘贴板
            /// </summary>
            /// <param name="content">内容</param>
            public static void Wrap_SystemCopyBuffer(string content)
            {
                GUIUtility.systemCopyBuffer = content;
            }
            /// <summary>
            /// 获得资产路径
            /// </summary>
            /// <param name="s">资产</param>
            /// <returns>Assets/路径</returns>
            public static string Wrap_GetAssetPath(UnityEngine.Object s)
            {
#if UNITY_EDITOR
                if (s != null)
                {
                    string path = AssetDatabase.GetAssetPath(s);
                    return path;
                }
#endif
                return null;
            }
            #endregion
            
            #region 资产查询
            /// <summary>
            /// 查询一类SO文件,支持接口
            /// </summary>
            /// <typeparam name="T">SO类型</typeparam>
            /// <returns></returns>
            public static List<T> FindAllSOAssets<T>() where T : class
            {
                List<T> values = new List<T>(3);
#if UNITY_EDITOR
                // 优先用更窄的 filter，避免全库扫描 ScriptableObject。
                // 注意：Unity 的 FindAssets("t:...") 仅支持具体类型（class），不支持接口。
                // 因此当 T 为接口/非 ScriptableObject 类型时，必须回退到全库扫描再用 IsAssignableFrom 过滤。
                Type targetType = typeof(T);
                string[] allScriptableObjectGuids = null;
                if (!targetType.IsInterface && typeof(ScriptableObject).IsAssignableFrom(targetType))
                {
                    var guids = AssetDatabase.FindAssets($"t:{targetType.Name}");
                    foreach (var guid in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        UnityEngine.Object ob = AssetDatabase.LoadAssetAtPath(path, targetType);
                        if (ob is T t)
                        {
                            values.Add(t);
                        }
                    }
                }
                else
                {
                    allScriptableObjectGuids = AssetDatabase.FindAssets("t:ScriptableObject");
                    foreach (var guid in allScriptableObjectGuids)
                    {
                        GUID id = default; GUID.TryParse(guid, out id);
                        Type type = AssetDatabase.GetMainAssetTypeFromGUID(id);
                        if (!typeof(T).IsAssignableFrom(type)) continue;

                        string path = AssetDatabase.GUIDToAssetPath(id);
                        UnityEngine.Object ob = AssetDatabase.LoadAssetAtPath(path, type);
                        if (ob is T t)
                        {
                            values.Add(t);
                        }
                    }
                }

                // 额外：支持从 ISoDataGroup 中展开 T。
                // 由于 Group 的 info 类型是动态的，无法用 t:TypeName 精确索引，因此这里仍需要扫描所有 ScriptableObject。
                {
                    var all = allScriptableObjectGuids ?? AssetDatabase.FindAssets("t:ScriptableObject");
                    foreach (var guid in all)
                    {
                        GUID id = default; GUID.TryParse(guid, out id);
                        Type type = AssetDatabase.GetMainAssetTypeFromGUID(id);
                        if (!typeof(ISoDataGroup).IsAssignableFrom(type)) continue;

                        string path = AssetDatabase.GUIDToAssetPath(id);
                        UnityEngine.Object obG = AssetDatabase.LoadAssetAtPath(path, type);
                        if (obG is ISoDataGroup g)
                        {
                            if (typeof(T).IsAssignableFrom(g.GetSOInfoType()))
                            {
                                foreach (var info in g.AllInfos)
                                {
                                    if (info is T t)
                                    {
                                        values.Add(t);
                                    }
                                }
                            }

                        }

                    }
                }
#endif
                return values;
            }
            /// <summary>
            /// 查询一类SO文件(参数Type但是返回List<T>,也就是T 为同获得父类)
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="typeUse"></param>
            /// <returns></returns>
            public static List<T> FindAllSOAssets<T>(Type typeUse) where T : class
            {
                List<T> values = new List<T>(3);
#if UNITY_EDITOR
                if (typeUse == null) { UnityEngine.Debug.LogWarning("查询NULL类型"); return values; }

                // 注意：Unity 的 FindAssets("t:...") 仅支持具体类型（class），不支持接口。
                // 因此当 typeUse 为接口/非 ScriptableObject 类型时，必须回退到全库扫描再用 IsAssignableFrom 过滤。
                if (!typeUse.IsInterface && typeof(ScriptableObject).IsAssignableFrom(typeUse))
                {
                    var guids = AssetDatabase.FindAssets($"t:{typeUse.Name}");
                    foreach (var guid in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        UnityEngine.Object ob = AssetDatabase.LoadAssetAtPath(path, typeUse);
                        if (ob is T t)
                        {
                            values.Add(t);
                        }
                    }
                }
                else
                {
                    var all = AssetDatabase.FindAssets("t:ScriptableObject");
                    foreach (var guid in all)
                    {
                        GUID id = default; GUID.TryParse(guid, out id);
                        Type type = AssetDatabase.GetMainAssetTypeFromGUID(id);
                        if (!typeUse.IsAssignableFrom(type)) continue;

                        string path = AssetDatabase.GUIDToAssetPath(id);
                        UnityEngine.Object ob = AssetDatabase.LoadAssetAtPath(path, type);
                        if (ob is T t)
                        {
                            values.Add(t);
                        }
                    }
                }
#endif
                return values;
            }
            /// <summary>
            /// 快速查询一类So文件(直接按泛型名查不筛选)
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <returns></returns>
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
            /// <summary>
            /// 通过GUID（string）加载出资产(泛型)
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="s"></param>
            /// <returns></returns>
            public static T LoadAssetByGUIDString<T>(string s) where T : UnityEngine.Object
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
            /// <summary>
            /// 通过GUID（string）加载出资产
            /// </summary>
            /// <param name="s"></param>
            /// <returns></returns>
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
            /// <summary>
            /// 获得资产的GUID（string）
            /// </summary>
            /// <param name="uo"></param>
            /// <returns></returns>
            public static string GetAssetGUID(UnityEngine.Object uo)
            {
#if UNITY_EDITOR
                string path = AssetDatabase.GetAssetPath(uo);
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (guid != null && !guid.IsNullOrWhitespace()) return guid;
#endif
                return "";
            }

            #endregion

            #region 判定
            /// <summary>
            /// 判定一个资产是不是文件夹
            /// </summary>
            /// <param name="ob"></param>
            /// <returns></returns>
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
            /// <summary>
            /// 标记指出一个路径的资产
            /// </summary>
            /// <param name="path"></param>
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
            /// <summary>
            /// 强制选中一个路径的资产
            /// </summary>
            /// <param name="path"></param>
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
            /// <summary>
            /// 初始化一个资产（通常是SO）
            /// </summary>
            /// <typeparam name="T"></typeparam>
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

            public static bool Quick_TryCreateChildFolder(string parentFolder, string folderName, out string end, bool saveAssets = true, bool refresh = true)
            {
#if UNITY_EDITOR
                if (AssetDatabase.IsValidFolder(parentFolder))
                {
                    string target = parentFolder + "/" + folderName;
                    if (!AssetDatabase.IsValidFolder(target))
                    {
                        AssetDatabase.CreateFolder(parentFolder, folderName);
                        end = target;
                        if (saveAssets) AssetDatabase.SaveAssets();
                        if (refresh) AssetDatabase.Refresh();
                        return true;
                    }
                    else
                    {
                        end = target;
                        return true;
                    }
                }

#endif
                end = "";
                return false;
            }

           
            #endregion

            #region 资产创建
            /// <summary>
            /// 创建一个SO资产
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="saveFolderPath">文件夹路径</param>
            /// <param name="name">文件名字</param>
            /// <param name="autoCreateFolder">当目标文件夹不存在时，是否自动创建</param>
            /// <param name="saveAssets">是否调用 AssetDatabase.SaveAssets()</param>
            /// <param name="refresh">是否调用 AssetDatabase.Refresh()</param>
            /// <returns></returns>
            public static T CreateSOAsset<T>(string saveFolderPath, string name, bool autoCreateFolder = true, bool saveAssets = true, bool refresh = true) where T : UnityEngine.ScriptableObject
            {
                var ins = ScriptableObject.CreateInstance<T>();
                ins.name = name;
                if (ins != null)
                {
#if UNITY_EDITOR
                    saveFolderPath = _NormalizeAssetPath(saveFolderPath);
                    if (string.IsNullOrEmpty(saveFolderPath) || !saveFolderPath.StartsWith("Assets", StringComparison.Ordinal))
                    {
                        UnityEngine.Debug.LogWarning($"CreateSOAsset 失败：folderPath 无效：{saveFolderPath}");
                        return null;
                    }

                    string safeName = (name ?? string.Empty)._ToValidIdentName();
                    if (string.IsNullOrEmpty(safeName))
                    {
                        UnityEngine.Debug.LogWarning("CreateSOAsset 失败：name 为空或非法。");
                        return null;
                    }

                    if (!AssetDatabase.IsValidFolder(saveFolderPath))
                    {
                        if (!autoCreateFolder)
                        {
                            UnityEngine.Debug.LogWarning($"CreateSOAsset 失败：目标文件夹不存在：{saveFolderPath}");
                            return null;
                        }

                        if (!Quick_CreateFolderByFullPath(saveFolderPath, refresh: false))
                        {
                            UnityEngine.Debug.LogWarning($"CreateSOAsset 失败：无法创建文件夹路径：{saveFolderPath}");
                            return null;
                        }
                    }

                    ins.name = safeName;
                    AssetDatabase.CreateAsset(ins, saveFolderPath + "/" + ins.name + ".asset");
                    if (saveAssets) AssetDatabase.SaveAssets();
                    if (refresh) AssetDatabase.Refresh();
#endif
                    return ins;
                }
                return null;
            }
            /// <summary>
            /// 创建一个SO资产(带回调与随机)
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="folderPath">父文件夹</param>
            /// <param name="assetName">资产名</param>
            /// <param name="appendRandomIfNotChangedDefaultName">如果仍使用默认名（未修改），是否追加随机数字以降低同名冲突概率</param>
            /// <param name="hasChange">是否已修改默认名</param>
            /// <param name="AfterCreate">完成创建后第一时间回调--才保存</param>
            /// <returns></returns>
            public static T CreateSOAsset<T>(string folderPath, string assetName, bool appendRandomIfNotChangedDefaultName = false, bool hasChange = false, Action<T> AfterCreate = null, bool saveAssets = true, bool refresh = true) where T : ScriptableObject
            {
#if UNITY_EDITOR

                folderPath = _NormalizeAssetPath(folderPath);

                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    if (Quick_CreateFolderByFullPath(folderPath, refresh: false))
                    {
                        UnityEngine.Debug.Log($"自动创建了文件夹: {folderPath}");
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning($"无法创建文件夹路径: {folderPath}");
                        return null;
                    }
                }
                T asset = ScriptableObject.CreateInstance<T>();
                asset.name = assetName._ToValidIdentName() + (appendRandomIfNotChangedDefaultName && !hasChange ? UnityEngine.Random.Range(0, 9999).ToString() : "");
                string path = $"{folderPath}/{asset.name}.asset";

                AssetDatabase.CreateAsset(asset, path);
                AfterCreate?.Invoke(asset);
                if (saveAssets) AssetDatabase.SaveAssets();
                if (refresh) AssetDatabase.Refresh();
                return asset;
#else
                return null;
#endif
            }
            /// <summary>
            /// 创建一个SO资产(带回调与随机)
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="folderPath">父文件夹</param>
            /// <param name="assetName">资产名</param>
            /// <param name="appendRandomIfNotChangedDefaultName">如果仍使用默认名（未修改），是否追加随机数字以降低同名冲突概率</param>
            /// <param name="hasChange">是否已修改默认名</param>
            /// <param name="afterCreate">完成创建后第一时间回调--才保存</param>
            /// <returns></returns>
            public static ScriptableObject CreateSOAsset(Type type, string folderPath, string assetName, bool appendRandomIfNotChangedDefaultName = false, bool hasChange = false, Action<ScriptableObject> afterCreate = null, bool saveAssets = true, bool refresh = true)
            {
#if UNITY_EDITOR
                if (type == null) return null;

                folderPath = _NormalizeAssetPath(folderPath);
                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    if (Quick_CreateFolderByFullPath(folderPath, refresh: false))
                    {
                        UnityEngine.Debug.Log($"自动创建了文件夹: {folderPath}");
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning($"无法创建文件夹路径: {folderPath}");
                        return null;
                    }
                }
                ScriptableObject asset = ScriptableObject.CreateInstance(type);
                asset.name = assetName._ToValidIdentName() + (appendRandomIfNotChangedDefaultName && !hasChange ? UnityEngine.Random.Range(0, 99999).ToString() : "");
                string path = $"{folderPath}/{asset.name}.asset";

                AssetDatabase.CreateAsset(asset, path);
                afterCreate?.Invoke(asset);
                if (saveAssets) AssetDatabase.SaveAssets();
                if (refresh) AssetDatabase.Refresh();
                return asset;
#else
                return null;
#endif
            }
            #endregion
            #region 更新
            /// <summary>
            /// 系统(安全)-获得文件夹下所有文件
            /// </summary>
            /// <param name="folder"></param>
            /// <param name="patten"></param>
            /// <returns></returns>
            public static List<string> Quick_System_GetFiles_AlwaysSafe(string folder, string patten = "*")
            {
                List<string> paths = new List<string>();
                string assetsPath = UnityEngine.Application.dataPath._KeepBeforeByLast("/Assets");
                string path = System.IO.Path.Combine(assetsPath, folder).Replace('\\', '/');

                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                {
                    return paths;
                }

                string[] allFiles;
                try
                {
                    allFiles = Directory.GetFiles(path, patten, SearchOption.AllDirectories);
                }
                catch
                {
                    return paths;
                }

                foreach (string file in allFiles)
                {
                    // 转换为Unity相对路径（如 "Assets/Scenes/Menu.unity"）
                    string relativePath = "Assets" + file.Replace(UnityEngine.Application.dataPath, "").Replace('\\', '/');
                    paths.Add(relativePath);
                }
                return paths;
            }
            /// <summary>
            /// 系统(安全)-删除文件夹下的所有文件
            /// </summary>
            /// <param name="fullPath"></param>
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
                            try
                            {
                                file.Delete();
                            }
                            catch (Exception ex)
                            {
                                UnityEngine.Debug.LogWarning($"删除失败: {file.FullName} ({ex.Message})");
                            }
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
            /// <summary>
            /// 系统-资源管理器打开路径
            /// </summary>
            /// <param name="Path"></param>
            /// <param name="FromAssets"></param>
            public static void Quick_OpenInSystemFolder(string Path, bool FromAssets = true)
            {
#if UNITY_EDITOR
                if (string.IsNullOrEmpty(Path))
                {
                    UnityEngine.Debug.LogError("路径为空。");
                    return;
                }

                string targetPath = Path;
                if (FromAssets)
                {
                    targetPath = _NormalizeAssetPath(targetPath);

                    // 兼容传入："Assets/..." 或 "SomeFolder"（默认当作 Assets 下相对路径）
                    if (!targetPath.StartsWith("Assets", StringComparison.Ordinal))
                    {
                        targetPath = "Assets/" + targetPath.TrimStart('/');
                    }

                    // 转成磁盘绝对路径
                    targetPath = UnityEngine.Application.dataPath + targetPath.Substring("Assets".Length);
                }

                targetPath = targetPath.Replace('/', System.IO.Path.DirectorySeparatorChar);
                if (!System.IO.Directory.Exists(targetPath) && !System.IO.File.Exists(targetPath))
                {
                    UnityEngine.Debug.LogError("目录/文件不存在或路径无效: " + targetPath);
                    return;
                }

                // Unity 官方跨平台实现：在资源管理器/访达中定位
                EditorUtility.RevealInFinder(targetPath);
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

            public static bool Quick_CreateFolderByFullPath(string fullPath, bool refresh = true)
            {
#if UNITY_EDITOR
                // 参数检查
                fullPath = _NormalizeAssetPath(fullPath);
                if (string.IsNullOrEmpty(fullPath) || !fullPath.StartsWith("Assets", StringComparison.Ordinal))
                {
                    UnityEngine.Debug.LogError("路径无效！必须是以 'Assets' 开头的有效路径。");
                    return false;
                }

                if (string.Equals(fullPath, "Assets", StringComparison.Ordinal))
                {
                    return true;
                }

                // 检查文件夹是否已存在
                if (AssetDatabase.IsValidFolder(fullPath))
                {

                    return true;
                }
                // 从完整路径中提取父文件夹路径和要创建的新文件夹名称
                string parentFolder = _NormalizeAssetPath(Path.GetDirectoryName(fullPath));
                string newFolderName = Path.GetFileName(fullPath);

                if (string.IsNullOrEmpty(parentFolder) || string.IsNullOrEmpty(newFolderName))
                {
                    UnityEngine.Debug.LogError($"路径解析失败：{fullPath}");
                    return false;
                }

                // 检查父目录是否存在，如果不存在，则递归创建父目录
                if (!AssetDatabase.IsValidFolder(parentFolder))
                {
                    // 递归调用自身来创建父目录
                    if (!Quick_CreateFolderByFullPath(parentFolder, refresh: false))
                    {
                        return false; // 如果父目录创建失败，则直接返回
                    }
                }

                // 创建最终的目标文件夹
                string resultGuid = AssetDatabase.CreateFolder(parentFolder, newFolderName);
                if (!string.IsNullOrEmpty(resultGuid))
                {
                    if (refresh) AssetDatabase.Refresh(); // 需要立即可见时才刷新（批量创建建议外层统一 Refresh）
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

