using Codice.CM.Common.Zlib;
using GluonGui.WorkspaceWindow.Views.WorkspaceExplorer;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
namespace ES
{
    public class ESEditorRes
    {
        //重复出现的资源
        private static HashSet<string> Caching_ReAsset = new HashSet<string>();


        public static void Build_PrepareAnalyzeAssetsBundles()
        {
            //找到全部的库
            var libs = ESEditorSO.SOS.GetNewGroupOfType<ResLibrary>();

            //清空信息
            ESResMaster.ESResData_AssetKeys.AssetKeys.Clear();
            ESResMaster.ESResData_AssetKeys.GUIDToAssetKeys.Clear();
            ESResMaster.ESResData_AssetKeys.PathToAssetKeys.Clear();
            //扫描应用一遍资源
            {
                foreach (var lib in libs)
                {
                    if (lib != null && lib.Books != null)
                        foreach (var gr in lib.Books)
                        {
                            if (gr != null && gr.pages != null)
                                foreach (var page in gr.pages)
                                {
                                    if (page != null && page.OB != null)
                                    {
                                        string path = AssetDatabase.GetAssetPath(page.OB);
                                        _TryApplyNewPage(lib.Name, path, page.OB, page);
                                    }
                                }
                        }
                }
            }
            //应用一遍AB包
     /*       var allAssetBundleNames = AssetDatabase.GetAllAssetBundleNames();
            foreach (var ab in allAssetBundleNames)
            {
                
            }*/
            //此处可以进行键Json构建
            ESResMaster.JsonData_CreateAssetKeys();
        }
        #region 辅助分析-
        private static void _TryApplyNewPage(string LibName, string path, UnityEngine.Object OB, ResPage page)
        {
            bool isFolder = ESDesignUtility.SafeEditor.Wrap_IsValidFolder(path);
            if (isFolder)
            {
                var paths = ESDesignUtility.SafeEditor.Quick_System_GetFiles_AlwaysSafe(path);
                foreach (var insidePath in paths)
                {
                    _TryAddRes(LibName, insidePath, OB, page, true, path);
                }
            }
            else
            {
                _TryAddRes(LibName, path, OB, page, false);
            }
        }
        private static void _TryAddRes(string LibName, string path, UnityEngine.Object OB, ResPage page, bool InFolder, string FolderPath = null)
        {
            if (ESResMaster.ESResData_AssetKeys.PathToAssetKeys.TryGetValue(path, out int index))
            {
                //没必要重复，重复直接忽略
                Caching_ReAsset.Add(path);
                return;
            }

            var ABNAMEUnsafe = path;
            if (page.namedOption == NamedOption.UsePageName)
            {
                ABNAMEUnsafe = page.Name;
            }
            else if (page.namedOption == NamedOption.UseParentPath)
            {
                ABNAMEUnsafe = Path.GetDirectoryName(path);
            }
            else if (page.namedOption == NamedOption.UsePageFolder)
            {
                ABNAMEUnsafe = FolderPath;
            }
            var ai = AssetImporter.GetAtPath(path);
            if (ai == null)
            {
                Debug.LogError($"AssetImporter未找到路径：{path}，跳过该资源");
                return;
            }
            var ABNameSafe = ESResMaster.ToSafeABName(ABNAMEUnsafe);
            Debug.Log("包名转化" + ABNAMEUnsafe + "=》" + ABNameSafe);
            ai.assetBundleName = ABNameSafe;
            //开始应用
            var key = new ESResKey() { LibName = LibName, ABName = ABNameSafe, SourceLoadType= ESResSourceLoadType.ABAsset, ResName = OB.name, TargetType = OB.GetType() };
            var count = ESResMaster.ESResData_AssetKeys.AssetKeys.Count;
            ESResMaster.ESResData_AssetKeys.AssetKeys.Add(key);
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (!ESResMaster.ESResData_AssetKeys.PathToAssetKeys.TryAdd(path, count))
            {
                Debug.LogWarning("ES预-构建发现了意外的重复的路径!!!" + path);
            };
            if (!ESResMaster.ESResData_AssetKeys.GUIDToAssetKeys.TryAdd(guid, count))
            {
                Debug.LogWarning("ES预-构建发现了意外的重复的GUID!!!" + guid);
            }
        }
        #endregion

