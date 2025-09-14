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


            #endregion

            #region 简单包装
            public static void SetDirty(UnityEngine.Object who, bool withRefresh = true)
            {
#if UNITY_EDITOR
                if (withRefresh)
                {
                    AssetDatabase.Refresh();
                    AssetDatabase.SaveAssets();
                }
                EditorUtility.SetDirty(who);
#endif
            }

            public static bool DisplayDialog(string title, string message, string ok = "好的", string cancel = "算了")
            {
#if UNITY_EDITOR

                return EditorUtility.DisplayDialog(title, message, ok, cancel);
#else
                return false;
#endif

            }

            public static string SelectorFolder(string targetPath = "Assets/Resources/Data/GlobalData", string title = "选择文件夹")
            {
#if UNITY_EDITOR
                return EditorUtility.OpenFolderPanel(title, targetPath, "");
#else
               return targetPath;
#endif
            }

            public static bool IsValidFolder(string path, bool want = false)
            {
#if UNITY_EDITOR
                return AssetDatabase.IsValidFolder(path);
#else
                return want;
#endif
            }

            public static void CreateFolderDic(string path, string name)
            {
#if UNITY_EDITOR
                AssetDatabase.CreateFolder(path, name);
#endif
            }

            public static void Copy(string content)
            {
                GUIUtility.systemCopyBuffer = content;
            }
            #endregion

            #region 资产查询



            public static List<T> FindSOAssets<T>() where T : class
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
            public static List<T> FindSOAssets<T>(Type typeUse) where T : class
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
            public static List<T> FindAllSOQuickly<T>() where T : ScriptableObject
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

            public static string GetAssetGUID(UnityEngine.Object s)
            {
#if UNITY_EDITOR
                string path = AssetDatabase.GetAssetPath(s);
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (guid != null && !guid.IsNullOrWhitespace()) return guid;
#endif
                return null;
            }

            public static string GetAssetPath(UnityEngine.Object s)
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

            #region 判定
            public static bool IsObjectAsFolder(UnityEngine.Object ob)
            {
#if UNITY_EDITOR
                if (ob != null)
                {
                    var path = GetAssetPath(ob);
                    return AssetDatabase.IsValidFolder(path);
                }
#endif
                return false;
            }


            #endregion

            #region 快捷功能
            public static void PingAssetByPath(string path)
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

            public static void SelectAssetByPath(string path)
            {
#if UNITY_EDITOR
                var asset = AssetDatabase.LoadAssetAtPath(path, typeof(UnityEngine.Object));
                if (asset != null)
                {
                   Selection.activeObject=(asset);
                }
                else
                {
                    Debug.LogError("未发现资产在路径" + path);
                }
#endif
            }

            public static string CreateEasySelectMenu(string title, string[] select, string[] back)
            {
#if UNITY_EDITOR
                GenericMenu menu = new GenericMenu();
                for (int i = 0; i < select.Length; i++)
                {

                }
                return null;
#else
                return null;
#endif
            }

            public static void InitAsset<T>() where T : UnityEngine.Object
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
            public static T CreateSO<T>(string savePath, string name) where T : UnityEngine.ScriptableObject
            {
                Debug.Log("创建 SO :" + name + "在" + savePath);
                var ins = ScriptableObject.CreateInstance<T>();
                if (ins != null)
                {
#if UNITY_EDITOR
                    ins.name = name;
                    AssetDatabase.CreateAsset(ins, savePath + "/" + ins.name + ".asset");
                    AssetDatabase.Refresh();
                    AssetDatabase.SaveAssets();
#endif
                    return ins;
                }
                return null;
            }
            public static T CreateSOAsset<T>(string folderPath, string assetName, bool appendRandomIfNotChangedDefaultName = false, bool hasChange = false, Action<T> beforeSave = null) where T : ScriptableObject
            {
#if UNITY_EDITOR

                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    Debug.LogError($"Invalid folder path_: {folderPath}");
                    return null;
                }
                T asset = ScriptableObject.CreateInstance<T>();
                asset.name = assetName + (appendRandomIfNotChangedDefaultName && !hasChange ? UnityEngine.Random.Range(0, 99999).ToString() : "");
                string path = $"{folderPath}/{asset.name}.asset";

                AssetDatabase.CreateAsset(asset, path);
                beforeSave?.Invoke(asset);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return asset;
#else
                return null;
#endif
            }
            public static ScriptableObject CreateSOAsset(Type type, string folderPath, string assetName, bool appendRandomIfNotChangedDefaultName = false, bool hasChange = false, Action<ScriptableObject> beforeSave = null)
            {
#if UNITY_EDITOR
                if (type == null) return null;
                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    Debug.LogError($"Invalid folder path_: {folderPath}");
                    return null;
                }
                ScriptableObject asset = ScriptableObject.CreateInstance(type);
                asset.name = assetName + (appendRandomIfNotChangedDefaultName && !hasChange ? UnityEngine.Random.Range(0, 99999).ToString() : "");
                string path = $"{folderPath}/{asset.name}.asset";

                AssetDatabase.CreateAsset(asset, path);
                beforeSave?.Invoke(asset);
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

