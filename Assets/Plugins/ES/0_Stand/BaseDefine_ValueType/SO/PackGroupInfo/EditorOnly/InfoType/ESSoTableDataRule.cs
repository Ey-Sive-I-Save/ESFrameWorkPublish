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

        [LabelText("CSV 和 XLSX")]
        CsvAndXlsx
    }

    public enum ESSoTableRuleDirection
    {
        [LabelText("双向")]
        Both,

        [LabelText("仅 SO 到表格")]
        SoToTableOnly,

        [LabelText("仅表格写回 SO")]
        TableToSoOnly,

        [LabelText("忽略")]
        Ignore
    }

    public enum ESTableColumnAvailability
    {
        [Tooltip("跟随本行的启用开关")]
        [LabelText("普通")]
        Normal,

        [Tooltip("即使启用开关关闭，也参与导入导出")]
        [LabelText("强制启用")]
        ForceEnabled,

        [Tooltip("无论启用开关如何，都不参与导入导出")]
        [LabelText("强制禁用")]
        ForceDisabled
    }

    public enum ESTableColumnNameMode
    {
        [LabelText("英文列名")]
        English,

        [LabelText("中文显示")]
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
        [LabelText("表格覆盖")]
        [Tooltip("导入时表格值覆盖 SO 字段")]
        TableAuthority,

        [LabelText("保留现有值")]
        [Tooltip("字段已有内容时，表格不覆盖；为空时允许表格补值")]
        SoAuthority,

        [LabelText("忽略")]
        [Tooltip("保留映射记录，但导入导出都不使用")]
        Ignore
    }

    public enum ESTableInfoExpandMode
    {
        [LabelText("只使用显式映射")]
        ExplicitMappingsOnly,

        [LabelText("展开可序列化字段")]
        SerializedFields,

        [LabelText("嵌套对象展开为多列")]
        NestedObjectColumns,

        [LabelText("复杂对象保存为 Json")]
        ComplexObjectAsJson
    }

    public enum ESTableGroupSliceMode
    {
        [LabelText("忽略 Group")]
        IgnoreGroup,

        [LabelText("Group 名写入列")]
        GroupNameColumn,

        [LabelText("每个 Group 一个 Sheet")]
        OneGroupPerSheet,

        [LabelText("每个 Group 一个文件")]
        OneGroupPerFile
    }

    public enum ESTableNameMatchMode
    {
        [LabelText("完全匹配")]
        Exact,

        [LabelText("忽略大小写")]
        IgnoreCase,

        [LabelText("字段名转列名")]
        FieldToColumn,

        [LabelText("自定义")]
        Custom
    }

    public enum ESTableValueWriteMode
    {
        [LabelText("普通值")]
        PlainValue,

        [LabelText("Unity 对象 GUID")]
        UnityObjectGuid,

        [LabelText("Unity 对象路径")]
        UnityObjectPath,

        [LabelText("Json")]
        Json,

        [LabelText("类型名")]
        TypeName
    }

    public enum ESTableConflictPolicy
    {
        [LabelText("跳过")]
        Skip,

        [LabelText("覆盖")]
        Overwrite,

        [LabelText("创建副本")]
        CreateCopy,

        [LabelText("报错")]
        Error
    }

    public enum ESTableExportWriteMode
    {
        [LabelText("整表重建")]
        [Tooltip("直接使用本次导出的完整表格内容。只有明确需要完全重建表格时使用")]
        Rebuild,

        [LabelText("按 Key 更新并追加")]
        [Tooltip("默认模式。目标表已存在时，按对象 Key/子级 Key 更新已有行，追加新行，并尽量保留旧表未映射列和备注")]
        MergeByKey,

        [LabelText("仅按 Key 更新")]
        [Tooltip("只更新目标表里已经存在的 Key；本次导出的新增 Key 不追加到旧表")]
        UpdateExistingOnly
    }

    public enum ESTableSerialChildWriteMode
    {
        [LabelText("按宿主重建子级")]
        [Tooltip("List 行导出时，只重建本次导出的宿主 SO 的子级行；不重建整张表，也不影响其他宿主")]
        RebuildByOwner,

        [LabelText("按子级 Key 合并")]
        [Tooltip("List 行导出时，按宿主 Key + 子级 Key 更新已有子级行，并追加新增子级行")]
        MergeByKey,

        [LabelText("仅更新已有子级")]
        [Tooltip("List 行导出时，只更新表格中已存在的子级行，不追加新增子级")]
        UpdateExistingOnly
    }

    public enum ESTableSerialChildImportSyncMode
    {
        [LabelText("保留表外子级")]
        [Tooltip("导入时，表格没有出现的旧子级继续保留")]
        KeepMissing,

        [LabelText("重建触达宿主子级")]
        [Tooltip("导入时，对本次表格触达的宿主 SO，先清空子级容器，再按表格顺序重建")]
        RebuildTouchedOwners,

        [LabelText("按表裁剪子级")]
        [Tooltip("导入时，只裁剪本次表格覆盖到的宿主 SO 子级。这个模式需要预检确认")]
        PruneMissingByTable,

        [LabelText("仅 delete 指令删除")]
        [Tooltip("导入时不自动裁剪，只有明确 delete 行指令才删除子级")]
        DeleteDirectiveOnly
    }
    public enum ESTableRowDirective
    {
        Normal,
        Skip,
        Required,
        Patch,
        Replace,
        Owner,
        Delete,
        Comment
    }

    public struct ESTableRowDirectiveInfo
    {
        public ESTableRowDirective directive;
        public string rawText;
        public bool debug;
        public string effectiveText;
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

    public enum ESTableBatchExecuteChoice
    {
        Execute,
        Plan,
        Cancel
    }

    public sealed class ESTablePlanRiskSummary
    {
        public int addedOwners;
        public int addedChildren;
        public int modifiedFields;
        public int skippedRows;
        public int errors;
        public int deleteOwners;
        public int deleteChildren;
        public int clearFields;
        public int overwriteNonEmptyFields;
        public int rebuildTables;
        public readonly List<string> errorLines = new List<string>();
        public readonly List<string> riskLines = new List<string>();

        public bool HasErrors => errors > 0;
        public bool HasHighRisk => deleteOwners > 0
            || deleteChildren > 0
            || clearFields > 0
            || overwriteNonEmptyFields > 0
            || rebuildTables > 0;
    }

    public enum ESSoTableRuleBindSourceKind
    {
        [LabelText("")]
        None,

        [LabelText("")]
        SoAsset,

        [LabelText("")]
        SoFolder,

        [LabelText("MonoScript")]
        MonoScript
    }

    public enum ESSoTableRuleObjectKind
    {
        [LabelText("SoData Pack/Group/Info")]
        SoData,

        [LabelText("普通 ScriptableObject")]
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
        [PropertyTooltip("普通状态下是否参与导入导出。强制启用/禁用会覆盖这个开关")]
        [TableColumnWidth(42, Resizable = false)]
        [PropertyOrder(-1)]
        [HideLabel]
        [ToggleLeft]
        public bool enabled = true;

        [LabelText("锁定")]
        [PropertyTooltip("锁定后不会被重建规则删除，也不会在导入时被表格写回覆盖")]
        public bool locked;

        [LabelText("导入策略")]
        [PropertyTooltip("表格覆盖：导入时表格覆盖 SO；保留现有值：SO 有值时不覆盖，SO 为空时允许补值；忽略：保留记录但不参与导入导出")]
        public ESTableColumnAuthority authority = ESTableColumnAuthority.SoAuthority;

        [LabelText("详情")]
        [PropertyTooltip("打开后显示写入方式、Key 标记、说明、表格类型和方向等低频字段")]
        public bool showDetail;

        [LabelText("SO 字段")]
        [TableColumnWidth(220, Resizable = true)]
        [PropertyOrder(0)]
        [PropertyTooltip("SO 对象里的字段路径，例如 name 或 config.value")]
        public string soFieldPath;

        [LabelText("写入方式")]
        [TableColumnWidth(110, Resizable = false)]
        [PropertyOrder(1)]
        [PropertyTooltip("控制导入时如何把表格文本写回 SO 字段")]
        public ESTableValueWriteMode valueWriteMode = ESTableValueWriteMode.PlainValue;

        [LabelText("InfoKey")]
        [TableColumnWidth(58, Resizable = false)]
        [PropertyOrder(2)]
        [PropertyTooltip("该列作为 Info 的唯一标识列")]
        public bool isInfoKey;

        [LabelText("GroupKey")]
        [TableColumnWidth(72, Resizable = false)]
        [PropertyOrder(3)]
        [PropertyTooltip("该列作为 Group 分组标识列")]
        public bool isGroupKey;

        [LabelText("SO 显示")]
        [TableColumnWidth(140, Resizable = true)]
        [PropertyOrder(4)]
        [PropertyTooltip("编辑器里给人看的名字，不要求和字段名一致")]
        public string displayName;

        [LabelText("说明")]
        [TableColumnWidth(240, Resizable = true)]
        [TextArea(1, 2)]
        [PropertyOrder(5)]
        [PropertyTooltip("写入表头注释行的说明文字")]
        public string comment;

        [LabelText("表格列名")]
        [TableColumnWidth(190, Resizable = true)]
        [PropertyOrder(6)]
        [PropertyTooltip("导出到 CSV/XLSX 的列名，也是导入时匹配列的名字")]
        public string columnName;

        [LabelText("表格类型")]
        [TableColumnWidth(105, Resizable = false)]
        [PropertyOrder(7)]
        [PropertyTooltip("写入表格表头类型行的类型名")]
        public string tableType;

        [LabelText("方向")]
        [TableColumnWidth(105, Resizable = false)]
        [PropertyOrder(8)]
        [PropertyTooltip("控制该字段参与导出、导入、双向，或完全忽略")]
        public ESSoTableRuleDirection direction = ESSoTableRuleDirection.Both;

        [LabelText("可用状态")]
        [PropertyTooltip("普通表示跟随启用开关；强制启用/强制禁用用于锁定关键字段")]
        public ESTableColumnAvailability availability = ESTableColumnAvailability.Normal;

        [LabelText("保留未映射列")]
        [PropertyTooltip("导出时尽量把旧表格里没有映射到 SO 字段的列追加保留下来，适合手工维护的备注列")]
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
        [LabelText("绑定来源")]
        [ReadOnly]
        public ESSoTableRuleBindSourceKind sourceKind;

        [TitleGroup("")]
        [LabelText("SO 文件")]
        [AssetsOnly]
        public ScriptableObject soAsset;

        [TitleGroup("")]
        [LabelText("SO 文件夹")]
        [AssetsOnly]
        public DefaultAsset soFolder;

        [TitleGroup("")]
        [LabelText("包含子文件夹")]
        public bool includeSubFolders = true;

        [TitleGroup("")]
        [LabelText("脚本")]
        [AssetsOnly]
        public MonoScript monoScript;

        [FoldoutGroup("")]
        [LabelText("文件夹资产")]
        [ReadOnly]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = false, ShowIndexLabels = true)]
        public List<ScriptableObject> folderAssets = new List<ScriptableObject>();

        [FoldoutGroup("", Expanded = false)]
        [LabelText("文件夹同步模式")]
        public ESTableFolderSyncMode folderSyncMode = ESTableFolderSyncMode.Incremental;

        [FoldoutGroup("")]
        [LabelText("允许创建缺失 SO")]
        public bool createMissingAssetsInFolder = true;

        [FoldoutGroup("")]
        [LabelText("允许更新已有 SO")]
        public bool updateExistingAssetsInFolder = true;

        [FoldoutGroup("")]
        [LabelText("来源 GUID")]
        [ReadOnly]
        public string sourceGuid;

        [FoldoutGroup("")]
        [LabelText("来源路径")]
        [ReadOnly]
        public string sourcePath;

        [FoldoutGroup("")]
        [LabelText("来源类型")]
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
        [PropertyTooltip("只用于生成或重建 Rule 字段映射，不用于批量导入导出")]
        [HideLabel]
        public ESSoTableRuleSourceBinding sourceBinding = new ESSoTableRuleSourceBinding();

        [LabelText("表格样本路径")]
        [PropertyTooltip("拖入或选择 CSV/XLSX，用它的表头生成字段映射")]
        [Sirenix.OdinInspector.FilePath(Extensions = "csv,xlsx", AbsolutePath = true)]
        public string tableFilePath;

        [LabelText("允许表头覆盖")]
        [PropertyTooltip("从表格样本重建时，允许表头结构覆盖现有字段映射")]
        public bool allowTableHeaderOverride = true;
    }

    [Serializable]
    [InlineProperty]
    [HideReferenceObjectPicker]
    public sealed class ESSoTableRuleUseBatch
    {
        [LabelText("启用")]
        [PropertyTooltip("关闭后执行全部批次时会跳过这一批")]
        public bool enabled = true;

        [LabelText("批次名")]
        [PropertyTooltip("给这一批导入导出配置起一个容易识别的名字")]
        public string batchName = "New Batch";

        [LabelText("执行方向")]
        [PropertyTooltip("控制这一批是导出、导入，还是先导入再导出")]
        public ESSoTableRuleUseDirection direction = ESSoTableRuleUseDirection.Export;

        [LabelText("超级批")]
        public bool useSuperBatch;

        [LabelText("超级批关系表")]
        [Sirenix.OdinInspector.FilePath(Extensions = "csv,xlsx", AbsolutePath = true)]
        public string superBatchTablePath;

        [LabelText("跳过无效关系行")]
        public bool superBatchSkipInvalidRows = true;

        [LabelText("数据来源")]
        [PropertyTooltip("这一批要处理的单个 SO 或 SO 文件夹")]
        [HideLabel]
        public ESSoTableRuleSourceBinding sourceBinding = new ESSoTableRuleSourceBinding();

        [LabelText("文件格式")]
        [PropertyTooltip("这一批输出或读取的表格格式")]
        public ESTableFileKind fileKind = ESTableFileKind.CsvAndXlsx;

        [LabelText("表格列名")]
        [PropertyTooltip("选择导出表头和导入匹配时使用英文列名，还是字段的中文显示名")]
        public ESTableColumnNameMode columnNameMode = ESTableColumnNameMode.English;

        [LabelText("文件名")]
        [PropertyTooltip("不带扩展名的 CSV/XLSX 文件名")]
        public string fileName;

        [LabelText("Sheet 名")]
        [PropertyTooltip("XLSX 工作表名。CSV 不使用")]
        public string sheetName;

        [LabelText("输出根目录")]
        [PropertyTooltip("相对项目根目录的表格输出目录")]
        [FolderPath(AbsolutePath = false)]
        public string outputRoot = "SoTableConfig/Tables";

        [LabelText("CSV 相对路径")]
        [PropertyTooltip("CSV 相对输出根目录的子路径")]
        [FolderPath(AbsolutePath = false)]
        public string csvRelativePath = "csv";

        [LabelText("XLSX 相对路径")]
        [PropertyTooltip("XLSX 相对输出根目录的子路径")]
        [FolderPath(AbsolutePath = false)]
        public string xlsxRelativePath = "xlsx";

        [LabelText("导入冲突")]
        [PropertyTooltip("导入表格写回 SO 时，遇到已有对象或冲突数据的处理策略")]
        public ESTableConflictPolicy importConflictPolicy = ESTableConflictPolicy.Overwrite;

        [LabelText("导出冲突")]
        [PropertyTooltip("导出表格时，目标文件已存在的处理策略")]
        public ESTableConflictPolicy exportConflictPolicy = ESTableConflictPolicy.Overwrite;

        [LabelText("导出写入模式")]
        [PropertyTooltip("控制导出到已存在表格时如何写入。默认按 Key 合并，避免重建表格导致备注、断言、未映射列丢失")]
        public ESTableExportWriteMode exportWriteMode = ESTableExportWriteMode.MergeByKey;

        [LabelText("子级导出模式")]
        [PropertyTooltip("当一行对应 List 子级元素时，控制子级行如何写入已有表格")]
        public ESTableSerialChildWriteMode serialChildWriteMode = ESTableSerialChildWriteMode.RebuildByOwner;

        [LabelText("子级导入同步")]
        [PropertyTooltip("当一行对应 List 子级元素时，控制表格缺失的旧子级是保留、重建、裁剪，还是只允许 delete 指令删除")]
        public ESTableSerialChildImportSyncMode serialChildImportSyncMode = ESTableSerialChildImportSyncMode.KeepMissing;

        [LabelText("仅生效字段")]
        [PropertyTooltip("逗号/分号分隔。留空表示全部字段。可写表格列名、SO 字段路径或显示名")]
        public string activeFields;

        [LabelText("排除字段")]
        [PropertyTooltip("逗号/分号分隔。优先级高于仅生效字段。可写表格列名、SO 字段路径或显示名")]
        public string excludedFields;

        [LabelText("应用范围")]
        [PropertyTooltip("导入写回时使用全量、片段截取，或仅写回某个 Group 下的某个 Info")]
        public ESTableBatchApplyRangeMode applyRangeMode = ESTableBatchApplyRangeMode.All;

        [LabelText("截取列名")]
        [PropertyTooltip("片段截取用的列名，可以是 Key 或其他表格列名。留空时使用 Info Key")]
        public string sliceColumnName;

        [LabelText("起点值")]
        [PropertyTooltip("片段截取起点。找到该值后开始应用")]
        public string sliceStartValue;

        [LabelText("终点值")]
        [PropertyTooltip("片段截取终点。找到该值后停止应用")]
        public string sliceEndValue;

        [LabelText("包含起点")]
        public bool includeSliceStart = true;

        [LabelText("包含终点")]
        public bool includeSliceEnd = true;

        [LabelText("目标 Group")]
        [PropertyTooltip("仅应用单个 Group/Info 时匹配的 Group 列值")]
        public string targetGroupKey;

        [LabelText("目标 Info")]
        [PropertyTooltip("仅应用单个 Group/Info 时匹配的 Info Key")]
        public string targetInfoKey;
    }
    [ESCreatePath("数据信息", "SO表格规则数据信息")]
    public partial class ESSoTableDataRule : SoDataInfo
    {
        private static readonly ESReflectionRowBridge RowBridge = new ESReflectionRowBridge();
        private static readonly JsonSerializerSettings CellJsonSettings = new JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.None,
            Converters = new List<JsonConverter> { new StringEnumConverter() }
        };
        [NonSerialized]
        private ESSoTableRuleUseBatch activeUseBatch;
        [NonSerialized]
        private string activeImportTablePath;

        [TitleGroup("")]
        [HorizontalGroup("", Width = 80)]
        [LabelText("")]
        public bool enabled = true;

        [HorizontalGroup("")]
        [LabelText("规则 Key")]
        public string ruleKey;

        [TitleGroup("")]
        [LabelText("")]
        [TextArea(2, 5)]
        public string description;

        [LabelText("Debug 行号")]
        [PropertyTooltip("用于行级 Debug 的表格行号，按 Excel 行号填写。表头第 1 行就是 1。")]
        public int debugRowNumber = 5;

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
        [LabelText("Group 截取方式")]
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

        [FoldoutGroup("导入导出策略")]
        [LabelText("导入时允许创建 Info")]
        public bool allowCreateInfoOnImport = true;

        [FoldoutGroup("导入导出策略")]
        [LabelText("导入时允许创建 Group")]
        public bool allowCreateGroupOnImport = true;

        [FoldoutGroup("导入导出策略")]
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
            TryExportTableFiles();
        }

        private bool TryExportTableFiles()
        {
            try
            {
                List<List<string>> table = BuildTableRows();
                if (table.Count == 0)
                {
                    Debug.LogWarning("No mapped columns available for export.", this);
                    return false;
                }

                ESTableFileKind currentFileKind = GetActiveFileKind();
                string csvPath = GetOutputPath(GetActiveCsvRelativePath(), ".csv");
                string xlsxPath = GetOutputPath(GetActiveXlsxRelativePath(), ".xlsx");
                List<List<string>> csvTable = null;
                List<List<string>> xlsxTable = null;

                if (currentFileKind == ESTableFileKind.Csv || currentFileKind == ESTableFileKind.CsvAndXlsx)
                {
                    csvTable = BuildExportWriteTable(table, csvPath);
                    if (csvTable == null)
                        return false;
                }

                if (currentFileKind == ESTableFileKind.Xlsx || currentFileKind == ESTableFileKind.CsvAndXlsx)
                {
                    xlsxTable = BuildExportWriteTable(table, xlsxPath);
                    if (xlsxTable == null)
                        return false;
                }

                if (csvTable != null)
                    WriteCsv(csvPath, csvTable);
                if (xlsxTable != null)
                    WriteXlsx(xlsxPath, xlsxTable, string.IsNullOrEmpty(GetActiveSheetName()) ? "Sheet1" : GetActiveSheetName());

                AssetDatabase.Refresh();
                Debug.Log($"Table export complete: {csvPath} / {xlsxPath}", this);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("Table export failed: " + e.Message + "\n" + e, this);
                return false;
            }
        }

        [TitleGroup("")]
        [HorizontalGroup("")]
        [Button("Auto Import")]
        public void ImportTableFileAuto()
        {
            TryImportTableFileAuto();
        }

        private bool TryImportTableFileAuto()
        {
            string path = ResolveExistingInputPath();
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("No CSV/XLSX file found for import.", this);
                return false;
            }

            return TryImportTableFile(path);
        }

        [TitleGroup("")]
        [HorizontalGroup("")]
        [Button("Import Selected Table")]
        public void ImportTableFileByPanel()
        {
            string path = EditorUtility.OpenFilePanel("Select CSV or XLSX table", GetOutputFolder(GetActiveCsvRelativePath()), "csv,xlsx");
            if (string.IsNullOrEmpty(path))
                return;

            TryImportTableFile(path);
        }

        private bool TryImportTableFile(string path)
        {
            try
            {
                List<List<string>> table = ReadTableFileAuto(path);
                if (table.Count < 5)
                {
                    Debug.LogWarning("SO Table import stopped: table needs 4 header rows and at least 1 data row.", this);
                    return false;
                }

                if (!ConfirmImportRiskBeforeWrite(table, path))
                    return false;

                int changedCount;
                activeImportTablePath = path;
                try
                {
                    changedCount = ApplyTableRowsToExistingObjects(table);
                }
                finally
                {
                    activeImportTablePath = null;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Table import complete: {path}, updated SO count: {changedCount}", this);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("Table import failed: " + e.Message + "\n" + e, this);
                return false;
            }
        }

        [TitleGroup("")]
        [HorizontalGroup("")]
        [Button("Generate Empty Example")]
        public void GenerateEmptyTableExample()
        {
            List<List<string>> table = BuildEmptyExampleTableRows();
            if (table.Count == 0)
            {
                Debug.LogWarning("没有可生成案例表的字段映射。请先生成字段映射。", this);
                return;
            }

            string file = FirstConfiguredName();
            if (string.IsNullOrWhiteSpace(file))
                file = "SoTableExample";
            file = file + "_Example";

            string root = Path.Combine(Application.dataPath, "..", "SoTableConfig", "Examples");
            string csvPath = Path.GetFullPath(Path.Combine(root, "csv", file + ".csv"));
            string xlsxPath = Path.GetFullPath(Path.Combine(root, "xlsx", file + ".xlsx"));
            WriteCsv(csvPath, table);
            WriteXlsx(xlsxPath, table, string.IsNullOrEmpty(GetActiveSheetName()) ? "Sheet1" : GetActiveSheetName());
            AssetDatabase.Refresh();
            Debug.Log("空表案例已生成：" + csvPath + " / " + xlsxPath, this);
        }

        private List<List<string>> BuildEmptyExampleTableRows()
        {
            List<ESTableColumnNameMap> enabledColumns = GetEnabledColumns();
            var table = new List<List<string>>();
            if (enabledColumns.Count == 0)
                return table;

            table.Add(BuildHeaderRow(header.varMark, enabledColumns, GetActiveTableColumnName));
            table.Add(BuildHeaderRow(header.typeMark, enabledColumns, c => string.IsNullOrEmpty(c.tableType) ? "string" : c.tableType));
            table.Add(BuildHeaderRow(header.groupMark, enabledColumns, c => string.IsNullOrEmpty(header.defaultGroup) ? "client" : header.defaultGroup));
            table.Add(BuildHeaderRow(header.commentMark, enabledColumns, c => string.IsNullOrEmpty(c.comment) ? c.displayName : c.comment));

            var assertRow = new List<string> { "##assert" };
            for (int i = 0; i < enabledColumns.Count; i++)
            {
                ESTableColumnNameMap column = enabledColumns[i];
                if (column.isInfoKey || IsObjectKeyColumn(column) || IsSerialChildRowKeyColumn(column))
                    assertRow.Add("required;unique");
                else if (string.Equals(column.tableType, "json", StringComparison.OrdinalIgnoreCase) || column.valueWriteMode == ESTableValueWriteMode.Json)
                    assertRow.Add("json");
                else
                    assertRow.Add(string.Empty);
            }
            table.Add(assertRow);

            var directiveRow = new List<string> { "##rowDirective | 空=正常导入；skip/ignore/disabled=跳过；comment:=备注；required=整行和Key必填；patch=只写非空；replace=强制覆盖；owner=只写SO本体；delete=删除；debug=打印本行追踪；debug:patch/debug:delete=按真实指令执行并打印" };
            for (int i = 0; i < enabledColumns.Count; i++)
                directiveRow.Add(string.Empty);
            table.Add(directiveRow);

            var demoRow = new List<string> { string.Empty };
            for (int i = 0; i < enabledColumns.Count; i++)
                demoRow.Add(BuildExampleCellValue(enabledColumns[i]));
            table.Add(demoRow);

            return table;
        }

        private string BuildExampleCellValue(ESTableColumnNameMap column)
        {
            if (column == null)
                return string.Empty;
            if (column.isInfoKey || IsObjectKeyColumn(column))
                return "example_001";
            if (IsSerialChildRowKeyColumn(column))
                return rowBinding != null && rowBinding.allowEmptyRowKey ? string.Empty : "row_001";

            string type = column.tableType ?? string.Empty;
            if (type.IndexOf("int", StringComparison.OrdinalIgnoreCase) >= 0)
                return "1";
            if (type.IndexOf("float", StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf("double", StringComparison.OrdinalIgnoreCase) >= 0)
                return "1.0";
            if (type.IndexOf("bool", StringComparison.OrdinalIgnoreCase) >= 0)
                return "true";
            if (column.valueWriteMode == ESTableValueWriteMode.Json || type.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0)
                return "{}";
            return "example";
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

            ESTableBatchExecuteChoice choice = ShowAllBatchesExecuteDialog();
            if (choice == ESTableBatchExecuteChoice.Cancel)
                return;
            if (choice == ESTableBatchExecuteChoice.Plan)
            {
                LogAllBatchesExecutionPlan();
                return;
            }

            ESTablePlanRiskSummary summary = BuildAllBatchesRiskSummary();
            if (!ConfirmPlanRiskBeforeExecute(summary, null))
                return;

            for (int i = 0; i < useBatches.Count; i++)
            {
                ESSoTableRuleUseBatch batch = useBatches[i];
                if (batch != null && batch.enabled)
                    ExecuteUseBatchDirect(batch);
            }
        }

        public void ExecuteUseBatch(ESSoTableRuleUseBatch batch)
        {
            if (batch == null)
                return;

            ESTableBatchExecuteChoice choice = ShowBatchExecuteDialog(batch);
            if (choice == ESTableBatchExecuteChoice.Cancel)
                return;
            if (choice == ESTableBatchExecuteChoice.Plan)
            {
                LogBatchExecutionPlan(batch);
                return;
            }

            ESTablePlanRiskSummary summary = BuildBatchRiskSummary(batch);
            if (!ConfirmPlanRiskBeforeExecute(summary, batch))
                return;

            ExecuteUseBatchDirect(batch);
        }

        private void ExecuteUseBatchDirect(ESSoTableRuleUseBatch batch)
        {
            if (batch == null)
                return;

            ESSoTableRuleUseBatch oldBatch = activeUseBatch;
            try
            {
                activeUseBatch = batch;
                if (batch.useSuperBatch)
                {
                    ExecuteSuperBatch(batch);
                    return;
                }

                if (batch.direction == ESSoTableRuleUseDirection.Export)
                {
                    TryExportTableFiles();
                    return;
                }

                if (batch.direction == ESSoTableRuleUseDirection.Import)
                {
                    TryImportTableFileAuto();
                    return;
                }

                if (TryImportTableFileAuto())
                    TryExportTableFiles();
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
            bool tableHasObjectKey = HasObjectKeyColumn(tableColumnMap);
            bool serialChildRows = rowBinding != null && rowBinding.IsListElementRow;
            bool pruneMissingChildren = serialChildRows && GetActiveSerialChildImportSyncMode() == ESTableSerialChildImportSyncMode.PruneMissingByTable;
            bool rebuildTouchedChildren = serialChildRows && GetActiveSerialChildImportSyncMode() == ESTableSerialChildImportSyncMode.RebuildTouchedOwners;
            Dictionary<ScriptableObject, HashSet<string>> importedChildKeysByOwner = pruneMissingChildren
                ? new Dictionary<ScriptableObject, HashSet<string>>()
                : null;
            HashSet<ScriptableObject> rebuiltChildOwners = rebuildTouchedChildren
                ? new HashSet<ScriptableObject>()
                : null;
            Dictionary<string, ISoDataGroup> groupsByKey = BuildGroupsByKey();
            ESTableBatchApplyFilter applyFilter = BuildApplyFilter(table, tableColumnMap);
            for (int rowIndex = GetDataStartRowIndex(table); rowIndex < table.Count; rowIndex++)
            {
                List<string> row = table[rowIndex];
                if (row == null || row.Count == 0)
                    continue;
                ESTableRowDirectiveInfo directive = ParseRowDirective(row);
                if (directive.directive == ESTableRowDirective.Skip || directive.directive == ESTableRowDirective.Comment)
                    continue;
                if (directive.directive == ESTableRowDirective.Required && IsDataRowEmpty(row))
                {
                    Debug.LogWarning("第 " + (rowIndex + 1) + " 行标记为 required，但整行为空，已跳过。", this);
                    continue;
                }
                if (!ShouldApplyTableRow(row, applyFilter))
                    continue;

                string groupKey = GetGroupKeyFromTableRow(row, tableColumnMap);
                ISoDataGroup targetGroup = ResolveImportGroup(groupKey, groupsByKey, ownerType, ownerType, row, tableColumnMap);
                ScriptableObject owner = FindOwnerForTableRow(row, tableColumnMap, ownersByKey, owners, ref ownerCursor, tableHasObjectKey);
                if (targetGroup != null && ownerType != null && typeof(ISoDataInfo).IsAssignableFrom(ownerType))
                {
                    string infoKey = GetObjectKeyFromTableRow(row, tableColumnMap);
                    if (directive.directive == ESTableRowDirective.Delete)
                    {
                        if (string.IsNullOrWhiteSpace(infoKey))
                        {
                            if (TryDeleteGroupAsset(targetGroup, out string deleteGroupReason))
                            {
                                RefreshPackAfterGroupDeletion(targetGroup);
                                changedCount++;
                            }
                            else
                            {
                                Debug.LogWarning("Row " + (rowIndex + 1) + " delete Group failed: " + deleteGroupReason, targetGroup as ScriptableObject);
                            }

                            continue;
                        }

                        if (TryDeleteInfoFromGroup(targetGroup, infoKey, out string deleteReason))
                        {
                            EditorUtility.SetDirty(targetGroup as ScriptableObject);
                            changedCount++;
                        }
                        else
                        {
                            Debug.LogWarning("第 " + (rowIndex + 1) + " 行删除 Group/Info 失败：" + deleteReason, targetGroup as ScriptableObject);
                        }
                        continue;
                    }

                    owner = ResolveInfoOwnerInGroup(targetGroup, infoKey, out bool createdInfo, out string resolveReason);
                    if (owner == null)
                    {
                        if (!string.IsNullOrEmpty(resolveReason))
                            Debug.LogWarning("第 " + (rowIndex + 1) + " 行 Group/Info 解析失败：" + resolveReason, targetGroup as ScriptableObject);
                        continue;
                    }

                    if (createdInfo)
                    {
                        changedCount++;
                        RefreshPackAfterGroupMutation(targetGroup);
                    }
                }

                if (owner == null)
                    owner = TryCreateOwnerForTableRow(row, tableColumnMap, ownerType, owners, ownersByKey);
                if (owner == null)
                    continue;

                object rowObject = owner;
                StringBuilder rowDebug = BuildRowDebugHeader(directive, rowIndex, owner);
                if (serialChildRows)
                {
                    string rowKey = rowKeyColumnIndex >= 0 && rowKeyColumnIndex < row.Count ? row[rowKeyColumnIndex] : string.Empty;
                    AppendRowDebugTarget(rowDebug, owner, rowKey);
                    bool emptyRowKeyAllowedByRebuild = string.IsNullOrWhiteSpace(rowKey) && CanImportEmptyChildKeyForRebuild(owner);
                    if (directive.directive == ESTableRowDirective.Required && string.IsNullOrWhiteSpace(rowKey) && !emptyRowKeyAllowedByRebuild)
                    {
                        Debug.LogWarning("第 " + (rowIndex + 1) + " 行标记为 required，但子级 Key 为空，已跳过。", owner);
                        continue;
                    }

                    if (directive.directive == ESTableRowDirective.Delete)
                    {
                        if (rowDebug != null)
                            rowDebug.AppendLine("操作：删除子级");
                        if (TryDeleteChildRow(owner, rowKey, out string deleteReason))
                        {
                            EditorUtility.SetDirty(owner);
                            if (rowDebug != null)
                                rowDebug.AppendLine("结果：删除子级成功");
                            changedCount++;
                        }
                        else
                        {
                            if (rowDebug != null)
                                rowDebug.AppendLine("结果：删除子级失败，" + deleteReason);
                            Debug.LogWarning("第 " + (rowIndex + 1) + " 行删除子级失败：" + deleteReason, owner);
                        }
                        if (rowDebug != null)
                            Debug.Log(rowDebug.ToString(), owner);
                        continue;
                    }

                    if (rebuildTouchedChildren && directive.directive != ESTableRowDirective.Owner && !EnsureChildRowsClearedForRebuild(owner, rebuiltChildOwners, out string rebuildReason))
                    {
                        Debug.LogWarning("第 " + (rowIndex + 1) + " 行子级重建失败：" + rebuildReason, owner);
                        continue;
                    }

                    if (directive.directive != ESTableRowDirective.Owner && emptyRowKeyAllowedByRebuild)
                    {
                        if (!TryCreateAppendedChildRow(owner, out rowObject, out string appendReason))
                        {
                            Debug.LogWarning("第 " + (rowIndex + 1) + " 行无 Key 子级追加失败：" + appendReason, owner);
                            continue;
                        }
                    }
                    else if (directive.directive != ESTableRowDirective.Owner && !RowBridge.TryGetOrCreateRow(owner, rowKey, rowBinding, out rowObject, out string reason))
                    {
                        Debug.LogWarning(reason, owner);
                        continue;
                    }

                    if (pruneMissingChildren && directive.directive != ESTableRowDirective.Owner && !string.IsNullOrWhiteSpace(rowKey))
                        AddImportedChildKey(importedChildKeysByOwner, owner, rowKey);
                }
                else if (directive.directive == ESTableRowDirective.Delete)
                {
                    if (rowDebug != null)
                        rowDebug.AppendLine("操作：删除 SO 资产");
                    if (TryDeleteOwnerAsset(owner, out string deleteReason))
                    {
                        if (rowDebug != null)
                            rowDebug.AppendLine("结果：删除 SO 成功");
                        changedCount++;
                    }
                    else
                    {
                        if (rowDebug != null)
                            rowDebug.AppendLine("结果：删除 SO 失败，" + deleteReason);
                        Debug.LogWarning("第 " + (rowIndex + 1) + " 行删除 SO 失败：" + deleteReason, owner);
                    }
                    if (rowDebug != null)
                        Debug.Log(rowDebug.ToString(), owner);
                    continue;
                }

                Undo.RecordObject(owner, "SO 表格导入写回");
                bool forceTableValues = directive.directive == ESTableRowDirective.Replace;
                bool skipEmptyCells = directive.directive == ESTableRowDirective.Patch;
                bool ownerColumnsOnly = directive.directive == ESTableRowDirective.Owner;
                if (rowDebug != null && !serialChildRows)
                    rowDebug.AppendLine("目标对象：SO 本体");
                ApplyTableRowToObject(row, compiledColumns, owner, rowObject, forceTableValues, skipEmptyCells, ownerColumnsOnly, rowDebug);
                EditorUtility.SetDirty(owner);
                changedCount++;
                if (rowDebug != null)
                    Debug.Log(rowDebug.ToString(), owner);
            }

            if (pruneMissingChildren)
                changedCount += PruneMissingChildRows(importedChildKeysByOwner);

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
                if (IsObjectKeyColumn(column))
                    return column;
            }

            return null;
        }

        private ScriptableObject FindOwnerForTableRow(List<string> row, Dictionary<int, ESTableColumnNameMap> tableColumnMap, Dictionary<string, ScriptableObject> ownersByKey, List<ScriptableObject> owners, ref int ownerCursor, bool tableHasObjectKey)
        {
            string key = null;
            foreach (KeyValuePair<int, ESTableColumnNameMap> pair in tableColumnMap)
            {
                ESTableColumnNameMap column = pair.Value;
                if (!IsObjectKeyColumn(column))
                    continue;

                key = pair.Key < row.Count ? row[pair.Key] : string.Empty;
                if (!string.IsNullOrEmpty(key))
                    break;
            }

            if (!string.IsNullOrEmpty(key) && ownersByKey.TryGetValue(key, out ScriptableObject keyedOwner))
                return keyedOwner;

            if (tableHasObjectKey)
                return null;

            if (ownerCursor < owners.Count)
                return owners[ownerCursor++];

            return null;
        }

        private bool HasObjectKeyColumn(Dictionary<int, ESTableColumnNameMap> tableColumnMap)
        {
            foreach (KeyValuePair<int, ESTableColumnNameMap> pair in tableColumnMap)
            {
                if (IsObjectKeyColumn(pair.Value))
                    return true;
            }

            return false;
        }

        private ScriptableObject TryCreateOwnerForTableRow(List<string> row, Dictionary<int, ESTableColumnNameMap> tableColumnMap, Type ownerType, List<ScriptableObject> owners, Dictionary<string, ScriptableObject> ownersByKey)
        {
            if (!CanCreateOwnerInActiveFolder(ownerType, out string folderPath))
                return null;

            string key = GetObjectKeyFromTableRow(row, tableColumnMap);
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogWarning("SO Table import skipped row: object key is empty, cannot create owner asset.", this);
                return null;
            }

            string assetName = SanitizeAssetFileName(key);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, assetName + ".asset"));
            var owner = ScriptableObject.CreateInstance(ownerType);
            owner.name = assetName;
            if (owner is ISoDataInfo info)
                info.SetKey(key);
            if (owner is IString stringKey)
                stringKey.SetSTR(key);

            AssetDatabase.CreateAsset(owner, assetPath);
            owners.Add(owner);
            ownersByKey[key] = owner;

            Debug.Log("SO Table import created owner asset: " + assetPath, owner);
            return owner;
        }

        private Dictionary<string, ISoDataGroup> BuildGroupsByKey()
        {
            var result = new Dictionary<string, ISoDataGroup>(StringComparer.OrdinalIgnoreCase);
            ESSoTableRuleSourceBinding source = GetActiveUseSourceBinding();
            if (source == null)
                return result;

            AddGroupCandidates(result, source.soAsset);

            if (source.folderAssets != null)
            {
                for (int i = 0; i < source.folderAssets.Count; i++)
                    AddGroupCandidates(result, source.folderAssets[i]);
            }

            return result;
        }

        private static void AddGroupCandidates(Dictionary<string, ISoDataGroup> result, ScriptableObject asset)
        {
            if (result == null || asset == null)
                return;

            if (asset is ISoDataGroup group)
            {
                string key = GetGroupAssetKey(group);
                if (!string.IsNullOrWhiteSpace(key) && !result.ContainsKey(key))
                    result[key] = group;
            }

            if (asset is ISoDataPack pack)
            {
                if (pack.CachingGroups != null)
                {
                    for (int i = 0; i < pack.CachingGroups.Count; i++)
                    {
                        ISoDataGroup cachedGroup = pack.CachingGroups[i];
                        if (cachedGroup == null)
                            continue;

                        string key = GetGroupAssetKey(cachedGroup);
                        if (!string.IsNullOrWhiteSpace(key) && !result.ContainsKey(key))
                            result[key] = cachedGroup;
                    }
                }
            }
        }

        private string GetGroupKeyFromTableRow(List<string> row, Dictionary<int, ESTableColumnNameMap> tableColumnMap)
        {
            if (tableColumnMap != null)
            {
                foreach (KeyValuePair<int, ESTableColumnNameMap> pair in tableColumnMap)
                {
                    ESTableColumnNameMap column = pair.Value;
                    if (column == null)
                        continue;

                    if (!column.isGroupKey && !string.Equals(GetActiveTableColumnName(column), groupColumnName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    return pair.Key < row.Count ? row[pair.Key] : string.Empty;
                }
            }

            return string.Empty;
        }

        private ISoDataGroup ResolveImportGroup(string groupKey, Dictionary<string, ISoDataGroup> groupsByKey, Type ownerType, Type groupOwnerType, List<string> row, Dictionary<int, ESTableColumnNameMap> tableColumnMap)
        {
            if (groupOwnerType == null || !typeof(ISoDataInfo).IsAssignableFrom(ownerType))
                return null;

            string effectiveKey = string.IsNullOrWhiteSpace(groupKey) ? GetActiveBatchTargetGroupKey() : groupKey;
            if (!string.IsNullOrWhiteSpace(effectiveKey) && groupsByKey != null && groupsByKey.TryGetValue(effectiveKey, out ISoDataGroup existingGroup))
                return existingGroup;

            if (groupsByKey != null && groupsByKey.Count == 1 && string.IsNullOrWhiteSpace(effectiveKey))
            {
                foreach (ISoDataGroup single in groupsByKey.Values)
                    return single;
            }

            if (!allowCreateGroupOnImport)
                return null;

            if (string.IsNullOrWhiteSpace(effectiveKey))
                return null;

            if (typeBinding == null || typeBinding.GroupType == null)
                return null;

            if (!CanCreateGroupInActiveFolder(out string folderPath))
                return null;

            ScriptableObject group = ScriptableObject.CreateInstance(typeBinding.GroupType);
            if (group == null)
                return null;

            string assetName = SanitizeAssetFileName(effectiveKey);
            group.name = assetName;
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, assetName + ".asset"));
            AssetDatabase.CreateAsset(group, assetPath);
            EditorUtility.SetDirty(group);

            ISoDataGroup createdGroup = group as ISoDataGroup;
            if (createdGroup != null)
            {
                if (groupsByKey != null)
                    groupsByKey[effectiveKey] = createdGroup;

                Debug.Log("SO Table import created group asset: " + assetPath, group);
                return createdGroup;
            }

            AssetDatabase.DeleteAsset(assetPath);
            return null;
        }

        private ScriptableObject ResolveInfoOwnerInGroup(ISoDataGroup targetGroup, string infoKey, out bool createdInfo, out string reason)
        {
            createdInfo = false;
            reason = string.Empty;

            if (targetGroup == null)
            {
                reason = "Group 目标为空。";
                return null;
            }

            if (string.IsNullOrWhiteSpace(infoKey))
            {
                reason = "Info Key 为空，无法在 Group 中定位或创建。";
                return null;
            }

            ISoDataInfo existing = targetGroup.GetInfoByKey(infoKey);
            if (existing is ScriptableObject existingObject)
                return existingObject;

            if (!allowCreateInfoOnImport)
            {
                reason = "当前规则不允许在导入时创建 Info。";
                return null;
            }

            Type infoType = targetGroup.GetSOInfoType();
            if (infoType == null || !typeof(ScriptableObject).IsAssignableFrom(infoType))
            {
                reason = "Group 未提供可创建的 Info 类型。";
                return null;
            }

            if (!CanWriteSubAssetToGroup(targetGroup, out string groupAssetPath))
            {
                reason = "无法写入 Group 子资产。";
                return null;
            }

            ScriptableObject infoAsset = ScriptableObject.CreateInstance(infoType);
            if (infoAsset == null)
            {
                reason = "创建 Info 实例失败。";
                return null;
            }

            infoAsset.name = SanitizeAssetFileName(infoKey);
            if (infoAsset is ISoDataInfo info)
                info.SetKey(infoKey);
            if (infoAsset is IString stringKey)
                stringKey.SetSTR(infoKey);

            Undo.RecordObject(targetGroup as ScriptableObject, "SO Table Create Group Info");
            AssetDatabase.AddObjectToAsset(infoAsset, groupAssetPath);
            targetGroup._TryAddInfoToDic(infoKey, infoAsset);
            EditorUtility.SetDirty(targetGroup as ScriptableObject);
            AssetDatabase.ImportAsset(groupAssetPath, ImportAssetOptions.ForceUpdate);
            createdInfo = true;
            return infoAsset;
        }

        private bool TryDeleteInfoFromGroup(ISoDataGroup targetGroup, string infoKey, out string reason)
        {
            reason = string.Empty;
            if (targetGroup == null)
            {
                reason = "Group 为空。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(infoKey))
            {
                reason = "Info Key 为空，无法删除。";
                return false;
            }

            ISoDataInfo info = targetGroup.GetInfoByKey(infoKey);
            ScriptableObject infoAsset = info as ScriptableObject;
            if (infoAsset == null)
            {
                reason = "未找到可删除的 Info 资产。";
                return false;
            }

            string groupAssetPath = AssetDatabase.GetAssetPath(targetGroup as ScriptableObject);
            if (!string.IsNullOrEmpty(groupAssetPath))
                Undo.RecordObject(targetGroup as ScriptableObject, "SO Table Delete Group Info");

            targetGroup._RemoveInfoFromDic(infoKey);
            Undo.DestroyObjectImmediate(infoAsset);
            if (!string.IsNullOrEmpty(groupAssetPath))
                AssetDatabase.ImportAsset(groupAssetPath, ImportAssetOptions.ForceUpdate);

            return true;
        }

        private bool TryDeleteGroupAsset(ISoDataGroup targetGroup, out string reason)
        {
            reason = string.Empty;
            if (targetGroup == null)
            {
                reason = "Group 为空。";
                return false;
            }

            ScriptableObject groupAsset = targetGroup as ScriptableObject;
            if (groupAsset == null)
            {
                reason = "Group 不是可删除的 ScriptableObject。";
                return false;
            }

            string groupAssetPath = AssetDatabase.GetAssetPath(groupAsset);
            if (string.IsNullOrWhiteSpace(groupAssetPath))
            {
                reason = "Group 没有有效资源路径。";
                return false;
            }

            return AssetDatabase.DeleteAsset(groupAssetPath);
        }

        private void RefreshPackAfterGroupMutation(ISoDataGroup targetGroup)
        {
            if (targetGroup == null)
                return;

            ESSoTableRuleSourceBinding source = GetActiveUseSourceBinding();
            if (source == null)
                return;

            HashSet<ISoDataPack> packs = new HashSet<ISoDataPack>();
            CollectPacksForRefresh(source.soAsset, packs);
            if (source.folderAssets != null)
            {
                for (int i = 0; i < source.folderAssets.Count; i++)
                    CollectPacksForRefresh(source.folderAssets[i], packs);
            }

            foreach (ISoDataPack pack in packs)
            {
                if (pack == null)
                    continue;

                if (pack is ScriptableObject packAsset)
                    Undo.RecordObject(packAsset, "SO Table Refresh Pack");

                pack._AddInfosFromGroup(targetGroup);
                if (pack is ScriptableObject dirtyPack)
                    EditorUtility.SetDirty(dirtyPack);
            }
        }

        private void RefreshPackAfterGroupDeletion(ISoDataGroup deletedGroup)
        {
            if (deletedGroup == null)
                return;

            ESSoTableRuleSourceBinding source = GetActiveUseSourceBinding();
            if (source == null)
                return;

            HashSet<ISoDataPack> packs = new HashSet<ISoDataPack>();
            CollectPacksForRefresh(source.soAsset, packs);
            if (source.folderAssets != null)
            {
                for (int i = 0; i < source.folderAssets.Count; i++)
                    CollectPacksForRefresh(source.folderAssets[i], packs);
            }

            foreach (ISoDataPack pack in packs)
            {
                if (pack == null)
                    continue;

                List<ISoDataGroup> cachedGroups = pack.CachingGroups;
                if (cachedGroups != null)
                    cachedGroups.RemoveAll(g => g == null || ReferenceEquals(g, deletedGroup));

                pack.Check();
                if (pack is ScriptableObject dirtyPack)
                    EditorUtility.SetDirty(dirtyPack);
            }
        }

        private static void CollectPacksForRefresh(UnityEngine.Object asset, HashSet<ISoDataPack> result)
        {
            if (asset == null || result == null)
                return;

            if (asset is ISoDataPack pack)
                result.Add(pack);
        }

        private bool CanCreateGroupInActiveFolder(out string folderPath)
        {
            folderPath = null;
            ESSoTableRuleSourceBinding source = GetActiveUseSourceBinding();
            if (source == null)
                return false;

            if (source.soFolder != null)
                folderPath = AssetDatabase.GetAssetPath(source.soFolder);
            else if (source.soAsset != null)
                folderPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(source.soAsset));

            return !string.IsNullOrWhiteSpace(folderPath) && AssetDatabase.IsValidFolder(folderPath);
        }

        private string GetActiveBatchTargetGroupKey()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null ? batch.targetGroupKey : string.Empty;
        }

        private static string GetGroupAssetKey(ISoDataGroup group)
        {
            if (group == null)
                return string.Empty;

            if (group is ScriptableObject so)
                return so.name;

            return group.FileName;
        }

        private static bool CanWriteSubAssetToGroup(ISoDataGroup group, out string groupAssetPath)
        {
            groupAssetPath = null;
            if (group == null)
                return false;

            groupAssetPath = AssetDatabase.GetAssetPath(group as ScriptableObject);
            return !string.IsNullOrWhiteSpace(groupAssetPath);
        }

        private bool CanCreateOwnerInActiveFolder(Type ownerType, out string folderPath)
        {
            folderPath = null;
            if (ownerType == null || ownerType.IsAbstract || !typeof(ScriptableObject).IsAssignableFrom(ownerType))
                return false;

            ESSoTableRuleSourceBinding source = GetActiveUseSourceBinding();
            if (source == null || source.soFolder == null || !source.createMissingAssetsInFolder)
                return false;

            folderPath = AssetDatabase.GetAssetPath(source.soFolder);
            return AssetDatabase.IsValidFolder(folderPath);
        }

        private string GetObjectKeyFromTableRow(List<string> row, Dictionary<int, ESTableColumnNameMap> tableColumnMap)
        {
            foreach (KeyValuePair<int, ESTableColumnNameMap> pair in tableColumnMap)
            {
                if (!IsObjectKeyColumn(pair.Value))
                    continue;

                return pair.Key < row.Count ? row[pair.Key] : string.Empty;
            }

            return null;
        }

        private bool IsObjectKeyColumn(ESTableColumnNameMap column)
        {
            if (column == null || IsSerialChildRowKeyColumn(column))
                return false;
            if (column.isInfoKey)
                return true;

            return IsLikelyObjectKeyName(column.columnName) || IsLikelyObjectKeyName(column.soFieldPath);
        }

        private bool IsSerialChildRowKeyColumn(ESTableColumnNameMap column)
        {
            if (column == null || rowBinding == null || !rowBinding.IsListElementRow)
                return false;

            string rowKeyPath = BuildListElementPath(rowBinding.elementKeyFieldPath);
            if (!string.IsNullOrWhiteSpace(column.soFieldPath) && string.Equals(column.soFieldPath, rowKeyPath, StringComparison.Ordinal))
                return true;

            return !string.IsNullOrWhiteSpace(column.columnName)
                && string.Equals(column.columnName, rowBinding.rowKeyColumnName, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(column.soFieldPath)
                && column.soFieldPath.StartsWith(rowBinding.listFieldPath + "[].", StringComparison.Ordinal);
        }

        private static bool IsLikelyObjectKeyName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string normalized = value.Replace("_", string.Empty).Replace("-", string.Empty).Replace(".", string.Empty).Trim();
            return normalized.Equals("id", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("key", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("itemid", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("keyname", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("rulekey", StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeAssetFileName(string value)
        {
            string name = string.IsNullOrWhiteSpace(value) ? "ImportedSO" : value.Trim();
            char[] invalidChars = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidChars.Length; i++)
                name = name.Replace(invalidChars[i], '_');

            return string.IsNullOrWhiteSpace(name) ? "ImportedSO" : name;
        }

        private static string GetProjectRelativePath(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return string.Empty;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string normalizedFullPath = Path.GetFullPath(fullPath);
            if (normalizedFullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                return normalizedFullPath.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return normalizedFullPath;
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

        private bool ConfirmImportRiskBeforeWrite(List<List<string>> table, string path)
        {
            ESTablePlanRiskSummary assertionSummary = BuildImportAssertionSummary(table);
            if (assertionSummary.HasErrors)
            {
                var errorBuilder = new StringBuilder();
                errorBuilder.AppendLine("表格断言预检失败，已阻止写入 SO。");
                errorBuilder.AppendLine("表格：" + path);
                for (int i = 0; i < assertionSummary.errorLines.Count; i++)
                    errorBuilder.AppendLine(assertionSummary.errorLines[i]);
                Debug.LogWarning(errorBuilder.ToString(), this);
                EditorUtility.DisplayDialog("SO 表格断言失败", "表格断言预检失败，已阻止写入 SO。请查看 Console 里的行列错误。", "知道了");
                return false;
            }

            int deleteRows = CountDirectiveRows(table, ESTableRowDirective.Delete);
            bool pruneMissingChildren = rowBinding != null
                && rowBinding.IsListElementRow
                && GetActiveSerialChildImportSyncMode() == ESTableSerialChildImportSyncMode.PruneMissingByTable;
            bool rebuildTouchedChildren = rowBinding != null
                && rowBinding.IsListElementRow
                && GetActiveSerialChildImportSyncMode() == ESTableSerialChildImportSyncMode.RebuildTouchedOwners;

            if (deleteRows <= 0 && !pruneMissingChildren && !rebuildTouchedChildren)
                return true;

            var builder = new StringBuilder();
            builder.AppendLine("SO 表格导入将执行高风险操作。");
            builder.AppendLine();
            builder.AppendLine("表格：" + path);
            if (deleteRows > 0)
                builder.AppendLine("delete 指令行数：" + deleteRows);
            if (pruneMissingChildren)
                builder.AppendLine("子级裁剪：已启用。触达宿主下表格缺失的旧子级可能被删除。");
            if (rebuildTouchedChildren)
                builder.AppendLine("子级重建：已启用。触达宿主下的子级会先清空，再按表格重建。");
            builder.AppendLine();
            builder.AppendLine("点击取消将停止写入，不会修改 SO。");

            return EditorUtility.DisplayDialog("SO 表格高风险导入", builder.ToString(), "继续", "取消");
        }

        private ESTablePlanRiskSummary BuildImportAssertionSummary(List<List<string>> table)
        {
            var summary = new ESTablePlanRiskSummary();
            if (table == null || FindAssertRowIndex(table) < 0)
                return summary;

            List<ESTableColumnNameMap> enabledColumns = GetEnabledColumns();
            Dictionary<string, ESTableColumnNameMap> columnMap = BuildColumnMap(enabledColumns);
            Dictionary<int, ESTableColumnNameMap> tableColumnMap = BuildTableColumnMap(table, columnMap);
            Type ownerType = typeBinding.RowOwnerType;
            Type listElementType = GetConfiguredListElementType(ownerType);
            List<ESTableCompiledColumn> compiledColumns = CompileColumns(enabledColumns, ownerType, listElementType, tableColumnMap);
            ESTableBatchApplyFilter applyFilter = BuildApplyFilter(table, tableColumnMap);
            ValidateColumnAssertions(table, compiledColumns, applyFilter, summary);
            return summary;
        }

        private static int CountDirectiveRows(List<List<string>> table, ESTableRowDirective directive)
        {
            if (table == null)
                return 0;

            int count = 0;
            for (int rowIndex = GetDataStartRowIndex(table); rowIndex < table.Count; rowIndex++)
            {
                if (ParseRowDirective(table[rowIndex]).directive == directive)
                    count++;
            }

            return count;
        }

        private static ESTableRowDirectiveInfo ParseRowDirective(List<string> row)
        {
            string raw = GetCell(row, 0).Trim();
            var result = new ESTableRowDirectiveInfo
            {
                rawText = raw,
                directive = ESTableRowDirective.Normal
            };

            if (string.IsNullOrWhiteSpace(raw))
                return result;

            string normalized = raw.Trim().ToLowerInvariant();
            if (normalized == "debug")
            {
                result.debug = true;
                result.effectiveText = string.Empty;
                return result;
            }

            if (normalized.StartsWith("debug:", StringComparison.Ordinal))
            {
                result.debug = true;
                normalized = normalized.Substring("debug:".Length).Trim();
                result.effectiveText = normalized;
            }

            int chineseStart = normalized.IndexOf('(');
            if (chineseStart >= 0)
                normalized = normalized.Substring(0, chineseStart).Trim();

            if (normalized.StartsWith("comment", StringComparison.Ordinal))
                result.directive = ESTableRowDirective.Comment;
            else if (normalized == "skip" || normalized == "ignore" || normalized == "disabled")
                result.directive = ESTableRowDirective.Skip;
            else if (normalized == "required")
                result.directive = ESTableRowDirective.Required;
            else if (normalized == "patch")
                result.directive = ESTableRowDirective.Patch;
            else if (normalized == "replace")
                result.directive = ESTableRowDirective.Replace;
            else if (normalized == "owner")
                result.directive = ESTableRowDirective.Owner;
            else if (normalized == "delete")
                result.directive = ESTableRowDirective.Delete;

            if (string.IsNullOrEmpty(result.effectiveText))
                result.effectiveText = normalized;

            return result;
        }

        private static bool IsDataRowEmpty(List<string> row)
        {
            if (row == null)
                return true;

            for (int i = 1; i < row.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(row[i]))
                    return false;
            }

            return true;
        }

        private static int GetDataStartRowIndex(List<List<string>> table)
        {
            int index = 4;
            if (table == null)
                return index;

            while (index < table.Count)
            {
                string marker = GetCell(table[index], 0).Trim();
                if (!marker.StartsWith("##", StringComparison.Ordinal))
                    break;
                index++;
            }

            return index;
        }

        private static void AddImportedChildKey(Dictionary<ScriptableObject, HashSet<string>> importedChildKeysByOwner, ScriptableObject owner, string rowKey)
        {
            if (importedChildKeysByOwner == null || owner == null || string.IsNullOrWhiteSpace(rowKey))
                return;

            if (!importedChildKeysByOwner.TryGetValue(owner, out HashSet<string> keys))
            {
                keys = new HashSet<string>(StringComparer.Ordinal);
                importedChildKeysByOwner[owner] = keys;
            }

            keys.Add(rowKey);
        }

        private int PruneMissingChildRows(Dictionary<ScriptableObject, HashSet<string>> importedChildKeysByOwner)
        {
            if (importedChildKeysByOwner == null || importedChildKeysByOwner.Count == 0)
                return 0;

            int changedCount = 0;
            foreach (KeyValuePair<ScriptableObject, HashSet<string>> pair in importedChildKeysByOwner)
            {
                ScriptableObject owner = pair.Key;
                HashSet<string> keepKeys = pair.Value;
                if (owner == null || keepKeys == null)
                    continue;

                int removed = PruneMissingChildRows(owner, keepKeys);
                if (removed > 0)
                {
                    EditorUtility.SetDirty(owner);
                    changedCount += removed;
                }
            }

            return changedCount;
        }

        private int PruneMissingChildRows(ScriptableObject owner, HashSet<string> keepKeys)
        {
            if (owner == null || keepKeys == null || rowBinding == null || !rowBinding.IsListElementRow)
                return 0;

            IDictionary dictionary = RowBridge.EnsureDictionary(owner, rowBinding);
            if (dictionary != null)
            {
                var removeKeys = new List<object>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    string key = entry.Key != null ? entry.Key.ToString() : string.Empty;
                    if (!keepKeys.Contains(key))
                        removeKeys.Add(entry.Key);
                }

                if (removeKeys.Count == 0)
                    return 0;

                Undo.RecordObject(owner, "SO Table Prune Child Rows");
                for (int i = 0; i < removeKeys.Count; i++)
                    dictionary.Remove(removeKeys[i]);
                return removeKeys.Count;
            }

            IList list = RowBridge.EnsureContainer(owner, rowBinding);
            if (list == null)
                return 0;

            int removed = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                object element = list[i];
                string key = ESRowBindingReflectionUtility.GetMemberValue(element, rowBinding.elementKeyFieldPath)?.ToString();
                if (string.IsNullOrWhiteSpace(key) || keepKeys.Contains(key))
                    continue;

                if (removed == 0)
                    Undo.RecordObject(owner, "SO Table Prune Child Rows");
                list.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        private bool EnsureChildRowsClearedForRebuild(ScriptableObject owner, HashSet<ScriptableObject> rebuiltOwners, out string reason)
        {
            reason = string.Empty;
            if (owner == null)
            {
                reason = "宿主 SO 为空。";
                return false;
            }
            if (rebuiltOwners == null)
                return true;
            if (rebuiltOwners.Contains(owner))
                return true;
            if (rowBinding == null || !rowBinding.IsListElementRow)
            {
                reason = "当前规则不是子级行模式。";
                return false;
            }

            IDictionary dictionary = RowBridge.EnsureDictionary(owner, rowBinding);
            if (dictionary != null)
            {
                Undo.RecordObject(owner, "SO Table Rebuild Child Rows");
                dictionary.Clear();
                rebuiltOwners.Add(owner);
                return true;
            }

            IList list = RowBridge.EnsureContainer(owner, rowBinding);
            if (list == null)
            {
                reason = "无法获取子级 List：" + rowBinding.listFieldPath;
                return false;
            }

            Undo.RecordObject(owner, "SO Table Rebuild Child Rows");
            list.Clear();
            rebuiltOwners.Add(owner);
            return true;
        }

        private bool TryCreateAppendedChildRow(ScriptableObject owner, out object rowObject, out string reason)
        {
            rowObject = null;
            reason = string.Empty;
            if (owner == null)
            {
                reason = "宿主 SO 为空。";
                return false;
            }
            if (rowBinding == null || !rowBinding.IsListElementRow)
            {
                reason = "当前规则不是子级行模式。";
                return false;
            }
            if (RowBridge.EnsureDictionary(owner, rowBinding) != null)
            {
                reason = "Dictionary 子级不能使用空 Key；请填写 Key，或改用 List 子级。";
                return false;
            }

            IList list = RowBridge.EnsureContainer(owner, rowBinding);
            if (list == null)
            {
                reason = "无法获取子级 List：" + rowBinding.listFieldPath;
                return false;
            }

            Type elementType = ESRowBindingReflectionUtility.GetListElementType(list.GetType());
            if (elementType == null)
            {
                reason = "无法推导 List 元素类型：" + rowBinding.listFieldPath;
                return false;
            }

            rowObject = Activator.CreateInstance(elementType);
            if (!string.IsNullOrWhiteSpace(rowBinding.elementKeyFieldPath))
                ESRowBindingReflectionUtility.SetMemberValue(rowObject, rowBinding.elementKeyFieldPath, string.Empty);
            list.Add(rowObject);
            return true;
        }

        private bool CanImportEmptyChildKeyForRebuild(ScriptableObject owner)
        {
            if (owner == null || rowBinding == null || !rowBinding.IsListElementRow || !rowBinding.allowEmptyRowKey)
                return false;
            if (GetActiveSerialChildImportSyncMode() != ESTableSerialChildImportSyncMode.RebuildTouchedOwners)
                return false;

            return RowBridge.EnsureDictionary(owner, rowBinding) == null;
        }

        private bool TryDeleteChildRow(ScriptableObject owner, string rowKey, out string reason)
        {
            reason = string.Empty;
            if (owner == null)
            {
                reason = "宿主 SO 为空。";
                return false;
            }
            if (rowBinding == null || !rowBinding.IsListElementRow)
            {
                reason = "当前规则不是 List 子级行。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(rowKey))
            {
                reason = "子级 Key 为空。";
                return false;
            }

            IDictionary dictionary = RowBridge.EnsureDictionary(owner, rowBinding);
            if (dictionary != null)
            {
                if (!dictionary.Contains(rowKey))
                {
                    reason = "找不到子级 Key：" + rowKey;
                    return false;
                }

                Undo.RecordObject(owner, "SO Table Delete Child Row");
                dictionary.Remove(rowKey);
                return true;
            }

            IList list = RowBridge.EnsureContainer(owner, rowBinding);
            if (list == null)
            {
                reason = "无法获取子级容器：" + rowBinding.listFieldPath;
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                object element = list[i];
                string elementKey = ESRowBindingReflectionUtility.GetMemberValue(element, rowBinding.elementKeyFieldPath)?.ToString();
                if (string.Equals(elementKey, rowKey, StringComparison.Ordinal))
                {
                    Undo.RecordObject(owner, "SO Table Delete Child Row");
                    list.RemoveAt(i);
                    return true;
                }
            }

            reason = "找不到子级 Key：" + rowKey;
            return false;
        }

        private static bool TryDeleteOwnerAsset(ScriptableObject owner, out string reason)
        {
            reason = string.Empty;
            if (owner == null)
            {
                reason = "目标 SO 为空。";
                return false;
            }

            string path = AssetDatabase.GetAssetPath(owner);
            if (string.IsNullOrWhiteSpace(path))
            {
                reason = "目标不是可删除的普通资产。";
                return false;
            }

            return AssetDatabase.DeleteAsset(path);
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

        private StringBuilder BuildRowDebugHeader(ESTableRowDirectiveInfo directive, int rowIndex, ScriptableObject owner)
        {
            if (!directive.debug)
                return null;

            var builder = new StringBuilder();
            builder.AppendLine("SO 表格行 Debug");
            builder.AppendLine("表格行号：" + (rowIndex + 1));
            builder.AppendLine("原始行指令：" + (string.IsNullOrEmpty(directive.rawText) ? "(空)" : directive.rawText));
            builder.AppendLine("实际行指令：" + directive.directive);
            builder.AppendLine("批次：" + (GetActiveUseBatch() != null ? GetActiveUseBatch().batchName : "(默认批次)"));
            builder.AppendLine("导入表格：" + (string.IsNullOrEmpty(activeImportTablePath) ? "(未记录)" : activeImportTablePath));
            builder.AppendLine("目标 SO：" + (owner != null ? owner.name : "(空)"));
            builder.AppendLine("SO 类型：" + (owner != null ? owner.GetType().FullName : "(空)"));
            builder.AppendLine("SO 路径：" + (owner != null ? AssetDatabase.GetAssetPath(owner) : "(空)"));
            return builder;
        }

        private void AppendRowDebugTarget(StringBuilder debugLog, ScriptableObject owner, string rowKey)
        {
            if (debugLog == null)
                return;

            debugLog.AppendLine("目标对象：List 子级");
            debugLog.AppendLine("子级 Key：" + (string.IsNullOrWhiteSpace(rowKey) ? "(空)" : rowKey));
            debugLog.AppendLine("容器字段：" + (rowBinding != null ? rowBinding.listFieldPath : string.Empty));
            debugLog.AppendLine("宿主路径：" + (owner != null ? AssetDatabase.GetAssetPath(owner) : "(空)"));
        }

        private static void AppendFieldDebug(StringBuilder debugLog, ESTableCompiledColumn column, string message)
        {
            if (debugLog == null)
                return;

            string columnName = column != null && column.map != null ? column.map.columnName : "(未知列)";
            string fieldPath = column != null ? column.memberPath : "(未知字段)";
            string target = column != null && column.useRowObject ? "子级" : "SO";
            debugLog.AppendLine("字段[" + columnName + "] -> " + target + "." + fieldPath + "：" + message);
        }

        private static string FormatDebugValue(object value)
        {
            if (value == null)
                return "(null)";
            if (value is UnityEngine.Object unityObject)
                return unityObject == null ? "(null)" : unityObject.name + " <" + AssetDatabase.GetAssetPath(unityObject) + ">";
            return value.ToString();
        }

        private void ApplyTableRowToObject(List<string> row, List<ESTableCompiledColumn> compiledColumns, ScriptableObject owner, object rowObject, bool forceTableValues, bool skipEmptyCells, bool ownerColumnsOnly, StringBuilder debugLog = null)
        {
            for (int i = 0; i < compiledColumns.Count; i++)
            {
                ESTableCompiledColumn column = compiledColumns[i];
                if (!column.canWrite || column.tableColumnIndex < 0 || column.tableColumnIndex >= row.Count)
                {
                    AppendFieldDebug(debugLog, column, "跳过：列不可写或表格索引无效");
                    continue;
                }
                if (ownerColumnsOnly && column.useRowObject)
                {
                    AppendFieldDebug(debugLog, column, "跳过：owner 行只写 SO 本体字段");
                    continue;
                }
                if (skipEmptyCells && string.IsNullOrWhiteSpace(row[column.tableColumnIndex]))
                {
                    AppendFieldDebug(debugLog, column, "跳过：patch 行空单元格不写回");
                    continue;
                }

                object target = column.useRowObject ? rowObject : owner;
                if (!forceTableValues && column.map.authority == ESTableColumnAuthority.SoAuthority)
                {
                    object currentValue = ESRowBindingReflectionUtility.GetMemberValue(target, column.memberPath);
                    if (!IsEmptyAuthorityValue(currentValue))
                    {
                        AppendFieldDebug(debugLog, column, "跳过：导入策略为保留现有值，当前值非空，当前值=" + FormatDebugValue(currentValue));
                        continue;
                    }
                }

                object oldValue = ESRowBindingReflectionUtility.GetMemberValue(target, column.memberPath);
                object value = ConvertStringToValue(row[column.tableColumnIndex], column.valueType, column.map.valueWriteMode);
                ESRowBindingReflectionUtility.SetMemberValue(target, column.memberPath, value);
                AppendFieldDebug(debugLog, column, "写入：" + FormatDebugValue(oldValue) + " -> " + FormatDebugValue(value));
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
                if (column != null && column.IsUsable && !string.IsNullOrWhiteSpace(column.columnName) && IsColumnAllowedByActiveBatch(column))
                    enabledColumns.Add(column);
            }

            return enabledColumns;
        }

        private bool IsColumnAllowedByActiveBatch(ESTableColumnNameMap column)
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            if (batch == null || column == null)
                return true;

            HashSet<string> excluded = BuildFieldNameSet(batch.excludedFields);
            if (MatchesFieldNameSet(column, excluded))
                return false;

            HashSet<string> active = BuildFieldNameSet(batch.activeFields);
            return active.Count == 0 || MatchesFieldNameSet(column, active);
        }

        private static HashSet<string> BuildFieldNameSet(string text)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text))
                return result;

            string[] parts = text.Split(new[] { ',', ';', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string item = parts[i].Trim();
                if (!string.IsNullOrEmpty(item))
                    result.Add(item);
            }

            return result;
        }

        private static bool MatchesFieldNameSet(ESTableColumnNameMap column, HashSet<string> names)
        {
            if (column == null || names == null || names.Count == 0)
                return false;

            return (!string.IsNullOrWhiteSpace(column.columnName) && names.Contains(column.columnName))
                || (!string.IsNullOrWhiteSpace(column.soFieldPath) && names.Contains(column.soFieldPath))
                || (!string.IsNullOrWhiteSpace(column.displayName) && names.Contains(column.displayName));
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

        private List<List<string>> BuildExportWriteTable(List<List<string>> newTable, string path)
        {
            if (newTable == null)
                return null;

            ESTableExportWriteMode mode = GetActiveExportWriteMode();
            ESTableSerialChildWriteMode childMode = GetActiveSerialChildWriteMode();
            bool serialChildRows = rowBinding != null && rowBinding.IsListElementRow;
            if (mode == ESTableExportWriteMode.Rebuild || string.IsNullOrEmpty(path) || !File.Exists(path))
                return CloneTable(newTable);

            List<List<string>> oldTable = ReadTableFileAuto(path);
            if (oldTable == null || oldTable.Count == 0)
                return CloneTable(newTable);

            if (serialChildRows && childMode == ESTableSerialChildWriteMode.RebuildByOwner && rowBinding.allowEmptyRowKey)
                return BuildExportWriteTableRebuildOwnersAllowEmptyKeys(newTable, oldTable);

            int oldKeyIndex = FindExportObjectKeyColumnIndex(oldTable);
            int newKeyIndex = FindExportObjectKeyColumnIndex(newTable);
            if (oldKeyIndex < 0 || newKeyIndex < 0)
            {
                Debug.LogWarning("Export stopped: missing object Key column. Configure an Info Key/object Key column before exporting to an existing table.", this);
                return null;
            }

            int oldRowKeyIndex = FindExportRowKeyColumnIndex(oldTable);
            int newRowKeyIndex = FindExportRowKeyColumnIndex(newTable);
            if (rowBinding != null && rowBinding.IsListElementRow && (oldRowKeyIndex < 0 || newRowKeyIndex < 0))
            {
                Debug.LogWarning("Export stopped: list-row export requires a row Key column when updating an existing table.", this);
                return null;
            }

            List<List<string>> result = CloneTable(oldTable);
            MergeExportHeaders(result, newTable);

            Dictionary<string, int> oldRowsByKey = BuildExportRowsByKey(result, oldKeyIndex, oldRowKeyIndex, out string oldKeyError);
            if (!string.IsNullOrEmpty(oldKeyError))
            {
                Debug.LogWarning("Export stopped: existing table has invalid Key data. " + oldKeyError, this);
                return null;
            }

            Dictionary<string, int> resultColumnMap = BuildRawHeaderIndexMap(result);
            Dictionary<string, int> newColumnMap = BuildRawHeaderIndexMap(newTable);
            HashSet<string> newKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> touchedOwnerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int rowIndex = GetDataStartRowIndex(newTable); rowIndex < newTable.Count; rowIndex++)
            {
                List<string> newRow = newTable[rowIndex];
                string key = BuildExportRowKey(newRow, newKeyIndex, newRowKeyIndex);
                if (string.IsNullOrWhiteSpace(key))
                {
                    Debug.LogWarning("Export stopped: new table contains an empty Key at row " + (rowIndex + 1) + ".", this);
                    return null;
                }
                if (!newKeys.Add(key))
                {
                    Debug.LogWarning("Export stopped: duplicate Key in new table: " + key + ".", this);
                    return null;
                }
                if (serialChildRows)
                {
                    string ownerKey = BuildExportObjectKey(newRow, newKeyIndex);
                    if (!string.IsNullOrWhiteSpace(ownerKey))
                        touchedOwnerKeys.Add(ownerKey);
                }

                if (oldRowsByKey.TryGetValue(key, out int targetRowIndex))
                {
                    MergeExportDataRow(result[targetRowIndex], newRow, newColumnMap, resultColumnMap);
                    continue;
                }

                if (ShouldAppendExportRow(serialChildRows, mode, childMode))
                {
                    List<string> appended = new List<string>();
                    EnsureCellCount(appended, result[0].Count);
                    MergeExportDataRow(appended, newRow, newColumnMap, resultColumnMap);
                    result.Add(appended);
                    oldRowsByKey[key] = result.Count - 1;
                }
            }

            if (serialChildRows && childMode == ESTableSerialChildWriteMode.RebuildByOwner)
                RemoveMissingSerialChildRowsForOwners(result, oldKeyIndex, oldRowKeyIndex, touchedOwnerKeys, newKeys);

            return result;
        }

        private ESTableExportWriteMode GetActiveExportWriteMode()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null ? batch.exportWriteMode : ESTableExportWriteMode.MergeByKey;
        }

        private ESTableConflictPolicy GetActiveImportConflictPolicy()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null ? batch.importConflictPolicy : ESTableConflictPolicy.Overwrite;
        }

        private ESTableSerialChildWriteMode GetActiveSerialChildWriteMode()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null ? batch.serialChildWriteMode : ESTableSerialChildWriteMode.RebuildByOwner;
        }

        private ESTableSerialChildImportSyncMode GetActiveSerialChildImportSyncMode()
        {
            ESSoTableRuleUseBatch batch = GetActiveUseBatch();
            return batch != null ? batch.serialChildImportSyncMode : ESTableSerialChildImportSyncMode.KeepMissing;
        }

        private static bool ShouldAppendExportRow(bool serialChildRows, ESTableExportWriteMode tableMode, ESTableSerialChildWriteMode childMode)
        {
            if (!serialChildRows)
                return tableMode == ESTableExportWriteMode.MergeByKey;

            return childMode == ESTableSerialChildWriteMode.MergeByKey
                || childMode == ESTableSerialChildWriteMode.RebuildByOwner;
        }

        private void RemoveMissingSerialChildRowsForOwners(List<List<string>> table, int objectKeyIndex, int rowKeyIndex, HashSet<string> touchedOwnerKeys, HashSet<string> newKeys)
        {
            if (table == null || touchedOwnerKeys == null || touchedOwnerKeys.Count == 0 || newKeys == null)
                return;

            for (int rowIndex = table.Count - 1; rowIndex >= GetDataStartRowIndex(table); rowIndex--)
            {
                List<string> row = table[rowIndex];
                string ownerKey = BuildExportObjectKey(row, objectKeyIndex);
                if (string.IsNullOrWhiteSpace(ownerKey) || !touchedOwnerKeys.Contains(ownerKey))
                    continue;

                string fullKey = BuildExportRowKey(row, objectKeyIndex, rowKeyIndex);
                if (!newKeys.Contains(fullKey))
                    table.RemoveAt(rowIndex);
            }
        }

        private List<List<string>> BuildExportWriteTableRebuildOwnersAllowEmptyKeys(List<List<string>> newTable, List<List<string>> oldTable)
        {
            int oldObjectKeyIndex = FindExportObjectKeyColumnIndex(oldTable);
            int newObjectKeyIndex = FindExportObjectKeyColumnIndex(newTable);
            if (oldObjectKeyIndex < 0 || newObjectKeyIndex < 0)
            {
                Debug.LogWarning("Export stopped: missing object Key column. Empty child Key rebuild still requires an owner/object Key column.", this);
                return null;
            }

            List<List<string>> result = CloneTable(oldTable);
            MergeExportHeaders(result, newTable);

            Dictionary<string, int> resultColumnMap = BuildRawHeaderIndexMap(result);
            Dictionary<string, int> newColumnMap = BuildRawHeaderIndexMap(newTable);
            HashSet<string> touchedOwnerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int rowIndex = GetDataStartRowIndex(newTable); rowIndex < newTable.Count; rowIndex++)
            {
                string ownerKey = BuildExportObjectKey(newTable[rowIndex], newObjectKeyIndex);
                if (string.IsNullOrWhiteSpace(ownerKey))
                {
                    Debug.LogWarning("Export stopped: new table contains an empty owner Key at row " + (rowIndex + 1) + ".", this);
                    return null;
                }

                touchedOwnerKeys.Add(ownerKey);
            }

            for (int rowIndex = result.Count - 1; rowIndex >= GetDataStartRowIndex(result); rowIndex--)
            {
                string ownerKey = BuildExportObjectKey(result[rowIndex], oldObjectKeyIndex);
                if (!string.IsNullOrWhiteSpace(ownerKey) && touchedOwnerKeys.Contains(ownerKey))
                    result.RemoveAt(rowIndex);
            }

            for (int rowIndex = GetDataStartRowIndex(newTable); rowIndex < newTable.Count; rowIndex++)
            {
                List<string> appended = new List<string>();
                EnsureCellCount(appended, result[0].Count);
                MergeExportDataRow(appended, newTable[rowIndex], newColumnMap, resultColumnMap);
                result.Add(appended);
            }

            return result;
        }

        private int FindExportObjectKeyColumnIndex(List<List<string>> table)
        {
            int index = FindRawTableColumnIndex(table, GetActiveInfoKeyColumnName());
            if (index >= 0)
                return index;

            string[] names = { "itemId", "id", "key", "KeyName", "name" };
            for (int i = 0; i < names.Length; i++)
            {
                index = FindRawTableColumnIndex(table, names[i]);
                if (index >= 0)
                    return index;
            }

            return -1;
        }

        private int FindExportRowKeyColumnIndex(List<List<string>> table)
        {
            return rowBinding != null && rowBinding.IsListElementRow
                ? FindRawTableColumnIndex(table, rowBinding.rowKeyColumnName)
                : -1;
        }

        private string BuildExportRowKey(List<string> row, int objectKeyIndex, int rowKeyIndex)
        {
            string objectKey = BuildExportObjectKey(row, objectKeyIndex);
            if (string.IsNullOrWhiteSpace(objectKey))
                return string.Empty;

            if (rowBinding == null || !rowBinding.IsListElementRow)
                return objectKey;

            string rowKey = GetCell(row, rowKeyIndex).Trim();
            if (string.IsNullOrWhiteSpace(rowKey))
                return string.Empty;

            return objectKey + "\u001f" + rowKey;
        }

        private static string BuildExportObjectKey(List<string> row, int objectKeyIndex)
        {
            return GetCell(row, objectKeyIndex).Trim();
        }

        private Dictionary<string, int> BuildExportRowsByKey(List<List<string>> table, int objectKeyIndex, int rowKeyIndex, out string error)
        {
            error = string.Empty;
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int rowIndex = GetDataStartRowIndex(table); rowIndex < table.Count; rowIndex++)
            {
                string key = BuildExportRowKey(table[rowIndex], objectKeyIndex, rowKeyIndex);
                if (string.IsNullOrWhiteSpace(key))
                {
                    error = "Empty Key at row " + (rowIndex + 1) + ".";
                    return result;
                }
                if (result.ContainsKey(key))
                {
                    error = "Duplicate Key " + key + " at row " + (rowIndex + 1) + ".";
                    return result;
                }

                result[key] = rowIndex;
            }

            return result;
        }

        private static Dictionary<string, int> BuildRawHeaderIndexMap(List<List<string>> table)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (table == null || table.Count == 0)
                return result;

            List<string> headerRow = table[0];
            for (int i = 1; i < headerRow.Count; i++)
            {
                string name = headerRow[i];
                if (!string.IsNullOrWhiteSpace(name) && !result.ContainsKey(name))
                    result[name] = i;
            }

            return result;
        }

        private static void MergeExportHeaders(List<List<string>> result, List<List<string>> newTable)
        {
            EnsureRowCount(result, Math.Min(4, newTable.Count));
            Dictionary<string, int> resultColumnMap = BuildRawHeaderIndexMap(result);
            for (int columnIndex = 1; newTable.Count > 0 && columnIndex < newTable[0].Count; columnIndex++)
            {
                string columnName = newTable[0][columnIndex];
                if (string.IsNullOrWhiteSpace(columnName) || resultColumnMap.ContainsKey(columnName))
                    continue;

                int addedIndex = result[0].Count;
                for (int rowIndex = 0; rowIndex < result.Count; rowIndex++)
                    EnsureCellCount(result[rowIndex], addedIndex + 1);
                for (int headerRow = 0; headerRow < 4 && headerRow < newTable.Count; headerRow++)
                    result[headerRow][addedIndex] = GetCell(newTable[headerRow], columnIndex);

                resultColumnMap[columnName] = addedIndex;
            }
        }

        private static void MergeExportDataRow(List<string> targetRow, List<string> sourceRow, Dictionary<string, int> sourceColumnMap, Dictionary<string, int> targetColumnMap)
        {
            EnsureCellCount(targetRow, targetColumnMap.Count + 1);
            foreach (KeyValuePair<string, int> pair in sourceColumnMap)
            {
                if (!targetColumnMap.TryGetValue(pair.Key, out int targetIndex))
                    continue;

                EnsureCellCount(targetRow, targetIndex + 1);
                targetRow[targetIndex] = GetCell(sourceRow, pair.Value);
            }
        }

        private static List<List<string>> CloneTable(List<List<string>> table)
        {
            var result = new List<List<string>>();
            if (table == null)
                return result;

            for (int i = 0; i < table.Count; i++)
                result.Add(table[i] == null ? new List<string>() : new List<string>(table[i]));
            return result;
        }

        private static void EnsureRowCount(List<List<string>> table, int count)
        {
            while (table.Count < count)
                table.Add(new List<string>());
        }

        private static void EnsureCellCount(List<string> row, int count)
        {
            while (row.Count < count)
                row.Add(string.Empty);
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

    }

}
#endif
