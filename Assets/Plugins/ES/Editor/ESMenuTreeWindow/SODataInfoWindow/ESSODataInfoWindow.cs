using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    using Sirenix.OdinInspector;
    using Sirenix.OdinInspector.Editor;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;
    using System;
    using ES;
    using Sirenix.Serialization;
    using System.IO;
    using UnityEngine.UIElements;
    using System.Linq;
    using Sirenix.Utilities;

    namespace ES
    {
        //窗口总览
        public class ESSODataInfoWindow : ESMenuTreeWindowAB<ESSODataInfoWindow> //OdinMenuEditorWindow
        {
            [MenuItem("Tools/ES工具/ES数据窗口")]
            public static void TryOpenWindow()
            {
                OpenWindow();
            }
            #region 简单重写
            public override GUIContent ESWindow_GetWindowGUIContent()
            {
                var content = new GUIContent("ES数据窗口", "使用ES数据工具完成快速各种数据配置与开发");
                return content;
            }
            public override void ESWindow_OnOpen()
            {
                base.ESWindow_OnOpen();
                if (UsingWindow.HasDelegate)
                {
                    //已经注册委托
                }
                else
                {
                    UsingWindow.DelegateHandle();
                }

            }
            private void DelegateHandle()
            {
                HasDelegate = true;
            }
            #endregion
            #region 数据滞留与声明

            //根页面名

            public const string PageName_DataMake = "数据脚本生成工具";

            public const string PageName_DataPackCreate = "【查询\\新建包】";

            public const string PageName_DataGroupCreate = "【查询\\新建组】";

            public const string PageName_DataGroupOnChooseEditInfo = "【编辑组】";


            [NonSerialized] public Page_Root_Data_StartUse pageForStartUsePage;
            [NonSerialized] public Page_Root_DataScpirtCodeTool pageRootForCodeGen;
            [NonSerialized] public Page_CreateNewSoPackOrSearch pageForSodataPack;
            [NonSerialized] public Page_CreateNewGroupOrSearch pageForSodataGroup;
            [NonSerialized] public Page_DataGroupOnChoose pageForGroupOnChoose;

            public string selectPackTypeName_ = "Buff数据";
            public string selectGroupTypeName_ = "Buff数据";
            private bool HasDelegate = false;

            public static string guidForCopiedGroup = "";
            public static string guidForCopiedInfoKey = "";
            public static ScriptableObject CopyGroup;
            #endregion
            #region 缓冲刷新和加载保存

            //缓冲回执
            protected override void OnImGUI()
            {
                if (UsingWindow == null)
                {
                    UsingWindow = this;
                    ES_LoadData();
                }
                if (UsingWindow != null)
                {

                }
                base.OnImGUI();
            }
            /// <summary>
            /// 刷新窗口
            /// </summary>
            public override void ESWindow_RefreshWindow()
            {
                base.ESWindow_RefreshWindow();
                ES_SaveData();
            }
            public string pathForDefaultSetting;
            public override void ES_LoadData()
            {
                if (EditorPrefs.HasKey("guidForCopiedGroup"))
                {
                    guidForCopiedGroup = EditorPrefs.GetString("guidForCopiedGroup");
                    CopyGroup = ESDesignUtility.SafeEditor.LoadAssetByGUIDString<ScriptableObject>(guidForCopiedGroup) ?? CopyGroup;
                }
                if (EditorPrefs.HasKey("guidForCopiedInfoKey"))
                {
                    guidForCopiedInfoKey = EditorPrefs.GetString("guidForCopiedInfoKey");
                }
            }
            public override void ES_SaveData()
            {
                if (CopyGroup != null)
                {
                    guidForCopiedGroup = ESDesignUtility.SafeEditor.GetAssetGUID(CopyGroup) ?? guidForCopiedGroup;
                    if (guidForCopiedGroup != null)
                    {
                        EditorPrefs.SetString("guidForCopiedGroup", guidForCopiedGroup);
                        EditorPrefs.SetString("guidForCopiedInfoKey", guidForCopiedInfoKey);
                    }
                }
            }

            #endregion
            protected override void ES_OnBuildMenuTree(OdinMenuTree tree)
            {
                base.ES_OnBuildMenuTree(tree);
                {   //独立功能块
                    Part_BuildDataScriptCodePage(tree);
                    Part_BuildSoPackPage(tree);
                    Part_BuildSoDataGroupPage(tree);
                    Part_BuildSoDataGroupOnChooseAndInfos(tree);
                    //开始使用界面
                    Part_BuildStartPage(tree);
                    //关于
                    Part_AboutPage(tree);
                }
                ES_LoadData();
            }
            #region 页面构建方法
            private void Part_BuildStartPage(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, "开始使用", ref pageForStartUsePage, SdfIconType.SunFill);
            }


            private void Part_BuildDataScriptCodePage(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, PageName_DataMake, ref pageRootForCodeGen, SdfIconType.Braces);
            }
            private void Part_BuildSoPackPage(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, PageName_DataPackCreate, ref pageForSodataPack, SdfIconType.CartXFill);
                var TypeSelect = ESSODataWindowHelper.GetPackType(pageForSodataPack.createPackType_);
                var allPacks = ESDesignUtility.SafeEditor.FindAllSOAssets<ISoDataPack>(TypeSelect);
                foreach (var i in allPacks)
                {
                    if (i is ScriptableObject so)
                    {
                        tree.Add(PageName_DataPackCreate + $"/包：{i.FileName} 文件:{so.name}", new Page_Index_EveryDataPack() { pack = i }.ES_Refresh(), SdfIconType.Cart);
                    }
                }
            }
            private void Part_BuildSoDataGroupPage(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, PageName_DataGroupCreate, ref pageForSodataGroup, SdfIconType.BagXFill);
                var TypeSelect = ESSODataWindowHelper.GetGroupType(pageForSodataGroup.createGroup_);
                var allGroups = ESDesignUtility.SafeEditor.FindAllSOAssets<ISoDataGroup>(TypeSelect);

                foreach (var i in allGroups)
                {
                    if (i is ScriptableObject so)
                    {
                        tree.Add(PageName_DataGroupCreate + $"/组：{i.FileName} 文件:{so.name}", new Page_Index_DataQuickSeeGroup() { group = so }, SdfIconType.Bag);
                    }
                }

            }
            public void Part_BuildSoDataGroupOnChooseAndInfos(OdinMenuTree tree)
            {
                QuickBuildRootMenu(tree, PageName_DataGroupOnChooseEditInfo, ref pageForGroupOnChoose, SdfIconType.BookmarkXFill);

                if (Selection.activeObject is ISoDataGroup group_)
                {
                  
                    pageForGroupOnChoose.group = group_;
                }
                if (pageForGroupOnChoose.group != null)
                {
                   
                    foreach (var i in pageForGroupOnChoose.group.AllKeys)
                    {
                        ISoDataInfo so = pageForGroupOnChoose.group.GetInfoByKey(i);
                        if (so != default)
                        {
                            
                            tree.Add($"{MenuItems[PageName_DataGroupOnChooseEditInfo].Name}/<{i}>编辑", new Page_Index_DataInfoSingle() { data = so }, SdfIconType.Bookmark); ;
                        }
                    }
                }
                else
                {

                }
                return;
            }

            private void Part_AboutPage(OdinMenuTree tree)
            {
                tree.Add("关于", new Page_About(), SdfIconType.ChatSquareQuoteFill);
            }
            #endregion
        }
        #region 数据工具
        //创建 数据工具 总页面
        public class Page_Root_DataScpirtCodeTool : ESWindowPageBase
        {
            [DisplayAsString(fontSize: 30), HideLabel]
            public string readMe = "数据分为\nDataInfo(信息),\nDataGroup(组),\nDataPack(包),\n\b现在开始填表来创建新的数据类型!\b";
            [LabelText("英文数据代码名(如\"Actor\")")]
            public string EnglishCodeName = "DataName";
            [LabelText("中文数据显示名(如\"角色\")")]
            public string ChineseDisplayName = "数据名";
            [LabelText("数据父文件夹"), FolderPath]
            public string folder = "Assets/Scripts/ESFramework/Data/DataToolScript";

            public override ESWindowPageBase ES_Refresh()
            {
                folder = ESGlobalEditorDefaultConfi.Instance.Path_SoInfoParent;
                return base.ES_Refresh();
            }

            [Button("开始生成", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public void GenerateData()
            {

                if (AssetDatabase.IsValidFolder(folder))
                {
                    if (ESDesignUtility.SafeEditor.Quick_TryCreateChildFolder(folder, "InfoType", out var toInfo)
                     && ESDesignUtility.SafeEditor.Quick_TryCreateChildFolder(folder, "GroupType", out var toGroup)
                     && ESDesignUtility.SafeEditor.Quick_TryCreateChildFolder(folder, "PackType", out var toPack))
                    {
                        string infoName = (EnglishCodeName + "DataInfo")._ToValidIdentName();
                        ESDesignUtility.SimpleScriptMaker.CreateScriptEasy(toInfo, infoName, Attribute:
                            $"[ESCreatePath({"数据信息"._AsStringValue()}, \"{ChineseDisplayName}数据信息\")]", parent: ": SoDataInfo");
                        ESDesignUtility.SimpleScriptMaker.CreateScriptEasy(toGroup, EnglishCodeName + "DataGroup", Attribute:
                            $"[ESCreatePath({"数据组"._AsStringValue()}, \"{ChineseDisplayName}数据组\")]", parent: $": SoDataGroup<{infoName}>");
                        ESDesignUtility.SimpleScriptMaker.CreateScriptEasy(toPack, EnglishCodeName + "DataPack", Attribute:
                            $"[ESCreatePath({"数据包"._AsStringValue()}, \"{ChineseDisplayName}数据包\")]", parent: $": SoDataPack<{infoName}>");
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();

                    }
                    else
                    {
                        ESDesignUtility.SafeEditor.Wrap_DisplayDialog("请选择正确的文件夹", $"默认使用【{ESGlobalEditorDefaultConfi.Instance.Path_SoInfoParent}】作为生成总路径哦", "知道了");
                    }
                }

            }
        }
        //创建数据包
        [Serializable]
        public class Page_CreateNewSoPackOrSearch : ESWindowPageBase
        {
            [Title("开始新建数据包！", "可以先选择预设类型", bold: true, titleAlignment: TitleAlignments.Centered)]

            [HorizontalGroup("总组")]
            [DisplayAsString(fontSize: 30, Alignment = TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            [VerticalGroup("总组/数据")]
            public string createText = "【创建新数据包】";
            [LabelText("初始化数据组"), SerializeReference, GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            [VerticalGroup("总组/数据")]
            public List<ISoDataGroup> CachingAddGroups = new List<ISoDataGroup>();


            [DetailedInfoBox("创建一个数据包用于引用大量数据！", "创建一个数据包！！将会支持Buff,技能,人物,物品等", infoMessageType: InfoMessageType.Info)]
            [InfoBox("请修改一下文件名否则会分配随机数字后缀", VisibleIf = "@!hasChange", InfoMessageType = InfoMessageType.Warning)]
            [VerticalGroup("总组/数据"), ESBackGround("yellow", 0.2f), LabelText("文件命名"), Space(5), GUIColor("@ESDesignUtility.ColorSelector.Color_04"), OnValueChanged("OnValueChanged_ChangeHappen")]
            public string createName_ = "新建数据包";
           
            private bool hasChange = false;
            private void OnValueChanged_ChangeHappen()
            {
                hasChange = true;
            }
            [FolderPath, OnValueChanged("Refresh")]
            [VerticalGroup("总组/数据"), LabelText("保存到文件夹"), Space(5), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public string FolderPath_ = "Assets/Resources/Data/PackData";
            [VerticalGroup("总组/数据")]
            [ESBoolOption("自动使用父级文件夹", "直接放到指定文件夹")]
            public bool AutoParentFolder = true;
            private void Refresh()
            {
                AssetDatabase.Refresh();
                createName_ = "新建" + createPackType_;
                createText = $"--新建【{createPackType_}】--";
            }
            public override ESWindowPageBase ES_Refresh()
            {
                if (createPackType_.IsNullOrWhitespace())
                {
                    createPackType_ = ESSODataWindowHelper.GetPackNames().First();
                }
                Refresh();
                FolderPath_ = ESGlobalEditorDefaultConfi.Instance.Path_PackParent;
                return base.ES_Refresh();
            }

            [DisplayAsString(fontSize: 30, Alignment = TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            [VerticalGroup("总组/数据")]
            public string createTypeText = "--选择数据包类型--";


            [OnValueChanged("OnValueChanged_ResetConfigure")]
            [VerticalGroup("总组/数据"), Space(5), LabelText("选择类型")]
            [ValueDropdown("@ESSODataWindowHelper.GetPackNames()", AppendNextDrawer = false)]
            public string createPackType_ = "";



            #region 按钮
            [HorizontalGroup("总组", width: 100)]
            [VerticalGroup("总组/按钮")]
            [PropertySpace(15)]
            [Button(ButtonHeight = 30, Name = "新建数据包", IconAlignment = IconAlignment.RightEdge), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void CreateSoPackAsset()
            {
                Type targetType = ESSODataWindowHelper.GetPackType(createPackType_);
                string path = FolderPath_;
                Debug.Log(path + targetType.Name + targetType.Name._RemoveSubStrings("Data", "Pack"));
                if (AutoParentFolder) ESDesignUtility.SafeEditor.Quick_TryCreateChildFolder(FolderPath_, targetType.Name._RemoveSubStrings("Data", "Pack"), out path);
                Debug.Log(path);
                var create = ESDesignUtility.SafeEditor.CreateSOAsset(targetType, path, createName_, true, hasChange, beforeSave);
                void beforeSave(ScriptableObject so)
                {
                    if (so is ISoDataPack pack)
                    {
                        if (CachingAddGroups != null)
                        {
                            foreach (var i in CachingAddGroups)
                            {
                                if (i != null)
                                    pack._AddInfosFromGroup(i);
                            }
                        }
                        Selection.activeObject = so;
                    }
                    else
                    {
                        Debug.LogError("非法文件夹路径或者类型错误！！");
                    }
                }
            }


            [VerticalGroup("总组/按钮")]
            [PropertySpace(15)]
            [Button(ButtonHeight = 30, Name = "搜集数据组"), GUIColor("@ ESDesignUtility.ColorSelector.Color_04")]
            public void FindAInfoGroupAsset()
            {
                //找到全部数据组
                var GroupType = ESSODataWindowHelper.GetGroupType(createPackType_.Replace("包", "组"));
                CachingAddGroups = ESEditorSO.SOS.GetGroup<ISoDataGroup>(GroupType) ?? CachingAddGroups;
            }
            private void OnValueChanged_ResetConfigure()
            {
                hasChange = false;
                ESSODataInfoWindow.UsingWindow.selectPackTypeName_ = createPackType_;
                ESSODataInfoWindow.UsingWindow.ESWindow_RefreshWindow();
                createName_ = "新建" + createPackType_;
            }
            private bool TypeSelectorSettingForDataPack(Type type)
            {
                return !type.IsAbstract && !type.IsInterface && typeof(ISoDataPack).IsAssignableFrom(type);
            }
            #endregion
        }
        //子层 搜集的数据包页面
        [Serializable]
        public class Page_Index_EveryDataPack : ESWindowPageBase
        {
            [HorizontalGroup("总组")]

            [Title("配置数据包！!", "数据包引用大量数据，整合起来一次使用", titleAlignment: TitleAlignments.Centered, Title = @"@  ""配置数据包:※ 【"" + pack.FileName  + ""】""   ")]
            [VerticalGroup("总组/数据包")]

            [VerticalGroup("总组/数据包"), AssetSelector, SerializeReference, LabelText("拖入或者用按钮用于缓冲的数据组")]
            public List<ISoDataGroup> CachingGroups = new List<ISoDataGroup>();
            [DisplayAsString(fontSize: 30, TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            [VerticalGroup("总组/数据包")]
            public string showText = "--查看数据包详情--";
            [InlineEditor(Expanded = true), SerializeReference, LabelText("数据包"), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            [VerticalGroup("总组/数据包")]
            public ISoDataPack pack;

            public override ESWindowPageBase ES_Refresh()
            {
                pack.Check();
                if (pack.EnableAutoRefresh && CachingGroups != null)
                {
                    Undo.RecordObject(this.pack as ScriptableObject, "this");
                    foreach (var i in pack.CachingGroups)
                    {
                        if (i == null) continue;
                        if (i is not ScriptableObject) continue;

                        if (pack.GetSOInfoType() == i.GetSOInfoType())
                        {
                            pack._AddInfosFromGroup(i);
                        }
                        else
                        {
                            Debug.LogError("数据组" + i.FileName + "的类型不合适或者已经销毁");
                        }
                    }
                    Undo.RecordObject(this.pack as ScriptableObject, "this");
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
                return base.ES_Refresh();

            }

            #region 按钮
            [HorizontalGroup("总组", width: 100)]
            [VerticalGroup("总组/按钮组")]
            [PropertySpace(15)]
            [Button("搜集匹配数据组", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void SearchAllGroup()
            {   //找到全部数据组
                var namePack = ESSODataWindowHelper.GetPackName(pack.GetType());
                var GroupType = ESSODataWindowHelper.GetGroupType(namePack.Replace("包", "组"));
                CachingGroups = ESEditorSO.SOS.GetGroup<ISoDataGroup>(GroupType) ?? CachingGroups;
            }
            [VerticalGroup("总组/按钮组")]
            [PropertySpace(15)]
            [Button("载入数据组", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public void PushInDataGroup()
            {

                if (CachingGroups != null)
                {

                    Undo.RecordObject(this.pack as ScriptableObject, "this");
                    foreach (var i in CachingGroups)
                    {
                        if (i == null) continue;
                        if (i is not ScriptableObject) continue;

                        if (pack.GetSOInfoType() == i.GetSOInfoType())
                        {
                            pack._AddInfosFromGroup(i);
                        }
                        else
                        {
                            Debug.LogError("数据组" + i.FileName + "的类型不合适或者已经销毁");
                        }

                    }
                    Undo.RecordObject(this.pack as ScriptableObject, "this");
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                }

                /* string s = @"@  ""开始配置数据组！！ 【"" + group.name_  + ""】""   ";*/

            }
            #endregion
        }
        //数据组创建页面
        [Serializable]
        public class Page_CreateNewGroupOrSearch : ESWindowPageBase
        {
            [Title("开始新建配置数据组！", "可以先选择预设类型", bold: true, titleAlignment: TitleAlignments.Centered)]
            [HorizontalGroup("总组")]
            [DisplayAsString(fontSize: 30, TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            [VerticalGroup("总组/数据")]
            public string createText = "【创建新的数据组文件】";
            
            [InfoBox("请修改一下文件名否则会分配随机数字后缀", VisibleIf = "@!hasChange", InfoMessageType = InfoMessageType.Warning)]
            [VerticalGroup("总组/数据"), ESBackGround("yellow", 0.2f), LabelText("文件命名"), Space(5), GUIColor("@ESDesignUtility.ColorSelector.Color_04"), OnValueChanged("OnValueChanged_ChangeHappen")]
            public string createName_ = "新建数据组";
            [VerticalGroup("总组/数据")]
            [ESBoolOption("自动使用父级文件夹", "直接放到指定文件夹")]
            public bool AutoParentFolder = true;
            private bool hasChange = false;
            private void OnValueChanged_ChangeHappen()
            {
                hasChange = true;
            }
            [FolderPath, OnValueChanged("Refresh")]
            [VerticalGroup("总组/数据"), LabelText("保存文件夹"), Space(5), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public string FolderPath_ = "Assets/Resources/Data/GroupData";
            private void Refresh()
            {
                AssetDatabase.Refresh();
                ESSODataInfoWindow.UsingWindow.selectGroupTypeName_ = createGroup_;
                createText = $"--新建【{createGroup_}】--";
            }
            public override ESWindowPageBase ES_Refresh()
            {
                FolderPath_ = ESGlobalEditorDefaultConfi.Instance.Path_GroupParent;
                if (createGroup_.IsNullOrWhitespace())
                {
                    createGroup_ = ESSODataWindowHelper.GetGroupNames().First();
                }
                Refresh();
                createName_ = "新建" + createGroup_;
                return base.ES_Refresh();
              
            }
            [DisplayAsString(fontSize: 30, TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            [VerticalGroup("总组/数据")]
            public string createTypeText = "--筛选数据组类型--";


            [OnValueChanged("OnValueChanged_ResetConfigure"), InfoBox("建议选择一个预设类型的配置组,或者自己创建支持,默认类型无法直接使用", infoMessageType: InfoMessageType.Warning/*, VisibleIf = "@createGroup_==EvWindowDataAndTool.DataType.None"*/)]
            [VerticalGroup("总组/数据"), Space(5), LabelText("预定义类型")]
            [ValueDropdown("@ESSODataWindowHelper.GetGroupNames()", AppendNextDrawer = false)]
            public string createGroup_ = "";

            #region 按钮
            [HorizontalGroup("总组", width: 100)]
            [VerticalGroup("总组/按钮")]
            [PropertySpace(15)]
            [Button(ButtonHeight = 30, Name = "新建数据组"), GUIColor("@ ESDesignUtility.ColorSelector.Color_03")]
            public void CreateInfoGroupAsset()
            {
                Type targetType = ESSODataWindowHelper.GetGroupType(createGroup_);
                string path = FolderPath_;
                Debug.Log(path + targetType.Name + targetType.Name._RemoveSubStrings("Data", "Group"));
                if (AutoParentFolder) ESDesignUtility.SafeEditor.Quick_TryCreateChildFolder(FolderPath_, targetType.Name._RemoveSubStrings("Data", "Group"), out path);
                Debug.Log(path);
                var create = ESDesignUtility.SafeEditor.CreateSOAsset(targetType, path, createName_, true, hasChange, beforeSave);
                void beforeSave(ScriptableObject so)
                {
                    Selection.activeObject = so;
                }
               
            }
            [VerticalGroup("总组/按钮")]
            [PropertySpace(15)]
            [Button(ButtonHeight = 30, Name = "选中一个组"), GUIColor("@ ESDesignUtility.ColorSelector.Color_04")]
            public void FindAInfoGroupAsset()
            {
                //找到全部数据组
                var all = ESDesignUtility.SafeEditor.FindAllSOAssets<ISoDataGroup>(ESSODataWindowHelper.GetGroupType(createGroup_));
                foreach (var i in all)
                {
                    if (i is ScriptableObject so)
                    {
                        ESSODataWindowHelper.SelectSoGroup(so);
                        break;
                    }
                }
            }
            private bool TypeSelectorSettingForDataGroup(Type type)
            {
                return !type.IsAbstract && !type.IsInterface && typeof(ISoDataGroup).IsAssignableFrom(type);
            }

            private void OnValueChanged_ResetConfigure()
            {
                hasChange = false;
                ESSODataInfoWindow.UsingWindow.selectGroupTypeName_ = createGroup_;
                ESSODataInfoWindow.UsingWindow.ESWindow_RefreshWindow();
                createName_ = "新建" + createGroup_;
            }
            #endregion
        }
        //子页面 数据组快速查看
        [Serializable]
        public class Page_Index_DataQuickSeeGroup : ESWindowPageBase
        {
            [HorizontalGroup("总组", VisibleIf = "@group!=null")]
            [VerticalGroup("总组/数据组")]
            [DisplayAsString(fontSize: 30, TextAlignment.Center), GUIColor("@ESDesignUtility.ColorSelector.Color_01"), HideLabel]
            public string readme = "--双击↓SO编辑该数据组--";
            [VerticalGroup("总组/数据组")]
            [InlineEditor(Expanded = true), ReadOnly, SerializeReference, LabelText("数据组")]
            public ScriptableObject group;

            [HorizontalGroup("总组", width: 100)]
            [VerticalGroup("总组/按钮组")]
            [Button("编辑", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            private void EditThisGroup()
            {
                ESSODataWindowHelper.SelectSoGroup(group);
            }

        }
        //数据组配置页面
        [Serializable]
        public class Page_DataGroupOnChoose : ESWindowPageBase
        {

            [Title("", "数据组可以把一类数据整合集中配置和保存", titleAlignment: TitleAlignments.Centered, Title = "@ShowTitle()"), DisplayAsString, HideLabel]
            public string s = "";
            [HorizontalGroup("总组", VisibleIf = "@group!=null")]
            [VerticalGroup("总组/数据组")]
            [DisplayAsString(fontSize: 30, TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            [VerticalGroup("总组/数据组")]
            public string createText = "--创建新信息配置--";
            [InfoBox("建议修改一下键名或者信息名防止重复！", VisibleIf = "@!hasChange", InfoMessageType = InfoMessageType.Warning)]
            [InfoBox("该元素的键已经出现了！！请修改", VisibleIf = "@!(group?.NotContainsInfoKey(DataKey)??false)", InfoMessageType = InfoMessageType.Error)]
            [OnValueChanged("Change"),ESBackGround("yellow",0.2f), LabelText("数据信息的键")]
            [VerticalGroup("总组/数据组")]
            public string DataKey = "数据键";
            [OnValueChanged("Change"), LabelText("数据信息的文件名")]
            [VerticalGroup("总组/数据组")]
            public string DataFileName = "信息";


            [VerticalGroup("总组/数据组"), LabelText("复制键"), Space(5), GUIColor("@ESDesignUtility.ColorSelector.Color_04"), ValueDropdown("@group.AllKeys")
                , InlineButton("pastePersis", "持久粘贴"), InlineButton("copyPersis", "持久复制")]
            public string copyFrom = "fromInfo";

            private bool hasChange = false;
            private void Change()
            {
                var b = !(group?.NotContainsInfoKey(DataKey) ?? false);
                hasChange = true;
            }
            [VerticalGroup("总组/数据组"), LabelText("手动载入子数据"), SerializeReference]
            public List<ISoDataInfo> soInfos = new List<ISoDataInfo>();
            [DisplayAsString(fontSize: 30, TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            [VerticalGroup("总组/数据组")]
            public string showText = "--数据组详情--";
            [InlineEditor(Expanded = true), SerializeReference, LabelText("数据组")]
            [VerticalGroup("总组/数据组"), OnValueChanged("RefreshInfos")]
            public ISoDataGroup group;

            private string ShowTitle()
            {
                return group != null ? "开始配置数据组:※ 【" + group.FileName + "】" : "!!先选中要配置的数据组！!";
            }

            #region 按钮
            [HorizontalGroup("总组", width: 100)]
            [VerticalGroup("总组/按钮组")]
            [PropertySpace(15)]
            [Button("去新建或筛选组", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public void CreateNewPage()
            {
                ESSODataInfoWindow.UsingWindow.selectGroupTypeName_ = ESSODataWindowHelper.GetGroupName(group.GetType());
                if (ESSODataInfoWindow.MenuItems.TryGetValue(ESSODataInfoWindow.PageName_DataGroupCreate, out var pageItem))
                {
                    ESSODataInfoWindow.UsingWindow.MenuTree.Selection.Add(pageItem);
                }
                ESSODataInfoWindow.UsingWindow.pageForSodataGroup.createGroup_= ESSODataWindowHelper.GetGroupName(group.GetType());
            }
            [VerticalGroup("总组/按钮组")]
            [PropertySpace(15)]
            [Button("新建单元信息", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void CreateNewSoDataInfo()
            {
                Type type = group.GetSOInfoType();
                if (group.NotContainsInfoKey(DataKey))
                {
                    ScriptableObject @object = ScriptableObject.CreateInstance(type);
                    @object.name = DataFileName + DataKey + (hasChange ? "" : UnityEngine.Random.Range(0, 99999));
                    if (@object is ISoDataInfo info)
                    {
                        info.SetKey(DataKey);
                        group._TryAddInfoToDic(DataKey, @object);
                        AssetDatabase.AddObjectToAsset(@object, AssetDatabase.GetAssetPath(group as ScriptableObject));
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }
                    else
                    {
                        Debug.LogError("不合理的值或者重复键");
                    }
                }
            }

            [VerticalGroup("总组/按钮组")]
            [PropertySpace(15)]
            [Button("拷贝选定信息", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void CopyFromSoDataInfo()
            {
                var from = group.GetInfoByKey(copyFrom) as ScriptableObject;
                if (from == null || !group.GetSOInfoType().IsAssignableFrom(from.GetType()))
                {
                    if (from != null) ESDesignUtility.SafeEditor.Wrap_DisplayDialog("类型不匹配！", $"想要拷贝信息数据为{from}({from?.GetType()})" +
                        $"，但是这个类型和当前数据组{group.GetSOInfoType()}不符");
                    else ESDesignUtility.SafeEditor.Wrap_DisplayDialog("为空！", $"没有可用的拷贝源");
                    return;
                }
                if (group.NotContainsInfoKey(DataKey))
                {
                    ScriptableObject copy = ScriptableObject.Instantiate(from);
                    copy.name = DataFileName + DataKey + (hasChange ? "" : UnityEngine.Random.Range(0, 99999));
                    if (copy is ISoDataInfo ifno)
                    {
                        ifno.SetKey(DataKey);

                        group._TryAddInfoToDic(DataKey, copy);
                        AssetDatabase.AddObjectToAsset(copy, AssetDatabase.GetAssetPath(group as ScriptableObject));
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }
                    else
                    {
                        Debug.LogError("不合理的值或者重复键");
                    }
                }

            }

            private void copyPersis()
            {
                var InfoFrom = group.GetInfoByKey(copyFrom);
                var from = InfoFrom as ScriptableObject;
                if (from != null)
                {
                    ESSODataInfoWindow.CopyGroup = group as ScriptableObject;
                    ESSODataInfoWindow.guidForCopiedInfoKey = InfoFrom.GetKey();
                }
            }
            private void pastePersis()
            {
                if (ESSODataInfoWindow.CopyGroup != null)
                {
                    var groupcopy = ESSODataInfoWindow.CopyGroup as ISoDataGroup;
                    if (groupcopy == null)
                    {
                        ESDesignUtility.SafeEditor.Wrap_DisplayDialog("为空！", $"没有可用的拷贝源<数据组不存在>");
                        return;
                    }
                    var from = groupcopy.GetInfoByKey(ESSODataInfoWindow.guidForCopiedInfoKey) as ScriptableObject;
                    Debug.Log(group.GetSOInfoType().IsAssignableFrom(from?.GetType()) + ESSODataInfoWindow.guidForCopiedInfoKey);
                    if (from == null || !group.GetSOInfoType().IsAssignableFrom(from.GetType()))
                    {
                        if (from != null) ESDesignUtility.SafeEditor.Wrap_DisplayDialog("类型不匹配！", $"想要拷贝信息数据为{from}({from?.GetType()})" +
                            $"，但是这个类型和当前数据组需要的{group.GetSOInfoType()}不符");
                        else ESDesignUtility.SafeEditor.Wrap_DisplayDialog("为空！", $"没有可用的拷贝信息<数据组>" + groupcopy + "<键>" + ESSODataInfoWindow.guidForCopiedInfoKey);
                        return;
                    }
                    if (group.NotContainsInfoKey(DataKey))
                    {
                        ScriptableObject copy = ScriptableObject.Instantiate(from);
                        copy.name = DataFileName + DataKey + (hasChange ? "" : UnityEngine.Random.Range(0, 99999));
                        if (copy is ISoDataInfo info)
                        {
                            info.SetKey(DataKey);
                            group._TryAddInfoToDic(DataKey, copy);
                            AssetDatabase.AddObjectToAsset(copy, AssetDatabase.GetAssetPath(group as ScriptableObject));
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();
                        }
                        else
                        {
                            Debug.LogError("不合理的值或者重复键");
                        }
                    }
                }
            }
            public void RefreshInfos()
            {
                if (group != null)
                {
                    if (ESSODataInfoWindow.MenuItems.TryGetValue(ESSODataInfoWindow.PageName_DataGroupOnChooseEditInfo, out var item))
                    {
                        ESSODataInfoWindow.ES_RefreshWindow();
                    }
                }
            }
            public override ESWindowPageBase ES_Refresh()
            {
                if (ESSODataInfoWindow.MenuItems.TryGetValue(ESSODataInfoWindow.PageName_DataGroupOnChooseEditInfo, out var item))
                {
                    if (group == null) group = Selection.activeObject as ISoDataGroup;
                    if (group != null) item.Name = ESSODataInfoWindow.PageName_DataGroupOnChooseEditInfo + "<" + group.FileName.Replace("新建", "") + ">";
                }
                if (group != null)
                {
                    var info = group.GetInfoByKey(copyFrom);
                    if (info == null && group.AllKeys.Count > 0)
                    {
                        copyFrom = group?.AllKeys.First();
                    }
                }
                Check();
                return base.ES_Refresh();
            }



            [VerticalGroup("总组/按钮组")]
            [PropertySpace(15)]
            [Button("检查刷新", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void Check()
            {
                List<string> ToRemove = new List<string>();
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
                bool hasChange = false;
                if (group != null)
                    foreach (var i in group.AllKeys)
                    {

                        ISoDataInfo so = group.GetInfoByKey(i);
                        ScriptableObject so_ = so as ScriptableObject;
                        if (so_ != null)
                        {
                            Debug.Log("apply");
                            if (so.GetKey() != i)
                            {
                                so.SetKey(i);
                                hasChange = true;
                            }
                        }
                        else
                        {
                            Debug.Log("RemoveInfo");
                            ToRemove.Add(i);
                            hasChange = true;
                        }
                    }
                foreach (var i in ToRemove)
                {
                    group._RemoveInfoFromDic(i);
                }
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                if (hasChange)
                {
                    ESSODataInfoWindow.UsingWindow?.ESWindow_RefreshWindow();
                }

            }
            [VerticalGroup("总组/按钮组")]
            [PropertySpace(15)]
            [Button("载入子数据", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void Collect()
            {
                UnityEngine.Object ob = group as ScriptableObject;
                string groupPath = AssetDatabase.GetAssetPath(ob);

                foreach (var i in soInfos)
                {
                    ScriptableObject obd = i as ScriptableObject;
                    string soPath = AssetDatabase.GetAssetPath(obd);
                    Debug.Log(soPath);
                    if (soPath.StartsWith(groupPath))
                    {
                        group._TryAddInfoToDic(i.GetKey(), obd);
                    }
                }
            }
            #endregion
        }
        //子层 数据信息配置页面
        [Serializable]
        public class Page_Index_DataInfoSingle : ESWindowPageBase
        {

            [HorizontalGroup("总组")]
            [Title("开始配置信息数据！!", "配置单个数据应用到游戏逻辑", titleAlignment: TitleAlignments.Centered, Title = "@Title()")]

            [DisplayAsString(fontSize: 30), GUIColor("@ESDesignUtility.ColorSelector.Color_04"), HideLabel]
            [VerticalGroup("总组/数据组")]
            public string handleName = "--文件相关--";

            [VerticalGroup("总组/数据组"), LabelText("重命名文件"), GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
            public string renameFile = "新文件名";
            [VerticalGroup("总组/数据组")]
            [DisplayAsString(fontSize: 30), GUIColor("@ESDesignUtility.ColorSelector.Color_04"), HideLabel]
            public string handleSOData = "--配置数据--";
            [InlineEditor, LabelText("数据")]

            [VerticalGroup("总组/数据组"), SerializeReference]
            public ISoDataInfo data;
            [HorizontalGroup("总组", width: 100)]

            [VerticalGroup("总组/按钮组")]
            [PropertySpace(15)]
            [Button("重命名文件", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public void RenameFileThis()
            {

                var file = data as ScriptableObject;
                if (file != null)
                {
                    file.name = renameFile;
                    AssetDatabase.Refresh();
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }
            private string Title()
            {
                return "开始配置数据文件:※ 【" + data.GetKey() + "】";
            }
            [VerticalGroup("总组/按钮组")]
            [PropertySpace(15)]
            [Button("删除", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void DeleteThis()
            {
                Undo.DestroyObjectImmediate(data as ScriptableObject);
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
                ESSODataInfoWindow.UsingWindow.pageForGroupOnChoose.Check();
            }
        }
        #endregion
        #region 开始使用界面
        [Serializable]
        public class Page_Root_Data_StartUse : ESWindowPageBase
        {
            [Title("开始使用ES SO数据管理窗口！！", subtitle: "为了快速入门，我们从最简单的概念开始排列介绍和布局")]
            [DisplayAsString(alignment: TextAlignment.Center, fontSize: 20), HideLabel]
            public string titleF = "SO数据管理窗口，为SO定义，创建和修改，创建全局寻址方式，提供便捷开发工具";
            [TabGroup("操作名词", "窗口概述", TextColor = "@ESDesignUtility.ColorSelector.Color_04"), HideLabel, TextArea(5, 10), DisplayAsString(alignment: TextAlignment.Left)]
            public string aboutThisWindow = "" +
         "该窗口主要围绕So数据的游戏核心体系展开" +
          "广泛支持实体怪物，道具,技能，状态,Buff,亦可以简单自定义\n" +
         "通常提供了各种数据的创建和搜索，并且可以直接在窗口编辑\n，" +
         "其中\n******【1】So数据与多态序列化深度绑定，用尽量少的内存占用实现数据保存和功能\n" +
         " ******【2】由于多态问题，时常需要类型转换，建议使用Refer功能或者自己写首次加载\n " +
         " ******【3】更多的独立非体系So分布在Data文件夹下，这种也是可以生成和收集的\n" +
         " ******【#】GlobalData很特殊，不要滥用，建议主要使用在编辑器下即可";

            [TabGroup("操作名词", "基础操作", TextColor = "@ESDesignUtility.ColorSelector.Color_04"), HideLabel, TextArea(5, 10), DisplayAsString(alignment: TextAlignment.Left)]
            public string aboutDetail = "" +
         "" +
          "每种SO数据会分配一个中文名，在查询/创建页可以进行筛选，筛选的数据会出现在子菜单供进一步操作\n" +
         "，筛选的数据会出现在子菜单供进一步操作，并且可以替代检查器自由编辑\n，" +
         "其中\n******【1】创建新的类型建议只在窗口配置和创建\n" +
         " ******【2】窗口提供快速定位到项目功能，自己也要做好文件夹分组\n " +
         " ******【3】删除元素建议只在窗口进行，否则自己去进行手动刷新之类的\n" +
         "";
            [TabGroup("概念", "关于数据信息", TextColor = "@ESDesignUtility.ColorSelector.Color_04"), HideLabel, TextArea(5, 10), DisplayAsString(alignment: TextAlignment.Left)]
            public string aboutDataInfo = "" +
         "数据信息，存放一个类型的单个数据，他是作为子资产进入(数据组)的！\n" +
          "数据信息存放单个的独立数据，比如一个怪物，一个飞行物，一个技能\n" +
         "在没有资源更新需求下，可以考虑直接引用数据信息，他直接作为文件用代码加载步骤略多\n，" +
         "其中\n******【1】数据信息是一个数据组的子资产，因为共性被整合，每个有独立的完整数据\n" +
         " ******【2】数据信息根据作用的对象编写大量数据内容\n " +
         " ******【3】英文Info,为它的专属名\n" +
         " ******【#】数据信息还可以实现共享与变量体系，";


            [TabGroup("概念", "关于数据组", TextColor = "@ESDesignUtility.ColorSelector.Color_04"), HideLabel, TextArea(5, 10), DisplayAsString(alignment: TextAlignment.Left)]
            public string aboutDataGroup = "" +
         "数据组，数据组把数个具有同特征的数据信息包含其中，作为独立的资产的最小格式！\n" +
          "组最大的作用是分组，以一个资产包含多个子信息\n" +
         " 通常来说不推荐直接把组用来引用至游戏，而是以后面的包来完成\n，" +
         "其中\n******【1】数据组是一个数据信息的持久字典，并且原则上把子信息都作为子资产包含其中，推荐容纳5-10个为佳\n" +
         " ******【2】数据组一般只有分组和编辑功能，不推荐用于游戏引用，加载和取用，这只是一个建议和规范，可以自己定\n " +
         " ******【3】英文Group,为它的专属名词\n" +
         " ******【#】以一个资产容纳一系列数据信息,高效分类整理，可以绑定到数据包来做到输出最新的内容";

            [TabGroup("概念", "关于数据包", TextColor = "@ESDesignUtility.ColorSelector.Color_04"), HideLabel, TextArea(5, 10), DisplayAsString(alignment: TextAlignment.Left)]
            public string aboutDataPack = "" +
           "数据包，同样以持久字典直接引用一些数据信息，跳过了数据组，一般来说一个包可以涵盖一套功能的基本结构！\n" +
            "包并不推荐为数据信息重命名,而是一般简单地从多个数据组缓冲入数据，他主要是为了能快速收集足够有效数据\n" +
           " 他的数据组主要有一个信息更新关系,，在游戏运行时，不推荐从包获得组再进行操作\n，" +
           "其中\n******【1】包只是一类数据信息引用的持久字典\n" +
           " ******【2】包可以选定和数据组建立更新链接，以便防止忘记手动载入\n " +
           " ******【3】英文Pack,为它的专属名词\n" +
           " ******【#】建议广泛使用包来简化游戏逻辑流程";

            [TabGroup("概念", "关于代码生成工具", TextColor = "@ESDesignUtility.ColorSelector.Color_04"), HideLabel, TextArea(5, 10), DisplayAsString(alignment: TextAlignment.Left)]
            public string aboutCodeGen = "" +
                "SO代码生成工具用于自动化创建C# Scriptable 文件\n" +
                "用来简化类似结构代码的声明工作量,\n" +
                "其中\n******【1】So体系生成,提供从So信息，组，包 的一键构建\n" +
                "******【2】So全局配置文件生成,这种配置文件可以被轻易地引用并且提供了自动创建引导和多配置选用\n " +
                "******【3】SharedData 与 VariableData 体系，是So游戏逻辑数据的一个标准，生成的代码主要为了指引深拷贝优化\n" +
                "******【#】这里是关于SO的数据生成，还有更多代码生成属于其他功能模块！";

        }
        #endregion
        #region 关于
        //关于页面
        [Serializable]
        public class Page_About : ESWindowPageBase
        {
            [Title("欢迎使用ES开发框架工具包")]
            [LabelText("版本号"), ShowInInspector, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string ToolVersion => "ES 0.4 测试版本";
            [LabelText("插件依赖"), ShowInInspector, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string PluginsDependence => "Odin插件,Dotween插件,FishNet插件，和一些库";
            [PropertySpace(15)]
            [Title("开发者", "工具开发者相关信息", titleAlignment: TitleAlignments.Split)]
            [LabelText("开发者团队"), ShowInInspector, GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
            public string DeveloperTeam => "Ey Sive企划";
            [LabelText("开发者名称"), ShowInInspector, GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
            public string DeveloperName => "超级依薇尔";
            [LabelText("开发者QQ"), ShowInInspector, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string DeveloperQQ => "2650026906";
            [LabelText("开发者邮箱"), ShowInInspector, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string DeveloperEmail => "2650026906@qq.com";
            [TitleGroup("最后的话", "来自超级依薇尔", alignment: TitleAlignments.Split), HideLabel, PropertyOrder(3), ReadOnly, TextArea(5, 10)]
            public string WordsOnEnd = "我是哔哩哔哩的超级依薇尔，欢迎关注我，另外我创建了QQ交流群982703564\n" +
                "欢迎前来进行Unity技术交流讨论和分享自己的作品和开发见解。\n" +
                "本框架特别感谢 凉鞋大佬(Q FrameWork作者)\n" + ""
                ;
        }
        #endregion
        #region 辅助
        //数据源和辅助工具
        [InitializeOnLoad]
        public static class ESSODataWindowHelper
        {
            static ESSODataWindowHelper()
            {
                Selection.selectionChanged += ForDataWindowSelection;
            }
            private static void ForDataWindowSelection()
            {
                if (Selection.activeObject is ISoDataGroup group)
                {
                    var page = ESSODataInfoWindow.UsingWindow?.pageForGroupOnChoose;
                    if (page == null || ESSODataInfoWindow.UsingWindow.MenuTree == null)
                    {
                        ESSODataInfoWindow.OpenWindow();
                        Debug.Log("自动打开数据窗口");
                        page = ESSODataInfoWindow.UsingWindow.pageForGroupOnChoose;
                    }
                   
                    if (page != null && ESSODataInfoWindow.menuTree != null)
                    {
                        page.group = group;
                        page.RefreshInfos();
                        SelectSoGroup(group as ScriptableObject);
                        
                       /* if (ESSODataInfoWindow.MenuItems.TryGetValue(ESSODataInfoWindow.PageName_DataGroupOnChooseEditInfo, out var item))
                        {
                            item.Name = ESSODataInfoWindow.PageName_DataGroupOnChooseEditInfo + "<" + group.FileName.Replace("新建", "") + ">";
                            ESSODataInfoWindow.menuTree.Selection.Add(item);
                        }*/
                    }
                }
            }
            public static void SelectSoGroup(ScriptableObject so)
            {
                Selection.activeObject = so;
                Debug.Log("AAA"+ ESSODataInfoWindow.MenuItems);
                if (ESSODataInfoWindow.MenuItems.TryGetValue(ESSODataInfoWindow.PageName_DataGroupOnChooseEditInfo, out var pageItem))
                {
                    ESSODataInfoWindow.UsingWindow.MenuTree.Selection.Add(pageItem);
                }
            }
            #region 简单方法
            public static string[] GetInfoNames()
            {
                return ESDesignUtility.Creator.CreatePaths.GetKeys("数据信息");
            }
            public static string[] GetGroupNames()
            {
                return ESDesignUtility.Creator.CreatePaths.GetKeys("数据组");
            }
            public static string[] GetPackNames()
            {
                return ESDesignUtility.Creator.CreatePaths.GetKeys("数据包");
            }
            public static Type GetInfoType(string name)
            {
                return ESDesignUtility.Creator.CreatePaths.GetElement("数据信息", name);
            }
            public static Type GetGroupType(string name)
            {
                return ESDesignUtility.Creator.CreatePaths.GetElement("数据组", name);
            }
            public static Type GetPackType(string name)
            {
                return ESDesignUtility.Creator.CreatePaths.GetElement("数据包", name);
            }

            public static string GetPackName(Type name)
            {
                var dic = ESDesignUtility.Creator.CreatePaths.GetDic("数据包");
                foreach(var i in dic)
                {
                    if (i.Value == name)
                    {
                        return i.Key;
                    }
                }
                return "";
            }
            public static string GetGroupName(Type name)
            {
                var dic = ESDesignUtility.Creator.CreatePaths.GetDic("数据组");
                foreach (var i in dic)
                {
                    if (i.Value == name)
                    {
                        return i.Key;
                    }
                }
                return "";
            }
            public static string GetInfoName(Type name)
            {
                var dic = ESDesignUtility.Creator.CreatePaths.GetDic("数据信息");
                foreach (var i in dic)
                {
                    if (i.Value == name)
                    {
                        return i.Key;
                    }
                }
                return "";
            }
            public static bool HasNull(params UnityEngine.Object[] objects)
            {
                if (objects == null) return true;
                foreach (var i in objects)
                {
                    if (i == null) return true;
                }
                return false;
            }
            public static bool AllIsOk(params UnityEngine.Object[] objects)
            {
                if (objects == null) return false;
                foreach (var i in objects)
                {
                    if (i == null) return false;
                }
                return true;
            }
            #endregion
        }

        #endregion
    }

}

