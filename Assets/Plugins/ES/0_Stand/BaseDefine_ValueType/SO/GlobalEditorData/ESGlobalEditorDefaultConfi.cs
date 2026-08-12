using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;


namespace ES
{
  [CreateAssetMenu(fileName = "全局编辑器流程基本配置", menuName = MenuItemPathDefine.ASSET_GLOBAL_SO_PATH + "全局编辑器流程基本配置")]
  [ESOnlyEditorSO("全局编辑器默认配置只服务编辑器流程和路径，不应进入运行时构建或AB资源包。")]
  [ESSOEditorPreLoad]
  public class ESGlobalEditorDefaultConfi : ESEditorGlobalSo<ESGlobalEditorDefaultConfi>
  {
    #region 文件夹管理

    [TabGroup("文件夹管理")]
    [FolderPath, LabelText("默认的SOInfo脚本父文件夹")]
    public string Path_SoInfoParent;

    [TabGroup("文件夹管理")]
    [FolderPath, LabelText("默认的DataPack包父文件夹")]
    public string Path_PackParent;

    [TabGroup("文件夹管理")]
    [FolderPath, LabelText("默认的DataGroup组父文件夹")]
    public string Path_GroupParent;

    [TabGroup("文件夹管理")]
    [FolderPath, LabelText("默认的常规SO脚本父文件夹")]
    public string Path_NormalScriptParent;

    [TabGroup("文件夹管理")]
    [FolderPath, LabelText("默认的常规SO父文件夹")]
    public string Path_NormalParent;

    [TabGroup("文件夹管理")]
    [FolderPath, LabelText("默认的资源管理父文件夹")]
    public string Path_ResourceParent;

    [TabGroup("文件夹管理")]
    [FolderPath, LabelText("默认的资产包烘焙数据父文件夹")]
    public string Path_AssetPackageBakeParent = "Assets/ESNormalAssets/Data/AssetPackageBake";

    [TabGroup("文件夹管理")]
    [FolderPath, LabelText("默认的全局Global父文件夹")]
    public string Path_GlobalParent;

    [TabGroup("文件夹管理")]
    [FolderPath, LabelText("默认Library库放置文件夹")]
    [InlineButton("Ping_", "<*>")]
    public string Path_AllLibraryFolder_ = "Assets/ESNormalAssets/Data/AssetLibrary";

    private void Ping_(string path)
    {
      ESStandUtility.SafeEditor.Quick_CreateFolderByFullPath(path);
      ESStandUtility.SafeEditor.Quick_PingAssetByPath(path);
    }

    #endregion

    #region SO管理支持

    [TabGroup("SO管理支持")]
    [LabelText("常规排除Pack/Group/Info")]
    public bool ExcludePackGroupInfo = true;

    [TabGroup("SO管理支持")]
    [LabelText("常规排除Window类")]
    public bool ExcludeWindow = true;

    [TabGroup("SO管理支持")]
    [LabelText("常规排除Global类")]
    public bool ExcludeGlobal = true;

    [TabGroup("SO管理支持")]
    [LabelText("常规排除列表")]
    public List<string> ExcludeNameList = new List<string>();

    [TabGroup("SO管理支持")]
    [LabelText("常规排除查询")]
    [InlineButton("Exclude", "排除")]
    [ValueDropdown("@ESGlobalEditorDefaultConfi.GetUseableNormalSoNames()", AppendNextDrawer = false)]
    public string ExcludeHandle = "";

    #endregion

    #region UnityPackage打包构建

    [TabGroup("UnityPackage打包构建")]
    [LabelText("UnityPackage发布打包输出到"), FolderPath]
    public const string DefaultUnityPackageOutputPath = "Assets/../ES/Output/UnityPackages";

    public string PackageOutputPathForPublish = DefaultUnityPackageOutputPath;


    [TabGroup("UnityPackage打包构建")]
    [LabelText("UnityPackage本体汇总位置"), FolderPath]
    public string PackageSelfPathForMain = "Assets/Plugins/ES/Editor/Installer/Downloads/Main";


