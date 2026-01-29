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

        // 全局资源依赖映射：资源路径 -> 其依赖的资源路径列表
        private static Dictionary<string, List<string>> AssetToDependencies = new Dictionary<string, List<string>>();

        // 全局资源到AB包名映射：资源路径 -> AB包名
        private static Dictionary<string, string> AssetToABName = new Dictionary<string, string>();

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

        public static void Build_PrepareAnalyzeAssetsKeys(bool onlyIndentity = false)
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
                AssetToDependencies.Clear(); // 清空依赖映射
                AssetToABName.Clear(); // 清空AB名映射
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

                        if (library != null)
                        {
                            // 如果同名库不存在，添加；如果存在，合并资源
                            if (!ESResMaster.TempResLibrarys.ContainsKey(library.Name))
                            {
                                var newTempLib = new ESBuildTempResLibrary()
                                {
                                    LibNameDisPlay = library.Name,
                                    LibFolderName = library.LibFolderName,
                                    ContainsBuild = library.ContainsBuild,
                                    IsNet = library.IsNet
                                };

                                ESResMaster.TempResLibrarys.Add(library.Name, newTempLib);
                            }

                            var tempLib = ESResMaster.TempResLibrarys[library.Name];

                            // 使用GetAllUseableBooks统一获取普通Books和DefaultBooks
                            foreach (var book in library.GetAllUseableBooks())
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
                EditorUtility.DisplayProgressBar("构建准备", "处理循环依赖...", 0.9f);

                // 处理循环依赖
                var cycleReport = HandleCircularDependencies(AssetToDependencies, AssetToABName);

                // 重新应用AB名（处理循环依赖后的更改）
                ApplyUpdatedABNames(summary);

                // 输出循环依赖汇总报告
                AppendCircularDependencyReport(summary, cycleReport);
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
            var localPath = ESGlobalResSetting.Instance.Path_LocalBuildOnEditorPath_;
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

        private static void HandleOnePage(ESBuildTempResLibrary tempLibrary, string libraryDisPlayName, string libraryFolderName, string assetPath, UnityEngine.Object assetObject, ResPage page, System.Text.StringBuilder summary)
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
        private static void HandleAsset(ESBuildTempResLibrary tempLibrary, string libraryDisPlayName, string libraryFolderName, string assetPath, UnityEngine.Object assetObject, ResPage page, bool inFolder, string folderPath, System.Text.StringBuilder summary)
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

            // 检查重复资源
            if (Caching_ReAsset.Contains(assetPath))
            {
                //没必要重复，重复直接忽略
                summary.AppendLine($"重复资源跳过: {assetPath}");
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

            // 记录AB名映射
            AssetToABName[assetPath] = safeABName;

            // 生成并记录资源键
            if (tempLibrary.ESResData_AssetKeys == null)
                tempLibrary.ESResData_AssetKeys = new ESResJsonData_AssetsKeys();

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            Type targetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath) ?? assetObject?.GetType();
            var resKey = new ESResKey
            {
                LibName = tempLibrary.LibNameDisPlay,
                LibFolderName = libraryFolderName,
                ABPreName = safeABName,
                SourceLoadType = ESResSourceLoadType.ABAsset,
                ResName = assetPath,
                GUID = guid,
                Path = assetPath,
                TargetType = targetType
            };
            tempLibrary.ESResData_AssetKeys.AssetKeys.Add(resKey);
            Caching_ReAsset.Add(assetPath);

            // 收集依赖
            string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
            List<string> depList = new List<string>();
            foreach (string dep in dependencies)
            {
                // 排除自身和不相关的文件
                if (dep != assetPath && !dep.EndsWith(".meta") && !dep.EndsWith(".cs") && !dep.EndsWith(".asmdef"))
                {
                    depList.Add(dep);
                }
            }
            depList.Sort(StringComparer.Ordinal);
            AssetToDependencies[assetPath] = depList;
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
            Debug.Log("[CollectLibraryABNames] 开始收集AB名称");
            // 若数据被清除或Domain Reload，重新走一遍依赖构建并处理循环依赖
            EnsureCircularDependencyProcessedForLibraryScan();

            // 若仍为空，再从AssetDatabase重建兜底
            EnsureAssetToABNameCache();
            var libraries = ESEditorSO.SOS.GetNewGroupOfType<ResLibrary>();
            var result = new Dictionary<string, HashSet<string>>();
            foreach (var library in libraries.OrderBy(l => l?.Name, StringComparer.Ordinal))
            {
                if (library == null || !library.ContainsBuild)
                    continue;
                var abNames = new HashSet<string>();
                var useableBooks = library.GetAllUseableBooks();
                if (useableBooks != null)
                {
                    foreach (var book in useableBooks.OrderBy(b => b?.Name, StringComparer.Ordinal))
                    {
                        if (book != null && book.pages != null)
                        {
                            foreach (var page in book.pages.OrderBy(p => p?.Name, StringComparer.Ordinal))
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

                                        string abName;
                                        if (!AssetToABName.TryGetValue(assetPath, out abName))
                                        {
                                            GetABNameForAssetAtResPage(assetPath, page, out _, out abName);
                                        }
                                        abNames.Add(abName);

                                        // 如果是文件夹，添加子文件的 ABName（但通常子文件使用相同 ABName）
                                        if (ESDesignUtility.SafeEditor.Wrap_IsValidFolder(assetPath))
                                        {
                                            var filePaths = ESDesignUtility.SafeEditor.Quick_System_GetFiles_AlwaysSafe(assetPath);
                                            foreach (var filePath in filePaths.OrderBy(p => p, StringComparer.Ordinal))
                                            {
                                                extension = Path.GetExtension(filePath).ToLowerInvariant();
                                                if (extension != ".meta" && extension != ".cs" && extension != ".asmdef" && extension != ".txt" && extension != ".md")
                                                {
                                                    if (!AssetToABName.TryGetValue(filePath, out var childAbName))
                                                    {
                                                        childAbName = abName;
                                                    }
                                                    abNames.Add(childAbName); // 相同 ABName
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
                Debug.Log($"[CollectLibraryABNames] 库 '{library.Name}' AB数量: {abNames.Count}");
            }
            Debug.Log($"[CollectLibraryABNames] 完成，库数: {result.Count}");
            return result;
        }

        /// <summary>
        /// 在CollectLibraryABNames前确保依赖扫描与循环依赖处理已完成
        /// </summary>
        private static void EnsureCircularDependencyProcessedForLibraryScan()
        {
            Debug.Log("[EnsureCircularDependencyProcessedForLibraryScan] 强制重建依赖缓存");

            if (AssetToDependencies == null)
                AssetToDependencies = new Dictionary<string, List<string>>();
            if (AssetToABName == null)
                AssetToABName = new Dictionary<string, string>();

            AssetToDependencies.Clear();
            AssetToABName.Clear();
            Caching_ReAsset.Clear();

            var libraries = ESEditorSO.SOS.GetNewGroupOfType<ResLibrary>();
            if (libraries == null || libraries.Count == 0)
            {
                Debug.LogWarning("[EnsureCircularDependencyProcessedForLibraryScan] 未找到任何库，终止重建");
                return;
            }

            if (ESResMaster.TempResLibrarys == null)
                ESResMaster.TempResLibrarys = new SafeDictionary<string, ESBuildTempResLibrary>(() => new ESBuildTempResLibrary());
            else
                ESResMaster.TempResLibrarys.Clear();

            // 重新扫描资源构建依赖
            var summary = new System.Text.StringBuilder();
            foreach (var library in libraries.OrderBy(l => l?.Name, StringComparer.Ordinal))
            {
                if (library == null || !library.ContainsBuild)
                    continue;

                Debug.Log($"[EnsureCircularDependencyProcessedForLibraryScan] 扫描库: {library.Name}");

                if (!ESResMaster.TempResLibrarys.ContainsKey(library.Name))
                {
                    ESResMaster.TempResLibrarys[library.Name] = new ESBuildTempResLibrary
                    {
                        LibNameDisPlay = library.Name,
                        LibFolderName = library.LibFolderName,
                        ContainsBuild = library.ContainsBuild,
                        IsNet = library.IsNet
                    };
                }

                var tempLib = ESResMaster.TempResLibrarys[library.Name];
                var useableBooks = library.GetAllUseableBooks();
                if (useableBooks == null)
                    continue;

                foreach (var book in useableBooks.OrderBy(b => b?.Name, StringComparer.Ordinal))
                {
                    if (book?.pages == null)
                        continue;

                    foreach (var page in book.pages.OrderBy(p => p?.Name, StringComparer.Ordinal))
                    {
                        if (page?.OB == null)
                            continue;

                        string assetPath = AssetDatabase.GetAssetPath(page.OB);
                        if (string.IsNullOrEmpty(assetPath))
                            continue;

                        HandleOnePage(tempLib, library.Name, library.LibFolderName, assetPath, page.OB, page, summary);
                    }
                }
            }

            // 处理循环依赖并同步AB名
            var cycleReport = HandleCircularDependencies(AssetToDependencies, AssetToABName);
            ApplyUpdatedABNames(summary);
            AppendCircularDependencyReport(summary, cycleReport);
            Debug.Log("[EnsureCircularDependencyProcessedForLibraryScan] 重建完成");
        }

        /// <summary>
        /// 确保AssetToABName缓存存在；若为空则从AssetDatabase重建
        /// </summary>
        private static void EnsureAssetToABNameCache()
        {
            if (AssetToABName != null && AssetToABName.Count > 0)
                return;

            if (AssetToABName == null)
                AssetToABName = new Dictionary<string, string>();

            AssetToABName.Clear();

            string[] allBundleNames = AssetDatabase.GetAllAssetBundleNames();
            foreach (var abName in allBundleNames)
            {
                var assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(abName);
                foreach (var path in assetPaths)
                {
                    if (string.IsNullOrEmpty(path))
                        continue;
                    AssetToABName[path] = abName;
                }
            }
        }


        private static Dictionary<string, HashSet<string>> GetLibraryActualABNames()
        {
            var libraries = ESEditorSO.SOS.GetNewGroupOfType<ResLibrary>();
            var result = new Dictionary<string, HashSet<string>>();
            var globalSettings = ESGlobalResSetting.Instance;
            string platformFolderName = ESResMaster.GetParentFolderNameByRuntimePlatform(globalSettings.applyPlatform);

            foreach (var library in libraries)
            {
                if (library == null || !library.ContainsBuild)
                    continue;

                // 根据 IsNet 选择基础路径
                string basePath = library.IsNet
                    ? globalSettings.Path_RemoteResOutBuildPath
                    : globalSettings.Path_LocalBuildOnEditorPath_;

                if (string.IsNullOrEmpty(basePath))
                {
                    Debug.LogError($"库 '{library.Name}' 的基础路径为空，跳过扫描");
                    continue;
                }

                // 库数据路径：basePath/{Platform}/ESResData/{LibFolderName}/AB
                string libDataPath = Path.Combine(basePath, platformFolderName, library.LibFolderName, "AB");

                // 确保目录存在
                Directory.CreateDirectory(libDataPath);

                if (!Directory.Exists(libDataPath))
                {
                    Debug.LogWarning($"库 '{library.Name}' 的数据路径不存在: {libDataPath}");
                    result[library.Name] = new HashSet<string>();
                    continue;
                }

                // 扫描文件夹获取实际存在的AB包文件名（带哈希）
                string[] allFiles = Directory.GetFiles(libDataPath);
                HashSet<string> actualABNames = new HashSet<string>();
                foreach (var file in allFiles)
                {
                    string fileName = Path.GetFileName(file);
                    // 假设AB包文件没有扩展名，且文件名是带哈希的AB名
                    // 如果有特定扩展名或条件，可在此添加过滤
                    actualABNames.Add(fileName);
                }

                result[library.Name] = actualABNames;
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
                    : ESGlobalResSetting.Instance.Path_LocalBuildOnEditorPath_;

                // 数据统计完成后，移动AB包从Init到对应构建文件夹


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
                    string DataParentPath = basePath + "/" + ESResMaster.GetParentFolderNameByRuntimePlatform(ESGlobalResSetting.Instance.applyPlatform);

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
            CreateJsonData_ABHashAndDependence_PlaceAB();
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

                // 大规模文件迁移：将构建的AB文件移动到本地AB路径（已禁用）
                // string localOutputPath = globalSettings.Path_LocalABPath_ + "/" + platformFolderName;
                // try
                // {
                //     // 如果本地路径已存在，先删除或处理
                //     if (Directory.Exists(localOutputPath))
                //     {
                //         Directory.Delete(localOutputPath, true);
                //     }
                //     // 移动整个目录
                //     Directory.Move(outputPath, localOutputPath);
                //     Debug.Log($"AB文件迁移完成：{outputPath} -> {localOutputPath}");
                // }
                // catch (Exception e)
                // {
                //     string errorMsg = $"AB文件迁移失败：{e.Message}";
                //     Debug.LogError(errorMsg);
                //     EditorUtility.DisplayDialog("错误", errorMsg, "确定");
                // }
            }
            else
            {
                string errorMsg = $"创建输出目录失败：{outputPath}，消息：{directoryCreationResult.Message}";
                Debug.LogError(errorMsg);
                EditorUtility.DisplayDialog("错误", errorMsg, "确定");
            }
        }

        private static void CreateJsonData_ABHashAndDependence_PlaceAB()
        {
            // 为每个库生成AB键， AB Hash 和 依赖关系的 JSON 数据，通过重新检索 Library
            System.Text.StringBuilder summary = new System.Text.StringBuilder();
            summary.AppendLine("AB键， Hash 和依赖 JSON 生成总结：");

            // 初始化变更计数
            var changeCounts = new Dictionary<string, int>();
            string plat = ESResMaster.GetParentFolderNameByRuntimePlatform(ESGlobalResSetting.Instance.applyPlatform);

            try
            {
                var libraries = ESEditorSO.SOS.GetNewGroupOfType<ResLibrary>();
                var libraryABNames = CollectLibraryABNames();
                var actualLibraryABNames = GetLibraryActualABNames();


                var ABNames = AssetDatabase.GetAllAssetBundleNames();
                //先卸载加载
                AssetBundle.UnloadAllAssetBundles(unloadAllObjects: false);
                string initPath = Path.Combine(ESGlobalResSetting.Instance.Path_BuildInitialTarget, plat);


                AssetBundle MainBundle = AssetBundle.LoadFromFile(Path.Combine(ESGlobalResSetting.Instance.Path_BuildInitialTarget, plat, plat));
                //FEST
                AssetBundleManifest manifest = MainBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                //带Hash值的AB
                string[] AllABWithHash = manifest.GetAllAssetBundles();

                // 构建 pre 到 withHash 的映射，提高效率
                Dictionary<string, string> preToHash = new Dictionary<string, string>();
                foreach (var withHash in AllABWithHash)
                {
                    Debug.Log($"发现带哈希的AB包: {withHash}");
                    string pre = ESResMaster.PathAndNameTool_GetPreName(withHash);
                    preToHash[pre] = withHash;
                }



                foreach (var kvp in libraryABNames.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    string libName = kvp.Key;
                    HashSet<string> libABNames = kvp.Value;
                    HashSet<string> actualABNames = actualLibraryABNames.ContainsKey(libName) ? actualLibraryABNames[libName] : new HashSet<string>();

                    changeCounts.TryAdd(libName, 0);
                    var library = libraries.FirstOrDefault(l => l.Name == libName);
                    if (library == null || !library.ContainsBuild)
                        continue;

                    // 初始化该库的 ABMetadata
                    var abMetadata = new ESResJsonData_ABMetadata();
                    abMetadata.PreToHashes.Clear();
                    abMetadata.Dependences.Clear();
                    abMetadata.ABKeys.Clear();

                    // 保存该库的 ABMetadata JSON
                    // 根据 IsNet 选择路径
                    string basePath = library.IsNet
                        ? ESGlobalResSetting.Instance.Path_RemoteResOutBuildPath
                        : ESGlobalResSetting.Instance.Path_LocalBuildOnEditorPath_;
                    // 目标目录：basePath/{Platform}/ESResData/{LibFolderName}
                    string LibDataPath = basePath + "/" + ESResMaster.GetParentFolderNameByRuntimePlatform(ESGlobalResSetting.Instance.applyPlatform) + "/" + library.LibFolderName;
                    string LibABPath = LibDataPath + "/AB";

                    foreach (var abName in libABNames.OrderBy(n => n, StringComparer.Ordinal))
                    {
                        Debug.Log($"处理库 '{libName}' 的 AB '{abName}'");
                        foreach (var pretohash in preToHash)
                        {
                            Debug.Log($"预映射: {pretohash.Key} -> {pretohash.Value}");
                        }
                        if (preToHash.TryGetValue(abName, out var withHash))
                        {
                            Debug.Log($"库 '{libName}' 的 AB '{abName}' 对应带哈希版本: {withHash}");
                            abMetadata.PreToHashes.TryAdd(abName, withHash);

                            string[] abDepend = manifest.GetAllDependencies(withHash);
                            for (int i = 0; i < abDepend.Length; i++)
                            {
                                abDepend[i] = ESResMaster.PathAndNameTool_GetPreName(abDepend[i]);
                            }
                            // 包括所有依赖，即使跨库
                            if (abDepend.Length > 0)
                                abMetadata.Dependences.Add(abName, abDepend);

                            ESResKey key = new ESResKey() { LibName = libName, LibFolderName = library.LibFolderName, ABPreName = abName, SourceLoadType = ESResSourceLoadType.AssetBundle, ResName = withHash, TargetType = typeof(AssetBundle) };
                            abMetadata.ABKeys.Add(key);


                            // 检查Hash是否不同（文件名不同表示Hash不同）
                            bool isHashDifferent = !actualABNames.Contains(withHash);
                            if (isHashDifferent)
                            {
                                changeCounts[libName]++;

                                // 移动前删除旧的AB包
                                string oldWithHash = null;
                                foreach (var actual in actualABNames)
                                {
                                    if (ESResMaster.PathAndNameTool_GetPreName(actual) == abName)
                                    {
                                        oldWithHash = actual;
                                        break;
                                    }
                                }
                                if (!string.IsNullOrEmpty(oldWithHash))
                                {
                                    string oldPath = Path.Combine(LibABPath, oldWithHash);
                                    if (File.Exists(oldPath))
                                    {
                                        File.Delete(oldPath);
                                        summary.AppendLine($"删除旧AB包: {oldPath}");
                                        Debug.Log($"删除旧AB包: {oldPath}");
                                    }
                                }

                                // 移动新的AB包
                                string sourcePath = Path.Combine(initPath, withHash);
                                string targetPath = Path.Combine(LibABPath, withHash);
                                if (File.Exists(sourcePath))
                                {
                                    try
                                    {
                                        File.Move(sourcePath, targetPath);
                                        summary.AppendLine($"移动AB包: {sourcePath} -> {targetPath}");
                                        Debug.Log($"移动AB包: {sourcePath} -> {targetPath}");
                                    }
                                    catch (Exception e)
                                    {
                                        string exMsg = $"移动AB包失败: {sourcePath} -> {targetPath}, 异常: {e.Message}";
                                        summary.AppendLine($"异常: {exMsg}");
                                        Debug.LogError(exMsg);
                                    }
                                }
                                else
                                {
                                    summary.AppendLine($"警告: AB包文件不存在: {sourcePath}");
                                }
                            }
                            else
                            {
                                // Hash未变，删除Init中的文件
                                string sourcePath = Path.Combine(initPath, withHash);
                                if (File.Exists(sourcePath))
                                {
                                    File.Delete(sourcePath);
                                    summary.AppendLine($"Hash未变，删除Init文件: {sourcePath}");
                                    Debug.Log($"Hash未变，删除Init文件: {sourcePath}");
                                }
                            }
                        }
                        else
                        {
                            summary.AppendLine($"警告: 库 '{libName}' 的 AB '{abName}' 未找到对应的带哈希版本");
                        }

                    }



                    if (string.IsNullOrEmpty(basePath))
                    {
                        string errorMsg = $"库 '{libName}' 的基础路径为空，跳过 ABMetadata JSON 生成。IsNet: {library.IsNet}";
                        summary.AppendLine($"异常: {errorMsg}");
                        Debug.LogError(errorMsg);
                        continue;
                    }

                    try
                    {

                        // 创建库目录
                        var createResult = ESDesignUtility.SafeEditor.Quick_System_CreateDirectory(LibABPath);
                        if (!createResult.Success)
                        {
                            string errorMsg = $"创建目录失败：{LibABPath}，消息：{createResult.Message}";
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


                        //把变更计数给Library累计上，并且生成验证文件LinIndentity
                        library.ChangeCount += changeCounts[libName];

                        var libIdentity = new ESResJsonData_LibIndentity()
                        {
                            LibraryDisplayName = library.Name,
                            LibFolderName = library.LibFolderName,
                            LibraryDescription = library.Desc,
                            ChangeCount = library.ChangeCount
                        };

                        string libIdentity_Json = JsonConvert.SerializeObject(libIdentity);
                        string libIdentity_Path = LibDataPath + "/" + ESResMaster.JsonDataFileName.PathJsonFileName_ESLibIdentity;
                        File.WriteAllText(libIdentity_Path, libIdentity_Json);
                        summary.AppendLine($"成功生成 LibIndentity JSON：{libIdentity_Path}");




                    }
                    catch (Exception e)
                    {
                        string exMsg = $"生成库 '{libName}' 的 ABMetadata JSON 时发生异常：{e.Message}";
                        summary.AppendLine($"异常: {exMsg}");
                        Debug.LogError(exMsg);
                    }
                }

                foreach (var kvp in libraryABNames.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    string libName = kvp.Key;
                    HashSet<string> libABNames = kvp.Value;
                    var library = libraries.FirstOrDefault(l => l.Name == libName);
                    if (library == null || !library.ContainsBuild)
                        continue;

                    string basePath = library.IsNet
                        ? ESGlobalResSetting.Instance.Path_RemoteResOutBuildPath
                        : ESGlobalResSetting.Instance.Path_LocalBuildOnEditorPath_;

                    if (string.IsNullOrEmpty(basePath))
                        continue;

                    string LibDataPath = Path.Combine(basePath, plat, library.LibFolderName);
                    string LibABPath = LibDataPath + "/AB";

                    foreach (var abName in libABNames.OrderBy(n => n, StringComparer.Ordinal))
                    {
                        if (preToHash.TryGetValue(abName, out var withHash))
                        {
                            string sourcePath = Path.Combine(initPath, withHash);
                            string targetPath = Path.Combine(LibABPath, withHash);
                            if (File.Exists(sourcePath))
                            {
                                try
                                {
                                    File.Move(sourcePath, targetPath);
                                    summary.AppendLine($"移动AB包: {sourcePath} -> {targetPath}");
                                    Debug.Log($"移动AB包: {sourcePath} -> {targetPath}");
                                }
                                catch (Exception e)
                                {
                                    string exMsg = $"移动AB包失败: {sourcePath} -> {targetPath}, 异常: {e.Message}";
                                    summary.AppendLine($"异常: {exMsg}");
                                    Debug.LogError(exMsg);
                                }
                            }
                            else
                            {
                                summary.AppendLine($"警告: AB包文件不存在: {sourcePath}");
                            }
                        }
                    }
                }

                // 移动Manifest到远端
                string remoteManifestDir = Path.Combine(ESGlobalResSetting.Instance.Path_RemoteResOutBuildPath, plat);
                Directory.CreateDirectory(remoteManifestDir);
                string remoteManifestPath = Path.Combine(remoteManifestDir, plat);
                string manifestFilePath = Path.Combine(initPath, plat);
                if (File.Exists(manifestFilePath))
                {
                    try
                    {
                        if (File.Exists(remoteManifestPath))
                        {
                            File.Delete(remoteManifestPath);
                        }
                        File.Move(manifestFilePath, remoteManifestPath);
                        summary.AppendLine($"移动Manifest到远端: {manifestFilePath} -> {remoteManifestPath}");
                        Debug.Log($"移动Manifest到远端: {manifestFilePath} -> {remoteManifestPath}");
                    }
                    catch (Exception e)
                    {
                        string exMsg = $"移动Manifest失败: {e.Message}";
                        summary.AppendLine($"异常: {exMsg}");
                        Debug.LogError(exMsg);
                    }
                }
                else
                {
                    summary.AppendLine($"警告: Manifest文件不存在，无法移动: {manifestFilePath}");
                }

                //生成GameIdentity文件
                {

                    var gameIdentity = new ESResJsonData_GameIdentity
                    {
                        BuildTimestamp = DateTime.Now.ToString("o"), // ISO 8601格式
                        Version = ESGlobalResSetting.Instance.Version,
                        RequiredLibrariesFolders = libraries.Where(l => l.ContainsBuild && l.IsMainInClude).Select(l => new RequiredLibrary { FolderName = l.LibFolderName, IsRemote = l.IsNet }).ToList()
                    };

                    // 在本地和远程都生成这个Indentity文件，顺便为全部的Conumsers生成单独的，并且放在ExpandConsumers文件夹下
                    string[] basePaths = { ESGlobalResSetting.Instance.Path_LocalBuildOnEditorPath_, ESGlobalResSetting.Instance.Path_RemoteResOutBuildPath };
                    var consumers = ESEditorSO.SOS.GetNewGroupOfType<ResLibConsumer>();

                    foreach (var basePath in basePaths)
                    {
                        string gameIdentityPath = Path.Combine(basePath, plat, ESResMaster.JsonDataFileName.PathJsonFileName_ESGameIdentity);

                        // 确保目录存在
                        Directory.CreateDirectory(Path.GetDirectoryName(gameIdentityPath));

                        // 序列化并写入GameIdentity
                        string json = JsonConvert.SerializeObject(gameIdentity);
                        File.WriteAllText(gameIdentityPath, json);
                        summary.AppendLine($"成功生成 GameIdentity JSON：{gameIdentityPath}");
                        Debug.Log($"成功生成 GameIdentity JSON：{gameIdentityPath}");

                        // 生成Consumers的Identity文件
                        string expandConsumersPath = Path.Combine(basePath, plat, ESGlobalResSetting.ResConsumersExpandParentFolderName);
                        Directory.CreateDirectory(expandConsumersPath);


                        if (consumers == null || consumers.Count == 0)
                        {
                            summary.AppendLine($"警告: 未找到任何 ResLibConsumer，跳过生成 ConsumerIdentity JSON");
                            Debug.LogWarning("未找到任何 ResLibConsumer，跳过生成 ConsumerIdentity JSON");
                            continue;
                        }
                        foreach (var consumer in consumers)
                        {
                            var consumerIdentity = new ESResJsonData_ConsumerIdentity
                            {
                                ConsumerDisplayName = consumer.Name,
                                Version = consumer.Version,
                                ConsumerDescription = consumer.Desc,
                                IncludedLibrariesFolders = consumer.ConsumerLibFolders.Select(lib => new RequiredLibrary { FolderName = lib.LibFolderName, IsRemote = lib.IsNet }).ToList()
                            };

                            string consumerJson = JsonConvert.SerializeObject(consumerIdentity);
                            string consumerPath = Path.Combine(expandConsumersPath, consumer.Name + ".json");
                            File.WriteAllText(consumerPath, consumerJson);
                            summary.AppendLine($"成功生成 ConsumerIdentity JSON：{consumerPath}");
                            Debug.Log($"成功生成 ConsumerIdentity JSON：{consumerPath}");
                        }
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

            // 输出总结并询问是否打开远程构建文件夹
            string finalSummary = summary.ToString();
            Debug.Log(finalSummary);
            int result = EditorUtility.DisplayDialogComplex("AB 生成总结", finalSummary + "\n\n是否打开远程构建文件夹？", "打开远端构建文件夹", "否", "关闭");
            if (result == 0) // 是
            {
                string remotePath = Path.Combine(ESGlobalResSetting.Instance.Path_RemoteResOutBuildPath, plat);
                if (Directory.Exists(remotePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                    {
                        FileName = remotePath,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
                else
                {
                    EditorUtility.DisplayDialog("错误", $"远程构建文件夹不存在: {remotePath}", "确定");
                }
            }
        }


        #endregion

        #region 循环依赖处理

        private sealed class CircularDependencyReport
        {
            public int TotalNodes;
            public int TotalEdges;
            public int SccCount;
            public int CycleGroupCount;
            public int AssetsReassigned;
            public List<List<string>> CycleGroups = new List<List<string>>();
            public List<MergedCycleGroup> MergedGroups = new List<MergedCycleGroup>();
        }

        private sealed class MergedCycleGroup
        {
            public string SharedABName;
            public List<string> Assets = new List<string>();
        }

        /// <summary>
        /// 将循环依赖报告汇总到Summary中
        /// </summary>
        private static void AppendCircularDependencyReport(System.Text.StringBuilder summary, CircularDependencyReport report)
        {
            summary.AppendLine("循环依赖检测汇总：");
            summary.AppendLine($"- 节点数: {report.TotalNodes}");
            summary.AppendLine($"- 边数: {report.TotalEdges}");
            summary.AppendLine($"- 强连通分量数: {report.SccCount}");
            summary.AppendLine($"- 循环组数(>1): {report.CycleGroupCount}");
            summary.AppendLine($"- 重新分配AB的资源数: {report.AssetsReassigned}");
            if (report.CycleGroups.Count > 0)
            {
                int idx = 0;
                foreach (var group in report.CycleGroups)
                {
                    string joined = string.Join("; ", group);
                    summary.AppendLine($"  * 循环组[{idx++}] 资源数: {group.Count} -> {joined}");
                }
            }
            if (report.MergedGroups.Count > 0)
            {
                summary.AppendLine("循环依赖合并明细(资源路径 -> 共享AB包名)：");
                foreach (var group in report.MergedGroups)
                {
                    string joined = string.Join("; ", group.Assets);
                    summary.AppendLine($"  * {group.SharedABName} <= {joined}");
                }
            }
        }

        /// <summary>
        /// 重新应用更新后的AB名（用于循环依赖处理后）
        /// </summary>
        private static void ApplyUpdatedABNames(System.Text.StringBuilder summary)
        {
            // 先同步到ESResKey（避免ABName更新后与AssetKeys不一致）
            SyncUpdatedABNamesToResKeys();

            foreach (var kvp in AssetToABName)
            {
                string assetPath = kvp.Key;
                string newABName = kvp.Value;

                var assetImporter = AssetImporter.GetAtPath(assetPath);
                if (assetImporter != null && assetImporter.assetBundleName != newABName)
                {
                    assetImporter.assetBundleName = newABName;
                    summary.AppendLine($"更新AB名: {assetPath} -> {newABName}");
                }
            }
        }

        private static void SyncUpdatedABNamesToResKeys()
        {
            if (ESResMaster.TempResLibrarys == null || ESResMaster.TempResLibrarys.Count == 0)
                return;

            foreach (var tempLib in ESResMaster.TempResLibrarys.Values)
            {
                if (tempLib?.ESResData_AssetKeys?.AssetKeys == null)
                    continue;

                foreach (var key in tempLib.ESResData_AssetKeys.AssetKeys)
                {
                    if (string.IsNullOrEmpty(key?.Path))
                        continue;

                    if (AssetToABName.TryGetValue(key.Path, out var newABPreName))
                    {
                        key.ABPreName = newABPreName;
                    }
                }
            }
        }

        /// <summary>
        /// 处理资源依赖中的循环依赖，将循环中的资源分配到共享AB包，并输出报告
        /// </summary>
        /// <param name="assetToDependencies">资源到其依赖资源的映射</param>
        /// <param name="assetToABName">资源到AB包名的映射（输出）</param>
        private static CircularDependencyReport HandleCircularDependencies(Dictionary<string, List<string>> assetToDependencies, Dictionary<string, string> assetToABName)
        {
            var report = new CircularDependencyReport();
            if (assetToDependencies == null || assetToDependencies.Count == 0)
                return report;

            // 统计节点与边
            report.TotalNodes = assetToDependencies.Count;
            int edgeCount = 0;
            foreach (var kvp in assetToDependencies)
            {
                edgeCount += kvp.Value != null ? kvp.Value.Count : 0;
            }
            report.TotalEdges = edgeCount;

            // Tarjan SCC
            var indexMap = new Dictionary<string, int>();
            var lowLink = new Dictionary<string, int>();
            var onStack = new HashSet<string>();
            var stack = new Stack<string>();
            int index = 0;
            var sccList = new List<List<string>>();

            foreach (var node in assetToDependencies.Keys.OrderBy(n => n, StringComparer.Ordinal))
            {
                if (!indexMap.ContainsKey(node))
                {
                    TarjanDfs(node, assetToDependencies, ref index, indexMap, lowLink, onStack, stack, sccList);
                }
            }

            report.SccCount = sccList.Count;

            int sharedABIndex = 0;
            foreach (var scc in sccList)
            {
                if (scc.Count <= 1)
                    continue;

                report.CycleGroupCount++;
                report.CycleGroups.Add(new List<string>(scc));

                string sharedABName = $"sharedab_cycle_{sharedABIndex++}";
                var mergedGroup = new MergedCycleGroup { SharedABName = sharedABName };
                foreach (string asset in scc)
                {
                    assetToABName[asset] = sharedABName;
                    report.AssetsReassigned++;
                    mergedGroup.Assets.Add(asset);
                }
                report.MergedGroups.Add(mergedGroup);
            }

            return report;
        }

        private static void TarjanDfs(
            string node,
            Dictionary<string, List<string>> graph,
            ref int index,
            Dictionary<string, int> indexMap,
            Dictionary<string, int> lowLink,
            HashSet<string> onStack,
            Stack<string> stack,
            List<List<string>> sccList)
        {
            indexMap[node] = index;
            lowLink[node] = index;
            index++;
            stack.Push(node);
            onStack.Add(node);

            if (graph.TryGetValue(node, out var deps) && deps != null)
            {
                foreach (var dep in deps.OrderBy(d => d, StringComparer.Ordinal))
                {
                    // 跳过不在图中的依赖，保证健壮性
                    if (!graph.ContainsKey(dep))
                        continue;

                    if (!indexMap.ContainsKey(dep))
                    {
                        TarjanDfs(dep, graph, ref index, indexMap, lowLink, onStack, stack, sccList);
                        lowLink[node] = Math.Min(lowLink[node], lowLink[dep]);
                    }
                    else if (onStack.Contains(dep))
                    {
                        lowLink[node] = Math.Min(lowLink[node], indexMap[dep]);
                    }
                }
            }

            // 根节点，弹出一个SCC
            if (lowLink[node] == indexMap[node])
            {
                var scc = new List<string>();
                string w;
                do
                {
                    w = stack.Pop();
                    onStack.Remove(w);
                    scc.Add(w);
                } while (w != node);

                sccList.Add(scc);
            }
        }

        #endregion

    }
}
