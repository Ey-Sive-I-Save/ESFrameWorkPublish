using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UpmPackageInfo = UnityEditor.PackageManager.PackageInfo;


namespace ES.EditorInternal.Installer
{
    public class ESInstallerStartupInitializer : ES.EditorInvoker_Level2
    {
        public override void InitInvoke()
        {
            ESInstaller.RegisterStartupCheck();
        }
    }

    /// <summary>
    /// ES框架安装器 - 商业级Unity插件安装管理工具
    /// </summary>
    public class ESInstaller : ESSinglePageIMGUIWindow<ESInstaller>
    {
        public override string ESWindow_PresentationShortTitle => "安装";
        #region 静态初始化

        internal static void RegisterStartupCheck()
        {
            // 延迟执行，避免在编辑器启动时立即检查
            EditorApplication.delayCall -= CheckDependenciesOnStartup;
            EditorApplication.delayCall += CheckDependenciesOnStartup;
        }

        private static async void CheckDependenciesOnStartup()
        {
            if (!EditorPrefs.GetBool(GetAutoCheckPreferenceKey(), false))
                return;

            await CheckAndShowInstallerIfNeededAsync();
        }

        private static ESInstaller installer;
        private static bool dependencyCheckInProgress;

        private static bool TryBeginDependencyCheck()
        {
            if (dependencyCheckInProgress)
                return false;
            dependencyCheckInProgress = true;
            return true;
        }

        private static void EndDependencyCheck()
        {
            dependencyCheckInProgress = false;
        }

        private sealed class DependencyCheckResult
        {
            internal int TotalRequired { get; private set; }
            internal int InstalledRequired { get; private set; }
            internal bool HasUninstalledRequiredDependencies => InstalledRequired < TotalRequired;

            internal void RecordRequiredDependency(bool isInstalled)
            {
                TotalRequired++;
                if (isInstalled)
                    InstalledRequired++;
            }
        }

        private sealed class InstalledPackageSnapshot
        {
            internal InstalledPackageSnapshot(
                UpmPackageInfo[] packages,
                string failureMessage)
            {
                Packages = packages ?? Array.Empty<UpmPackageInfo>();
                FailureMessage = failureMessage;
            }

            internal UpmPackageInfo[] Packages { get; }
            internal string FailureMessage { get; }
            internal bool IsAvailable => string.IsNullOrEmpty(FailureMessage);
        }

        private static InstallationProfile LoadCanonicalInstallationProfile()
        {
            InstallationProfile profile = InstallationProfile.LoadFromFile()
                ?? new InstallationProfile();
            profile.enableAutoCheck = EditorPrefs.GetBool(GetAutoCheckPreferenceKey(), false);
            profile.skipNextAutoCheck = SessionState.GetBool(
                GetSkipNextAutoCheckSessionKey(),
                false);
            return profile;
        }

        private static ESInstaller GetOrCreateInstallerWindow()
        {
            ESInstaller window = installer;
            if (window == null)
            {
                window = GetWindow<ESInstaller>("ES 安装管理器");
                installer = window;
            }

            return window;
        }

        private static async Task CheckAndShowInstallerIfNeededAsync()
        {
            if (!TryBeginDependencyCheck())
                return;
            try
            {
                InstallationProfile profile = LoadCanonicalInstallationProfile();

                // 检查是否启用自动检查
                if (!profile.enableAutoCheck)
                    return;

                // 检查是否跳过此次检查
                if (profile.skipNextAutoCheck)
                {
                    profile.skipNextAutoCheck = false;
                    SessionState.SetBool(GetSkipNextAutoCheckSessionKey(), false);
                    return;
                }
                // 检查是否有未安装的必需依赖
                DependencyCheckResult result =
                    await CheckRequiredDependenciesAsync(profile.mainPackage);

                // 如果有未安装的必需依赖，显示安装器
                if (result.HasUninstalledRequiredDependencies)
                    ShowInstallerWithWarning();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ES Installer 启动检查失败: {e.Message}");
            }
            finally
            {
                EndDependencyCheck();
            }
        }

        private static bool CheckUnityPackageInstalled(
            UnityPackageDependency dependency,
            IReadOnlyList<UpmPackageInfo> installedPackages)
        {
            if (dependency == null)
                return false;

            // 首先检查类是否存在（同步操作）
            if (!string.IsNullOrEmpty(dependency.checkClass))
            {
                if (IsClassExists(dependency.checkClass))
                {
                    return true;
                }
            }

            // 如果没有类检查或类检查失败，检查UPM
            if (string.IsNullOrEmpty(dependency.packageId))
                return false;

            return installedPackages != null
                && installedPackages.Any(package => package != null
                    && string.Equals(package.name, dependency.packageId, StringComparison.Ordinal));
        }

        private static bool CheckGitPackageInstalled(
            GitPackageDependency dependency,
            IReadOnlyList<UpmPackageInfo> installedPackages)
        {
            if (dependency == null)
                return false;
            if (!TryValidatePinnedGitUrl(dependency.gitUrl, out string pinnedGitUrl, out _, out _))
                return false;

            // 首先检查类是否存在（同步操作）
            if (!string.IsNullOrEmpty(dependency.checkClass))
            {
                if (IsClassExists(dependency.checkClass))
                {
                    return true;
                }
            }

            // 如果没有类检查或类检查失败，检查UPM
            if (string.IsNullOrEmpty(dependency.gitUrl))
                return false;

            return installedPackages != null
                && installedPackages.Any(package => package != null
                    && (string.Equals(package.packageId, pinnedGitUrl, StringComparison.Ordinal)
                        || string.Equals(package.name, pinnedGitUrl, StringComparison.Ordinal)));
        }

        private static bool IsUserPackageInstalled(UserPackageDependency dependency)
        {
            // 用户包只通过类检查（同步操作）
            if (dependency == null || string.IsNullOrEmpty(dependency.checkClass))
                return false;

            return IsClassExists(dependency.checkClass);
        }

        /// <summary>
        /// 使用一次 UPM 快照统一检查四类必需依赖。
        /// </summary>
        private static async Task<DependencyCheckResult> CheckRequiredDependenciesAsync(
            ESPackageBase package)
        {
            var result = new DependencyCheckResult();
            if (package == null)
                return result;

            InstalledPackageSnapshot snapshot = await CaptureInstalledPackageSnapshotAsync();
            if (!snapshot.IsAvailable)
                throw new InvalidOperationException(snapshot.FailureMessage);
            UpmPackageInfo[] installedPackages = snapshot.Packages;
            foreach (UnityPackageDependency dependency in
                     (IEnumerable<UnityPackageDependency>)package.unityDependencies
                     ?? Enumerable.Empty<UnityPackageDependency>())
            {
                if (dependency == null || !dependency.isRequired)
                    continue;
                result.RecordRequiredDependency(
                    CheckUnityPackageInstalled(dependency, installedPackages));
            }

            foreach (GitPackageDependency dependency in
                     (IEnumerable<GitPackageDependency>)package.gitDependencies
                     ?? Enumerable.Empty<GitPackageDependency>())
            {
                if (dependency == null || !dependency.isRequired)
                    continue;
                result.RecordRequiredDependency(
                    CheckGitPackageInstalled(dependency, installedPackages));
            }

            foreach (UserPackageDependency dependency in
                     (IEnumerable<UserPackageDependency>)package.userDependencies
                     ?? Enumerable.Empty<UserPackageDependency>())
            {
                if (dependency == null || !dependency.isRequired)
                    continue;
                result.RecordRequiredDependency(IsUserPackageInstalled(dependency));
            }

            foreach (AssetFileDependency dependency in
                     (IEnumerable<AssetFileDependency>)package.assetFileDependencies
                     ?? Enumerable.Empty<AssetFileDependency>())
            {
                if (dependency == null || !dependency.isRequired)
                    continue;
                result.RecordRequiredDependency(CheckAssetFileInstalled(dependency));
            }

            return result;
        }

        private static async Task<InstalledPackageSnapshot> CaptureInstalledPackageSnapshotAsync()
        {
            try
            {
                ListRequest request = Client.List(false, false);
                await WaitForListRequestCompletion(request);
                if (request.Status == StatusCode.Success && request.Result != null)
                    return new InstalledPackageSnapshot(request.Result.ToArray(), null);

                return new InstalledPackageSnapshot(
                    null,
                    "ES Installer 无法读取 UPM 包快照: "
                    + (request.Error?.message ?? request.Status.ToString()));
            }
            catch (Exception exception)
            {
                return new InstalledPackageSnapshot(
                    null,
                    $"ES Installer 无法读取 UPM 包快照: {exception.Message}");
            }
        }

        private static Task WaitForListRequestCompletion(ListRequest request)
        {
            var tcs = new TaskCompletionSource<bool>();
            void CheckCompletion()
            {
                if (request.IsCompleted)
                {
                    EditorApplication.update -= CheckCompletion;
                    tcs.SetResult(true);
                }
            }
            EditorApplication.update += CheckCompletion;
            return tcs.Task;
        }

        private static Task WaitForAddRequestCompletion(AddRequest request)
        {
            var tcs = new TaskCompletionSource<bool>();
            void CheckCompletion()
            {
                if (request.IsCompleted)
                {
                    EditorApplication.update -= CheckCompletion;
                    tcs.SetResult(true);
                }
            }
            EditorApplication.update += CheckCompletion;
            return tcs.Task;
        }