    [TabGroup("UnityPackage打包构建")]
    [LabelText("UnityPackage默认包名")]
    public string PackageName = "ESPackage0.35_";

    public const int DefaultPackagePublishMaxAssetCount = 12000;
    public const long DefaultPackagePublishMaxSourceBytes = 96L * 1024L * 1024L;

    public static List<string> CreateDefaultPackagePublishAssetPaths()
    {
      return new List<string>
      {
        "Assets/Plugins/ES",
        "Assets/Scripts/ESLogic",
        "Assets/KinematicCharacterController",
        "Assets/Packages/Newtonsoft.Json.13.0.4",
        "Assets/Plugins/Sirenix",
        "Assets/Plugins/RootMotion",
        "Assets/Plugins/Easy Save 3",
        "Assets/ESNormalAssets/Camera",
        "Assets/ESNormalAssets/Data/GlobalData/EditorConfi",
        "Assets/ESNormalAssets/Data/GlobalData/EditorTheme",
        "Assets/ESNormalAssets/Data/GlobalData/GameCore"
      };
    }

    public static List<string> CreateDefaultPackagePublishRequiredAssetPaths()
    {
      return new List<string>
      {
        "Assets/ESNormalAssets/Data/GlobalData/AssetSettings/ESAssetReleaseUploadSettings.asset",
        "Assets/ESNormalAssets/Data/GlobalData/AssetSettings/全局资源管理设置.asset",
        "Assets/ESNormalAssets/Data/GlobalData/TrackSequenceEditorSettings/TrackSequenceEditorSettings.asset"
      };
    }

    [TabGroup("UnityPackage打包构建")]
    [LabelText("收集的路径ES"), FolderPath]
    public List<string> PackageCollectPath = new List<string>()
    {
      "Assets/Plugins/ES",
      "Assets/Scripts/ESLogic",
      "Assets/KinematicCharacterController",
      "Assets/Packages/Newtonsoft.Json.13.0.4",
      "Assets/Plugins/Sirenix",
      "Assets/Plugins/RootMotion",
      "Assets/Plugins/Easy Save 3"
    };

    [TabGroup("UnityPackage打包构建")]
    [LabelText("正式发布资产白名单"), FolderPath]
    [InfoBox("正式发布会完整导出这里的目录。当前完整包包含 3_Examples、Odin、RootMotion 和 Easy Save 3，只能在相应授权允许的范围内使用或分发。")]
    public List<string> PackagePublishAssetPaths = CreateDefaultPackagePublishAssetPaths();

    [TabGroup("UnityPackage打包构建")]
    [LabelText("正式发布必需资产（文件或文件夹）")]
    [InfoBox("这里保存不能由 Unity 序列化依赖自动发现、但完整框架必须携带的稳定默认资产。删除内置必需项会导致闭包检查失败。")]
    public List<string> PackagePublishRequiredAssetPaths = CreateDefaultPackagePublishRequiredAssetPaths();

    [TabGroup("UnityPackage打包构建")]
    [LabelText("发布依赖允许根"), FolderPath]
    [InfoBox("这些目录不会整体打包。发布工具只纳入正式根实际引用到的资源；发现未允许的包外 Assets 依赖时会拒绝发布。")]
    public List<string> PackagePublishDependencyAllowPaths = new List<string>()
    {
      "Assets/ESNormalAssets",
      "Assets/LoafbrrAssets",
      "Assets/Demo_FGT"
    };

    [TabGroup("UnityPackage打包构建")]
    [LabelText("外部依赖引用根"), FolderPath]
    [InfoBox("这里用于未来仍需预装、允许引用但禁止随包导出的 Assets 依赖。当前完整包没有此类默认目录。")]
    public List<string> PackagePublishExternalReferencePaths = new List<string>();

    [TabGroup("UnityPackage打包构建")]
    [LabelText("正式发布最大资源数")]
    [MinValue(1)]
    public int PackagePublishMaxAssetCount = DefaultPackagePublishMaxAssetCount;

