
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Sirenix.Serialization;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Newtonsoft.Json;
namespace ES
{
    public class ESEditorRes
    {
        public static ESResJsonData_ABMetadata MainESResData_ABMetadata = new ESResJsonData_ABMetadata();
        #region 库英文路径的唯一性验证

        public bool TrySetResLibFolderName(ResLibrary resLibrary, string preLibFolderName, int attemptCount = 0)
        {
            const int maxAttempts = 10; // 防止无限递归
            if (attemptCount >= maxAttempts)
            {
                string errorMessage = $"无法为库 '{resLibrary.Name}' 生成唯一的文件夹名，已达到最大尝试次数。";
                Debug.LogError(errorMessage);
                EditorUtility.DisplayDialog("错误", errorMessage, "确定");
                return false;
            }

            var allLibraries = ESEditorSO.SOS.GetNewGroupOfType<ResLibrary>();
            var validName = preLibFolderName._ToValidIdentName();
            foreach (var lib in allLibraries)
            {
                if (lib != resLibrary && lib.LibFolderName == validName)
                {
                    // 有重复，加一个"_r"再次判定
                    return TrySetResLibFolderName(resLibrary, validName + "_r", attemptCount + 1);
                }
            }
            resLibrary.LibFolderName = validName;
            EditorUtility.SetDirty(resLibrary);
            return true;
        }

        #endregion

        #region 资源分析


        //重复出现的资源
        private static HashSet<string> Caching_ReAsset = new HashSet<string>();

        public static void Build_PrepareAnalyzeAssetsKeys(bool onlyIndentity=false)
        {
            // 初始化总结信息收集
            System.Text.StringBuilder summary = new System.Text.StringBuilder();
            summary.AppendLine("构建准备分析资产键总结：");
            var libraries = ESEditorSO.SOS.GetNewGroupOfType<ResLibrary>();
            int totalLibraries = libraries.Count;
            try
            {
                //找到全部的库


                //清空信息
                ESResMaster.TempResLibrarys.Clear();
                foreach (var library in libraries)
                {
                    if (library != null)
                        library.Refresh();
                }
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                string errorMsg = $"初始化库时发生异常: {e.Message}";
                summary.AppendLine($"严重异常: {errorMsg}");
                Debug.LogError(errorMsg);
                EditorUtility.DisplayDialog("错误", errorMsg, "确定");
                return;
            }

            try
            {
                //扫描应用一遍资源
                {
                    int libraryIndex = 0;
                    foreach (var library in libraries)
                    {
                        libraryIndex++;
                        EditorUtility.DisplayProgressBar("构建准备", $"处理库: {library.Name} ({libraryIndex}/{totalLibraries})", (float)libraryIndex / totalLibraries);

                        if (library != null && library.Books != null)
                        {
                            // 如果同名库不存在，添加；如果存在，合并资源
                            if (!ESResMaster.TempResLibrarys.ContainsKey(library.Name))
                            {
                                var newTempLib = new TempLibrary()
                                {
                                    LibNameDisPlay = library.Name,
                                    LibFolderName = library.LibFolderName,
                                    ContainsBuild = library.ContainsBuild,
                                    IsNet = library.IsNet
                                };

                                ESResMaster.TempResLibrarys.Add(library.Name, newTempLib);
                            }

                            var tempLib = ESResMaster.TempResLibrarys[library.Name];



                            foreach (var book in library.Books)
                            {
                                if (book != null && book.pages != null)
                                {
                                    foreach (var page in book.pages)
                                    {
                                        if (page != null && page.OB != null)
                                        {
                                            string assetPath = AssetDatabase.GetAssetPath(page.OB);
                                            if (string.IsNullOrEmpty(assetPath))
                                            {
                                                string errorMsg = $"资源路径无效，跳过该资源分析：库[{library.Name}]，Book[{book.Name}]，Page[{page.Name}]";
                                                summary.AppendLine($"异常: {errorMsg}");
                                                Debug.LogError(errorMsg);
                                                continue;
                                            }
                                            try
                                            {
                                                HandleOnePage(tempLib, library.Name, library.LibFolderName, assetPath, page.OB, page, summary);
                                            }
                                            catch (Exception e)
                                            {
                                                string exMsg = $"处理页面 {page.Name} 时异常: {e.Message}";
                                                summary.AppendLine($"异常: {exMsg}");
                                                Debug.LogError(exMsg);
                                            }
                                        }
                                    }
                                }
                            }

                        }
                    }
                }
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                string errorMsg = $"扫描资源时发生异常: {e.Message}";
                summary.AppendLine($"严重异常: {errorMsg}");
                Debug.LogError(errorMsg);
                EditorUtility.DisplayDialog("错误", errorMsg, "确定");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("构建准备", "生成 JSON 数据...", 0.9f);
                //此处可以进行键Json构建
                CreateJsonData_AssetKeys(summary);

                EditorUtility.ClearProgressBar();
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                string errorMsg = $"生成 JSON 时发生异常: {e.Message}";
                summary.AppendLine($"严重异常: {errorMsg}");
                Debug.LogError(errorMsg);
                EditorUtility.DisplayDialog("错误", errorMsg, "确定");
                return;
            }

            // 输出总结
            string finalSummary = summary.ToString();
            Debug.Log(finalSummary);
            int dialogResult = EditorUtility.DisplayDialogComplex("构建总结", finalSummary, "打开远程构建位置", "继续", "取消");
            if (dialogResult == 0) // 打开远程构建位置
            {
                EditorUtility.RevealInFinder(ESGlobalResSetting.Instance.Path_RemoteResOutBuildPath);
            }
            // 无论选择什么，都Ping本地构建位置
            var localPath = ESGlobalResSetting.Instance.Path_LocalABPath_;
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(localPath);
            if (obj != null)
            {
                EditorGUIUtility.PingObject(obj);
            }
            else
            {
                EditorUtility.RevealInFinder(localPath);
            }

        }

