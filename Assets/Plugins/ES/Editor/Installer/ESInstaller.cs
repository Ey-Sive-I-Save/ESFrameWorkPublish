using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using System.Threading;
using Sirenix.OdinInspector;

namespace ES.ESInstaller
{
    /// <summary>
    /// ES框架安装器 - 商业级Unity插件安装管理工具
    /// </summary>
    public class ESInstaller : EditorWindow
    {
        #region 静态初始化

        [InitializeOnLoadMethod]
        private static void InitializeOnEditorLoad()
        {
            // 延迟执行，避免在编辑器启动时立即检查
            EditorApplication.delayCall += CheckDependenciesOnStartup;
        }

        private static async void CheckDependenciesOnStartup()
        {
            // 只在有ESInstaller脚本的情况下检查
            if (HasESInstallerScript())
            {
                await CheckAndShowInstallerIfNeededAsync();
            }


        }

        private static bool HasESInstallerScript()
        {
            // 检查ESInstaller脚本是否存在
            var script = Resources.FindObjectsOfTypeAll<MonoScript>()
                .FirstOrDefault(s => s.GetClass() == typeof(ESInstaller));
            return script != null;
        }

        public static ESInstaller installer;

        private static async Task CheckAndShowInstallerIfNeededAsync()
        {
            try
            {


                // 创建临时实例来检查配置
                if (installer == null)
                {
                    installer = EditorWindow.CreateInstance<ESInstaller>();

                }
                installer.InitializePaths();
             //   Debug.Log("ES Installer 启动检查中..." + installer.configFilePath);
                // 加载配置
                if (File.Exists(installer.configFilePath))
                {
//                    Debug.Log("加载配置文件: " + installer.configFilePath);
                    string json = File.ReadAllText(installer.configFilePath);
                    installer.currentProfile = JsonUtility.FromJson<InstallationProfile>(json);
                }
                else
                {
                    installer.InitializeDefaultProfile();
                }

                // 检查是否启用自动检查
                if (!installer.currentProfile.enableAutoCheck)
                {
                    DestroyImmediate(installer);
                    return;
                }

                // 检查是否跳过此次检查
                if (installer.currentProfile.skipNextAutoCheck)
                {
                    installer.currentProfile.skipNextAutoCheck = false;
                    installer.SaveConfiguration();
                    DestroyImmediate(installer);
                    return;
                }
                // 检查是否有未安装的必需依赖
                bool hasUninstalledRequiredDependencies = await CheckForUninstalledRequiredDependenciesAsync(installer.currentProfile.mainPackage);

                // 如果有未安装的必需依赖，显示安装器
                if (hasUninstalledRequiredDependencies)
                {
                    ShowInstallerWithWarning();
                    ShowInstaller(); // 直接打开安装器窗口
                }

                // 清理临时实例
                // DestroyImmediate(tempInstance);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ES Installer 启动检查失败: {e.Message}");
            }
        }