    [TabGroup("UnityPackage打包构建")]
    [LabelText("正式发布最大源文件字节")]
    [MinValue(1)]
    public long PackagePublishMaxSourceBytes = DefaultPackagePublishMaxSourceBytes;

    [TabGroup("UnityPackage打包构建")]
    [LabelText("正式发布排除路径（文件或文件夹）")]
    [InfoBox("排除测试、Obsolete、安装产物、可选 AITest 适配器，以及会引入项目专用资源或已裁剪依赖的示例资产。3_Examples 主体、FinalIK 运行时和 Easy Save 3 运行时继续保留。")]
    public List<string> PackagePublishExcludePaths = new List<string>()
    {
      "Assets/Plugins/ES/Obsolete",
      "Assets/Plugins/ES/Editor/Installer/Downloads",
      "Assets/Plugins/ES/0_Stand/Tests",
      "Assets/Plugins/ES/1_Design/Tests",
      "Assets/Scripts/ESLogic/Tests",
      "Assets/Scripts/ESLogic/Editor/Generation/Tests",
      "Assets/Scripts/ESLogic/Runtime/Developer/AITest",
      "Assets/Plugins/RootMotion/Shared Demo Assets",
      "Assets/Plugins/RootMotion/FinalIK/_DEMOS",
      "Assets/Plugins/RootMotion/FinalIK/_Integration",
      "Assets/Plugins/RootMotion/Baker",
      "Assets/Plugins/RootMotion/Editor/Baker",
      "Assets/Plugins/RootMotion/Editor/FinalIK/_DEMOS",
      "Assets/Plugins/RootMotion/Editor/Shared Demo Scripts",
      "Assets/Plugins/RootMotion/FinalIK/Tools/VRIK Animated Locomotion.controller",
      "Assets/Plugins/Easy Save 3/Scripts/Save Slots",
      "Assets/Plugins/ES/3_Examples/1_Runtime/Example_SimpleTools/New Scene 1.unity",
      "Assets/Plugins/ES/ThirdParty/JUMP_SystemSpeech.asset"
    };

    [TabGroup("UnityPackage打包构建")]
    [LabelText("包含依赖项")]
    public bool IncludeDependencies_ = false;

    [TabGroup("UnityPackage打包构建")]
    [LabelText("发布Editor安装器位置"), FolderPath]
    public string PackagePublishPath = "Assets\\Plugins\\ES\\Editor\\Installer";



    #endregion

    #region UnityPackage扩展打包配置

    [Serializable]
    public class UnityPackageConfig
    {
      [LabelText("配置名称")]
      public string ConfigName = "新配置";

      [LabelText("UnityPackage输出位置"), FolderPath]
      public string OutputPath = DefaultUnityPackageOutputPath;

      [LabelText("UnityPackage包名")]
      public string PackageName = "ESPackage_Ext_";

      [LabelText("收集的路径列表"), FolderPath]
      public List<string> CollectPaths = new List<string>() { "Assets/Plugins/ES" };

      [LabelText("排除的文件夹列表"), FolderPath]
      public List<string> ExcludeFolders = new List<string>();

      [LabelText("是否启用")]
      public bool IsEnabled = true;

      [LabelText("包含依赖项")]
      public bool IncludeDependencies_ = false;
    }

    [TabGroup("UnityPackage扩展配置")]
    [LabelText("扩展打包配置列表")]
    [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "ConfigName")]
    public List<UnityPackageConfig> ExtendedPackageConfigs = new List<UnityPackageConfig>();

    [TabGroup("UnityPackage扩展配置")]
    [Button("添加新配置", ButtonSizes.Medium)]
    [GUIColor(0.5f, 0.8f, 1f)]
    public void AddNewPackageConfig()
    {
      var newConfig = new UnityPackageConfig
      {
        ConfigName = $"配置 {ExtendedPackageConfigs.Count + 1}",
        OutputPath = DefaultUnityPackageOutputPath,
        PackageName = $"ESPackage_Ext_{ExtendedPackageConfigs.Count + 1}_",
        CollectPaths = new List<string>() { "Assets/Plugins/ES" },
        ExcludeFolders = new List<string>(),
        IsEnabled = true
      };
      ExtendedPackageConfigs.Add(newConfig);
#if UNITY_EDITOR
      EditorUtility.SetDirty(this);
#endif
    }

