using ES;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using UnityEngine.SocialPlatforms;
namespace ES
{

    public partial class ESResMaster
    {
        #region 全局下载状态
        [NonSerialized]
        public ESResGlobalDownloadState GlobalDownloadState = ESResGlobalDownloadState.None;
        [TabGroup("AB包下载测试")]
        [LabelText("AB包状态"), ShowInInspector]
        public static Dictionary<string, int> ABStates = new Dictionary<string, int>();//-1 无 0更新 1完美
        [TabGroup("AB包下载测试")]
        [LabelText("尝试下载单个")]
        [ValueDropdown("TryDownloadABNames")]
        public string TryDownloadABSingle;

        private string[] TryDownloadABNames => ABStates.Keys.ToArray();

        private const int DefaultRequestTimeoutSeconds = 30;
        private const int DefaultMaxRetryCount = 3;
        private const float DefaultRetryDelaySeconds = 1.5f;
        private const int DefaultMaxConcurrentAbDownloads = 4;

        private static HashSet<string> _injectedLibs = new HashSet<string>();
        #endregion
        public void GameInit_ResCompareAndDownload()
        {
            // 路径已在Awake中初始化，这里直接开始下载流程

            //全部卸载
            AssetBundle.UnloadAllAssetBundles(unloadAllObjects: false);
            _injectedLibs.Clear();
            GlobalAssetKeys.Clear();
            GlobalABKeys.Clear();
            GlobalABPreToHashes.Clear();
            GlobalABHashToPres.Clear();
            GlobalDependencies.Clear();

            var callback = new ESCallback<string>();
            callback.OnSuccess = (message) => Debug.Log($"初始化下载完成: {message}");
            callback.OnError = (error) => Debug.LogError($"初始化下载失败: {error}");

            StartCoroutine(InitTryDownload(callback));
        }

        #region 游戏初始化下载
        private IEnumerator InitTryDownload(ESCallback<string> callback)
        {
            callback?.UpdateProgress(0f, "开始初始化下载流程");

            #region 下载并解析GameIdentity
            // 首先检查本地是否已有GameIdentity
            ESResJsonData_GameIdentity oldGameIdentity = null;
            if (File.Exists(DefaultPaths.LocalGameIdentityPath))
            {
                try
                {
                    string oldGameIdentityJson = File.ReadAllText(DefaultPaths.LocalGameIdentityPath);
                    oldGameIdentity = JsonConvert.DeserializeObject<ESResJsonData_GameIdentity>(oldGameIdentityJson);
                    callback?.UpdateProgress(0.05f, "读取本地GameIdentity缓存");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"读取本地GameIdentity失败: {ex.Message}");
                }
            }

            // 下载远程GameIdentity
            callback?.UpdateProgress(0.1f, "下载远程GameIdentity");
            string remoteGameIdentityJson = null;
            bool gameIdentityDownloadFailed = false;
            yield return DownloadTextWithRetries(
                DefaultPaths.NetGameIdentityPath,
                text => remoteGameIdentityJson = text,
                error =>
                {
                    gameIdentityDownloadFailed = true;
                    callback?.Error($"下载GameIdentity失败: {error}");
                });

            if (gameIdentityDownloadFailed || string.IsNullOrEmpty(remoteGameIdentityJson))
            {
                yield break;
            }

            // 解析远程GameIdentity
            callback?.UpdateProgress(0.15f, "解析远程GameIdentity");
            var remoteGameIdentity = JsonConvert.DeserializeObject<ESResJsonData_GameIdentity>(remoteGameIdentityJson);

            // 保存远程GameIdentity到本地
            Directory.CreateDirectory(Path.GetDirectoryName(DefaultPaths.LocalGameIdentityPath));
            File.WriteAllText(DefaultPaths.LocalGameIdentityPath, remoteGameIdentityJson);

            // 比较版本和构建时间，决定是否需要下载
            bool needDownload = NeedDownloadGameIdentity(oldGameIdentity, remoteGameIdentity);

            if (!needDownload)
            {
                // 即使不需要下载，也必须加载每个库的JSON信息
                foreach (var lib in remoteGameIdentity.RequiredLibrariesFolders)
                {
                    EnsureLibraryMetadataLoaded(lib.FolderName, lib.IsRemote);
                }

                GlobalDownloadState = ESResGlobalDownloadState.AllReady;
                callback?.Success("所有资源已准备就绪，无需下载");
                yield break;
            }

            // 使用远程GameIdentity继续流程
            var gameIdentity = remoteGameIdentity;
            #endregion

            #region 下载必需库
            yield return StartCoroutine(DownloadLibrariesAsync(gameIdentity.RequiredLibrariesFolders, callback));
            #endregion
        }
        #endregion