        private static async Task QuickCheckAndShowResultAsync()
        {
            if (!TryBeginDependencyCheck())
            {
                EditorUtility.DisplayDialog(
                    "依赖检查进行中",
                    "已有一次依赖检查正在运行，请等待当前检查结束。",
                    "确定");
                return;
            }
            try
            {
                InstallationProfile profile = LoadCanonicalInstallationProfile();
                DependencyCheckResult result =
                    await CheckRequiredDependenciesAsync(profile.mainPackage);

                // 显示检查结果
                if (result.HasUninstalledRequiredDependencies)
                {
                    bool openInstaller = EditorUtility.DisplayDialog(
                        "ES框架依赖检查结果",
                        $"发现未安装的必需依赖！\n\n已安装: {result.InstalledRequired}/{result.TotalRequired}\n\n是否打开安装管理器来解决依赖问题？",
                        "打开安装器",
                        "稍后处理"
                    );

                    if (openInstaller)
                    {
                        ShowInstaller();
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "ES框架依赖检查结果",
                        $"所有必需依赖都已正确安装！\n\n已安装: {result.InstalledRequired}/{result.TotalRequired}",
                        "确定"
                    );
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ES Installer 快速检查失败: {e.Message}");
                EditorUtility.DisplayDialog(
                    "检查失败",
                    $"依赖检查过程中出现错误:\n\n{e.Message}",
                    "确定"
                );
            }
            finally
            {
                EndDependencyCheck();
            }
        }

        private static bool IsClassExists(string className)
        {

            if (string.IsNullOrEmpty(className))
                return false;

            try
            {
                // Debug.Log("1PADNINGH: Checking class existence for " + className );
                // 尝试直接获取类型
                var type = System.Type.GetType(className);
                if (type != null)
                {
                    return true;
                }
                else
                {
                    // Debug.Log("2PADNINGH: Checking class existence for " + className );

                    // 如果直接获取失败，遍历所有程序集
                    var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                    foreach (var assembly in assemblies)
                    {
                        try
                        {
                            var types = assembly.GetTypes();
                            if (types.Any(t => t.FullName == className))
                            {
                                return true;
                            }
                        }
                        catch (System.Reflection.ReflectionTypeLoadException ex)
                        {
                            Debug.LogWarning($"无法加载程序集 {assembly.FullName}: {ex.Message}");
                            continue;
                        }
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            // Debug.Log("4PADNINGH: Checking class existence for " + className );


            return false;
        }

        private static void ShowInstallerWithWarning()
        {
            if (installer != null)
            {
                installer.Focus();
                return;
            }

            bool showInstaller = EditorUtility.DisplayDialog(
                "ES框架依赖检查",
                "检测到ES框架有未安装的必需依赖项。\n\n是否现在打开安装管理器来解决依赖问题？",
                "打开安装器",
                "稍后处理"
            );

            if (showInstaller)
                ShowInstaller();
        }

        #endregion
        #region 数据结构

        /// <summary>
        /// Unity官方包依赖 - 通过Unity Package Manager直接安装的包
        /// </summary>
        [System.Serializable]
        public class UnityPackageDependency
        {
            public string name;
            public string version;
            public string description;
            public bool isRequired = true;
            public bool isInstalled;
            public string installUrl;
            public string packageId; // Unity Package Manager ID
            public string checkClass; // 可选：用于验证安装状态的完整类名（包含命名空间）
        }

        /// <summary>
        /// Git包依赖 - 通过Git URL安装的包，通常来自GitHub或其他Git仓库
        /// </summary>
        [System.Serializable]
        public class GitPackageDependency
        {
            public string name;
            public string version;
            public string description;
            public bool isRequired = true;
            public bool isInstalled;
            public string gitUrl; // Git仓库URL
            public string checkClass; // 可选：用于验证安装状态的完整类名（包含命名空间）
        }

        /// <summary>
        /// 用户包依赖 - 需要用户手动安装的包，安装器只负责检查是否存在指定的类
        /// </summary>
        [System.Serializable]
        public class UserPackageDependency
        {
            public string name;
            public string version;
            public string description;
            public bool isRequired = true;
            public bool isInstalled;
            public string checkClass; // 必需：用于验证安装状态的完整类名（包含命名空间）
            public string installInstructions; // 安装说明
        }

        /// <summary>
        /// 资产文件依赖 - 检查Assets路径中是否存在指定的文件
        /// </summary>
        [System.Serializable]
        public class AssetFileDependency
        {
            public string name;
            public string version;
            public string description;
            public bool isRequired = true;
            public bool isInstalled;
            public string assetPath; // Assets路径，如"Assets/Plugins/SomeFile.dll"
            public string checkClass; // 可选：用于验证安装状态的完整类名（包含命名空间）
        }

        /// <summary>
        /// 包安装状态枚举
        /// </summary>
        public enum PackageInstallState
        {
            Loading,      // 加载中
            NotInstalled, // 未安装
            Installed     // 已安装
        }

        /// <summary>
        /// ES包基类 - 主包和扩展包的共同基类
        /// </summary>
        [System.Serializable]
        public class ESPackageBase
        {
            public string packageId; // 包唯一标识符
            public string displayName; // 显示名称
            public string version; // 版本号
            public string description; // 描述
            public bool isRequired = true; // 是否必需
            [NonSerialized] public PackageInstallState installState = PackageInstallState.Loading; // 安装状态
            [NonSerialized] public string packageFolderPath; // 包文件夹的完整路径（运行时设置）
            public string folderName; // 在Downloads下的文件夹名称
            public List<UnityPackageDependency> unityDependencies = new List<UnityPackageDependency>(); // Unity包依赖
            public List<GitPackageDependency> gitDependencies = new List<GitPackageDependency>(); // Git包依赖
            public List<UserPackageDependency> userDependencies = new List<UserPackageDependency>(); // 用户包依赖
            public List<AssetFileDependency> assetFileDependencies = new List<AssetFileDependency>(); // 资产文件依赖
            public string installNotes; // 安装说明
            public string checkClass; // 可选：用于验证安装状态的完整类名（包含命名空间）
            public string assetPath; // 可选：用于验证安装状态的资产路径（如"Assets/Whisper"）
            public List<string> tags = new List<string>(); // 标签
            public string author; // 作者
            public string website; // 官网
            public string license; // 许可证

            /// <summary>
            /// 从指定文件夹的package.json加载包信息
            /// </summary>
            public static T LoadFromJson<T>(string folderPath) where T : ESPackageBase, new()
            {
                string packageJsonPath = Path.Combine(folderPath, "package.json");
                if (File.Exists(packageJsonPath))
                {
                    try
                    {
                        string json = File.ReadAllText(
                            packageJsonPath,
                            new UTF8Encoding(false, true));
                        T package = JsonUtility.FromJson<T>(json);
                        if (package != null)
                        {
                            ExtensionPackageJsonData serializedData =
                                JsonUtility.FromJson<ExtensionPackageJsonData>(json);
                            if (serializedData?.installationNotes != null)
                                package.installNotes = serializedData.installationNotes;
                            package.packageFolderPath = folderPath;
                            package.installState = PackageInstallState.Loading;
                            package.unityDependencies ??= new List<UnityPackageDependency>();
                            package.gitDependencies ??= new List<GitPackageDependency>();
                            package.userDependencies ??= new List<UserPackageDependency>();
                            package.assetFileDependencies ??= new List<AssetFileDependency>();
                            package.tags ??= new List<string>();
                            // 从文件夹名推断folderName
                            if (string.IsNullOrEmpty(package.folderName))
                            {
                                package.folderName = Path.GetFileName(folderPath);
                            }
                            return package;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"解析包配置失败 {packageJsonPath}: {e.Message}");
                    }
                }
                // 返回默认实例
                return new T { packageFolderPath = folderPath, folderName = Path.GetFileName(folderPath) };
            }
        }

        /// <summary>
        /// ES主包 - ES框架的核心包，必需安装
        /// </summary>
        [System.Serializable]
        public class ESMainPackage : ESPackageBase
        {
            public ESMainPackage()
            {
                packageId = "es_main";
                displayName = "ES Framework 主包";
                folderName = "Main";
                isRequired = true;
            }
        }

        /// <summary>
        /// ES扩展包 - ES框架的可选扩展包，每个扩展包有独立的文件夹和依赖
        /// </summary>
        [System.Serializable]
        public class ESExtensionPackage : ESPackageBase
        {
            public bool isSelectedForInstall = false; // 用户选择是否安装
            public List<string> requiredMainPackages = new List<string>(); // 依赖的主包列表

            public ESExtensionPackage()
            {
                isRequired = false; // 扩展包默认非必需
            }
        }

        [System.Serializable]
        public class InstallationProfile
        {
            public string profileName = "Default Profile";

            // ES包系统
            public ESMainPackage mainPackage = new ESMainPackage(); // 主包配置
            public List<ESExtensionPackage> extensionPackages = new List<ESExtensionPackage>(); // 扩展包列表

            public string installationNotes;
            public DateTime lastModified;
            public bool enableAutoCheck = false; // 外部依赖检查默认由用户显式触发
            public bool skipNextAutoCheck = false; // 跳过下次自动检查

            /// <summary>
            /// 从Downloads文件夹扫描并加载所有包配置
            /// </summary>
            public static InstallationProfile LoadFromFile()
            {
                try
                {
                    string downloadsFolder = Path.Combine(
                        Application.dataPath,
                        "Plugins",
                        "ES",
                        "Editor",
                        "Installer",
                        "Downloads");

                    if (!Directory.Exists(downloadsFolder))
                    {
                        Debug.LogWarning($"Downloads文件夹不存在: {downloadsFolder}");
                        return CreateDefaultProfile();
                    }

                    var profile = new InstallationProfile();

                    // 扫描Downloads文件夹下的所有子文件夹
                    string[] subFolders = Directory.GetDirectories(downloadsFolder)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    foreach (string folderPath in subFolders)
                    {

                        string folderName = Path.GetFileName(folderPath);
                        string packageJsonPath = Path.Combine(folderPath, "package.json");

                        if (!File.Exists(packageJsonPath))
                        {
                            continue; // 跳过没有package.json的文件夹
                        }

                        // 根据文件夹名判断是主包还是扩展包
                        if (folderName.Equals("Main", StringComparison.OrdinalIgnoreCase))
                        {
                            // 加载主包
                            var mainPackage = ESPackageBase.LoadFromJson<ESMainPackage>(folderPath);
                            mainPackage.packageId = "es_main";
                            mainPackage.folderName = "Main";
                            profile.mainPackage = mainPackage;
                            profile.profileName = $"{mainPackage.displayName} {mainPackage.version}";
                        }
                        else
                        {
                            // 加载扩展包
                            var extensionPackage = ESPackageBase.LoadFromJson<ESExtensionPackage>(folderPath);
                            extensionPackage.packageId = $"ext_{folderName}";
                            extensionPackage.folderName = folderName;
                            extensionPackage.requiredMainPackages ??= new List<string>();
                            profile.extensionPackages.Add(extensionPackage);
                        }
                    }

                    profile.lastModified = DateTime.Now;
                    return profile;
                }
                catch (Exception e)
                {
                    Debug.LogError($"加载配置文件失败: {e.Message}\n{e.StackTrace}");
                    return CreateDefaultProfile();
                }
            }

            /// <summary>
            /// 创建默认配置
            /// </summary>
            private static InstallationProfile CreateDefaultProfile()
            {
                var profile = new InstallationProfile
                {
                    profileName = "Default Profile",
                    mainPackage = new ESMainPackage(),
                    enableAutoCheck = false,
                    skipNextAutoCheck = false,
                    lastModified = DateTime.Now
                };
                return profile;
            }
        }

        #endregion

        #region 私有字段

        private InstallationProfile _currentProfile;
        private InstallationProfile currentProfile
        {
            get { return _currentProfile; }
            set
            {
                _currentProfile = value;
                // Debug.Log("currentProfile has been set.");
            }
        }
        private Vector2 scrollPosition;
        private bool showUnityPackages = true;
        private bool showGitPackages = true;
        private bool showUserPackages = true;
        private bool showESPackageSystem = true;
        private bool showInstallation = true;
        private bool showDebug = false;
        private bool showDependencyEditor;
        private string statusMessage = "";
        private MessageType statusType = MessageType.Info;

        private string downloadsFolderPath;
        private const string DOWNLOADS_FOLDER_NAME = "Downloads";
        private const string AutoCheckPreferencePrefix = "ESInstaller.AutoCheck.";
        private const string SkipNextAutoCheckSessionPrefix = "ESInstaller.SkipNextAutoCheck.";

        // UI样式
        private GUIStyle headerStyle;
        private GUIStyle sectionStyle;
        private GUIStyle statusStyle;
        private GUIStyle packageNameStyle;

        // UI状态
        private bool isRefreshingStatuses;
        private bool isMainPackageInstalled = false; // 主包是否已安装

        // 包选择相关
        private List<string> availablePackageIds = new List<string>();
        private Dictionary<string, string> packageDisplayNames = new Dictionary<string, string>();
        private string currentSelectedPackageId = "es_main";
        private string[] packageDisplayNameBuffer = Array.Empty<string>();

        // 配置相关
        private bool isConfigModified = false;
        private string configFilePath;

        private const string UnityPackageTrustManifestFileName = ESInstallerPackageTrust.ManifestFileName;
        private readonly Queue<VerifiedUnityPackage> verifiedImportQueue = new Queue<VerifiedUnityPackage>();
        private VerifiedUnityPackage activeVerifiedImport;
        private string lastImportReceiptPath;

        #endregion

        #region 菜单项

        [MenuItem(MenuItemPathDefine.INSTALL_DEPENDENCY_PATH + "打开安装管理器", false, 0)]
        static void ShowInstaller()
        {
            installer = GetOrCreateInstallerWindow();
            installer.minSize = new Vector2(600, 500);
            installer.Show();
            installer.Focus();
        }

        [MenuItem(MenuItemPathDefine.INSTALL_DEPENDENCY_PATH + "检查依赖", false, 2)]
        static void QuickCheckDependencies()
        {
            // 异步检查依赖并显示结果
            _ = QuickCheckAndShowResultAsync();
        }

        #endregion

        #region Unity生命周期

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent("ES 安装管理器", "检查并安装 ES 主包、扩展包及外部依赖");
        }

        protected override string ESWindow_Subtitle => "包依赖、受信导入与安装回执";
        protected override Vector2 ESWindow_MinSize => new Vector2(600f, 500f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(980f, 760f);
        protected override string ESWindow_PageStableId => "installer.packages";
        protected override string ESWindow_PageTitle => "安装与依赖";
        protected override string ESWindow_PageKeywords => "安装器 依赖 UPM Git UnityPackage 扩展包 回执";

        protected override void ESWindow_BuildPageActions(
            ICollection<ESMenuTreePageAction> actions)
        {
            actions.Add(new ESMenuTreePageAction(
                    "installer.refresh-status",
                    "刷新状态",
                    "重新加载配置并检查所有包与依赖状态。",
                    context =>
                    {
                        RefreshAllStatuses();
                        context.RefreshPageActions();
                        context.SetStatus("正在刷新安装状态");
                    })
                .When(() => !isRefreshingStatuses && currentProfile != null)
                .WithUnityIcon("Refresh")
                .WithPriority(100));
            actions.Add(new ESMenuTreePageAction(
                    "installer.report",
                    "安装报告",
                    "生成当前包与依赖安装报告。",
                    context =>
                    {
                        GenerateInstallationReport();
                        context.SetStatus("安装报告已生成");
                    })
                .When(() => currentProfile != null)
                .WithUnityIcon("Clipboard")
                .WithPriority(80));
            actions.Add(new ESMenuTreePageAction(
                    "installer.help",
                    "帮助",
                    "打开安装管理器帮助。",
                    _ => ShowHelp())
                .WithUnityIcon("_Help")
                .WithPriority(20));
        }

        protected override void ESWindow_OnHostEnable()
        {
            // 确保静态引用正确设置
            if (installer == null)
            {
                installer = this;
            }

            // 确保在重新编译后窗口能正常工作
            //Close();
          //  Debug.Log("ES Installer 窗口已启用"); 
            InitializePaths();
            LoadConfiguration();

            if (string.IsNullOrEmpty(statusMessage))
            {
                statusMessage = "配置和本地包已加载；点击“刷新状态”检查外部依赖。";
                statusType = MessageType.Info;
            }

            AssetDatabase.importPackageCompleted -= OnTrustedImportCompleted;
            AssetDatabase.importPackageCompleted += OnTrustedImportCompleted;
            AssetDatabase.importPackageCancelled -= OnTrustedImportCancelled;
            AssetDatabase.importPackageCancelled += OnTrustedImportCancelled;
            AssetDatabase.importPackageFailed -= OnTrustedImportFailed;
            AssetDatabase.importPackageFailed += OnTrustedImportFailed;

        }

        private void InitializePaths()
        {
            // 获取当前脚本所在文件夹的路径
            var script = MonoScript.FromScriptableObject(this);
            string scriptPath = AssetDatabase.GetAssetPath(script);
            string scriptFolder = Path.GetDirectoryName(scriptPath);

            // 设置下载文件夹路径
            downloadsFolderPath = Path.Combine(scriptFolder, DOWNLOADS_FOLDER_NAME);
            // 设置配置文件路径
            configFilePath = Path.Combine(downloadsFolderPath, "Main", "package.json");

            // 确保下载文件夹存在
            ESManagedFileIO.EnsurePath(downloadsFolderPath, false, Application.dataPath);
            if (!Directory.Exists(downloadsFolderPath))
            {
                Directory.CreateDirectory(downloadsFolderPath);
            }
        }

        private sealed class VerifiedUnityPackage
        {
            public string packageId;
            public string packageVersion;
            public string source;
            public string keyId;
            public string signature;
            public string originalPath;
            public string stagedPath;
            public string stagingDirectory;
            public string receiptId;
            public string receiptPath;
            public long size;
            public string sha256;
        }

        [Serializable]
        private sealed class UnityPackageImportReceipt
        {
            public int schemaVersion = 1;
            public string receiptId;
            public string recordedAtUtc;
            public string result;
            public string unityPackageName;
            public string packageId;
            public string packageVersion;
            public string source;
            public string keyId;
            public string originalPath;
            public long expectedSize;
            public string expectedSha256;
            public long actualSize;
            public string actualSha256;
            public string detail;
        }

        private static class ESInstallerTrustedKeys
        {
            public static bool TryGetRsaPublicKey(string keyId, out string publicKeyXml)
            {
                return ESInstallerPackageTrust.TryGetTrustedRsaPublicKey(keyId, out publicKeyXml);
            }
        }

        /// <summary>
        /// 将配置文件中的 packageFolderPath/folderName 收口到 Installer 自己的 Downloads 根目录。
        /// 安装器会读取外部 JSON，不能把这些字段直接交给 Path.Combine 和 AssetDatabase.ImportPackage，
        /// 否则一个绝对路径或 .. 片段就能让安装器读取/导入工程外的 unitypackage。
        /// </summary>
        private string GetSafePackageFolderPath(ESPackageBase package)
        {
            if (package == null || string.IsNullOrWhiteSpace(downloadsFolderPath))
                return null;

            try
            {
                string downloadsRoot = Path.GetFullPath(downloadsFolderPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fullPath;
                if (string.IsNullOrWhiteSpace(package.packageFolderPath))
                {
                    string folderName = (package.folderName ?? string.Empty).Trim().Replace('\\', '/');
                    if (string.IsNullOrWhiteSpace(folderName)
                        || Path.IsPathRooted(folderName)
                        || folderName.IndexOf('/') >= 0
                        || folderName.IndexOf(':') >= 0
                        || folderName == "."
                        || folderName == "..")
                        return null;

                    fullPath = Path.GetFullPath(Path.Combine(downloadsRoot, folderName));
                }
                else
                {
                    fullPath = Path.GetFullPath(package.packageFolderPath);
                }

                string requiredPrefix = downloadsRoot + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
                    return null;
                return ContainsExistingReparsePoint(downloadsRoot, fullPath) ? null : fullPath;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool ContainsExistingReparsePoint(string root, string candidate)
        {
            string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string current = rootFull;
            string relative = candidate.Substring(rootFull.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (string segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (string.IsNullOrEmpty(segment)) continue;
                current = Path.Combine(current, segment);
                if (!Directory.Exists(current) && !File.Exists(current)) break;
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            }
            return false;
        }

        /// <summary>
        /// .unitypackage 必须由受信发布公钥签名的清单逐一声明。清单与包同目录可以被替换，
        /// 所以它不是信任根；只接受编译进安装器的受信 keyId 对应公钥。
        /// </summary>
        private bool TryPrepareTrustedUnityPackages(
            ESPackageBase package,
            out List<VerifiedUnityPackage> verifiedPackages,
            out string error)
        {
            verifiedPackages = new List<VerifiedUnityPackage>();
            error = string.Empty;
            string stagingDirectory = null;
            try
            {
                if (package == null || string.IsNullOrWhiteSpace(package.packageId) || string.IsNullOrWhiteSpace(package.version))
                    throw new InvalidDataException("安装包必须声明 packageId 和 version，才能建立可追溯供应链身份。");

                string packageDirectory = GetSafePackageFolderPath(package);
                if (string.IsNullOrWhiteSpace(packageDirectory) || !Directory.Exists(packageDirectory))
                    throw new DirectoryNotFoundException("安装包目录不存在或越出 Downloads 受管根目录。");

                ESManagedFileIO.EnsurePath(packageDirectory, false, downloadsFolderPath);
                ESManagedFileIO.EnsureNoNestedReparsePoints(packageDirectory);

                string manifestPath = Path.Combine(packageDirectory, UnityPackageTrustManifestFileName);
                ESManagedFileIO.EnsurePath(manifestPath, true, packageDirectory);
                if (!File.Exists(manifestPath))
                    throw new FileNotFoundException("缺少已签名 .unitypackage 清单：" + UnityPackageTrustManifestFileName, manifestPath);

                UnityPackageTrustManifest manifest = JsonUtility.FromJson<UnityPackageTrustManifest>(File.ReadAllText(manifestPath, Encoding.UTF8));
                if (!TryValidateUnityPackageManifest(package, packageDirectory, manifest, out List<UnityPackageTrustArtifact> artifacts, out string validationError))
                    throw new InvalidDataException(validationError);

                string stagingRoot = GetTrustedImportStagingRoot();
                EnsureInstallerLibraryDirectory(stagingRoot);
                stagingDirectory = Path.Combine(stagingRoot, "batch-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(stagingDirectory);
                ESManagedFileIO.EnsurePath(stagingDirectory, false, stagingRoot);
                ESManagedFileIO.EnsureNoNestedReparsePoints(stagingDirectory);

                foreach (UnityPackageTrustArtifact artifact in artifacts)
                {
                    if (!TryResolveManifestArtifactPath(packageDirectory, artifact.relativePath, out string sourcePath, out string pathError))
                        throw new InvalidDataException(pathError);
                    ESManagedFileIO.EnsurePath(sourcePath, true, packageDirectory);
                    if ((File.GetAttributes(sourcePath) & FileAttributes.ReparsePoint) != 0)
                        throw new UnauthorizedAccessException("安装包文件不能是 junction/symlink：" + sourcePath);

                    if (!ESArtifactTrustVerifier.TryCaptureStableFileIdentity(sourcePath, out long sourceSize, out string sourceHash, out string identityError))
                        throw new IOException("无法建立安装包文件身份：" + identityError);
                    if (sourceSize != artifact.size || !string.Equals(sourceHash, artifact.sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("安装包 Hash 或 Size 与已签名清单不一致：" + artifact.relativePath);

                    string stagedPath = Path.Combine(stagingDirectory, Path.GetFileName(sourcePath));
                    ESManagedFileIO.CopyFileAtomic(sourcePath, stagedPath, packageDirectory, stagingDirectory);
                    if (!ESArtifactTrustVerifier.TryCaptureStableFileIdentity(stagedPath, out long stagedSize, out string stagedHash, out identityError)
                        || stagedSize != artifact.size
                        || !string.Equals(stagedHash, artifact.sha256, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("受信暂存文件 Hash 或 Size 不一致：" + artifact.relativePath + "；" + identityError);

                    verifiedPackages.Add(new VerifiedUnityPackage
                    {
                        packageId = manifest.packageId,
                        packageVersion = manifest.packageVersion,
                        source = manifest.source,
                        keyId = manifest.keyId,
                        signature = manifest.signature,
                        originalPath = sourcePath,
                        stagedPath = stagedPath,
                        stagingDirectory = stagingDirectory,
                        size = artifact.size,
                        sha256 = artifact.sha256.ToLowerInvariant(),
                    });
                }

                return true;
            }
            catch (Exception exception)
            {
                if (!string.IsNullOrWhiteSpace(stagingDirectory) && Directory.Exists(stagingDirectory))
                {
                    try
                    {
                        ESManagedFileIO.DeleteDirectory(stagingDirectory, GetTrustedImportStagingRoot());
                    }
                    catch (Exception cleanupException)
                    {
                        error = "受信安装预检失败：" + exception.Message + "；暂存现场保留：" + stagingDirectory + "；清理失败：" + cleanupException.Message;
                        return false;
                    }
                }

                error = "受信安装预检失败：" + exception.Message;
                return false;
            }
        }

        private static bool TryValidateUnityPackageManifest(
            ESPackageBase package,
            string packageDirectory,
            UnityPackageTrustManifest manifest,
            out List<UnityPackageTrustArtifact> artifacts,
            out string error)
        {
            artifacts = new List<UnityPackageTrustArtifact>();
            error = string.Empty;
            if (manifest == null || manifest.schemaVersion != 1)
            {
                error = "仅支持 schemaVersion=1 的已签名 .unitypackage 清单。";
                return false;
            }

            if (!string.Equals(manifest.packageId, package.packageId, StringComparison.Ordinal)
                || !string.Equals(manifest.packageVersion, package.version, StringComparison.Ordinal))
            {
                error = "已签名清单的 packageId/version 与当前安装配置不一致。";
                return false;
            }

            if (!IsSafeManifestValue(manifest.keyId, false)
                || !IsSafeManifestValue(manifest.packageId, false)
                || !IsSafeManifestValue(manifest.packageVersion, false)
                || !IsSafeManifestValue(manifest.source, false)
                || !IsSafeTrustKeyId(manifest.keyId))
            {
                error = "已签名清单包含无效 keyId、packageId、version 或 source。";
                return false;
            }

            if (!ESInstallerTrustedKeys.TryGetRsaPublicKey(manifest.keyId, out string publicKeyXml))
            {
                error = "未配置 keyId“" + manifest.keyId + "”的受信发布公钥；安装器按 fail-closed 拒绝导入。";
                return false;
            }

            string[] packageFiles = Directory.GetFiles(packageDirectory, "*.unitypackage", SearchOption.TopDirectoryOnly);
            if (packageFiles.Length == 0)
            {
                error = "安装包目录中没有 .unitypackage 文件。";
                return false;
            }

            var filesByName = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string packageFile in packageFiles)
            {
                string fileName = Path.GetFileName(packageFile);
                if (string.IsNullOrWhiteSpace(fileName) || filesByName.ContainsKey(fileName))
                {
                    error = "安装包目录存在名称冲突的 .unitypackage 文件。";
                    return false;
                }
                filesByName.Add(fileName, packageFile);
            }

            if (manifest.artifacts == null || manifest.artifacts.Count == 0 || manifest.artifacts.Count != filesByName.Count)
            {
                error = "已签名清单必须逐一且仅一次声明目录内所有 .unitypackage 文件。";
                return false;
            }

            var manifestNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (UnityPackageTrustArtifact artifact in manifest.artifacts)
            {
                if (artifact == null)
                {
                    error = "已签名清单包含无效 artifact。";
                    return false;
                }

                string resolvedPath;
                string artifactPathError;
                if (!TryResolveManifestArtifactPath(packageDirectory, artifact.relativePath, out resolvedPath, out artifactPathError)
                    || !IsSafeManifestValue(artifact.relativePath, false)
                    || artifact.size <= 0
                    || !ESArtifactTrustVerifier.IsSha256(artifact.sha256))
                {
                    error = "已签名清单包含无效 artifact：" + artifactPathError;
                    return false;
                }

                string fileName = Path.GetFileName(resolvedPath);
                if (!manifestNames.Add(fileName) || !filesByName.ContainsKey(fileName))
                {
                    error = "已签名清单存在重复、未知或未声明的 .unitypackage 文件。";
                    return false;
                }

                artifacts.Add(new UnityPackageTrustArtifact
                {
                    relativePath = artifact.relativePath,
                    size = artifact.size,
                    sha256 = artifact.sha256.ToLowerInvariant(),
                });
            }

            if (manifestNames.Count != filesByName.Count)
            {
                error = "目录内存在未被已签名清单声明的 .unitypackage 文件。";
                return false;
            }

            byte[] canonicalPayload = Encoding.UTF8.GetBytes(BuildCanonicalManifestPayload(manifest, artifacts));
            if (!ESArtifactTrustVerifier.TryVerifyRsaSha256(publicKeyXml, canonicalPayload, manifest.signature, out string signatureError))
            {
                error = "已签名清单验证失败：" + signatureError;
                return false;
            }

            artifacts.Sort((left, right) => StringComparer.Ordinal.Compare(left.relativePath, right.relativePath));
            return true;
        }

        private static string BuildCanonicalManifestPayload(
            UnityPackageTrustManifest manifest,
            List<UnityPackageTrustArtifact> artifacts)
        {
            return ESInstallerPackageTrust.BuildCanonicalManifestPayload(manifest, artifacts);
        }

        private static bool TryResolveManifestArtifactPath(string packageDirectory, string relativePath, out string fullPath, out string error)
        {
            fullPath = null;
            error = string.Empty;
            string normalized = (relativePath ?? string.Empty).Trim().Replace('\\', '/');
            if (string.IsNullOrEmpty(normalized)
                || Path.IsPathRooted(normalized)
                || normalized.Contains("..")
                || normalized.IndexOf('/') >= 0
                || normalized.IndexOf(':') >= 0
                || !normalized.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
            {
                error = "artifact.relativePath 必须是包目录内的单个 .unitypackage 文件名。";
                return false;
            }

            try
            {
                string candidate = Path.GetFullPath(Path.Combine(packageDirectory, normalized));
                if (!ESManagedFileIO.IsWithinRoot(candidate, packageDirectory)
                    || !string.Equals(Path.GetFileName(candidate), normalized, StringComparison.Ordinal))
                {
                    error = "artifact.relativePath 越出包目录。";
                    return false;
                }

                fullPath = candidate;
                return true;
            }
            catch (Exception exception)
            {
                error = "artifact.relativePath 无效：" + exception.Message;
                return false;
            }
        }

        private static bool IsSafeTrustKeyId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64) return false;
            foreach (char character in value)
            {
                bool permitted = character >= 'a' && character <= 'z'
                    || character >= 'A' && character <= 'Z'
                    || character >= '0' && character <= '9'
                    || character == '.' || character == '_' || character == '-';
                if (!permitted) return false;
            }

            return true;
        }

        private static bool IsSafeManifestValue(string value, bool allowEmpty)
        {
            if (string.IsNullOrEmpty(value)) return allowEmpty;
            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal) || value.Length > 2048) return false;
            foreach (char character in value)
            {
                if (char.IsControl(character)) return false;
            }

            return true;
        }

        private string GetTrustedImportStagingRoot()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, "Library", "ESInstaller", "VerifiedImports");
        }

        private string GetTrustedImportReceiptRoot()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, "Library", "ESInstaller", "ImportReceipts");
        }

        private static void EnsureInstallerLibraryDirectory(string directory)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string libraryRoot = Path.Combine(projectRoot, "Library");
            Directory.CreateDirectory(directory);
            ESManagedFileIO.EnsurePath(directory, false, libraryRoot);
            ESManagedFileIO.EnsureNoNestedReparsePoints(directory);
        }

        private bool TryQueueTrustedImports(List<VerifiedUnityPackage> verifiedPackages, string operationName, out string error)
        {
            error = string.Empty;
            if (verifiedPackages == null || verifiedPackages.Count == 0)
            {
                error = "没有通过签名与 Hash 校验的 .unitypackage 可导入。";
                return false;
            }

            if (activeVerifiedImport != null || verifiedImportQueue.Count > 0)
            {
                error = "已有受信安装任务正在等待 Unity 导入结果；请先完成、取消或关闭当前导入面板。";
                return false;
            }

            foreach (VerifiedUnityPackage package in verifiedPackages)
                verifiedImportQueue.Enqueue(package);

            ShowStatus(operationName + "：已完成签名、Size、SHA-256 与受信暂存校验，准备导入 " + verifiedPackages.Count + " 个包。", MessageType.Info);
            StartNextTrustedImport();
            return true;
        }

        private static bool ConfirmTrustedImportPreview(
            IReadOnlyList<VerifiedUnityPackage> verifiedPackages,
            string operationName)
        {
            if (verifiedPackages == null || verifiedPackages.Count == 0)
                return false;

            long totalBytes = 0;
            bool containsDevelopmentSignature = false;
            var preview = new StringBuilder();
            for (int i = 0; i < verifiedPackages.Count; i++)
            {
                VerifiedUnityPackage package = verifiedPackages[i];
                totalBytes += package.size;
                containsDevelopmentSignature |= string.Equals(
                    package.keyId,
                    ESInstallerPackageTrust.LocalDevelopmentKeyId,
                    StringComparison.Ordinal);
                preview.Append("• ")
                    .Append(Path.GetFileName(package.originalPath))
                    .Append(" | ")
                    .Append(package.packageVersion)
                    .Append(" | ")
                    .Append(package.size / 1024L)
                    .Append(" KB | SHA-256 ")
                    .Append(package.sha256.Substring(0, Math.Min(12, package.sha256.Length)))
                    .Append("…\n");
            }

            string trustWarning = containsDevelopmentSignature
                ? "\n警告：其中包含仅本机受信的开发签名，不能作为生产发布证据。\n"
                : string.Empty;
            return EditorUtility.DisplayDialog(
                operationName + "影响预览",
                "以下包已通过签名、Size、SHA-256 和可信暂存校验：\n\n"
                + preview
                + "\n合计：" + verifiedPackages.Count + " 个包，" + (totalBytes / 1024L) + " KB。"
                + trustWarning
                + "\n继续后将打开 Unity 标准 Import Package 面板；最终导入内容仍由你确认。",
                "继续导入",
                "取消");
        }

        private void StartNextTrustedImport()
        {
            if (activeVerifiedImport != null) return;
            if (verifiedImportQueue.Count == 0)
            {
                ShowStatus("所有受信 .unitypackage 导入请求均已处理；可通过“定位上次导入收据”查看结果。", MessageType.Info);
                return;
            }

            activeVerifiedImport = verifiedImportQueue.Dequeue();
            try
            {
                if (!ESArtifactTrustVerifier.TryCaptureStableFileIdentity(
                        activeVerifiedImport.stagedPath,
                        out long actualSize,
                        out string actualHash,
                        out string identityError)
                    || actualSize != activeVerifiedImport.size
                    || !string.Equals(actualHash, activeVerifiedImport.sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("导入前受信暂存文件身份不一致：" + identityError);
                }

                activeVerifiedImport.receiptId = Guid.NewGuid().ToString("N");
                string receiptRoot = GetTrustedImportReceiptRoot();
                EnsureInstallerLibraryDirectory(receiptRoot);
                activeVerifiedImport.receiptPath = Path.Combine(receiptRoot, activeVerifiedImport.receiptId + ".json");
                WriteTrustedImportReceipt(activeVerifiedImport, "ImportRequested", string.Empty, string.Empty, true);
                lastImportReceiptPath = activeVerifiedImport.receiptPath;
                ShowStatus("正在导入已验证包：" + Path.GetFileName(activeVerifiedImport.originalPath) + "。Unity 导入面板关闭后将写入最终收据。", MessageType.Info);
                AssetDatabase.ImportPackage(activeVerifiedImport.stagedPath, true);
            }
            catch (Exception exception)
            {
                CompleteActiveTrustedImport("ImportFailed", string.Empty, exception.Message);
            }
        }

        private void OnTrustedImportCompleted(string packageName)
        {
            if (activeVerifiedImport == null) return;
            CompleteActiveTrustedImport("ImportCompleted", packageName, string.Empty);
        }

        private void OnTrustedImportCancelled(string packageName)
        {
            if (activeVerifiedImport == null) return;
            CompleteActiveTrustedImport("ImportCancelled", packageName, "用户取消了 Unity 导入面板。");
        }

        private void OnTrustedImportFailed(string packageName, string message)
        {
            if (activeVerifiedImport == null) return;
            CompleteActiveTrustedImport("ImportFailed", packageName, message ?? string.Empty);
        }

        private void CompleteActiveTrustedImport(string result, string unityPackageName, string detail)
        {
            VerifiedUnityPackage completed = activeVerifiedImport;
            activeVerifiedImport = null;
            bool integrityPreserved = ESArtifactTrustVerifier.TryCaptureStableFileIdentity(
                completed.stagedPath,
                out long actualSize,
                out string actualHash,
                out string identityError)
                && actualSize == completed.size
                && string.Equals(actualHash, completed.sha256, StringComparison.OrdinalIgnoreCase);
            if (!integrityPreserved)
            {
                result = "ImportFailed";
                detail = string.IsNullOrWhiteSpace(detail)
                    ? "导入完成信号后无法复核受信暂存文件身份：" + identityError
                    : detail + "；导入完成信号后无法复核受信暂存文件身份：" + identityError;
            }

            try
            {
                WriteTrustedImportReceipt(completed, result, unityPackageName, detail, false);
                lastImportReceiptPath = completed.receiptPath;
            }
            catch (Exception receiptException)
            {
                result = "ImportFailed";
                detail = "导入状态收据写入失败：" + receiptException.Message;
            }

            if (!string.Equals(result, "ImportCompleted", StringComparison.Ordinal))
            {
                CancelQueuedTrustedImports("前一个受信安装未完成，剩余包未导入：" + detail);
                CleanupTrustedStagingDirectory(completed.stagingDirectory);
                ShowStatus("受信安装失败或已取消：" + detail + "。已停止后续导入；收据：" + completed.receiptPath, MessageType.Error);
                return;
            }

            CleanupTrustedStagingDirectoryIfUnused(completed.stagingDirectory);
            ShowStatus("已记录导入完成：" + Path.GetFileName(completed.originalPath) + "；收据：" + completed.receiptPath, MessageType.Info);
            StartNextTrustedImport();
        }

        private void CancelQueuedTrustedImports(string detail)
        {
            while (verifiedImportQueue.Count > 0)
            {
                VerifiedUnityPackage queued = verifiedImportQueue.Dequeue();
                queued.receiptId = Guid.NewGuid().ToString("N");
                string receiptRoot = GetTrustedImportReceiptRoot();
                try
                {
                    EnsureInstallerLibraryDirectory(receiptRoot);
                    queued.receiptPath = Path.Combine(receiptRoot, queued.receiptId + ".json");
                    WriteTrustedImportReceipt(queued, "NotImported", string.Empty, detail, true);
                    lastImportReceiptPath = queued.receiptPath;
                }
                catch (Exception exception)
                {
                    Debug.LogError("[ESInstaller] 无法写入未导入包的受信收据：" + exception);
                }
                CleanupTrustedStagingDirectoryIfUnused(queued.stagingDirectory);
            }
        }

        private void WriteTrustedImportReceipt(
            VerifiedUnityPackage package,
            string result,
            string unityPackageName,
            string detail,
            bool createNew)
        {
            if (package == null || string.IsNullOrWhiteSpace(package.receiptPath))
                throw new InvalidOperationException("受信安装收据路径尚未分配。");

            long actualSize = 0;
            string actualHash = string.Empty;
            string identityError = string.Empty;
            ESArtifactTrustVerifier.TryCaptureStableFileIdentity(package.stagedPath, out actualSize, out actualHash, out identityError);
            var receipt = new UnityPackageImportReceipt
            {
                receiptId = package.receiptId,
                recordedAtUtc = DateTime.UtcNow.ToString("O"),
                result = result ?? string.Empty,
                unityPackageName = unityPackageName ?? string.Empty,
                packageId = package.packageId,
                packageVersion = package.packageVersion,
                source = package.source,
                keyId = package.keyId,
                originalPath = package.originalPath,
                expectedSize = package.size,
                expectedSha256 = package.sha256,
                actualSize = actualSize,
                actualSha256 = actualHash,
                detail = string.IsNullOrWhiteSpace(identityError) ? detail ?? string.Empty : (detail ?? string.Empty) + "；身份复核：" + identityError,
            };

            string json = JsonUtility.ToJson(receipt, true);
            if (createNew)
                ESManagedFileIO.WriteTextAtomicCreateNew(package.receiptPath, json, new UTF8Encoding(false), GetTrustedImportReceiptRoot());
            else
                ESManagedFileIO.WriteTextAtomic(package.receiptPath, json, new UTF8Encoding(false), GetTrustedImportReceiptRoot());
        }

        private void CleanupTrustedStagingDirectoryIfUnused(string stagingDirectory)
        {
            if (string.IsNullOrWhiteSpace(stagingDirectory)) return;
            if (activeVerifiedImport != null && string.Equals(activeVerifiedImport.stagingDirectory, stagingDirectory, StringComparison.OrdinalIgnoreCase)) return;
            foreach (VerifiedUnityPackage queued in verifiedImportQueue)
            {
                if (string.Equals(queued.stagingDirectory, stagingDirectory, StringComparison.OrdinalIgnoreCase)) return;
            }
            CleanupTrustedStagingDirectory(stagingDirectory);
        }

        private void CleanupTrustedStagingDirectory(string stagingDirectory)
        {
            if (string.IsNullOrWhiteSpace(stagingDirectory) || !Directory.Exists(stagingDirectory)) return;
            try
            {
                ESManagedFileIO.DeleteDirectory(stagingDirectory, GetTrustedImportStagingRoot());
            }
            catch (Exception exception)
            {
                Debug.LogError("[ESInstaller] 受信安装暂存目录清理失败，已保留现场：" + stagingDirectory + "；" + exception);
            }
        }

        /// <summary>
        /// Git URL 只能引用不可变完整 commit。branch、tag 和短 SHA 都会移动或产生歧义，
        /// 不能作为安装器的供应链身份。UPM 仍使用原始 URL 安装，但所有入口都必须先经过本门禁。
        /// </summary>
        private static bool TryValidatePinnedGitUrl(string gitUrl, out string normalizedUrl, out string commit, out string error)
        {
            normalizedUrl = (gitUrl ?? string.Empty).Trim();
            commit = string.Empty;
            error = string.Empty;
            if (string.IsNullOrEmpty(normalizedUrl))
            {
                error = "Git URL 为空。";
                return false;
            }

            int markerIndex = normalizedUrl.LastIndexOf('#');
            if (markerIndex <= 0 || markerIndex == normalizedUrl.Length - 1)
            {
                error = "Git URL 必须以 # 加完整 commit SHA（40 或 64 位十六进制）结尾。";
                return false;
            }

            string source = normalizedUrl.Substring(0, markerIndex);
            commit = normalizedUrl.Substring(markerIndex + 1);
            if (!IsSupportedGitSource(source))
            {
                error = "Git URL 仅支持 git+https、https、ssh 或 git@ 远端来源。";
                return false;
            }
            if ((commit.Length != 40 && commit.Length != 64) || !commit.All(Uri.IsHexDigit))
            {
                error = "Git URL 只能使用完整 40 或 64 位十六进制 commit SHA，不能使用 branch、tag 或短 SHA。";
                return false;
            }

            commit = commit.ToLowerInvariant();
            normalizedUrl = source + "#" + commit;
            return true;
        }

        private static bool IsSupportedGitSource(string source)
        {
            return source.StartsWith("git+https://", StringComparison.OrdinalIgnoreCase)
                || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || source.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase)
                || source.StartsWith("git@", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryValidatePackageGitDependencies(ESPackageBase package, out string error)
        {
            error = string.Empty;
            if (package == null)
            {
                error = "安装包配置为空。";
                return false;
            }

            foreach (GitPackageDependency dependency in package.gitDependencies ?? new List<GitPackageDependency>())
            {
                if (dependency == null)
                {
                    error = "Git 依赖为空。";
                    return false;
                }
                if (!TryValidatePinnedGitUrl(dependency.gitUrl, out _, out _, out string pinError))
                {
                    error = "Git 依赖“" + dependency.name + "”不满足固定 commit 要求：" + pinError;
                    return false;
                }
            }

            return true;
        }

        private static void DrawGitSupplyChainStatus(GitPackageDependency dependency)
        {
            if (dependency == null)
            {
                EditorGUILayout.HelpBox("已阻断安装：Git 依赖为空。", MessageType.Error);
                return;
            }

            if (TryValidatePinnedGitUrl(dependency.gitUrl, out _, out string commit, out string error))
            {
                EditorGUILayout.LabelField("供应链固定 Commit", commit, EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.HelpBox("已阻断安装：" + error, MessageType.Error);
        }

        private static bool TryResolveProjectAssetPath(string assetPath, out string fullPath)
        {
            fullPath = null;
            string normalized = (assetPath ?? string.Empty).Trim().Replace('\\', '/');
            if (normalized.Length == 0
                || normalized.Contains("://", StringComparison.Ordinal)
                || normalized.StartsWith("jar:", StringComparison.OrdinalIgnoreCase)
                || Path.IsPathRooted(normalized)
                || (!normalized.Equals("Assets", StringComparison.OrdinalIgnoreCase)
                    && !normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)))
                return false;

            string[] segments = normalized.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == ".." || segments[i].IndexOf(':') >= 0)
                    return false;
            }

            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string candidate = Path.GetFullPath(Path.Combine(projectRoot, normalized));
                string assetsRoot = Path.GetFullPath(Application.dataPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string requiredPrefix = assetsRoot + Path.DirectorySeparatorChar;
                if (!candidate.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(candidate, assetsRoot, StringComparison.OrdinalIgnoreCase))
                    return false;

                fullPath = candidate;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void LoadConfiguration()
        {
            currentProfile = LoadCanonicalInstallationProfile();
            RebuildPackageUiIndex(currentProfile);
        }

        private void RebuildPackageUiIndex(InstallationProfile profile)
        {
            availablePackageIds.Clear();
            packageDisplayNames.Clear();

            ESMainPackage mainPackage = profile?.mainPackage;
            if (mainPackage == null)
                return;

            mainPackage.packageId = "es_main";
            availablePackageIds.Add(mainPackage.packageId);
            packageDisplayNames[mainPackage.packageId] =
                string.IsNullOrWhiteSpace(mainPackage.displayName)
                    ? "ES Framework 主包 (必需)"
                    : $"{mainPackage.displayName} (必需)";

            foreach (ESExtensionPackage package in
                     (IEnumerable<ESExtensionPackage>)profile.extensionPackages
                     ?? Enumerable.Empty<ESExtensionPackage>())
            {
                if (package == null || string.IsNullOrWhiteSpace(package.packageId))
                    continue;
                if (packageDisplayNames.ContainsKey(package.packageId))
                {
                    Debug.LogWarning($"ES Installer 忽略重复包 ID: {package.packageId}");
                    continue;
                }

                availablePackageIds.Add(package.packageId);
                packageDisplayNames[package.packageId] =
                    string.IsNullOrWhiteSpace(package.displayName)
                        ? package.folderName
                        : $"{package.displayName} v{package.version}";
            }

            if (!packageDisplayNames.ContainsKey(currentSelectedPackageId))
                currentSelectedPackageId = mainPackage.packageId;
        }

        private static string GetAutoCheckPreferenceKey()
        {
            return AutoCheckPreferencePrefix + GetProjectIdentityHash();
        }

        private static string GetSkipNextAutoCheckSessionKey()
        {
            return SkipNextAutoCheckSessionPrefix + GetProjectIdentityHash();
        }

        private static string GetProjectIdentityHash()
        {
            string projectRoot = (Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath)
                .Replace('\\', '/')
                .TrimEnd('/')
                .ToLowerInvariant();
            byte[] bytes = Encoding.UTF8.GetBytes(projectRoot);
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(16);
                for (int i = 0; i < 8; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        /// <summary>
        /// 异步检查所有包的安装状态
        /// </summary>
        private async Task CheckAllPackagesInstallStateAsync()
        {
            // 检查主包
            if (currentProfile.mainPackage != null)
            {
                _ = CheckPackageInstallStateAsync(currentProfile.mainPackage);
            }

            // 检查所有扩展包
            if (currentProfile.extensionPackages != null)
            {
                foreach (var package in currentProfile.extensionPackages)
                {
                    _ = CheckPackageInstallStateAsync(package);
                }
            }

            // 检查依赖
            await CheckPackageDependenciesAsync(currentProfile.mainPackage);


            Repaint(); // 刷新UI
        }

        /// <summary>
        /// 异步检查单个包的安装状态
        /// </summary>
        private async Task CheckPackageInstallStateAsync(ESPackageBase package)
        {
            await Task.Run(() =>
          {
              // 检查文件夹是否存在
              string packagePath = GetSafePackageFolderPath(package);

              if (string.IsNullOrEmpty(packagePath) || !Directory.Exists(packagePath))
              {
                  package.installState = PackageInstallState.NotInstalled;
                  return;
              }

              // 检查是否有.unitypackage文件
              string[] packageFiles = Directory.GetFiles(packagePath, "*.unitypackage");

              if (packageFiles.Length == 0)
              {
                  package.installState = PackageInstallState.NotInstalled;
                  return;
              }

              // 如果有checkClass，检查类是否存在
              if (!string.IsNullOrEmpty(package.checkClass))
              {
                  bool classExists = IsClassExists(package.checkClass);
                  if (classExists)
                  {
                      package.installState = PackageInstallState.Installed;
                  }
                  else
                  {
                      package.installState = PackageInstallState.NotInstalled;
                  }
              }
              // 如果有assetPath，检查资产路径是否存在
              else if (!string.IsNullOrEmpty(package.assetPath))
              {
                  if (!TryResolveProjectAssetPath(package.assetPath, out string fullPath))
                  {
                      package.installState = PackageInstallState.NotInstalled;
                      return;
                  }
                  // Debug.Log("Checking asset path: " + fullPath);
                  if (File.Exists(fullPath) || Directory.Exists(fullPath))
                  {
                      package.installState = PackageInstallState.Installed;
                  }
                  else
                  {
                      package.installState = PackageInstallState.NotInstalled;
                  }
              }
              else
              {
                  // 没有checkClass或assetPath，只要有package文件就认为可以安装（但不一定已安装）
                  package.installState = PackageInstallState.NotInstalled;
              }
          });

            // 如果是主包，更新isMainPackageInstalled
            if (package.packageId == "es_main")
            {
                isMainPackageInstalled = package.installState == PackageInstallState.Installed;
            }

            EditorApplication.delayCall += () => Repaint(); // 在主线程刷新UI
        }

        /// <summary>
        /// 异步检查单个包的依赖
        /// </summary>
        private async Task CheckPackageDependenciesAsync(ESPackageBase package)
        {
            if (package == null) return;
            InstalledPackageSnapshot snapshot = await CaptureInstalledPackageSnapshotAsync();
            if (!snapshot.IsAvailable)
            {
                ShowStatus(snapshot.FailureMessage, MessageType.Error);
                return;
            }
            UpmPackageInfo[] installedPackages = snapshot.Packages;

            // 检查Unity包
            if (package.unityDependencies != null)
            {
                foreach (var dep in package.unityDependencies)
                {
                    if (dep != null)
                        dep.isInstalled = CheckUnityPackageInstalled(dep, installedPackages);
                }
            }

            // 检查Git包
            if (package.gitDependencies != null)
            {
                foreach (var dep in package.gitDependencies)
                {
                    if (dep != null)
                        dep.isInstalled = CheckGitPackageInstalled(dep, installedPackages);
                }
            }

            // 检查用户包
            if (package.userDependencies != null)
            {
                foreach (var dep in package.userDependencies)
                {
                    if (dep != null)
                        dep.isInstalled = IsUserPackageInstalled(dep);
                }
            }

            // 检查资产文件
            if (package.assetFileDependencies != null)
            {
                foreach (var dep in package.assetFileDependencies)
                {
                    if (dep != null)
                        dep.isInstalled = CheckAssetFileInstalled(dep);
                }
            }
        }

        /// <summary>
        /// 同步检查资产文件是否已安装
        /// </summary>
        private static bool CheckAssetFileInstalled(AssetFileDependency dependency)
        {
            // 检查资产路径是否存在
            if (string.IsNullOrEmpty(dependency.assetPath))
                return false;

            // 如果指定了checkClass，优先检查类是否存在
            if (!string.IsNullOrEmpty(dependency.checkClass))
            {
                if (IsClassExists(dependency.checkClass))
                {
                    return true;
                }
            }

            // 检查文件是否存在
            return TryResolveProjectAssetPath(dependency.assetPath, out string fullPath)
                && (File.Exists(fullPath) || Directory.Exists(fullPath));
        }

        /// <summary>
        /// 检查主包是否已安装
        /// </summary>
        private void CheckMainPackageInstallation()
        {
            // 【主包安装验证】：这里是主包是否安装的核心验证逻辑
            // 首先检查类是否存在（如果指定了checkClass）
            if (!string.IsNullOrEmpty(currentProfile.mainPackage.checkClass))
            {
                if (IsClassExists(currentProfile.mainPackage.checkClass))
                {
                    isMainPackageInstalled = true;
                    currentProfile.mainPackage.installState = PackageInstallState.Installed;
                    return;
                }
                else
                {
                    isMainPackageInstalled = false;
                    currentProfile.mainPackage.installState = PackageInstallState.NotInstalled;
                    return;
                }
            }

            // 如果没有类检查，则检查文件夹和文件是否存在
            string mainPackagePath = GetSafePackageFolderPath(currentProfile.mainPackage);

            // 检查主包文件夹是否存在
            if (!Directory.Exists(mainPackagePath))
            {
                isMainPackageInstalled = false;
                currentProfile.mainPackage.installState = PackageInstallState.NotInstalled;
                return;
            }

            // 检查是否有Unity Package文件
            string[] scannedFiles = Directory.GetFiles(mainPackagePath, "*.unitypackage");
            bool hasPackageFiles = scannedFiles.Length > 0;

            if (!hasPackageFiles)
            {
                isMainPackageInstalled = false;
                currentProfile.mainPackage.installState = PackageInstallState.NotInstalled;
                return;
            }

            // 如果文件夹和文件都存在，认为主包已安装
            isMainPackageInstalled = true;
            currentProfile.mainPackage.installState = PackageInstallState.Installed;
        }

        /// <summary>
        /// 检查扩展包是否已安装
        /// </summary>
        private void CheckExtensionPackageInstallation(ESExtensionPackage package)
        {
            // 首先检查类是否存在（如果指定了checkClass）
            if (!string.IsNullOrEmpty(package.checkClass))
            {
                package.installState = IsClassExists(package.checkClass) ? PackageInstallState.Installed : PackageInstallState.NotInstalled;
                return;
            }

            // 如果没有类检查，则检查文件夹是否存在（已安装的包应该有文件夹记录）
            // 注意：扩展包安装后可能没有保留文件夹，所以主要依赖类检查
            // 这里简化为保持当前状态，除非有其他逻辑
            // 可以考虑添加更复杂的检查，比如检查特定的资源文件
        }

        [System.Serializable]
        private class ExtensionPackageJsonData
        {
            public string displayName;
            public string folderName;
            public string version;
            public string description;
            public DependencyJsonData[] unityDependencies; // Unity包依赖
            public GitDependencyJsonData[] gitDependencies; // Git包依赖
            public UserDependencyJsonData[] userDependencies; // 用户包依赖
            public AssetFileDependencyJsonData[] assetFileDependencies; // 资产文件依赖
            public string[] requiredMainPackages; // 依赖的主包
            public string installationNotes;
            public string checkClass; // 可选：用于验证安装状态的完整类名
            public string assetPath; // 可选：用于验证安装状态的资产路径
            public string[] tags;
            public string author;
            public string website;
            public string license;
        }

        [System.Serializable]
        private class DependencyJsonData
        {
            public string name;
            public string version;
            public string description;
            public bool isRequired;
            public string checkClass; // 可选：用于验证安装状态的完整类名
            public string packageId; // Unity Package Manager ID
            public string installUrl;
        }

        [System.Serializable]
        private class GitDependencyJsonData
        {
            public string name;
            public string version;
            public string description;
            public string gitUrl;
            public string checkClass;
            public bool isRequired;
        }

        [System.Serializable]
        private class UserDependencyJsonData
        {
            public string name;
            public string version;
            public string description;
            public string checkClass;
            public string installInstructions;
            public bool isRequired;
        }

        [System.Serializable]
        private class AssetFileDependencyJsonData
        {
            public string name;
            public string version;
            public string description;
            public string assetPath;
            public string checkClass;
            public bool isRequired;
        }

        protected override void ESWindow_OnHostDisable()
        {
            AssetDatabase.importPackageCompleted -= OnTrustedImportCompleted;
            AssetDatabase.importPackageCancelled -= OnTrustedImportCancelled;
            AssetDatabase.importPackageFailed -= OnTrustedImportFailed;
            if (installer == this)
                installer = null;

            // 只有在有未保存的更改时才询问用户是否保存
            if (isConfigModified)
            {
                bool saveChanges = EditorUtility.DisplayDialog(
                    "保存配置",
                    "配置已被修改，是否保存更改？",
                    "保存",
                    "不保存"
                );

                if (saveChanges)
                {
                    SaveConfiguration();
                }
                else
                {
                    // 重新加载配置以撤销更改
                    // LoadConfiguration();
                }
            }
        }

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
            InitializeStyles();

            // 确保配置已加载
            if (currentProfile == null)
            {
                InitializePaths();
                LoadConfiguration();
            }

            // 标题
            EditorGUILayout.LabelField("ES 框架安装管理器", headerStyle);
            EditorGUILayout.Space();

            // 顶部包选择器
            DrawPackageSelector();
            EditorGUILayout.Space(5);

            // 状态信息
            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
                EditorGUILayout.Space();
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // 根据当前选中的包显示内容
            if (currentSelectedPackageId == "es_main")
            {
                DrawPackageContent(currentProfile.mainPackage);
            }
            else
            {
                var currentPackage = currentProfile.extensionPackages.FirstOrDefault(p => p.packageId == currentSelectedPackageId);
                if (currentPackage != null)
                {
                    DrawPackageContent(currentPackage);
                }
                else
                {
                    EditorGUILayout.HelpBox("未找到当前选中的扩展包配置", MessageType.Error);
                }
            }

            EditorGUILayout.EndScrollView();


            showDebug = EditorGUILayout.Foldout(showDebug, "Debug");
            if (showDebug)
            {
                if (GUILayout.Button("输出InstallationProfile信息"))
                {
                    if (installer.currentProfile != null)
                    {
                        string json = JsonUtility.ToJson(installer.currentProfile, true);
                        Debug.Log("InstallationProfile: " + json);
                    }
                }

                if (GUILayout.Button("输出availablePackageIds信息"))
                {
                    string ids = string.Join(", ", availablePackageIds);
                    Debug.Log("availablePackageIds: " + ids);
                    EditorUtility.DisplayDialog("Debug", "availablePackageIds信息已输出到控制台", "OK");
                }
            }
            EditorGUILayout.Space();

            // 底部按钮
            DrawBottomButtons();
        }

        #endregion

        #region UI绘制方法

        private void InitializeStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel);
                headerStyle.fontSize = 18;
                headerStyle.alignment = TextAnchor.MiddleCenter;
                headerStyle.margin = new RectOffset(0, 0, 10, 10);
            }

            if (sectionStyle == null)
            {
                sectionStyle = new GUIStyle(EditorStyles.foldout);
                sectionStyle.fontStyle = FontStyle.Bold;
            }

            if (statusStyle == null)
            {
                statusStyle = new GUIStyle(EditorStyles.helpBox);
            }

            if (packageNameStyle == null)
            {
                packageNameStyle = new GUIStyle(EditorStyles.label);
                packageNameStyle.fontStyle = FontStyle.Bold;
                packageNameStyle.fontSize = 12;
                packageNameStyle.normal.textColor = new Color(0.1f, 0.4f, 0.8f); // 深蓝色
            }
        }

        /// <summary>
        /// 绘制顶部包选择器
        /// </summary>
        private void DrawPackageSelector()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📦 当前包选择", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            // 包选择下拉菜单
            int currentIndex = availablePackageIds.IndexOf(currentSelectedPackageId);
            if (currentIndex < 0) currentIndex = 0;

            if (packageDisplayNameBuffer.Length != availablePackageIds.Count)
                packageDisplayNameBuffer = new string[availablePackageIds.Count];
            for (int i = 0; i < availablePackageIds.Count; i++)
            {
                string packageId = availablePackageIds[i];
                packageDisplayNameBuffer[i] = packageDisplayNames.TryGetValue(packageId, out string displayName)
                    ? displayName
                    : packageId;
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("选择包:", currentIndex, packageDisplayNameBuffer);
            if (EditorGUI.EndChangeCheck() && newIndex != currentIndex && newIndex >= 0 && newIndex < availablePackageIds.Count)
            {
                string newPackageId = availablePackageIds[newIndex];

                // 检查是否选择扩展包且主包未安装
                if (newPackageId != "es_main" && !isMainPackageInstalled)
                {
                    bool switchAnyway = EditorUtility.DisplayDialog(
                        "主包未安装警告",
                        "检测到主包尚未安装。所有扩展包都依赖于主包。\n\n建议先安装主包后再安装扩展包。\n\n是否仍要切换到此扩展包？",
                        "仍要切换",
                        "返回主包"
                    );

                    if (switchAnyway)
                    {
                        currentSelectedPackageId = newPackageId;
                        ShowStatus($"已切换到: {packageDisplayNames[newPackageId]}", MessageType.Warning);
                        // 加载新包的依赖

                    }
                    else
                    {
                        currentSelectedPackageId = "es_main";
                    }
                }
                else
                {
                    var selectedPackage = currentProfile.extensionPackages.FirstOrDefault(p => p.packageId == newPackageId);
                    if (selectedPackage != null)
                    {
                        _ = CheckPackageDependenciesAsync(selectedPackage);
                    }
                    currentSelectedPackageId = newPackageId;
                    ShowStatus($"已切换到: {packageDisplayNames[currentSelectedPackageId]}", MessageType.Info);
                }

                Repaint();
            }

            // 快速返回主包按钮
            if (currentSelectedPackageId != "es_main")
            {
                if (GUILayout.Button("🏠 返回主包", GUILayout.Width(100)))
                {
                    currentSelectedPackageId = "es_main";
                    ShowStatus("已返回主包安装界面", MessageType.Info);
                    Repaint();
                }
            }

            EditorGUILayout.EndHorizontal();

            // 显示当前包信息
            if (currentSelectedPackageId == "es_main")
            {
                EditorGUILayout.HelpBox("📦 主包 (必需): ES框架的核心包，所有扩展包都依赖于此包", MessageType.Info);
            }
            else
            {
                var currentPackage = currentProfile.extensionPackages.FirstOrDefault(p => p.packageId == currentSelectedPackageId);
                if (currentPackage != null)
                {
                    string warningMsg = currentProfile.mainPackage.installState != PackageInstallState.Installed
                        ? "⚠️ 警告: 主包尚未安装，建议先安装主包\n\n"
                        : "";
                    EditorGUILayout.HelpBox($"{warningMsg}📦 扩展包: {currentPackage.displayName}\n{currentPackage.description}", MessageType.Info);
                }
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制主包内容
        /// </summary>
        private void DrawMainPackageContent()
        {
            // 主包安装状态
            DrawPackageInstallationStatus(currentProfile.mainPackage);
            EditorGUILayout.Space(10);

            // 配置文件管理
            DrawProfileManagement();
            EditorGUILayout.Space(10);

            // Unity官方包管理
            DrawUnityPackagesSection();
            EditorGUILayout.Space(10);

            // Git包管理
            DrawGitPackagesSection();
            EditorGUILayout.Space(10);

            // 用户包管理
            DrawUserPackagesSection();
            EditorGUILayout.Space(10);

            // 主包安装管理
            DrawMainPackageInstallationSection();
        }

        /// <summary>
        /// 绘制扩展包内容
        /// </summary>
        private void DrawExtensionPackageContent()
        {
            var currentPackage = currentProfile.extensionPackages.FirstOrDefault(p => p.packageId == currentSelectedPackageId);

            if (currentPackage == null)
            {
                EditorGUILayout.HelpBox("未找到当前选中的扩展包配置", MessageType.Error);
                return;
            }

            // 扩展包安装状态
            DrawPackageInstallationStatus(currentPackage);
            EditorGUILayout.Space(10);

            // 扩展包信息
            DrawExtensionPackageInfo(currentPackage);
            EditorGUILayout.Space(10);

            // 扩展包的Unity依赖
            if (currentPackage.unityDependencies != null && currentPackage.unityDependencies.Count > 0)
            {
                DrawExtensionUnityDependencies(currentPackage);
                EditorGUILayout.Space(10);
            }

            // 扩展包的Git依赖
            if (currentPackage.gitDependencies != null && currentPackage.gitDependencies.Count > 0)
            {
                DrawExtensionGitDependencies(currentPackage);
                EditorGUILayout.Space(10);
            }

            // 扩展包的用户包依赖
            if (currentPackage.userDependencies != null && currentPackage.userDependencies.Count > 0)
            {
                DrawExtensionUserDependencies(currentPackage);
                EditorGUILayout.Space(10);
            }

            // 扩展包安装管理
            DrawExtensionPackageInstallationSection(currentPackage);
        }

        /// <summary>
        /// 统一绘制包内容
        /// </summary>
        private void DrawPackageContent(ESPackageBase package)
        {
            // 包安装状态
            DrawPackageInstallationStatus(package);
            EditorGUILayout.Space(10);

            // 如果是主包，显示配置文件管理
            if (package.packageId == "es_main")
            {
                DrawProfileManagement();
                EditorGUILayout.Space(10);
            }
            else
            {
                // 扩展包信息
                DrawExtensionPackageInfo((ESExtensionPackage)package);
                EditorGUILayout.Space(10);
            }
            // 显示包的依赖项
            if (package.packageId == "es_main")
            {
                // Debug.Log("Drawing content for package: " + package.packageId);

                // 主包使用全局配置的依赖项
                DrawUnityPackagesSection();
                EditorGUILayout.Space(10);

                DrawGitPackagesSection();
                EditorGUILayout.Space(10);

                DrawUserPackagesSection();
                EditorGUILayout.Space(10);
            }
            else
            {
                // 扩展包使用自己的依赖项
                var extPackage = (ESExtensionPackage)package;

                if (extPackage.unityDependencies != null && extPackage.unityDependencies.Count > 0)
                {
                    DrawExtensionUnityDependencies(extPackage);
                    EditorGUILayout.Space(10);
                }

                if (extPackage.gitDependencies != null && extPackage.gitDependencies.Count > 0)
                {
                    DrawExtensionGitDependencies(extPackage);
                    EditorGUILayout.Space(10);
                }

                if (extPackage.userDependencies != null && extPackage.userDependencies.Count > 0)
                {
                    DrawExtensionUserDependencies(extPackage);
                    EditorGUILayout.Space(10);
                }
            }

            // 包安装管理
            if (package.packageId == "es_main")
            {
                DrawMainPackageInstallationSection();
            }
            else
            {
                DrawExtensionPackageInstallationSection((ESExtensionPackage)package);
            }
        }

        /// <summary>
        /// 绘制包的安装状态
        /// </summary>
        private void DrawPackageInstallationStatus(ESPackageBase package)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📊 安装状态", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("包名称:", package.displayName, packageNameStyle);
            EditorGUILayout.LabelField("版本:", package.version, GUILayout.Width(150));

            // 状态指示器
            bool isInstalled = package.installState == PackageInstallState.Installed;
            GUI.color = isInstalled ? Color.green : Color.red;
            string statusText = isInstalled ? "✓ 已安装" : "✗ 未安装";
            string statusTooltip = isInstalled ? "此包已正确安装" : "此包尚未安装或安装不完整";
            EditorGUILayout.LabelField(new GUIContent(statusText, statusTooltip), GUILayout.Width(80));
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(package.description))
            {
                EditorGUILayout.LabelField($"描述: {package.description}");
            }

            // 显示验证方式
            if (!string.IsNullOrEmpty(package.checkClass))
            {
                EditorGUILayout.LabelField($"验证类: {package.checkClass}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("验证方式: 文件存在检查", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawProfileManagement()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📁 配置管理", EditorStyles.boldLabel);

            if (currentProfile == null)
            {
                EditorGUILayout.LabelField("配置加载中...", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField($"配置名称: {currentProfile.profileName}");

            // 自动检查设置
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("自动检查设置", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool enableAutoCheck = EditorGUILayout.ToggleLeft(
                "启动 Unity 后自动检查外部依赖（可能访问 Package Manager / Git）",
                currentProfile.enableAutoCheck);
            if (EditorGUI.EndChangeCheck())
            {
                currentProfile.enableAutoCheck = enableAutoCheck;
                EditorPrefs.SetBool(GetAutoCheckPreferenceKey(), enableAutoCheck);
                ShowStatus(enableAutoCheck ? "已启用项目级启动依赖检查。" : "已关闭项目级启动依赖检查。", MessageType.Info);
            }

            if (currentProfile.enableAutoCheck)
            {
                EditorGUILayout.HelpBox("该开关按项目保存。默认关闭；启用后会在启动时检查必需依赖。", MessageType.Info);
                if (GUILayout.Button(currentProfile.skipNextAutoCheck ? "已设置跳过下次启动检查" : "跳过下次启动检查", GUILayout.MinHeight(24)))
                {
                    currentProfile.skipNextAutoCheck = true;
                    SessionState.SetBool(GetSkipNextAutoCheckSessionKey(), true);
                }
            }

            EditorGUILayout.Space(5);
            showDependencyEditor = EditorGUILayout.Foldout(showDependencyEditor, "编辑主包依赖清单", true);
            if (showDependencyEditor)
            {
                EditorGUILayout.HelpBox(
                    "这里编辑的是 Downloads/Main/package.json。Git 依赖必须固定到完整 commit；商业插件只声明检查，不会被打进主包。",
                    MessageType.Info);
                DrawMainDependencyConfigurationEditor();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("💾 保存配置"))
            {
                SaveConfiguration();
                ShowStatus("配置已保存", MessageType.Info);
            }

            if (GUILayout.Button("📂 加载配置"))
            {
                bool confirmLoad = true;
                if (isConfigModified)
                {
                    confirmLoad = EditorUtility.DisplayDialog(
                        "确认加载",
                        "当前有未保存的修改，加载配置将丢失这些修改。是否继续？",
                        "确认加载",
                        "取消"
                    );
                }

                if (confirmLoad)
                {
                    LoadSavedConfiguration();
                    isConfigModified = false;
                    ShowStatus("配置已加载", MessageType.Info);
                }
            }

            if (GUILayout.Button("定位配置"))
            {
                string assetPath = configFilePath.Replace('\\', '/');
                TextAsset configAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                if (configAsset != null)
                {
                    Selection.activeObject = configAsset;
                    EditorGUIUtility.PingObject(configAsset);
                }
                else
                {
                    ShowStatus("无法在 Project 中定位主包配置：" + assetPath, MessageType.Warning);
                }
            }

            if (GUILayout.Button("🔄 重置为默认"))
            {
                bool confirmReset = EditorUtility.DisplayDialog(
                    "确认重置",
                    "这将重置所有配置为默认值，当前修改将丢失。是否继续？",
                    "确认重置",
                    "取消"
                );

                if (confirmReset)
                {
                    InitializeDefaultProfile();
                    isConfigModified = true;
                    ShowStatus("已重置为默认配置，请记得保存", MessageType.Warning);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"最后修改: {currentProfile.lastModified:yyyy-MM-dd HH:mm:ss}", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawMainDependencyConfigurationEditor()
        {
            ESMainPackage package = currentProfile.mainPackage;
            package.unityDependencies ??= new List<UnityPackageDependency>();
            package.gitDependencies ??= new List<GitPackageDependency>();
            package.userDependencies ??= new List<UserPackageDependency>();
            package.assetFileDependencies ??= new List<AssetFileDependency>();

            EditorGUI.BeginChangeCheck();
            DrawUnityDependencyConfiguration(package.unityDependencies);
            DrawGitDependencyConfiguration(package.gitDependencies);
            DrawUserDependencyConfiguration(package.userDependencies);
            DrawAssetDependencyConfiguration(package.assetFileDependencies);
            if (EditorGUI.EndChangeCheck())
                isConfigModified = true;

            if (GUILayout.Button("校验依赖配置", GUILayout.MinHeight(26)))
            {
                if (TryValidateMainPackageConfiguration(out string validationError))
                    ShowStatus("主包依赖配置有效，可以保存。", MessageType.Info);
                else
                    ShowStatus("主包依赖配置无效：" + validationError, MessageType.Error);
            }
        }

        private void DrawUnityDependencyConfiguration(List<UnityPackageDependency> dependencies)
        {
            EditorGUILayout.LabelField("Unity / Registry 依赖", EditorStyles.boldLabel);
            for (int i = 0; i < dependencies.Count; i++)
            {
                UnityPackageDependency dependency = dependencies[i] ?? (dependencies[i] = new UnityPackageDependency());
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                dependency.name = EditorGUILayout.TextField("名称", dependency.name);
                if (GUILayout.Button("删除", GUILayout.MinWidth(52), GUILayout.MaxWidth(64)))
                {
                    dependencies.RemoveAt(i);
                    isConfigModified = true;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();
                dependency.version = EditorGUILayout.TextField("版本", dependency.version);
                dependency.packageId = EditorGUILayout.TextField("Package ID", dependency.packageId);
                dependency.checkClass = EditorGUILayout.TextField("检查类", dependency.checkClass);
                dependency.description = EditorGUILayout.TextField("说明", dependency.description);
                dependency.isRequired = EditorGUILayout.Toggle("必需", dependency.isRequired);
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("添加 Unity 依赖", GUILayout.MinHeight(24)))
            {
                dependencies.Add(new UnityPackageDependency());
                isConfigModified = true;
            }
        }

        private void DrawGitDependencyConfiguration(List<GitPackageDependency> dependencies)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Git UPM 依赖", EditorStyles.boldLabel);
            for (int i = 0; i < dependencies.Count; i++)
            {
                GitPackageDependency dependency = dependencies[i] ?? (dependencies[i] = new GitPackageDependency());
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                dependency.name = EditorGUILayout.TextField("名称", dependency.name);
                if (GUILayout.Button("删除", GUILayout.MinWidth(52), GUILayout.MaxWidth(64)))
                {
                    dependencies.RemoveAt(i);
                    isConfigModified = true;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();
                dependency.version = EditorGUILayout.TextField("版本", dependency.version);
                dependency.gitUrl = EditorGUILayout.TextField("Git URL", dependency.gitUrl);
                dependency.checkClass = EditorGUILayout.TextField("检查类", dependency.checkClass);
                dependency.description = EditorGUILayout.TextField("说明", dependency.description);
                dependency.isRequired = EditorGUILayout.Toggle("必需", dependency.isRequired);
                if (!TryValidatePinnedGitUrl(dependency.gitUrl, out _, out _, out string pinError))
                    EditorGUILayout.HelpBox(pinError, MessageType.Error);
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("添加 Git 依赖", GUILayout.MinHeight(24)))
            {
                dependencies.Add(new GitPackageDependency());
                isConfigModified = true;
            }
        }

        private void DrawUserDependencyConfiguration(List<UserPackageDependency> dependencies)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("手动 / 商业插件依赖", EditorStyles.boldLabel);
            for (int i = 0; i < dependencies.Count; i++)
            {
                UserPackageDependency dependency = dependencies[i] ?? (dependencies[i] = new UserPackageDependency());
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                dependency.name = EditorGUILayout.TextField("名称", dependency.name);
                if (GUILayout.Button("删除", GUILayout.MinWidth(52), GUILayout.MaxWidth(64)))
                {
                    dependencies.RemoveAt(i);
                    isConfigModified = true;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();
                dependency.version = EditorGUILayout.TextField("版本", dependency.version);
                dependency.checkClass = EditorGUILayout.TextField("检查类", dependency.checkClass);
                dependency.installInstructions = EditorGUILayout.TextField("安装说明", dependency.installInstructions);
                dependency.description = EditorGUILayout.TextField("说明", dependency.description);
                dependency.isRequired = EditorGUILayout.Toggle("必需", dependency.isRequired);
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("添加手动依赖", GUILayout.MinHeight(24)))
            {
                dependencies.Add(new UserPackageDependency());
                isConfigModified = true;
            }
        }

        private void DrawAssetDependencyConfiguration(List<AssetFileDependency> dependencies)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("项目资产依赖", EditorStyles.boldLabel);
            for (int i = 0; i < dependencies.Count; i++)
            {
                AssetFileDependency dependency = dependencies[i] ?? (dependencies[i] = new AssetFileDependency());
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                dependency.name = EditorGUILayout.TextField("名称", dependency.name);
                if (GUILayout.Button("删除", GUILayout.MinWidth(52), GUILayout.MaxWidth(64)))
                {
                    dependencies.RemoveAt(i);
                    isConfigModified = true;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();
                dependency.version = EditorGUILayout.TextField("版本", dependency.version);
                dependency.assetPath = EditorGUILayout.TextField("Assets 路径", dependency.assetPath);
                dependency.checkClass = EditorGUILayout.TextField("检查类", dependency.checkClass);
                dependency.description = EditorGUILayout.TextField("说明", dependency.description);
                dependency.isRequired = EditorGUILayout.Toggle("必需", dependency.isRequired);
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("添加资产依赖", GUILayout.MinHeight(24)))
            {
                dependencies.Add(new AssetFileDependency());
                isConfigModified = true;
            }
        }

        /// <summary>
        /// 通用绘制Unity依赖列表
        /// </summary>
        private void DrawUnityDependencies(List<UnityPackageDependency> deps, string title, bool showBatchOperations = false, bool showManualToggle = false)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            if (deps == null || deps.Count == 0)
            {
                EditorGUILayout.LabelField("无依赖项", EditorStyles.miniLabel);
            }
            else
            {
                // 依赖列表
                for (int i = 0; i < deps.Count; i++)
                {
                    DrawUnityDependencyItem(deps[i], showManualToggle);
                }

                // 批量操作
                if (showBatchOperations && deps.Count > 0)
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("🔍 检查所有Unity包"))
                    {
                        _ = CheckAllUnityPackages();
                    }
                    if (GUILayout.Button("📦 安装所有Unity包"))
                    {
                        _ = InstallAllUnityPackages();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 通用绘制单个Unity依赖项
        /// </summary>
        private void DrawUnityDependencyItem(UnityPackageDependency dependency, bool showManualToggle = false)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("名称", dependency.name, packageNameStyle);
            EditorGUILayout.LabelField("必需", dependency.isRequired ? "是" : "否", GUILayout.Width(60));

            // 状态指示器
            GUI.color = dependency.isInstalled ? Color.green : Color.red;
            EditorGUILayout.LabelField(dependency.isInstalled ? "✓ 已安装" : "✗ 未安装", GUILayout.Width(80));
            GUI.color = Color.white;

            if (showManualToggle)
            {
                EditorGUI.BeginChangeCheck();
                dependency.isInstalled = EditorGUILayout.Toggle("手动设置", dependency.isInstalled, GUILayout.Width(80));
                if (EditorGUI.EndChangeCheck())
                {
                    isConfigModified = true;
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"版本: {dependency.version}");
            EditorGUILayout.LabelField($"描述: {dependency.description}");
            EditorGUILayout.LabelField($"Package ID: {dependency.packageId}");
            if (!string.IsNullOrEmpty(dependency.checkClass))
                EditorGUILayout.LabelField($"检查类名: {dependency.checkClass}");
            if (!string.IsNullOrEmpty(dependency.installUrl))
                EditorGUILayout.LabelField($"安装URL: {dependency.installUrl}");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔍 检查"))
            {
                _ = CheckUnityPackageDependency(dependency);
            }
            if (GUILayout.Button("📦 安装"))
            {
                _ = InstallUnityPackageDependency(dependency);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        /// <summary>
        /// 通用绘制Git依赖列表
        /// </summary>
        private void DrawGitDependencies(List<GitPackageDependency> deps, string title, bool showBatchOperations = false, bool showManualToggle = false)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            if (deps == null || deps.Count == 0)
            {
                EditorGUILayout.LabelField("无依赖项", EditorStyles.miniLabel);
            }
            else
            {
                // 依赖列表
                for (int i = 0; i < deps.Count; i++)
                {
                    DrawGitDependencyItem(deps[i], showManualToggle);
                }

                // 批量操作
                if (showBatchOperations && deps.Count > 0)
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("🔍 检查所有Git包"))
                    {
                        _ = CheckAllGitPackages();
                    }
                    if (GUILayout.Button("📦 安装所有Git包"))
                    {
                        _ = InstallAllGitPackages();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 通用绘制单个Git依赖项
        /// </summary>
        private void DrawGitDependencyItem(GitPackageDependency dependency, bool showManualToggle = false)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("名称", dependency.name, packageNameStyle);
            EditorGUILayout.LabelField("必需", dependency.isRequired ? "是" : "否", GUILayout.Width(60));

            // 状态指示器
            GUI.color = dependency.isInstalled ? Color.green : Color.red;
            EditorGUILayout.LabelField(dependency.isInstalled ? "✓ 已安装" : "✗ 未安装", GUILayout.Width(80));
            GUI.color = Color.white;

            if (showManualToggle)
            {
                EditorGUI.BeginChangeCheck();
                dependency.isInstalled = EditorGUILayout.Toggle("手动设置", dependency.isInstalled, GUILayout.Width(80));
                if (EditorGUI.EndChangeCheck())
                {
                    isConfigModified = true;
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"版本: {dependency.version}");
            EditorGUILayout.LabelField($"描述: {dependency.description}");
            EditorGUILayout.LabelField($"Git URL: {dependency.gitUrl}");
            DrawGitSupplyChainStatus(dependency);
            if (!string.IsNullOrEmpty(dependency.checkClass))
                EditorGUILayout.LabelField($"检查类名: {dependency.checkClass}");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔍 检查"))
            {
                _ = CheckGitPackageDependency(dependency);
            }
            if (GUILayout.Button("📦 安装"))
            {
                _ = InstallGitPackageDependency(dependency);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        /// <summary>
        /// 通用绘制用户依赖列表
        /// </summary>
        private void DrawUserDependencies(List<UserPackageDependency> deps, string title, bool showBatchOperations = false, bool showManualToggle = false)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            if (deps == null || deps.Count == 0)
            {
                EditorGUILayout.LabelField("无依赖项", EditorStyles.miniLabel);
            }
            else
            {
                // 依赖列表
                for (int i = 0; i < deps.Count; i++)
                {
                    DrawUserDependencyItem(deps[i], showManualToggle);
                }

                // 批量操作
                if (showBatchOperations && deps.Count > 0)
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("🔍 检查所有用户包"))
                    {
                        _ = CheckAllUserPackages();
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (showBatchOperations)
                {
                    EditorGUILayout.HelpBox("用户包需要手动安装，安装器只负责检查是否存在指定的类。", MessageType.Info);
                }
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 通用绘制单个用户依赖项
        /// </summary>
        private void DrawUserDependencyItem(UserPackageDependency dependency, bool showManualToggle = false)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("名称", dependency.name, packageNameStyle);
            EditorGUILayout.LabelField("必需", dependency.isRequired ? "是" : "否", GUILayout.Width(60));

            // 状态指示器
            GUI.color = dependency.isInstalled ? Color.green : Color.red;
            EditorGUILayout.LabelField(dependency.isInstalled ? "✓ 已安装" : "✗ 未安装", GUILayout.Width(80));
            GUI.color = Color.white;

            if (showManualToggle)
            {
                EditorGUI.BeginChangeCheck();
                dependency.isInstalled = EditorGUILayout.Toggle("手动设置", dependency.isInstalled, GUILayout.Width(80));
                if (EditorGUI.EndChangeCheck())
                {
                    isConfigModified = true;
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"版本: {dependency.version}");
            EditorGUILayout.LabelField($"描述: {dependency.description}");
            if (!string.IsNullOrEmpty(dependency.checkClass))
                EditorGUILayout.LabelField($"检查类名: {dependency.checkClass}");
            if (!string.IsNullOrEmpty(dependency.installInstructions))
                EditorGUILayout.LabelField($"安装说明: {dependency.installInstructions}");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔍 检查"))
            {
                _ = CheckUserPackageDependency(dependency);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void DrawUnityPackagesSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            showUnityPackages = EditorGUILayout.Foldout(showUnityPackages, "📦 Unity官方包 (Package Manager)", sectionStyle);
            // Debug.Log("2Drawing content for package: " + currentSelectedPackageId);

            if (showUnityPackages)
            {
                EditorGUILayout.Space(5);
                // Debug.Log("3Drawing content for package: " + currentProfile.mainPackage.unityDependencies.Count);

                if (currentProfile == null)
                {
                    EditorGUILayout.LabelField("配置加载中...", EditorStyles.miniLabel);
                }
                else
                {
                    DrawUnityDependencies(currentProfile.mainPackage.unityDependencies, "Unity包依赖", true, true);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawGitPackagesSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            showGitPackages = EditorGUILayout.Foldout(showGitPackages, "🔗 Git包 (通过URL安装)", sectionStyle);

            if (showGitPackages)
            {
                EditorGUILayout.Space(5);

                if (currentProfile == null || currentProfile.mainPackage.gitDependencies == null)
                {
                    EditorGUILayout.LabelField("配置加载中...", EditorStyles.miniLabel);
                }
                else
                {
                    // 依赖列表
                    for (int i = 0; i < currentProfile.mainPackage.gitDependencies.Count; i++)
                    {
                        DrawGitPackageItem(i);
                    }

                    // 批量操作
                    if (currentProfile.mainPackage.gitDependencies.Count > 0)
                    {
                        EditorGUILayout.Space(10);
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("🔍 检查所有Git包"))
                        {
                            _ = CheckAllGitPackages();
                        }
                        if (GUILayout.Button("📦 安装所有Git包"))
                        {
                            _ = InstallAllGitPackages();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawGitPackageItem(int index)
        {
            var dependency = currentProfile.mainPackage.gitDependencies[index];

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("名称", dependency.name, packageNameStyle);
            EditorGUILayout.LabelField("必需", dependency.isRequired ? "是" : "否", GUILayout.Width(60));

            // 状态指示器和手动设置
            GUI.color = dependency.isInstalled ? Color.green : Color.red;
            EditorGUILayout.LabelField(dependency.isInstalled ? "✓ 已安装" : "✗ 未安装", GUILayout.Width(80));
            GUI.color = Color.white;

            EditorGUI.BeginChangeCheck();
            dependency.isInstalled = EditorGUILayout.Toggle("手动设置", dependency.isInstalled, GUILayout.Width(80));
            if (EditorGUI.EndChangeCheck())
            {
                isConfigModified = true;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"版本: {dependency.version}");
            EditorGUILayout.LabelField($"描述: {dependency.description}");
            EditorGUILayout.LabelField($"Git URL: {dependency.gitUrl}");
            DrawGitSupplyChainStatus(dependency);
            EditorGUILayout.LabelField($"检查类名: {dependency.checkClass}");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔍 检查"))
            {
                _ = CheckGitPackageDependency(dependency);
            }
            if (GUILayout.Button("📦 安装"))
            {
                _ = InstallGitPackageDependency(dependency);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // private void DrawGitPackagesSection()
        // {
        //     EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        //     showGitPackages = EditorGUILayout.Foldout(showGitPackages, "🔗 Git包 (通过URL安装)", sectionStyle);

        //     if (showGitPackages)
        //     {
        //         EditorGUILayout.Space(5);

        //         if (currentProfile == null || currentProfile.gitPackages == null)
        //         {
        //             EditorGUILayout.LabelField("配置加载中...", EditorStyles.miniLabel);
        //         }
        //         else
        //         {
        //             DrawGitDependencies(currentProfile.gitPackages, "Git包依赖", true, true);
        //         }
        //     }

        //     EditorGUILayout.EndVertical();
        // }

        private void DrawUserPackagesSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            showUserPackages = EditorGUILayout.Foldout(showUserPackages, "👤 用户包 (手动安装)", sectionStyle);

            if (showUserPackages)
            {
                EditorGUILayout.Space(5);

                if (currentProfile == null || currentProfile.mainPackage.userDependencies == null)
                {
                    EditorGUILayout.LabelField("配置加载中...", EditorStyles.miniLabel);
                }
                else
                {
                    DrawUserDependencies(currentProfile.mainPackage.userDependencies, "用户包依赖", true, true);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawUserPackageItem(int index)
        {
            var dependency = currentProfile.mainPackage.userDependencies[index];

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("名称", dependency.name, packageNameStyle);
            EditorGUILayout.LabelField("必需", dependency.isRequired ? "是" : "否", GUILayout.Width(60));

            // 状态指示器和手动设置
            GUI.color = dependency.isInstalled ? Color.green : Color.red;
            EditorGUILayout.LabelField(dependency.isInstalled ? "✓ 已安装" : "✗ 未安装", GUILayout.Width(80));
            GUI.color = Color.white;

            EditorGUI.BeginChangeCheck();
            dependency.isInstalled = EditorGUILayout.Toggle("手动设置", dependency.isInstalled, GUILayout.Width(80));
            if (EditorGUI.EndChangeCheck())
            {
                isConfigModified = true;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"版本: {dependency.version}");
            EditorGUILayout.LabelField($"描述: {dependency.description}");
            EditorGUILayout.LabelField($"检查类名: {dependency.checkClass}");
            EditorGUILayout.LabelField($"安装说明: {dependency.installInstructions}");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔍 检查"))
            {
                _ = CheckUserPackageDependency(dependency);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void DrawESPackageSystemSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            showESPackageSystem = EditorGUILayout.Foldout(showESPackageSystem, "📦 ES包系统", sectionStyle);

            if (showESPackageSystem)
            {
                EditorGUILayout.Space(5);

                if (currentProfile == null)
                {
                    EditorGUILayout.LabelField("配置加载中...", EditorStyles.miniLabel);
                }
                else
                {
                    // 主包信息
                    EditorGUILayout.LabelField("主包 (必需)", EditorStyles.boldLabel);
                    string mainPackagePath = GetSafePackageFolderPath(currentProfile.mainPackage);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"路径: {mainPackagePath}", GUILayout.ExpandWidth(true));
                    EditorGUI.BeginDisabledGroup(true);
                    if (GUILayout.Button("📁 选择文件夹", GUILayout.Width(100)))
                    {
                        // 不允许通过面板修改
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.HelpBox("主包文件夹路径不允许通过面板修改，请手动编辑配置文件。", MessageType.Info);

                    // 检查主包状态
                    if (Directory.Exists(mainPackagePath))
                    {
                        string[] unityPackages = Directory.GetFiles(mainPackagePath, "*.unitypackage");
                        GUI.color = unityPackages.Length > 0 ? Color.green : Color.yellow;
                        EditorGUILayout.LabelField($"发现 {unityPackages.Length} 个Unity Package文件", EditorStyles.miniLabel);
                        GUI.color = Color.white;
                    }
                    else
                    {
                        GUI.color = Color.red;
                        EditorGUILayout.LabelField("主包文件夹不存在", EditorStyles.miniLabel);
                        GUI.color = Color.white;
                    }

                    EditorGUILayout.Space(10);

                    // 扩展包列表
                    EditorGUILayout.LabelField("扩展包 (可选)", EditorStyles.boldLabel);
                    EditorGUILayout.Space(5);

                    if (currentProfile.extensionPackages.Count == 0)
                    {
                        EditorGUILayout.HelpBox("暂无扩展包配置", MessageType.Info);
                    }
                    else
                    {
                        for (int i = 0; i < currentProfile.extensionPackages.Count; i++)
                        {
                            var extPackage = currentProfile.extensionPackages[i];

                            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                            EditorGUILayout.BeginHorizontal();

                            // 包选择复选框
                            EditorGUI.BeginChangeCheck();
                            extPackage.isSelectedForInstall = EditorGUILayout.Toggle(extPackage.isSelectedForInstall, GUILayout.Width(20));
                            if (EditorGUI.EndChangeCheck())
                            {
                                isConfigModified = true;
                            }

                            // 包信息
                            EditorGUILayout.LabelField($"{extPackage.displayName} (v{extPackage.version})", EditorStyles.boldLabel);

                            // 安装状态
                            bool isInstalled = extPackage.installState == PackageInstallState.Installed;
                            GUI.color = isInstalled ? Color.green : Color.red;
                            EditorGUILayout.LabelField(isInstalled ? "✓ 已安装" : "✗ 未安装", GUILayout.Width(60));
                            GUI.color = Color.white;

                            EditorGUILayout.EndHorizontal();

                            // 包描述
                            if (!string.IsNullOrEmpty(extPackage.description))
                            {
                                EditorGUILayout.LabelField(extPackage.description, EditorStyles.wordWrappedMiniLabel);
                            }

                            // 包位置信息
                            string packagePath = GetSafePackageFolderPath(extPackage);
                            EditorGUILayout.LabelField($"位置: {packagePath}", EditorStyles.miniLabel);

                            // 检查包文件状态
                            if (Directory.Exists(packagePath))
                            {
                                string[] unityPackages = Directory.GetFiles(packagePath, "*.unitypackage");
                                GUI.color = unityPackages.Length > 0 ? Color.green : Color.yellow;
                                EditorGUILayout.LabelField($"发现 {unityPackages.Length} 个Unity Package文件", EditorStyles.miniLabel);
                                GUI.color = Color.white;
                            }
                            else
                            {
                                GUI.color = Color.red;
                                EditorGUILayout.LabelField("扩展包文件夹不存在", EditorStyles.miniLabel);
                                GUI.color = Color.white;
                            }

                            EditorGUILayout.EndVertical();
                            EditorGUILayout.Space(2);
                        }
                    }

                    // 添加扩展包按钮
                    EditorGUILayout.Space(5);
                    if (GUILayout.Button("➕ 添加扩展包", GUILayout.Height(25)))
                    {
                        AddNewExtensionPackage();
                    }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void DrawInstallationSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            showInstallation = EditorGUILayout.Foldout(showInstallation, "🚀 安装管理", sectionStyle);

            if (showInstallation)
            {
                EditorGUILayout.Space(5);

                if (currentProfile == null)
                {
                    EditorGUILayout.LabelField("配置加载中...", EditorStyles.miniLabel);
                }
                else
                {
                    // ES包系统安装预览
                    EditorGUILayout.LabelField("ES包系统安装预览", EditorStyles.boldLabel);
                    EditorGUILayout.Space(5);

                    // 主包信息
                    EditorGUILayout.LabelField("主包 (必需)", EditorStyles.miniBoldLabel);
                    string mainPackagePath = GetSafePackageFolderPath(currentProfile.mainPackage);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"路径: {mainPackagePath}", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                    EditorGUILayout.EndHorizontal();

                    int mainPackageCount = 0;
                    if (Directory.Exists(mainPackagePath))
                    {
                        string[] mainPackages = Directory.GetFiles(mainPackagePath, "*.unitypackage");
                        mainPackageCount = mainPackages.Length;
                        GUI.color = mainPackageCount > 0 ? Color.green : Color.red;
                        EditorGUILayout.LabelField($"发现 {mainPackageCount} 个Unity Package文件", EditorStyles.miniLabel);
                        GUI.color = Color.white;
                    }
                    else
                    {
                        GUI.color = Color.red;
                        EditorGUILayout.LabelField("主包文件夹不存在", EditorStyles.miniLabel);
                        GUI.color = Color.white;
                    }

                    EditorGUILayout.Space(5);

                    // 扩展包信息
                    List<ESExtensionPackage> selectedExtensions = currentProfile.extensionPackages
                        .Where(ext => ext.isSelectedForInstall)
                        .ToList();

                    if (selectedExtensions.Count > 0)
                    {
                        EditorGUILayout.LabelField($"扩展包 ({selectedExtensions.Count} 个已选择)", EditorStyles.miniBoldLabel);

                        int totalExtPackages = 0;
                        foreach (var extPackage in selectedExtensions)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField($"• {extPackage.displayName}", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));

                            string extFolderPath = GetSafePackageFolderPath(extPackage);
                            if (Directory.Exists(extFolderPath))
                            {
                                string[] extPackages = Directory.GetFiles(extFolderPath, "*.unitypackage");
                                totalExtPackages += extPackages.Length;
                                GUI.color = extPackages.Length > 0 ? Color.green : Color.yellow;
                                EditorGUILayout.LabelField($"{extPackages.Length} 个文件", EditorStyles.miniLabel, GUILayout.Width(80));
                                GUI.color = Color.white;
                            }
                            else
                            {
                                GUI.color = Color.red;
                                EditorGUILayout.LabelField("文件夹不存在", EditorStyles.miniLabel, GUILayout.Width(80));
                                GUI.color = Color.white;
                            }
                            EditorGUILayout.EndHorizontal();
                        }

                        EditorGUILayout.Space(5);
                        EditorGUILayout.LabelField($"总计: 主包 {mainPackageCount} 个 + 扩展包 {totalExtPackages} 个 = {mainPackageCount + totalExtPackages} 个Unity Package文件", EditorStyles.boldLabel);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("扩展包 (未选择)", EditorStyles.miniBoldLabel);
                        EditorGUILayout.LabelField("总计: 主包 " + mainPackageCount + " 个Unity Package文件", EditorStyles.boldLabel);
                    }

                    // 安装说明
                    EditorGUILayout.LabelField($"安装说明: {currentProfile.installationNotes}");

                    EditorGUILayout.Space(10);

                    // 依赖检查
                    bool allDependenciesValid = CheckAllDependenciesValid();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("依赖状态", EditorStyles.boldLabel);
                    GUI.color = allDependenciesValid ? Color.green : Color.red;
                    EditorGUILayout.LabelField(allDependenciesValid ? "✓ 所有依赖有效" : "✗ 存在无效依赖");
                    GUI.color = Color.white;
                    EditorGUILayout.EndHorizontal();

                    // 安装按钮
                    EditorGUI.BeginDisabledGroup(!allDependenciesValid);
                    if (GUILayout.Button("🚀 开始安装 ES 框架", GUILayout.Height(40)))
                    {
                        StartInstallation();
                    }
                    EditorGUI.EndDisabledGroup();

                    if (!allDependenciesValid)
                    {
                        EditorGUILayout.HelpBox("请先解决所有依赖问题后再进行安装。", MessageType.Warning);
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制主包安装部分
        /// </summary>
        private void DrawMainPackageInstallationSection()
        {
            DrawPackageInstallation(currentProfile.mainPackage, "🚀 主包安装");
        }

        /// <summary>
        /// 通用的包安装UI绘制方法
        /// </summary>
        private void DrawPackageInstallation(ESPackageBase package, string title)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            string packagePath = GetSafePackageFolderPath(package);

            // 显示包路径
            EditorGUILayout.LabelField($"路径: {packagePath}", EditorStyles.miniLabel);

            // 显示安装状态
            switch (package.installState)
            {
                case PackageInstallState.Loading:
                    EditorGUILayout.HelpBox("⏳ 正在检查安装状态...", MessageType.Info);
                    break;

                case PackageInstallState.Installed:
                    EditorGUILayout.HelpBox("✅ 已安装", MessageType.Info);

                    // 显示依赖状态
                    bool areDependenciesSatisfied = CheckPackageDependencies(package);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("依赖状态", EditorStyles.miniBoldLabel);
                    GUI.color = areDependenciesSatisfied ? Color.green : Color.red;
                    EditorGUILayout.LabelField(areDependenciesSatisfied ? "✓ 依赖满足" : "✗ 依赖不满足", EditorStyles.miniLabel);
                    GUI.color = Color.white;
                    EditorGUILayout.EndHorizontal();

                    // 重新安装和强制安装按钮
                    EditorGUILayout.Space(5);
                    EditorGUILayout.BeginHorizontal();

                    GUI.backgroundColor = Color.yellow; // 橙色表示重新安装
                    if (GUILayout.Button($"🔄 重新安装", GUILayout.Height(35)))
                    {
                        InstallPackage(package, false);
                    } 
                    GUI.backgroundColor = Color.red; // 红色表示强制安装
                    if (GUILayout.Button($"⚡ 强制安装", GUILayout.Height(35)))
                    {
                        InstallPackage(package, true);
                    }
                    GUI.backgroundColor = Color.white;

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox("• 重新安装：弹出确认对话框，检查依赖\n• 强制安装：直接安装，跳过确认和依赖检查", MessageType.Warning);

                    if (!string.IsNullOrEmpty(package.installNotes))
                    {
                        EditorGUILayout.HelpBox($"安装说明: {package.installNotes}", MessageType.Info);
                    }
                    break;

                case PackageInstallState.NotInstalled:
                    // 检查包文件
                    if (!Directory.Exists(packagePath))
                    {
                        EditorGUILayout.HelpBox("包文件夹不存在", MessageType.Error);
                    }
                    else
                    {
                        // 显示包文件信息
                        string[] scannedFiles = Directory.GetFiles(packagePath, "*.unitypackage");

                        if (scannedFiles.Length == 0)
                        {
                            EditorGUILayout.HelpBox("没有找到 .unitypackage 文件", MessageType.Warning);
                        }
                        else
                        {
                            EditorGUILayout.HelpBox($"找到 {scannedFiles.Length} 个Unity Package文件", MessageType.Info);
                            DrawUnityPackageTrustSummary(package, packagePath, scannedFiles.Length);

                            // 显社找到的包文件
                            EditorGUILayout.LabelField("包文件列表:", EditorStyles.miniBoldLabel);
                            foreach (string file in scannedFiles)
                            {
                                EditorGUILayout.LabelField($"  • {Path.GetFileName(file)}", EditorStyles.miniLabel);
                            }

                            EditorGUILayout.Space(5);

                            // 检查依赖状态
                            bool areDependenciesSatisfied3 = CheckPackageDependencies(package);
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField("依赖状态", EditorStyles.miniBoldLabel);
                            GUI.color = areDependenciesSatisfied3 ? Color.green : Color.red;
                            EditorGUILayout.LabelField(areDependenciesSatisfied3 ? "✓ 依赖满足" : "✗ 依赖不满足", EditorStyles.miniLabel);
                            GUI.color = Color.white;
                            EditorGUILayout.EndHorizontal();

                            if (!areDependenciesSatisfied3)
                            {
                                EditorGUILayout.HelpBox("必需的依赖项未满足，无法安装此包。请先安装所有必需的依赖项。", MessageType.Warning);
                                
                                // 强制安装选项
                                EditorGUILayout.Space(5);
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("或者:", EditorStyles.miniBoldLabel);
                                GUI.backgroundColor = Color.red;
                                if (GUILayout.Button($"⚡ 强制安装 (跳过依赖检查)", GUILayout.Width(200)))
                                {
                                    bool confirmForceInstall = EditorUtility.DisplayDialog(
                                        "强制安装确认",
                                        $"您确定要强制安装 {package.displayName} 吗？\n\n这将跳过依赖检查，可能导致安装不完整或出现问题。",
                                        "强制安装",
                                        "取消"
                                    );
                                    
                                    if (confirmForceInstall)
                                    {
                                        InstallPackage(package, true);
                                    }
                                }
                                GUI.backgroundColor = Color.white;
                                EditorGUILayout.EndHorizontal();
                            }

                            EditorGUILayout.Space(5);

                            // 安装按钮
                            EditorGUI.BeginDisabledGroup(!areDependenciesSatisfied3);
                            GUI.backgroundColor = Color.green;
                            if (GUILayout.Button($"🚀 安装 {package.displayName}", GUILayout.Height(40)))
                            {
                                InstallPackage(package, false);
                            }
                            GUI.backgroundColor = Color.white;
                            EditorGUI.EndDisabledGroup();

                            EditorGUILayout.Space(5);
                            EditorGUILayout.HelpBox("点击安装后将弹出Unity标准导入面板，您可以选择要导入的资源", MessageType.Info);

                            if (!string.IsNullOrEmpty(package.installNotes))
                            {
                                EditorGUILayout.HelpBox($"安装说明: {package.installNotes}", MessageType.Info);
                            }
                        }
                    }
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawUnityPackageTrustSummary(
            ESPackageBase package,
            string packagePath,
            int unityPackageCount)
        {
            string manifestPath = Path.Combine(packagePath, UnityPackageTrustManifestFileName);
            if (!File.Exists(manifestPath))
            {
                EditorGUILayout.HelpBox(
                    "缺少 " + UnityPackageTrustManifestFileName + "；旧 ESInstaller 会按 fail-closed 拒绝安装。请使用 UnityPackage 工具的“发布打包”生成正式主包与清单。",
                    MessageType.Error);
                return;
            }

            try
            {
                UnityPackageTrustManifest manifest = JsonUtility.FromJson<UnityPackageTrustManifest>(
                    File.ReadAllText(manifestPath, Encoding.UTF8));
                if (manifest == null)
                {
                    EditorGUILayout.HelpBox("签名清单无法解析。", MessageType.Error);
                    return;
                }

                bool identityMatches = string.Equals(manifest.packageId, package.packageId, StringComparison.Ordinal)
                    && string.Equals(manifest.packageVersion, package.version, StringComparison.Ordinal);
                bool artifactCountMatches = manifest.artifacts != null
                    && manifest.artifacts.Count == unityPackageCount;
                MessageType type = identityMatches && artifactCountMatches
                    ? MessageType.Info
                    : MessageType.Error;
                string trustKind = string.Equals(
                    manifest.keyId,
                    ESInstallerPackageTrust.LocalDevelopmentKeyId,
                    StringComparison.Ordinal)
                    ? "本机开发签名"
                    : "生产签名";
                EditorGUILayout.HelpBox(
                    "供应链清单：" + trustKind
                    + " | keyId " + manifest.keyId
                    + " | 版本 " + manifest.packageVersion
                    + " | 声明 " + (manifest.artifacts?.Count ?? 0) + " 个包"
                    + (identityMatches ? string.Empty : "\n清单 packageId/version 与当前安装配置不一致。")
                    + (artifactCountMatches ? string.Empty : "\n清单 artifact 数量与目录内 .unitypackage 数量不一致。")
                    + "\n安装时仍会完整复核 RSA 签名、Size、SHA-256 和可信暂存。",
                    type);
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox("读取签名清单失败：" + exception.Message, MessageType.Error);
            }
        }

        /// <summary>
        /// 绘制扩展包信息
        /// </summary>
        private void DrawExtensionPackageInfo(ESExtensionPackage package)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📦 扩展包信息", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField($"名称: {package.displayName}");
            EditorGUILayout.LabelField($"版本: {package.version}");
            EditorGUILayout.LabelField($"描述: {package.description}");
            if (!string.IsNullOrEmpty(package.author))
                EditorGUILayout.LabelField($"作者: {package.author}");
            if (!string.IsNullOrEmpty(package.license))
                EditorGUILayout.LabelField($"许可证: {package.license}");
            if (!string.IsNullOrEmpty(package.website))
                EditorGUILayout.LabelField($"官网: {package.website}");

            if (package.tags != null && package.tags.Count > 0)
            {
                string tagsStr = string.Join(", ", package.tags.ToArray());
                EditorGUILayout.LabelField($"标签: {tagsStr}");
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制扩展包的Unity依赖
        /// </summary>
        private void DrawExtensionUnityDependencies(ESExtensionPackage package)
        {
            DrawUnityDependencies(package.unityDependencies, "📦 Unity包依赖", false, false);
        }

        /// <summary>
        /// 绘制扩展包的Git依赖
        /// </summary>
        private void DrawExtensionGitDependencies(ESExtensionPackage package)
        {
            DrawGitDependencies(package.gitDependencies, "🔗 Git包依赖", false, false);
        }

        /// <summary>
        /// 绘制扩展包的用户包依赖
        /// </summary>
        private void DrawExtensionUserDependencies(ESExtensionPackage package)
        {
            DrawUserDependencies(package.userDependencies, "👤 用户包依赖", false, false);
        }

        /// <summary>
        /// 绘制扩展包安装部分
        /// </summary>
        private void DrawExtensionPackageInstallationSection(ESExtensionPackage package)
        {
            // 检查主包是否已安装
            if (currentProfile.mainPackage.installState != PackageInstallState.Installed)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.HelpBox("⚠️ 主包尚未安装！\n\n所有扩展包都依赖于主包，请先安装主包。", MessageType.Error);

                if (GUILayout.Button("🏠 前往主包安装界面"))
                {
                    // 切换到主包视图
                    Repaint();
                }

                EditorGUILayout.EndVertical();
                return;
            }

            // 使用通用的绘制方法
            DrawPackageInstallation(package, $"🚀 安装 {package.displayName}");





        }

        private void DrawBottomButtons()
        {
            EditorGUILayout.Space(10);

            if (!string.IsNullOrWhiteSpace(lastImportReceiptPath))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField("最近受信导入收据", EditorStyles.miniBoldLabel, GUILayout.Width(110));
                EditorGUILayout.SelectableLabel(lastImportReceiptPath, EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("定位", GUILayout.Width(48)))
                    EditorUtility.RevealInFinder(lastImportReceiptPath);
                if (GUILayout.Button("复制路径", GUILayout.Width(70)))
                    EditorGUIUtility.systemCopyBuffer = lastImportReceiptPath;
                EditorGUILayout.EndHorizontal();
            }
        }

        #endregion

        #region 功能方法

        /// <summary>
        /// 安装主包
        /// </summary>
        /// <summary>
        /// 通用的包安装方法
        /// </summary>
        private void InstallPackage(ESPackageBase package, bool forceInstall = false)
        {
            if (!TryValidatePackageGitDependencies(package, out string gitPinError))
            {
                ShowStatus("安装被供应链门禁拒绝：" + gitPinError, MessageType.Error);
                return;
            }

            // 检查是否已安装，如果是则弹出提示（除非是强制安装）
            if (package.installState == PackageInstallState.Installed && !forceInstall)
            {
                bool confirmReinstall = EditorUtility.DisplayDialog(
                    "重复安装确认",
                    $"{package.displayName} 似乎已经安装。\n\n是否要重复安装？\n\n注意：重复安装可能会覆盖现有文件。",
                    "继续安装",
                    "取消"
                );

                if (!confirmReinstall)
                {
                    ShowStatus("安装已取消", MessageType.Info);
                    return;
                }
            }

            string packagePath = GetSafePackageFolderPath(package);

            if (!Directory.Exists(packagePath))
            {
                ShowStatus("包文件夹不存在", MessageType.Error);
                return;
            }

            // 检查必需的依赖项是否都已满足（强制安装可以跳过此检查）
            if (!forceInstall && !CheckPackageDependencies(package))
            {
                ShowStatus($"无法安装 {package.displayName}：必需的依赖项未满足", MessageType.Error);
                return;
            }

            string installMode = forceInstall ? "强制安装" : "安装";
            if (!TryPrepareTrustedUnityPackages(package, out List<VerifiedUnityPackage> verifiedPackages, out string trustError))
            {
                ShowStatus("安装被 .unitypackage 供应链门禁拒绝：" + trustError, MessageType.Error);
                return;
            }

            if (!ConfirmTrustedImportPreview(verifiedPackages, package.displayName + " " + installMode))
            {
                foreach (VerifiedUnityPackage verified in verifiedPackages)
                    CleanupTrustedStagingDirectoryIfUnused(verified.stagingDirectory);
                ShowStatus("安装已在影响预览阶段取消。", MessageType.Info);
                return;
            }

            if (!TryQueueTrustedImports(verifiedPackages, package.displayName + " " + installMode, out string queueError))
            {
                foreach (VerifiedUnityPackage verified in verifiedPackages)
                    CleanupTrustedStagingDirectoryIfUnused(verified.stagingDirectory);
                ShowStatus("安装未启动：" + queueError, MessageType.Error);
                return;
            }

            // 延迟检查安装状态
            EditorApplication.delayCall += () =>
            {
                _ = CheckPackageInstallStateAsync(package);
            };
        }

        /// <summary>
        /// 检查包的依赖是否满足
        /// </summary>
        private bool CheckPackageDependencies(ESPackageBase package)
        {
            bool allValid = true;

            // 检查Unity包依赖
            if (package.unityDependencies != null)
            {
                foreach (var dep in package.unityDependencies)
                {
                    if (dep.isRequired && !dep.isInstalled)
                    {
                        allValid = false;
                        break;
                    }
                }
            }

            // 检查Git包依赖
            if (allValid && package.gitDependencies != null)
            {
                foreach (var dep in package.gitDependencies)
                {
                    if (!TryValidatePinnedGitUrl(dep?.gitUrl, out _, out _, out _)
                        || (dep.isRequired && !dep.isInstalled))
                    {
                        allValid = false;
                        break;
                    }
                }
            }

            // 检查用户包依赖
            if (allValid && package.userDependencies != null)
            {
                foreach (var dep in package.userDependencies)
                {
                    if (dep.isRequired && !dep.isInstalled)
                    {
                        allValid = false;
                        break;
                    }
                }
            }

            return allValid;
        }

        private void InitializeDefaultProfile()
        {
            if (currentProfile == null)
            {
                currentProfile = new InstallationProfile();
            }

            currentProfile.mainPackage.folderName = "Main";
            currentProfile.lastModified = DateTime.Now;
        }

        private void SaveConfiguration()
        {
            try
            {
                if (!TryValidateMainPackageConfiguration(out string validationError))
                {
                    ShowStatus("配置未保存：" + validationError, MessageType.Error);
                    return;
                }

                currentProfile.lastModified = DateTime.Now;
                ExtensionPackageJsonData jsonData = CreateJsonData(currentProfile.mainPackage);
                string json = JsonUtility.ToJson(jsonData, true);
                ESManagedFileIO.WriteTextAtomic(configFilePath, json, new UTF8Encoding(false), Application.dataPath);
                isConfigModified = false; // 重置未保存更改标志
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogError($"保存配置失败: {e.Message}");
                ShowStatus($"保存配置失败: {e.Message}", MessageType.Error);
            }
        }

        private void LoadSavedConfiguration()
        {
            try
            {
                LoadConfiguration();
                isConfigModified = false;
            }
            catch (Exception e)
            {
                Debug.LogError($"加载配置失败: {e.Message}");
                ShowStatus($"加载配置失败: {e.Message}", MessageType.Error);
                InitializeDefaultProfile();
            }
        }

        private bool TryValidateMainPackageConfiguration(out string error)
        {
            error = string.Empty;
            ESMainPackage package = currentProfile?.mainPackage;
            if (package == null)
            {
                error = "主包配置为空。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(package.displayName) || string.IsNullOrWhiteSpace(package.version))
            {
                error = "主包名称和版本不能为空。";
                return false;
            }

            foreach (UnityPackageDependency dependency in package.unityDependencies ?? new List<UnityPackageDependency>())
            {
                if (dependency == null || string.IsNullOrWhiteSpace(dependency.name) || string.IsNullOrWhiteSpace(dependency.packageId))
                {
                    error = "Unity 依赖必须填写名称和 Package ID。";
                    return false;
                }
            }
            foreach (GitPackageDependency dependency in package.gitDependencies ?? new List<GitPackageDependency>())
            {
                if (dependency == null || string.IsNullOrWhiteSpace(dependency.name))
                {
                    error = "Git 依赖必须填写名称。";
                    return false;
                }
                if (!TryValidatePinnedGitUrl(dependency.gitUrl, out _, out _, out string pinError))
                {
                    error = "Git 依赖“" + dependency.name + "”无效：" + pinError;
                    return false;
                }
            }
            foreach (UserPackageDependency dependency in package.userDependencies ?? new List<UserPackageDependency>())
            {
                if (dependency == null || string.IsNullOrWhiteSpace(dependency.name) || string.IsNullOrWhiteSpace(dependency.checkClass))
                {
                    error = "手动依赖必须填写名称和检查类名。";
                    return false;
                }
            }
            foreach (AssetFileDependency dependency in package.assetFileDependencies ?? new List<AssetFileDependency>())
            {
                if (dependency == null || string.IsNullOrWhiteSpace(dependency.name) || string.IsNullOrWhiteSpace(dependency.assetPath))
                {
                    error = "资产依赖必须填写名称和 Assets 路径。";
                    return false;
                }
                string normalizedPath = dependency.assetPath.Replace('\\', '/');
                if (!normalizedPath.StartsWith("Assets/", StringComparison.Ordinal)
                    || normalizedPath.Contains("/../")
                    || normalizedPath.EndsWith("/..", StringComparison.Ordinal))
                {
                    error = "资产依赖路径不安全：" + dependency.assetPath;
                    return false;
                }
            }
            return true;
        }

        private static ExtensionPackageJsonData CreateJsonData(ESMainPackage package)
        {
            return new ExtensionPackageJsonData
            {
                displayName = package.displayName,
                folderName = "Main",
                version = package.version,
                description = package.description,
                unityDependencies = (package.unityDependencies ?? new List<UnityPackageDependency>()).Select(dependency => new DependencyJsonData
                {
                    name = dependency.name,
                    version = dependency.version,
                    description = dependency.description,
                    isRequired = dependency.isRequired,
                    checkClass = dependency.checkClass,
                    packageId = dependency.packageId,
                    installUrl = dependency.installUrl
                }).ToArray(),
                gitDependencies = (package.gitDependencies ?? new List<GitPackageDependency>()).Select(dependency => new GitDependencyJsonData
                {
                    name = dependency.name,
                    version = dependency.version,
                    description = dependency.description,
                    gitUrl = dependency.gitUrl,
                    checkClass = dependency.checkClass,
                    isRequired = dependency.isRequired
                }).ToArray(),
                userDependencies = (package.userDependencies ?? new List<UserPackageDependency>()).Select(dependency => new UserDependencyJsonData
                {
                    name = dependency.name,
                    version = dependency.version,
                    description = dependency.description,
                    checkClass = dependency.checkClass,
                    installInstructions = dependency.installInstructions,
                    isRequired = dependency.isRequired
                }).ToArray(),
                assetFileDependencies = (package.assetFileDependencies ?? new List<AssetFileDependency>()).Select(dependency => new AssetFileDependencyJsonData
                {
                    name = dependency.name,
                    version = dependency.version,
                    description = dependency.description,
                    assetPath = dependency.assetPath,
                    checkClass = dependency.checkClass,
                    isRequired = dependency.isRequired
                }).ToArray(),
                requiredMainPackages = Array.Empty<string>(),
                installationNotes = package.installNotes,
                checkClass = package.checkClass,
                assetPath = package.assetPath,
                tags = package.tags?.ToArray() ?? Array.Empty<string>(),
                author = package.author,
                website = package.website,
                license = package.license
            };
        }

        private async Task CheckAllUnityPackages()
        {
            if (currentProfile == null || currentProfile.mainPackage.unityDependencies == null)
            {
                ShowStatus("配置未加载，无法检查Unity包", MessageType.Warning);
                return;
            }

            ShowStatus("正在检查所有Unity官方包...", MessageType.Info);
            InstalledPackageSnapshot snapshot = await CaptureInstalledPackageSnapshotAsync();
            if (!snapshot.IsAvailable)
            {
                ShowStatus(snapshot.FailureMessage, MessageType.Error);
                return;
            }
            UpmPackageInfo[] installedPackages = snapshot.Packages;
            foreach (UnityPackageDependency dependency in currentProfile.mainPackage.unityDependencies)
                if (dependency != null)
                    dependency.isInstalled = CheckUnityPackageInstalled(dependency, installedPackages);

            ShowStatus("Unity官方包检查完成", MessageType.Info);
            Repaint();
        }

        private async Task InstallAllUnityPackages()
        {
            ShowStatus("正在安装所有Unity官方包...", MessageType.Info);

            foreach (var dependency in currentProfile.mainPackage.unityDependencies.Where(d => !d.isInstalled))
            {
                await InstallUnityPackageDependency(dependency);
                await Task.Delay(100); // 给安装一些时间
            }

            ShowStatus("Unity官方包安装完成", MessageType.Info);
            Repaint();
        }

        private async Task CheckUnityPackageDependency(UnityPackageDependency dependency)
        {
            if (dependency == null)
            {
                Debug.LogWarning("UnityPackageDependency 为空");
                return;
            }
            try
            {
                InstalledPackageSnapshot snapshot = await CaptureInstalledPackageSnapshotAsync();
                if (!snapshot.IsAvailable)
                    throw new InvalidOperationException(snapshot.FailureMessage);
                dependency.isInstalled = CheckUnityPackageInstalled(dependency, snapshot.Packages);
                ShowStatus(
                    dependency.isInstalled
                        ? $"Unity包 {dependency.name} 已安装"
                        : $"Unity包 {dependency.name} 未安装",
                    dependency.isInstalled ? MessageType.Info : MessageType.Warning);
            }
            catch (Exception e)
            {
                ShowStatus($"检查Unity包 {dependency.name} 异常: {e.Message}", MessageType.Error);
            }

            Repaint();
        }

        private async Task InstallUnityPackageDependency(UnityPackageDependency dependency)
        {
            if (string.IsNullOrEmpty(dependency.packageId))
            {
                ShowStatus($"Unity包 {dependency.name} 缺少Package ID", MessageType.Error);
                return;
            }

            AddRequest request;
            try
            {
                // 在主线程同步发起请求（这不会阻塞）
                request = Client.Add(dependency.packageId);
                ShowStatus($"正在安装Unity包 {dependency.name}...", MessageType.Info);
            }
            catch (Exception e)
            {
                ShowStatus($"安装Unity包 {dependency.name} 异常: {e.Message}", MessageType.Error);
                return;
            }

            await WaitForAddRequestCompletion(request);

            try
            {
                if (request.Status == StatusCode.Success)
                {
                    dependency.isInstalled = true;
                    ShowStatus($"Unity包 {dependency.name} 安装成功", MessageType.Info);
                }
                else
                {
                    ShowStatus($"Unity包 {dependency.name} 安装失败: {request.Error.message}", MessageType.Error);
                }
            }
            catch (Exception e)
            {
                ShowStatus($"安装Unity包 {dependency.name} 异常: {e.Message}", MessageType.Error);
            }

            Repaint();
        }

        private async Task CheckGitPackageDependency(GitPackageDependency dependency)
        {
            if (dependency == null)
            {
                ShowStatus("Git包依赖为空，无法进行供应链校验。", MessageType.Error);
                return;
            }
            if (!TryValidatePinnedGitUrl(dependency.gitUrl, out _, out _, out string pinError))
            {
                dependency.isInstalled = false;
                ShowStatus($"Git包 {dependency.name} 被供应链门禁拒绝：{pinError}", MessageType.Error);
                return;
            }

            try
            {
                InstalledPackageSnapshot snapshot = await CaptureInstalledPackageSnapshotAsync();
                if (!snapshot.IsAvailable)
                    throw new InvalidOperationException(snapshot.FailureMessage);
                dependency.isInstalled = CheckGitPackageInstalled(dependency, snapshot.Packages);
                ShowStatus(
                    dependency.isInstalled
                        ? $"Git包 {dependency.name} 已安装"
                        : $"Git包 {dependency.name} 未安装",
                    dependency.isInstalled ? MessageType.Info : MessageType.Warning);
            }
            catch (Exception e)
            {
                ShowStatus($"检查Git包 {dependency.name} 异常: {e.Message}", MessageType.Error);
            }

            Repaint();
        }

        private async Task InstallGitPackageDependency(GitPackageDependency dependency)
        {
            if (dependency == null)
            {
                ShowStatus("Git包依赖为空，无法安装。", MessageType.Error);
                return;
            }
            if (!TryValidatePinnedGitUrl(dependency.gitUrl, out string pinnedGitUrl, out string commit, out string pinError))
            {
                dependency.isInstalled = false;
                ShowStatus($"Git包 {dependency.name} 被供应链门禁拒绝：{pinError}", MessageType.Error);
                return;
            }

            AddRequest request;
            try
            {
                // 在主线程同步发起请求（这不会阻塞）
                request = Client.Add(pinnedGitUrl);
                ShowStatus($"正在安装Git包 {dependency.name}（固定 commit {commit.Substring(0, 12)}…）...", MessageType.Info);
            }
            catch (Exception e)
            {
                ShowStatus($"安装Git包 {dependency.name} 异常: {e.Message}", MessageType.Error);
                return;
            }

            await WaitForAddRequestCompletion(request);

            try
            {
                if (request.Status == StatusCode.Success)
                {
                    dependency.isInstalled = true;
                    ShowStatus($"Git包 {dependency.name} 安装成功", MessageType.Info);
                }
                else
                {
                    ShowStatus($"Git包 {dependency.name} 安装失败: {request.Error.message}", MessageType.Error);
                }
            }
            catch (Exception e)
            {
                ShowStatus($"安装Git包 {dependency.name} 异常: {e.Message}", MessageType.Error);
            }

            Repaint();
        }

        private Task CheckUserPackageDependency(UserPackageDependency dependency)
        {
            if (string.IsNullOrEmpty(dependency.checkClass))
            {
                dependency.isInstalled = false;
                ShowStatus($"用户包 {dependency.name} 缺少检查类名", MessageType.Warning);
                return Task.CompletedTask;
            }

            try
            {
                bool classFound = IsClassExists(dependency.checkClass);

                dependency.isInstalled = classFound;
                if (classFound)
                {
                    ShowStatus($"用户包 {dependency.name} 已安装 (通过类验证)", MessageType.Info);
                }
                else
                {
                    ShowStatus($"用户包 {dependency.name} 未安装", MessageType.Warning);
                }
            }
            catch (Exception e)
            {
                dependency.isInstalled = false;
                ShowStatus($"检查用户包 {dependency.name} 异常: {e.Message}", MessageType.Error);
            }

            Repaint();
            return Task.CompletedTask;
        }

        private async Task CheckAllGitPackages()
        {
            if (currentProfile == null || currentProfile.mainPackage.gitDependencies == null)
            {
                ShowStatus("配置未加载，无法检查Git包", MessageType.Warning);
                return;
            }

            ShowStatus("正在检查所有Git包...", MessageType.Info);
            InstalledPackageSnapshot snapshot = await CaptureInstalledPackageSnapshotAsync();
            if (!snapshot.IsAvailable)
            {
                ShowStatus(snapshot.FailureMessage, MessageType.Error);
                return;
            }
            UpmPackageInfo[] installedPackages = snapshot.Packages;
            foreach (GitPackageDependency dependency in currentProfile.mainPackage.gitDependencies)
                if (dependency != null)
                    dependency.isInstalled = CheckGitPackageInstalled(dependency, installedPackages);

            ShowStatus("Git包检查完成", MessageType.Info);
            Repaint();
        }

        private async Task InstallAllGitPackages()
        {
            ShowStatus("正在安装所有Git包...", MessageType.Info);

            foreach (var dependency in currentProfile.mainPackage.gitDependencies.Where(d => !d.isInstalled))
            {
                await InstallGitPackageDependency(dependency);
                await Task.Delay(100);
            }

            ShowStatus("Git包安装完成", MessageType.Info);
            Repaint();
        }

        private async Task CheckAllUserPackages()
        {
            if (currentProfile == null || currentProfile.mainPackage.userDependencies == null)
            {
                ShowStatus("配置未加载，无法检查用户包", MessageType.Warning);
                return;
            }

            ShowStatus("正在检查所有用户包...", MessageType.Info);

            foreach (var dependency in currentProfile.mainPackage.userDependencies)
            {
                await CheckUserPackageDependency(dependency);
            }

            ShowStatus("用户包检查完成", MessageType.Info);
            Repaint();
        }

        private bool CheckAllDependenciesValid()
        {
            if (currentProfile == null ||
                currentProfile.mainPackage.unityDependencies == null ||
                currentProfile.mainPackage.gitDependencies == null ||
                currentProfile.mainPackage.userDependencies == null)
            {
                return false;
            }

            bool unityPackagesValid = currentProfile.mainPackage.unityDependencies.All(d => !d.isRequired || d.isInstalled);
            bool gitPackagesValid = currentProfile.mainPackage.gitDependencies.All(d => d != null
                && TryValidatePinnedGitUrl(d.gitUrl, out _, out _, out _)
                && (!d.isRequired || d.isInstalled));
            bool userPackagesValid = currentProfile.mainPackage.userDependencies.All(d => !d.isRequired || d.isInstalled);

            // 检查ES包系统：主包必需，选中的扩展包也必需
            bool esPackagesValid = true;

            // 检查主包
            string mainPackagePath = GetSafePackageFolderPath(currentProfile.mainPackage);
            if (!Directory.Exists(mainPackagePath))
            {
                esPackagesValid = false;
            }
            else
            {
                string[] mainPackages = Directory.GetFiles(mainPackagePath, "*.unitypackage");
                if (mainPackages.Length == 0)
                {
                    esPackagesValid = false;
                }
            }

            // 检查选中的扩展包
            if (currentProfile.extensionPackages != null)
            {
                foreach (var extPackage in currentProfile.extensionPackages.Where(e => e.isSelectedForInstall))
                {
                    string extFolderPath = GetSafePackageFolderPath(extPackage);
                    if (!Directory.Exists(extFolderPath))
                    {
                        esPackagesValid = false;
                        break;
                    }
                    string[] extPackages = Directory.GetFiles(extFolderPath, "*.unitypackage");
                    if (extPackages.Length == 0)
                    {
                        esPackagesValid = false;
                        break;
                    }
                }
            }

            return unityPackagesValid && gitPackagesValid && userPackagesValid && esPackagesValid;
        }

        private void StartInstallation()
        {
            if (!TryValidatePackageGitDependencies(currentProfile?.mainPackage, out string mainGitPinError))
            {
                ShowStatus("主包安装被供应链门禁拒绝：" + mainGitPinError, MessageType.Error);
                return;
            }

            var packagesToInstall = new List<ESPackageBase> { currentProfile.mainPackage };
            foreach (ESExtensionPackage extension in currentProfile.extensionPackages ?? new List<ESExtensionPackage>())
            {
                if (!extension.isSelectedForInstall) continue;
                if (!TryValidatePackageGitDependencies(extension, out string extensionGitPinError))
                {
                    ShowStatus("扩展包“" + extension.displayName + "”被供应链门禁拒绝：" + extensionGitPinError, MessageType.Error);
                    return;
                }
                packagesToInstall.Add(extension);
            }

            var verifiedPackages = new List<VerifiedUnityPackage>();
            foreach (ESPackageBase package in packagesToInstall)
            {
                if (!TryPrepareTrustedUnityPackages(package, out List<VerifiedUnityPackage> prepared, out string trustError))
                {
                    foreach (VerifiedUnityPackage verified in verifiedPackages)
                        CleanupTrustedStagingDirectoryIfUnused(verified.stagingDirectory);
                    ShowStatus("批量安装被 .unitypackage 供应链门禁拒绝（" + package.displayName + "）：" + trustError, MessageType.Error);
                    return;
                }
                verifiedPackages.AddRange(prepared);
            }

            if (!ConfirmTrustedImportPreview(verifiedPackages, "ES 框架批量安装"))
            {
                foreach (VerifiedUnityPackage verified in verifiedPackages)
                    CleanupTrustedStagingDirectoryIfUnused(verified.stagingDirectory);
                ShowStatus("批量安装已在影响预览阶段取消。", MessageType.Info);
                return;
            }

            if (!TryQueueTrustedImports(verifiedPackages, "ES 框架批量安装", out string queueError))
            {
                foreach (VerifiedUnityPackage verified in verifiedPackages)
                    CleanupTrustedStagingDirectoryIfUnused(verified.stagingDirectory);
                ShowStatus("批量安装未启动：" + queueError, MessageType.Error);
            }
        }

        private async void RefreshAllStatuses()
        {
            if (isRefreshingStatuses || currentProfile == null)
            {
                return;
            }

            isRefreshingStatuses = true;
            ShowStatus("正在全面刷新所有状态...", MessageType.Info);

            try
            {
                // 1. 重新加载配置文件
                LoadSavedConfiguration();

                // 2. 基于同一 canonical profile 检查包和四类依赖状态。
                await CheckAllPackagesInstallStateAsync();

                ShowStatus("所有状态刷新完成", MessageType.Info);
            }
            catch (Exception e)
            {
                ShowStatus($"刷新状态时出现错误: {e.Message}", MessageType.Error);
                Debug.LogError($"RefreshAllStatuses error: {e}");
            }
            finally
            {
                isRefreshingStatuses = false;
                ESWindow_CurrentPageContext?.RefreshPageActions();
                Repaint();
            }
        }

        private void GenerateInstallationReport()
        {
            string report = "ES框架安装报告\n";
            report += $"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n";

            report += "Unity官方包:\n";
            foreach (var dep in currentProfile.mainPackage.unityDependencies)
            {
                report += $"- {dep.name} ({dep.version}): {(dep.isInstalled ? "已安装" : "未安装")}\n";
            }

            report += "\nGit包:\n";
            foreach (var dep in currentProfile.mainPackage.gitDependencies)
            {
                report += $"- {dep.name} ({dep.version}): {(dep.isInstalled ? "已安装" : "未安装")}\n";
            }

            report += "\n用户包:\n";
            foreach (var dep in currentProfile.mainPackage.userDependencies)
            {
                report += $"- {dep.name} ({dep.version}): {(dep.isInstalled ? "已安装" : "未安装")}\n";
            }

            report += $"\n主包: {currentProfile.mainPackage.displayName} v{currentProfile.mainPackage.version}\n";
            string mainPackagePath = GetSafePackageFolderPath(currentProfile.mainPackage);
            report += $"主包文件夹: {mainPackagePath}\n";

            // 统计主包文件
            if (Directory.Exists(mainPackagePath))
            {
                string[] mainPackages = Directory.GetFiles(mainPackagePath, "*.unitypackage");
                report += $"主包文件 ({mainPackages.Length}个):\n";
                foreach (string packagePath in mainPackages)
                {
                    report += $"  • {Path.GetFileName(packagePath)}\n";
                }
            }
            else
            {
                report += "主包文件夹不存在\n";
            }

            // 扩展包信息
            if (currentProfile.extensionPackages != null && currentProfile.extensionPackages.Count > 0)
            {
                report += $"\n扩展包配置 ({currentProfile.extensionPackages.Count}个):\n";
                foreach (var extPackage in currentProfile.extensionPackages)
                {
                    string status = extPackage.isSelectedForInstall ? "已选择" : "未选择";
                    string folderPath = GetSafePackageFolderPath(extPackage);
                    report += $"  • {extPackage.displayName} v{extPackage.version} ({status})\n";
                    report += $"    文件夹: {folderPath}\n";

                    if (Directory.Exists(folderPath))
                    {
                        string[] extPackages = Directory.GetFiles(folderPath, "*.unitypackage");
                        report += $"    包文件 ({extPackages.Length}个):\n";
                        foreach (string packagePath in extPackages)
                        {
                            report += $"      - {Path.GetFileName(packagePath)}\n";
                        }
                    }
                    else
                    {
                        report += $"    文件夹不存在\n";
                    }
                }
            }

            report += $"安装说明: {currentProfile.installationNotes}\n";

            // 保存报告到当前文件夹
            string reportFileName = $"ES_Installation_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string reportPath = Path.Combine(downloadsFolderPath, reportFileName);
            ESManagedFileIO.WriteTextAtomic(reportPath, report, new UTF8Encoding(false), downloadsFolderPath);
            AssetDatabase.Refresh();

            ShowStatus($"安装报告已生成: {reportPath}", MessageType.Info);
            if (EditorUtility.DisplayDialog("安装报告已生成", reportPath, "打开所在文件夹", "关闭"))
                EditorUtility.RevealInFinder(reportPath);
        }

        private void ShowHelp()
        {
            string helpText = @"ES框架安装管理器使用帮助:

1. 配置管理:
   - 配置名称: 为当前配置设置一个易记的名称
   - 自动检查设置: 控制编辑器启动时是否自动检查依赖状态
   - 保存/加载配置: 将配置保存到JSON文件或从文件加载

2. 插件依赖:
   - Package ID: Unity Package Manager的包标识符
   - 安装URL: 可选的手动安装URL
   - 检查: 验证插件是否已安装
   - 安装: 通过UPM安装插件

4. 安装管理:
   - Unity Package父文件夹: 存放Unity Package文件的文件夹路径
   - 文件扫描: 自动扫描文件夹中的所有 .unitypackage 文件
   - 安装说明: 安装相关的说明信息
   - 依赖状态: 显示所有依赖是否满足
   - 开始安装: 导入扫描到的所有Unity Package文件

5. 自动检查功能:
   - 编辑器启动时自动检查所有必需依赖的安装状态
   - 如果发现未安装的依赖，会弹出提醒对话框
   - 可以选择立即打开安装器或稍后提醒
   - 可以在设置中禁用此功能

注意: 安装前请确保所有必需依赖都已正确安装。";

            EditorUtility.DisplayDialog("ES安装管理器帮助", helpText, "确定");
        }

        // private void ShowStatus(string message, MessageType type)
        // {
        //     statusMessage = message;
        //     statusType = type;
        // }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        /// <summary>
        /// 添加新的扩展包配置
        /// </summary>
        private void AddNewExtensionPackage()
        {
            if (currentProfile == null) return;

            var newPackage = new ESExtensionPackage
            {
                packageId = $"ext_{currentProfile.extensionPackages.Count + 1}",
                displayName = $"扩展包 {currentProfile.extensionPackages.Count + 1}",
                version = "1.0.0",
                description = "新扩展包描述",
                isRequired = false,
                installState = PackageInstallState.NotInstalled,
                isSelectedForInstall = false,
                folderName = $"Extension{currentProfile.extensionPackages.Count + 1}",
                unityDependencies = new List<UnityPackageDependency>(),
                gitDependencies = new List<GitPackageDependency>(),
                userDependencies = new List<UserPackageDependency>(),
                installNotes = "安装说明"
            };

            currentProfile.extensionPackages.Add(newPackage);
            ShowStatus($"已添加新扩展包: {newPackage.displayName}", MessageType.Info);
        }

        /// <summary>
        /// 显示状态消息
        /// </summary>
        private void ShowStatus(string message, MessageType type = MessageType.Info)
        {
            statusMessage = message;
            statusType = type;
            Repaint();
        }

        #endregion
    }

    internal static class ESInstallerPackageTrust
    {
        internal const string ManifestFileName = "es-unitypackage.manifest.json";
        internal const string LocalDevelopmentKeyId = "es-local-dev";

        private const string SigningKeyIdEnvironmentVariable = "ES_INSTALLER_SIGNING_KEY_ID";
        private const string SigningPrivateKeyPathEnvironmentVariable = "ES_INSTALLER_SIGNING_PRIVATE_KEY_PATH";

        private static readonly Dictionary<string, string> ProductionPublicKeys =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // Production public keys must be committed through code review. Private keys never belong in the project.
            };

        internal static bool TryPublishMainPackage(
            string exportedPackagePath,
            string packageId,
            string packageVersion,
            out string installedPackagePath,
            out string manifestPath,
            out string signingKeyId,
            out string error)
        {
            installedPackagePath = string.Empty;
            manifestPath = string.Empty;
            signingKeyId = string.Empty;
            error = string.Empty;

            try
            {
                ValidateIdentity(packageId, nameof(packageId));
                ValidateIdentity(packageVersion, nameof(packageVersion));

                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string outputRoot = Path.Combine(projectRoot, "ES", "Output", "UnityPackages");
                string mainRoot = Path.Combine(projectRoot, "Assets", "Plugins", "ES", "Editor", "Installer", "Downloads", "Main");
                string sourcePath = Path.GetFullPath(exportedPackagePath ?? string.Empty);
                ESManagedFileIO.EnsurePath(sourcePath, true, outputRoot);
                if (!File.Exists(sourcePath) || !sourcePath.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("发布源必须是 ES/Output/UnityPackages 下存在的 .unitypackage。");

                Directory.CreateDirectory(mainRoot);
                ESManagedFileIO.EnsurePath(mainRoot, false, mainRoot);
                ESManagedFileIO.EnsureNoNestedReparsePoints(mainRoot);

                string archiveRoot = ArchiveCurrentMainArtifacts(mainRoot, outputRoot);
                string targetPath = Path.Combine(mainRoot, Path.GetFileName(sourcePath));
                try
                {
                    ESManagedFileIO.CopyFileAtomic(sourcePath, targetPath, outputRoot, mainRoot);
                    if (!TryWriteSignedManifest(mainRoot, packageId, packageVersion, out manifestPath, out signingKeyId, out error))
                        throw new InvalidDataException(error);
                    installedPackagePath = targetPath;
                    return true;
                }
                catch
                {
                    TryDeletePublishedFile(targetPath, mainRoot);
                    TryDeletePublishedFile(Path.Combine(mainRoot, ManifestFileName), mainRoot);
                    RestoreArchivedMainArtifacts(archiveRoot, mainRoot, outputRoot);
                    throw;
                }
            }
            catch (Exception exception)
            {
                error = "发布到旧 ESInstaller 主目录失败：" + exception.Message;
                return false;
            }
        }

        internal static bool TryGetTrustedRsaPublicKey(string keyId, out string publicKeyXml)
        {
            publicKeyXml = null;
            if (string.IsNullOrWhiteSpace(keyId))
                return false;
            if (ProductionPublicKeys.TryGetValue(keyId, out publicKeyXml) && !string.IsNullOrWhiteSpace(publicKeyXml))
                return true;
            if (!string.Equals(keyId, LocalDevelopmentKeyId, StringComparison.Ordinal))
                return false;

            string path = GetLocalDevelopmentPublicKeyPath();
            if (!File.Exists(path) || ESManagedFileIO.ContainsExistingReparsePoint(path))
                return false;
            publicKeyXml = File.ReadAllText(path, Encoding.UTF8);
            return !string.IsNullOrWhiteSpace(publicKeyXml);
        }

        internal static string BuildCanonicalManifestPayload(
            UnityPackageTrustManifest manifest,
            IEnumerable<UnityPackageTrustArtifact> artifacts)
        {
            var ordered = new List<UnityPackageTrustArtifact>(artifacts ?? Enumerable.Empty<UnityPackageTrustArtifact>());
            ordered.Sort((left, right) => StringComparer.Ordinal.Compare(left.relativePath, right.relativePath));
            var builder = new StringBuilder();
            builder.Append("ESInstaller.UnityPackageManifest\n");
            builder.Append("schemaVersion=").Append(manifest.schemaVersion).Append('\n');
            AppendField(builder, "keyId", manifest.keyId);
            AppendField(builder, "packageId", manifest.packageId);
            AppendField(builder, "packageVersion", manifest.packageVersion);
            AppendField(builder, "source", manifest.source);
            builder.Append("artifactCount=").Append(ordered.Count).Append('\n');
            for (int i = 0; i < ordered.Count; i++)
            {
                UnityPackageTrustArtifact artifact = ordered[i];
                builder.Append("artifact[").Append(i).Append("]\n");
                AppendField(builder, "relativePath", artifact.relativePath);
                builder.Append("size=").Append(artifact.size).Append('\n');
                AppendField(builder, "sha256", artifact.sha256.ToLowerInvariant());
            }
            return builder.ToString();
        }

        private static bool TryWriteSignedManifest(
            string packageDirectory,
            string packageId,
            string packageVersion,
            out string manifestPath,
            out string signingKeyId,
            out string error)
        {
            manifestPath = Path.Combine(packageDirectory, ManifestFileName);
            signingKeyId = string.Empty;
            error = string.Empty;
            try
            {
                if (!TryResolveSigningKey(out signingKeyId, out string privateKeyXml, out string source, out error))
                    return false;
                if (!TryGetTrustedRsaPublicKey(signingKeyId, out string publicKeyXml))
                    throw new InvalidDataException(
                        "签名 keyId“" + signingKeyId + "”没有对应的安装器受信公钥；拒绝生成无法安装的发布物。");

                string[] packageFiles = Directory.GetFiles(packageDirectory, "*.unitypackage", SearchOption.TopDirectoryOnly);
                if (packageFiles.Length != 1)
                    throw new InvalidDataException("旧 ESInstaller 主目录必须且只能保留一个正式 .unitypackage。");

                var manifest = new UnityPackageTrustManifest
                {
                    schemaVersion = 1,
                    keyId = signingKeyId,
                    packageId = packageId,
                    packageVersion = packageVersion,
                    source = source,
                };

                foreach (string packageFile in packageFiles)
                {
                    if (!ESArtifactTrustVerifier.TryCaptureStableFileIdentity(packageFile, out long size, out string sha256, out string identityError))
                        throw new IOException("无法读取发布包身份：" + identityError);
                    manifest.artifacts.Add(new UnityPackageTrustArtifact
                    {
                        relativePath = Path.GetFileName(packageFile),
                        size = size,
                        sha256 = sha256.ToLowerInvariant(),
                    });
                }

                byte[] payload = Encoding.UTF8.GetBytes(BuildCanonicalManifestPayload(manifest, manifest.artifacts));
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.PersistKeyInCsp = false;
                    rsa.FromXmlString(privateKeyXml);
                    manifest.signature = Convert.ToBase64String(rsa.SignData(payload, CryptoConfig.MapNameToOID("SHA256")));
                }
                if (!ESArtifactTrustVerifier.TryVerifyRsaSha256(publicKeyXml, payload, manifest.signature, out string signatureError))
                    throw new CryptographicException("签名私钥与受信公钥不匹配：" + signatureError);

                ESManagedFileIO.WriteTextAtomic(manifestPath, JsonUtility.ToJson(manifest, true), new UTF8Encoding(false), packageDirectory);
                return true;
            }
            catch (Exception exception)
            {
                error = "签名清单生成失败：" + exception.Message;
                return false;
            }
        }

        private static bool TryResolveSigningKey(out string keyId, out string privateKeyXml, out string source, out string error)
        {
            keyId = (Environment.GetEnvironmentVariable(SigningKeyIdEnvironmentVariable) ?? string.Empty).Trim();
            string configuredPath = (Environment.GetEnvironmentVariable(SigningPrivateKeyPathEnvironmentVariable) ?? string.Empty).Trim();
            privateKeyXml = string.Empty;
            source = string.Empty;
            error = string.Empty;

            if (!string.IsNullOrEmpty(keyId) || !string.IsNullOrEmpty(configuredPath))
            {
                if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(configuredPath))
                {
                    error = SigningKeyIdEnvironmentVariable + " 与 " + SigningPrivateKeyPathEnvironmentVariable + " 必须同时配置。";
                    return false;
                }
                ValidateIdentity(keyId, SigningKeyIdEnvironmentVariable);
                string fullPath = Path.GetFullPath(configuredPath);
                if (!File.Exists(fullPath) || ESManagedFileIO.ContainsExistingReparsePoint(fullPath))
                {
                    error = "生产签名私钥不存在或位于重解析路径：" + fullPath;
                    return false;
                }
                privateKeyXml = File.ReadAllText(fullPath, Encoding.UTF8);
                source = "production-release";
                return !string.IsNullOrWhiteSpace(privateKeyXml);
            }

            keyId = LocalDevelopmentKeyId;
            source = "local-development";
            return TryGetOrCreateLocalDevelopmentKey(out privateKeyXml, out error);
        }

        private static bool TryGetOrCreateLocalDevelopmentKey(out string privateKeyXml, out string error)
        {
            privateKeyXml = string.Empty;
            error = string.Empty;
            try
            {
                string privatePath = GetLocalDevelopmentPrivateKeyPath();
                string publicPath = GetLocalDevelopmentPublicKeyPath();
                if (File.Exists(privatePath) && File.Exists(publicPath))
                {
                    string existingPrivateKey = File.ReadAllText(privatePath, Encoding.UTF8);
                    string existingPublicKey = File.ReadAllText(publicPath, Encoding.UTF8);
                    if (IsMatchingKeyPair(existingPrivateKey, existingPublicKey))
                    {
                        privateKeyXml = existingPrivateKey;
                        return true;
                    }
                }

                string privateRoot = Path.GetDirectoryName(privatePath);
                string publicRoot = Path.GetDirectoryName(publicPath);
                Directory.CreateDirectory(privateRoot);
                Directory.CreateDirectory(publicRoot);
                using (var rsa = new RSACryptoServiceProvider(3072))
                {
                    rsa.PersistKeyInCsp = false;
                    privateKeyXml = rsa.ToXmlString(true);
                    ESManagedFileIO.WriteTextAtomic(privatePath, privateKeyXml, new UTF8Encoding(false), privateRoot);
                    ESManagedFileIO.WriteTextAtomic(publicPath, rsa.ToXmlString(false), new UTF8Encoding(false), publicRoot);
                }
                return true;
            }
            catch (Exception exception)
            {
                error = "本机开发签名密钥初始化失败：" + exception.Message;
                return false;
            }
        }

        private static bool IsMatchingKeyPair(string privateKeyXml, string publicKeyXml)
        {
            try
            {
                byte[] probe = Encoding.UTF8.GetBytes("ESInstaller.LocalDevelopmentKeyPair");
                string signature;
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.PersistKeyInCsp = false;
                    rsa.FromXmlString(privateKeyXml);
                    signature = Convert.ToBase64String(rsa.SignData(probe, CryptoConfig.MapNameToOID("SHA256")));
                }
                return ESArtifactTrustVerifier.TryVerifyRsaSha256(publicKeyXml, probe, signature, out _);
            }
            catch
            {
                return false;
            }
        }

        private static string ArchiveCurrentMainArtifacts(string mainRoot, string outputRoot)
        {
            string[] packages = Directory.GetFiles(mainRoot, "*.unitypackage", SearchOption.TopDirectoryOnly);
            string manifest = Path.Combine(mainRoot, ManifestFileName);
            if (packages.Length == 0 && !File.Exists(manifest))
                return string.Empty;

            string archiveRoot = Path.Combine(outputRoot, "Archive", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(archiveRoot);
            ESManagedFileIO.EnsurePath(archiveRoot, false, outputRoot);
            foreach (string package in packages)
            {
                File.Move(package, Path.Combine(archiveRoot, Path.GetFileName(package)));
                MoveCompanionMetaIfPresent(package, archiveRoot);
            }
            if (File.Exists(manifest))
            {
                File.Move(manifest, Path.Combine(archiveRoot, ManifestFileName));
                MoveCompanionMetaIfPresent(manifest, archiveRoot);
            }
            return archiveRoot;
        }

        private static void RestoreArchivedMainArtifacts(string archiveRoot, string mainRoot, string outputRoot)
        {
            if (string.IsNullOrWhiteSpace(archiveRoot) || !Directory.Exists(archiveRoot))
                return;
            ESManagedFileIO.EnsurePath(archiveRoot, false, outputRoot);
            foreach (string file in Directory.GetFiles(archiveRoot, "*", SearchOption.TopDirectoryOnly))
                File.Move(file, Path.Combine(mainRoot, Path.GetFileName(file)));
        }

        private static void MoveCompanionMetaIfPresent(string assetPath, string destinationRoot)
        {
            string metaPath = assetPath + ".meta";
            if (File.Exists(metaPath))
                File.Move(metaPath, Path.Combine(destinationRoot, Path.GetFileName(metaPath)));
        }

        private static void TryDeletePublishedFile(string path, string root)
        {
            try
            {
                if (File.Exists(path))
                    ESManagedFileIO.DeleteFile(path, root);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ESInstaller] 发布失败后的文件清理失败：" + exception.Message);
            }
        }

        private static string GetLocalDevelopmentPrivateKeyPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ESFramework", "InstallerSigning", "es-local-dev.private.xml");
        }

        private static string GetLocalDevelopmentPublicKeyPath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, "Library", "ESInstaller", "TrustRoots", "es-local-dev.public.xml");
        }

        private static void ValidateIdentity(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64 || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
                throw new InvalidDataException(name + " 为空、过长或包含首尾空白。");
            foreach (char character in value)
            {
                bool valid = char.IsLetterOrDigit(character) || character == '.' || character == '_' || character == '-';
                if (!valid)
                    throw new InvalidDataException(name + " 只能包含字母、数字、点、下划线和连字符。");
            }
        }

        private static void AppendField(StringBuilder builder, string name, string value)
        {
            builder.Append(name).Append('=').Append(value).Append('\n');
        }
    }
}
