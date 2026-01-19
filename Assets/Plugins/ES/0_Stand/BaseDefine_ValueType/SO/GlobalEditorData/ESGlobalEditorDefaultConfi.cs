using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;


namespace ES
{
  [CreateAssetMenu(fileName = "全局编辑器流程基本配置", menuName = "全局SO/全局编辑器流程基本配置")]
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
    [FolderPath, LabelText("默认的全局Global父文件夹")]
    public string Path_GlobalParent;

    #endregion

    #region SO管理支持

    [TabGroup("SO管理支持")]
    [LabelText("常规排除Pack/Group/Info")]
    public bool ExcludePackGroupInfo = true;

    [TabGroup("SO管理支持")]
    [LabelText("常规排除Window类")]
    public bool ExcludeWindow = true;

    [TabGroup("SO管理支持")]
    [LabelText("常规排除NodeRunner类")]
    public bool ExcludeNodeRunner = true;

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
    public string PackageOutputPath = "Assets/../ESOutput/UnityPackage";


    [TabGroup("UnityPackage打包构建")]
    [LabelText("UnityPackage本体汇总位置"), FolderPath]
    public string PackageSelfPath = "Assets\\Plugins\\ES\\Editor\\Installer\\Downloads";


    [TabGroup("UnityPackage打包构建")]
    [LabelText("UnityPackage默认包名")]
    public string PackageName = "ESPackage0.35_";

    [TabGroup("UnityPackage打包构建")]
    [LabelText("收集的路径ES"), FolderPath]
    public List<string> PackageCollectPath = new List<string>() { "Assets/Plugins/ES", "Assets/Scripts/ESLogic" };
    
    [TabGroup("UnityPackage打包构建")]
    [LabelText("包含依赖项")]
    public bool IncludeDependencies = true;
    
    [TabGroup("UnityPackage打包构建")]
    [LabelText("发布Editor位置"), FolderPath]
    public string PackagePublishPath ="Assets\\Plugins\\ES\\Editor\\Installer" ;



    #endregion

    #region UnityPackage扩展打包配置

    [Serializable]
    public class UnityPackageConfig
    {
        [LabelText("配置名称")]
        public string ConfigName = "新配置";

        [LabelText("UnityPackage输出位置"), FolderPath]
        public string OutputPath = "Assets/../ESOutput/UnityPackage";

        [LabelText("UnityPackage包名")]
        public string PackageName = "ESPackage_Ext_";

        [LabelText("收集的路径列表"), FolderPath]
        public List<string> CollectPaths = new List<string>() { "Assets/Plugins/ES" };

        [LabelText("排除的文件夹列表"), FolderPath]
        public List<string> ExcludeFolders = new List<string>();

        [LabelText("是否启用")]
        public bool IsEnabled = true;

        [LabelText("包含依赖项")]
        public bool IncludeDependencies = true;
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
            OutputPath = "Assets/../ESOutput/UnityPackage",
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

        if (ESGlobalEditorDefaultConfi.Instance.ExcludeNodeRunner)
        {
          if (typeof(INodeRunner_Origin).IsAssignableFrom(t))
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