        #region 下载库列表方法
        /// <summary>
        /// 异步下载库列表，支持远程库的版本比较和本地库的直接使用
        /// </summary>
        /// <param name="requiredLibs">必需的库列表</param>
        /// <param name="callback">下载回调</param>
        private IEnumerator DownloadLibrariesAsync(List<RequiredLibrary> requiredLibs, ESCallback<string> callback)
        {
            GlobalDownloadState = ESResGlobalDownloadState.Comparing;

            var libsToDownload = new List<RequiredLibrary>();
            var remoteLibIdentities = new Dictionary<string, ESResJsonData_LibIndentity>();

            if (requiredLibs == null || requiredLibs.Count == 0)
            {
                GlobalDownloadState = ESResGlobalDownloadState.AllReady;
                callback?.Success("无必需库需要处理");
                yield break;
            }

            foreach (var lib in requiredLibs)
            {
                if (lib == null || string.IsNullOrEmpty(lib.FolderName))
                {
                    Debug.LogWarning("跳过无效的 RequiredLibrary 配置");
                    continue;
                }

                if (lib.IsRemote)
                {
                    string localLibIdentityPath = DefaultPaths.GetLocalLibIdentityPath(lib.FolderName);
                    ESResJsonData_LibIndentity localLibIdentity = null;
                    if (File.Exists(localLibIdentityPath))
                    {
                        try
                        {
                            string localJson = File.ReadAllText(localLibIdentityPath);
                            localLibIdentity = JsonConvert.DeserializeObject<ESResJsonData_LibIndentity>(localJson);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"读取本地LibIdentity失败 {lib.FolderName}: {ex.Message}");
                        }
                    }

                    ESResJsonData_LibIndentity remoteLibIdentity = null;
                    bool identityDownloadFailed = false;
                    yield return DownloadTextWithRetries(
                        DefaultPaths.GetNetLibIdentityPath(lib.FolderName),
                        text =>
                        {
                            try
                            {
                                remoteLibIdentity = JsonConvert.DeserializeObject<ESResJsonData_LibIndentity>(text);
                                if (remoteLibIdentity == null)
                                {
                                    identityDownloadFailed = true;
                                    Debug.LogError($"解析远程LibIdentity失败 {lib.FolderName}");
                                    return;
                                }
                                Directory.CreateDirectory(Path.GetDirectoryName(localLibIdentityPath));
                                File.WriteAllText(localLibIdentityPath, text);
                                remoteLibIdentities[lib.FolderName] = remoteLibIdentity;
                            }
                            catch (Exception ex)
                            {
                                identityDownloadFailed = true;
                                Debug.LogError($"处理远程LibIdentity失败 {lib.FolderName}: {ex.Message}");
                            }
                        },
                        error =>
                        {
                            identityDownloadFailed = true;
                            Debug.LogError($"下载库 {lib.FolderName} LibIdentity失败: {error}");
                        });

                    if (identityDownloadFailed || remoteLibIdentity == null)
                    {
                        callback?.Error($"库 {lib.FolderName} LibIdentity 下载失败");
                        continue;
                    }

                    bool libNeedDownload = NeedDownloadLibrary(lib.FolderName, remoteLibIdentity, localLibIdentity);
                    if (libNeedDownload)
                    {
                        libsToDownload.Add(lib);
                    }
                    else
                    {
                        if (TryRegisterLibraryFromLocal(lib.FolderName, localLibIdentityPath, true, remoteLibIdentity, out var registeredRemoteLib))
                        {
                            EnsureLibraryMetadataLoaded(lib.FolderName, true);
                            DownloadedLibraries[lib.FolderName] = registeredRemoteLib;
                        }
                    }
                }
                else
                {
                    string libIdentityPath = DefaultPaths.GetLocalBuildLibIdentityPath(lib.FolderName);
                    if (TryRegisterLibraryFromLocal(lib.FolderName, libIdentityPath, false, null, out var registeredLocalLib))
                    {
                        EnsureLibraryMetadataLoaded(lib.FolderName, false);
                        DownloadedLibraries[lib.FolderName] = registeredLocalLib;
                    }
                    else
                    {
                        Debug.LogWarning($"本地库 {lib.FolderName} 不存在或LibIdentity无效");
                    }
                }
            }

            if (libsToDownload.Count == 0)
            {
                GlobalDownloadState = ESResGlobalDownloadState.AllReady;
                callback?.UpdateProgress(1f, "所有库均已是最新版本");
                callback?.Success("无需下载任何库");
                yield break;
            }

            callback?.UpdateProgress(0.3f, $"开始下载 {libsToDownload.Count} 个远程库");
            GlobalDownloadState = ESResGlobalDownloadState.Downloading;

