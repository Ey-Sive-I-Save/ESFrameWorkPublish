using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;


namespace ES
{
    public class ESEditorToolBar
    {

        [InitializeOnLoad]
        public static class CustomToolbarMenu
        {
            public static bool IncludeNoBuild = false;
            public static bool AdditiveModel = false;
            public static bool WithTheAlwaysNoDieCore = false;
            static CustomToolbarMenu()
            {
                // 注册到右边工具栏
                ToolbarExtender.RightToolbarGUI.Add(OnSceneSelectorToolbarGUI);
                ToolbarExtender.RightToolbarGUI.Add(OnSceneSelectorSettingsToolbarGUI);
                //左边
                ToolbarExtender.LeftToolbarGUI.Add(OnQuickSelectionToolbarGUI);
            }
            public static bool init = true;
            static void OnSceneSelectorToolbarGUI()
            {
                if (init)
                {
                    IncludeNoBuild = EditorPrefs.GetBool("IncludeNoBuild", false);
                    AdditiveModel = EditorPrefs.GetBool("AdditiveModel", false);
                    WithTheAlwaysNoDieCore = EditorPrefs.GetBool("WithTheAlwaysNoDieCore", false);
                    init = false;
                }
                // 创建下拉菜单按钮
                if (EditorGUILayout.DropdownButton(
                    new GUIContent("场景跳转", EditorGUIUtility.IconContent("d__Popup").image),
                    FocusType.Passive,
                    EditorStyles.toolbarDropDown))
                {
                    var menu = new GenericMenu();

                    if (IncludeNoBuild)
                    {
                        string assetsPath = Application.dataPath;
                        string[] allFiles = Directory.GetFiles(assetsPath, "*.unity", SearchOption.AllDirectories);

                        foreach (string file in allFiles)
                        {
                            string relativePath = "Assets" + file.Replace(Application.dataPath, "").Replace('\\', '/');

                            string use = relativePath;
                            int indexXIE = relativePath.LastIndexOf('/') + 1;
                            int indexLast = relativePath.LastIndexOf(".unity");
                            if (indexXIE >= 0 && indexLast >= 0)
                            {
                                string display = relativePath.Substring(indexXIE, indexLast - indexXIE);
                                menu.AddItem(new GUIContent("<场景>" + display), false, () =>
                                {
                                    Debug.Log(use);
                                    ESDesignUtility.SafeEditor.Quick_PingAssetByPath(use);
                                    UnityEngine.SceneManagement.Scene activeScene = EditorSceneManager.GetActiveScene();
                                    bool b = EditorSceneManager.SaveScene(activeScene);
                                    Debug.Log("自动保存场景" + activeScene + (b ? "成功" : "失败"));
                                    if (AdditiveModel) EditorSceneManager.OpenScene(use, mode: OpenSceneMode.Additive);
                                    else EditorSceneManager.OpenScene(use);

                                    
                                });
                            }

                        }

                    }
                    else
                    {
                        var ss = EditorBuildSettings.scenes;
                        foreach (var i in ss)
                        {
                            string use = i.path;
                            int indexXIE = i.path.LastIndexOf('/') + 1;
                            int indexLast = i.path.LastIndexOf(".unity");
                            if (indexXIE >= 0 && indexLast >= 0)
                            {
                                string display = i.path.Substring(indexXIE, indexLast - indexXIE);
                                menu.AddItem(new GUIContent("<场景>" + display), false, () =>
                                {
                                    UnityEngine.SceneManagement.Scene activeScene = EditorSceneManager.GetActiveScene();
                                    bool save = EditorSceneManager.SaveOpenScenes();
                                    Debug.Log("自动保存场景" +(save ? "成功" : "失败"));
                                    if (AdditiveModel) EditorSceneManager.OpenScene(use, mode: OpenSceneMode.Additive);
                                    else EditorSceneManager.OpenScene(use);
                                });
                              
                            }
                        }
                    }
                    // 添加菜单项

                    menu.AddSeparator("");

                    // 显示菜单
                    menu.ShowAsContext();
                }
            }
            static void OnSceneSelectorSettingsToolbarGUI()
            {
                // 创建下拉菜单按钮
                if (EditorGUILayout.DropdownButton(
                    new GUIContent("场景跳转设置", EditorGUIUtility.IconContent("d__Popup").image),
                    FocusType.Passive,
                    EditorStyles.toolbarDropDown))
                {
                    var menu = new GenericMenu();
                    var ss = EditorBuildSettings.scenes;
                    bool thisDirty = false;
                    menu.AddItem(new GUIContent("<包含未构建场景>"), IncludeNoBuild, () =>
                    {
                        IncludeNoBuild = !IncludeNoBuild;
                        thisDirty = true;
                    });
                    menu.AddItem(new GUIContent("<使用叠加场景模式>"), AdditiveModel, () =>
                    {
                        AdditiveModel = !AdditiveModel;
                        thisDirty = true;
                    });
                  /*  menu.AddItem(new GUIContent("<保持核心存在>"), WithTheAlwaysNoDieCore, () =>
                    {
                        WithTheAlwaysNoDieCore = !WithTheAlwaysNoDieCore;
                        thisDirty = true;
                    });*/
                    if (thisDirty)
                    {
                        EditorPrefs.SetBool("IncludeNoBuild", IncludeNoBuild);
                        EditorPrefs.SetBool("AdditiveModel", AdditiveModel);
                        EditorPrefs.SetBool("WithTheAlwaysNoDieCore", WithTheAlwaysNoDieCore);
                    }
                    // 添加菜单项

                    menu.AddSeparator("");
                    menu.ShowAsContext();
                }
            }