    [TabGroup("UnityPackage扩展配置")]
    [Button("清理禁用配置", ButtonSizes.Medium)]
    [GUIColor(1f, 0.6f, 0.6f)]
    public void CleanDisabledConfigs()
    {
      ExtendedPackageConfigs.RemoveAll(config => !config.IsEnabled);
#if UNITY_EDITOR
      EditorUtility.SetDirty(this);
#endif
    }

#if UNITY_EDITOR
    [TabGroup("UnityPackage鎵╁睍閰嶇疆")]
    [Button("校验并清理无效路径", ButtonSizes.Medium)]
    [GUIColor(0.8f, 1f, 0.7f)]
    public void ValidateAndCleanPackagePaths()
    {
      CleanPathList(PackageCollectPath, nameof(PackageCollectPath), true);
      CleanPathList(PackagePublishAssetPaths, nameof(PackagePublishAssetPaths), true);
      CleanAssetPathList(PackagePublishRequiredAssetPaths, nameof(PackagePublishRequiredAssetPaths));
      CleanPathList(PackagePublishDependencyAllowPaths, nameof(PackagePublishDependencyAllowPaths), true);
      CleanPathList(PackagePublishExternalReferencePaths, nameof(PackagePublishExternalReferencePaths), true);
      CleanPathList(PackagePublishExcludePaths, nameof(PackagePublishExcludePaths), true);

      if (ExtendedPackageConfigs != null)
      {
        for (int i = 0; i < ExtendedPackageConfigs.Count; i++)
        {
          var config = ExtendedPackageConfigs[i];
          if (config == null) continue;

          config.OutputPath = NormalizeAssetPath(config.OutputPath);
          CleanPathList(config.CollectPaths, $"{config.ConfigName}.CollectPaths", true);
          CleanPathList(config.ExcludeFolders, $"{config.ConfigName}.ExcludeFolders", true);
        }
      }

      EditorUtility.SetDirty(this);
      AssetDatabase.SaveAssets();
    }

    [TabGroup("鏂囦欢澶圭鐞?")]
    [Button("校验基础文件夹路径", ButtonSizes.Medium)]
    [GUIColor(0.8f, 0.9f, 1f)]
    public void ValidateBaseFolderPaths()
    {
      ValidateFolderPath(Path_SoInfoParent, nameof(Path_SoInfoParent), false);
      ValidateFolderPath(Path_PackParent, nameof(Path_PackParent), false);
      ValidateFolderPath(Path_GroupParent, nameof(Path_GroupParent), false);
      ValidateFolderPath(Path_NormalScriptParent, nameof(Path_NormalScriptParent), false);
      ValidateFolderPath(Path_NormalParent, nameof(Path_NormalParent), false);
      ValidateFolderPath(Path_ResourceParent, nameof(Path_ResourceParent), false);
      ValidateFolderPath(Path_AssetPackageBakeParent, nameof(Path_AssetPackageBakeParent), false);
      ValidateFolderPath(Path_GlobalParent, nameof(Path_GlobalParent), true);
      ValidateFolderPath(Path_AllLibraryFolder_, nameof(Path_AllLibraryFolder_), false);
    }

    private static string NormalizeAssetPath(string path)
    {
      return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').Trim();
    }

    private static bool ValidateFolderPath(string path, string label, bool allowEmpty)
    {
      path = NormalizeAssetPath(path);
      if (string.IsNullOrEmpty(path))
      {
        if (!allowEmpty) Debug.LogWarning($"[ESGlobalEditorDefaultConfi] {label} is empty.");
        return allowEmpty;
      }

      if (AssetDatabase.IsValidFolder(path)) return true;

      Debug.LogWarning($"[ESGlobalEditorDefaultConfi] Invalid folder path in {label}: {path}");
      return false;
    }

