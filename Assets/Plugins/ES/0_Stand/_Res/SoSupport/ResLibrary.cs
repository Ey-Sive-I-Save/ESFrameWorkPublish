using ES;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ResLibrary
    /// 
    /// ScriptableObject 形式的资源库：
    /// - 继承 SoLibrary&lt;ResBook&gt;，一份 Library 可以包含多本 ResBook；
    /// - 通过 Inspector 决定是否参与构建、是否走远程下载；
    /// - 是上层“资源分组 / 逻辑分类”的载体，真正的 AB / 路径信息由 ResPage 决定。
    /// </summary>
    public class ResLibrary : SoLibrary<ResBook>
    {
        [LabelText("参与构建")]
        public bool ContainsBuild = true;
        [ESBoolOption("通过远程下载", "是本体库")]
        public bool IsNet = true;
        [LabelText("描述")]
        public string Desc = "";
    }
    [Serializable]
    public class ResBook : Book<ResPage>
    {
        /// <summary>
        /// 对当前 Book 的补充说明，例如“战斗相关资源”“登录场景资源”等，
        /// 仅用于编辑器标记与文档，可结合构建管线生成报表。
        /// </summary>
        [LabelText("描述")]
        public string Desc = "";


    }
    [Serializable]
    public class ResPage : IString
    {
        [LabelText("资源页名")]
        public string Name = "资源页名";
        [LabelText("分AB包命名方式")]
        public NamedOption namedOption;
        [LabelText("绑定资源")]
        public UnityEngine.Object OB;
        private bool foldVisiable;
        private bool refresh = true;

        private List<string> paths = new List<string>();
        public string GetSTR()
        {
            return Name;
        }

        public void SetSTR(string str)
        {
            Name = str;
        }
        public bool Draw()
        {

            bool dirty = false;
#if UNITY_EDITOR
            var pre = OB;
            OB = EditorGUILayout.ObjectField("文件夹或资源", OB, typeof(UnityEngine.Object), allowSceneObjects: false);
            if (OB != null)
            {
                if (pre != OB)
                {
                    Name = OB.name;
                    dirty = true;
                    AssetDatabase.SaveAssets();
                }
                var path = ESStandUtility.SafeEditor.Wrap_GetAssetPath(OB);
                bool isFolder = (ESStandUtility.SafeEditor.Wrap_IsValidFolder(path));

                {
                    SirenixEditorGUI.BeginBox();
                    SirenixEditorGUI.BeginBoxHeader();
                    bool ago = foldVisiable;
                    foldVisiable = SirenixEditorGUI.Foldout(foldVisiable, "路径");
                    if (ago != foldVisiable)
                    {
                        refresh = true;
                    }
                    SirenixEditorGUI.EndBoxHeader();
                    if (foldVisiable)
                    {

                        if (isFolder)
                        {
                            if (refresh)
                            {
                                dirty = true;
                                paths = ESStandUtility.SafeEditor.Quick_System_GetFilePaths_AlwaysSafe(path);
                                refresh = false;
                            }
                            foreach (var i in paths)
                            {
                                EditorGUILayout.LabelField(i, GUILayout.MaxWidth(400));
                            }
                        }
                        else
                        {
                            EditorGUILayout.LabelField(path, GUILayout.MaxWidth(400));
                        }
                    }
                    SirenixEditorGUI.EndBox();
                };


            }
#endif
            return dirty;

        }
    }
    public enum NamedOption
    {
        [InspectorName("包名-使用自己的文件路径")] UseFilePath,
        [InspectorName("包名-使用自己的父文件夹路径")] UseParentPath,
        [InspectorName("包名-使用页的顶级路径")] UsePageFolder,
        [InspectorName("包名-使用Page命名")] UsePageName,

    }
}