            static void OnQuickSelectionToolbarGUI()
            {
                // 创建下拉菜单按钮
                if (EditorGUILayout.DropdownButton(
                    new GUIContent("快速定位操作", EditorGUIUtility.IconContent("d__Popup").image),
                    FocusType.Passive,
                    EditorStyles.toolbarDropDown, GUILayout.Width(300)))
                {
                    var menu = new GenericMenu();



                   
                    menu.AddItem(new GUIContent("<玩家对象>"), false, () =>
                    {
                        var player = GameObject.FindGameObjectWithTag("Player");
                        if (player != null) { Selection.activeGameObject = player; EditorGUIUtility.PingObject(player); }
                    });
                    menu.AddSeparator("");
                    foreach (var (k,v) in ESGlobalEditorLocation.Instance.Assets)
                    {
                        if (v != null) {
                            
                            menu.AddItem(new GUIContent($"<{k}>"), false, () => { Selection.activeObject = v; EditorGUIUtility.PingObject(v); });
                        }
                    }

                    // 添加菜单项

                   /* var assembly = typeof(ESEditorExpand_QuickSelect).Assembly; // 获取父类所在程序集
                    var use = assembly.GetTypes()
                       .Where(t => t.IsClass && !t.IsAbstract && t.BaseType == typeof(ESEditorExpand_QuickSelect))
                       .ToList();
                    foreach (var i in use)
                    {
                        var aClass = Activator.CreateInstance(i) as ESEditorExpand_QuickSelect;
                        menu.AddItem(new GUIContent(aClass.GetGroup._Get_ATT_ESStringMessage() + "/" + aClass.MenuName), false, () =>
                        {
                            var func = aClass.GetPingUnityObject();
                            var ob = func.Invoke();
                            if (ob != null) { Selection.activeObject = ob; EditorGUIUtility.PingObject(ob); }
                        });
                    }*/


                    menu.AddSeparator("");
                    menu.ShowAsContext();
                }
            }

        }