            int completedLibs = 0;
            foreach (var lib in libsToDownload)
            {
                var libCallback = ESCallback<DownloadedLibraryData>.Pool.GetInPool();
                libCallback.OnProgress = (progress, msg) =>
                {
                    float baseProgress = 0.3f + progress * 0.6f;
                    callback?.UpdateProgress(baseProgress, $"库 {lib.FolderName}: {msg}");
                };
                libCallback.OnSuccess = downloadedLib =>
                {
                    DownloadedLibraries[lib.FolderName] = downloadedLib;
                    completedLibs++;
                    float overallProgress = 0.3f + (0.7f * completedLibs / Mathf.Max(libsToDownload.Count, 1));
                    callback?.UpdateProgress(overallProgress, $"已完成 {completedLibs}/{libsToDownload.Count} 个库下载");
                    libCallback.TryAutoPushedToPool();
                };
                libCallback.OnError = error =>
                {
                    completedLibs++;
                    float overallProgress = 0.3f + (0.7f * completedLibs / Mathf.Max(libsToDownload.Count, 1));
                    callback?.UpdateProgress(overallProgress, $"库 {lib.FolderName} 下载失败: {error}");
                    libCallback.TryAutoPushedToPool();
                };
                libCallback.OnComplete = () => libCallback.TryAutoPushedToPool();

                ESResJsonData_LibIndentity remoteIdentity = null;
                remoteLibIdentities.TryGetValue(lib.FolderName, out remoteIdentity);
                yield return StartCoroutine(DownloadLibraryAsync(lib, remoteIdentity, libCallback));
            }