    private static void CleanPathList(List<string> paths, string label, bool removeInvalid)
    {
      if (paths == null) return;

      HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      for (int i = paths.Count - 1; i >= 0; i--)
      {
        string path = NormalizeAssetPath(paths[i]);
        bool remove = string.IsNullOrEmpty(path);

        if (!remove && used.Contains(path))
        {
          remove = true;
        }

        if (!remove && removeInvalid && !AssetDatabase.IsValidFolder(path))
        {
          Debug.LogWarning($"[ESGlobalEditorDefaultConfi] Remove invalid folder path in {label}: {path}");
          remove = true;
        }

        if (remove)
        {
          paths.RemoveAt(i);
        }
        else
        {
          paths[i] = path;
          used.Add(path);
        }
      }
    }

    private static void CleanAssetPathList(List<string> paths, string label)
    {
      if (paths == null) return;

      HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      for (int i = paths.Count - 1; i >= 0; i--)
      {
        string path = NormalizeAssetPath(paths[i]);
        bool exists = AssetDatabase.IsValidFolder(path)
          || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path));
        if (string.IsNullOrEmpty(path) || !exists || !used.Add(path))
        {
          if (!string.IsNullOrEmpty(path) && !exists)
            Debug.LogWarning($"[ESGlobalEditorDefaultConfi] Remove invalid asset path in {label}: {path}");
          paths.RemoveAt(i);
          continue;
        }

        paths[i] = path;
      }
    }
#endif

    #endregion

    #region 工具支持配置
    [TabGroup("工具支持")]
    public AudioSource audioPlayer;
    private static AudioSource _previewSource;
    [Button("测试播放")]
    public static void Play(AudioClip clip)
    {
      if (clip == null)
      {
        Debug.LogWarning("EditorAudio.Play: clip is null");
        return;
      }

      // 如果还没有创建，就建一个
      if (_previewSource == null)
      {
        var go = new GameObject("EditorAudioPreview");
        go.hideFlags = HideFlags.HideAndDontSave;

        _previewSource = go.AddComponent<AudioSource>();
        _previewSource.playOnAwake = false;
      }

      _previewSource.Stop();
      _previewSource.clip = clip;
      _previewSource.Play();
    }

    #endregion


    #region 方法

    public void Exclude()
    {
      if (ExcludeNameList.Contains(ExcludeHandle))
      {

      }
      else
      {
        ExcludeNameList.Add(ExcludeHandle);
      }
#if UNITY_EDITOR
      EditorUtility.SetDirty(this);
#endif
    }

    public static List<string> GetUseableNormalSoNames()
    {
      var list = ESEditorSO.AllSoNames.Keys.ToList();
#if UNITY_EDITOR
      var types = ESEditorSO.AllSoNames.Values.ToArray();
      var listToRemove = ESGlobalEditorDefaultConfi.Instance.ExcludeNameList.ToHashSet();

      foreach (var t in types)
      {
        if (ESGlobalEditorDefaultConfi.Instance.ExcludeWindow)
        {
          if (t.IsSubclassOf(typeof(EditorWindow)))
          {
            listToRemove.Add(ESEditorSO.AllSoNames.GetKey(t));
          }
        }

        if (ESGlobalEditorDefaultConfi.Instance.ExcludeGlobal)
        {
          if (typeof(IESGlobalData).IsAssignableFrom(t))
          {
            listToRemove.Add(ESEditorSO.AllSoNames.GetKey(t));
          }
        }

        if (ESGlobalEditorDefaultConfi.Instance.ExcludePackGroupInfo)
        {
          if (typeof(ISoDataPack).IsAssignableFrom(t) ||
              typeof(ISoDataGroup).IsAssignableFrom(t) ||
              typeof(ISoDataInfo).IsAssignableFrom(t))
          {
            listToRemove.Add(ESEditorSO.AllSoNames.GetKey(t));
          }
        }
      }

      foreach (var s in listToRemove)
      {
        list.Remove(s);
      }
#endif
      return list;
    }

    #endregion
  }
}