        private static async Task<bool> CheckUnityPackageInstalledAsync(UnityPackageDependency dependency)
        {

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

            try
            {
                var request = Client.List(false, false);
                await WaitForListRequestCompletion(request);

                if (request.Status == StatusCode.Success)
                {
                    return request.Result.Any(p => p.name == dependency.packageId);
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> CheckGitPackageInstalledAsync(GitPackageDependency dependency)
        {
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

            try
            {
                var request = Client.List(false, false);
                await WaitForListRequestCompletion(request);

                if (request.Status == StatusCode.Success)
                {
                    return request.Result.Any(p => p.packageId == dependency.gitUrl || p.name == dependency.gitUrl);
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static Task<bool> CheckUserPackageInstalledAsync(UserPackageDependency dependency)
        {
            // 用户包只通过类检查（同步操作）
            if (string.IsNullOrEmpty(dependency.checkClass))
                return Task.FromResult(false);

            return Task.FromResult(IsClassExists(dependency.checkClass));
        }

        /// <summary>
        /// 检查包是否有未安装的必需依赖
        /// </summary>
        private static async Task<bool> CheckForUninstalledRequiredDependenciesAsync(ESPackageBase package)
        {
            if (package == null) return false;

            // 收集所有必需依赖的检查任务
            var checkTasks = new List<Task<bool>>();

            // 添加Unity包检查任务
            foreach (var dep in package.unityDependencies.Where(d => d.isRequired))
            {
                checkTasks.Add(CheckUnityPackageInstalledAsync(dep));
            }

            // 添加Git包检查任务
            foreach (var dep in package.gitDependencies.Where(d => d.isRequired))
            {
                checkTasks.Add(CheckGitPackageInstalledAsync(dep));
            }

            // 添加用户包检查任务
            foreach (var dep in package.userDependencies.Where(d => d.isRequired))
            {
                checkTasks.Add(CheckUserPackageInstalledAsync(dep));
            }

            // 添加资产文件检查任务
            foreach (var dep in package.assetFileDependencies.Where(d => d.isRequired))
            {
                checkTasks.Add(Task.Run(() => CheckAssetFileInstalled(dep)));
            }

            // 并行执行所有检查任务
            var results = await Task.WhenAll(checkTasks);

            // 如果任何一个必需依赖未安装，返回true
            return results.Any(installed => !installed);
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
            try
            {
                // 创建临时实例来检查配置
                var tempInstance = EditorWindow.CreateInstance<ESInstaller>();
                tempInstance.InitializePaths();

                // 加载配置
                if (File.Exists(tempInstance.configFilePath))
                {
                    string json = File.ReadAllText(tempInstance.configFilePath);
                    tempInstance.currentProfile = JsonUtility.FromJson<InstallationProfile>(json);
                }
                else
                {
                    tempInstance.InitializeDefaultProfile();
                }

                // 检查是否有未安装的必需依赖
                bool hasUninstalledRequiredDependencies = false;
                int totalRequired = 0;
                int installedRequired = 0;

                // 检查Unity官方包
                foreach (var dependency in tempInstance.currentProfile.mainPackage.unityDependencies.Where(d => d.isRequired))
                {
                    totalRequired++;
                    if (await CheckUnityPackageInstalledAsync(dependency))
                    {
                        installedRequired++;
                    }
                    else
                    {
                        hasUninstalledRequiredDependencies = true;
                    }
                }

                // 检查Git包
                foreach (var dependency in tempInstance.currentProfile.mainPackage.gitDependencies.Where(d => d.isRequired))
                {
                    totalRequired++;
                    if (await CheckGitPackageInstalledAsync(dependency))
                    {
                        installedRequired++;
                    }
                    else
                    {
                        hasUninstalledRequiredDependencies = true;
                    }
                }

                // 检查用户包
                foreach (var dependency in tempInstance.currentProfile.mainPackage.userDependencies.Where(d => d.isRequired))
                {
                    totalRequired++;
                    if (await CheckUserPackageInstalledAsync(dependency))
                    {
                        installedRequired++;
                    }
                    else
                    {
                        hasUninstalledRequiredDependencies = true;
                    }
                }

                // 显示检查结果
                if (hasUninstalledRequiredDependencies)
                {
                    bool openInstaller = EditorUtility.DisplayDialog(
                        "ES框架依赖检查结果",
                        $"发现未安装的必需依赖！\n\n已安装: {installedRequired}/{totalRequired}\n\n是否打开安装管理器来解决依赖问题？",
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
                        $"所有必需依赖都已正确安装！\n\n已安装: {installedRequired}/{totalRequired}",
                        "确定"
                    );
                }

                // 清理临时实例
                DestroyImmediate(tempInstance);
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
            // 检查安装器窗口是否已经打开
            if (HasOpenInstances<ESInstaller>())
            {
                // 窗口已经打开，不重复显示警告对话框
                return;
            }

            // 显示警告对话框
            bool showInstaller = EditorUtility.DisplayDialog(
                "ES框架依赖检查",
                "检测到ES框架有未安装的必需依赖项。\n\n是否现在打开安装管理器来解决依赖问题？",
                "打开安装器",
                "稍后提醒"
            );

            if (showInstaller)
            {
                ShowInstaller();
            }
            else
            {
                // 设置一个延迟提醒
                EditorApplication.delayCall += () =>
                {
                    if (EditorUtility.DisplayDialog(
                        "ES框架提醒",
                        "ES框架依赖项尚未完全安装，建议运行安装管理器。",
                        "现在安装",
                        "忽略"
                    ))
                    {
                        ShowInstaller();
                    }
                };
            }
        }

        private static new bool HasOpenInstances<T>() where T : EditorWindow
        {
            return Resources.FindObjectsOfTypeAll<T>().Length > 0;
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
                        string json = File.ReadAllText(packageJsonPath);
                        T package = JsonUtility.FromJson<T>(json);
                        if (package != null)
                        {
                            package.packageFolderPath = folderPath;
                            package.installState = PackageInstallState.Loading;
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
            public bool enableAutoCheck = true; // 是否启用编辑器启动时自动检查
            public bool skipNextAutoCheck = false; // 跳过下次自动检查

            /// <summary>
            /// 从Downloads文件夹扫描并加载所有包配置
            /// </summary>
            public static InstallationProfile LoadFromFile()
            {
                try
                {
                    // 查找ESInstaller脚本的路径
                    string[] guids = AssetDatabase.FindAssets("ESInstaller t:MonoScript");
                    if (guids.Length == 0)
                    {
                        Debug.LogWarning("无法找到ESInstaller脚本");
                        return CreateDefaultProfile();
                    }

                    string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    string scriptFolder = Path.GetDirectoryName(scriptPath);
                    string downloadsFolder = Path.Combine(scriptFolder, "Downloads");

                    if (!Directory.Exists(downloadsFolder))
                    {
                        Debug.LogWarning($"Downloads文件夹不存在: {downloadsFolder}");
                        return CreateDefaultProfile();
                    }

                    var profile = new InstallationProfile();

                    // 扫描Downloads文件夹下的所有子文件夹
                    string[] subFolders = Directory.GetDirectories(downloadsFolder);

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
                            profile.mainPackage = mainPackage;
                            profile.profileName = $"{mainPackage.displayName} {mainPackage.version}";
                        }
                        else
                        {
                            // 加载扩展包
                            var extensionPackage = ESPackageBase.LoadFromJson<ESExtensionPackage>(folderPath);
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
                    enableAutoCheck = true,
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
        private string statusMessage = "";
        private MessageType statusType = MessageType.Info;

        private string downloadsFolderPath;
        private const string DOWNLOADS_FOLDER_NAME = "Downloads";

        // UI样式
        private GUIStyle headerStyle;
        private GUIStyle sectionStyle;
        private GUIStyle statusStyle;
        private GUIStyle packageNameStyle;

        // UI状态
        private bool hasInitialized = false;
        private bool isMainPackageInstalled = false; // 主包是否已安装

        // 包选择相关
        private List<string> availablePackageIds = new List<string>();
        private Dictionary<string, string> packageDisplayNames = new Dictionary<string, string>();
        private string currentSelectedPackageId = "es_main";

        // 配置相关
        private bool isConfigModified = false;
        private string configFilePath;

        #endregion

        #region 菜单项

        [MenuItem(MenuItemPathDefine.INSTALL_DEPENDENCY_PATH + "安装管理器", false, 0)]
        static void ShowInstaller()
        {
            if (installer == null)
            {
                installer = GetWindow<ESInstaller>("ES 安装管理器");

            }
            installer.minSize = new Vector2(600, 500);
            installer.Show();

            installer.InitializePaths();
            installer.LoadConfiguration();
            // 加载配置
            if (File.Exists(installer.configFilePath))
            {
                string json = File.ReadAllText(installer.configFilePath);
                installer.currentProfile = JsonUtility.FromJson<InstallationProfile>(json);
            }
            else
            {
                installer.InitializeDefaultProfile();
            }

            //在这个地方开始执行异步方法,检查全部资源
            installer.ScanAndLoadAllPackages();
            _ = installer.CheckAllPackagesInstallStateAsync();
        }

        [MenuItem(MenuItemPathDefine.INSTALL_DEPENDENCY_PATH + "检查依赖", false, 2)]
        static void QuickCheckDependencies()
        {
            // 异步检查依赖并显示结果
            _ = QuickCheckAndShowResultAsync();
        }

        #endregion

        #region Unity生命周期

        private void OnEnable()
        {
            // 确保静态引用正确设置
            if (installer == null)
            {
                installer = this;
            }

            // 确保在重新编译后窗口能正常工作

            InitializePaths();
            LoadConfiguration();
            ScanAndLoadAllPackages(); // 扫描并加载所有包
            _ = CheckAllPackagesInstallStateAsync(); // 异步检查所有包的安装状态

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
            if (!Directory.Exists(downloadsFolderPath))
            {
                Directory.CreateDirectory(downloadsFolderPath);
            }
        }

        private void LoadConfiguration()
        {
            currentProfile = InstallationProfile.LoadFromFile();

            // 检查主包安装状态
            CheckMainPackageInstallation();
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
              string packagePath = string.IsNullOrEmpty(package.packageFolderPath)
                  ? Path.Combine(downloadsFolderPath, package.folderName)
                  : package.packageFolderPath;

              if (!Directory.Exists(packagePath))
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
                  string fullPath;
                  if (package.assetPath.Contains("StreamingAssets"))
                  {
                      // 对于 StreamingAssets，使用专用路径，避免替换
                      string relativePath = package.assetPath.Replace("Assets/StreamingAssets/", "").Replace("Assets/StreamingAssets", "");
                      fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
                  }
                  else
                  {
                      // 对于其他路径，保持原逻辑
                      fullPath = Path.Combine(Application.dataPath, package.assetPath.Replace("Assets/", ""));
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

            // 检查Unity包
            if (package.unityDependencies != null)
            {
                foreach (var dep in package.unityDependencies)
                {
                    dep.isInstalled = await CheckUnityPackageInstalledAsync(dep);
                }
            }

            // 检查Git包
            if (package.gitDependencies != null)
            {
                foreach (var dep in package.gitDependencies)
                {
                    dep.isInstalled = await CheckGitPackageInstalledAsync(dep);
                }
            }

            // 检查用户包
            if (package.userDependencies != null)
            {
                foreach (var dep in package.userDependencies)
                {
                    dep.isInstalled = CheckUserPackageInstalled(dep);
                }
            }

            // 检查资产文件
            if (package.assetFileDependencies != null)
            {
                foreach (var dep in package.assetFileDependencies)
                {
                    dep.isInstalled = CheckAssetFileInstalled(dep);
                }
            }
        }

        /// <summary>
        /// 同步检查用户包是否已安装
        /// </summary>
        private bool CheckUserPackageInstalled(UserPackageDependency dependency)
        {
            // 用户包只通过类检查（同步操作）
            if (string.IsNullOrEmpty(dependency.checkClass))
                return false;

            return IsClassExists(dependency.checkClass);
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
            string fullPath = Path.Combine(Application.dataPath, dependency.assetPath.Replace("Assets/", ""));
            return File.Exists(fullPath) || Directory.Exists(fullPath);
        }

        /// <summary>
        /// 扫描并加载所有包（主包+扩展包）
        /// </summary>
        private void ScanAndLoadAllPackages()
        {
            if (!Directory.Exists(downloadsFolderPath))
            {
                Directory.CreateDirectory(downloadsFolderPath);
                return;
            }

            // 清空包列表
            availablePackageIds.Clear();
            packageDisplayNames.Clear();

            // 添加主包
            availablePackageIds.Add("es_main");
            packageDisplayNames["es_main"] = "ES Framework 主包 (必需)";

            // 首先处理主包
            string mainFolderPath = Path.Combine(downloadsFolderPath, "Main");
            string mainJsonPath = Path.Combine(mainFolderPath, "package.json");
            //    Debug.Log("Scanning main package folder: " + mainFolderPath);

            if (File.Exists(mainJsonPath))
            {
                // Debug.Log("Loading main package configuration: " + mainJsonPath);
                try
                {
                    string jsonContent = File.ReadAllText(mainJsonPath);
                    var packageData = JsonUtility.FromJson<ExtensionPackageJsonData>(jsonContent);

                    // 更新主包配置
                    currentProfile.mainPackage.displayName = packageData.displayName;
                    currentProfile.mainPackage.version = packageData.version;
                    currentProfile.mainPackage.description = packageData.description;
                    currentProfile.mainPackage.checkClass = packageData.checkClass;
                    currentProfile.mainPackage.assetPath = packageData.assetPath;
                    currentProfile.mainPackage.installNotes = packageData.installationNotes;

                    // 更新依赖项
                    currentProfile.mainPackage.unityDependencies?.Clear();
                    currentProfile.mainPackage.gitDependencies?.Clear();
                    currentProfile.mainPackage.userDependencies?.Clear();


                    if (packageData.unityDependencies != null)
                    {

                        foreach (var dep in packageData.unityDependencies)
                        {
                            currentProfile.mainPackage.unityDependencies.Add(new UnityPackageDependency
                            {
                                name = dep.name,
                                version = dep.version,
                                description = dep.description,
                                packageId = dep.name,
                                isRequired = dep.isRequired,
                                checkClass = dep.checkClass
                            });
                        }
                    }
                    // Debug.Log("1Updating main package dependencies...");

                    if (packageData.gitDependencies != null)
                    {
                        foreach (var dep in packageData.gitDependencies)
                        {
                            currentProfile.mainPackage.gitDependencies.Add(new GitPackageDependency
                            {
                                name = dep.name,
                                version = dep.version,
                                gitUrl = dep.gitUrl,
                                checkClass = dep.checkClass,
                                isRequired = dep.isRequired
                            });
                        }
                    }
                    // Debug.Log("2Updating main package dependencies...");
                    if (packageData.userDependencies != null)
                    {
                        foreach (var dep in packageData.userDependencies)
                        {
                            currentProfile.mainPackage.userDependencies.Add(new UserPackageDependency
                            {
                                name = dep.name,
                                version = dep.version,
                                checkClass = dep.checkClass,
                                installInstructions = dep.installInstructions,
                                isRequired = dep.isRequired
                            });
                        }
                    }
                    // Debug.Log("3Updating main package dependencies...");
                    if (packageData.assetFileDependencies != null)
                    {
                        foreach (var dep in packageData.assetFileDependencies)
                        {
                            currentProfile.mainPackage.assetFileDependencies.Add(new AssetFileDependency
                            {
                                name = dep.name,
                                version = dep.version,
                                assetPath = dep.assetPath,
                                checkClass = dep.checkClass,
                                isRequired = dep.isRequired
                            });
                        }
                    }
                    // Debug.Log("4Updating main package dependencies...");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"加载主包配置失败: {mainJsonPath}, 错误: {e.Message}");
                }
            }
            else
            {
            }

            // 扫描Downloads文件夹下的所有子文件夹
            string[] subDirectories = Directory.GetDirectories(downloadsFolderPath);

            foreach (string subDir in subDirectories)
            {
                string folderName = Path.GetFileName(subDir);
                string jsonPath = Path.Combine(subDir, "package.json");

                // 跳过Main文件夹（主包已处理）
                if (folderName.Equals("Main", StringComparison.OrdinalIgnoreCase))
                    continue;
                //Debug.Log("Scanning extension package folder: " + folderName);
                // 尝试加载扩展包配置
                if (File.Exists(jsonPath))
                {
                    try
                    {
                        string jsonContent = File.ReadAllText(jsonPath);
                        var packageData = JsonUtility.FromJson<ExtensionPackageJsonData>(jsonContent);

                        string packageId = $"ext_{folderName}";

                        // 添加到可用包列表
                        availablePackageIds.Add(packageId);
                        packageDisplayNames[packageId] = $"{packageData.displayName} v{packageData.version}";

                        // 检查是否已经存在相同的扩展包
                        var existingPackage = currentProfile.extensionPackages.FirstOrDefault(p => p.packageId == packageId);

                        if (existingPackage == null)
                        {
                            // 创建新的扩展包
                            var newPackage = CreateExtensionPackageFromJson(packageData, folderName, packageId);
                            currentProfile.extensionPackages.Add(newPackage);
                        }
                        else
                        {
                            // 更新现有包的信息（保留安装状态）
                            UpdateExtensionPackageFromJson(existingPackage, packageData);
                            // 检查安装状态
                            CheckExtensionPackageInstallation(existingPackage);
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"加载扩展包配置失败: {jsonPath}, 错误: {e.Message}");
                    }
                }
                else
                {
                    // 没有JSON配置文件，创建基本配置
                    string packageId = $"ext_{folderName}";
                    availablePackageIds.Add(packageId);
                    packageDisplayNames[packageId] = $"{folderName} (无配置)";
                }
            }

        }

        /// <summary>
        /// 从JSON数据创建扩展包
        /// </summary>
        private ESExtensionPackage CreateExtensionPackageFromJson(ExtensionPackageJsonData data, string folderName, string packageId)
        {
            var package = new ESExtensionPackage
            {
                packageId = packageId,
                displayName = data.displayName,
                version = data.version,
                description = data.description,
                folderName = folderName,
                installNotes = data.installationNotes,
                checkClass = data.checkClass,
                assetPath = data.assetPath,
                tags = data.tags != null ? new List<string>(data.tags) : new List<string>(),
                author = data.author,
                website = data.website,
                license = data.license,
                requiredMainPackages = data.requiredMainPackages != null ? new List<string>(data.requiredMainPackages) : new List<string>(),
                unityDependencies = new List<UnityPackageDependency>(),
                gitDependencies = new List<GitPackageDependency>(),
                userDependencies = new List<UserPackageDependency>(),
                assetFileDependencies = new List<AssetFileDependency>()
            };

            // 添加Unity包依赖项
            if (data.unityDependencies != null)
            {
                foreach (var dep in data.unityDependencies)
                {
                    package.unityDependencies.Add(new UnityPackageDependency
                    {
                        name = dep.name,
                        version = dep.version,
                        description = dep.description,
                        packageId = dep.name,
                        isRequired = dep.isRequired,
                        checkClass = dep.checkClass
                    });
                }
            }

            // 添加Git包依赖项
            if (data.gitDependencies != null)
            {
                foreach (var dep in data.gitDependencies)
                {
                    package.gitDependencies.Add(new GitPackageDependency
                    {
                        name = dep.name,
                        version = dep.version,
                        gitUrl = dep.gitUrl,
                        checkClass = dep.checkClass,
                        isRequired = dep.isRequired
                    });
                }
            }

            // 添加用户包依赖项
            if (data.userDependencies != null)
            {
                foreach (var dep in data.userDependencies)
                {
                    package.userDependencies.Add(new UserPackageDependency
                    {
                        name = dep.name,
                        version = dep.version,
                        checkClass = dep.checkClass,
                        installInstructions = dep.installInstructions,
                        isRequired = dep.isRequired
                    });
                }
            }

            return package;
        }

        /// <summary>
        /// 从JSON数据更新现有扩展包
        /// </summary>
        private void UpdateExtensionPackageFromJson(ESExtensionPackage package, ExtensionPackageJsonData data)
        {
            // 更新基本信息（保留安装状态）
            package.displayName = data.displayName;
            package.version = data.version;
            package.description = data.description;
            package.installNotes = data.installationNotes;
            package.checkClass = data.checkClass;
            package.assetPath = data.assetPath;
            package.tags = data.tags != null ? new List<string>(data.tags) : new List<string>();
            package.author = data.author;
            package.website = data.website;
            package.license = data.license;
            package.requiredMainPackages = data.requiredMainPackages != null ? new List<string>(data.requiredMainPackages) : new List<string>();

            // 更新依赖项（清空并重新添加）
            package.unityDependencies.Clear();
            package.gitDependencies.Clear();
            package.userDependencies.Clear();
            package.assetFileDependencies.Clear();

            if (data.unityDependencies != null)
            {
                foreach (var dep in data.unityDependencies)
                {
                    package.unityDependencies.Add(new UnityPackageDependency
                    {
                        name = dep.name,
                        version = dep.version,
                        description = dep.description,
                        packageId = dep.name,
                        isRequired = dep.isRequired,
                        checkClass = dep.checkClass
                    });
                }
            }

            if (data.gitDependencies != null)
            {
                foreach (var dep in data.gitDependencies)
                {
                    package.gitDependencies.Add(new GitPackageDependency
                    {
                        name = dep.name,
                        version = dep.version,
                        gitUrl = dep.gitUrl,
                        checkClass = dep.checkClass,
                        isRequired = dep.isRequired
                    });
                }
            }

            if (data.userDependencies != null)
            {
                foreach (var dep in data.userDependencies)
                {
                    package.userDependencies.Add(new UserPackageDependency
                    {
                        name = dep.name,
                        version = dep.version,
                        checkClass = dep.checkClass,
                        installInstructions = dep.installInstructions,
                        isRequired = dep.isRequired
                    });
                }
            }

            if (data.assetFileDependencies != null)
            {
                foreach (var dep in data.assetFileDependencies)
                {
                    package.assetFileDependencies.Add(new AssetFileDependency
                    {
                        name = dep.name,
                        version = dep.version,
                        assetPath = dep.assetPath,
                        checkClass = dep.checkClass,
                        isRequired = dep.isRequired
                    });
                }
            }
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
            string mainPackagePath = Path.Combine(downloadsFolderPath, currentProfile.mainPackage.folderName);

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
        }

        [System.Serializable]
        private class GitDependencyJsonData
        {
            public string name;
            public string version;
            public string gitUrl;
            public string checkClass;
            public bool isRequired;
        }

        [System.Serializable]
        private class UserDependencyJsonData
        {
            public string name;
            public string version;
            public string checkClass;
            public string installInstructions;
            public bool isRequired;
        }

        [System.Serializable]
        private class AssetFileDependencyJsonData
        {
            public string name;
            public string version;
            public string assetPath;
            public string checkClass;
            public bool isRequired;
        }

        private void OnDisable()
        {
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

        private void OnGUI()
        {
            InitializeStyles();

            // 确保配置已加载
            if (currentProfile == null)
            {
                InitializePaths();
                LoadConfiguration();
            }

            // 首次显示时自动刷新所有状态
            if (!hasInitialized)
            {
                RefreshAllStatuses();
                hasInitialized = true;
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
                        //EditorUtility.DisplayDialog("Debug", "currentProfile is null", "OK");
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

            string[] displayNames = availablePackageIds.Select(id => packageDisplayNames.ContainsKey(id) ? packageDisplayNames[id] : id).ToArray();

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("选择包:", currentIndex, displayNames);
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
            EditorGUILayout.LabelField($"启用编辑器启动时自动检查: {(currentProfile.enableAutoCheck ? "是" : "否")}");

            if (currentProfile.enableAutoCheck)
            {
                EditorGUILayout.LabelField($"跳过下次自动检查: {(currentProfile.skipNextAutoCheck ? "是" : "否")}");
                EditorGUILayout.HelpBox("启用后，每次打开Unity编辑器时会自动检查依赖状态，如果发现未安装的必需依赖会弹出安装器。", MessageType.Info);
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
                    string mainPackagePath = Path.Combine(downloadsFolderPath, currentProfile.mainPackage.folderName);
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
                            string packagePath = Path.Combine("Assets/Plugins/ES/Editor/Installer/Downloads", extPackage.folderName);
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
                    string mainPackagePath = Path.Combine(downloadsFolderPath, currentProfile.mainPackage.folderName);
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

                            string extFolderPath = Path.Combine("Assets/Plugins/ES/Editor/Installer/Downloads", extPackage.folderName);
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

            string packagePath = string.IsNullOrEmpty(package.packageFolderPath)
                ? Path.Combine(downloadsFolderPath, package.folderName)
                : package.packageFolderPath;

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

            // 快速刷新按钮 - 更突出显示
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 0.6f, 1.0f); // 蓝色背景
            if (GUILayout.Button("🚀 快速全部刷新状态", GUILayout.Height(35)))
            {
                RefreshAllStatuses();
                ShowStatus("所有依赖状态已刷新", MessageType.Info);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 其他按钮
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("📋 生成安装报告"))
            {
                GenerateInstallationReport();
            }

            if (GUILayout.Button("🔄 刷新状态"))
            {
                RefreshAllStatuses();
            }

            if (GUILayout.Button("❓ 帮助"))
            {
                ShowHelp();
            }

            EditorGUILayout.EndHorizontal();
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

            string packagePath = string.IsNullOrEmpty(package.packageFolderPath)
                ? Path.Combine(downloadsFolderPath, package.folderName)
                : package.packageFolderPath;

            if (!Directory.Exists(packagePath))
            {
                ShowStatus("包文件夹不存在", MessageType.Error);
                return;
            }

            // 扫描文件夹中的所有Unity Package文件
            string[] scannedFiles = Directory.GetFiles(packagePath, "*.unitypackage");

            if (scannedFiles.Length == 0)
            {
                ShowStatus("没有找到要安装的包文件", MessageType.Error);
                return;
            }

            // 检查必需的依赖项是否都已满足（强制安装可以跳过此检查）
            if (!forceInstall && !CheckPackageDependencies(package))
            {
                ShowStatus($"无法安装 {package.displayName}：必需的依赖项未满足", MessageType.Error);
                return;
            }

            string installMode = forceInstall ? "强制安装" : "安装";
            ShowStatus($"开始{installMode} {package.displayName}，共 {scannedFiles.Length} 个文件...", MessageType.Info);

            foreach (string packageFile in scannedFiles)
            {
                // 使用interactive模式显示Unity标准导入面板
                AssetDatabase.ImportPackage(packageFile, true);
            }

            ShowStatus($"{package.displayName} {installMode}已启动，请在弹出的导入面板中选择要导入的资源", MessageType.Info);

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
                    if (dep.isRequired && !dep.isInstalled)
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
                string json = JsonUtility.ToJson(currentProfile, true);
                File.WriteAllText(configFilePath, json);
                currentProfile.lastModified = DateTime.Now;
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
                if (File.Exists(configFilePath))
                {
                    string json = File.ReadAllText(configFilePath);
                    currentProfile = JsonUtility.FromJson<InstallationProfile>(json);
                }
                else
                {
                    InitializeDefaultProfile();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"加载配置失败: {e.Message}");
                ShowStatus($"加载配置失败: {e.Message}", MessageType.Error);
                InitializeDefaultProfile();
            }
        }

        private async Task CheckAllUnityPackages()
        {
            if (currentProfile == null || currentProfile.mainPackage.unityDependencies == null)
            {
                ShowStatus("配置未加载，无法检查Unity包", MessageType.Warning);
                return;
            }

            ShowStatus("正在检查所有Unity官方包...", MessageType.Info);

            foreach (var dependency in currentProfile.mainPackage.unityDependencies)
            {
                await CheckUnityPackageDependency(dependency);
            }

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
            if (string.IsNullOrEmpty(dependency.packageId))
            {
                dependency.isInstalled = false;
                ShowStatus($"Unity包 {dependency.name} 缺少Package ID", MessageType.Warning);
                return;
            }

            try
            {
                // 首先尝试通过类检查（如果提供了检查类名）
                if (!string.IsNullOrEmpty(dependency.checkClass))
                {
                    if (IsClassExists(dependency.checkClass))
                    {
                        dependency.isInstalled = true;
                        ShowStatus($"Unity包 {dependency.name} 已安装 (通过类验证)", MessageType.Info);
                        Repaint();
                        return;
                    }
                }

                // 如果没有检查类或类检查失败，使用UPM检查
                // 在主线程同步发起请求（这不会阻塞）
                var request = Client.List(false, false);
                await WaitForListRequestCompletion(request);

                if (request.Status == StatusCode.Success)
                {
                    dependency.isInstalled = request.Result.Any(p => p.name == dependency.packageId);
                    if (dependency.isInstalled)
                    {
                        ShowStatus($"Unity包 {dependency.name} 已安装 (通过UPM验证)", MessageType.Info);
                    }
                    else
                    {
                        ShowStatus($"Unity包 {dependency.name} 未安装", MessageType.Warning);
                    }
                }
                else
                {
                    dependency.isInstalled = false;
                    ShowStatus($"检查Unity包 {dependency.name} 失败: {request.Error?.message}", MessageType.Error);
                }
            }
            catch (Exception e)
            {
                dependency.isInstalled = false;
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
            if (string.IsNullOrEmpty(dependency.gitUrl))
            {
                dependency.isInstalled = false;
                ShowStatus($"Git包 {dependency.name} 缺少Git URL", MessageType.Warning);
                return;
            }

            try
            {
                // 首先尝试通过类检查（如果提供了检查类名）
                if (!string.IsNullOrEmpty(dependency.checkClass))
                {
                    if (IsClassExists(dependency.checkClass))
                    {
                        dependency.isInstalled = true;
                        ShowStatus($"Git包 {dependency.name} 已安装 (通过类验证)", MessageType.Info);
                        Repaint();
                        return;
                    }
                }

                // 如果没有检查类或类检查失败，使用UPM检查
                // 在主线程同步发起请求（这不会阻塞）
                var request = Client.List(false, false);
                await WaitForListRequestCompletion(request);

                if (request.Status == StatusCode.Success)
                {
                    dependency.isInstalled = request.Result.Any(p => p.packageId == dependency.gitUrl || p.name == dependency.gitUrl);
                    if (dependency.isInstalled)
                    {
                        ShowStatus($"Git包 {dependency.name} 已安装 (通过UPM验证)", MessageType.Info);
                    }
                    else
                    {
                        ShowStatus($"Git包 {dependency.name} 未安装", MessageType.Warning);
                    }
                }
                else
                {
                    dependency.isInstalled = false;
                    ShowStatus($"检查Git包 {dependency.name} 失败: {request.Error.message}", MessageType.Error);
                }
            }
            catch (Exception e)
            {
                dependency.isInstalled = false;
                ShowStatus($"检查Git包 {dependency.name} 异常: {e.Message}", MessageType.Error);
            }

            Repaint();
        }

        private async Task InstallGitPackageDependency(GitPackageDependency dependency)
        {
            if (string.IsNullOrEmpty(dependency.gitUrl))
            {
                ShowStatus($"Git包 {dependency.name} 缺少Git URL", MessageType.Error);
                return;
            }

            AddRequest request;
            try
            {
                // 在主线程同步发起请求（这不会阻塞）
                request = Client.Add(dependency.gitUrl);
                ShowStatus($"正在安装Git包 {dependency.name}...", MessageType.Info);
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

            foreach (var dependency in currentProfile.mainPackage.gitDependencies)
            {
                await CheckGitPackageDependency(dependency);
            }

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
            bool gitPackagesValid = currentProfile.mainPackage.gitDependencies.All(d => !d.isRequired || d.isInstalled);
            bool userPackagesValid = currentProfile.mainPackage.userDependencies.All(d => !d.isRequired || d.isInstalled);

            // 检查ES包系统：主包必需，选中的扩展包也必需
            bool esPackagesValid = true;

            // 检查主包
            string mainPackagePath = Path.Combine(downloadsFolderPath, currentProfile.mainPackage.folderName);
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
                    string extFolderPath = Path.Combine("Assets/Plugins/ES/Editor/Installer/Downloads", extPackage.folderName);
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
            // 检查主包
            string mainPackagePath = Path.Combine(downloadsFolderPath, currentProfile.mainPackage.folderName);
            if (!Directory.Exists(mainPackagePath))
            {
                ShowStatus($"主包文件夹不存在: {mainPackagePath}", MessageType.Error);
                return;
            }

            // 扫描主包文件
            string[] mainPackageFiles = Directory.GetFiles(mainPackagePath, "*.unitypackage");

            if (mainPackageFiles.Length == 0)
            {
                ShowStatus("在主包文件夹中未找到任何 .unitypackage 文件", MessageType.Error);
                return;
            }

            // 收集所有需要安装的包
            List<string> allPackageFiles = new List<string>(mainPackageFiles);

            // 检查选中的扩展包
            List<ESExtensionPackage> selectedExtensions = currentProfile.extensionPackages
                .Where(ext => ext.isSelectedForInstall)
                .ToList();

            foreach (var extPackage in selectedExtensions)
            {
                string extFolderPath = Path.Combine(downloadsFolderPath, extPackage.folderName);
                if (Directory.Exists(extFolderPath))
                {
                    string[] extPackageFiles = Directory.GetFiles(extFolderPath, "*.unitypackage");
                    if (extPackageFiles.Length > 0)
                    {
                        allPackageFiles.AddRange(extPackageFiles);
                        ShowStatus($"扩展包 {extPackage.displayName} 已添加到安装列表", MessageType.Info);
                    }
                    else
                    {
                        ShowStatus($"警告: 扩展包 {extPackage.displayName} 文件夹中未找到Unity Package文件", MessageType.Warning);
                    }
                }
                else
                {
                    ShowStatus($"警告: 扩展包 {extPackage.displayName} 文件夹不存在", MessageType.Warning);
                }
            }

            if (allPackageFiles.Count == 0)
            {
                ShowStatus("没有找到任何可安装的Unity Package文件", MessageType.Error);
                return;
            }

            // 开始导入所有找到的Unity Package文件
            ShowStatus($"开始导入 {allPackageFiles.Count} 个Unity Package文件 (主包: {mainPackageFiles.Length}, 扩展包: {allPackageFiles.Count - mainPackageFiles.Length})...", MessageType.Info);

            foreach (string packagePath in allPackageFiles)
            {
                string fileName = Path.GetFileName(packagePath);
                ShowStatus($"正在导入: {fileName}", MessageType.Info);

                // 导入Unity Package，显示导入面板 (interactive = true)
                AssetDatabase.ImportPackage(packagePath, true);
            }

            ShowStatus($"ES框架安装已开始，共导入 {allPackageFiles.Count} 个Unity Package文件，请在弹出的面板中选择要导入的资源", MessageType.Info);
        }

        private async void RefreshAllStatuses()
        {
            if (currentProfile == null)
            {
                return;
            }

            await CheckAllUnityPackages();
            await CheckAllGitPackages();
            await CheckAllUserPackages();
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
            string mainPackagePath = Path.Combine(downloadsFolderPath, currentProfile.mainPackage.folderName);
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
                    string folderPath = Path.Combine(downloadsFolderPath, extPackage.folderName);
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
            File.WriteAllText(reportPath, report);
            AssetDatabase.Refresh();

            ShowStatus($"安装报告已生成: {reportPath}", MessageType.Info);
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
}
