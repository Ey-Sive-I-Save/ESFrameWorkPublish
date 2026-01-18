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

        private static async Task CheckAndShowInstallerIfNeededAsync()
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

                // 检查是否启用自动检查
                if (!tempInstance.currentProfile.enableAutoCheck)
                {
                    DestroyImmediate(tempInstance);
                    return;
                }

                // 检查是否跳过此次检查
                if (tempInstance.currentProfile.skipNextAutoCheck)
                {
                    tempInstance.currentProfile.skipNextAutoCheck = false;
                    tempInstance.SaveConfiguration();
                    DestroyImmediate(tempInstance);
                    return;
                }

                // 检查是否有未安装的必需依赖
                bool hasUninstalledRequiredDependencies = false;

                // 检查Unity官方包
                foreach (var dependency in tempInstance.currentProfile.unityPackages.Where(d => d.isRequired))
                {
                    if (!await CheckUnityPackageInstalledAsync(dependency))
                    {
                        hasUninstalledRequiredDependencies = true;
                        break;
                    }
                }

                // 检查Git包
                if (!hasUninstalledRequiredDependencies)
                {
                    foreach (var dependency in tempInstance.currentProfile.gitPackages.Where(d => d.isRequired))
                    {
                        if (!await CheckGitPackageInstalledAsync(dependency))
                        {
                            hasUninstalledRequiredDependencies = true;
                            break;
                        }
                    }
                }

                // 检查用户包
                if (!hasUninstalledRequiredDependencies)
                {
                    foreach (var dependency in tempInstance.currentProfile.userPackages.Where(d => d.isRequired))
                    {
                        if (!await CheckUserPackageInstalledAsync(dependency))
                        {
                            hasUninstalledRequiredDependencies = true;
                            break;
                        }
                    }
                }

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
                foreach (var dependency in tempInstance.currentProfile.unityPackages.Where(d => d.isRequired))
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
                foreach (var dependency in tempInstance.currentProfile.gitPackages.Where(d => d.isRequired))
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
                foreach (var dependency in tempInstance.currentProfile.userPackages.Where(d => d.isRequired))
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
                // 尝试直接获取类型
                var type = System.Type.GetType(className);
                if (type != null)
                {
                    return true;
                }
                else
                {
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

        [System.Serializable]
        public class InstallationProfile
        {
            public string profileName = "Default Profile";
            // Unity官方包 - 通过Package Manager直接安装
            public List<UnityPackageDependency> unityPackages = new List<UnityPackageDependency>();
            // Git包 - 通过Git URL安装
            public List<GitPackageDependency> gitPackages = new List<GitPackageDependency>();
            // 用户包 - 用户手动安装的包
            public List<UserPackageDependency> userPackages = new List<UserPackageDependency>();
            public string parentFolderPath; // Unity Package父文件夹路径
            public string installationNotes;
            public DateTime lastModified;
            public bool enableAutoCheck = true; // 是否启用编辑器启动时自动检查
            public bool skipNextAutoCheck = false; // 跳过下次自动检查

            /// <summary>
            /// 从文件加载配置
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
                    string configFilePath = Path.Combine(scriptFolder, "ESInstaller_Config.json");

                    if (File.Exists(configFilePath))
                    {
                        string json = File.ReadAllText(configFilePath);
                        var profile = JsonUtility.FromJson<InstallationProfile>(json);
                        return profile ?? CreateDefaultProfile();
                    }
                    else
                    {
                        return CreateDefaultProfile();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"加载配置文件失败: {e.Message}");
                    return CreateDefaultProfile();
                }
            }

            /// <summary>
            /// 创建默认配置
            /// </summary>
            private static InstallationProfile CreateDefaultProfile()
            {
                return new InstallationProfile
                {
                    profileName = "Default Profile",
                    unityPackages = new List<UnityPackageDependency>(),
                    gitPackages = new List<GitPackageDependency>(),
                    userPackages = new List<UserPackageDependency>(),
                    enableAutoCheck = true,
                    skipNextAutoCheck = false,
                    lastModified = DateTime.Now
                };
            }
        }

        #endregion

        #region 私有字段

        private InstallationProfile currentProfile;
        private Vector2 scrollPosition;
        private bool showUnityPackages = true;
        private bool showGitPackages = true;
        private bool showUserPackages = true;
        private bool showInstallation = true;
        private string statusMessage = "";
        private MessageType statusType = MessageType.Info;

        private string configFilePath;
        private string downloadsFolderPath;
        private const string CONFIG_FILE_NAME = "ESInstaller_Config.json";
        private const string DOWNLOADS_FOLDER_NAME = "Downloads";
        private const string DEFAULT_UNITY_PACKAGE_NAME = "ES_Framework_Package.unitypackage";

        // UI样式
        private GUIStyle headerStyle;
        private GUIStyle sectionStyle;
        private GUIStyle statusStyle;
        private GUIStyle buttonStyle;
        private GUIStyle packageNameStyle;

        // UI状态
        private bool hasInitialized = false;
        private bool isConfigModified = false;

        // 辅助方法：安全地修改配置并标记为已更改
        private void ModifyConfiguration(System.Action modificationAction)
        {
            modificationAction?.Invoke();
            isConfigModified = true;
        }

        #endregion

        #region 菜单项

        [MenuItem("ES/安装管理器", false, 0)]
        static void ShowInstaller()
        {
            var window = GetWindow<ESInstaller>("ES 安装管理器");
            window.minSize = new Vector2(600, 500);
            window.Show();
        }

        [MenuItem("ES/检查依赖", false, 2)]
        static void QuickCheckDependencies()
        {
            // 异步检查依赖并显示结果
            _ = QuickCheckAndShowResultAsync();
        }

        #endregion

        #region Unity生命周期

        private void OnEnable()
        {
            InitializePaths();
            LoadConfiguration();
            InitializeDefaultProfile();
        }

        private void InitializePaths()
        {
            // 获取当前脚本所在文件夹的路径
            var script = MonoScript.FromScriptableObject(this);
            string scriptPath = AssetDatabase.GetAssetPath(script);
            string scriptFolder = Path.GetDirectoryName(scriptPath);

            // 设置配置文件路径
            configFilePath = Path.Combine(scriptFolder, CONFIG_FILE_NAME);

            // 设置下载文件夹路径
            downloadsFolderPath = Path.Combine(scriptFolder, DOWNLOADS_FOLDER_NAME);

            // 确保下载文件夹存在
            if (!Directory.Exists(downloadsFolderPath))
            {
                Directory.CreateDirectory(downloadsFolderPath);
            }
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
                    LoadConfiguration();
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

            // 状态信息
            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
                EditorGUILayout.Space();
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

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

            // 安装管理
            DrawInstallationSection();

            EditorGUILayout.EndScrollView();

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

            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.fontStyle = FontStyle.Bold;
            }

            if (packageNameStyle == null)
            {
                packageNameStyle = new GUIStyle(EditorStyles.label);
                packageNameStyle.fontStyle = FontStyle.Bold;
                packageNameStyle.fontSize = 12;
                packageNameStyle.normal.textColor = new Color(0.1f, 0.4f, 0.8f); // 深蓝色
            }
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
                    LoadConfiguration();
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

        private void DrawUnityPackagesSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            showUnityPackages = EditorGUILayout.Foldout(showUnityPackages, "📦 Unity官方包 (Package Manager)", sectionStyle);

            if (showUnityPackages)
            {
                EditorGUILayout.Space(5);

                if (currentProfile == null || currentProfile.unityPackages == null)
                {
                    EditorGUILayout.LabelField("配置加载中...", EditorStyles.miniLabel);
                }
                else
                {
                    // 依赖列表
                    for (int i = 0; i < currentProfile.unityPackages.Count; i++)
                    {
                        DrawUnityPackageItem(i);
                    }

                    // 批量操作
                    if (currentProfile.unityPackages.Count > 0)
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
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawUnityPackageItem(int index)
        {
            var dependency = currentProfile.unityPackages[index];

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
            EditorGUILayout.LabelField($"Package ID: {dependency.packageId}");
            EditorGUILayout.LabelField($"检查类名: {dependency.checkClass}");
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

        private void DrawGitPackagesSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            showGitPackages = EditorGUILayout.Foldout(showGitPackages, "🔗 Git包 (通过URL安装)", sectionStyle);

            if (showGitPackages)
            {
                EditorGUILayout.Space(5);

                if (currentProfile == null || currentProfile.gitPackages == null)
                {
                    EditorGUILayout.LabelField("配置加载中...", EditorStyles.miniLabel);
                }
                else
                {
                    // 依赖列表
                    for (int i = 0; i < currentProfile.gitPackages.Count; i++)
                    {
                        DrawGitPackageItem(i);
                    }

                    // 批量操作
                    if (currentProfile.gitPackages.Count > 0)
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
            var dependency = currentProfile.gitPackages[index];

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

        private void DrawUserPackagesSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            showUserPackages = EditorGUILayout.Foldout(showUserPackages, "👤 用户包 (手动安装)", sectionStyle);

            if (showUserPackages)
            {
                EditorGUILayout.Space(5);

                if (currentProfile == null || currentProfile.userPackages == null)
                {
                    EditorGUILayout.LabelField("配置加载中...", EditorStyles.miniLabel);
                }
                else
                {
                    // 依赖列表
                    for (int i = 0; i < currentProfile.userPackages.Count; i++)
                    {
                        DrawUserPackageItem(i);
                    }

                    // 批量操作
                    if (currentProfile.userPackages.Count > 0)
                    {
                        EditorGUILayout.Space(10);
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("🔍 检查所有用户包"))
                        {
                            _ = CheckAllUserPackages();
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.HelpBox("用户包需要手动安装，安装器只负责检查是否存在指定的类。", MessageType.Info);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawUserPackageItem(int index)
        {
            var dependency = currentProfile.userPackages[index];

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
                    // Unity Package父文件夹
                    EditorGUILayout.LabelField("Unity Package 父文件夹", EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"路径: {currentProfile.parentFolderPath}", GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("📁 选择文件夹", GUILayout.Width(100)))
                    {
                        string selectedPath = EditorUtility.OpenFolderPanel("选择Unity Package父文件夹", currentProfile.parentFolderPath ?? "", "");
                        if (!string.IsNullOrEmpty(selectedPath))
                        {
                            ModifyConfiguration(() => currentProfile.parentFolderPath = selectedPath);
                            ShowStatus("父文件夹已更新，请记得保存配置", MessageType.Info);
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    // 显示扫描到的Unity Package文件
                    if (!string.IsNullOrEmpty(currentProfile.parentFolderPath) && Directory.Exists(currentProfile.parentFolderPath))
                    {
                        string[] unityPackages = Directory.GetFiles(currentProfile.parentFolderPath, "*.unitypackage");
                        if (unityPackages.Length > 0)
                        {
                            EditorGUILayout.LabelField($"找到 {unityPackages.Length} 个Unity Package文件:", EditorStyles.miniBoldLabel);
                            foreach (string packagePath in unityPackages)
                            {
                                string fileName = Path.GetFileName(packagePath);
                                EditorGUILayout.LabelField($"• {fileName}", EditorStyles.miniLabel);
                            }
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("在指定文件夹中未找到任何 .unitypackage 文件", MessageType.Warning);
                        }
                    }
                    else if (!string.IsNullOrEmpty(currentProfile.parentFolderPath))
                    {
                        EditorGUILayout.HelpBox("指定的父文件夹不存在", MessageType.Error);
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
                    if (GUILayout.Button("🚀 开始安装 ES 框架", buttonStyle, GUILayout.Height(40)))
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

        private void DrawBottomButtons()
        {
            EditorGUILayout.Space(10);

            // 快速刷新按钮 - 更突出显示
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 0.6f, 1.0f); // 蓝色背景
            if (GUILayout.Button("🚀 快速全部刷新状态", buttonStyle, GUILayout.Height(35)))
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

        private void InitializeDefaultProfile()
        {
            if (currentProfile == null)
            {
                currentProfile = new InstallationProfile();
            }

            // 如果是空的，添加一些默认依赖
            if (currentProfile.unityPackages.Count == 0)
            {
                // 添加一些常见的Unity官方包作为示例
                currentProfile.unityPackages.Add(new UnityPackageDependency
                {
                    name = "TextMeshPro",
                    version = "3.0.6",
                    description = "Unity文本渲染系统",
                    packageId = "com.unity.textmeshpro",
                    isRequired = true
                });

                currentProfile.unityPackages.Add(new UnityPackageDependency
                {
                    name = "Unity UI",
                    version = "1.0.0",
                    description = "Unity用户界面系统",
                    packageId = "com.unity.ugui",
                    isRequired = true
                });

                currentProfile.unityPackages.Add(new UnityPackageDependency
                {
                    name = "Timeline",
                    version = "1.8.2",
                    description = "Unity时间轴系统，用于创建复杂的动画和叙事序列",
                    packageId = "com.unity.timeline",
                    isRequired = true
                });

                currentProfile.unityPackages.Add(new UnityPackageDependency
                {
                    name = "Universal RP",
                    version = "14.0.8",
                    description = "Universal Render Pipeline，Unity的通用渲染管线",
                    packageId = "com.unity.render-pipelines.universal",
                    isRequired = true
                });
            }

            if (currentProfile.gitPackages.Count == 0)
            {
                // 添加一些Git包作为示例
                currentProfile.gitPackages.Add(new GitPackageDependency
                {
                    name = "Whisper",
                    version = "1.0.0",
                    description = "语音转文字支持",
                    gitUrl = "https://gitcode.com/gh_mirrors/wh/whisper.unity.git",
                    checkClass = "Whisper.WhisperManager",
                    isRequired = true
                });
            }

            if (currentProfile.userPackages.Count == 0)
            {
                // 添加一些用户包作为示例
                currentProfile.userPackages.Add(new UserPackageDependency
                {
                    name = "用户自定义包",
                    version = "1.0.0",
                    description = "用户手动安装的自定义包",
                    checkClass = "MyCustomNamespace.MyCustomClass",
                    isRequired = false
                });
            }

            currentProfile.parentFolderPath = downloadsFolderPath;
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

        private void LoadConfiguration()
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
            if (currentProfile == null || currentProfile.unityPackages == null)
            {
                ShowStatus("配置未加载，无法检查Unity包", MessageType.Warning);
                return;
            }

            ShowStatus("正在检查所有Unity官方包...", MessageType.Info);

            foreach (var dependency in currentProfile.unityPackages)
            {
                await CheckUnityPackageDependency(dependency);
            }

            ShowStatus("Unity官方包检查完成", MessageType.Info);
            Repaint();
        }

        private async Task InstallAllUnityPackages()
        {
            ShowStatus("正在安装所有Unity官方包...", MessageType.Info);

            foreach (var dependency in currentProfile.unityPackages.Where(d => !d.isInstalled))
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
            if (currentProfile == null || currentProfile.gitPackages == null)
            {
                ShowStatus("配置未加载，无法检查Git包", MessageType.Warning);
                return;
            }

            ShowStatus("正在检查所有Git包...", MessageType.Info);

            foreach (var dependency in currentProfile.gitPackages)
            {
                await CheckGitPackageDependency(dependency);
            }

            ShowStatus("Git包检查完成", MessageType.Info);
            Repaint();
        }

        private async Task InstallAllGitPackages()
        {
            ShowStatus("正在安装所有Git包...", MessageType.Info);

            foreach (var dependency in currentProfile.gitPackages.Where(d => !d.isInstalled))
            {
                await InstallGitPackageDependency(dependency);
                await Task.Delay(100);
            }

            ShowStatus("Git包安装完成", MessageType.Info);
            Repaint();
        }

        private async Task CheckAllUserPackages()
        {
            if (currentProfile == null || currentProfile.userPackages == null)
            {
                ShowStatus("配置未加载，无法检查用户包", MessageType.Warning);
                return;
            }

            ShowStatus("正在检查所有用户包...", MessageType.Info);

            foreach (var dependency in currentProfile.userPackages)
            {
                await CheckUserPackageDependency(dependency);
            }

            ShowStatus("用户包检查完成", MessageType.Info);
            Repaint();
        }

        private bool CheckAllDependenciesValid()
        {
            if (currentProfile == null ||
                currentProfile.unityPackages == null ||
                currentProfile.gitPackages == null ||
                currentProfile.userPackages == null)
            {
                return false;
            }

            bool unityPackagesValid = currentProfile.unityPackages.All(d => !d.isRequired || d.isInstalled);
            bool gitPackagesValid = currentProfile.gitPackages.All(d => !d.isRequired || d.isInstalled);
            bool userPackagesValid = currentProfile.userPackages.All(d => !d.isRequired || d.isInstalled);
            return unityPackagesValid && gitPackagesValid && userPackagesValid;
        }

        private void StartInstallation()
        {
            if (string.IsNullOrEmpty(currentProfile.parentFolderPath))
            {
                ShowStatus("未指定Unity Package父文件夹路径", MessageType.Error);
                return;
            }

            if (!Directory.Exists(currentProfile.parentFolderPath))
            {
                ShowStatus($"Unity Package父文件夹不存在: {currentProfile.parentFolderPath}", MessageType.Error);
                return;
            }

            // 扫描Unity Package文件
            string[] unityPackageFiles = Directory.GetFiles(currentProfile.parentFolderPath, "*.unitypackage");

            if (unityPackageFiles.Length == 0)
            {
                ShowStatus("在指定文件夹中未找到任何 .unitypackage 文件", MessageType.Error);
                return;
            }

            // 开始导入所有找到的Unity Package文件
            ShowStatus($"开始导入 {unityPackageFiles.Length} 个Unity Package文件...", MessageType.Info);

            foreach (string packagePath in unityPackageFiles)
            {
                string fileName = Path.GetFileName(packagePath);
                ShowStatus($"正在导入: {fileName}", MessageType.Info);

                // 导入Unity Package
                AssetDatabase.ImportPackage(packagePath, false);
            }

            ShowStatus($"ES框架安装已开始，共导入 {unityPackageFiles.Length} 个Unity Package文件，请等待Unity完成导入", MessageType.Info);
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
            foreach (var dep in currentProfile.unityPackages)
            {
                report += $"- {dep.name} ({dep.version}): {(dep.isInstalled ? "已安装" : "未安装")}\n";
            }

            report += "\nGit包:\n";
            foreach (var dep in currentProfile.gitPackages)
            {
                report += $"- {dep.name} ({dep.version}): {(dep.isInstalled ? "已安装" : "未安装")}\n";
            }

            report += "\n用户包:\n";
            foreach (var dep in currentProfile.userPackages)
            {
                report += $"- {dep.name} ({dep.version}): {(dep.isInstalled ? "已安装" : "未安装")}\n";
            }

            report += $"\nUnity Package 父文件夹: {currentProfile.parentFolderPath}\n";

            // 列出扫描到的Unity Package文件
            if (!string.IsNullOrEmpty(currentProfile.parentFolderPath) && Directory.Exists(currentProfile.parentFolderPath))
            {
                string[] unityPackages = Directory.GetFiles(currentProfile.parentFolderPath, "*.unitypackage");
                if (unityPackages.Length > 0)
                {
                    report += $"找到的Unity Package文件 ({unityPackages.Length}个):\n";
                    foreach (string packagePath in unityPackages)
                    {
                        string fileName = Path.GetFileName(packagePath);
                        report += $"- {fileName}\n";
                    }
                }
                else
                {
                    report += "未找到任何Unity Package文件\n";
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

        private void ShowStatus(string message, MessageType type)
        {
            statusMessage = message;
            statusType = type;
            Debug.Log($"[ES Installer] {message}");
        }

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

        #endregion
    }
}