        #region 自主扩展
        public enum ESEditorQuickSelectGroup
        {
            [ESMessage("【文件夹】")] Dir,
            [ESMessage("【资产】")] AssetObject,
            [ESMessage("【管理器】")] Manager,
            [ESMessage("【场景特殊物体】")] SceneGameObjectObject,

        }
        public abstract class ESEditorExpand_QuickSelect
        {
            public abstract ESEditorQuickSelectGroup GetGroup { get; }
            public abstract string MenuName { get; }
            public abstract Func<UnityEngine.Object> GetPingUnityObject();
            public static UnityEngine.Object Helper_GetFromTag(string tag)
            {
                return GameObject.FindGameObjectWithTag(tag);
            }
            public static UnityEngine.Object[] Helper_Get_S_FromTag(string tag)
            {
                return GameObject.FindGameObjectsWithTag(tag);
            }
            public static UnityEngine.Object Helper_GetFromCompo<T>() where T : Component
            {
                T t = UnityEngine.Object.FindAnyObjectByType<T>();
                if (t != null)
                {
                    return t.gameObject;
                }
                return null;
            }
            public static UnityEngine.Object[] Helper_Get_S_FromCompo<T>() where T : Component
            {
                var ts = UnityEngine.Object.FindObjectsByType<T>(sortMode: FindObjectsSortMode.None);
                return ts.Select((n) => n.gameObject).ToArray();
            }
            public static ScriptableObject Helper_GetSO<T>() where T : ScriptableObject
            {
                T t = UnityEngine.Object.FindAnyObjectByType<T>();
                return t;
            }
            public static ScriptableObject[] Helper_Get_S_SO<T>() where T : ScriptableObject
            {
                var ts = UnityEngine.Object.FindObjectsByType<T>(sortMode: FindObjectsSortMode.None);
                return ts;
            }

            public static UnityEngine.Object Helper_Asset_GetFromNameAndParent(string name, params string[] withparent)
            {
                string[] guids = AssetDatabase.FindAssets(name);
                foreach (var i in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(i);
                    var use = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    bool Cancel = false;
                    if (use != null)
                    {
                        if (withparent != null && withparent.Length != 0)
                        {
                            foreach (var p in withparent)
                            {
                                if (!path.Contains(p))
                                {
                                    Cancel = true;
                                    break;
                                }
                            }
                        }
                        else
                        {

                        }
                    }
                    else
                    {
                        continue;
                    }
                    if (Cancel) continue;
                    return use;
                }
                return name != "ESFramework" ? Helper_Asset_GetFromNameAndParent("ESFramework") : null;
            }

            public static string Name_ESFramework = "ESFramework";
        }
        #region 演示
        public class ESEditorExpand_QuickSelect_EntityDir : ESEditorExpand_QuickSelect
        {
            public override ESEditorQuickSelectGroup GetGroup => ESEditorQuickSelectGroup.Dir;
            public override string MenuName => "实体定义文件夹";
            public override Func<UnityEngine.Object> GetPingUnityObject()
            {
                return () => Helper_Asset_GetFromNameAndParent("Entity", Name_ESFramework);
            }
        }


        public class ESEditorExpand_QuickSelect_LinkDir : ESEditorExpand_QuickSelect
        {
            public override ESEditorQuickSelectGroup GetGroup => ESEditorQuickSelectGroup.Dir;
            public override string MenuName => "Link定义文件夹";
            public override Func<UnityEngine.Object> GetPingUnityObject()
            {
                return () => Helper_Asset_GetFromNameAndParent("Link", "Assets/Scripts/ESFramework/Interface_Abstract_Extension_Design/Link");
            }
        }

        public class ESEditorExpand_QuickSelect_SceneCamera : ESEditorExpand_QuickSelect
        {
            public override ESEditorQuickSelectGroup GetGroup => ESEditorQuickSelectGroup.SceneGameObjectObject;
            public override string MenuName => "主相机";
            public override Func<UnityEngine.Object> GetPingUnityObject()
            {
                return () => Helper_GetFromTag("MainCamera");
            }
        }
        #endregion


        #endregion
    }
}

