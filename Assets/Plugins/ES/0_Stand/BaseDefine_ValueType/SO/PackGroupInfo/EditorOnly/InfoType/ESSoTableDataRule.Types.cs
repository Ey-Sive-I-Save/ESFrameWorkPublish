#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ES.Internal;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace ES
{
    #region Public Types
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
    #endregion
}
#endif