        #endregion


        #region  资源分析部分辅助

        private static void HandleOnePage(TempLibrary tempLibrary, string libraryDisPlayName, string libraryFolderName, string assetPath, UnityEngine.Object assetObject, ResPage page, System.Text.StringBuilder summary)
        {
            bool isFolder = ESDesignUtility.SafeEditor.Wrap_IsValidFolder(assetPath);
            if (isFolder)
            {
                var filePaths = ESDesignUtility.SafeEditor.Quick_System_GetFiles_AlwaysSafe(assetPath);
                foreach (var filePath in filePaths)
                {
                    HandleAsset(tempLibrary, libraryDisPlayName, libraryFolderName, filePath, assetObject, page, true, assetPath, summary);
                }
            }
            else
            {
                HandleAsset(tempLibrary, libraryDisPlayName, libraryFolderName, assetPath, assetObject, page, false, null, summary);
            }
        }
        private static void HandleAsset(TempLibrary tempLibrary, string libraryDisPlayName, string libraryFolderName, string assetPath, UnityEngine.Object assetObject, ResPage page, bool inFolder, string folderPath, System.Text.StringBuilder summary)
        {
            // 排除不适用文件：.meta, .cs, .asmdef 等
            string extension = Path.GetExtension(assetPath).ToLowerInvariant();
            if (extension == ".meta" || extension == ".cs" || extension == ".asmdef" || extension == ".txt" || extension == ".md")
            {
                if (extension == ".cs")
                {
                    summary.AppendLine($"跳过不适用文件: {assetPath}");
                }

                return;
            }

            if (tempLibrary.ESResData_AssetKeys.PathToAssetKeys.TryGetValue(assetPath, out int index))
            {
                //没必要重复，重复直接忽略
                summary.AppendLine($"重复资源跳过: {assetPath}");
                Caching_ReAsset.Add(assetPath);
                return;
            }

            GetABNameForAssetAtResPage(assetPath, page, out var unsafeABName, out var safeABName);

            var assetImporter = AssetImporter.GetAtPath(assetPath);
            if (assetImporter == null)
            {
                string errorMsg = $"AssetImporter 未找到路径：{assetPath}，跳过该资源";
                summary.AppendLine($"异常: {errorMsg}");
                Debug.LogError(errorMsg);
                return;
            }

            if (safeABName != unsafeABName)
            {
                summary.AppendLine($"资产名优化: '{unsafeABName}' -> '{safeABName}'");
            }
            assetImporter.assetBundleName = safeABName;

            // 创建资产键
            var assetKey = new ESResKey
            {
                LibName = libraryDisPlayName,
                LibFolderName = libraryFolderName,
                ABName = safeABName,
                SourceLoadType = ESResSourceLoadType.ABAsset,
                ResName = assetObject.name,
                TargetType = assetObject.GetType()
            };

            var assetKeyCount = tempLibrary.ESResData_AssetKeys.AssetKeys.Count;
            tempLibrary.ESResData_AssetKeys.AssetKeys.Add(assetKey);

            var assetGUID = AssetDatabase.AssetPathToGUID(assetPath);
            if (!tempLibrary.ESResData_AssetKeys.PathToAssetKeys.TryAdd(assetPath, assetKeyCount))
            {
                string warnMsg = $"ES 预构建发现重复路径：{assetPath}";
                summary.AppendLine($"警告: {warnMsg}");
                Debug.LogWarning(warnMsg);
            }

            if (!tempLibrary.ESResData_AssetKeys.GUIDToAssetKeys.TryAdd(assetGUID, assetKeyCount))
            {
                string warnMsg = $"ES 预构建发现重复 GUID：{assetGUID}（路径：{assetPath}）";
                summary.AppendLine($"警告: {warnMsg}");
                Debug.LogWarning(warnMsg);
            }
        }
        #endregion