        public static void Build_BuildAB()
        {
            var target = GetValidBuildTargetFromRuntimePlatform(ESGlobalResSetting.Instance.applyPlatform);

            BuildABForTarget(BuildAssetBundleOptions.AppendHashToAssetBundleName,target);
        }
        #region 辅助打包
        private static void BuildABForTarget(BuildAssetBundleOptions assetBundleOptions, BuildTarget target)
        {
            var setting = ESGlobalResSetting.Instance;
            string folderChild =BuildTargetFolderName(target);
            string pathWithPlat = setting.Path_RemoteResOutBuildPath + "/" + folderChild;
            var valid = ESDesignUtility.SafeEditor.Quick_System_CreateDirectory(pathWithPlat);
            if (valid.Success)
            {
                Debug.Log("尝试构建" + valid.Message);
                BuildPipeline.BuildAssetBundles(pathWithPlat, assetBundleOptions, target);
            }
        }
        private static BuildTarget GetValidBuildTargetFromRuntimePlatform(RuntimePlatform? RunTime = null)
        {
            var tryUse = RunTime ?? Application.platform;
            if (tryUse == RuntimePlatform.WindowsPlayer || tryUse == RuntimePlatform.WindowsEditor)
            {
                return BuildTarget.StandaloneWindows64;
            }
            else if (tryUse == RuntimePlatform.Android)
            {
                return BuildTarget.Android;
            }
            else if (tryUse == RuntimePlatform.IPhonePlayer)
            {
                return BuildTarget.iOS;
            }
            return BuildTarget.StandaloneWindows64;
        }




        /// <summary>
        /// 将BuildTarget映射为对应的文件夹名称字符串
        /// </summary>
        /// <param name="target">BuildTarget枚举值</param>
        /// <returns>对应的文件夹名称字符串</returns>
        private static string BuildTargetFolderName(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "WindowsPlayer";
                case BuildTarget.Android:
                    return "Android";
                case BuildTarget.iOS:
                case BuildTarget.tvOS:
                    return "iOS";
                case BuildTarget.WebGL:
                    return "WebGL";
                case BuildTarget.StandaloneOSX:
                    return "macOS";
                case BuildTarget.StandaloneLinux64:
                    return "Linux";
                case BuildTarget.WSAPlayer: // UWP
                    return "UWP";
                case BuildTarget.PS5:
                    return "PS5";
                case BuildTarget.XboxOne:
                    return "Xbox";
                default:
                    // 未知平台使用枚举名称的小写形式（如"unknown"）
                    return target.ToString().ToLower();
            }
        }

        /// <summary>
        /// 将RuntimePlatform映射为对应的文件夹名称字符串
        /// </summary>
        /// <param name="platfoem">BuildTarget枚举值</param>
        /// <returns>对应的文件夹名称字符串</returns>
        private static string RunTimePlatformFolderName(RuntimePlatform platfoem)
        {
            switch (platfoem)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return "Windows";
                case RuntimePlatform.Android:
                    return "Android";
                case RuntimePlatform.IPhonePlayer:
                case RuntimePlatform.tvOS:
                    return "iOS";
                case RuntimePlatform.WebGLPlayer:
                    return "WebGL";
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    return "Linux";
                case RuntimePlatform.PS5:
                    return "PS5";
                case RuntimePlatform.XboxOne:
                    return "Xbox";
                default:
                    // 未知平台使用枚举名称的小写形式（如"unknown"）
                    return platfoem.ToString().ToLower();
            }
        }
        #endregion
    }
}
