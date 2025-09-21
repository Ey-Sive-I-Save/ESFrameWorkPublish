using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.Utilities;

#if UNITY_EDITOR
using UnityEditor;
#endif


#if UNITY_EDITOR

#endif


namespace ES
{
    public static partial class ESDesignUtility
    {
        //SafeEditor提供了一系列已经被#if UnityEditor包裹的安全编辑器功能，可以直接在任何地方使用并且不需要额外处理
        public class SafeEditor
        {
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
                    int mask = LayerMask.GetMask(i);
                    int layer = mask > 0 ? (int)Mathf.Round(Mathf.Log(mask, (2))) : 0;
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
            /// SetDirey脏标记对象
            /// </summary>
            /// <param name="which"></param>
            /// <param name="Refresh">强制保存刷新</param>
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
            /// <param name="IfPlayerRunTime">如果在运行时(无法实际判断)，决定返回？？</param>
            /// <returns></returns>
            public static bool Wrap_IsValidFolder(string path, bool IfPlayerRunTime = false)
            {
#if UNITY_EDITOR
                return AssetDatabase.IsValidFolder(path);
#else
                return IfPlayerRunTime;
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
                return "";
            }
            #endregion
            
            #region 资产查询
            /// <summary>
            /// 查询一类SO文件
            /// </summary>
            /// <typeparam name="T">SO类型</typeparam>
            /// <returns></returns>
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
                if (typeUse == null) { Debug.LogWarning("查询NULL类型"); return values; }
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
                    Debug.LogError("未发现资产在路径" + path);
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
                    Debug.LogError("未发现资产在路径" + path);
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
            #endregion

            #region 资产创建
            /// <summary>
            /// 创建一个SO资产
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="saveFolderPath">文件夹路径</param>
            /// <param name="name">文件名字</param>
            /// <returns></returns>
            public static T CreateSOAsset<T>(string saveFolderPath, string name) where T : UnityEngine.ScriptableObject
            {
                var ins = ScriptableObject.CreateInstance<T>();
                ins.name = name;
                if (ins != null)
                {
#if UNITY_EDITOR
                    AssetDatabase.CreateAsset(ins, saveFolderPath + "/" + ins.name + ".asset");
                    AssetDatabase.Refresh();
                    AssetDatabase.SaveAssets();
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
            /// <param name="appendRandomIfNotChangedDefaultName">如果发生冲突，是否加入随机数字结尾</param>
            /// <param name="hasCharge">是否冲突</param>
            /// <param name="AfterCreate">完成创建后第一时间回调--才保存</param>
            /// <returns></returns>
            public static T CreateSOAsset<T>(string folderPath, string assetName, bool appendRandomIfNotChangedDefaultName = false, bool hasCharge = false, Action<T> AfterCreate = null) where T : ScriptableObject
            {
#if UNITY_EDITOR

                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    Debug.LogError($"Invalid folder path_: {folderPath}");
                    return null;
                }
                T asset = ScriptableObject.CreateInstance<T>();
                asset.name = assetName + (appendRandomIfNotChangedDefaultName && !hasCharge ? UnityEngine.Random.Range(0, 9999).ToString() : "");
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
            /// <summary>
            /// 创建一个SO资产(带回调与随机)
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="folderPath">父文件夹</param>
            /// <param name="assetName">资产名</param>
            /// <param name="appendRandomIfNotChangedDefaultName">如果发生冲突，是否加入随机数字结尾</param>
            /// <param name="hasCharge">是否冲突</param>
            /// <param name="afterCreate">完成创建后第一时间回调--才保存</param>
            /// <returns></returns>
            public static ScriptableObject CreateSOAsset(Type type, string folderPath, string assetName, bool appendRandomIfNotChangedDefaultName = false, bool hasCharge = false, Action<ScriptableObject> afterCreate = null)
            {
#if UNITY_EDITOR
                if (type == null) return null;
                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    Debug.LogError($"Invalid folder path_: {folderPath}");
                    return null;
                }
                ScriptableObject asset = ScriptableObject.CreateInstance(type);
                asset.name = assetName + (appendRandomIfNotChangedDefaultName && !hasCharge ? UnityEngine.Random.Range(0, 99999).ToString() : "");
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
        }
    }
}

