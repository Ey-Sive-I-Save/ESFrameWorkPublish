using ES;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;


namespace ES
{
    [HideMonoScript]
    //为快速定位准备的匹配信息
    [CreateAssetMenu(fileName = "全局资源管理设置", menuName = "全局SO/全局资源管理设置")]
    public class ESGlobalResSetting : ESEditorGlobalSo<ESGlobalResSetting>
    {

        [DisplayAsString(fontSize: 30, Alignment = TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
        public string createText = "--资源管理全局设置--";
        [HorizontalGroup("总体", Order = 5, MarginRight = 50)]

        [VerticalGroup("总体/构建与运行")]
        [Header("构建与运行")]
        [LabelText("应用平台")]
        public RuntimePlatform applyPlatform = RuntimePlatform.WindowsPlayer;

        [VerticalGroup("总体/构建与运行")]
        [ESBoolOption("启用发布模式", "使用模拟模式")]
        public bool EnablePublishMode = true;
        [VerticalGroup("总体/构建与运行")]
        [LabelText("辅助代码生成模式")]
        public ESABCodegenMode CodegenMode = ESABCodegenMode.CodeAsOriginal;

        [HorizontalGroup("总体")]
        [Header("文件夹")]
        [VerticalGroup("总体/文件夹路径")]
        [DetailedInfoBox("↓这个需要自己配置好!", "↓参考本体包资源路径，他们是一个标准", InfoMessageType = InfoMessageType.Warning, VisibleIf = "@Path_Net.Length<10")]
        [LabelText("服务器/网络路径")]
        public string Path_Net = "http....";

        [VerticalGroup("总体/文件夹路径")]
        [LabelText("远程资源库构建文件夹"), ShowInInspector, InlineButton("OpenOutBuild", "打开远端构建文件夹")]
        public string Path_RemoteResOutBuildPath { get => _path_RemoteResOutPath; set { } }
        private string _path_RemoteResOutPath = Application.dataPath._RemoveSubStrings("/Assets") + "/ES/Res";

        [VerticalGroup("总体/文件夹路径")]
        [FolderPath, LabelText("默认资源库放置文件夹")]
        [InlineButton("Ping_", "<*>")]
        public string Path_ResLibraryFolder = "";

        [VerticalGroup("总体/文件夹路径")]
        [FolderPath, LabelText("AB帮助代码生成文件夹")]
        [InlineButton("Ping_", "<*>")]
        public string Path_ABHelperCodeGen = "";

        [VerticalGroup("总体/文件夹路径")]
        [FolderPath, LabelText("本地AB包资源父路径")]
        [InlineButton("Ping_", "<*>")]
        public string Path_LocalABPath = "";

        [VerticalGroup("总体/文件夹路径")]
        [LabelText("下载持久相对路径")]
        [InlineButton("OpenPersist", "打开持久下载文件夹")]
        public string Path_Sub_DownloadRelative = "ABDownLoad";


        private static Color color = new Color(0.05f, 0.05f, 0.05f, 1);



        public override void OnEditorInitialized()
        {
#if UNITY_EDITOR
            base.OnEditorInitialized();
            this.SHOW_Global = () => { return Selection.activeObject == this; };
#endif
        }

        private void OpenOutBuild()
        {
            string log= ESStandUtility.SafeEditor.Quick_System_CreateDirectory(_path_RemoteResOutPath).Message;
            Debug.Log(log);
            ESStandUtility.SafeEditor.Quick_OpenInSystemFolder(_path_RemoteResOutPath, false);
        }
        private void OpenPersist()
        {
            ESStandUtility.SafeEditor.Quick_System_CreateDirectory(Application.persistentDataPath + "/" + Path_Sub_DownloadRelative);
            ESStandUtility.SafeEditor.Quick_OpenInSystemFolder(Application.persistentDataPath + "/" + Path_Sub_DownloadRelative, false);
        }
        private void Ping_(string path)
        {
            ESStandUtility.SafeEditor.Quick_CreateFolderByFullPath(path);
            ESStandUtility.SafeEditor.Quick_PingAssetByPath(path);
        }

    }
    public enum ESABCodegenMode
    {
        [InspectorName("不生成代码")] NoneCode,
        [InspectorName("默认生成")] CodeAsOriginal,
        [InspectorName("转为大写")] CodeAsUpper,
        [InspectorName("转为小写")] CodeAsLower
    }
}