            GlobalDownloadState = ESResGlobalDownloadState.AllReady;
            callback?.UpdateProgress(1.0f, "初始化下载流程完成");
            callback?.Success($"成功初始化 {libsToDownload.Count} 个库的下载流程");
        }
        #endregion

        #region 单个库下载方法
        /// <summary>
        /// 异步下载单个库的所有资源
        /// </summary>
        /// <param name="lib">库信息</param>
        /// <param name="callback">下载回调</param>
        private IEnumerator DownloadLibraryAsync(RequiredLibrary lib, ESResJsonData_LibIndentity remoteLibIdentity, ESCallback<DownloadedLibraryData> callback)
        {
            callback?.UpdateProgress(0f, "开始下载库资源");

            string libNetPath = DefaultPaths.GetNetLibBasePath(lib.FolderName);
            string libLocalPath = DefaultPaths.GetLocalLibBasePath(lib.FolderName);

            Directory.CreateDirectory(libLocalPath);


            // 步骤1: 下载AssetKeys.json
            callback?.UpdateProgress(0.1f, "下载AssetKeys.json");
            string assetKeysLocal = DefaultPaths.GetLocalAssetKeysPath(lib.FolderName);
            bool assetKeysFailed = false;
            yield return DownloadFileWithRetries(
                DefaultPaths.GetNetAssetKeysPath(lib.FolderName),
                assetKeysLocal,
                () =>
                {
                    InjectAssetKeysData(lib.FolderName, assetKeysLocal);
                    callback?.UpdateProgress(0.3f, "AssetKeys.json下载完成");
                },
                error =>
                {
                    assetKeysFailed = true;
                    callback?.Error($"下载AssetKeys失败: {error}");
                });

            if (assetKeysFailed)
            {
                yield break;
            }

            // 步骤2: 下载ABMetadata.json
            callback?.UpdateProgress(0.4f, "下载ABMetadata.json");
            string abMetadataLocal = DefaultPaths.GetLocalABMetadataPath(lib.FolderName);
            ESResJsonData_ABMetadata abMetadata = null;
            bool abMetadataFailed = false;
            yield return DownloadFileWithRetries(
                DefaultPaths.GetNetABMetadataPath(lib.FolderName),
                abMetadataLocal,
                () =>
                {
                    abMetadata = InjectABMetadataData(lib.FolderName, abMetadataLocal);
                    callback?.UpdateProgress(0.6f, "ABMetadata.json下载完成");
                    if (abMetadata == null)
                    {
                        abMetadataFailed = true;
                        callback?.Error("ABMetadata数据注入失败");
                    }
                },
                error =>
                {
                    abMetadataFailed = true;
                    callback?.Error($"下载ABMetadata失败: {error}");
                });

            if (abMetadataFailed || abMetadata == null)
            {
                yield break;
            }

            // 步骤3: 解析ABMetadata并下载AB包
            callback?.UpdateProgress(0.7f, "解析AB包列表");
            string abLocalPath = DefaultPaths.GetLocalABBasePath(lib.FolderName);
            Directory.CreateDirectory(abLocalPath);

            var localABFiles = Directory.Exists(abLocalPath)
                ? Directory.GetFiles(abLocalPath, "*").Where(f => Path.GetExtension(f) != ".json").Select(Path.GetFileName).ToHashSet()
                : new HashSet<string>();

            var abToDownload = new List<string>();
            var abToDelete = new List<string>();

            foreach (var kvp in abMetadata.PreToHashes)
            {
                string preName = kvp.Key;
                string hashedName = kvp.Value;

                if (!localABFiles.Contains(hashedName))
                {
                    abToDownload.Add(hashedName);

                    foreach (var localFile in localABFiles)
                    {
                        if (!string.IsNullOrEmpty(localFile) && localFile.StartsWith(preName + "_", StringComparison.OrdinalIgnoreCase))
                        {
                            abToDelete.Add(localFile);
                            break;
                        }
                    }
                }
            }

            callback?.UpdateProgress(0.82f, $"需要下载 {abToDownload.Count} 个AB包，删除 {abToDelete.Count} 个旧包");

            int downloadedCount = 0;
            foreach (var hashedName in abToDownload)
            {
                bool abDownloadFailed = false;
                string abLocalFilePath = Path.Combine(abLocalPath, hashedName);
                yield return DownloadFileWithRetries(
                    libNetPath + "/" + hashedName,
                    abLocalFilePath,
                    () =>
                    {
                        downloadedCount++;
                        float progress = 0.82f + (0.15f * downloadedCount / Mathf.Max(abToDownload.Count, 1));
                        callback?.UpdateProgress(progress, $"已下载 {hashedName}");
                    },
                    error =>
                    {
                        abDownloadFailed = true;
                        callback?.Error($"下载AB包失败 {hashedName}: {error}");
                    });

                if (abDownloadFailed)
                {
                    yield break;
                }
            }

            callback?.UpdateProgress(0.97f, "清理旧AB包");
            foreach (var oldHashedName in abToDelete)
            {
                string localABPath = Path.Combine(abLocalPath, oldHashedName);
                if (File.Exists(localABPath))
                {
                    try
                    {
                        File.Delete(localABPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"删除旧AB包失败 {oldHashedName}: {ex.Message}");
                    }
                }
            }

            callback?.UpdateProgress(0.9f, "AB包下载完成");

            long totalSize = CalculateABTotalSize(abLocalPath);

            callback?.UpdateProgress(1.0f, "创建库数据");
            if (TryRegisterLibraryFromLocal(lib.FolderName, DefaultPaths.GetLocalLibIdentityPath(lib.FolderName), true, remoteLibIdentity, out var downloadedLib))
            {
                downloadedLib.TotalSize = totalSize;
                downloadedLib.RemotePath = EnsureTrailingSlash(libNetPath);
                downloadedLib.LocalPath = EnsureTrailingSlash(libLocalPath);
                _injectedLibs.Add(lib.FolderName);
                callback?.Success(downloadedLib);
            }
            else
            {
                callback?.Error("无法读取库身份信息");
            }
        }

        #endregion

        #region 注入辅助
        private void EnsureLibraryMetadataLoaded(string libFolderName, bool isRemote)
        {
            if (_injectedLibs.Contains(libFolderName))
            {
                return;
            }

            string assetKeysLocal = isRemote ? DefaultPaths.GetLocalAssetKeysPath(libFolderName) : DefaultPaths.GetLocalBuildAssetKeysPath(libFolderName);
            string abMetadataLocal = isRemote ? DefaultPaths.GetLocalABMetadataPath(libFolderName) : DefaultPaths.GetLocalBuildABMetadataPath(libFolderName);

            // 注意：这里使用 File.ReadAllText，这在 Android 上直接读取 StreamingAssets (jar:file://) 可能会失败
            // 如果 isRemote 为 false 且在 Android 上，可能需要改用 UnityWebRequest。
            // 但目前的架构中，ResMaster.PathAndName.cs 假定了本地构建路径是 StreamingAssets。
            // 为了安全起见，如果在 Android 且不是 Remote，我们应该使用 DownloadTextWithRetries 或类似的机制，但这里是同步调用。
            // 暂时假定构建流程会将内置库解包到 Persistent 或者使用特定的加载方式，或者当前仅针对 PC/Editor 调试。
            // 更稳健的做法是把 Inject 改成支持 content string，然后外部负责读取。

            if (File.Exists(assetKeysLocal))
            {
                InjectAssetKeysData(libFolderName, assetKeysLocal);
            }
            
            if (File.Exists(abMetadataLocal))
            {
                InjectABMetadataData(libFolderName, abMetadataLocal);
            }

            _injectedLibs.Add(libFolderName);
        }
        #endregion

        #region 扩展包下载方法
        /// <summary>
        /// 下载扩展包（Consumer）的库
        /// </summary>
        /// <param name="consumerName">Consumer名称</param>
        public IEnumerator ExtensionDownload(string consumerName)
        {
            // 首先尝试下载远程ConsumerIdentity
            string netConsumerPath = DefaultPaths.NetConsumerBasePath + "/" + consumerName + ".json";
            ESResJsonData_ConsumerIdentity remoteConsumerIdentity = null;

            var reqRemote = UnityWebRequest.Get(netConsumerPath);
            reqRemote.SendWebRequest();

            while (!reqRemote.isDone)
            {
                yield return null;
            }

            if (reqRemote.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string remoteJson = reqRemote.downloadHandler.text;
                    remoteConsumerIdentity = JsonConvert.DeserializeObject<ESResJsonData_ConsumerIdentity>(remoteJson);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"解析远程ConsumerIdentity失败 {consumerName}: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"下载远程ConsumerIdentity失败 {consumerName}: {reqRemote.error}");
            }

            // 检查本地ConsumerIdentity
            string localConsumerPath = Path.Combine(DefaultPaths.LocalConsumerBasePath, consumerName + ".json");
            ESResJsonData_ConsumerIdentity localConsumerIdentity = null;

            if (File.Exists(localConsumerPath))
            {
                try
                {
                    string localJson = File.ReadAllText(localConsumerPath);
                    localConsumerIdentity = JsonConvert.DeserializeObject<ESResJsonData_ConsumerIdentity>(localJson);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"解析本地ConsumerIdentity失败 {consumerName}: {ex.Message}");
                }
            }

            // 比较版本，决定是否需要下载
            bool needDownload = false;

            if (remoteConsumerIdentity != null)
            {
                if (localConsumerIdentity == null || remoteConsumerIdentity.Version != localConsumerIdentity.Version)
                {
                    // 保存远程到本地
                    Directory.CreateDirectory(Path.GetDirectoryName(localConsumerPath));
                    File.WriteAllText(localConsumerPath, reqRemote.downloadHandler.text);
                    localConsumerIdentity = remoteConsumerIdentity;
                    needDownload = true;
                    Debug.Log($"ConsumerIdentity {consumerName} 需要更新: 远程版本={remoteConsumerIdentity.Version}, 本地版本={localConsumerIdentity?.Version ?? "无"}");
                }
                else
                {
                    Debug.Log($"ConsumerIdentity {consumerName} 无需更新: 版本匹配 ({remoteConsumerIdentity.Version})");
                }
            }
            else if (localConsumerIdentity != null)
            {
                // 使用本地
                Debug.Log($"使用本地ConsumerIdentity {consumerName}");
            }
            else
            {
                Debug.LogError($"ConsumerIdentity {consumerName} 远程和本地都不存在");
                yield break;
            }

            if (localConsumerIdentity == null || localConsumerIdentity.IncludedLibrariesFolders == null)
            {
                Debug.LogError($"ConsumerIdentity解析失败或无库列表 {consumerName}");
                yield break;
            }

            if (!needDownload)
            {
                Debug.Log($"扩展包 {consumerName} 无需下载");
                yield break;
            }

            // 下载Consumer的库
            var callback = new ESCallback<string>();
            callback.OnSuccess = (message) => Debug.Log($"扩展包 {consumerName} 下载完成: {message}");
            callback.OnError = (error) => Debug.LogError($"扩展包 {consumerName} 下载失败: {error}");

            yield return StartCoroutine(DownloadLibrariesAsync(localConsumerIdentity.IncludedLibrariesFolders, callback));
        }
        #endregion

        #region JSON文件下载方法
        /// <summary>
        /// 从网络文件夹下载所有JSON文件到本地文件夹，并生成验证记录
        /// 支持通过索引文件指定文件列表，或尝试解析服务器目录列表
        /// </summary>
        /// <param name="netFolderPath">网络文件夹路径</param>
        /// <param name="localFolderPath">本地文件夹路径</param>
        /// <param name="indexUrl">索引文件URL（可选），索引文件应为JSON数组，包含文件名列表</param>
        /// <param name="tryParseDirectoryListing">如果为true且无索引，尝试解析服务器目录列表（需要服务器支持目录索引）</param>
        /// <param name="onComplete">完成回调</param>
        private IEnumerator DownloadAllJsonFilesInFolder(string netFolderPath, string localFolderPath, string indexUrl = null, bool tryParseDirectoryListing = false, Action onComplete = null)
        {
            string[] jsonFileNames = null;

            if (!string.IsNullOrEmpty(indexUrl))
            {
                // 下载索引文件获取文件列表
                var indexReq = UnityWebRequest.Get(indexUrl);
                indexReq.SendWebRequest();

                while (!indexReq.isDone)
                {
                    yield return null;
                }

                if (indexReq.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string indexJson = indexReq.downloadHandler.text;
                        jsonFileNames = JsonConvert.DeserializeObject<string[]>(indexJson);
                        Debug.Log($"从索引文件获取到 {jsonFileNames.Length} 个文件");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"解析索引文件失败: {ex.Message}");
                        onComplete?.Invoke();
                        yield break;
                    }
                }
                else
                {
                    Debug.LogError($"下载索引文件失败: {indexReq.error}");
                    onComplete?.Invoke();
                    yield break;
                }
            }
            else if (tryParseDirectoryListing)
            {
                // 尝试解析服务器目录列表
                var dirReq = UnityWebRequest.Get(netFolderPath);
                dirReq.SendWebRequest();

                while (!dirReq.isDone)
                {
                    yield return null;
                }

                if (dirReq.result == UnityWebRequest.Result.Success)
                {
                    string htmlContent = dirReq.downloadHandler.text;
                    jsonFileNames = ParseJsonFilesFromDirectoryListing(htmlContent);
                    if (jsonFileNames.Length == 0)
                    {
                        Debug.LogWarning("未从目录列表中找到JSON文件，使用默认列表");
                        jsonFileNames = new string[] { "LibIdentity.json", "AssetKeys.json", "ABMetadata.json" };
                    }
                    else
                    {
                        Debug.Log($"从目录列表解析到 {jsonFileNames.Length} 个JSON文件");
                    }
                }
                else
                {
                    Debug.LogWarning($"获取目录列表失败: {dirReq.error}，使用默认列表");
                    jsonFileNames = new string[] { "LibIdentity.json", "AssetKeys.json", "ABMetadata.json" };
                }
            }
            else
            {
                // 使用默认文件列表
                jsonFileNames = new string[] { "LibIdentity.json", "AssetKeys.json", "ABMetadata.json" };
            }

            Directory.CreateDirectory(localFolderPath);

            foreach (string fileName in jsonFileNames)
            {
                if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // 只下载JSON文件
                }

                string netPath = netFolderPath + "/" + fileName;
                string localPath = Path.Combine(localFolderPath, fileName);

                var req = UnityWebRequest.Get(netPath);
                req.downloadHandler = new DownloadHandlerFile(localPath);
                req.SendWebRequest();

                while (!req.isDone)
                {
                    yield return null;
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"下载JSON文件失败 {fileName}: {req.error}");
                    // 继续下载其他文件
                }
                else
                {
                    Debug.Log($"下载JSON文件成功 {fileName}");
                }
            }

            onComplete?.Invoke();
        }
        #endregion

        #region 目录解析辅助方法
        /// <summary>
        /// 从HTML目录列表中解析JSON文件名
        /// </summary>
        /// <param name="htmlContent">HTML内容</param>
        /// <returns>JSON文件名数组</returns>
        private string[] ParseJsonFilesFromDirectoryListing(string htmlContent)
        {
            var jsonFiles = new System.Collections.Generic.List<string>();
            // 使用正则表达式匹配 <a href="filename.json"> 或类似
            var regex = new System.Text.RegularExpressions.Regex(@"<a[^>]*href=[""']([^""']*\.json)[""'][^>]*>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var matches = regex.Matches(htmlContent);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    string fileName = match.Groups[1].Value;
                    // 提取文件名部分（去掉路径）
                    fileName = System.IO.Path.GetFileName(fileName);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        jsonFiles.Add(fileName);
                    }
                }
            }
            return jsonFiles.ToArray();
        #endregion
        }

        /// <summary>
        /// 比较本地和远程GameIdentity，判断是否需要下载
        /// </summary>
        /// <param name="oldGameIdentity">本地缓存的GameIdentity（可能为null）</param>
        /// <param name="remoteGameIdentity">远程GameIdentity</param>
        /// <returns>true表示需要下载，false表示本地已是最新</returns>
        private bool NeedDownloadGameIdentity(ESResJsonData_GameIdentity oldGameIdentity, ESResJsonData_GameIdentity remoteGameIdentity)
        {
            if (oldGameIdentity == null)
            {
                // 本地没有缓存，需要下载
                Debug.Log("本地GameIdentity不存在，需要下载");
                return true;
            }

            // 比较版本号
            if (remoteGameIdentity.Version != oldGameIdentity.Version)
            {
                Debug.Log($"GameIdentity需要更新: 本地版本={oldGameIdentity.Version}, 远程版本={remoteGameIdentity.Version}");
                return true;
            }

            // 比较构建时间戳
            if (remoteGameIdentity.BuildTimestamp != oldGameIdentity.BuildTimestamp)
            {
                Debug.Log($"GameIdentity需要更新: 本地构建时间={oldGameIdentity.BuildTimestamp}, 远程构建时间={remoteGameIdentity.BuildTimestamp}");
                return true;
            }

            Debug.Log("GameIdentity无需更新，所有信息匹配");
            return false;
        }

        /// <summary>
        /// 比较本地和远程库信息，判断是否需要下载
        /// </summary>
        /// <param name="libFolderName">库文件夹名</param>
        /// <param name="remoteLibIdentity">远程库身份信息</param>
        /// <param name="oldLibIdentity">本地缓存的库身份信息（可能为null）</param>
        /// <returns>true表示需要下载，false表示本地已是最新</returns>
        private bool NeedDownloadLibrary(string libFolderName, ESResJsonData_LibIndentity remoteLibIdentity, ESResJsonData_LibIndentity oldLibIdentity)
        {
            if (oldLibIdentity == null)
            {
                // 本地没有缓存，需要下载
                Debug.Log($"库 {libFolderName} 本地不存在，需要下载");
                return true;
            }

            // 比较ChangeCount
            if (remoteLibIdentity.ChangeCount != oldLibIdentity.ChangeCount)
            {
                Debug.Log($"库 {libFolderName} 需要更新: 本地ChangeCount={oldLibIdentity.ChangeCount}, 远程ChangeCount={remoteLibIdentity.ChangeCount}");
                return true;
            }
            else
            {
                Debug.Log($"库 {libFolderName} 无需更新: ChangeCount匹配 ({remoteLibIdentity.ChangeCount})");
                return false;
            }
        }

        /// <summary>
        /// 计算AB包目录的总大小（排除.json文件）
        /// </summary>
        /// <param name="abDirectoryPath">AB包目录路径</param>
        /// <returns>总大小（字节）</returns>
        private long CalculateABTotalSize(string abDirectoryPath)
        {
            if (!Directory.Exists(abDirectoryPath)) return 0;
            return Directory.GetFiles(abDirectoryPath, "*")
                .Where(f => Path.GetExtension(f) != ".json")
                .Sum(f => new FileInfo(f).Length);
        }

        /// <summary>
        /// 注入AssetKeys数据到大型字典
        /// </summary>
        /// <param name="libFolderName">库文件夹名</param>
        /// <param name="assetKeysFilePath">AssetKeys文件路径</param>
        private void InjectAssetKeysData(string libFolderName, string assetKeysFilePath)
        {
            try
            {
                string assetKeysJson = File.ReadAllText(assetKeysFilePath);
                var assetKeysData = JsonConvert.DeserializeObject<ESResJsonData_AssetsKeys>(assetKeysJson);

                if (assetKeysData != null)
                {
                    // 遍历AssetKeys列表，将每个资源键注入到GlobalAssetKeys双键字典
                    foreach (var assetKey in assetKeysData.AssetKeys)
                    {
                        // 设置库信息
                        assetKey.LibName = libFolderName;
                        assetKey.LibFolderName = libFolderName;

                        // 直接使用资源键中的GUID和Path
                        string pathKey = assetKey.Path ?? assetKey.ResName;
                        string guidKey = assetKey.GUID;

                        // 确保键不为空
                        if (string.IsNullOrEmpty(pathKey) || string.IsNullOrEmpty(guidKey))
                        {
                            Debug.LogWarning($"跳过无效的AssetKey: Path='{pathKey}', GUID='{guidKey}', ResName='{assetKey.ResName}'");
                            continue;
                        }

                        try
                        {
                            // 添加到GlobalAssetKeys双键字典
                            GlobalAssetKeys.Add(pathKey, guidKey, assetKey);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"添加AssetKey失败 {assetKey.ResName}: {ex.Message}");
                        }
                    }

                    Debug.Log($"成功注入AssetKeys数据到GlobalAssetKeys: {libFolderName}, 资产数量: {assetKeysData.AssetKeys.Count}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"注入AssetKeys数据失败 {libFolderName}: {ex.Message}");
            }
        }

        /// <summary>
        /// 注入ABMetadata数据到大型字典
        /// </summary>
        /// <param name="libFolderName">库文件夹名</param>
        /// <param name="abMetadataFilePath">ABMetadata文件路径</param>
        /// <returns>反序列化的ABMetadata数据对象</returns>
        private ESResJsonData_ABMetadata InjectABMetadataData(string libFolderName, string abMetadataFilePath)
        {
            try
            {
                string abMetadataJson = File.ReadAllText(abMetadataFilePath);
                var abMetadataData = JsonConvert.DeserializeObject<ESResJsonData_ABMetadata>(abMetadataJson);

                if (abMetadataData != null)
                {
                    // 合并哈希数据到GlobalABPreToHashes
                    foreach (var kvp in abMetadataData.PreToHashes)
                    {
                        if (!GlobalABPreToHashes.ContainsKey(kvp.Key))
                        {
                            GlobalABPreToHashes[kvp.Key] = kvp.Value;
                        }

                        // 同时添加到反向映射 GlobalABHashToPres
                        if (!GlobalABHashToPres.ContainsKey(kvp.Value))
                        {
                            GlobalABHashToPres[kvp.Value] = kvp.Key;
                        }
                    }

                    // 合并ABKeys数据到GlobalABKeys
                    foreach (var abKey in abMetadataData.ABKeys)
                    {
                        // 检查现有值是否完整，不完整则替换
                        bool shouldReplace = true;
                        if (GlobalABKeys.TryGetValue(abKey.ABName, out var existingKey))
                        {
                            // 检查关键字段是否完整
                            shouldReplace = string.IsNullOrEmpty(existingKey.LibName) ||
                                           string.IsNullOrEmpty(existingKey.ABName);
                        }

                        if (shouldReplace)
                        {
                            GlobalABKeys[abKey.ABName] = abKey;
                        }
                    }

                    // 合并依赖数据到GlobalDependences
                    foreach (var kvp in abMetadataData.Dependences)
                    {
                        if (!GlobalDependencies.ContainsKey(kvp.Key))
                        {
                            GlobalDependencies[kvp.Key] = kvp.Value;
                        }
                    }

                    Debug.Log($"成功注入ABMetadata数据到全局字典: {libFolderName}, AB包数量: {abMetadataData.PreToHashes.Count}");
                    return abMetadataData;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"注入ABMetadata数据失败 {libFolderName}: {ex.Message}");
            }
            return null;
        }

        private bool TryRegisterLibraryFromLocal(string libFolderName, string libIdentityPath, bool isRemote, ESResJsonData_LibIndentity knownIdentity, out DownloadedLibraryData registeredLib)
        {
            registeredLib = null;

            ESResJsonData_LibIndentity libIdentity = knownIdentity;
            if (libIdentity == null)
            {
                if (!File.Exists(libIdentityPath))
                {
                    return false;
                }

                try
                {
                    string libIdentityJson = File.ReadAllText(libIdentityPath);
                    libIdentity = JsonConvert.DeserializeObject<ESResJsonData_LibIndentity>(libIdentityJson);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"解析LibIdentity失败 {libFolderName}: {ex.Message}");
                    return false;
                }
            }

            if (libIdentity == null)
            {
                return false;
            }

            string localBasePath = isRemote ? DefaultPaths.GetLocalLibBasePath(libFolderName) : DefaultPaths.GetLocalBuildLibBasePath(libFolderName);
            string remoteBasePath = isRemote ? DefaultPaths.GetNetLibBasePath(libFolderName) : string.Empty;
            string abDirectory = isRemote ? DefaultPaths.GetLocalABBasePath(libFolderName) : localBasePath;
            long totalSize = CalculateABTotalSize(abDirectory);

            registeredLib = new DownloadedLibraryData
            {
                LibraryName = string.IsNullOrEmpty(libIdentity.LibraryDisplayName) ? libFolderName : libIdentity.LibraryDisplayName,
                LibFolderName = string.IsNullOrEmpty(libIdentity.LibFolderName) ? libFolderName : libIdentity.LibFolderName,
                LocalPath = EnsureTrailingSlash(localBasePath),
                RemotePath = EnsureTrailingSlash(remoteBasePath),
                IsRemote = isRemote,
                Version = libIdentity.ChangeCount.ToString(),
                Description = libIdentity.LibraryDescription,
                TotalSize = totalSize,
                ChangeCount = libIdentity.ChangeCount
            };

            return true;
        }

        private static string EnsureTrailingSlash(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            return path.EndsWith("/", StringComparison.Ordinal) ? path : path + "/";
        }

        private IEnumerator DownloadTextWithRetries(string url, Action<string> onSuccess, Action<string> onFailure, int maxRetryCount = DefaultMaxRetryCount, float retryDelaySeconds = DefaultRetryDelaySeconds, int timeoutSeconds = DefaultRequestTimeoutSeconds)
        {
            if (string.IsNullOrEmpty(url))
            {
                onFailure?.Invoke("URL为空");
                yield break;
            }

            int safeRetryCount = Mathf.Max(1, maxRetryCount);

            for (int attempt = 1; attempt <= safeRetryCount; attempt++)
            {
                using (var request = UnityWebRequest.Get(url))
                {
                    request.timeout = timeoutSeconds;
                    var asyncOp = request.SendWebRequest();
                    while (!asyncOp.isDone)
                    {
                        yield return null;
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        onSuccess?.Invoke(request.downloadHandler.text);
                        yield break;
                    }

                    string error = request.error;
                    Debug.LogWarning($"DownloadText失败 (尝试 {attempt}/{safeRetryCount}) {url}: {error}");

                    if (attempt >= safeRetryCount)
                    {
                        onFailure?.Invoke(error);
                        yield break;
                    }
                }

                yield return new WaitForSeconds(retryDelaySeconds);
            }
        }

        private IEnumerator DownloadFileWithRetries(string url, string localPath, Action onSuccess, Action<string> onFailure, int maxRetryCount = DefaultMaxRetryCount, float retryDelaySeconds = DefaultRetryDelaySeconds, int timeoutSeconds = DefaultRequestTimeoutSeconds)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(localPath))
            {
                onFailure?.Invoke("URL或本地路径为空");
                yield break;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(localPath));

            int safeRetryCount = Mathf.Max(1, maxRetryCount);

            for (int attempt = 1; attempt <= safeRetryCount; attempt++)
            {
                using (var request = UnityWebRequest.Get(url))
                {
                    request.timeout = timeoutSeconds;
                    request.downloadHandler = new DownloadHandlerFile(localPath);
                    var asyncOp = request.SendWebRequest();
                    while (!asyncOp.isDone)
                    {
                        yield return null;
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        onSuccess?.Invoke();
                        yield break;
                    }

                    string error = request.error;
                    Debug.LogWarning($"DownloadFile失败 (尝试 {attempt}/{safeRetryCount}) {url}: {error}");
                    SafeDeleteFile(localPath);

                    if (attempt >= safeRetryCount)
                    {
                        onFailure?.Invoke(error);
                        yield break;
                    }
                }

                yield return new WaitForSeconds(retryDelaySeconds);
            }
        }

        private static void SafeDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"删除文件失败 {path}: {ex.Message}");
            }
        }

        #region 下载状态枚举
        /// <summary>
        /// 资源下载【仅限于全局下载】流程状态机。
        /// - <c>None</c>：未开始或无活动状态。
        /// - <c>Compare</c>：已下载并解析远端清单，正在比对本地与远端资源差异（决定哪些需要下载）。
        /// - <c>Download</c>：处于下载阶段，正在下载缺失或需要更新的 AB 包。
        /// - <c>Ready</c>：所有必要的下载与比对完成，资源已准备就绪。
        /// </summary>
        public enum ESResGlobalDownloadState
        {
            /// <summary>未开始或未进入下载流程。</summary>
            None,

            /// <summary>已获取远端清单并正在与本地进行比较，生成待下载列表。</summary>
            Comparing,

            /// <summary>正在下载缺失或需要更新的 AB 包。</summary>
            Downloading,

            /// <summary>下载与比对流程完成，资源可直接使用。</summary>
            AllReady
        }
        #endregion
    }
}
