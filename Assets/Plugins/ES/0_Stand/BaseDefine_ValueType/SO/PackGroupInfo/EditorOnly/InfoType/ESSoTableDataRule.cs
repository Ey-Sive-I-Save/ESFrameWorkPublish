#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public enum ESTableFileKind
    {
        [LabelText("CSV")]
        Csv,

        [LabelText("XLSX")]
        Xlsx,

        [LabelText("CSV 闂?XLSX")]
        CsvAndXlsx
    }

    public enum ESSoTableRuleDirection
    {
        [LabelText("")]
        Both,

        [LabelText("")]
        SoToTableOnly,

        [LabelText("婵炲濮撮幊鎾诲Υ閸愵喖鍐€闁绘挸楠搁悡?SO")]
        TableToSoOnly,

        [LabelText("")]
        Ignore
    }

    public enum ESTableColumnAvailability
    {
        [Tooltip("跟随本行的启用开关。")]
        [LabelText("普通")]
        Normal,

        [Tooltip("即使启用开关关闭，也参与导入导出。")]
        [LabelText("强制启用")]
        ForceEnabled,

        [Tooltip("无论启用开关如何，都不参与导入导出。")]
        [LabelText("强制禁用")]
        ForceDisabled
    }

    public enum ESTableColumnNameMode
    {
        [LabelText("英文列名")]
        English,

        [LabelText("中文显示名")]
        Chinese
    }

    public enum ESTableBatchApplyRangeMode
    {
        [LabelText("全量应用")]
        All,

        [LabelText("片段截取")]
        Slice,

        [LabelText("单个 Group/Info")]
        SingleGroupInfo
    }

    public enum ESTableColumnAuthority
    {
        [LabelText("表格权威")]
        [Tooltip("导入时表格值覆盖 SO 字段。")]
        TableAuthority,

        [LabelText("SO权威")]
        [Tooltip("SO 字段已有内容时，表格不能覆盖；SO 为空时允许表格补值。")]
        SoAuthority,

        [LabelText("忽略")]
        [Tooltip("保留映射记录，但导入导出都不使用。")]
        Ignore
    }

    public enum ESTableInfoExpandMode
    {
        [LabelText("")]
        ExplicitMappingsOnly,

        [LabelText("")]
        SerializedFields,

        [LabelText("")]
        NestedObjectColumns,

        [LabelText("婵犮垼娉涚粔闈涱焽閸涱垪鍋撻悽娈挎敯闁芥牕瀚粚鍗炩攽閸℃瑦鎲兼繛?Json")]
        ComplexObjectAsJson
    }

    public enum ESTableGroupSliceMode
    {
        [LabelText("闂婎偄娲ㄩ弲顐﹀汲?Group")]
        IgnoreGroup,

        [LabelText("")]
        GroupNameColumn,

        [LabelText("濠殿噯绲界换瀣煂?Group 婵炴垶鎸撮崑鎾斥槈?Sheet")]
        OneGroupPerSheet,

        [LabelText("")]
        OneGroupPerFile
    }

    public enum ESTableNameMatchMode
    {
        [LabelText("")]
        Exact,

        [LabelText("")]
        IgnoreCase,

        [LabelText("")]
        FieldToColumn,

        [LabelText("")]
        Custom
    }

    public enum ESTableValueWriteMode
    {
        [LabelText("")]
        PlainValue,

        [LabelText("Unity 闁诲海鏁搁、濠囨寘?GUID")]
        UnityObjectGuid,

        [LabelText("")]
        UnityObjectPath,

        [LabelText("Json")]
        Json,

        [LabelText("")]
        TypeName
    }

    public enum ESTableConflictPolicy
    {
        [LabelText("")]
        Skip,

        [LabelText("")]
        Overwrite,

        [LabelText("")]
        CreateCopy,

        [LabelText("")]
        Error
    }

    public enum ESSoTableRuleUseDirection
    {
        [LabelText("")]
        Export,

        [LabelText("")]
        Import,

        [LabelText("")]
        ImportAndExport
    }

    public enum ESSoTableRuleBindSourceKind
    {
        [LabelText("")]
        None,

        [LabelText("")]
        SoAsset,

        [LabelText("")]
        SoFolder,

        [LabelText("闂佺厧鐡ㄧ喊宥咃耿?MonoScript")]
        MonoScript
    }

    public enum ESSoTableRuleObjectKind
    {
        [LabelText("SoData Pack/Group/Info")]
        SoData,

        [LabelText("闂佸搫鎷嬮崳锝夊焵?ScriptableObject")]
        ScriptableObject
    }

    public enum ESTableFolderSyncMode
    {
        [LabelText("")]
        CompareOnly,

        [LabelText("")]
        Incremental,

        [LabelText("")]
        Rebuild
    }

    [Serializable]
    [InlineProperty]
    [HideReferenceObjectPicker]
    public sealed class ESTableRowBindingRule : ESRowBindingRule
    {
    }

    [Serializable]
    [InlineProperty]
    [HideReferenceObjectPicker]
    public sealed class ESTableNestedFieldRule
    {
        [LabelText("")]
        public bool expandNestedFields = true;

        [LabelText("")]
        [MinValue(0)]
        public int maxDepth = 1;

        [LabelText("")]
        public string columnSeparator = "_";
    }

    [Serializable]
    [InlineProperty]
    [HideReferenceObjectPicker]
    public sealed class ESTableColumnNameMap
    {
        [LabelText("启用")]
        [PropertyTooltip("普通状态下是否参与导入导出。强制启用/禁用会覆盖这个开关。")]
        [TableColumnWidth(42, Resizable = false)]
        [PropertyOrder(-1)]
        [HideLabel]
        [ToggleLeft]
        public bool enabled = true;

        [LabelText("锁定")]
        [PropertyTooltip("锁定后不会被重建规则删除，也不会在导入时被表格写回覆盖。")]
        public bool locked;

        [LabelText("字段权威")]
        [PropertyTooltip("表格权威：导入覆盖 SO；SO权威：SO 有值时不被表格覆盖；忽略：保留记录但不参与导入导出。")]
        public ESTableColumnAuthority authority = ESTableColumnAuthority.TableAuthority;

        [LabelText("详情")]
        [PropertyTooltip("打开后显示写入方式、Key 标记、说明、表格类型和方向等低频字段。")]
        public bool showDetail;

        [LabelText("SO 字段名")]
        [TableColumnWidth(220, Resizable = true)]
        [PropertyOrder(0)]
        [PropertyTooltip("SO 对象里的字段路径，例如 name 或 config.value。")]
        public string soFieldPath;

        [LabelText("写入方式")]
        [TableColumnWidth(110, Resizable = false)]
        [PropertyOrder(1)]
        [PropertyTooltip("控制导入时如何把表格文本写回 SO 字段。")]
        public ESTableValueWriteMode valueWriteMode = ESTableValueWriteMode.PlainValue;

        [LabelText("InfoKey")]
        [TableColumnWidth(58, Resizable = false)]
        [PropertyOrder(2)]
        [PropertyTooltip("该列作为 Info 的唯一标识列。")]
        public bool isInfoKey;

        [LabelText("GroupKey")]
        [TableColumnWidth(72, Resizable = false)]
        [PropertyOrder(3)]
        [PropertyTooltip("该列作为 Group 分组标识列。")]
        public bool isGroupKey;

        [LabelText("SO 显示名")]
        [TableColumnWidth(140, Resizable = true)]
        [PropertyOrder(4)]
        [PropertyTooltip("编辑器里给人看的名字，不要求和字段名一致。")]
        public string displayName;

        [LabelText("说明")]
        [TableColumnWidth(240, Resizable = true)]
        [TextArea(1, 2)]
        [PropertyOrder(5)]
        [PropertyTooltip("写入表头注释行的说明文字。")]
        public string comment;

        [LabelText("表格列名")]
        [TableColumnWidth(190, Resizable = true)]
        [PropertyOrder(6)]
        [PropertyTooltip("导出到 CSV/XLSX 的列名，也是导入时匹配列的名字。")]
        public string columnName;

        [LabelText("表格类型")]
        [TableColumnWidth(105, Resizable = false)]
        [PropertyOrder(7)]
        [PropertyTooltip("写入 表格表头类型行的类型名。")]
        public string tableType;

        [LabelText("方向")]
        [TableColumnWidth(105, Resizable = false)]
        [PropertyOrder(8)]
        [PropertyTooltip("控制该字段参与导出、导入、双向，或完全忽略。")]
        public ESSoTableRuleDirection direction = ESSoTableRuleDirection.Both;

        [LabelText("可用状态")]
        [PropertyTooltip("普通表示跟随启用开关；强制启用/强制禁用用于锁定关键字段。")]
        public ESTableColumnAvailability availability = ESTableColumnAvailability.Normal;

        [LabelText("保留未映射列")]
        [PropertyTooltip("导出时尽量把旧表格里没有映射到 SO 字段的列追加保留下来，适合手工维护的备注列。")]
        public bool allowPassThrough;

        public bool IsIgnored => authority == ESTableColumnAuthority.Ignore || direction == ESSoTableRuleDirection.Ignore;
        public bool IsUsable => !IsIgnored && (availability == ESTableColumnAvailability.ForceEnabled || (availability == ESTableColumnAvailability.Normal && enabled));
    }

    public sealed class ESTableCompiledColumn
    {
        public int tableColumnIndex;
        public ESTableColumnNameMap map;
        public Type ownerType;
        public string memberPath;
        public Type valueType;
        public bool useRowObject;
        public bool canRead;
        public bool canWrite;
    }

    public sealed class ESTableBatchApplyFilter
    {
        public ESTableBatchApplyRangeMode mode;
        public int sliceColumnIndex = -1;
        public int groupColumnIndex = -1;
        public int infoColumnIndex = -1;
        public string startValue;
        public string endValue;
        public bool includeStart;
        public bool includeEnd;
        public bool sliceStarted;
        public bool sliceFinished;
        public string targetGroupKey;
        public string targetInfoKey;
    }

    [Serializable]
    public sealed class ESTableHeaderLayout
    {
        [LabelText("")]
        public string varMark = "##var";

        [LabelText("")]
        public string typeMark = "##type";

        [LabelText("")]
        public string groupMark = "##group";

        [LabelText("")]
        public string commentMark = "##";

        [LabelText("")]
        public string defaultGroup = "client,server,editor";
    }

    [Serializable]
    public sealed class ESSoTableRuleTypeBinding
    {
        [FoldoutGroup("", Expanded = false)]
        [LabelText("")]
        [ReadOnly]
        public ESSoTableRuleObjectKind objectKind;

        [FoldoutGroup("")]
        [LabelText("")]
        [ReadOnly]
        public string objectTypeName;

        [FoldoutGroup("")]
        [LabelText("")]
        [ReadOnly]
        public string packTypeName;

        [FoldoutGroup("")]
        [LabelText("")]
        [ReadOnly]
        public string groupTypeName;

        [FoldoutGroup("")]
        [LabelText("")]
        [ReadOnly]
        public string infoTypeName;

        public Type PackType => ESSoTableRuleTypeUtility.FindType(packTypeName);
        public Type GroupType => ESSoTableRuleTypeUtility.FindType(groupTypeName);
        public Type InfoType => ESSoTableRuleTypeUtility.FindType(infoTypeName);
        public Type ObjectType => ESSoTableRuleTypeUtility.FindType(objectTypeName);
        public Type RowOwnerType => objectKind == ESSoTableRuleObjectKind.SoData ? InfoType : ObjectType;
    }

    [Serializable]
    [InlineProperty]
    [HideReferenceObjectPicker]
    public sealed class ESSoTableRuleSourceBinding
    {
        [FoldoutGroup("", Expanded = false)]
        [LabelText("")]
        [ReadOnly]
        public ESSoTableRuleBindSourceKind sourceKind;

        [TitleGroup("")]
        [LabelText("")]
        [AssetsOnly]
        public ScriptableObject soAsset;

        [TitleGroup("")]
        [LabelText("")]
        [AssetsOnly]
        public DefaultAsset soFolder;

        [TitleGroup("")]
        [LabelText("")]
        public bool includeSubFolders = true;

        [TitleGroup("")]
        [LabelText("")]
        [AssetsOnly]
        public MonoScript monoScript;

        [FoldoutGroup("")]
        [LabelText("")]
        [ReadOnly]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = false, ShowIndexLabels = true)]
        public List<ScriptableObject> folderAssets = new List<ScriptableObject>();

        [FoldoutGroup("", Expanded = false)]
        [LabelText("")]
        public ESTableFolderSyncMode folderSyncMode = ESTableFolderSyncMode.Incremental;

        [FoldoutGroup("")]
        [LabelText("闂佹眹鍨婚崰鎰板垂濮樿京纾介柛婵嗗娴?SO")]
        public bool createMissingAssetsInFolder = true;

        [FoldoutGroup("")]
        [LabelText("闂佸搫娲ら悺銊╁蓟婵犲偆鍟呴柛娆忣槹缁?SO")]
        public bool updateExistingAssetsInFolder = true;

        [FoldoutGroup("")]
        [LabelText("闂佸搫顦崕鑼姳?GUID")]
        [ReadOnly]
        public string sourceGuid;

        [FoldoutGroup("")]
        [LabelText("")]
        [ReadOnly]
        public string sourcePath;

        [FoldoutGroup("")]
        [LabelText("")]
        [ReadOnly]
        public string sourceTypeName;

        public void Capture(ESSoTableRuleBindSourceKind kind, UnityEngine.Object source, Type sourceType)
        {
            sourceKind = kind;
            sourceTypeName = sourceType != null ? sourceType.FullName : string.Empty;
            sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
            sourceGuid = string.IsNullOrEmpty(sourcePath) ? string.Empty : AssetDatabase.AssetPathToGUID(sourcePath);
        }
    }

    [Serializable]
    [InlineProperty]
    [HideReferenceObjectPicker]
    public sealed class ESSoTableRuleBuildStage
    {
        [LabelText("构建来源")]
        [PropertyTooltip("只用于生成或重建 Rule 字段映射，不用于批量导入导出。")]
        [HideLabel]
        public ESSoTableRuleSourceBinding sourceBinding = new ESSoTableRuleSourceBinding();

        [LabelText("表格样本路径")]
        [PropertyTooltip("拖入或选择 CSV/XLSX，用它的表头生成字段映射。")]
        [Sirenix.OdinInspector.FilePath(Extensions = "csv,xlsx", AbsolutePath = true)]
        public string tableFilePath;

        [LabelText("允许表头覆盖")]
        [PropertyTooltip("从表格样本重建时，允许表头结果覆盖现有字段映射。")]
        public bool allowTableHeaderOverride = true;
    }

    [Serializable]
    [InlineProperty]
    [HideReferenceObjectPicker]
    public sealed class ESSoTableRuleUseBatch
    {
        [LabelText("启用")]
        [PropertyTooltip("关闭后执行全部批次时会跳过这一批。")]
        public bool enabled = true;

        [LabelText("批次名")]
        [PropertyTooltip("给这一批导入导出配置起一个容易识别的名字。")]
        public string batchName = "New Batch";

        [LabelText("执行方向")]
        [PropertyTooltip("控制这一批是导出、导入，还是先导入再导出。")]
        public ESSoTableRuleUseDirection direction = ESSoTableRuleUseDirection.Export;

        [LabelText("数据来源")]
        [PropertyTooltip("这一批要处理的单个 SO 或 SO 文件夹。")]
        [HideLabel]
        public ESSoTableRuleSourceBinding sourceBinding = new ESSoTableRuleSourceBinding();

        [LabelText("文件格式")]
        [PropertyTooltip("这一批输出或读取的表格格式。")]
        public ESTableFileKind fileKind = ESTableFileKind.CsvAndXlsx;

        [LabelText("表格列名")]
        [PropertyTooltip("选择导出表头和导入匹配时使用英文列名，还是字段的中文显示名。")]
        public ESTableColumnNameMode columnNameMode = ESTableColumnNameMode.English;

        [LabelText("文件名")]
        [PropertyTooltip("不带扩展名的 CSV/XLSX 文件名。")]
        public string fileName;

        [LabelText("Sheet 名")]
        [PropertyTooltip("XLSX 工作表名。CSV 不使用。")]
        public string sheetName;

        [LabelText("输出根目录")]
        [PropertyTooltip("相对项目根目录的表格输出目录。")]
        [FolderPath(AbsolutePath = false)]
        public string outputRoot = "SoTableConfig/Tables";

        [LabelText("CSV 相对路径")]
        [PropertyTooltip("CSV 相对输出根目录的子路径。")]
        [FolderPath(AbsolutePath = false)]
        public string csvRelativePath = "csv";

        [LabelText("XLSX 相对路径")]
        [PropertyTooltip("XLSX 相对输出根目录的子路径。")]
        [FolderPath(AbsolutePath = false)]
        public string xlsxRelativePath = "xlsx";

        [LabelText("导入冲突")]
        [PropertyTooltip("导入表格写回 SO 时，遇到已有对象或冲突数据的处理策略。")]
        public ESTableConflictPolicy importConflictPolicy = ESTableConflictPolicy.Overwrite;

        [LabelText("导出冲突")]
        [PropertyTooltip("导出表格时，目标文件已存在的处理策略。")]
        public ESTableConflictPolicy exportConflictPolicy = ESTableConflictPolicy.Overwrite;

        [LabelText("应用范围")]
        [PropertyTooltip("导入写回时使用全量、片段截取，或仅写回某个 Group 下的某个 Info。")]
        public ESTableBatchApplyRangeMode applyRangeMode = ESTableBatchApplyRangeMode.All;

        [LabelText("截取列名")]
        [PropertyTooltip("片段截取用的列名，可以是 Key 或其他表格列名。留空时使用 Info Key。")]
        public string sliceColumnName;

        [LabelText("起点值")]
        [PropertyTooltip("片段截取起点。找到该值后开始应用。")]
        public string sliceStartValue;

        [LabelText("终点值")]
        [PropertyTooltip("片段截取终点。找到该值后停止应用。")]
        public string sliceEndValue;

        [LabelText("包含起点")]
        public bool includeSliceStart = true;

        [LabelText("包含终点")]
        public bool includeSliceEnd = true;

        [LabelText("目标 Group")]
        [PropertyTooltip("仅应用单个 Group/Info 时匹配的 Group 列值。")]
        public string targetGroupKey;

        [LabelText("目标 Info")]
        [PropertyTooltip("仅应用单个 Group/Info 时匹配的 Info Key。")]
        public string targetInfoKey;
    }

    [ESCreatePath("数据信息", "SO表格规则数据信息")]
    public class ESSoTableDataRule : SoDataInfo
    {
        private static readonly ESReflectionRowBridge RowBridge = new ESReflectionRowBridge();
        private static readonly JsonSerializerSettings CellJsonSettings = new JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.None,
            Converters = new List<JsonConverter> { new StringEnumConverter() }
        };
        [NonSerialized]
        private ESSoTableRuleUseBatch activeUseBatch;

        [TitleGroup("")]
        [HorizontalGroup("", Width = 80)]
        [LabelText("")]
        public bool enabled = true;

        [HorizontalGroup("")]
        [LabelText("闁荤喐鐟ョ€氼剟宕?Key")]
        public string ruleKey;

        [TitleGroup("")]
        [LabelText("")]
        [TextArea(2, 5)]
        public string description;

        [FoldoutGroup("", Expanded = false)]
        [HideLabel]
        public ESSoTableRuleTypeBinding typeBinding = new ESSoTableRuleTypeBinding();

        [HideLabel]
        [PropertyOrder(8)]
        public ESSoTableRuleBuildStage buildStage = new ESSoTableRuleBuildStage();

        [LabelText("")]
        [PropertyOrder(12)]
        public List<ESSoTableRuleUseBatch> useBatches = new List<ESSoTableRuleUseBatch>();

        [FoldoutGroup("", Expanded = false)]
        [ReadOnly]
        [LabelText("")]
        public string tableName;

        [FoldoutGroup("")]
        [ReadOnly]
        [LabelText("")]
        public string beanName;

        [FoldoutGroup("婵°倕鍊归…鍥殽?Pack Group Info", Expanded = false)]
        [LabelText("Info 闁诲繒鍋炲ú鏍閹达箑妫橀柣鐔稿绾偓")]
        public ESTableInfoExpandMode infoExpandMode = ESTableInfoExpandMode.ExplicitMappingsOnly;

        [FoldoutGroup("婵°倕鍊归…鍥殽?Pack Group Info")]
        [LabelText("Group 闂佽鎯屾禍婊嗐亹閸ヮ剙妫橀柣鐔稿绾偓")]
        public ESTableGroupSliceMode groupSliceMode = ESTableGroupSliceMode.GroupNameColumn;

        [FoldoutGroup("婵°倕鍊归…鍥殽?Pack Group Info")]
        [LabelText("")]
        public string packColumnName = "pack";

        [FoldoutGroup("婵°倕鍊归…鍥殽?Pack Group Info")]
        [LabelText("")]
        public string groupColumnName = "group";

        [FoldoutGroup("婵°倕鍊归…鍥殽?Pack Group Info")]
        [LabelText("")]
        public string infoKeyColumnName = "key";

        [FoldoutGroup("婵°倕鍊归…鍥殽?Pack Group Info")]
        [LabelText("")]
        public ESTableNameMatchMode nameMatchMode = ESTableNameMatchMode.Exact;

        [FoldoutGroup("", Expanded = false)]
        [InfoBox("")]
        [HideLabel]
        public ESTableRowBindingRule rowBinding = new ESTableRowBindingRule();

        [FoldoutGroup("", Expanded = false)]
        [InfoBox("")]
        [HideLabel]
        public ESTableNestedFieldRule nestedFieldRule = new ESTableNestedFieldRule();

        [FoldoutGroup("", Expanded = false)]
        [HideLabel]
        public ESTableHeaderLayout header = new ESTableHeaderLayout();

        [TitleGroup("")]
        [LabelText("")]
        [InfoBox("")]
        [TableList(AlwaysExpanded = true, ShowIndexLabels = true, DrawScrollView = true, MinScrollViewHeight = 180)]
        public List<ESTableColumnNameMap> columns = new List<ESTableColumnNameMap>();

        [FoldoutGroup("婵°倕鍊归…鍥殽?闁诲海鏁搁崢褔宕ｉ崱娆屽亾閻㈤潧甯堕柛銈庡幘缁敻寮介锝嗩吅")]
        [LabelText("闁诲海鏁搁崢褔宕ｉ崱娑樼睄閻犲搫鎼敮鎺楁偣娴ｄ警鐒鹃柛銊ュ船椤?Info")]
        public bool allowCreateInfoOnImport = true;

        [FoldoutGroup("婵°倕鍊归…鍥殽?闁诲海鏁搁崢褔宕ｉ崱娆屽亾閻㈤潧甯堕柛銈庡幘缁敻寮介锝嗩吅")]
        [LabelText("闁诲海鏁搁崢褔宕ｉ崱娑樼睄閻犲搫鎼敮鎺楁偣娴ｄ警鐒鹃柛銊ュ船椤?Group")]
        public bool allowCreateGroupOnImport = true;

        [FoldoutGroup("婵°倕鍊归…鍥殽?闁诲海鏁搁崢褔宕ｉ崱娆屽亾閻㈤潧甯堕柛銈庡幘缁敻寮介锝嗩吅")]
        [LabelText("")]
        public bool refreshPackBeforeExport = true;

        private ESSoTableRuleSourceBinding BuildSourceBinding => buildStage != null ? buildStage.sourceBinding : null;

        public bool TryGetTargetTypes(out Type packType, out Type groupType, out Type infoType)
        {
            packType = typeBinding.PackType;
            groupType = typeBinding.GroupType;
            infoType = typeBinding.InfoType;
            return packType != null && groupType != null && infoType != null;
        }

        [TitleGroup("")]
        [HorizontalGroup("")]
        [PropertyOrder(11)]
        [Button("SO绑定生成")]
        public void BindAndGenerateFromSoAsset()
        {
            ESSoTableRuleSourceBinding buildSource = BuildSourceBinding;
            if (buildSource == null || buildSource.soAsset == null)
            {
                Debug.LogWarning("构建阶段未指定单个 SO 样本。", this);
                return;
            }

            BindAndGenerate(buildSource.soAsset.GetType(), ESSoTableRuleBindSourceKind.SoAsset, buildSource.soAsset);
        }

        [TitleGroup("")]
        [HorizontalGroup("")]
        [PropertyOrder(11)]
        [Button("脚本绑定生成")]
        public void BindAndGenerateFromMonoScript()
        {
            ESSoTableRuleSourceBinding buildSource = BuildSourceBinding;
            if (buildSource == null || buildSource.monoScript == null)
            {
                Debug.LogWarning("构建阶段未指定脚本类型。", this);
                return;
            }

            Type scriptType = buildSource.monoScript.GetClass();
            if (scriptType == null)
            {
                Debug.LogWarning("脚本没有可绑定的类型。", this);
                return;
            }

            BindAndGenerate(scriptType, ESSoTableRuleBindSourceKind.MonoScript, buildSource.monoScript);
        }

        [TitleGroup("")]
        [HorizontalGroup("")]
        [PropertyOrder(11)]
        [Button("文件夹绑定生成")]
        public void BindAndGenerateFromFolder()
        {
            ESSoTableRuleSourceBinding buildSource = BuildSourceBinding;
            if (buildSource == null || buildSource.soFolder == null)
            {
                Debug.LogWarning("构建阶段未指定文件夹。当前界面建议把文件夹放到使用阶段批次。", this);
                return;
            }

            List<ScriptableObject> assets = CollectScriptableObjectsFromFolder(buildSource.soFolder, buildSource.includeSubFolders);
            buildSource.folderAssets.Clear();
            buildSource.folderAssets.AddRange(assets);

            if (assets.Count == 0)
            {
                Debug.LogWarning("文件夹里没有可用的 ScriptableObject。", this);
                EditorUtility.SetDirty(this);
                return;
            }

            Type sourceType = buildSource.monoScript != null ? buildSource.monoScript.GetClass() : assets[0].GetType();
            if (sourceType == null)
            {
                Debug.LogWarning("无法解析绑定类型。", this);
                return;
            }

            int matchedCount = CountAssignableAssets(assets, sourceType);
            if (matchedCount != assets.Count)
                Debug.LogWarning("文件夹内存在类型不匹配的 SO，构建规则时只会按匹配类型处理。", this);

            BindAndGenerate(sourceType, ESSoTableRuleBindSourceKind.SoFolder, buildSource.soFolder);
        }

        [TitleGroup("")]
        [HorizontalGroup("")]
        [PropertyOrder(11)]
        [Button("当前选择生成")]
        public void BindAndGenerateFromSelection()
        {
            UnityEngine.Object active = Selection.activeObject;
            ESSoTableRuleSourceBinding buildSource = BuildSourceBinding;
            if (buildSource == null)
                return;

            if (active is MonoScript script)
            {
                buildSource.monoScript = script;
                BindAndGenerateFromMonoScript();
                return;
            }

            if (active is ScriptableObject so)
            {
                buildSource.soAsset = so;
                BindAndGenerateFromSoAsset();
                return;
            }

            Debug.LogWarning("\u89c4\u5219\u6784\u5efa\u9636\u6bb5\u53ea\u63a5\u53d7\u5355\u4e2a SO \u6216 MonoScript\u3002\u6587\u4ef6\u5939\u8bf7\u653e\u5230\u4f7f\u7528\u9636\u6bb5\u7684\u6279\u6b21\u91cc\u3002", this);
        }
        public bool IsValidForSoData(out string reason)
        {
            reason = string.Empty;

            if (!TryGetTargetTypes(out Type packType, out Type groupType, out Type infoType))
            {
                reason = "Invalid rule.";
                return false;
            }

            if (!typeof(ISoDataPack).IsAssignableFrom(packType))
            {
                reason = "Invalid rule.";
                return false;
            }

            if (!typeof(ISoDataGroup).IsAssignableFrom(groupType))
            {
                reason = "Invalid rule.";
                return false;
            }

            if (!typeof(ISoDataInfo).IsAssignableFrom(infoType))
            {
                reason = "Invalid rule.";
                return false;
            }

            return true;
        }

        private void BindAndGenerate(Type sourceType, ESSoTableRuleBindSourceKind sourceKind, UnityEngine.Object sourceObject)
        {
            if (sourceType == null)
                return;

            Undo.RecordObject(this, "缂傚倷鐒﹂崹鐢告偩?SO 表格 Rule");

            if (ESSoTableRuleTypeUtility.TryResolveSoDataTypes(sourceType, out Type packType, out Type groupType, out Type infoType, out string reason))
            {
                CaptureBuildSource(sourceKind, sourceObject, sourceType);
                typeBinding.objectKind = ESSoTableRuleObjectKind.SoData;
                typeBinding.objectTypeName = string.Empty;
                typeBinding.packTypeName = packType.FullName;
                typeBinding.groupTypeName = groupType.FullName;
                typeBinding.infoTypeName = infoType.FullName;

                FillDefaultNames(infoType);
                RebuildColumnsFromInfoFields();

                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
                Debug.Log("SO 表格规则 类型解析完成：Pack=" + packType.Name + "，Group=" + groupType.Name + "，Info=" + infoType.Name, this);
                return;
            }

            if (!typeof(ScriptableObject).IsAssignableFrom(sourceType))
            {
                Debug.LogWarning(reason, this);
                return;
            }

            CaptureBuildSource(sourceKind, sourceObject, sourceType);
            typeBinding.objectKind = ESSoTableRuleObjectKind.ScriptableObject;
            typeBinding.objectTypeName = sourceType.FullName;
            typeBinding.packTypeName = string.Empty;
            typeBinding.groupTypeName = string.Empty;
            typeBinding.infoTypeName = string.Empty;

            FillDefaultNames(sourceType);
            RebuildColumnsFromInfoFields();

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            Debug.Log("SO 表格规则 普通 ScriptableObject 类型解析完成：" + sourceType.Name, this);
        }

        private void CaptureBuildSource(ESSoTableRuleBindSourceKind sourceKind, UnityEngine.Object sourceObject, Type sourceType)
        {
            ESSoTableRuleSourceBinding buildSource = BuildSourceBinding;
            if (buildSource == null)
                return;

            buildSource.Capture(sourceKind, sourceObject, sourceType);
        }

        private static List<ScriptableObject> CollectScriptableObjectsFromFolder(DefaultAsset folder, bool includeSubFolders)
        {
            var assets = new List<ScriptableObject>();
            string folderPath = AssetDatabase.GetAssetPath(folder);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                return assets;

            string[] folders = includeSubFolders ? new[] { folderPath } : GetDirectFolderOnly(folderPath);
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", folders);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!includeSubFolders && System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/') != folderPath)
                    continue;

                ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset != null)
                    assets.Add(asset);
            }

            return assets;
        }

        private static string[] GetDirectFolderOnly(string folderPath)
        {
            return new[] { folderPath };
        }

        private static int CountAssignableAssets(List<ScriptableObject> assets, Type sourceType)
        {
            int count = 0;
            for (int i = 0; i < assets.Count; i++)
            {
                if (assets[i] != null && sourceType.IsAssignableFrom(assets[i].GetType()))
                    count++;
            }

            return count;
        }

        private void FillDefaultNames(Type infoType)
        {
            if (infoType == null)
                return;

            string shortName = infoType.Name;
            if (string.IsNullOrEmpty(ruleKey))
                ruleKey = shortName;
            if (string.IsNullOrEmpty(tableName))
                tableName = "Tb" + shortName;
            if (string.IsNullOrEmpty(beanName))
                beanName = shortName;
        }

        [TitleGroup("")]
        [Button("从绑定类型生成字段映射")]
        public void RebuildColumnsFromInfoFields()
        {
            Type rowOwnerType = typeBinding.RowOwnerType;
            if (rowOwnerType == null)
                return;

            List<ESTableColumnNameMap> lockedColumns = CollectLockedColumns();
            columns.Clear();
            if (typeof(ISoDataInfo).IsAssignableFrom(rowOwnerType))
            {
                columns.Add(new ESTableColumnNameMap
                {
                    soFieldPath = nameof(SoDataInfo.KeyName),
                    columnName = infoKeyColumnName,
                    displayName = "Info Key",
                    tableType = "string",
                    isInfoKey = true
                });
            }

            if (rowBinding != null && rowBinding.IsListElementRow)
            {
                Type elementType = ESRowBindingReflectionUtility.GetListElementType(ESRowBindingReflectionUtility.GetMemberType(rowOwnerType, rowBinding.listFieldPath));
                if (elementType == null)
                    return;

                columns.Add(new ESTableColumnNameMap
                {
                    soFieldPath = BuildListElementPath(rowBinding.elementKeyFieldPath),
                    columnName = rowBinding.rowKeyColumnName,
                    displayName = "闁?Key",
                    tableType = "string"
                });

                RebuildColumnsFromTypeFields(elementType, true);
                RestoreLockedColumns(lockedColumns);
                return;
            }

            RebuildColumnsFromTypeFields(rowOwnerType, false);
            RestoreLockedColumns(lockedColumns);
        }

        [FoldoutGroup("", Expanded = false)]
        [Button("预热反射字段缓存")]
        public void PrewarmReflectionCache()
        {
            Type rowOwnerType = typeBinding.RowOwnerType;
            if (rowOwnerType == null || columns == null)
                return;

            Type listElementType = null;
            if (rowBinding != null && rowBinding.IsListElementRow)
            {
                Type listType = ESRowBindingReflectionUtility.GetMemberType(rowOwnerType, rowBinding.listFieldPath);
                listElementType = ESRowBindingReflectionUtility.GetListElementType(listType);
                ESRowBindingReflectionUtility.GetOrCreateMemberPath(rowOwnerType, rowBinding.listFieldPath);
            }

            for (int i = 0; i < columns.Count; i++)
            {
                ESTableColumnNameMap column = columns[i];
                if (column == null || !column.IsUsable || string.IsNullOrWhiteSpace(column.soFieldPath))
                    continue;

                if (TryGetReflectionPathForColumn(column.soFieldPath, rowOwnerType, listElementType, out Type ownerType, out string memberPath))
                    ESRowBindingReflectionUtility.GetOrCreateMemberPath(ownerType, memberPath);
            }
        }

        public void RebuildColumnsFromBuildTable()
        {
            if (buildStage == null || string.IsNullOrWhiteSpace(buildStage.tableFilePath))
            {
                Debug.LogWarning("构建阶段未指定表格样本路径。", this);
                return;
            }

            string path = buildStage.tableFilePath;
            if (!File.Exists(path))
            {
                Debug.LogWarning("构建表格不存在：" + path, this);
                return;
            }

            List<List<string>> table = ReadTableFileAuto(path);
            if (table.Count == 0)
            {
                Debug.LogWarning("构建表格没有可读取的表头。", this);
                return;
            }

            Undo.RecordObject(this, "从表格表头构建 SO 表格规则");
            List<ESTableColumnNameMap> lockedColumns = CollectLockedColumns();
            columns.Clear();

            List<string> varRow = table[0];
            List<string> typeRow = table.Count > 1 ? table[1] : null;
            List<string> commentRow = table.Count > 3 ? table[3] : null;
            int start = varRow.Count > 0 && IsHeaderMark(varRow[0]) ? 1 : 0;
            for (int i = start; i < varRow.Count; i++)
            {
                string columnName = varRow[i];
                if (string.IsNullOrWhiteSpace(columnName))
                    continue;

                columns.Add(new ESTableColumnNameMap
                {
                    soFieldPath = columnName,
                    columnName = columnName,
                    displayName = columnName,
                    tableType = typeRow != null && i < typeRow.Count && !string.IsNullOrWhiteSpace(typeRow[i]) ? typeRow[i] : "string",
                    comment = commentRow != null && i < commentRow.Count ? commentRow[i] : string.Empty
                });
            }
            RestoreLockedColumns(lockedColumns);

            if (string.IsNullOrEmpty(ruleKey))
                ruleKey = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(tableName))
                tableName = "Tb" + Path.GetFileNameWithoutExtension(path);

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        private static bool IsHeaderMark(string value)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith("##", StringComparison.Ordinal);
        }

        [TitleGroup("")]
        [HorizontalGroup("")]
        [Button("导出表格")]
        public void ExportTableFiles()
        {
            List<List<string>> table = BuildTableRows();
            if (table.Count == 0)
            {
                Debug.LogWarning("没有可导出的字段映射。", this);
                return;
            }

            ESTableFileKind currentFileKind = GetActiveFileKind();
            string csvPath = GetOutputPath(GetActiveCsvRelativePath(), ".csv");
            string xlsxPath = GetOutputPath(GetActiveXlsxRelativePath(), ".xlsx");

            if (currentFileKind == ESTableFileKind.Csv || currentFileKind == ESTableFileKind.CsvAndXlsx)
                WriteCsv(csvPath, table);

            if (currentFileKind == ESTableFileKind.Xlsx || currentFileKind == ESTableFileKind.CsvAndXlsx)
                WriteXlsx(xlsxPath, table, string.IsNullOrEmpty(GetActiveSheetName()) ? "Sheet1" : GetActiveSheetName());

            AssetDatabase.Refresh();
            Debug.Log($"表格导出完成：{csvPath} / {xlsxPath}", this);
        }

        [TitleGroup("")]
        [HorizontalGroup("")]
        [Button("自动读取写回")]
        public void ImportTableFileAuto()
        {
            string path = ResolveExistingInputPath();
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("没有找到可导入的 CSV 或 XLSX 文件。", this);
                return;
            }

            List<List<string>> table = ReadTableFileAuto(path);
            if (table.Count < 5)
            {
                Debug.LogWarning("表格行数不足，无法按当前 表格表头结构导入。", this);
                return;
            }

            int changedCount = ApplyTableRowsToExistingObjects(table);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"表格导入完成：{path}，已更新 SO 数量：{changedCount}", this);
        }

        [TitleGroup("")]
        [HorizontalGroup("")]
        [Button("选择表格写回")]
        public void ImportTableFileByPanel()
        {
            string path = EditorUtility.OpenFilePanel("选择 CSV 或 XLSX 表格", GetOutputFolder(GetActiveCsvRelativePath()), "csv,xlsx");
            if (string.IsNullOrEmpty(path))
                return;

            List<List<string>> table = ReadTableFileAuto(path);
            int changedCount = ApplyTableRowsToExistingObjects(table);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"表格导入完成：{path}，已更新 SO 数量：{changedCount}", this);
        }

        public void AddUseBatch()
        {
            if (useBatches == null)
                useBatches = new List<ESSoTableRuleUseBatch>();

            useBatches.Add(new ESSoTableRuleUseBatch
            {
                batchName = string.IsNullOrEmpty(ruleKey) ? "New Batch" : ruleKey,
                fileName = FirstConfiguredName(),
                sheetName = FirstConfiguredName()
            });

            EditorUtility.SetDirty(this);
        }

        [ContextMenu("SO 表格规则/快速方案/标准 SO 表")]
        public void ApplyPresetStandardSoTable()
        {
            infoExpandMode = ESTableInfoExpandMode.ExplicitMappingsOnly;
            groupSliceMode = ESTableGroupSliceMode.GroupNameColumn;
            nameMatchMode = ESTableNameMatchMode.FieldToColumn;
            allowCreateInfoOnImport = true;
            allowCreateGroupOnImport = true;
            refreshPackBeforeExport = true;
            ApplyDefaultBatchPolicy(ESSoTableRuleUseDirection.ImportAndExport);
        }

        [ContextMenu("SO 表格规则/快速方案/只导出表格")]
        public void ApplyPresetExportOnly()
        {
            infoExpandMode = ESTableInfoExpandMode.ExplicitMappingsOnly;
            groupSliceMode = ESTableGroupSliceMode.GroupNameColumn;
            refreshPackBeforeExport = true;
            ApplyDefaultBatchPolicy(ESSoTableRuleUseDirection.Export);
        }

        [ContextMenu("SO 表格规则/快速方案/表格写回 SO")]
        public void ApplyPresetImportBack()
        {
            infoExpandMode = ESTableInfoExpandMode.ExplicitMappingsOnly;
            groupSliceMode = ESTableGroupSliceMode.GroupNameColumn;
            allowCreateInfoOnImport = true;
            allowCreateGroupOnImport = true;
            ApplyDefaultBatchPolicy(ESSoTableRuleUseDirection.Import);
        }

        [ContextMenu("SO 表格规则/快速方案/普通 SO 简化表")]
        public void ApplyPresetSimpleScriptableObject()
        {
            if (typeBinding != null)
                typeBinding.objectKind = ESSoTableRuleObjectKind.ScriptableObject;

            infoExpandMode = ESTableInfoExpandMode.SerializedFields;
            groupSliceMode = ESTableGroupSliceMode.IgnoreGroup;
            nameMatchMode = ESTableNameMatchMode.FieldToColumn;
            ApplyDefaultBatchPolicy(ESSoTableRuleUseDirection.ImportAndExport);
        }

        public void ApplyDefaultBatchPolicy(ESSoTableRuleUseDirection direction)
        {
            if (useBatches == null)
                useBatches = new List<ESSoTableRuleUseBatch>();

            if (useBatches.Count == 0)
                AddUseBatch();

            for (int i = 0; i < useBatches.Count; i++)
            {
                ESSoTableRuleUseBatch batch = useBatches[i];
                if (batch == null)
                    continue;

                batch.direction = direction;
                if (string.IsNullOrWhiteSpace(batch.fileName))
                    batch.fileName = FirstConfiguredName();
                if (string.IsNullOrWhiteSpace(batch.sheetName))
                    batch.sheetName = FirstConfiguredName();
                if (string.IsNullOrWhiteSpace(batch.outputRoot))
                    batch.outputRoot = "SoTableConfig/Tables";
                if (string.IsNullOrWhiteSpace(batch.csvRelativePath))
                    batch.csvRelativePath = "csv";
                if (string.IsNullOrWhiteSpace(batch.xlsxRelativePath))
                    batch.xlsxRelativePath = "xlsx";
            }

            EditorUtility.SetDirty(this);
        }

        private string FirstConfiguredName()
        {
            if (!string.IsNullOrWhiteSpace(ruleKey))
                return ruleKey;
            if (!string.IsNullOrWhiteSpace(tableName))
                return tableName.StartsWith("Tb", StringComparison.Ordinal) && tableName.Length > 2 ? tableName.Substring(2) : tableName;
            if (!string.IsNullOrWhiteSpace(beanName))
                return beanName;
            return name;
        }

        public void ExecuteAllEnabledBatches()
        {
            if (useBatches == null)
                return;

            for (int i = 0; i < useBatches.Count; i++)
            {
                ESSoTableRuleUseBatch batch = useBatches[i];
                if (batch != null && batch.enabled)
                    ExecuteUseBatch(batch);
            }
        }

        public void ExecuteUseBatch(ESSoTableRuleUseBatch batch)
        {
            if (batch == null)
                return;

            ESSoTableRuleUseBatch oldBatch = activeUseBatch;
            try
            {
                activeUseBatch = batch;
                if (batch.direction == ESSoTableRuleUseDirection.Export)
                    ExportTableFiles();
                else if (batch.direction == ESSoTableRuleUseDirection.Import)
                    ImportTableFileAuto();
                else
                {
                    ImportTableFileAuto();
                    ExportTableFiles();
                }
            }
            finally
            {
                activeUseBatch = oldBatch;
            }
        }

        private List<List<string>> BuildTableRows()
        {
            List<ESTableColumnNameMap> enabledColumns = GetEnabledColumns();
            var table = new List<List<string>>();
            if (enabledColumns.Count == 0)
                return table;

            table.Add(BuildHeaderRow(header.varMark, enabledColumns, GetActiveTableColumnName));
            table.Add(BuildHeaderRow(header.typeMark, enabledColumns, c => string.IsNullOrEmpty(c.tableType) ? "string" : c.tableType));
            table.Add(BuildHeaderRow(header.groupMark, enabledColumns, c => string.IsNullOrEmpty(header.defaultGroup) ? "client" : header.defaultGroup));
            table.Add(BuildHeaderRow(header.commentMark, enabledColumns, c => string.IsNullOrEmpty(c.comment) ? c.displayName : c.comment));

            List<ScriptableObject> owners = CollectExportOwners();
            Type ownerType = typeBinding.RowOwnerType;
            Type listElementType = GetConfiguredListElementType(ownerType);
            List<ESTableCompiledColumn> compiledColumns = CompileColumns(enabledColumns, ownerType, listElementType, null);
            for (int i = 0; i < owners.Count; i++)
                AppendOwnerRows(table, owners[i], compiledColumns);

            AppendPassThroughColumnsIfNeeded(table, enabledColumns);
            return table;
        }

        private int ApplyTableRowsToExistingObjects(List<List<string>> table)
        {
            List<ESTableColumnNameMap> enabledColumns = GetEnabledColumns();
            Dictionary<string, ESTableColumnNameMap> columnMap = BuildColumnMap(enabledColumns);
            Dictionary<int, ESTableColumnNameMap> tableColumnMap = BuildTableColumnMap(table, columnMap);
            List<ScriptableObject> owners = CollectExportOwners();
            Dictionary<string, ScriptableObject> ownersByKey = BuildOwnersByKey(owners, enabledColumns);
            Type ownerType = typeBinding.RowOwnerType;
            Type listElementType = GetConfiguredListElementType(ownerType);
            List<ESTableCompiledColumn> compiledColumns = CompileColumns(enabledColumns, ownerType, listElementType, tableColumnMap);
            int rowKeyColumnIndex = FindTableColumnIndex(tableColumnMap, rowBinding != null ? rowBinding.rowKeyColumnName : null);

            int changedCount = 0;
            int ownerCursor = 0;
            ESTableBatchApplyFilter applyFilter = BuildApplyFilter(table, tableColumnMap);
            for (int rowIndex = 4; rowIndex < table.Count; rowIndex++)
            {
                List<string> row = table[rowIndex];
                if (row == null || row.Count == 0)
                    continue;
                if (!ShouldApplyTableRow(row, applyFilter))
                    continue;

                ScriptableObject owner = FindOwnerForTableRow(row, tableColumnMap, ownersByKey, owners, ref ownerCursor);
                if (owner == null)
                    continue;

                object rowObject = owner;
                if (rowBinding != null && rowBinding.IsListElementRow)
                {
                    string rowKey = rowKeyColumnIndex >= 0 && rowKeyColumnIndex < row.Count ? row[rowKeyColumnIndex] : string.Empty;
                    if (!RowBridge.TryGetOrCreateRow(owner, rowKey, rowBinding, out rowObject, out string reason))
                    {
                        Debug.LogWarning(reason, owner);
                        continue;
                    }
                }

                Undo.RecordObject(owner, "闁荤偞绋忛崝宥夋偋閹间礁绀冩繛鍡楃箲缁€鈧?SO");
                ApplyTableRowToObject(row, compiledColumns, owner, rowObject);
                EditorUtility.SetDirty(owner);
                changedCount++;
            }

            return changedCount;
        }

        private Dictionary<string, ESTableColumnNameMap> BuildColumnMap(List<ESTableColumnNameMap> enabledColumns)
        {
            var map = new Dictionary<string, ESTableColumnNameMap>();
            for (int i = 0; i < enabledColumns.Count; i++)
            {
                ESTableColumnNameMap column = enabledColumns[i];
                string activeName = GetActiveTableColumnName(column);
                if (!string.IsNullOrEmpty(activeName))
                    map[activeName] = column;
            }

            return map;
        }

        private static Dictionary<int, ESTableColumnNameMap> BuildTableColumnMap(List<List<string>> table, Dictionary<string, ESTableColumnNameMap> columnMap)
        {
            var result = new Dictionary<int, ESTableColumnNameMap>();
            if (table == null || table.Count == 0)
                return result;

            List<string> varRow = table[0];
            for (int i = 1; i < varRow.Count; i++)
            {
                string columnName = varRow[i];
                if (!string.IsNullOrEmpty(columnName) && columnMap.TryGetValue(columnName, out ESTableColumnNameMap column))
                    result[i] = column;
            }

            return result;
        }

        private List<ESTableCompiledColumn> CompileColumns(List<ESTableColumnNameMap> enabledColumns, Type ownerType, Type listElementType, Dictionary<int, ESTableColumnNameMap> tableColumnMap)
        {
            var result = new List<ESTableCompiledColumn>(enabledColumns.Count);
            for (int i = 0; i < enabledColumns.Count; i++)
            {
                ESTableColumnNameMap map = enabledColumns[i];
                int tableColumnIndex = tableColumnMap == null ? i + 1 : FindTableColumnIndex(tableColumnMap, map);
                if (tableColumnMap != null && tableColumnIndex < 0)
                    continue;

                var compiled = new ESTableCompiledColumn
                {
                    tableColumnIndex = tableColumnIndex,
                    map = map,
                    canRead = map.direction != ESSoTableRuleDirection.TableToSoOnly && map.direction != ESSoTableRuleDirection.Ignore,
                    canWrite = map.direction != ESSoTableRuleDirection.SoToTableOnly && map.direction != ESSoTableRuleDirection.Ignore && !map.isInfoKey && !map.isGroupKey && !map.locked
                };

                if (TryGetReflectionPathForColumn(map.soFieldPath, ownerType, listElementType, out Type targetType, out string memberPath))
                {
                    compiled.ownerType = targetType;
                    compiled.memberPath = memberPath;
                    compiled.useRowObject = targetType == listElementType;
                    compiled.valueType = ESRowBindingReflectionUtility.GetMemberType(targetType, memberPath);
                    ESRowBindingReflectionUtility.GetOrCreateMemberPath(targetType, memberPath);
                }

                if (map.isInfoKey)
                    compiled.canRead = true;
                if (compiled.valueType == null && !map.isInfoKey)
                {
                    compiled.canRead = false;
                    compiled.canWrite = false;
                }

                result.Add(compiled);
            }

            return result;
        }

        private static int FindTableColumnIndex(Dictionary<int, ESTableColumnNameMap> tableColumnMap, ESTableColumnNameMap map)
        {
            foreach (KeyValuePair<int, ESTableColumnNameMap> pair in tableColumnMap)
            {
                if (ReferenceEquals(pair.Value, map))
                    return pair.Key;
            }

            return -1;
        }

        private int FindTableColumnIndex(Dictionary<int, ESTableColumnNameMap> tableColumnMap, string columnName)
        {
            if (string.IsNullOrEmpty(columnName))
                return -1;

            foreach (KeyValuePair<int, ESTableColumnNameMap> pair in tableColumnMap)
            {
                if (GetActiveTableColumnName(pair.Value) == columnName)
                    return pair.Key;
            }

            return -1;
        }

        private Type GetConfiguredListElementType(Type ownerType)
        {
            if (ownerType == null || rowBinding == null || !rowBinding.IsListElementRow)
                return null;

            Type listType = ESRowBindingReflectionUtility.GetMemberType(ownerType, rowBinding.listFieldPath);
            return ESRowBindingReflectionUtility.GetListElementType(listType);
        }

        private Dictionary<string, ScriptableObject> BuildOwnersByKey(List<ScriptableObject> owners, List<ESTableColumnNameMap> enabledColumns)
        {
            var result = new Dictionary<string, ScriptableObject>();
            ESTableColumnNameMap objectKeyColumn = FindObjectKeyColumn(enabledColumns);
            ESTableCompiledColumn compiledKeyColumn = null;
            Type ownerType = typeBinding.RowOwnerType;
            if (objectKeyColumn != null)
            {
                List<ESTableCompiledColumn> compiled = CompileColumns(new List<ESTableColumnNameMap> { objectKeyColumn }, ownerType, GetConfiguredListElementType(ownerType), null);
                if (compiled.Count > 0)
                    compiledKeyColumn = compiled[0];
            }

            for (int i = 0; i < owners.Count; i++)
            {
                ScriptableObject owner = owners[i];
                if (owner == null)
                    continue;

                string key = null;
                if (owner is ISoDataInfo info)
                    key = info.GetKey();
                else if (compiledKeyColumn != null)
                    key = ConvertCellValue(GetColumnValue(owner, owner, compiledKeyColumn), compiledKeyColumn.map.valueWriteMode);

                if (!string.IsNullOrEmpty(key))
                    result[key] = owner;
            }

            return result;
        }

        private ESTableColumnNameMap FindObjectKeyColumn(List<ESTableColumnNameMap> enabledColumns)
        {
            for (int i = 0; i < enabledColumns.Count; i++)
            {
                ESTableColumnNameMap column = enabledColumns[i];
                if (column.isInfoKey || (rowBinding != null && column.columnName == rowBinding.rowKeyColumnName))
                    return column;
            }

            return null;
        }

        private ScriptableObject FindOwnerForTableRow(List<string> row, Dictionary<int, ESTableColumnNameMap> tableColumnMap, Dictionary<string, ScriptableObject> ownersByKey, List<ScriptableObject> owners, ref int ownerCursor)
        {
            string key = null;
            foreach (KeyValuePair<int, ESTableColumnNameMap> pair in tableColumnMap)
            {
                ESTableColumnNameMap column = pair.Value;
                if (!column.isInfoKey && (rowBinding == null || column.columnName != rowBinding.rowKeyColumnName))
                    continue;

                key = pair.Key < row.Count ? row[pair.Key] : string.Empty;
                if (!string.IsNullOrEmpty(key))
                    break;
            }

            if (!string.IsNullOrEmpty(key) && ownersByKey.TryGetValue(key, out ScriptableObject keyedOwner))
                return keyedOwner;

            if (ownerCursor < owners.Count)
                return owners[ownerCursor++];

            return null;
        }

        private ESTableBatchApplyFilter BuildApplyFilter(List<List<string>> table, Dictionary<int, ESTableColumnNameMap> tableColumnMap)
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            var filter = new ESTableBatchApplyFilter
            {
                mode = batch != null ? batch.applyRangeMode : ESTableBatchApplyRangeMode.All,
                startValue = batch != null ? batch.sliceStartValue : string.Empty,
                endValue = batch != null ? batch.sliceEndValue : string.Empty,
                includeStart = batch == null || batch.includeSliceStart,
                includeEnd = batch == null || batch.includeSliceEnd,
                targetGroupKey = batch != null ? batch.targetGroupKey : string.Empty,
                targetInfoKey = batch != null ? batch.targetInfoKey : string.Empty
            };

            if (filter.mode == ESTableBatchApplyRangeMode.All)
                return filter;

            string sliceColumnName = batch != null ? batch.sliceColumnName : string.Empty;
            filter.sliceColumnIndex = FindRawTableColumnIndex(table, string.IsNullOrWhiteSpace(sliceColumnName) ? GetActiveInfoKeyColumnName() : sliceColumnName);
            filter.groupColumnIndex = FindRawTableColumnIndex(table, groupColumnName);
            filter.infoColumnIndex = FindRawTableColumnIndex(table, GetActiveInfoKeyColumnName());

            if (filter.sliceColumnIndex < 0)
                filter.sliceColumnIndex = FindTableColumnIndex(tableColumnMap, GetActiveInfoKeyColumnName());
            if (filter.infoColumnIndex < 0)
                filter.infoColumnIndex = FindTableColumnIndex(tableColumnMap, GetActiveInfoKeyColumnName());

            return filter;
        }

        private bool ShouldApplyTableRow(List<string> row, ESTableBatchApplyFilter filter)
        {
            if (filter == null || filter.mode == ESTableBatchApplyRangeMode.All)
                return true;

            if (filter.mode == ESTableBatchApplyRangeMode.SingleGroupInfo)
            {
                bool groupMatched = string.IsNullOrWhiteSpace(filter.targetGroupKey) || GetCell(row, filter.groupColumnIndex) == filter.targetGroupKey;
                bool infoMatched = string.IsNullOrWhiteSpace(filter.targetInfoKey) || GetCell(row, filter.infoColumnIndex) == filter.targetInfoKey;
                return groupMatched && infoMatched;
            }

            if (filter.mode != ESTableBatchApplyRangeMode.Slice)
                return true;

            string value = GetCell(row, filter.sliceColumnIndex);
            bool isStart = !string.IsNullOrWhiteSpace(filter.startValue) && value == filter.startValue;
            bool isEnd = !string.IsNullOrWhiteSpace(filter.endValue) && value == filter.endValue;

            if (filter.sliceFinished)
                return false;

            if (!filter.sliceStarted)
            {
                if (string.IsNullOrWhiteSpace(filter.startValue))
                    filter.sliceStarted = true;
                else if (isStart)
                    filter.sliceStarted = true;
                else
                    return false;

                if (isStart && !filter.includeStart)
                    return false;
            }

            if (isEnd)
            {
                filter.sliceFinished = true;
                return filter.includeEnd;
            }

            return filter.sliceStarted;
        }

        private static string GetCell(List<string> row, int columnIndex)
        {
            return row != null && columnIndex >= 0 && columnIndex < row.Count ? row[columnIndex] : string.Empty;
        }

        private static int FindRawTableColumnIndex(List<List<string>> table, string columnName)
        {
            if (table == null || table.Count == 0 || string.IsNullOrWhiteSpace(columnName))
                return -1;

            List<string> varRow = table[0];
            for (int i = 1; i < varRow.Count; i++)
            {
                if (string.Equals(varRow[i], columnName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private void ApplyTableRowToObject(List<string> row, List<ESTableCompiledColumn> compiledColumns, ScriptableObject owner, object rowObject)
        {
            for (int i = 0; i < compiledColumns.Count; i++)
            {
                ESTableCompiledColumn column = compiledColumns[i];
                if (!column.canWrite || column.tableColumnIndex < 0 || column.tableColumnIndex >= row.Count)
                    continue;

                object target = column.useRowObject ? rowObject : owner;
                if (column.map.authority == ESTableColumnAuthority.SoAuthority)
                {
                    object currentValue = ESRowBindingReflectionUtility.GetMemberValue(target, column.memberPath);
                    if (!IsEmptyAuthorityValue(currentValue))
                        continue;
                }

                object value = ConvertStringToValue(row[column.tableColumnIndex], column.valueType, column.map.valueWriteMode);
                ESRowBindingReflectionUtility.SetMemberValue(target, column.memberPath, value);
            }
        }

        private static bool IsEmptyAuthorityValue(object value)
        {
            if (value == null)
                return true;
            if (value is string text)
                return string.IsNullOrEmpty(text);
            if (value is UnityEngine.Object unityObject)
                return unityObject == null;
            if (value is ICollection collection)
                return collection.Count == 0;

            Type type = value.GetType();
            if (type.IsValueType)
                return value.Equals(Activator.CreateInstance(type));

            return false;
        }

        private static object ConvertStringToValue(string value, Type targetType, ESTableValueWriteMode writeMode)
        {
            string raw = value ?? string.Empty;
            string trimmed = raw.Trim();

            if (targetType == null)
                return raw;
            if (targetType == typeof(string))
                return raw;
            if (string.IsNullOrEmpty(trimmed))
            {
                if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
                    return null;

                object emptyInstance = ESRowBindingReflectionUtility.CreateObjectInstance(targetType);
                return emptyInstance ?? (targetType.IsValueType ? Activator.CreateInstance(targetType) : null);
            }
            if (targetType == typeof(bool))
                return trimmed.Equals("true", StringComparison.OrdinalIgnoreCase) || trimmed == "1";
            if (targetType.IsEnum)
                return ConvertStringToEnum(trimmed, targetType);
            if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
            {
                string path = writeMode == ESTableValueWriteMode.UnityObjectPath ? trimmed : AssetDatabase.GUIDToAssetPath(trimmed);
                if (string.IsNullOrEmpty(path))
                    path = trimmed;
                return AssetDatabase.LoadAssetAtPath(path, targetType);
            }

            if (ShouldUseJsonCell(targetType))
                return JsonConvert.DeserializeObject(trimmed, targetType, CellJsonSettings);

            return Convert.ChangeType(trimmed, targetType, CultureInfo.InvariantCulture);
        }

        private static object ConvertStringToEnum(string value, Type enumType)
        {
            string normalized = NormalizeEnumCell(value);
            if (Enum.TryParse(enumType, normalized, true, out object enumValue))
                return enumValue;

            object displayNameValue = TryConvertEnumDisplayName(value, enumType);
            if (displayNameValue != null)
                return displayNameValue;

            object numericValue = TryConvertEnumNumber(value, enumType);
            if (numericValue != null)
                return numericValue;

            throw new FormatException("枚举导入失败：值 \"" + value + "\" 不能转换为 " + enumType.Name + "。请使用枚举名、数字值，或字段上的中文显示名。");
        }

        private static string NormalizeEnumCell(string value)
        {
            return (value ?? string.Empty)
                .Trim()
                .Replace("，", ",")
                .Replace("|", ",")
                .Replace("+", ",")
                .Replace("/", ",");
        }

        private static object TryConvertEnumDisplayName(string value, Type enumType)
        {
            string normalized = NormalizeEnumCell(value);
            string[] parts = normalized.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return null;

            long combined = 0L;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                FieldInfo matchedField = FindEnumFieldByDisplayName(enumType, part);
                if (matchedField == null)
                    return null;

                object raw = matchedField.GetValue(null);
                combined |= Convert.ToInt64(raw, CultureInfo.InvariantCulture);
            }

            return Enum.ToObject(enumType, combined);
        }

        private static FieldInfo FindEnumFieldByDisplayName(Type enumType, string displayName)
        {
            FieldInfo[] fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (string.Equals(field.Name, displayName, StringComparison.OrdinalIgnoreCase))
                    return field;

                string attributeName = GetEnumDisplayName(field);
                if (!string.IsNullOrEmpty(attributeName) && string.Equals(attributeName, displayName, StringComparison.OrdinalIgnoreCase))
                    return field;
            }

            return null;
        }

        private static string GetEnumDisplayName(FieldInfo field)
        {
            object[] attributes = field.GetCustomAttributes(false);
            for (int i = 0; i < attributes.Length; i++)
            {
                object attribute = attributes[i];
                string typeName = attribute.GetType().Name;
                if (typeName != "LabelTextAttribute" && typeName != "InspectorNameAttribute")
                    continue;

                string text = TryReadStringProperty(attribute, "Text");
                if (!string.IsNullOrEmpty(text))
                    return text;

                text = TryReadStringProperty(attribute, "Name");
                if (!string.IsNullOrEmpty(text))
                    return text;

                text = TryReadStringProperty(attribute, "displayName");
                if (!string.IsNullOrEmpty(text))
                    return text;
            }

            return null;
        }

        private static string TryReadStringProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null || property.PropertyType != typeof(string))
                return null;

            return property.GetValue(target, null) as string;
        }

        private static object TryConvertEnumNumber(string value, Type enumType)
        {
            string trimmed = (value ?? string.Empty).Trim();
            try
            {
                Type underlyingType = Enum.GetUnderlyingType(enumType);
                object number = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ChangeType(Convert.ToInt64(trimmed.Substring(2), 16), underlyingType, CultureInfo.InvariantCulture)
                    : Convert.ChangeType(trimmed, underlyingType, CultureInfo.InvariantCulture);
                return Enum.ToObject(enumType, number);
            }
            catch
            {
                return null;
            }
        }

        private List<ESTableColumnNameMap> GetEnabledColumns()
        {
            var enabledColumns = new List<ESTableColumnNameMap>();
            if (columns == null)
                return enabledColumns;

            for (int i = 0; i < columns.Count; i++)
            {
                ESTableColumnNameMap column = columns[i];
                if (column != null && column.IsUsable && !string.IsNullOrWhiteSpace(column.columnName))
                    enabledColumns.Add(column);
            }

            return enabledColumns;
        }

        private List<ESTableColumnNameMap> CollectLockedColumns()
        {
            var result = new List<ESTableColumnNameMap>();
            if (columns == null)
                return result;

            for (int i = 0; i < columns.Count; i++)
            {
                ESTableColumnNameMap column = columns[i];
                if (column != null && column.locked)
                    result.Add(column);
            }

            return result;
        }

        private void RestoreLockedColumns(List<ESTableColumnNameMap> lockedColumns)
        {
            if (lockedColumns == null || lockedColumns.Count == 0)
                return;
            if (columns == null)
                columns = new List<ESTableColumnNameMap>();

            for (int i = 0; i < lockedColumns.Count; i++)
            {
                ESTableColumnNameMap lockedColumn = lockedColumns[i];
                if (lockedColumn != null && !ContainsEquivalentColumn(columns, lockedColumn))
                    columns.Add(lockedColumn);
            }
        }

        private static bool ContainsEquivalentColumn(List<ESTableColumnNameMap> list, ESTableColumnNameMap column)
        {
            for (int i = 0; i < list.Count; i++)
            {
                ESTableColumnNameMap item = list[i];
                if (item == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(column.soFieldPath) && item.soFieldPath == column.soFieldPath)
                    return true;
                if (!string.IsNullOrWhiteSpace(column.columnName) && item.columnName == column.columnName)
                    return true;
            }

            return false;
        }

        private static List<string> BuildHeaderRow(string mark, List<ESTableColumnNameMap> enabledColumns, Func<ESTableColumnNameMap, string> valueGetter)
        {
            var row = new List<string>(enabledColumns.Count + 1) { mark };
            for (int i = 0; i < enabledColumns.Count; i++)
                row.Add(valueGetter(enabledColumns[i]) ?? string.Empty);

            return row;
        }

        private void AppendPassThroughColumnsIfNeeded(List<List<string>> table, List<ESTableColumnNameMap> enabledColumns)
        {
            if (!ShouldPassThroughUnmappedColumns(enabledColumns))
                return;

            string existingPath = ResolveExistingInputPath();
            if (string.IsNullOrEmpty(existingPath) || !File.Exists(existingPath))
                return;

            List<List<string>> oldTable = ReadTableFileAuto(existingPath);
            if (oldTable.Count == 0 || oldTable[0].Count == 0)
                return;

            HashSet<string> mappedColumnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < enabledColumns.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(enabledColumns[i].columnName))
                    mappedColumnNames.Add(enabledColumns[i].columnName);
            }

            int oldStart = oldTable[0].Count > 0 && IsHeaderMark(oldTable[0][0]) ? 1 : 0;
            var passThroughIndices = new List<int>();
            for (int i = oldStart; i < oldTable[0].Count; i++)
            {
                string oldColumnName = oldTable[0][i];
                if (!string.IsNullOrWhiteSpace(oldColumnName) && !mappedColumnNames.Contains(oldColumnName))
                    passThroughIndices.Add(i);
            }

            if (passThroughIndices.Count == 0)
                return;

            for (int rowIndex = 0; rowIndex < table.Count; rowIndex++)
            {
                List<string> row = table[rowIndex];
                List<string> oldRow = rowIndex < oldTable.Count ? oldTable[rowIndex] : null;
                for (int i = 0; i < passThroughIndices.Count; i++)
                {
                    int oldColumnIndex = passThroughIndices[i];
                    row.Add(oldRow != null && oldColumnIndex < oldRow.Count ? oldRow[oldColumnIndex] : string.Empty);
                }
            }
        }

        private static bool ShouldPassThroughUnmappedColumns(List<ESTableColumnNameMap> enabledColumns)
        {
            if (enabledColumns == null)
                return false;

            for (int i = 0; i < enabledColumns.Count; i++)
            {
                if (enabledColumns[i] != null && enabledColumns[i].allowPassThrough)
                    return true;
            }

            return false;
        }

        private List<ScriptableObject> CollectExportOwners()
        {
            var owners = new List<ScriptableObject>();
            ESSoTableRuleSourceBinding source = GetActiveUseSourceBinding();

            if (source != null && source.soFolder != null)
            {
                if (source.folderAssets == null)
                    source.folderAssets = new List<ScriptableObject>();

                if (source.folderAssets.Count == 0)
                {
                    source.folderAssets = CollectScriptableObjectsFromFolder(source.soFolder, source.includeSubFolders);
                    EditorUtility.SetDirty(this);
                }

                AddMatchingOwners(owners, source.folderAssets);
                return owners;
            }

            if (source != null && source.soAsset != null)
            {
                AddOwnerOrSoDataChildren(owners, source.soAsset);
                return owners;
            }

            AddMatchingOwners(owners, source != null ? source.folderAssets : null);
            return owners;
        }

        private void AddMatchingOwners(List<ScriptableObject> owners, List<ScriptableObject> assets)
        {
            if (assets == null)
                return;

            Type ownerType = typeBinding.RowOwnerType;
            for (int i = 0; i < assets.Count; i++)
            {
                ScriptableObject asset = assets[i];
                if (asset == null)
                    continue;

                if (ownerType == null || ownerType.IsAssignableFrom(asset.GetType()) || asset is ISoDataPack || asset is ISoDataGroup)
                    AddOwnerOrSoDataChildren(owners, asset);
            }
        }

        private void AddOwnerOrSoDataChildren(List<ScriptableObject> owners, ScriptableObject asset)
        {
            if (asset == null)
                return;

            Type ownerType = typeBinding.RowOwnerType;
            if (asset is ISoDataPack pack)
            {
                foreach (object value in pack.AllInfos.Values)
                {
                    if (value is ScriptableObject so && (ownerType == null || ownerType.IsAssignableFrom(so.GetType())))
                        owners.Add(so);
                }
                return;
            }

            if (asset is ISoDataGroup group)
            {
                foreach (ISoDataInfo info in group.AllInfos)
                {
                    if (info is ScriptableObject so && (ownerType == null || ownerType.IsAssignableFrom(so.GetType())))
                        owners.Add(so);
                }
                return;
            }

            if (ownerType == null || ownerType.IsAssignableFrom(asset.GetType()))
                owners.Add(asset);
        }

        private void AppendOwnerRows(List<List<string>> table, ScriptableObject owner, List<ESTableCompiledColumn> compiledColumns)
        {
            if (owner == null)
                return;

            if (rowBinding != null && rowBinding.IsListElementRow)
            {
                IList list = RowBridge.EnsureContainer(owner, rowBinding);
                if (list == null)
                    return;

                for (int i = 0; i < list.Count; i++)
                    table.Add(BuildDataRow(owner, list[i], compiledColumns));

                return;
            }

            table.Add(BuildDataRow(owner, owner, compiledColumns));
        }

        private List<string> BuildDataRow(ScriptableObject owner, object rowObject, List<ESTableCompiledColumn> compiledColumns)
        {
            var row = new List<string>(compiledColumns.Count + 1) { string.Empty };

            for (int i = 0; i < compiledColumns.Count; i++)
            {
                ESTableCompiledColumn column = compiledColumns[i];
                object value = GetColumnValue(owner, rowObject, column);
                row.Add(ConvertCellValue(value, column.map.valueWriteMode));
            }

            return row;
        }

        private object GetColumnValue(ScriptableObject owner, object rowObject, ESTableCompiledColumn column)
        {
            if (column.map.isInfoKey && owner is ISoDataInfo info)
                return info.GetKey();

            if (!column.canRead)
                return null;

            object target = column.useRowObject ? rowObject : owner;
            return ESRowBindingReflectionUtility.GetMemberValue(target, column.memberPath);
        }

        private static string ConvertCellValue(object value, ESTableValueWriteMode writeMode)
        {
            if (value == null)
                return string.Empty;

            if (value is UnityEngine.Object unityObject)
            {
                string path = AssetDatabase.GetAssetPath(unityObject);
                if (writeMode == ESTableValueWriteMode.UnityObjectPath)
                    return path;

                string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
                return string.IsNullOrEmpty(guid) ? unityObject.name : guid;
            }

            if (value is bool boolValue)
                return boolValue ? "true" : "false";
            if (value is float floatValue)
                return floatValue.ToString(CultureInfo.InvariantCulture);
            if (value is double doubleValue)
                return doubleValue.ToString(CultureInfo.InvariantCulture);
            if (value is decimal decimalValue)
                return decimalValue.ToString(CultureInfo.InvariantCulture);
            if (ShouldUseJsonCell(value.GetType()))
                return JsonConvert.SerializeObject(value, CellJsonSettings);
            if (value is IFormattable formattable)
                return formattable.ToString(null, CultureInfo.InvariantCulture);

            return value.ToString();
        }

        private static bool ShouldUseJsonCell(Type type)
        {
            if (type == null)
                return false;
            if (type.IsPrimitive || type.IsEnum)
                return false;
            if (type == typeof(string) || type == typeof(decimal))
                return false;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return false;

            if (typeof(IDictionary).IsAssignableFrom(type))
                return true;
            if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
                return true;

            return type.IsClass || (type.IsValueType && !type.IsPrimitive && !type.IsEnum);
        }

        private string GetOutputPath(string relativeFolder, string extension)
        {
            string root = string.IsNullOrEmpty(GetActiveOutputRoot()) ? "SoTableConfig/Tables" : GetActiveOutputRoot();
            string folder = string.IsNullOrEmpty(relativeFolder) ? string.Empty : relativeFolder;
            string file = string.IsNullOrEmpty(GetActiveFileName()) ? ruleKey : GetActiveFileName();
            if (string.IsNullOrEmpty(file))
                file = "NewTable";

            string path = Path.Combine(Application.dataPath, "..", root, folder, file + extension);
            return Path.GetFullPath(path);
        }

        private string GetOutputFolder(string relativeFolder)
        {
            string root = string.IsNullOrEmpty(GetActiveOutputRoot()) ? "SoTableConfig/Tables" : GetActiveOutputRoot();
            string folder = string.IsNullOrEmpty(relativeFolder) ? string.Empty : relativeFolder;
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", root, folder));
        }

        private string ResolveExistingInputPath()
        {
            ESTableFileKind currentFileKind = GetActiveFileKind();
            string csvPath = GetOutputPath(GetActiveCsvRelativePath(), ".csv");
            string xlsxPath = GetOutputPath(GetActiveXlsxRelativePath(), ".xlsx");

            bool csvExists = File.Exists(csvPath);
            bool xlsxExists = File.Exists(xlsxPath);

            if (currentFileKind == ESTableFileKind.Csv)
                return csvExists ? csvPath : null;
            if (currentFileKind == ESTableFileKind.Xlsx)
                return xlsxExists ? xlsxPath : null;

            if (csvExists && xlsxExists)
                return File.GetLastWriteTimeUtc(csvPath) >= File.GetLastWriteTimeUtc(xlsxPath) ? csvPath : xlsxPath;
            if (csvExists)
                return csvPath;
            if (xlsxExists)
                return xlsxPath;

            return null;
        }

        private ESSoTableRuleUseBatch GetActiveUseBatch()
        {
            if (activeUseBatch != null)
                return activeUseBatch;

            if (useBatches == null || useBatches.Count == 0)
                return null;

            for (int i = 0; i < useBatches.Count; i++)
            {
                if (useBatches[i] != null && useBatches[i].enabled)
                    return useBatches[i];
            }

            return useBatches[0];
        }

        private ESSoTableRuleSourceBinding GetActiveUseSourceBinding()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null ? batch.sourceBinding : null;
        }

        private string GetActiveTableColumnName(ESTableColumnNameMap column)
        {
            if (column == null)
                return string.Empty;

            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            if (batch != null && batch.columnNameMode == ESTableColumnNameMode.Chinese)
                return string.IsNullOrWhiteSpace(column.displayName) ? column.columnName : column.displayName;

            return string.IsNullOrWhiteSpace(column.columnName) ? column.displayName : column.columnName;
        }

        private string GetActiveInfoKeyColumnName()
        {
            if (columns != null)
            {
                for (int i = 0; i < columns.Count; i++)
                {
                    ESTableColumnNameMap column = columns[i];
                    if (column != null && column.isInfoKey)
                        return GetActiveTableColumnName(column);
                }
            }

            return infoKeyColumnName;
        }

        private ESTableFileKind GetActiveFileKind()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null ? batch.fileKind : ESTableFileKind.CsvAndXlsx;
        }

        private string GetActiveFileName()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null && !string.IsNullOrEmpty(batch.fileName) ? batch.fileName : FirstConfiguredName();
        }

        private string GetActiveSheetName()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null && !string.IsNullOrEmpty(batch.sheetName) ? batch.sheetName : FirstConfiguredName();
        }

        private string GetActiveOutputRoot()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null && !string.IsNullOrEmpty(batch.outputRoot) ? batch.outputRoot : "SoTableConfig/Tables";
        }

        private string GetActiveCsvRelativePath()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null && !string.IsNullOrEmpty(batch.csvRelativePath) ? batch.csvRelativePath : "csv";
        }

        private string GetActiveXlsxRelativePath()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null && !string.IsNullOrEmpty(batch.xlsxRelativePath) ? batch.xlsxRelativePath : "xlsx";
        }

        private static List<List<string>> ReadTableFileAuto(string path)
        {
            string extension = Path.GetExtension(path)?.ToLowerInvariant();
            if (extension == ".csv")
                return ReadCsv(path);
            if (extension == ".xlsx")
                return ReadXlsx(path);

            throw new NotSupportedException("Unsupported table type: " + extension);
        }

        private static void WriteCsv(string path, List<List<string>> table)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var builder = new StringBuilder(4096);
            for (int i = 0; i < table.Count; i++)
            {
                List<string> row = table[i];
                for (int j = 0; j < row.Count; j++)
                {
                    if (j > 0)
                        builder.Append(',');
                    builder.Append(EscapeCsv(row[j]));
                }

                builder.AppendLine();
            }

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
        }

        private static List<List<string>> ReadCsv(string path)
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            var table = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool inQuote = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuote)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuote = false;
                        }
                    }
                    else
                    {
                        cell.Append(c);
                    }

                    continue;
                }

                if (c == '"')
                {
                    inQuote = true;
                }
                else if (c == ',')
                {
                    row.Add(cell.ToString());
                    cell.Length = 0;
                }
                else if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;

                    row.Add(cell.ToString());
                    cell.Length = 0;
                    table.Add(row);
                    row = new List<string>();
                }
                else
                {
                    cell.Append(c);
                }
            }

            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                table.Add(row);
            }

            return table;
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            bool needQuote = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            if (!needQuote)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static void WriteXlsx(string path, List<List<string>> table, string sheet)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path))
                File.Delete(path);

            using (FileStream stream = File.Create(path))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                AddZipText(archive, "[Content_Types].xml", BuildContentTypesXml());
                AddZipText(archive, "_rels/.rels", BuildRootRelsXml());
                AddZipText(archive, "xl/workbook.xml", BuildWorkbookXml(sheet));
                AddZipText(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelsXml());
                AddZipText(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(table));
            }
        }

        private static List<List<string>> ReadXlsx(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                List<string> sharedStrings = ReadSharedStrings(archive);
                ZipArchiveEntry sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
                if (sheetEntry == null)
                    return new List<List<string>>();

                using (Stream sheetStream = sheetEntry.Open())
                    return ReadWorksheet(sheetStream, sharedStrings);
            }
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var values = new List<string>();
            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
                return values;

            var doc = new XmlDocument();
            using (Stream stream = entry.Open())
                doc.Load(stream);

            XmlNodeList items = doc.GetElementsByTagName("si", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            for (int i = 0; i < items.Count; i++)
                values.Add(GetCombinedText(items[i]));

            return values;
        }

        private static List<List<string>> ReadWorksheet(Stream sheetStream, List<string> sharedStrings)
        {
            var doc = new XmlDocument();
            doc.Load(sheetStream);

            var table = new List<List<string>>();
            XmlNodeList rows = doc.GetElementsByTagName("row", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            for (int i = 0; i < rows.Count; i++)
            {
                var row = new List<string>();
                foreach (XmlNode cellNode in rows[i].ChildNodes)
                {
                    if (cellNode.LocalName != "c")
                        continue;

                    int columnIndex = GetCellColumnIndex(cellNode.Attributes?["r"]?.Value);
                    while (row.Count < columnIndex)
                        row.Add(string.Empty);

                    row.Add(ReadCellValue(cellNode, sharedStrings));
                }

                table.Add(row);
            }

            return table;
        }

        private static string ReadCellValue(XmlNode cellNode, List<string> sharedStrings)
        {
            string type = cellNode.Attributes?["t"]?.Value;
            if (type == "inlineStr")
            {
                XmlNode inlineNode = FindFirstChild(cellNode, "is");
                return inlineNode != null ? GetCombinedText(inlineNode) : string.Empty;
            }

            XmlNode valueNode = FindFirstChild(cellNode, "v");
            string raw = valueNode?.InnerText ?? string.Empty;
            if (type == "s" && int.TryParse(raw, out int sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
                return sharedStrings[sharedIndex];

            return raw;
        }

        private static XmlNode FindFirstChild(XmlNode node, string localName)
        {
            if (node == null)
                return null;

            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.LocalName == localName)
                    return child;
            }

            return null;
        }

        private static string GetCombinedText(XmlNode node)
        {
            if (node == null)
                return string.Empty;

            var builder = new StringBuilder();
            AppendTextNodes(node, builder);
            return builder.ToString();
        }

        private static void AppendTextNodes(XmlNode node, StringBuilder builder)
        {
            if (node.LocalName == "t")
                builder.Append(node.InnerText);

            foreach (XmlNode child in node.ChildNodes)
                AppendTextNodes(child, builder);
        }

        private static int GetCellColumnIndex(string cellRef)
        {
            if (string.IsNullOrEmpty(cellRef))
                return 0;

            int column = 0;
            for (int i = 0; i < cellRef.Length; i++)
            {
                char c = cellRef[i];
                if (c < 'A' || c > 'Z')
                    break;

                column = column * 26 + (c - 'A' + 1);
            }

            return Math.Max(0, column - 1);
        }

        private static void AddZipText(ZipArchive archive, string path, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path);
            using (Stream entryStream = entry.Open())
            using (var writer = new StreamWriter(entryStream, new UTF8Encoding(false)))
                writer.Write(content);
        }

        private static string BuildContentTypesXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                   "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                   "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                   "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                   "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                   "</Types>";
        }

        private static string BuildRootRelsXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                   "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                   "</Relationships>";
        }

        private static string BuildWorkbookXml(string sheet)
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                   "<sheets><sheet name=\"" + EscapeXml(sheet) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
                   "</workbook>";
        }

        private static string BuildWorkbookRelsXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                   "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                   "</Relationships>";
        }

        private static string BuildWorksheetXml(List<List<string>> table)
        {
            var builder = new StringBuilder(8192);
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

            for (int rowIndex = 0; rowIndex < table.Count; rowIndex++)
            {
                builder.Append("<row r=\"").Append(rowIndex + 1).Append("\">");
                List<string> row = table[rowIndex];
                for (int columnIndex = 0; columnIndex < row.Count; columnIndex++)
                {
                    string cellRef = GetExcelColumnName(columnIndex + 1) + (rowIndex + 1).ToString(CultureInfo.InvariantCulture);
                    builder.Append("<c r=\"").Append(cellRef).Append("\" t=\"inlineStr\"><is><t>");
                    builder.Append(EscapeXml(row[columnIndex]));
                    builder.Append("</t></is></c>");
                }
                builder.Append("</row>");
            }

            builder.Append("</sheetData></worksheet>");
            return builder.ToString();
        }

        private static string GetExcelColumnName(int columnNumber)
        {
            var columnName = new StringBuilder();
            while (columnNumber > 0)
            {
                int modulo = (columnNumber - 1) % 26;
                columnName.Insert(0, (char)('A' + modulo));
                columnNumber = (columnNumber - modulo) / 26;
            }

            return columnName.ToString();
        }

        private static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private bool TryGetReflectionPathForColumn(string soFieldPath, Type rowOwnerType, Type listElementType, out Type ownerType, out string memberPath)
        {
            ownerType = rowOwnerType;
            memberPath = soFieldPath;

            if (rowBinding == null || !rowBinding.IsListElementRow || listElementType == null)
                return ownerType != null && !string.IsNullOrWhiteSpace(memberPath);

            string listPrefix = rowBinding.listFieldPath + "[].";
            if (soFieldPath.StartsWith(listPrefix, StringComparison.Ordinal))
            {
                ownerType = listElementType;
                memberPath = soFieldPath.Substring(listPrefix.Length);
                return !string.IsNullOrWhiteSpace(memberPath);
            }

            if (soFieldPath == rowBinding.listFieldPath || soFieldPath == rowBinding.listFieldPath + "[]")
                return false;

            return ownerType != null && !string.IsNullOrWhiteSpace(memberPath);
        }

        private void RebuildColumnsFromTypeFields(Type dataType, bool listElementField)
        {
            RebuildColumnsFromTypeFields(dataType, listElementField, string.Empty, string.Empty, 0);
        }

        private void RebuildColumnsFromTypeFields(Type dataType, bool listElementField, string fieldPrefix, string columnPrefix, int depth)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo[] fields = dataType.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!ShouldExportField(field))
                    continue;

                if (field.DeclaringType == typeof(SoDataInfo) && field.Name == nameof(SoDataInfo.KeyName))
                    continue;
                if (listElementField && rowBinding != null && field.Name == rowBinding.elementKeyFieldPath)
                    continue;

                string fieldPath = CombineFieldPath(fieldPrefix, field.Name);
                string columnName = CombineColumnName(columnPrefix, field.Name);
                if (ShouldExpandNestedField(field.FieldType, depth))
                {
                    RebuildColumnsFromTypeFields(field.FieldType, listElementField, fieldPath, columnName, depth + 1);
                    continue;
                }

                columns.Add(new ESTableColumnNameMap
                {
                    soFieldPath = listElementField ? BuildListElementPath(fieldPath) : fieldPath,
                    columnName = columnName,
                    displayName = fieldPath,
                    tableType = ESSoTableRuleTypeUtility.GuessTableType(field.FieldType),
                    valueWriteMode = ESSoTableRuleTypeUtility.GuessValueWriteMode(field.FieldType)
                });
            }
        }

        private bool ShouldExpandNestedField(Type fieldType, int depth)
        {
            if (nestedFieldRule == null || !nestedFieldRule.expandNestedFields)
                return false;
            if (depth >= nestedFieldRule.maxDepth)
                return false;

            return ESSoTableRuleTypeUtility.CanExpandAsNestedObject(fieldType);
        }

        private static string CombineFieldPath(string prefix, string fieldName)
        {
            return string.IsNullOrEmpty(prefix) ? fieldName : prefix + "." + fieldName;
        }

        private string CombineColumnName(string prefix, string fieldName)
        {
            if (string.IsNullOrEmpty(prefix))
                return fieldName;

            string separator = nestedFieldRule != null && !string.IsNullOrEmpty(nestedFieldRule.columnSeparator)
                ? nestedFieldRule.columnSeparator
                : "_";
            return prefix + separator + fieldName;
        }

        private string BuildListElementPath(string elementFieldPath)
        {
            if (rowBinding == null || string.IsNullOrEmpty(rowBinding.listFieldPath))
                return elementFieldPath;

            if (string.IsNullOrEmpty(elementFieldPath))
                return rowBinding.listFieldPath;

            return rowBinding.listFieldPath + "[]." + elementFieldPath;
        }

        private static bool ShouldExportField(FieldInfo field)
        {
            if (field == null || field.IsStatic || field.IsLiteral || field.IsInitOnly || field.IsNotSerialized)
                return false;

            if (Attribute.IsDefined(field, typeof(NonSerializedAttribute)))
                return false;

            if (HasOdinSerializeAttribute(field))
                return true;

            bool unitySerializedByVisibility = field.IsPublic || Attribute.IsDefined(field, typeof(SerializeField));
            return unitySerializedByVisibility && IsUnitySerializableFieldType(field.FieldType);
        }

        private static bool HasOdinSerializeAttribute(FieldInfo field)
        {
            object[] attributes = field.GetCustomAttributes(false);
            for (int i = 0; i < attributes.Length; i++)
            {
                if (attributes[i].GetType().FullName == "Sirenix.Serialization.OdinSerializeAttribute")
                    return true;
            }

            return false;
        }

        private static bool IsUnitySerializableFieldType(Type type)
        {
            if (type == null || type.IsPointer)
                return false;

            if (type.IsPrimitive || type.IsEnum || type == typeof(string))
                return true;

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return true;

            if (type.IsArray)
                return type.GetArrayRank() == 1 && IsUnitySerializableFieldType(type.GetElementType());

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return IsUnitySerializableFieldType(type.GetGenericArguments()[0]);

            if (type.IsClass || type.IsValueType)
                return Attribute.IsDefined(type, typeof(SerializableAttribute));

            return false;
        }
    }

    [CustomEditor(typeof(ESSoTableDataRule))]
    public sealed class ESSoTableDataRuleEditor : Editor
    {
        private GUIStyle _heroStyle;
        private GUIStyle _heroTitleStyle;
        private GUIStyle _heroSubTitleStyle;
        private GUIStyle _cardStyle;
        private GUIStyle _foldoutTitleStyle;
        private GUIStyle _foldoutHintStyle;
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _pillStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _metricValueStyle;
        private readonly ESDropZoneSolver _tablePathDropZone = new ESDropZoneSolver();
        private readonly ESDropZoneSolver _folderPathDropZone = new ESDropZoneSolver();
        private string _prefsKeyPrefix;
        private bool _showBasic = true;
        private bool _showBuildStage = true;
        private bool _showUseStage = true;
        private bool _showColumns = true;
        private bool _showAdvanced;
        private bool _showAdvancedExpert;
        private bool _showTypeCache;

        private void OnEnable()
        {
            _tablePathDropZone.InitSolver<UnityEngine.Object>(allowFolderExpand: false, rejectScripts: false, maxCount: 1);
            _folderPathDropZone.InitSolver<DefaultAsset>(allowFolderExpand: false, rejectScripts: true, maxCount: 1);
            _prefsKeyPrefix = BuildPrefsKeyPrefix(target);
            LoadFoldoutPrefs();
        }

        private static string BuildPrefsKeyPrefix(UnityEngine.Object editorTarget)
        {
            string assetPath = editorTarget != null ? AssetDatabase.GetAssetPath(editorTarget) : string.Empty;
            string guid = string.IsNullOrEmpty(assetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
            string identity = string.IsNullOrEmpty(guid) && editorTarget != null ? editorTarget.GetInstanceID().ToString(CultureInfo.InvariantCulture) : guid;
            return "ES.ESSoTableDataRuleEditor." + identity + ".";
        }

        private void LoadFoldoutPrefs()
        {
            _showBasic = LoadFoldoutPref("basic", _showBasic);
            _showBuildStage = LoadFoldoutPref("build", _showBuildStage);
            _showUseStage = LoadFoldoutPref("use", _showUseStage);
            _showColumns = LoadFoldoutPref("columns", _showColumns);
            _showAdvanced = LoadFoldoutPref("advanced", _showAdvanced);
            _showAdvancedExpert = LoadFoldoutPref("advancedExpert", _showAdvancedExpert);
            _showTypeCache = LoadFoldoutPref("typeCache", _showTypeCache);
        }

        private bool LoadFoldoutPref(string key, bool defaultValue)
        {
            return EditorPrefs.GetBool(_prefsKeyPrefix + key, defaultValue);
        }

        private void SaveFoldoutPref(string key, bool value)
        {
            EditorPrefs.SetBool(_prefsKeyPrefix + key, value);
        }

        public override void OnInspectorGUI()
        {
            var rule = target as ESSoTableDataRule;
            if (rule == null || targets.Length != 1)
            {
                base.OnInspectorGUI();
                return;
            }

            EnsureStyles();
            DrawHeader(rule);
            DrawQuickActions(rule);
            DrawOverview(rule);
            DrawWarnings(rule);
            EditorGUILayout.Space(8);
            DrawChineseFields();
        }

        private void EnsureStyles()
        {
            if (_heroStyle != null)
                return;

            _heroStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(14, 14, 12, 12),
                margin = new RectOffset(0, 0, 4, 8)
            };

            _heroTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft
            };

            _heroSubTitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true
            };

            _cardStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 4, 4)
            };

            _foldoutTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(6, 6, 0, 0)
            };

            _foldoutHintStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                wordWrap = true,
                padding = new RectOffset(9, 9, 5, 7)
            };

            _sectionTitleStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleLeft
            };

            _pillStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 20,
                padding = new RectOffset(8, 8, 2, 2)
            };

            _mutedStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true
            };

            _metricValueStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleRight
            };
        }

        private void DrawHeader(ESSoTableDataRule rule)
        {
            using (new EditorGUILayout.VerticalScope(_heroStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        string title = string.IsNullOrWhiteSpace(rule.ruleKey) ? rule.name : rule.ruleKey;
                        EditorGUILayout.LabelField(title, _heroTitleStyle);
                        EditorGUILayout.LabelField(BuildHeroSubtitle(rule), _heroSubTitleStyle);
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.Label(rule.enabled ? "\u5df2\u542f\u7528" : "\u5df2\u505c\u7528", _pillStyle, GUILayout.Width(82));
                }
            }
        }

        private string BuildHeroSubtitle(ESSoTableDataRule rule)
        {
            string source = GetSourceSummary(rule);
            string table = string.IsNullOrWhiteSpace(rule.tableName)
                ? FirstNotEmpty(GetBatchFileName(rule), rule.ruleKey, "\u672a\u8bbe\u7f6e\u8868\u540d")
                : rule.tableName;

            return "SO 表格 \u8868\u683c\u89c4\u5219  |  " + source + "  |  " + table;
        }

        private void DrawQuickActions(ESSoTableDataRule rule)
        {
            using (new EditorGUILayout.VerticalScope(_cardStyle))
            {
                EditorGUILayout.LabelField("Rule \u6784\u5efa\u64cd\u4f5c", _sectionTitleStyle);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    if (GUILayout.Button("\u4ece\u5f53\u524d\u9009\u62e9\u751f\u6210", EditorStyles.toolbarButton))
                        ExecuteAndExit(rule, rule.BindAndGenerateFromSelection);

                    using (new EditorGUI.DisabledScope(!HasAnySource(rule)))
                    {
                        if (GUILayout.Button("\u4ece\u7ed1\u5b9a\u6765\u6e90\u751f\u6210", EditorStyles.toolbarButton))
                            ExecuteAndExit(rule, () => BindPreferredSource(rule));
                    }

                    if (GUILayout.Button("\u91cd\u5efa\u5b57\u6bb5\u6620\u5c04", EditorStyles.toolbarButton))
                        ExecuteAndExit(rule, rule.RebuildColumnsFromInfoFields);

                    if (GUILayout.Button("\u9884\u70ed\u53cd\u5c04\u7f13\u5b58", EditorStyles.toolbarButton))
                        ExecuteAndExit(rule, rule.PrewarmReflectionCache);
                    if (GUILayout.Button("\u4ece\u8868\u683c\u8868\u5934\u6784\u5efa", EditorStyles.toolbarButton))
                        ExecuteAndExit(rule, rule.RebuildColumnsFromBuildTable);

                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.LabelField("Rule \u4f7f\u7528\u64cd\u4f5c", _sectionTitleStyle);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    if (GUILayout.Button("\u65b0\u589e\u6279\u6b21", EditorStyles.toolbarButton, GUILayout.Width(90)))
                        ExecuteAndExit(rule, rule.AddUseBatch);

                    using (new EditorGUI.DisabledScope(!HasColumns(rule)))
                    {
                        if (GUILayout.Button("\u6267\u884c\u5168\u90e8\u542f\u7528\u6279\u6b21", EditorStyles.toolbarButton, GUILayout.Width(160)))
                            ExecuteAndExit(rule, rule.ExecuteAllEnabledBatches);
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private static void ExecuteAndExit(ESSoTableDataRule rule, Action action)
        {
            if (action == null)
                return;

            action();
            EditorUtility.SetDirty(rule);
            GUIUtility.ExitGUI();
        }

        private void DrawOverview(ESSoTableDataRule rule)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetricCard("\u5b57\u6bb5\u6620\u5c04", GetColumnCount(rule).ToString(), "\u5df2\u542f\u7528 " + GetEnabledColumnCount(rule));
                DrawMetricCard("\u7ed1\u5b9a\u6765\u6e90", GetSourceKind(rule), ShortenMiddle(GetSourcePath(rule), 36));
                DrawMetricCard("\u7c7b\u578b", GetInfoTypeName(rule), GetPackGroupSummary(rule));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetricCard("\u8f93\u51fa", GetOutputMode(rule), ShortenMiddle(GetOutputPath(rule), 42));
                DrawMetricCard("\u547d\u540d", FirstNotEmpty(rule.tableName, GetBatchFileName(rule), "-"), FirstNotEmpty(rule.beanName, GetBatchSheetName(rule), "-"));
                DrawMetricCard("\u6279\u6b21", GetBatchCountText(rule), GetBatchPolicyText(rule));
            }
        }

        private void DrawMetricCard(string title, string value, string detail)
        {
            using (new EditorGUILayout.VerticalScope(_cardStyle, GUILayout.MinHeight(58)))
            {
                EditorGUILayout.LabelField(title, _sectionTitleStyle);
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(value) ? "-" : value, _metricValueStyle);
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(detail) ? "-" : detail, _mutedStyle);
            }
        }

        private void DrawWarnings(ESSoTableDataRule rule)
        {
            if (!rule.enabled)
                EditorGUILayout.HelpBox("\u5f53\u524d\u89c4\u5219\u5df2\u505c\u7528\u3002\u4ecd\u7136\u53ef\u4ee5\u4ece\u8fd9\u4e2a\u9762\u677f\u624b\u52a8\u6267\u884c\u5bfc\u5165\u5bfc\u51fa\u3002", MessageType.Info);

            if (!HasAnySource(rule))
                EditorGUILayout.HelpBox("\u8fd8\u6ca1\u6709\u6307\u5b9a SO\u3001\u6587\u4ef6\u5939\u6216\u811a\u672c\u3002\u53ef\u4ee5\u5148\u5728\u7ed1\u5b9a\u533a\u57df\u62d6\u5165\u6765\u6e90\uff0c\u6216\u5728 Project \u91cc\u9009\u4e2d\u5bf9\u8c61\u540e\u70b9\u201c\u4ece\u5f53\u524d\u9009\u62e9\u751f\u6210\u201d\u3002", MessageType.Warning);

            if (!HasColumns(rule))
                EditorGUILayout.HelpBox("\u8fd8\u6ca1\u6709\u5b57\u6bb5\u6620\u5c04\u3002\u5bfc\u51fa\u524d\u9700\u8981\u5148\u7ed1\u5b9a\u6765\u6e90\uff0c\u6216\u70b9\u51fb\u201c\u91cd\u5efa\u5b57\u6bb5\u6620\u5c04\u201d\u3002", MessageType.Warning);

            Type packType;
            Type groupType;
            Type infoType;
            if (!rule.TryGetTargetTypes(out packType, out groupType, out infoType))
                EditorGUILayout.HelpBox("Pack\u3001Group \u6216 Info \u7c7b\u578b\u8fd8\u6ca1\u6709\u5b8c\u6574\u89e3\u6790\u3002\u901a\u5e38\u4ece\u6709\u6548\u7684 SoData \u6765\u6e90\u91cd\u65b0\u751f\u6210\u5373\u53ef\u3002", MessageType.Info);
        }

        private void DrawChineseFields()
        {
            serializedObject.Update();

            DrawFoldout("basic", ref _showBasic, "\u57fa\u7840\u914d\u7f6e", "\u8fd9\u91cc\u53ea\u653e Rule \u81ea\u8eab\u7684\u542f\u7528\u3001Key \u548c\u8bf4\u660e\uff0c\u4e0d\u51b3\u5b9a\u5b57\u6bb5\u548c\u6279\u6b21\u3002", DrawBasicFields);
            DrawFoldout("build", ref _showBuildStage, "Rule \u6784\u5efa\u9636\u6bb5", "\u7528 SO\u3001MonoScript \u6216\u8868\u683c\u6837\u672c\u751f\u6210\u5b57\u6bb5\u6620\u5c04\uff1b\u53ea\u6784\u5efa\u89c4\u5219\uff0c\u4e0d\u6267\u884c\u6279\u91cf\u5bfc\u5165\u5bfc\u51fa\u3002", DrawBuildStageFields);
            DrawFoldout("use", ref _showUseStage, "Rule \u4f7f\u7528\u9636\u6bb5", "\u914d\u7f6e\u4e00\u4e2a\u6216\u591a\u4e2a\u6267\u884c\u6279\u6b21\uff1a\u5904\u7406\u54ea\u4e9b SO\u3001\u8f93\u51fa\u5230\u54ea\u4e2a\u8868\u683c\u3001\u6309\u4ec0\u4e48\u8303\u56f4\u5199\u56de\u3002", DrawUseStageFields);
            DrawFoldout("columns", ref _showColumns, "\u5b57\u6bb5\u6620\u5c04", "\u5b9a\u4e49 SO \u5b57\u6bb5\u4e0e\u8868\u683c\u5217\u7684\u5bf9\u5e94\u5173\u7cfb\uff1b\u9501\u5b9a\u3001\u6743\u5a01\u3001\u6392\u5e8f\u90fd\u5728\u8fd9\u91cc\u7ba1\u3002", DrawColumnFields);
            DrawFoldout("advanced", ref _showAdvanced, "\u9ad8\u7ea7\u914d\u7f6e", "\u65e5\u5e38\u5148\u7528\u5feb\u901f\u65b9\u6848\u3002\u53ea\u6709\u884c\u7ed1\u5b9a\u3001\u5b50\u5bf9\u8c61\u5c55\u5f00\u3001\u8868\u5934\u6a21\u677f\u9700\u8981\u8c03\u65f6\u624d\u8fdb\u4e13\u5bb6\u8be6\u60c5\u3002", DrawAdvancedFields);
            DrawFoldout("typeCache", ref _showTypeCache, "\u7c7b\u578b\u7f13\u5b58", "\u8fd9\u662f\u4ece\u6784\u5efa\u6765\u6e90\u89e3\u6790\u51fa\u7684\u7c7b\u578b\u7ed3\u679c\uff0c\u901a\u5e38\u53ea\u7528\u67e5\u770b\uff0c\u4e0d\u624b\u52a8\u6539\u3002", DrawTypeCacheFields);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFoldout(string prefsKey, ref bool expanded, string title, string hint, Action drawContent)
        {
            using (new EditorGUILayout.VerticalScope(_cardStyle))
            {
                bool oldExpanded = expanded;
                Rect headerRect = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));
                Color headerColor = expanded
                    ? new Color(0.18f, 0.29f, 0.38f, 0.98f)
                    : new Color(0.13f, 0.16f, 0.20f, 0.98f);
                EditorGUI.DrawRect(headerRect, headerColor);
                EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 5, headerRect.height), new Color(0.32f, 0.72f, 0.92f, 1f));

                Event current = Event.current;
                if (current != null && current.type == EventType.MouseDown && headerRect.Contains(current.mousePosition))
                {
                    expanded = !expanded;
                    current.Use();
                }

                Rect foldoutRect = new Rect(headerRect.x + 9, headerRect.y + 6, 18, 20);
                expanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, true);
                if (expanded != oldExpanded)
                    SaveFoldoutPref(prefsKey, expanded);

                Rect titleRect = new Rect(headerRect.x + 29, headerRect.y + 6, headerRect.width - 36, 20);
                GUI.Label(titleRect, title, _foldoutTitleStyle);

                if (!string.IsNullOrWhiteSpace(hint))
                {
                    Rect hintRect = GUILayoutUtility.GetRect(0, 38, GUILayout.ExpandWidth(true));
                    EditorGUI.DrawRect(hintRect, expanded ? new Color(0.10f, 0.13f, 0.16f, 0.92f) : new Color(0.09f, 0.10f, 0.12f, 0.82f));
                    GUI.Label(hintRect, hint, _foldoutHintStyle);
                }

                if (!expanded)
                    return;

                EditorGUILayout.Space(4);
                drawContent?.Invoke();
            }
        }

        private void DrawBasicFields()
        {
            DrawProperty("enabled", "\u542f\u7528");
            DrawProperty("ruleKey", "\u89c4\u5219 Key");
            DrawProperty("description", "\u89c4\u5219\u8bf4\u660e");
        }

        private void DrawBuildStageFields()
        {
            SerializedProperty buildStageProperty = serializedObject.FindProperty("buildStage");
            if (buildStageProperty == null)
                return;

            SerializedProperty source = buildStageProperty.FindPropertyRelative("sourceBinding");
            if (source != null)
            {
                EditorGUILayout.LabelField("\u6784\u5efa\u6765\u6e90\uff08\u7528\u6765\u751f\u6210\u89c4\u5219\uff09", _sectionTitleStyle);
                DrawChild(source, "soAsset", "\u5355\u4e2a SO \u6837\u672c");
                DrawChild(source, "monoScript", "\u811a\u672c\u7c7b\u578b");
            }

            DrawPathProperty(buildStageProperty.FindPropertyRelative("tableFilePath"), "\u8868\u683c\u6837\u672c\u8def\u5f84", true, "csv,xlsx");
            DrawChild(buildStageProperty, "allowTableHeaderOverride", "\u5141\u8bb8\u8868\u5934\u8986\u76d6\u5b57\u6bb5\u6620\u5c04");

            EditorGUILayout.HelpBox("\u6784\u5efa\u9636\u6bb5\u53ea\u8d1f\u8d23\u751f\u6210\u89c4\u5219\uff1a\u7528\u5355\u4e2a SO\u3001\u811a\u672c\u6216\u8868\u683c\u8868\u5934\u63a8\u5bfc\u5b57\u6bb5\u6620\u5c04\u3002\u4e0d\u5728\u8fd9\u91cc\u914d\u6279\u91cf\u5bfc\u5165\u5bfc\u51fa\u6570\u636e\u3002", MessageType.Info);
        }

        private void DrawUseStageFields()
        {
            SerializedProperty batches = serializedObject.FindProperty("useBatches");
            if (batches == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("\u6279\u6b21\u6570\u91cf\uff1a" + batches.arraySize, _sectionTitleStyle);
                if (GUILayout.Button("\u65b0\u589e\u6279\u6b21", EditorStyles.miniButton, GUILayout.Width(82)))
                {
                    serializedObject.ApplyModifiedProperties();
                    ((ESSoTableDataRule)target).AddUseBatch();
                    serializedObject.Update();
                }
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("\u542f\u7528\u5168\u90e8", EditorStyles.toolbarButton, GUILayout.Width(76)))
                    SetAllBatchesEnabled(batches, true);
                if (GUILayout.Button("\u505c\u7528\u5168\u90e8", EditorStyles.toolbarButton, GUILayout.Width(76)))
                    SetAllBatchesEnabled(batches, false);
                if (GUILayout.Button("\u8865\u9f50\u9ed8\u8ba4\u8def\u5f84", EditorStyles.toolbarButton, GUILayout.Width(108)))
                    ApplyDefaultPathToAllBatches(batches, (ESSoTableDataRule)target);
                GUILayout.FlexibleSpace();
            }

            for (int i = 0; i < batches.arraySize; i++)
            {
                SerializedProperty batch = batches.GetArrayElementAtIndex(i);
                SerializedProperty batchName = batch.FindPropertyRelative("batchName");
                string title = "\u6279\u6b21 " + (i + 1);
                if (batchName != null && !string.IsNullOrEmpty(batchName.stringValue))
                    title += "  " + batchName.stringValue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        batch.isExpanded = EditorGUILayout.Foldout(batch.isExpanded, title, true);
                        using (new EditorGUI.DisabledScope(!HasColumns((ESSoTableDataRule)target)))
                        {
                            if (GUILayout.Button("\u6267\u884c", EditorStyles.miniButton, GUILayout.Width(52)))
                            {
                                serializedObject.ApplyModifiedProperties();
                                ((ESSoTableDataRule)target).ExecuteUseBatch(((ESSoTableDataRule)target).useBatches[i]);
                                GUIUtility.ExitGUI();
                            }
                        }

                        using (new EditorGUI.DisabledScope(i == 0))
                        {
                            if (GUILayout.Button("\u4e0a\u79fb", EditorStyles.miniButton, GUILayout.Width(44)))
                            {
                                batches.MoveArrayElement(i, i - 1);
                                break;
                            }
                        }

                        using (new EditorGUI.DisabledScope(i >= batches.arraySize - 1))
                        {
                            if (GUILayout.Button("\u4e0b\u79fb", EditorStyles.miniButton, GUILayout.Width(44)))
                            {
                                batches.MoveArrayElement(i, i + 1);
                                break;
                            }
                        }

                        if (GUILayout.Button("\u590d\u5236", EditorStyles.miniButton, GUILayout.Width(44)))
                        {
                            batches.InsertArrayElementAtIndex(i);
                            SerializedProperty copy = batches.GetArrayElementAtIndex(i + 1);
                            SerializedProperty copyName = copy.FindPropertyRelative("batchName");
                            if (copyName != null)
                                copyName.stringValue = FirstNotEmpty(copyName.stringValue, "\u6279\u6b21") + " \u526f\u672c";
                            break;
                        }

                        if (GUILayout.Button("\u5220\u9664", EditorStyles.miniButton, GUILayout.Width(52)))
                        {
                            batches.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }

                    if (!batch.isExpanded)
                        continue;

                    DrawChild(batch, "enabled", "\u542f\u7528");
                    DrawChild(batch, "batchName", "\u6279\u6b21\u540d");
                    DrawChildEnum(batch, "direction", "\u6267\u884c\u65b9\u5411", new[] { "\u5bfc\u51fa", "\u5bfc\u5165", "\u5bfc\u5165\u5e76\u5bfc\u51fa" });

                    SerializedProperty source = batch.FindPropertyRelative("sourceBinding");
                    if (source != null)
                    {
                        EditorGUILayout.LabelField("\u6570\u636e\u6765\u6e90\uff08\u8fd9\u4e00\u6279\u8981\u5904\u7406\u54ea\u4e9b SO\uff09", _sectionTitleStyle);
                        DrawChild(source, "soAsset", "SO \u6587\u4ef6");
                        DrawChild(source, "soFolder", "SO \u6587\u4ef6\u5939");
                        DrawChild(source, "includeSubFolders", "\u5305\u542b\u5b50\u6587\u4ef6\u5939");
                        DrawChildEnum(source, "folderSyncMode", "\u6587\u4ef6\u5939\u540c\u6b65\u6a21\u5f0f", new[] { "\u53ea\u6bd4\u5bf9", "\u589e\u91cf\u540c\u6b65", "\u91cd\u5efa\u751f\u6210" });
                    }

                    EditorGUILayout.LabelField("\u8868\u683c\u8def\u5f84", _sectionTitleStyle);
                    DrawChildEnum(batch, "fileKind", "\u6587\u4ef6\u683c\u5f0f", "\u8fd9\u4e00\u6279\u5bfc\u5165\u6216\u5bfc\u51fa CSV\u3001XLSX\uff0c\u6216\u4e24\u8005\u540c\u65f6\u751f\u6210\u3002", new[] { "CSV", "XLSX", "CSV \u548c XLSX" });
                    DrawChildEnum(batch, "columnNameMode", "\u8868\u683c\u5217\u540d", "\u82f1\u6587\u5217\u540d\u4f7f\u7528\u6620\u5c04\u7684\u8868\u683c\u5217\u540d\uff1b\u4e2d\u6587\u663e\u793a\u540d\u4f7f\u7528 SO \u663e\u793a\u540d\u4f5c\u4e3a\u8868\u5934\u5339\u914d\u3002", new[] { "\u82f1\u6587\u5217\u540d", "\u4e2d\u6587\u663e\u793a\u540d" });
                    DrawChild(batch, "fileName", "\u6587\u4ef6\u540d", "\u8868\u683c\u6587\u4ef6\u540d\uff0c\u4e0d\u9700\u8981\u586b .csv \u6216 .xlsx \u6269\u5c55\u540d\u3002");
                    DrawChild(batch, "sheetName", "Sheet \u540d", "XLSX \u7684 Sheet \u540d\u3002CSV \u4e0d\u4f7f\u7528\u8fd9\u4e2a\u5b57\u6bb5\u3002");
                    DrawPathProperty(batch.FindPropertyRelative("outputRoot"), "\u8f93\u51fa\u6839\u76ee\u5f55", false, string.Empty);
                    DrawChild(batch, "csvRelativePath", "CSV \u76f8\u5bf9\u8def\u5f84", "\u76f8\u5bf9\u8f93\u51fa\u6839\u76ee\u5f55\u7684 CSV \u5b50\u76ee\u5f55\u3002");
                    DrawChild(batch, "xlsxRelativePath", "XLSX \u76f8\u5bf9\u8def\u5f84", "\u76f8\u5bf9\u8f93\u51fa\u6839\u76ee\u5f55\u7684 XLSX \u5b50\u76ee\u5f55\u3002");
                    if (GUILayout.Button("\u5e94\u7528\u9ed8\u8ba4\u8868\u683c\u8def\u5f84", EditorStyles.miniButton, GUILayout.Width(150)))
                        ApplyDefaultBatchPath(batch, (ESSoTableDataRule)target);

                    EditorGUILayout.LabelField("\u6279\u6b21\u7b56\u7565", _sectionTitleStyle);
                    DrawChildEnum(batch, "importConflictPolicy", "\u5bfc\u5165\u51b2\u7a81", "\u8868\u683c\u5199\u56de SO \u65f6\u9047\u5230\u5df2\u6709\u6570\u636e\u6216\u51b2\u7a81\u9879\u7684\u5904\u7406\u65b9\u5f0f\u3002", new[] { "\u8df3\u8fc7", "\u8986\u76d6", "\u521b\u5efa\u526f\u672c", "\u62a5\u9519" });
                    DrawChildEnum(batch, "exportConflictPolicy", "\u5bfc\u51fa\u51b2\u7a81", "\u5bfc\u51fa\u8868\u683c\u65f6\u5982\u679c\u76ee\u6807\u6587\u4ef6\u5df2\u5b58\u5728\uff0c\u91c7\u7528\u54ea\u79cd\u5904\u7406\u65b9\u5f0f\u3002", new[] { "\u8df3\u8fc7", "\u8986\u76d6", "\u521b\u5efa\u526f\u672c", "\u62a5\u9519" });

                    EditorGUILayout.LabelField("\u5e94\u7528\u8303\u56f4\uff08\u5bfc\u5165\u5199\u56de\uff09", _sectionTitleStyle);
                    DrawChildEnum(batch, "applyRangeMode", "\u8303\u56f4", "\u5bfc\u5165\u5199\u56de\u65f6\u662f\u5e94\u7528\u6574\u5f20\u8868\uff0c\u8fd8\u662f\u53ea\u6309\u67d0\u5217\u7684\u8d77\u6b62\u503c\u5e94\u7528\u4e00\u6bb5\uff0c\u6216\u53ea\u5e94\u7528\u4e00\u4e2a Group/Info\u3002", new[] { "\u5168\u91cf\u5e94\u7528", "\u7247\u6bb5\u622a\u53d6", "\u5355\u4e2a Group/Info" });
                    SerializedProperty range = batch.FindPropertyRelative("applyRangeMode");
                    if (range != null && range.enumValueIndex == (int)ESTableBatchApplyRangeMode.Slice)
                    {
                        DrawChild(batch, "sliceColumnName", "\u622a\u53d6\u5217\u540d", "\u7528\u54ea\u4e00\u5217\u6765\u5224\u65ad\u7247\u6bb5\u8d77\u6b62\u3002\u7559\u7a7a\u65f6\u4f7f\u7528 Info Key \u5217\u3002");
                        DrawChild(batch, "sliceStartValue", "\u8d77\u70b9\u503c", "\u5728\u622a\u53d6\u5217\u91cc\u627e\u5230\u8fd9\u4e2a\u503c\u540e\u5f00\u59cb\u5199\u56de\u3002");
                        DrawChild(batch, "sliceEndValue", "\u7ec8\u70b9\u503c", "\u5728\u622a\u53d6\u5217\u91cc\u627e\u5230\u8fd9\u4e2a\u503c\u540e\u505c\u6b62\u5199\u56de\u3002");
                        DrawChild(batch, "includeSliceStart", "\u5305\u542b\u8d77\u70b9", "\u8d77\u70b9\u884c\u672c\u8eab\u662f\u5426\u4e5f\u8981\u5199\u56de\u3002");
                        DrawChild(batch, "includeSliceEnd", "\u5305\u542b\u7ec8\u70b9", "\u7ec8\u70b9\u884c\u672c\u8eab\u662f\u5426\u4e5f\u8981\u5199\u56de\u3002");
                    }
                    else if (range != null && range.enumValueIndex == (int)ESTableBatchApplyRangeMode.SingleGroupInfo)
                    {
                        DrawChild(batch, "targetGroupKey", "\u76ee\u6807 Group", "\u53ea\u5199\u56de\u8fd9\u4e2a Group \u4e0b\u7684\u6570\u636e\u3002\u7559\u7a7a\u5219\u4e0d\u9650\u5236 Group\u3002");
                        DrawChild(batch, "targetInfoKey", "\u76ee\u6807 Info", "\u53ea\u5199\u56de\u8fd9\u4e2a Info Key \u5bf9\u5e94\u7684\u6570\u636e\u3002\u7559\u7a7a\u5219\u4e0d\u9650\u5236 Info\u3002");
                    }
                }
            }

            EditorGUILayout.HelpBox("\u4f7f\u7528\u9636\u6bb5\u53ef\u4ee5\u914d\u591a\u4e2a\u6279\u6b21\uff1a\u540c\u4e00\u5957\u5b57\u6bb5\u89c4\u5219\uff0c\u5206\u522b\u5904\u7406\u4e0d\u540c SO \u6587\u4ef6\u6216\u6587\u4ef6\u5939\u3002", MessageType.Info);
        }

        private void DrawAdvancedFields()
        {
            EditorGUILayout.LabelField("\u5feb\u901f\u65b9\u6848", _sectionTitleStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                var rule = target as ESSoTableDataRule;
                if (GUILayout.Button("\u6807\u51c6 SO \u8868", EditorStyles.miniButton))
                    ExecuteAndExit(rule, rule.ApplyPresetStandardSoTable);
                if (GUILayout.Button("\u53ea\u5bfc\u51fa", EditorStyles.miniButton))
                    ExecuteAndExit(rule, rule.ApplyPresetExportOnly);
                if (GUILayout.Button("\u8868\u683c\u5199\u56de", EditorStyles.miniButton))
                    ExecuteAndExit(rule, rule.ApplyPresetImportBack);
                if (GUILayout.Button("\u666e\u901a SO \u7b80\u5316", EditorStyles.miniButton))
                    ExecuteAndExit(rule, rule.ApplyPresetSimpleScriptableObject);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("\u5e38\u7528\u89c4\u5219", _sectionTitleStyle);
            DrawEnumProperty("infoExpandMode", "Info \u5c55\u5f00\u65b9\u5f0f", "\u51b3\u5b9a\u4e00\u4e2a Info \u5bf9\u8c61\u7684\u5b57\u6bb5\u5982\u4f55\u751f\u6210\u8868\u683c\u5217\uff1a\u53ea\u7528\u663e\u5f0f\u6620\u5c04\u3001\u626b\u53ef\u5e8f\u5217\u5316\u5b57\u6bb5\u3001\u628a\u5b50\u5bf9\u8c61\u5c55\u5f00\u6210\u591a\u5217\uff0c\u6216\u628a\u590d\u6742\u5bf9\u8c61\u4fdd\u5b58\u4e3a Json\u3002", new[] { "\u53ea\u4f7f\u7528\u663e\u5f0f\u6620\u5c04", "\u5c55\u5f00\u53ef\u5e8f\u5217\u5316\u5b57\u6bb5", "\u5d4c\u5957\u5bf9\u8c61\u5c55\u5f00\u591a\u5217", "\u590d\u6742\u5bf9\u8c61\u4fdd\u5b58\u4e3a Json" });
            DrawEnumProperty("groupSliceMode", "Group \u622a\u53d6\u65b9\u5f0f", "\u51b3\u5b9a\u5bfc\u51fa\u8868\u683c\u65f6 Group \u5982\u4f55\u8868\u8fbe\uff1a\u5ffd\u7565\u3001\u5199\u5165\u4e00\u5217\u3001\u6bcf\u4e2a Group \u4e00\u4e2a Sheet\uff0c\u6216\u6bcf\u4e2a Group \u4e00\u4e2a\u6587\u4ef6\u3002", new[] { "\u5ffd\u7565 Group", "Group \u540d\u5199\u5165\u5217", "\u6bcf\u4e2a Group \u4e00\u4e2a Sheet", "\u6bcf\u4e2a Group \u4e00\u4e2a\u6587\u4ef6" });
            DrawProperty("infoKeyColumnName", "Info Key \u5217\u540d", "\u8868\u683c\u4e2d\u7528\u6765\u5339\u914d Info \u7684 Key \u5217\u540d\u3002\u5bfc\u5165\u5199\u56de\u65f6\u4f18\u5148\u7528\u5b83\u627e\u5230\u5bf9\u5e94 SO\u3002");
            DrawEnumProperty("nameMatchMode", "\u540d\u79f0\u5339\u914d", "\u5bfc\u5165\u65f6\u8868\u683c\u5217\u540d\u548c\u5b57\u6bb5\u540d\u7684\u5339\u914d\u7b56\u7565\u3002\u4e00\u822c\u7528\u5b57\u6bb5\u540d\u8f6c\u5217\u540d\u6216\u5b8c\u5168\u5339\u914d\u3002", new[] { "\u5b8c\u5168\u5339\u914d", "\u5ffd\u7565\u5927\u5c0f\u5199", "\u5b57\u6bb5\u540d\u8f6c\u5217\u540d", "\u81ea\u5b9a\u4e49" });
            DrawProperty("allowCreateInfoOnImport", "\u5bfc\u5165\u65f6\u5141\u8bb8\u521b\u5efa Info", "\u8868\u683c\u91cc\u6709\u65b0 Key\uff0c\u4f46 SO \u4e2d\u627e\u4e0d\u5230\u5bf9\u5e94 Info \u65f6\uff0c\u662f\u5426\u5141\u8bb8\u81ea\u52a8\u521b\u5efa\u3002");
            DrawProperty("refreshPackBeforeExport", "\u5bfc\u51fa\u524d\u540c\u6b65 Pack \u7f13\u5b58", "\u5bfc\u51fa\u524d\u5148\u8ba9 Pack \u5237\u65b0\u5185\u90e8\u7f13\u5b58\uff0c\u907f\u514d\u8868\u683c\u4f7f\u7528\u5230\u8fc7\u671f\u7684 Group/Info \u5217\u8868\u3002");

            EditorGUILayout.Space(6);
            bool oldExpert = _showAdvancedExpert;
            _showAdvancedExpert = EditorGUILayout.Foldout(_showAdvancedExpert, "\u4e13\u5bb6\u8be6\u60c5", true, EditorStyles.foldout);
            if (_showAdvancedExpert != oldExpert)
                SaveFoldoutPref("advancedExpert", _showAdvancedExpert);
            if (!_showAdvancedExpert)
            {
                EditorGUILayout.HelpBox("\u65e5\u5e38\u60c5\u51b5\u7528\u4e0a\u9762\u7684\u5feb\u901f\u65b9\u6848\u548c\u5e38\u7528\u89c4\u5219\u5c31\u591f\u4e86\u3002\u884c\u7ed1\u5b9a\u3001\u5b50\u5bf9\u8c61\u5c55\u5f00\u3001\u8868\u5934\u6a21\u677f\u5df2\u653e\u5230\u4e13\u5bb6\u8be6\u60c5\u91cc\u3002", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Pack / Group / Info", _sectionTitleStyle);
            DrawProperty("packColumnName", "Pack \u5217\u540d", "\u5bfc\u51fa\u8868\u683c\u65f6 Pack \u6807\u8bc6\u5217\u7684\u5217\u540d\u3002\u591a Pack \u5408\u5e76\u8868\u65f6\u624d\u5e38\u7528\u3002");
            DrawProperty("groupColumnName", "Group \u5217\u540d", "\u5bfc\u51fa\u8868\u683c\u65f6 Group \u6807\u8bc6\u5217\u7684\u5217\u540d\u3002\u5bfc\u5165\u5355\u4e2a Group/Info \u65f6\u4e5f\u4f1a\u7528\u5230\u3002");

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("\u884c\u7ed1\u5b9a", _sectionTitleStyle);
            DrawChildEnum("rowBinding", "targetMode", "\u884c\u76ee\u6807", "\u51b3\u5b9a\u8868\u683c\u7684\u4e00\u884c\u5bf9\u5e94\u4ec0\u4e48\uff1a\u6574\u4e2a Info/SO \u5bf9\u8c61\uff0c\u6216\u5bf9\u8c61\u5185\u90e8\u67d0\u4e2a List \u7684\u5143\u7d20\u3002", new[] { "\u4e00\u884c = \u4e00\u4e2a\u5bf9\u8c61", "\u4e00\u884c = \u5bf9\u8c61\u5185 List \u5143\u7d20" });
            DrawChild("rowBinding", "rowKeyColumnName", "\u884c Key \u5217\u540d", "\u5f53\u4e00\u884c\u5bf9\u5e94 List \u5143\u7d20\u65f6\uff0c\u7528\u8fd9\u5217\u627e\u5230\u5177\u4f53\u5143\u7d20\u3002");
            DrawChild("rowBinding", "listFieldPath", "List \u5b57\u6bb5\u8def\u5f84", "\u5bf9\u8c61\u5185\u7684 List \u5b57\u6bb5\u8def\u5f84\uff0c\u4f8b\u5982 rewards \u6216 config.items\u3002");
            DrawChild("rowBinding", "elementKeyFieldPath", "\u5143\u7d20 Key \u5b57\u6bb5\u8def\u5f84", "\u7528\u6765\u5339\u914d List \u5143\u7d20\u7684 Key \u5b57\u6bb5\u3002");
            DrawChild("rowBinding", "createMissingElement", "\u5bfc\u5165\u65f6\u521b\u5efa\u7f3a\u5931\u5143\u7d20", "\u8868\u683c\u91cc\u6709\u65b0\u5143\u7d20 Key\uff0cList \u91cc\u4e0d\u5b58\u5728\u65f6\uff0c\u662f\u5426\u81ea\u52a8\u521b\u5efa\u3002");
            DrawChild("rowBinding", "allowEmptyRowKey", "\u5141\u8bb8\u7a7a Key", "\u5141\u8bb8\u884c Key \u4e3a\u7a7a\u3002\u4e00\u822c\u4e0d\u5efa\u8bae\u6253\u5f00\uff0c\u9664\u975e\u884c\u987a\u5e8f\u5c31\u662f\u552f\u4e00\u5339\u914d\u4f9d\u636e\u3002");

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("\u5b50\u5bf9\u8c61\u5b57\u6bb5", _sectionTitleStyle);
            DrawChild("nestedFieldRule", "expandNestedFields", "\u5c55\u5f00\u5b50\u5bf9\u8c61\u5b57\u6bb5", "\u6784\u5efa\u6620\u5c04\u65f6\uff0c\u662f\u5426\u628a\u53ef\u5e8f\u5217\u5316\u7684\u5b50\u5bf9\u8c61\u5b57\u6bb5\u5c55\u5f00\u6210\u591a\u4e2a\u8868\u683c\u5217\u3002");
            DrawChild("nestedFieldRule", "maxDepth", "\u6700\u5927\u5c55\u5f00\u6df1\u5ea6", "\u5b50\u5bf9\u8c61\u6700\u591a\u5c55\u5f00\u51e0\u5c42\u3002\u8d8a\u5927\u5217\u8d8a\u591a\uff0c\u8868\u683c\u4e5f\u8d8a\u590d\u6742\u3002");
            DrawChild("nestedFieldRule", "columnSeparator", "\u5217\u540d\u5206\u9694\u7b26", "\u5b50\u5bf9\u8c61\u5c55\u5f00\u540e\u5217\u540d\u7684\u8fde\u63a5\u7b26\uff0c\u4f8b\u5982 config_value\u4e2d\u7684 _\u3002");

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("\u8868\u5934\u6a21\u677f", _sectionTitleStyle);
            DrawChild("header", "varMark", "\u53d8\u91cf\u884c\u6807\u8bb0", "SO表格 \u8868\u5934\u7b2c 1 \u884c\u7684\u6807\u8bb0\uff0c\u9ed8\u8ba4 ##var\u3002");
            DrawChild("header", "typeMark", "\u7c7b\u578b\u884c\u6807\u8bb0", "SO表格 \u8868\u5934\u7b2c 2 \u884c\u7684\u6807\u8bb0\uff0c\u9ed8\u8ba4 ##type\u3002");
            DrawChild("header", "groupMark", "\u5206\u7ec4\u884c\u6807\u8bb0", "SO表格 \u8868\u5934\u7b2c 3 \u884c\u7684\u6807\u8bb0\uff0c\u9ed8\u8ba4 ##group\u3002");
            DrawChild("header", "commentMark", "\u6ce8\u91ca\u884c\u6807\u8bb0", "SO表格 \u8868\u5934\u7b2c 4 \u884c\u7684\u6807\u8bb0\uff0c\u9ed8\u8ba4 ##\u3002");
            DrawChild("header", "defaultGroup", "\u9ed8\u8ba4\u5206\u7ec4", "\u5b57\u6bb5\u6ca1\u6709\u5355\u72ec\u6307\u5b9a group \u65f6\uff0c\u5199\u5165\u8868\u5934\u7684\u9ed8\u8ba4 group \u503c\u3002");

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("\u5bfc\u5165\u884c\u4e3a", _sectionTitleStyle);
            DrawProperty("allowCreateGroupOnImport", "\u5bfc\u5165\u65f6\u5141\u8bb8\u521b\u5efa Group", "\u8868\u683c\u91cc\u51fa\u73b0\u65b0 Group\uff0c\u4f46 Pack \u91cc\u627e\u4e0d\u5230\u5bf9\u5e94 Group \u65f6\uff0c\u662f\u5426\u5141\u8bb8\u81ea\u52a8\u521b\u5efa\u3002");
        }

        private void DrawTypeCacheFields()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                DrawChildEnum("typeBinding", "objectKind", "\u5bf9\u8c61\u4f53\u7cfb", new[] { "SoData Pack / Group / Info", "\u666e\u901a ScriptableObject" });
                DrawChild("typeBinding", "objectTypeName", "\u666e\u901a SO \u7c7b\u578b");
                DrawChild("typeBinding", "packTypeName", "Pack \u7c7b\u578b");
                DrawChild("typeBinding", "groupTypeName", "Group \u7c7b\u578b");
                DrawChild("typeBinding", "infoTypeName", "Info \u7c7b\u578b");
            }
        }

        private void DrawColumnFields()
        {
            SerializedProperty columns = serializedObject.FindProperty("columns");
            if (columns == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("\u5217\u6620\u5c04\u6570\u91cf\uff1a" + columns.arraySize, _sectionTitleStyle);
                if (GUILayout.Button("\u5168\u90e8\u8be6\u60c5", EditorStyles.miniButton, GUILayout.Width(82)))
                    SetColumnDetails(columns, true);
                if (GUILayout.Button("\u6536\u8d77\u8be6\u60c5", EditorStyles.miniButton, GUILayout.Width(82)))
                    SetColumnDetails(columns, false);
                if (GUILayout.Button("\u6e05\u9664\u672a\u9501\u5b9a", EditorStyles.miniButton, GUILayout.Width(96)))
                    ClearUnlockedColumns(columns);
                if (GUILayout.Button("\u5168\u90e8\u5ffd\u7565", EditorStyles.miniButton, GUILayout.Width(82)))
                    SetColumnAuthority(columns, ESTableColumnAuthority.Ignore);
                if (GUILayout.Button("\u8868\u683c\u6743\u5a01", EditorStyles.miniButton, GUILayout.Width(82)))
                    SetColumnAuthority(columns, ESTableColumnAuthority.TableAuthority);
                if (GUILayout.Button("\u65b0\u589e\u5217", EditorStyles.miniButton, GUILayout.Width(72)))
                    columns.InsertArrayElementAtIndex(columns.arraySize);
            }

            DrawColumnHeader();

            for (int i = 0; i < columns.arraySize; i++)
            {
                SerializedProperty item = columns.GetArrayElementAtIndex(i);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope(GUILayout.MinHeight(22)))
                    {
                        DrawColumnIndex(i);
                        DrawCompactChild(item, "enabled", GUIContent.none, 34);
                        DrawCompactChild(item, "locked", GUIContent.none, 34);
                        DrawCompactChild(item, "authority", GUIContent.none, 86);
                        DrawCompactChild(item, "soFieldPath", GUIContent.none, 190);
                        DrawCompactChild(item, "displayName", GUIContent.none, 150);
                        DrawCompactChild(item, "columnName", GUIContent.none, 150);
                        DrawCompactChild(item, "availability", GUIContent.none, 84);
                        DrawCompactChild(item, "showDetail", GUIContent.none, 48);

                        using (new EditorGUI.DisabledScope(i == 0))
                        {
                            if (GUILayout.Button("\u4e0a", EditorStyles.miniButton, GUILayout.Width(32)))
                            {
                                columns.MoveArrayElement(i, i - 1);
                                break;
                            }
                        }

                        using (new EditorGUI.DisabledScope(i >= columns.arraySize - 1))
                        {
                            if (GUILayout.Button("\u4e0b", EditorStyles.miniButton, GUILayout.Width(32)))
                            {
                                columns.MoveArrayElement(i, i + 1);
                                break;
                            }
                        }

                        SerializedProperty locked = item.FindPropertyRelative("locked");
                        using (new EditorGUI.DisabledScope(locked != null && locked.boolValue))
                        {
                            if (GUILayout.Button("\u5220", EditorStyles.miniButton, GUILayout.Width(34)))
                            {
                                columns.DeleteArrayElementAtIndex(i);
                                break;
                            }
                        }
                    }

                    SerializedProperty showDetail = item.FindPropertyRelative("showDetail");
                    if (showDetail == null || !showDetail.boolValue)
                        continue;

                    EditorGUILayout.Space(2);
                    DrawChildEnum(item, "valueWriteMode", "SO \u5199\u5165\u65b9\u5f0f", new[] { "\u666e\u901a\u503c", "Unity \u5bf9\u8c61 GUID", "Unity \u5bf9\u8c61\u8def\u5f84", "Json", "\u7c7b\u578b\u540d" });
                    DrawChild(item, "isInfoKey", "Info Key");
                    DrawChild(item, "isGroupKey", "Group Key");
                    DrawChild(item, "comment", "\u4e2d\u6587\u8bf4\u660e");
                    DrawChild(item, "tableType", "SO表格 \u7c7b\u578b");
                    DrawChildEnum(item, "direction", "\u65b9\u5411", new[] { "\u53cc\u5411", "\u4ec5 SO \u5230\u8868\u683c", "\u4ec5\u8868\u683c\u5230 SO", "\u5ffd\u7565" });
                    DrawChild(item, "allowPassThrough", "\u4fdd\u7559\u672a\u6620\u5c04\u5217");
                }
            }
        }

        private void DrawColumnHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("\u5217", EditorStyles.miniBoldLabel, GUILayout.Width(48));
                GUILayout.Label("\u7528", EditorStyles.miniBoldLabel, GUILayout.Width(34));
                GUILayout.Label("\u9501", EditorStyles.miniBoldLabel, GUILayout.Width(34));
                GUILayout.Label("\u6743\u5a01", EditorStyles.miniBoldLabel, GUILayout.Width(86));
                GUILayout.Label("SO \u5b57\u6bb5\u540d", EditorStyles.miniBoldLabel, GUILayout.Width(190));
                GUILayout.Label("SO \u663e\u793a\u540d", EditorStyles.miniBoldLabel, GUILayout.Width(150));
                GUILayout.Label("\u8868\u683c\u5217\u540d", EditorStyles.miniBoldLabel, GUILayout.Width(150));
                GUILayout.Label("\u53ef\u7528", EditorStyles.miniBoldLabel, GUILayout.Width(84));
                GUILayout.Label("\u8be6\u60c5", EditorStyles.miniBoldLabel, GUILayout.Width(48));
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawColumnIndex(int index)
        {
            GUILayout.Label((index + 1).ToString() + " / " + ToColumnLetters(index), EditorStyles.miniLabel, GUILayout.Width(48));
        }

        private static string ToColumnLetters(int index)
        {
            index = Mathf.Max(0, index);
            var builder = new StringBuilder();
            int value = index;
            do
            {
                int mod = value % 26;
                builder.Insert(0, (char)('A' + mod));
                value = value / 26 - 1;
            }
            while (value >= 0);

            return builder.ToString();
        }

        private static void SetColumnDetails(SerializedProperty columns, bool value)
        {
            if (columns == null)
                return;

            for (int i = 0; i < columns.arraySize; i++)
            {
                SerializedProperty item = columns.GetArrayElementAtIndex(i);
                SerializedProperty showDetail = item.FindPropertyRelative("showDetail");
                if (showDetail != null)
                    showDetail.boolValue = value;
            }
        }

        private static void SetColumnAuthority(SerializedProperty columns, ESTableColumnAuthority authority)
        {
            if (columns == null)
                return;

            for (int i = 0; i < columns.arraySize; i++)
            {
                SerializedProperty item = columns.GetArrayElementAtIndex(i);
                SerializedProperty authorityProperty = item.FindPropertyRelative("authority");
                if (authorityProperty != null)
                    authorityProperty.enumValueIndex = (int)authority;
            }
        }

        private static void ClearUnlockedColumns(SerializedProperty columns)
        {
            if (columns == null)
                return;

            for (int i = columns.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty item = columns.GetArrayElementAtIndex(i);
                SerializedProperty locked = item.FindPropertyRelative("locked");
                if (locked == null || !locked.boolValue)
                    columns.DeleteArrayElementAtIndex(i);
            }
        }

        private void DrawCompactChild(SerializedProperty parent, string childName, GUIContent label, float width)
        {
            SerializedProperty child = parent.FindPropertyRelative(childName);
            if (child == null)
            {
                GUILayout.Space(width);
                return;
            }

            EditorGUILayout.PropertyField(child, label, GUILayout.Width(width));
        }

        private void DrawPathProperty(SerializedProperty property, string label, bool filePath, string extensions)
        {
            if (property == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
                if (GUILayout.Button(filePath ? "\u9009\u62e9\u6587\u4ef6" : "\u9009\u62e9\u6587\u4ef6\u5939", EditorStyles.miniButton, GUILayout.Width(86)))
                {
                    string selected = filePath
                        ? EditorUtility.OpenFilePanel(label, GetPathPanelFolder(property.stringValue), extensions)
                        : EditorUtility.OpenFolderPanel(label, GetPathPanelFolder(property.stringValue), string.Empty);
                    if (!string.IsNullOrEmpty(selected))
                        property.stringValue = filePath ? selected : MakeProjectRelative(selected);
                }
            }

            Rect dropRect = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
            ESDropZoneSolver dropZone = filePath ? _tablePathDropZone : _folderPathDropZone;
            if (dropZone.Draw(dropRect, out UnityEngine.Object[] dropped))
            {
                string path = ResolveDroppedPath(dropped, filePath, extensions);
                if (!string.IsNullOrEmpty(path))
                    property.stringValue = filePath ? path : MakeProjectRelative(path);
            }

            string prompt = filePath ? "\u62d6\u5165 Project \u91cc\u7684 CSV / XLSX \u8868\u683c" : "\u62d6\u5165 Project \u91cc\u7684\u8868\u683c\u8f93\u51fa\u6587\u4ef6\u5939";
            string detail = string.IsNullOrEmpty(dropZone.LastRejectReason)
                ? dropZone.LastAcceptedCount > 0 ? "\u677e\u5f00\u540e\u63a5\u6536 " + dropZone.LastAcceptedCount + " \u4e2a\u5bf9\u8c61" : prompt
                : "\u62d2\u7edd\uff1a" + dropZone.LastRejectReason;
            GUI.Label(dropRect, detail, EditorStyles.centeredGreyMiniLabel);
        }

        private static string GetPathPanelFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Application.dataPath;

            string fullPath = Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            if (Directory.Exists(fullPath))
                return fullPath;

            string folder = Path.GetDirectoryName(fullPath);
            return string.IsNullOrEmpty(folder) ? Application.dataPath : folder;
        }

        private static string MakeProjectRelative(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');
            string fullPath = Path.GetFullPath(path).Replace('\\', '/');
            if (fullPath.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(projectRoot.Length + 1);

            return fullPath;
        }

        private static string ResolveDroppedPath(UnityEngine.Object[] dropped, bool filePath, string extensions)
        {
            if (dropped == null)
                return string.Empty;

            for (int i = 0; i < dropped.Length; i++)
            {
                string assetPath = AssetDatabase.GetAssetPath(dropped[i]);
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
                if (!filePath && AssetDatabase.IsValidFolder(assetPath))
                    return assetPath;

                if (filePath && File.Exists(fullPath) && IsAllowedExtension(fullPath, extensions))
                    return fullPath;
            }

            return string.Empty;
        }

        private static bool IsAllowedExtension(string path, string extensions)
        {
            if (string.IsNullOrWhiteSpace(extensions))
                return true;

            string extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            string[] allowed = extensions.Split(',');
            for (int i = 0; i < allowed.Length; i++)
            {
                if (extension == allowed[i].Trim().TrimStart('.').ToLowerInvariant())
                    return true;
            }

            return false;
        }

        private static void ApplyDefaultBatchPath(SerializedProperty batch, ESSoTableDataRule rule)
        {
            if (batch == null || rule == null)
                return;

            SetStringChild(batch, "fileName", FirstNotEmpty(GetStringChild(batch, "fileName"), rule.ruleKey, rule.tableName, rule.name));
            SetStringChild(batch, "sheetName", FirstNotEmpty(GetStringChild(batch, "sheetName"), rule.ruleKey, rule.beanName, rule.name));
            SetStringChild(batch, "outputRoot", FirstNotEmpty(GetStringChild(batch, "outputRoot"), "SoTableConfig/Tables"));
            SetStringChild(batch, "csvRelativePath", FirstNotEmpty(GetStringChild(batch, "csvRelativePath"), "csv"));
            SetStringChild(batch, "xlsxRelativePath", FirstNotEmpty(GetStringChild(batch, "xlsxRelativePath"), "xlsx"));
        }

        private static void ApplyDefaultPathToAllBatches(SerializedProperty batches, ESSoTableDataRule rule)
        {
            if (batches == null)
                return;

            for (int i = 0; i < batches.arraySize; i++)
                ApplyDefaultBatchPath(batches.GetArrayElementAtIndex(i), rule);
        }

        private static void SetAllBatchesEnabled(SerializedProperty batches, bool enabled)
        {
            if (batches == null)
                return;

            for (int i = 0; i < batches.arraySize; i++)
            {
                SerializedProperty batch = batches.GetArrayElementAtIndex(i);
                SerializedProperty enabledProperty = batch.FindPropertyRelative("enabled");
                if (enabledProperty != null)
                    enabledProperty.boolValue = enabled;
            }
        }

        private static string GetStringChild(SerializedProperty parent, string childName)
        {
            SerializedProperty child = parent.FindPropertyRelative(childName);
            return child != null ? child.stringValue : string.Empty;
        }

        private static void SetStringChild(SerializedProperty parent, string childName, string value)
        {
            SerializedProperty child = parent.FindPropertyRelative(childName);
            if (child != null)
                child.stringValue = value;
        }

        private void DrawProperty(string propertyName, string label)
        {
            DrawProperty(propertyName, label, string.Empty);
        }

        private void DrawProperty(string propertyName, string label, string tooltip)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
        }

        private void DrawEnumProperty(string propertyName, string label, string[] displayNames)
        {
            DrawEnumProperty(propertyName, label, string.Empty, displayNames);
        }

        private void DrawEnumProperty(string propertyName, string label, string tooltip, string[] displayNames)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            DrawEnumProperty(property, label, tooltip, displayNames);
        }

        private void DrawChild(string parentName, string childName, string label)
        {
            DrawChild(parentName, childName, label, string.Empty);
        }

        private void DrawChild(string parentName, string childName, string label, string tooltip)
        {
            SerializedProperty parent = serializedObject.FindProperty(parentName);
            if (parent == null)
                return;

            DrawChild(parent, childName, label, tooltip);
        }

        private void DrawChild(SerializedProperty parent, string childName, string label)
        {
            DrawChild(parent, childName, label, string.Empty);
        }

        private void DrawChild(SerializedProperty parent, string childName, string label, string tooltip)
        {
            SerializedProperty child = parent.FindPropertyRelative(childName);
            if (child != null)
                EditorGUILayout.PropertyField(child, new GUIContent(label, tooltip), true);
        }

        private void DrawChildEnum(string parentName, string childName, string label, string[] displayNames)
        {
            DrawChildEnum(parentName, childName, label, string.Empty, displayNames);
        }

        private void DrawChildEnum(string parentName, string childName, string label, string tooltip, string[] displayNames)
        {
            SerializedProperty parent = serializedObject.FindProperty(parentName);
            if (parent == null)
                return;

            DrawChildEnum(parent, childName, label, tooltip, displayNames);
        }

        private void DrawChildEnum(SerializedProperty parent, string childName, string label, string[] displayNames)
        {
            DrawChildEnum(parent, childName, label, string.Empty, displayNames);
        }

        private void DrawChildEnum(SerializedProperty parent, string childName, string label, string tooltip, string[] displayNames)
        {
            SerializedProperty child = parent.FindPropertyRelative(childName);
            DrawEnumProperty(child, label, tooltip, displayNames);
        }

        private void DrawEnumProperty(SerializedProperty property, string label, string[] displayNames)
        {
            DrawEnumProperty(property, label, string.Empty, displayNames);
        }

        private void DrawEnumProperty(SerializedProperty property, string label, string tooltip, string[] displayNames)
        {
            if (property == null)
                return;

            if (property.propertyType != SerializedPropertyType.Enum || displayNames == null || displayNames.Length == 0)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
                return;
            }

            int index = Mathf.Clamp(property.enumValueIndex, 0, displayNames.Length - 1);
            property.enumValueIndex = EditorGUILayout.Popup(new GUIContent(label, tooltip), index, displayNames);
        }

        private static void BindPreferredSource(ESSoTableDataRule rule)
        {
            ESSoTableRuleSourceBinding source = GetBuildSourceBinding(rule);
            if (source == null)
                return;

            if (source.soFolder != null)
            {
                rule.BindAndGenerateFromFolder();
                return;
            }

            if (source.soAsset != null)
            {
                rule.BindAndGenerateFromSoAsset();
                return;
            }

            if (source.monoScript != null)
                rule.BindAndGenerateFromMonoScript();
        }

        private static bool HasAnySource(ESSoTableDataRule rule)
        {
            ESSoTableRuleSourceBinding source = GetBuildSourceBinding(rule);
            return source != null &&
                   (source.soAsset != null || source.soFolder != null || source.monoScript != null);
        }

        private static bool HasColumns(ESSoTableDataRule rule)
        {
            return rule.columns != null && rule.columns.Count > 0;
        }

        private static int GetColumnCount(ESSoTableDataRule rule)
        {
            return rule.columns == null ? 0 : rule.columns.Count;
        }

        private static int GetEnabledColumnCount(ESSoTableDataRule rule)
        {
            if (rule.columns == null)
                return 0;

            int count = 0;
            for (int i = 0; i < rule.columns.Count; i++)
            {
                if (rule.columns[i] != null && rule.columns[i].enabled)
                    count++;
            }

            return count;
        }

        private static string GetSourceSummary(ESSoTableDataRule rule)
        {
            ESSoTableRuleSourceBinding source = GetBuildSourceBinding(rule);
            if (source == null)
                return "\u65e0\u6765\u6e90";

            if (source.soFolder != null)
                return "\u6587\u4ef6\u5939\uff1a" + source.soFolder.name;
            if (source.soAsset != null)
                return "SO\uff1a" + source.soAsset.name;
            if (source.monoScript != null)
                return "\u811a\u672c\uff1a" + source.monoScript.name;

            return GetBindSourceKindText(source.sourceKind);
        }

        private static string GetSourceKind(ESSoTableDataRule rule)
        {
            ESSoTableRuleSourceBinding source = GetBuildSourceBinding(rule);
            if (source == null)
                return "\u65e0";

            if (source.soFolder != null)
                return "\u6587\u4ef6\u5939";
            if (source.soAsset != null)
                return "SO \u6587\u4ef6";
            if (source.monoScript != null)
                return "\u811a\u672c";

            return GetBindSourceKindText(source.sourceKind);
        }

        private static ESSoTableRuleSourceBinding GetBuildSourceBinding(ESSoTableDataRule rule)
        {
            if (rule == null)
                return null;

            return rule.buildStage != null ? rule.buildStage.sourceBinding : null;
        }
        private static string GetSourcePath(ESSoTableDataRule rule)
        {
            ESSoTableRuleSourceBinding source = GetBuildSourceBinding(rule);
            if (source == null)
                return string.Empty;

            string path = string.Empty;
            if (source.soFolder != null)
                path = AssetDatabase.GetAssetPath(source.soFolder);
            else if (source.soAsset != null)
                path = AssetDatabase.GetAssetPath(source.soAsset);
            else if (source.monoScript != null)
                path = AssetDatabase.GetAssetPath(source.monoScript);

            return FirstNotEmpty(path, source.sourcePath, "-");
        }

        private static string GetInfoTypeName(ESSoTableDataRule rule)
        {
            if (rule.typeBinding == null)
                return "-";

            return ShortTypeName(FirstNotEmpty(rule.typeBinding.infoTypeName, rule.typeBinding.objectTypeName, "-"));
        }

        private static string GetPackGroupSummary(ESSoTableDataRule rule)
        {
            if (rule.typeBinding == null)
                return "-";

            return ShortTypeName(rule.typeBinding.packTypeName) + " / " + ShortTypeName(rule.typeBinding.groupTypeName);
        }

        private static string GetOutputMode(ESSoTableDataRule rule)
        {
            ESSoTableRuleUseBatch batch = GetPrimaryBatch(rule);
            ESTableFileKind fileKind = batch != null ? batch.fileKind : ESTableFileKind.CsvAndXlsx;
            return GetFileKindText(fileKind) + "  |  " + GetGroupSliceModeText(rule.groupSliceMode);
        }

        private static string GetOutputPath(ESSoTableDataRule rule)
        {
            ESSoTableRuleUseBatch batch = GetPrimaryBatch(rule);
            string root = batch != null ? FirstNotEmpty(batch.outputRoot, "SoTableConfig/Tables") : "SoTableConfig/Tables";
            string file = FirstNotEmpty(GetBatchFileName(rule), rule.tableName, rule.ruleKey, rule.name);
            return root + "/" + file;
        }

        private static ESSoTableRuleUseBatch GetPrimaryBatch(ESSoTableDataRule rule)
        {
            if (rule == null || rule.useBatches == null || rule.useBatches.Count == 0)
                return null;

            for (int i = 0; i < rule.useBatches.Count; i++)
            {
                if (rule.useBatches[i] != null && rule.useBatches[i].enabled)
                    return rule.useBatches[i];
            }

            return rule.useBatches[0];
        }

        private static string GetBatchFileName(ESSoTableDataRule rule)
        {
            ESSoTableRuleUseBatch batch = GetPrimaryBatch(rule);
            return batch != null ? batch.fileName : string.Empty;
        }

        private static string GetBatchSheetName(ESSoTableDataRule rule)
        {
            ESSoTableRuleUseBatch batch = GetPrimaryBatch(rule);
            return batch != null ? batch.sheetName : string.Empty;
        }

        private static string GetBatchCountText(ESSoTableDataRule rule)
        {
            int count = rule != null && rule.useBatches != null ? rule.useBatches.Count : 0;
            if (count == 0)
                return "\u672a\u914d\u7f6e";
            return count + " \u4e2a\u6279\u6b21";
        }

        private static string GetBatchPolicyText(ESSoTableDataRule rule)
        {
            ESSoTableRuleUseBatch batch = GetPrimaryBatch(rule);
            if (batch == null)
                return "\u4f7f\u7528\u9636\u6bb5\u5c1a\u672a\u914d\u7f6e";

            return "\u5bfc\u5165 " + GetConflictPolicyText(batch.importConflictPolicy) + " / \u5bfc\u51fa " + GetConflictPolicyText(batch.exportConflictPolicy);
        }

        private static string FirstNotEmpty(params string[] values)
        {
            if (values == null)
                return string.Empty;

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            }

            return string.Empty;
        }

        private static string ShortTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return "-";

            int index = typeName.LastIndexOf('.');
            return index >= 0 && index + 1 < typeName.Length ? typeName.Substring(index + 1) : typeName;
        }

        private static string ShortenMiddle(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
                return string.IsNullOrWhiteSpace(value) ? "-" : value;

            int keep = Math.Max(4, (maxLength - 3) / 2);
            return value.Substring(0, keep) + "..." + value.Substring(value.Length - keep);
        }

        private static string GetFileKindText(ESTableFileKind value)
        {
            switch (value)
            {
                case ESTableFileKind.Csv:
                    return "CSV";
                case ESTableFileKind.Xlsx:
                    return "XLSX";
                case ESTableFileKind.CsvAndXlsx:
                    return "CSV \u548c XLSX";
                default:
                    return value.ToString();
            }
        }

        private static string GetGroupSliceModeText(ESTableGroupSliceMode value)
        {
            switch (value)
            {
                case ESTableGroupSliceMode.IgnoreGroup:
                    return "\u5ffd\u7565 Group";
                case ESTableGroupSliceMode.GroupNameColumn:
                    return "Group \u540d\u5199\u5165\u5217";
                case ESTableGroupSliceMode.OneGroupPerSheet:
                    return "\u6bcf\u4e2a Group \u4e00\u4e2a Sheet";
                case ESTableGroupSliceMode.OneGroupPerFile:
                    return "\u6bcf\u4e2a Group \u4e00\u4e2a\u6587\u4ef6";
                default:
                    return value.ToString();
            }
        }

        private static string GetConflictPolicyText(ESTableConflictPolicy value)
        {
            switch (value)
            {
                case ESTableConflictPolicy.Skip:
                    return "\u8df3\u8fc7";
                case ESTableConflictPolicy.Overwrite:
                    return "\u8986\u76d6";
                case ESTableConflictPolicy.CreateCopy:
                    return "\u521b\u5efa\u526f\u672c";
                case ESTableConflictPolicy.Error:
                    return "\u62a5\u9519";
                default:
                    return value.ToString();
            }
        }

        private static string GetBindSourceKindText(ESSoTableRuleBindSourceKind value)
        {
            switch (value)
            {
                case ESSoTableRuleBindSourceKind.None:
                    return "\u65e0";
                case ESSoTableRuleBindSourceKind.SoAsset:
                    return "SO \u6587\u4ef6";
                case ESSoTableRuleBindSourceKind.SoFolder:
                    return "SO \u6587\u4ef6\u5939";
                case ESSoTableRuleBindSourceKind.MonoScript:
                    return "\u811a\u672c";
                default:
                    return value.ToString();
            }
        }
    }

    public static class ESSoTableRuleTypeUtility
    {
        public static Type FindType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;

            Type type = Type.GetType(typeName);
            if (type != null)
                return type;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName);
                if (type != null)
                    return type;
            }

            return null;
        }

        public static bool TryResolveSoDataTypes(Type sourceType, out Type packType, out Type groupType, out Type infoType, out string reason)
        {
            packType = null;
            groupType = null;
            infoType = null;
            reason = string.Empty;

            if (sourceType == null)
            {
                reason = "脚本类型为空，无法生成 Rule。";
                return false;
            }

            if (typeof(ISoDataPack).IsAssignableFrom(sourceType))
            {
                packType = sourceType;
                infoType = TryGetInfoTypeFromPack(sourceType);
            }
            else if (typeof(ISoDataGroup).IsAssignableFrom(sourceType))
            {
                groupType = sourceType;
                infoType = TryGetInfoTypeFromGroup(sourceType);
            }
            else if (typeof(ISoDataInfo).IsAssignableFrom(sourceType))
            {
                infoType = sourceType;
            }
            else
            {
                reason = "脚本不是 SoData Pack、SoData Group 或 SoData Info。";
                return false;
            }

            if (infoType == null)
            {
                reason = "无法从 Pack/Group 脚本推断 Info 类型。请确认脚本继承 SoDataPack<TInfo> 或 SoDataGroup<TInfo>。";
                return false;
            }

            if (packType == null)
                packType = FindSoDataPackType(infoType);
            if (groupType == null)
                groupType = FindSoDataGroupType(infoType);

            if (packType == null || groupType == null)
            {
                reason = "已解析 Info 类型 " + infoType.Name + "，但没有找到对应的 SoDataPack<" + infoType.Name + "> 或 SoDataGroup<" + infoType.Name + "> 实现。";
                return false;
            }

            return true;
        }

        public static Type FindSoDataPackType(Type infoType)
        {
            return FindFirstDerivedGeneric(typeof(SoDataPack<>), infoType);
        }

        public static Type FindSoDataGroupType(Type infoType)
        {
            return FindFirstDerivedGeneric(typeof(SoDataGroup<>), infoType);
        }

        private static Type TryGetInfoTypeFromPack(Type packType)
        {
            return TryGetGenericArgumentFromBase(packType, typeof(SoDataPack<>));
        }

        private static Type TryGetInfoTypeFromGroup(Type groupType)
        {
            return TryGetGenericArgumentFromBase(groupType, typeof(SoDataGroup<>));
        }

        private static Type TryGetGenericArgumentFromBase(Type type, Type openGenericBase)
        {
            Type current = type;
            while (current != null && current != typeof(object))
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == openGenericBase)
                    return current.GetGenericArguments()[0];

                current = current.BaseType;
            }

            return null;
        }

        private static Type FindFirstDerivedGeneric(Type openGenericBase, Type genericArgument)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try
                {
                    types = assemblies[i].GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                for (int j = 0; j < types.Length; j++)
                {
                    Type type = types[j];
                    if (type == null || type.IsAbstract)
                        continue;

                    Type argument = TryGetGenericArgumentFromBase(type, openGenericBase);
                    if (argument == genericArgument)
                        return type;
                }
            }

            return null;
        }

        public static string GuessTableType(Type type)
        {
            if (type == typeof(bool))
                return "bool";
            if (type == typeof(byte) || type == typeof(short) || type == typeof(int))
                return "int";
            if (type == typeof(long))
                return "long";
            if (type == typeof(float))
                return "float";
            if (type == typeof(double))
                return "double";
            if (type == typeof(string))
                return "string";
            if (type.IsEnum)
                return "string";
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return "string";
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return "list," + GuessTableType(type.GetGenericArguments()[0]);

            return "string";
        }

        public static bool CanExpandAsNestedObject(Type type)
        {
            if (type == null)
                return false;
            if (type.IsPrimitive || type.IsEnum)
                return false;
            if (type == typeof(string) || type == typeof(decimal))
                return false;
            if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4) || type == typeof(Color) || type == typeof(Rect) || type == typeof(Quaternion))
                return false;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return false;
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string))
                return false;
            if (type.IsAbstract || type.IsInterface)
                return false;

            return Attribute.IsDefined(type, typeof(SerializableAttribute)) || type.IsValueType;
        }

        public static ESTableValueWriteMode GuessValueWriteMode(Type type)
        {
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return ESTableValueWriteMode.UnityObjectGuid;

            if (type.IsEnum)
                return ESTableValueWriteMode.PlainValue;

            if (type.IsPrimitive || type == typeof(string))
                return ESTableValueWriteMode.PlainValue;

            return ESTableValueWriteMode.Json;
        }
    }
}
#endif
