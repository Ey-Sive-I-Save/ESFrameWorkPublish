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
    [FolderPath, LabelText("默认的常规SO父文件夹")]
    public string Path_NormalParent;
    [TabGroup("文件夹管理")]
    [FolderPath, LabelText("默认的全局Global父文件夹")]
    public string Path_GlobalParent;


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
      }



      if (ESGlobalEditorDefaultConfi.Instance.ExcludePackGroupInfo)
      {
        foreach (var s in list)
        {
          if (s.Contains("DataPack") || s.Contains("DataGroup") || s.Contains("DataInfo"))
          {
            listToRemove.Add(s);
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
  }
}