        #region AB 收集辅助方法

        private static void GetABNameForAssetAtResPage(string assetPath, ResPage page, out string unsafeABName, out string safeABName)
        {
            bool isFolder = ESDesignUtility.SafeEditor.Wrap_IsValidFolder(assetPath);
            unsafeABName = assetPath;
            if (page.namedOption == ABNamedOption.UsePageName)
            {
                unsafeABName = page.Name;
            }
            else if (page.namedOption == ABNamedOption.UseParentPath)
            {
                unsafeABName = Path.GetDirectoryName(assetPath);
            }
            else if (page.namedOption == ABNamedOption.UsePageFolder)
            {
                if (isFolder)
                {
                    unsafeABName = assetPath;
                }
                else
                {
                    unsafeABName = Path.GetDirectoryName(assetPath);
                }
            }
            safeABName = ESResMaster.ToSafeABName(unsafeABName);
        }

        private static Dictionary<string, HashSet<string>> CollectLibraryABNames()
        {
            var libraries = ESEditorSO.SOS.GetNewGroupOfType<ResLibrary>();
            var result = new Dictionary<string, HashSet<string>>();
            foreach (var library in libraries)
            {
                if (library == null || !library.ContainsBuild)
                    continue;
                var abNames = new HashSet<string>();
                if (library.Books != null)
                {
                    foreach (var book in library.Books)
                    {
                        if (book != null && book.pages != null)
                        {
                            foreach (var page in book.pages)
                            {
                                if (page != null && page.OB != null)
                                {
                                    string assetPath = AssetDatabase.GetAssetPath(page.OB);
                                    if (!string.IsNullOrEmpty(assetPath))
                                    {
                                        // 排除不适用文件
                                        string extension = Path.GetExtension(assetPath).ToLowerInvariant();
                                        if (extension == ".meta" || extension == ".cs" || extension == ".asmdef" || extension == ".txt" || extension == ".md")
                                            continue;

                                        GetABNameForAssetAtResPage(assetPath, page, out _, out string abName);
                                        abNames.Add(abName);

                                        // 如果是文件夹，添加子文件的 ABName（但通常子文件使用相同 ABName）
                                        if (ESDesignUtility.SafeEditor.Wrap_IsValidFolder(assetPath))
                                        {
                                            var filePaths = ESDesignUtility.SafeEditor.Quick_System_GetFiles_AlwaysSafe(assetPath);
                                            foreach (var filePath in filePaths)
                                            {
                                                extension = Path.GetExtension(filePath).ToLowerInvariant();
                                                if (extension != ".meta" && extension != ".cs" && extension != ".asmdef" && extension != ".txt" && extension != ".md")
                                                {
                                                    abNames.Add(abName); // 相同 ABName
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                result[library.Name] = abNames;
            }
            return result;
        }

        #endregion

        #region  AssetKeys Json生成
        private static void CreateJsonData_AssetKeys(System.Text.StringBuilder summary)
        {
            foreach (var tempLib in ESResMaster.TempResLibrarys.Values)
            {
                // 过滤：只处理需要构建的库
                if (!tempLib.ContainsBuild)
                    continue;

                // 检查数据：如果无资产键，跳过
                if (tempLib.ESResData_AssetKeys == null || tempLib.ESResData_AssetKeys.AssetKeys.Count == 0)
                {
                    summary.AppendLine($"生成Json时跳过库 '{tempLib.LibNameDisPlay}': 无资产键");
                    continue;
                }

                // 根据 IsNet 选择路径：远程库用远端构建路径，本地库用本地AB路径
                string basePath = tempLib.IsNet
                    ? ESGlobalResSetting.Instance.Path_RemoteResOutBuildPath
                    : ESGlobalResSetting.Instance.Path_LocalABPath_;

                // 检查路径有效性
                if (string.IsNullOrEmpty(basePath))
                {
                    string errorMsg = $"库 '{tempLib.LibNameDisPlay}' 的基础路径为空，跳过 JSON 生成。IsNet: {tempLib.IsNet}";
                    summary.AppendLine($"异常: {errorMsg}");
                    Debug.LogError(errorMsg);
                    continue;
                }

                try
                {
                    // 目标目录：basePath/{Platform}/ESResData
                    string DataParentPath = basePath + "/" + ESResMaster.GetParentFolderNameByRuntimePlatform(ESGlobalResSetting.Instance.applyPlatform) + "/" + ESResMaster.JsonDataFileName.PathParentFolder_ESResJsonData;

                    // 库目录：DataParentPath/{LibFolderName}
                    string LibDataPath = DataParentPath + "/" + tempLib.LibFolderName;
                    // 创建库目录
                    var createResult = ESDesignUtility.SafeEditor.Quick_System_CreateDirectory(LibDataPath);
                    if (!createResult.Success)
                    {
                        string errorMsg = $"创建目录失败：{LibDataPath}，消息：{createResult.Message}";
                        summary.AppendLine($"异常: {errorMsg}");
                        Debug.LogError(errorMsg);
                        continue;
                    }

                    // 序列化并写入各个库的 AssetKeys.json
                    string libKeysJson = JsonConvert.SerializeObject(tempLib.ESResData_AssetKeys);
                    string libKeysPath = LibDataPath + "/" + ESResMaster.JsonDataFileName.PathFileName_ESAssetkeys;

                    File.WriteAllText(libKeysPath, libKeysJson);
                    summary.AppendLine($"成功生成 AssetKeys JSON：{libKeysPath} (资产数: {tempLib.ESResData_AssetKeys.AssetKeys.Count})");
                    Debug.Log($"成功生成 AssetKeys JSON：{libKeysPath}");
                }
                catch (Exception e)
                {
                    string exMsg = $"生成库 '{tempLib.LibNameDisPlay}' 的 AssetKeys JSON 时发生异常：{e.Message}";
                    summary.AppendLine($"异常: {exMsg}");
                    Debug.LogError(exMsg);
                }
            }
#if UNITY_EDITOR
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
#endif
        }


        #endregion

        #region AB包构建和依赖，Hash生成
        public static void BuildABAndCreateABHashAndDependsJsonData()
        {
            var buildTarget = ESResMaster.GetValidBuildTargetByRuntimePlatform(ESGlobalResSetting.Instance.applyPlatform);

            BuildABForTarget(BuildAssetBundleOptions.AppendHashToAssetBundleName, buildTarget);
            CreateJsonData_ABHashAndDependence();
        }

        private static void BuildABForTarget(BuildAssetBundleOptions assetBundleOptions, BuildTarget buildTarget)
        {
            var globalSettings = ESGlobalResSetting.Instance;
            string platformFolderName = ESResMaster.GetFolderNameByBuildTarget(buildTarget);

            // 路径验证
            if (string.IsNullOrEmpty(globalSettings.Path_BuildInitialTarget) || string.IsNullOrEmpty(platformFolderName))
            {
                string errorMsg = "输出路径无效：Path_BuildInitialTarget或platformFolderName为空";
                Debug.LogError(errorMsg);
                EditorUtility.DisplayDialog("错误", errorMsg, "确定");
                return;
            }

            string outputPath = globalSettings.Path_BuildInitialTarget + "/" + platformFolderName;

            if (string.IsNullOrEmpty(outputPath))
            {
                string errorMsg = "输出路径构建失败";
                Debug.LogError(errorMsg);
                EditorUtility.DisplayDialog("错误", errorMsg, "确定");
                return;
            }
            var directoryCreationResult = ESDesignUtility.SafeEditor.Quick_System_CreateDirectory(outputPath);
            if (directoryCreationResult.Success)
            {
                Debug.Log("尝试构建" + directoryCreationResult.Message);
                var manifest = BuildPipeline.BuildAssetBundles(outputPath, assetBundleOptions, buildTarget);

                // 构建成功检查
                if (manifest == null)
                {
                    string errorMsg = "ABMeta：AssetBundleManifest为null";
                    Debug.LogError(errorMsg);
                    EditorUtility.DisplayDialog("错误", errorMsg, "确定");
                    return;
                }
                else
                {
                    Debug.Log("AB构建成功");
                }

                if (false)
                {
                    // 大规模文件迁移：将构建的AB文件移动到本地AB路径
                    string localOutputPath = globalSettings.Path_LocalABPath_ + "/" + platformFolderName;
                    try
                    {
                        // 如果本地路径已存在，先删除或处理
                        if (Directory.Exists(localOutputPath))
                        {
                            Directory.Delete(localOutputPath, true);
                        }
                        // 移动整个目录
                        Directory.Move(outputPath, localOutputPath);
                        Debug.Log($"AB文件迁移完成：{outputPath} -> {localOutputPath}");
                    }
                    catch (Exception e)
                    {
                        string errorMsg = $"AB文件迁移失败：{e.Message}";
                        Debug.LogError(errorMsg);
                        EditorUtility.DisplayDialog("错误", errorMsg, "确定");
                    }
                }
            }
            else
            {
                string errorMsg = $"创建输出目录失败：{outputPath}，消息：{directoryCreationResult.Message}";
                Debug.LogError(errorMsg);
                EditorUtility.DisplayDialog("错误", errorMsg, "确定");
            }
        }
        
        private static void CreateJsonData_ABHashAndDependence()
        {
            // 为每个库生成AB键， AB Hash 和 依赖关系的 JSON 数据，通过重新检索 Library
            System.Text.StringBuilder summary = new System.Text.StringBuilder();
            summary.AppendLine("AB键， Hash 和依赖 JSON 生成总结：");

            try
            {
                var libraries = ESEditorSO.SOS.GetNewGroupOfType<ResLibrary>();
                var libraryABNames = CollectLibraryABNames();
                var ABNames = AssetDatabase.GetAllAssetBundleNames();
                //先卸载加载
                AssetBundle.UnloadAllAssetBundles(unloadAllObjects: false);
                string plat = ESResMaster.GetParentFolderNameByRuntimePlatform(ESGlobalResSetting.Instance.applyPlatform);
                AssetBundle MainBundle = AssetBundle.LoadFromFile(Path.Combine(ESGlobalResSetting.Instance.Path_BuildInitialTarget, plat, plat));
                //FEST
                AssetBundleManifest manifest = MainBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                //带Hash值的AB
                string[] AllABWithHash = manifest.GetAllAssetBundles();

                // 构建 pre 到 withHash 的映射，提高效率
                Dictionary<string, string> preToHash = new Dictionary<string, string>();
                foreach (var withHash in AllABWithHash)
                {
                    string pre = ESResMaster.PathAndNameTool_GetPreName(withHash);
                    preToHash[pre] = withHash;
                }

                foreach (var kvp in libraryABNames)
                {
                    string libName = kvp.Key;
                    HashSet<string> libABNames = kvp.Value;
                    var library = libraries.FirstOrDefault(l => l.Name == libName);
                    if (library == null || !library.ContainsBuild)
                        continue;

                    // 初始化该库的 ABMetadata
                    var abMetadata = new ESResJsonData_ABMetadata();
                    abMetadata.PreToHashes.Clear();
                    abMetadata.Dependences.Clear();
                    abMetadata.ABKeys.Clear();

                    foreach (var abName in libABNames)
                    {
                        if (preToHash.TryGetValue(abName, out var withHash))
                        {
                            abMetadata.PreToHashes.TryAdd(abName, withHash);

                            string[] abDepend = manifest.GetAllDependencies(withHash);
                            for (int i = 0; i < abDepend.Length; i++)
                            {
                                abDepend[i] = ESResMaster.PathAndNameTool_GetPreName(abDepend[i]);
                            }
                            // 包括所有依赖，即使跨库
                            if (abDepend.Length > 0)
                                abMetadata.Dependences.Add(abName, abDepend);

                            ESResKey key = new ESResKey() { LibName = libName, ABName = abName, SourceLoadType = ESResSourceLoadType.AssetBundle, ResName = withHash, TargetType = typeof(AssetBundle) };
                            abMetadata.ABKeys.Add(key);
                            
                        }
                        else
                        {
                            summary.AppendLine($"警告: 库 '{libName}' 的 AB '{abName}' 未找到对应的带哈希版本");
                        }
                    
                    }
                

                    // 保存该库的 ABMetadata JSON
                    // 根据 IsNet 选择路径
                    string basePath = library.IsNet
                        ? ESGlobalResSetting.Instance.Path_RemoteResOutBuildPath
                        : ESGlobalResSetting.Instance.Path_LocalABPath_;

                    if (string.IsNullOrEmpty(basePath))
                    {
                        string errorMsg = $"库 '{libName}' 的基础路径为空，跳过 ABMetadata JSON 生成。IsNet: {library.IsNet}";
                        summary.AppendLine($"异常: {errorMsg}");
                        Debug.LogError(errorMsg);
                        continue;
                    }

                    try
                    {
                        // 目标目录：basePath/{Platform}/ESResData/{LibFolderName}
                        string LibDataPath = basePath + "/" + ESResMaster.GetParentFolderNameByRuntimePlatform(ESGlobalResSetting.Instance.applyPlatform) + "/" + ESResMaster.JsonDataFileName.PathParentFolder_ESResJsonData + "/" + library.LibFolderName;
                        // 创建库目录
                        var createResult = ESDesignUtility.SafeEditor.Quick_System_CreateDirectory(LibDataPath);
                        if (!createResult.Success)
                        {
                            string errorMsg = $"创建目录失败：{LibDataPath}，消息：{createResult.Message}";
                            summary.AppendLine($"异常: {errorMsg}");
                            Debug.LogError(errorMsg);
                            continue;
                        }

                        // 序列化并写入 ABMetadata.json
                        string metadata_Json = JsonConvert.SerializeObject(abMetadata);
                        string metadata_Path = LibDataPath + "/" + ESResMaster.JsonDataFileName.PathJsonFileName_ESABMetadata;
                        File.WriteAllText(metadata_Path, metadata_Json);
                        summary.AppendLine($"成功生成 ABMetadata JSON：{metadata_Path} (AB数: {abMetadata.ABKeys.Count})");
                        Debug.Log($"成功生成 ABMetadata JSON：{metadata_Path}");
                    }
                    catch (Exception e)
                    {
                        string exMsg = $"生成库 '{libName}' 的 ABMetadata JSON 时发生异常：{e.Message}";
                        summary.AppendLine($"异常: {exMsg}");
                        Debug.LogError(exMsg);
                    }
                }

                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                string errorMsg = $"生成 AB Hash 和依赖 JSON 时发生异常: {e.Message}";
                summary.AppendLine($"严重异常: {errorMsg}");
                Debug.LogError(errorMsg);
                EditorUtility.DisplayDialog("错误", errorMsg, "确定");
                return;
            }

            // 输出总结
            string finalSummary = summary.ToString();
            Debug.Log(finalSummary);
            EditorUtility.DisplayDialog("AB 生成总结", finalSummary, "确定");
        }
       
       
        #endregion
    }
}
