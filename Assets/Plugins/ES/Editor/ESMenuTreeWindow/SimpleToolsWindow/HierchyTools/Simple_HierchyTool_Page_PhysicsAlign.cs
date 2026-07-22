using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ES
{
    #region 智能对齐与分布工具
    /// <summary>
    /// 商业级智能对齐与分布工具
    /// 支持2D/3D/UI多场景，提供精确对齐、均匀分布、尺寸匹配等高级功能
    /// </summary>
    [Serializable]
    public class Page_PhysicsAlign : ESWindowPageBase
    {
        #region 标题与说明
        [Title("智能对齐与分布工具", "专业级对象对齐、分布、匹配工具", bold: true, titleAlignment: TitleAlignments.Centered)]

        
        [PropertySpace(10)]
        [ShowInInspector, ReadOnly, DisplayAsString, HideLabel, PropertyOrder(-10)]
        private string PanelSummary =>
            $"当前选择: {(Selection.transforms != null ? Selection.transforms.Length : 0)} 个对象 | 对齐: {alignMode} | 分布: {distributeMode} | 坐标: {coordinateMode} | 边界: {boundsMode}";

        private string lastResultSummary = "";
        private string lastResultDetail = "";
        private string auditSearch = "";
        private bool showOnlyRisks = false;
        private List<TransformAuditRecord> auditRecords = new List<TransformAuditRecord>();
        private const int LargeTransformOperationThreshold = 200;

        [OnInspectorGUI, PropertyOrder(-200)]
        private void DrawResultPanel()
        {
            SimpleToolsPanelUtility.DrawToolHeader(
                "对齐、分布与布景微调工作台",
                "用于 3D / 2D / UI 对象的批量对齐、均匀分布、尺寸匹配、落地吸附、网格吸附和轻微随机化。",
                SimpleToolsMaturity.Upgrading,
                "会直接修改 Transform 或 RectTransform。建议开启执行前确认；父子对象同时选中时保持“跳过重复子级”，避免重复位移。");
            SimpleToolsPanelUtility.DrawSummary(
                "有效选区: " + GetValidSelection().Length,
                "审计项: " + auditRecords.Count,
                "预览中: " + (isPreviewing ? "是" : "否"),
                "确认: " + (confirmBeforeApply ? "开" : "关"),
                "Prefab资产保护: " + (protectPrefabAssets ? "开" : "关"));
            SimpleToolsPanelUtility.DrawSectionTitle("使用顺序", "先刷新选区审计，再选择对齐/分布/匹配/落地类动作；分布预览满意后再应用。");
            DrawWorkflowShortcuts();
            if (GetValidSelection().Length >= LargeTransformOperationThreshold)
                SimpleToolsPanelUtility.DrawWarning($"当前有效选区超过 {LargeTransformOperationThreshold} 个对象，大批量 Transform 操作会强制二次确认，并建议先刷新审计。");
            SimpleToolsPanelUtility.DrawResultSummary("最近对齐操作", lastResultSummary, lastResultDetail);
        }

        private void DrawWorkflowShortcuts()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("快捷工作流", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("刷新审计", SimpleToolsActionTone.Primary, 28, GUILayout.Width(88)))
                        RefreshSelectionAudit();
                    if (SimpleToolsPanelUtility.DrawActionButton("吸附到表面", SimpleToolsActionTone.Success, 28, GUILayout.Width(96)))
                        ExecuteSnapToSurface();
                    if (SimpleToolsPanelUtility.DrawActionButton("网格吸附", SimpleToolsActionTone.Success, 28, GUILayout.Width(88)))
                        ExecuteGridSnap();
                    if (SimpleToolsPanelUtility.DrawActionButton("随机错落", SimpleToolsActionTone.Warning, 28, GUILayout.Width(88)))
                        ExecuteRandomDressing();
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("执行对齐", SimpleToolsActionTone.Warning, 28, GUILayout.Width(88)))
                        AlignObjects();
                    if (SimpleToolsPanelUtility.DrawActionButton("预览分布", SimpleToolsActionTone.Primary, 28, GUILayout.Width(88)))
                        PreviewDistributeObjects();
                    if (SimpleToolsPanelUtility.DrawActionButton("应用预览", SimpleToolsActionTone.Success, 28, GUILayout.Width(88)))
                        ApplyDistributionPreview();
                    if (SimpleToolsPanelUtility.DrawActionButton("清除预览", SimpleToolsActionTone.Neutral, 28, GUILayout.Width(88)))
                        ClearDistributionPreview();
                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.LabelField("下方标签页保留完整高级按钮。首屏快捷区只放高频动作，减少找按钮成本。", EditorStyles.wordWrappedMiniLabel);
            }
        }
        #endregion

        #region 审计数据
        [Serializable]
        private class TransformAuditRecord
        {
            [ReadOnly, TableColumnWidth(170, false), LabelText("对象")]
            public GameObject Object;

            [ReadOnly, TableColumnWidth(220, false), LabelText("路径")]
            public string Path;

            [ReadOnly, TableColumnWidth(90, false), LabelText("类型")]
            public string Kind;

            [ReadOnly, TableColumnWidth(90, false), LabelText("边界")]
            public string BoundsSource;

            [ReadOnly, TableColumnWidth(120, false), LabelText("尺寸")]
            public string Size;

            [ReadOnly, TableColumnWidth(70, false), LabelText("Prefab")]
            public string PrefabState;

            [ReadOnly, TableColumnWidth(70, false), LabelText("风险")]
            public string RiskLevel;

            [ReadOnly, TableColumnWidth(260, false), LabelText("提示")]
            public string Note;

            [Button("定位", ButtonSizes.Small), TableColumnWidth(48, false)]
            private void Ping()
            {
                if (Object == null) return;
                Selection.activeGameObject = Object;
                EditorGUIUtility.PingObject(Object);
            }
        }

        private struct TransformChangeSnapshot
        {
            public Vector3 Position;
            public Vector3 LocalPosition;
            public Vector3 LocalScale;
            public Quaternion Rotation;
            public Vector2 RectSize;
            public bool IsRectTransform;
        }
        #endregion

        #region 对齐模式枚举
        public enum AlignMode
        {
            [LabelText("左对齐")] Left,
            [LabelText("右对齐")] Right,
            [LabelText("上对齐")] Top,
            [LabelText("下对齐")] Bottom,
            [LabelText("前对齐(Z轴)")] Front,
            [LabelText("后对齐(Z轴)")] Back,
            [LabelText("水平居中")] HorizontalCenter,
            [LabelText("垂直居中")] VerticalCenter,
            [LabelText("深度居中(Z轴)")] DepthCenter,
            [LabelText("镜头左对齐")] CameraLeft,
            [LabelText("镜头右对齐")] CameraRight,
            [LabelText("镜头上对齐")] CameraTop,
            [LabelText("镜头下对齐")] CameraBottom,
            [LabelText("镜头前对齐")] CameraFront,
            [LabelText("镜头后对齐")] CameraBack,
            [LabelText("镜头水平居中")] CameraHorizontalCenter,
            [LabelText("镜头垂直居中")] CameraVerticalCenter,
            [LabelText("镜头深度居中")] CameraDepthCenter,
        }

        public enum DistributeMode
        {
            [LabelText("水平均匀分布")] HorizontalEven,
            [LabelText("垂直均匀分布")] VerticalEven,
            [LabelText("深度均匀分布(Z轴)")] DepthEven,
            [LabelText("水平间距分布")] HorizontalSpacing,
            [LabelText("垂直间距分布")] VerticalSpacing,
            [LabelText("深度间距分布(Z轴)")] DepthSpacing,
            [LabelText("镜头水平均匀分布")] CameraHorizontalEven,
            [LabelText("镜头垂直均匀分布")] CameraVerticalEven,
            [LabelText("镜头深度均匀分布")] CameraDepthEven,
            [LabelText("镜头水平间距分布")] CameraHorizontalSpacing,
            [LabelText("镜头垂直间距分布")] CameraVerticalSpacing,
            [LabelText("镜头深度间距分布")] CameraDepthSpacing,
        }

        public enum AlignReference
        {
            [LabelText("所有对象边界")] AllBounds,
            [LabelText("第一个选中对象")] FirstSelected,
            [LabelText("最后选中对象")] LastSelected,
            [LabelText("父对象中心")] ParentCenter,
            [LabelText("世界中心")] WorldCenter,
        }

        public enum BoundsCalculationMode
        {
            [LabelText("自动检测")] Auto,
            [LabelText("Renderer边界")] Renderer,
            [LabelText("Collider边界")] Collider,
            [LabelText("RectTransform(UI)")] RectTransform,
            [LabelText("仅Transform位置")] TransformOnly,
        }

        public enum CoordinateMode
        {
            [LabelText("世界坐标")] WorldSpace,
            [LabelText("局部坐标")] LocalSpace,
            [LabelText("相对镜头")] CameraRelative,
        }

        public enum MatchReferenceMode
        {
            [LabelText("第一个选中对象")] FirstSelected,
            [LabelText("最后选中对象")] LastSelected,
            [LabelText("父对象")] Parent,
        }
        #endregion

        #region 预览系统字段
        private Dictionary<Transform, Vector3> originalPositions = new Dictionary<Transform, Vector3>();
        private bool isPreviewing = false;
        private int previewUndoGroup = -1;
        private const float ProjectionEpsilon = 0.0001f;
        #endregion

        #region 基础设置
        [TabGroup("对齐", "基础对齐")]
        [HorizontalGroup("对齐/基础对齐/Settings")]
        [VerticalGroup("对齐/基础对齐/Settings/Left"), LabelWidth(100)]
        [LabelText("对齐模式"), PropertySpace(5)]
        public AlignMode alignMode = AlignMode.Left;

        [VerticalGroup("对齐/基础对齐/Settings/Left"), LabelWidth(100)]
        [LabelText("参考对象"), PropertySpace(5)]
        public AlignReference alignReference = AlignReference.AllBounds;

        [VerticalGroup("对齐/基础对齐/Settings/Right"), LabelWidth(100)]
        [LabelText("边界计算模式"), PropertySpace(5)]
        public BoundsCalculationMode boundsMode = BoundsCalculationMode.Auto;

        [VerticalGroup("对齐/基础对齐/Settings/Right"), LabelWidth(100)]
        [LabelText("坐标系模式"), PropertySpace(5)]
        public CoordinateMode coordinateMode = CoordinateMode.CameraRelative;

        [TabGroup("对齐", "基础对齐")]
        [PropertySpace(10)]
        [HorizontalGroup("对齐/基础对齐/AlignButtons")]
        [Button("← 左对齐", ButtonHeight = 30), GUIColor(0.3f, 0.8f, 0.8f)]
        private void QuickAlignLeft() { alignMode = AlignMode.Left; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/AlignButtons")]
        [Button("→ 右对齐", ButtonHeight = 30), GUIColor(0.3f, 0.8f, 0.8f)]
        private void QuickAlignRight() { alignMode = AlignMode.Right; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/AlignButtons")]
        [Button("↑ 上对齐", ButtonHeight = 30), GUIColor(0.3f, 0.8f, 0.8f)]
        private void QuickAlignTop() { alignMode = AlignMode.Top; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/AlignButtons")]
        [Button("↓ 下对齐", ButtonHeight = 30), GUIColor(0.3f, 0.8f, 0.8f)]
        private void QuickAlignBottom() { alignMode = AlignMode.Bottom; AlignObjects(); }

        [TabGroup("对齐", "基础对齐")]
        [HorizontalGroup("对齐/基础对齐/CenterButtons" )]
        [Button("⊟ 水平居中", ButtonHeight = 30), GUIColor(0.5f, 0.7f, 0.9f)]
        private void QuickAlignHCenter() { alignMode = AlignMode.HorizontalCenter; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/CenterButtons")]
        [Button("⊞ 垂直居中", ButtonHeight = 30), GUIColor(0.5f, 0.7f, 0.9f)]
        private void QuickAlignVCenter() { alignMode = AlignMode.VerticalCenter; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/CenterButtons")]
        [Button("◎ 深度居中", ButtonHeight = 30), GUIColor(0.5f, 0.7f, 0.9f)]
        private void QuickAlignDepthCenter() { alignMode = AlignMode.DepthCenter; AlignObjects(); }

        [TabGroup("对齐", "基础对齐")]
        [InfoBox("📦 深度对齐（Z轴）：前后位置对齐控制", InfoMessageType.None)]
        [HorizontalGroup("对齐/基础对齐/DepthButtons")]
        [Button("◀ 前对齐(近)", ButtonHeight = 30), GUIColor(0.6f, 0.8f, 0.6f)]
        private void QuickAlignFront() { alignMode = AlignMode.Front; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/DepthButtons")]
        [Button("▶ 后对齐(远)", ButtonHeight = 30), GUIColor(0.6f, 0.8f, 0.6f)]
        private void QuickAlignBack() { alignMode = AlignMode.Back; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/DepthButtons")]
        [Button("⬌ 深度居中", ButtonHeight = 30), GUIColor(0.6f, 0.8f, 0.6f)]
        private void QuickAlignDepthCenter2() { alignMode = AlignMode.DepthCenter; AlignObjects(); }

        [TabGroup("对齐", "基础对齐")]
        [HorizontalGroup("对齐/基础对齐/CameraAlignButtons")]
        [Button("⬅ 镜头左对齐", ButtonHeight = 30), GUIColor(0.4f, 0.6f, 0.8f)]
        private void QuickAlignCameraLeft() { alignMode = AlignMode.CameraLeft; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/CameraAlignButtons")]
        [Button("➡ 镜头右对齐", ButtonHeight = 30), GUIColor(0.4f, 0.6f, 0.8f)]
        private void QuickAlignCameraRight() { alignMode = AlignMode.CameraRight; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/CameraAlignButtons")]
        [Button("⬆ 镜头上对齐", ButtonHeight = 30), GUIColor(0.4f, 0.6f, 0.8f)]
        private void QuickAlignCameraTop() { alignMode = AlignMode.CameraTop; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/CameraAlignButtons")]
        [Button("⬇ 镜头下对齐", ButtonHeight = 30), GUIColor(0.4f, 0.6f, 0.8f)]
        private void QuickAlignCameraBottom() { alignMode = AlignMode.CameraBottom; AlignObjects(); }

        [TabGroup("对齐", "基础对齐")]
        [HorizontalGroup("对齐/基础对齐/CameraCenterButtons")]
        [Button("⬌ 镜头水平居中", ButtonHeight = 30), GUIColor(0.5f, 0.7f, 0.8f)]
        private void QuickAlignCameraHCenter() { alignMode = AlignMode.CameraHorizontalCenter; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/CameraCenterButtons")]
        [Button("⬍ 镜头垂直居中", ButtonHeight = 30), GUIColor(0.5f, 0.7f, 0.8f)]
        private void QuickAlignCameraVCenter() { alignMode = AlignMode.CameraVerticalCenter; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/CameraCenterButtons")]
        [Button("⬊ 镜头深度居中", ButtonHeight = 30), GUIColor(0.5f, 0.7f, 0.8f)]
        private void QuickAlignCameraDepthCenter() { alignMode = AlignMode.CameraDepthCenter; AlignObjects(); }
        #endregion

        #region 分布设置
        [TabGroup("对齐", "智能分布")]
        [InfoBox("分布会按当前位置排序后移动对象；固定间距模式会使用下方间距值。", InfoMessageType.Info)]

        [TabGroup("对齐", "智能分布")]
        [HorizontalGroup("对齐/智能分布/Settings")]
        [VerticalGroup("对齐/智能分布/Settings/Left"), LabelWidth(100)]
        [LabelText("分布模式"), PropertySpace(5)]
        [OnValueChanged("OnDistributeModeChanged")]
        public DistributeMode distributeMode = DistributeMode.HorizontalEven;

        [VerticalGroup("对齐/智能分布/Settings/Left"), LabelWidth(100)]
        [LabelText("固定间距"), ShowIf("@IsSpacingDistribute()"), PropertySpace(5)]
        [MinValue(0)]
        public float distributionSpacing = 10f;

        [VerticalGroup("对齐/智能分布/Settings/Right"), LabelWidth(100)]
        [LabelText("保持相对顺序"), PropertySpace(5)]
        public bool maintainOrder = true;

        [VerticalGroup("对齐/智能分布/Settings/Right"), LabelWidth(100)]
        [LabelText("反向排列"), PropertySpace(5)]
        public bool reverseOrder = false;

        [VerticalGroup("对齐/智能分布/Settings/Right"), LabelWidth(100)]
        [LabelText("预览模式"), PropertySpace(5)]
        public bool previewMode = false;

        [TabGroup("对齐", "智能分布")]
        [InfoBox("动态间距只影响间距分布模式；拖动时会刷新预览。", InfoMessageType.Info)]

        [TabGroup("对齐", "智能分布")]
        [HorizontalGroup("对齐/智能分布/DynamicSpacing")]
        [VerticalGroup("对齐/智能分布/DynamicSpacing/Left"), LabelWidth(120)]
        [LabelText("启用实时间距调整"), PropertySpace(5)]
        public bool enableDynamicSpacing = false;

        [VerticalGroup("对齐/智能分布/DynamicSpacing/Left"), LabelWidth(120)]
        [LabelText("当前间距"), ShowIf("@enableDynamicSpacing"), PropertySpace(5)]
        [Range(0f, 50f)]
        [OnValueChanged("OnDynamicSpacingChanged")]
        public float dynamicSpacing = 1f;

        [VerticalGroup("对齐/智能分布/DynamicSpacing/Left"), LabelWidth(120)]
        [LabelText("间距数值"), ShowIf("@enableDynamicSpacing"), PropertySpace(5)]
        [ReadOnly, ShowInInspector]
        private string CurrentSpacingText => $"{dynamicSpacing:F2} 单位";

        [HorizontalGroup("对齐/智能分布/DynamicSpacing")]
        [VerticalGroup("对齐/智能分布/DynamicSpacing/Right"), LabelWidth(100)]
        [Button("🔄 同步间距值", ButtonHeight = 30), ShowIf("@enableDynamicSpacing && IsSpacingDistribute()")]
        private void SyncSpacingValues() { dynamicSpacing = distributionSpacing; }

        [HorizontalGroup("对齐/智能分布/DynamicSpacing")]
        [VerticalGroup("对齐/智能分布/DynamicSpacing/Right"), LabelWidth(100)]
        [Button("🎯 智能间距", ButtonHeight = 30), ShowIf("@enableDynamicSpacing && IsSpacingDistribute()")]
        private void AutoCalculateSpacing() { dynamicSpacing = CalculateOptimalSpacing(); }

        [HorizontalGroup("对齐/智能分布/DynamicSpacing")]
        [VerticalGroup("对齐/智能分布/DynamicSpacing/Right"), LabelWidth(100)]
        [Button("🔄 重置", ButtonHeight = 30), ShowIf("@enableDynamicSpacing && IsSpacingDistribute()")]
        private void ResetSpacing() { dynamicSpacing = 0f; }
        [HorizontalGroup("对齐/智能分布/PreviewButtons")]
        [Button("👁 预览分布", ButtonHeight = 30), GUIColor(0.4f, 0.8f, 0.6f)]
        private void PreviewDistribution() { PreviewDistributeObjects(); }

        [HorizontalGroup("对齐/智能分布/PreviewButtons")]
        [Button("✓ 应用预览", ButtonHeight = 30), GUIColor(0.6f, 0.8f, 0.4f), ShowIf("@isPreviewing")]
        private void ApplyPreview() { ApplyDistributionPreview(); }

        [HorizontalGroup("对齐/智能分布/PreviewButtons")]
        [Button("✖ 清除预览", ButtonHeight = 30), GUIColor(0.8f, 0.4f, 0.4f), ShowIf("@isPreviewing")]
        private void ClearPreview() { ClearDistributionPreview(); }

        [TabGroup("对齐", "智能分布")]
        [HorizontalGroup("对齐/智能分布/DistributeButtons" )]
        [Button("↔ 水平均匀", ButtonHeight = 30), GUIColor(0.7f, 0.5f, 0.9f)]
        private void QuickDistributeH() { distributeMode = DistributeMode.HorizontalEven; DistributeObjects(); }

        [HorizontalGroup("对齐/智能分布/DistributeButtons")]
        [Button("↕ 垂直均匀", ButtonHeight = 30), GUIColor(0.7f, 0.5f, 0.9f)]
        private void QuickDistributeV() { distributeMode = DistributeMode.VerticalEven; DistributeObjects(); }

        [HorizontalGroup("对齐/智能分布/DistributeButtons")]
        [Button("⇿ 深度均匀", ButtonHeight = 30), GUIColor(0.7f, 0.5f, 0.9f)]
        private void QuickDistributeD() { distributeMode = DistributeMode.DepthEven; DistributeObjects(); }

        [TabGroup("对齐", "智能分布")]
        [HorizontalGroup("对齐/智能分布/SpacingButtons")]
        [Button("⟷ 水平间距", ButtonHeight = 30), GUIColor(0.9f, 0.6f, 0.4f)]
        private void QuickDistributeHS() { distributeMode = DistributeMode.HorizontalSpacing; DistributeObjects(); }

        [HorizontalGroup("对齐/智能分布/SpacingButtons")]
        [Button("⟺ 垂直间距", ButtonHeight = 30), GUIColor(0.9f, 0.6f, 0.4f)]
        private void QuickDistributeVS() { distributeMode = DistributeMode.VerticalSpacing; DistributeObjects(); }

        [HorizontalGroup("对齐/智能分布/SpacingButtons")]
        [Button("⇆ 深度间距", ButtonHeight = 30), GUIColor(0.9f, 0.6f, 0.4f)]
        private void QuickDistributeDS() { distributeMode = DistributeMode.DepthSpacing; DistributeObjects(); }

        [TabGroup("对齐", "智能分布")]
        [HorizontalGroup("对齐/智能分布/CameraButtons")]
        [Button("📷 镜头水平均匀", ButtonHeight = 30), GUIColor(0.4f, 0.7f, 0.9f)]
        private void QuickDistributeCH() { distributeMode = DistributeMode.CameraHorizontalEven; DistributeObjects(); }

        [HorizontalGroup("对齐/智能分布/CameraButtons")]
        [Button("📷 镜头垂直均匀", ButtonHeight = 30), GUIColor(0.4f, 0.7f, 0.9f)]
        private void QuickDistributeCV() { distributeMode = DistributeMode.CameraVerticalEven; DistributeObjects(); }

        [HorizontalGroup("对齐/智能分布/CameraButtons")]
        [Button("📷 镜头深度均匀", ButtonHeight = 30), GUIColor(0.4f, 0.7f, 0.9f)]
        private void QuickDistributeCD() { distributeMode = DistributeMode.CameraDepthEven; DistributeObjects(); }

        [TabGroup("对齐", "智能分布")]
        [HorizontalGroup("对齐/智能分布/CameraSpacingButtons")]
        [Button("📐 镜头水平间距", ButtonHeight = 30), GUIColor(0.6f, 0.8f, 0.7f)]
        private void QuickDistributeCHS() { distributeMode = DistributeMode.CameraHorizontalSpacing; DistributeObjects(); }

        [HorizontalGroup("对齐/智能分布/CameraSpacingButtons")]
        [Button("📐 镜头垂直间距", ButtonHeight = 30), GUIColor(0.6f, 0.8f, 0.7f)]
        private void QuickDistributeCVS() { distributeMode = DistributeMode.CameraVerticalSpacing; DistributeObjects(); }

        [HorizontalGroup("对齐/智能分布/CameraSpacingButtons")]
        [Button("📐 镜头深度间距", ButtonHeight = 30), GUIColor(0.6f, 0.8f, 0.7f)]
        private void QuickDistributeCDS() { distributeMode = DistributeMode.CameraDepthSpacing; DistributeObjects(); }
        #endregion

        #region 匹配设置
        [TabGroup("对齐", "尺寸匹配")]
        [InfoBox("尺寸匹配支持宽、高、深度、旋转和整体缩放；整体缩放优先级最高。", InfoMessageType.Info)]
        [PropertySpace(5)]

        [TabGroup("对齐", "尺寸匹配")]
        [LabelText("匹配参考对象"), LabelWidth(120)]
        [InfoBox("尺寸匹配必须有一个明确参考对象；不会使用“所有对象边界”或“世界中心”这类对齐参考。", InfoMessageType.None)]
        public MatchReferenceMode matchReference = MatchReferenceMode.FirstSelected;

        [TabGroup("对齐", "尺寸匹配")]
        [InfoBox("3D 对象通过 Scale 匹配，UI 对象优先调整 RectTransform 尺寸。", InfoMessageType.Info)]
        [HorizontalGroup("对齐/尺寸匹配/Options")]
        [VerticalGroup("对齐/尺寸匹配/Options/Left"), LabelWidth(120)]
        [LabelText("✓ 匹配宽度(X轴)"), PropertySpace(5)]
        public bool matchWidth = true;

        [VerticalGroup("对齐/尺寸匹配/Options/Left"), LabelWidth(120)]
        [LabelText("✓ 匹配高度(Y轴)"), PropertySpace(5)]
        public bool matchHeight = true;

        [VerticalGroup("对齐/尺寸匹配/Options/Left"), LabelWidth(120)]
        [LabelText("匹配深度(Z轴)"), PropertySpace(5)]
        [Tooltip("3D对象的纵深尺寸，UI对象的Z轴缩放")]
        public bool matchDepth = false;

        [VerticalGroup("对齐/尺寸匹配/Options/Right"), LabelWidth(120)]
        [LabelText("匹配旋转角度"), PropertySpace(5)]
        [InfoBox("复制参考对象的Rotation\n适用于对齐倾斜或旋转的对象")]
        public bool matchRotation = false;

        [VerticalGroup("对齐/尺寸匹配/Options/Right"), LabelWidth(120)]
        [LabelText("⚠ 匹配整体缩放"), PropertySpace(5)]
        [InfoBox("直接复制参考对象的Scale\n⚠ 会覆盖上面的单独尺寸匹配\n适用于完全克隆对象尺寸")]
        public bool matchScale = false;

        [TabGroup("对齐", "尺寸匹配")]
        [PropertySpace(10)]
        [Button("✓ 执行匹配", ButtonHeight = 34), GUIColor(0.28f, 0.52f, 0.85f)]
        private void MatchObjects() { ExecuteMatch(); }
        #endregion

        #region 布景整理
        [TabGroup("对齐", "布景整理")]
        [InfoBox("面向场景布置：落地/贴表面、网格归整、轻微随机错落。所有操作都有确认、Undo 和变更报告。", InfoMessageType.Info)]

        [TabGroup("对齐", "布景整理")]
        [HorizontalGroup("对齐/布景整理/Surface")]
        [VerticalGroup("对齐/布景整理/Surface/Left"), LabelWidth(120)]
        [LabelText("射线层"), PropertySpace(5)]
        public LayerMask surfaceLayerMask = ~0;

        [VerticalGroup("对齐/布景整理/Surface/Left"), LabelWidth(120)]
        [LabelText("上方起点"), MinValue(0.1f)]
        public float surfaceCastHeight = 50f;

        [VerticalGroup("对齐/布景整理/Surface/Left"), LabelWidth(120)]
        [LabelText("检测距离"), MinValue(0.1f)]
        public float surfaceCastDistance = 200f;

        [VerticalGroup("对齐/布景整理/Surface/Right"), LabelWidth(120)]
        [LabelText("表面偏移")]
        public float surfaceOffset = 0f;

        [VerticalGroup("对齐/布景整理/Surface/Right"), LabelWidth(120)]
        [LabelText("对齐法线")]
        public bool alignToSurfaceNormal = false;

        [VerticalGroup("对齐/布景整理/Surface/Right"), LabelWidth(120)]
        [LabelText("忽略自身碰撞")]
        public bool ignoreSelfColliders = true;

        [VerticalGroup("对齐/布景整理/Surface/Right"), LabelWidth(120)]
        [LabelText("忽略选区碰撞")]
        public bool ignoreSelectionColliders = true;

        [TabGroup("对齐", "布景整理")]
        [HorizontalGroup("对齐/布景整理/Grid")]
        [VerticalGroup("对齐/布景整理/Grid/Left"), LabelWidth(120)]
        [LabelText("网格尺寸")]
        public Vector3 gridSize = Vector3.one;

        [VerticalGroup("对齐/布景整理/Grid/Right"), LabelWidth(80)]
        [LabelText("吸附X")]
        public bool snapGridX = true;

        [VerticalGroup("对齐/布景整理/Grid/Right"), LabelWidth(80)]
        [LabelText("吸附Y")]
        public bool snapGridY = false;

        [VerticalGroup("对齐/布景整理/Grid/Right"), LabelWidth(80)]
        [LabelText("吸附Z")]
        public bool snapGridZ = true;

        [TabGroup("对齐", "布景整理")]
        [HorizontalGroup("对齐/布景整理/Random")]
        [VerticalGroup("对齐/布景整理/Random/Left"), LabelWidth(120)]
        [LabelText("随机种子")]
        public int randomSeed = 2026;

        [VerticalGroup("对齐/布景整理/Random/Left"), LabelWidth(120)]
        [LabelText("位置扰动")]
        public Vector3 randomPositionRange = new Vector3(0.25f, 0f, 0.25f);

        [VerticalGroup("对齐/布景整理/Random/Right"), LabelWidth(120)]
        [LabelText("Y旋转范围")]
        [MinMaxSlider(-180f, 180f, true)]
        public Vector2 randomYawRange = new Vector2(-8f, 8f);

        [VerticalGroup("对齐/布景整理/Random/Right"), LabelWidth(120)]
        [LabelText("统一缩放范围")]
        [MinMaxSlider(0.01f, 3f, true)]
        public Vector2 randomUniformScaleRange = new Vector2(1f, 1f);

        [TabGroup("对齐", "布景整理")]
        [HorizontalGroup("对齐/布景整理/Buttons")]
        [Button("吸附到表面", ButtonHeight = 32), GUIColor(0.35f, 0.65f, 0.42f)]
        private void SnapSelectionToSurface() { ExecuteSnapToSurface(); }

        [HorizontalGroup("对齐/布景整理/Buttons")]
        [Button("网格吸附", ButtonHeight = 32), GUIColor(0.35f, 0.55f, 0.78f)]
        private void SnapSelectionToGrid() { ExecuteGridSnap(); }

        [HorizontalGroup("对齐/布景整理/Buttons")]
        [Button("随机错落", ButtonHeight = 32), GUIColor(0.72f, 0.55f, 0.32f)]
        private void RandomizeSelectionDressing() { ExecuteRandomDressing(); }
        #endregion

        #region 高级选项
        [TabGroup("对齐", "高级选项")]
        [InfoBox("高级选项控制边界计算、对象过滤、确认弹窗和成功提示。", InfoMessageType.Info)]

        
        [TabGroup("对齐", "高级选项")]
        [LabelText("包含子对象"), PropertySpace(5)]
        [InfoBox("开启后计算子对象组合边界。")]
        public bool includeChildren = false;

        [TabGroup("对齐", "高级选项")]
        [LabelText("仅处理活跃对象"), PropertySpace(5)]
        [InfoBox("开启后跳过未激活对象。")]
        public bool activeOnly = true;

        [TabGroup("对齐", "高级选项")]
        [LabelText("忽略锁定对象"), PropertySpace(5)]
        [InfoBox("开启后跳过 HideFlags.NotEditable 对象。")]
        public bool ignoreLocked = true;

        [TabGroup("对齐", "高级选项")]
        [LabelText("跳过重复子级"), PropertySpace(5)]
        [InfoBox("开启后，如果父对象和子对象同时选中，只处理父对象，避免子对象被父级带动后又单独移动一次。")]
        public bool skipNestedSelection = true;

        [TabGroup("对齐", "高级选项")]
        [LabelText("保护Prefab资产"), PropertySpace(5)]
        [InfoBox("开启后跳过 Project 窗口里选中的 Prefab 资产，只处理场景实例和 Prefab Mode 中的对象。")]
        public bool protectPrefabAssets = true;

        [TabGroup("对齐", "高级选项")]
        [LabelText("对齐后选中"), PropertySpace(5)]
        [InfoBox("开启后操作完成仍保持目标选中。")]
        public bool selectAfterAlign = true;

        [TabGroup("对齐", "高级选项")]
        [LabelText("显示成功提示"), PropertySpace(5)]
        [InfoBox("关闭可减少连续操作时的弹窗。")]
        public bool showSuccessDialogs = false;

        [TabGroup("对齐", "高级选项")]
        [LabelText("执行前确认"), PropertySpace(5)]
        [InfoBox("开启后，对齐、分布、尺寸匹配会先显示实际处理对象预览。建议商业项目保持开启，避免误改大量对象。")]
        public bool confirmBeforeApply = true;

        [TabGroup("对齐", "高级选项")]
        [PropertySpace(10)]
        [InfoBox("等同于执行一次 Unity Undo。", InfoMessageType.Warning)]
        [Button("⟲ 撤销上次操作", ButtonHeight = 30)]
        private void UndoLastOperation() { Undo.PerformUndo(); }

        [TabGroup("对齐", "高级选项")]
        [PropertySpace(10)]
        [InfoBox("实时显示当前有效选中和首个对象尺寸。", InfoMessageType.None)]
        [InfoBox("当前选中: @GetSelectionInfo()", InfoMessageType.None)]
        #endregion

        #region 选区审计
        [TabGroup("对齐", "选区审计")]
        [InfoBox("执行前先看有效对象、边界来源、Prefab 状态和 UI 布局风险。这里不修改场景。", InfoMessageType.Info)]
        [HorizontalGroup("对齐/选区审计/Toolbar")]
        [Button("刷新选区审计", ButtonHeight = 30), GUIColor(0.28f, 0.52f, 0.85f)]
        private void RefreshSelectionAudit()
        {
            RebuildAuditRecords(GetValidSelection());
        }

        [HorizontalGroup("对齐/选区审计/Toolbar")]
        [Button("清空审计", ButtonHeight = 30)]
        private void ClearSelectionAudit()
        {
            auditRecords.Clear();
        }

        [TabGroup("对齐", "选区审计")]
        [HorizontalGroup("对齐/选区审计/Filters")]
        [LabelText("搜索"), LabelWidth(40)]
        public string AuditSearch
        {
            get => auditSearch;
            set => auditSearch = value ?? "";
        }

        [HorizontalGroup("对齐/选区审计/Filters")]
        [LabelText("只看风险"), LabelWidth(70)]
        public bool ShowOnlyRisks
        {
            get => showOnlyRisks;
            set => showOnlyRisks = value;
        }

        [TabGroup("对齐", "选区审计")]
        [ShowInInspector, ReadOnly, DisplayAsString, HideLabel]
        private string AuditSummary => BuildAuditSummary();

        [TabGroup("对齐", "选区审计")]
        [ShowInInspector, ReadOnly, TableList(IsReadOnly = true, AlwaysExpanded = true), LabelText("当前审计清单")]
        private List<TransformAuditRecord> FilteredAuditRecords => GetFilteredAuditRecords();
        #endregion

        private bool ConfirmTransformOperation(string title, string action, GameObject[] selectedObjects, int affectedCountOffset = 0)
        {
            if (selectedObjects == null)
                selectedObjects = Array.Empty<GameObject>();

            int affectedCount = Mathf.Max(0, selectedObjects.Length - affectedCountOffset);
            bool forceConfirm = affectedCount >= LargeTransformOperationThreshold;
            if (!confirmBeforeApply && !forceConfirm)
                return true;

            string preview = SimpleToolsSafetyUtility.JoinPreview(selectedObjects.Select(obj => obj != null ? obj.name : "<丢失对象>"), 10);
            string riskSummary = BuildRiskSummary(selectedObjects);
            string impactSummary = BuildTransformImpactSummary(action);
            string forceHint = forceConfirm && !confirmBeforeApply
                ? "\n\n当前目标数量较大，即使关闭了执行前确认，也会强制确认一次。"
                : string.Empty;
            return EditorUtility.DisplayDialog(title,
                $"将{action} {affectedCount} 个对象。\n\n会修改：{impactSummary}\n\n实际选区：\n{preview}\n\n{riskSummary}{forceHint}\n\n支持 Ctrl+Z 撤销。继续吗？",
                "开始处理", "取消");
        }

        private string BuildTransformImpactSummary(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return "Transform/RectTransform";

            if (action.Contains("尺寸") || action.Contains("匹配"))
                return "Transform.localScale；UI 对象可能修改 RectTransform 尺寸";

            if (action.Contains("随机"))
                return "Transform.position / rotation / localScale 中已启用的随机项";

            return "Transform.position；UI 对象按 RectTransform/边界计算后移动";
        }

        private void MarkScenesDirty(IEnumerable<GameObject> selectedObjects)
        {
            if (selectedObjects == null)
                return;

            foreach (var scene in selectedObjects
                .Where(obj => obj != null && obj.scene.IsValid())
                .Select(obj => obj.scene)
                .Distinct())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private void RebuildAuditRecords(GameObject[] selectedObjects)
        {
            auditRecords.Clear();
            if (selectedObjects == null)
                return;

            foreach (var obj in selectedObjects)
            {
                if (obj == null)
                    continue;

                var bounds = GetObjectBounds(obj.transform);
                var notes = new List<string>();
                string risk = "低";

                if (HasUILayoutRisk(obj.transform, out var layoutNote))
                {
                    risk = "高";
                    notes.Add(layoutNote);
                }

                if (PrefabUtility.IsPartOfPrefabInstance(obj))
                {
                    if (risk != "高") risk = "中";
                    notes.Add("会写入 Prefab 实例 Override");
                }

                if (obj.transform is RectTransform && boundsMode != BoundsCalculationMode.RectTransform && boundsMode != BoundsCalculationMode.Auto)
                {
                    if (risk != "高") risk = "中";
                    notes.Add("UI 对象未使用 RectTransform 边界模式");
                }

                if (bounds.size.sqrMagnitude <= ProjectionEpsilon)
                {
                    if (risk != "高") risk = "中";
                    notes.Add("边界接近零，可能只按 Transform 点处理");
                }

                auditRecords.Add(new TransformAuditRecord
                {
                    Object = obj,
                    Path = GetHierarchyPath(obj.transform),
                    Kind = GetObjectKind(obj.transform),
                    BoundsSource = GetBoundsSourceName(obj.transform),
                    Size = bounds.size.ToString("F2"),
                    PrefabState = PrefabUtility.IsPartOfPrefabInstance(obj) ? "实例" : "场景",
                    RiskLevel = risk,
                    Note = notes.Count == 0 ? "可处理" : string.Join("；", notes)
                });
            }

            lastResultSummary = $"选区审计完成: {auditRecords.Count} 个有效对象 | 风险 {auditRecords.Count(item => item.RiskLevel != "低")} 项";
            lastResultDetail = BuildAuditSummary();
        }

        private List<TransformAuditRecord> GetFilteredAuditRecords()
        {
            IEnumerable<TransformAuditRecord> query = auditRecords;
            if (showOnlyRisks)
                query = query.Where(item => item.RiskLevel != "低");

            if (!string.IsNullOrWhiteSpace(auditSearch))
            {
                string keyword = auditSearch.Trim();
                query = query.Where(item =>
                    ContainsIgnoreCase(item.Object != null ? item.Object.name : "", keyword) ||
                    ContainsIgnoreCase(item.Path, keyword) ||
                    ContainsIgnoreCase(item.Kind, keyword) ||
                    ContainsIgnoreCase(item.BoundsSource, keyword) ||
                    ContainsIgnoreCase(item.Note, keyword));
            }

            return query.ToList();
        }

        private string BuildAuditSummary()
        {
            if (auditRecords.Count == 0)
                return "尚未生成审计。点击“刷新选区审计”查看当前有效选区。";

            int prefabCount = auditRecords.Count(item => item.PrefabState == "实例");
            int highRisk = auditRecords.Count(item => item.RiskLevel == "高");
            int mediumRisk = auditRecords.Count(item => item.RiskLevel == "中");
            int uiCount = auditRecords.Count(item => item.Kind.Contains("UI"));
            return $"对象 {auditRecords.Count} | Prefab实例 {prefabCount} | UI {uiCount} | 高风险 {highRisk} | 中风险 {mediumRisk} | 搜索后 {GetFilteredAuditRecords().Count}";
        }

        private string BuildRiskSummary(GameObject[] selectedObjects)
        {
            if (selectedObjects == null || selectedObjects.Length == 0)
                return "风险: 无有效对象。";

            int prefabCount = 0;
            int layoutRisk = 0;
            int zeroBounds = 0;

            foreach (var obj in selectedObjects)
            {
                if (obj == null) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(obj)) prefabCount++;
                if (HasUILayoutRisk(obj.transform, out _)) layoutRisk++;
                if (GetObjectBounds(obj.transform).size.sqrMagnitude <= ProjectionEpsilon) zeroBounds++;
            }

            var parts = new List<string>();
            if (prefabCount > 0) parts.Add($"Prefab实例 {prefabCount} 个会记录 Override");
            if (layoutRisk > 0) parts.Add($"UI布局驱动风险 {layoutRisk} 个");
            if (zeroBounds > 0) parts.Add($"零尺寸边界 {zeroBounds} 个");
            return parts.Count == 0 ? "风险: 未发现明显风险。" : "风险: " + string.Join("；", parts);
        }

        private bool ContainsIgnoreCase(string source, string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
                return true;
            return (source ?? "").IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "<丢失>";

            var names = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", names);
        }

        private string GetObjectKind(Transform transform)
        {
            if (transform is RectTransform) return "UI";
            if (transform.GetComponent<Renderer>() != null || transform.GetComponentInChildren<Renderer>() != null) return "Renderer";
            if (transform.GetComponent<Collider>() != null || transform.GetComponentInChildren<Collider>() != null) return "Collider3D";
            if (transform.GetComponent<Collider2D>() != null || transform.GetComponentInChildren<Collider2D>() != null) return "Collider2D";
            return "Transform";
        }

        private string GetBoundsSourceName(Transform transform)
        {
            if (transform == null) return "无";
            if (boundsMode == BoundsCalculationMode.TransformOnly) return "Transform";
            if (boundsMode == BoundsCalculationMode.RectTransform || (boundsMode == BoundsCalculationMode.Auto && transform is RectTransform)) return "RectTransform";
            if (boundsMode == BoundsCalculationMode.Renderer || boundsMode == BoundsCalculationMode.Auto)
            {
                bool hasRenderer = includeChildren
                    ? transform.GetComponentsInChildren<Renderer>().Any(renderer => renderer != null && (!activeOnly || renderer.gameObject.activeInHierarchy))
                    : transform.GetComponent<Renderer>() != null;
                if (hasRenderer) return "Renderer";
            }
            if (boundsMode == BoundsCalculationMode.Collider || boundsMode == BoundsCalculationMode.Auto)
            {
                bool hasCollider = includeChildren
                    ? transform.GetComponentsInChildren<Collider>().Any(collider => collider != null && (!activeOnly || collider.gameObject.activeInHierarchy)) ||
                      transform.GetComponentsInChildren<Collider2D>().Any(collider => collider != null && (!activeOnly || collider.gameObject.activeInHierarchy))
                    : transform.GetComponent<Collider>() != null || transform.GetComponent<Collider2D>() != null;
                if (hasCollider) return "Collider";
            }
            return "Transform";
        }

        private bool HasUILayoutRisk(Transform transform, out string note)
        {
            note = "";
            var rect = transform as RectTransform;
            if (rect == null)
                return false;

            if (rect.GetComponent<ContentSizeFitter>() != null || rect.GetComponent<AspectRatioFitter>() != null)
            {
                note = "自身带 UI 尺寸驱动组件";
                return true;
            }

            var parent = rect.parent;
            while (parent != null)
            {
                if (parent.GetComponent<LayoutGroup>() != null)
                {
                    note = $"受父级布局组件驱动: {parent.name}";
                    return true;
                }
                parent = parent.parent;
            }

            return false;
        }

        private void FinalizeTransformChanges(IEnumerable<GameObject> selectedObjects)
        {
            if (selectedObjects == null)
                return;

            foreach (var obj in selectedObjects)
            {
                if (obj == null)
                    continue;

                var transform = obj.transform;
                EditorUtility.SetDirty(transform);

                if (PrefabUtility.IsPartOfPrefabInstance(transform))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(transform);

                if (transform is RectTransform rectTransform && PrefabUtility.IsPartOfPrefabInstance(rectTransform))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rectTransform);
            }

            MarkScenesDirty(selectedObjects);
        }

        #region 核心对齐功能
        [InfoBox("按当前设置移动选中对象；写入前会记录 Undo。", InfoMessageType.Info)]
        
        [Button("▶ 执行对齐", ButtonHeight = 34), GUIColor(0.28f, 0.52f, 0.85f)]
        [PropertyOrder(-1)]
        public void AlignObjects()
        {
            var selectedObjects = GetValidSelection();
            if (selectedObjects.Length < 2)
            {
                string message = selectedObjects.Length == 0 ? 
                    "❌ 未选中任何对象\n\n请在场景或层级视图中选择至少2个GameObject" :
                    "❌ 选中对象不足\n\n对齐功能需要至少2个对象\n当前有效选中：1个对象\n\n💡 提示：请检查对象是否被锁定或未激活";
                EditorUtility.DisplayDialog("对齐功能提示", message, "确定");
                return;
            }

            if (!ValidateCoordinateMode(selectedObjects, IsCameraAlignMode(alignMode)))
                return;

            if (!ValidateAlignReference(selectedObjects))
                return;

            if (!ConfirmTransformOperation("确认执行对齐", "对齐", selectedObjects))
                return;

            Undo.RecordObjects(selectedObjects.Select(obj => obj.transform).ToArray(), "Align Objects");
            var beforeSnapshots = CaptureTransformSnapshots(selectedObjects);

            var transforms = selectedObjects.Select(obj => obj.transform).ToArray();
            var referenceBounds = GetReferenceBounds(transforms);
            Vector3 axis = GetAlignAxis(alignMode, transforms);
            axis.Normalize();
            GetBoundsProjection(referenceBounds, axis, out float refMin, out float refMax);
            float refCenter = (refMin + refMax) * 0.5f;

            foreach (var transform in transforms)
            {
                var objectBounds = GetObjectBounds(transform);
                GetBoundsProjection(objectBounds, axis, out float objMin, out float objMax);
                float objCenter = (objMin + objMax) * 0.5f;
                float moveDistance = 0f;

                switch (alignMode)
                {
                    case AlignMode.Left:
                    case AlignMode.Bottom:
                    case AlignMode.Front:
                    case AlignMode.CameraLeft:
                    case AlignMode.CameraBottom:
                    case AlignMode.CameraFront:
                        moveDistance = refMin - objMin;
                        break;
                    case AlignMode.Right:
                    case AlignMode.Top:
                    case AlignMode.Back:
                    case AlignMode.CameraRight:
                    case AlignMode.CameraTop:
                    case AlignMode.CameraBack:
                        moveDistance = refMax - objMax;
                        break;
                    case AlignMode.HorizontalCenter:
                    case AlignMode.VerticalCenter:
                    case AlignMode.DepthCenter:
                    case AlignMode.CameraHorizontalCenter:
                    case AlignMode.CameraVerticalCenter:
                    case AlignMode.CameraDepthCenter:
                        moveDistance = refCenter - objCenter;
                        break;
                }

                if (Mathf.Abs(moveDistance) > ProjectionEpsilon)
                    transform.position += axis * moveDistance;
            }

            FinalizeTransformChanges(selectedObjects);
            lastResultSummary = $"对齐完成: {selectedObjects.Length} 个对象 | 模式 {alignMode} | 参考 {alignReference}";
            lastResultDetail = BuildTransformChangeReport(selectedObjects, beforeSnapshots);

            if (showSuccessDialogs)
                EditorUtility.DisplayDialog("成功", $"✓ 成功对齐 {selectedObjects.Length} 个对象！", "确定");
            
            if (selectAfterAlign)
                Selection.objects = selectedObjects;
        }
        #endregion

        #region 分布功能
        [InfoBox("分布至少需要 2 个有效对象，3 个以上效果更直观。", InfoMessageType.Info)]
        [PropertySpace(5),HideLabel,ReadOnly]
        public string NULLSTR="";
        
        public void DistributeObjects()
        {
            var selectedObjects = GetValidSelection();
            if (selectedObjects.Length < 2)
            {
                string message = selectedObjects.Length == 0 ?
                    "❌ 未选中任何对象\n\n请在场景或层级视图中选择至少2个GameObject" :
                    "❌ 选中对象不足\n\n分布功能需要至少2个对象\n当前有效选中：1个对象\n\n💡 提示：\n• 检查对象是否被锁定或未激活\n• 建议选择3个或以上对象以获得更好的分布效果";
                EditorUtility.DisplayDialog("分布功能提示", message, "确定");
                return;
            }

            if (!ValidateCoordinateMode(selectedObjects, IsCameraDistributeMode(distributeMode)))
                return;

            if (previewMode)
            {
                PreviewDistributeObjects();
                return;
            }

            if (!ConfirmTransformOperation("确认执行分布", "分布", selectedObjects))
                return;

            // 如果在预览模式，先清除预览
            if (isPreviewing)
                ClearDistributionPreview();

            Undo.RecordObjects(selectedObjects.Select(obj => obj.transform).ToArray(), "Distribute Objects");
            var beforeSnapshots = CaptureTransformSnapshots(selectedObjects);

            var transforms = selectedObjects.Select(obj => obj.transform).ToList();
            
            // 根据位置排序
            SortTransformsByDistributionMode(transforms);
            
            if (reverseOrder)
                transforms.Reverse();

            switch (distributeMode)
            {
                case DistributeMode.HorizontalEven:
                case DistributeMode.VerticalEven:
                case DistributeMode.DepthEven:
                case DistributeMode.CameraHorizontalEven:
                case DistributeMode.CameraVerticalEven:
                case DistributeMode.CameraDepthEven:
                    DistributeEvenly(transforms);
                    break;
                case DistributeMode.HorizontalSpacing:
                case DistributeMode.VerticalSpacing:
                case DistributeMode.DepthSpacing:
                case DistributeMode.CameraHorizontalSpacing:
                case DistributeMode.CameraVerticalSpacing:
                case DistributeMode.CameraDepthSpacing:
                    DistributeWithSpacing(transforms);
                    break;
            }

            FinalizeTransformChanges(selectedObjects);
            lastResultSummary = $"分布完成: {selectedObjects.Length} 个对象 | 模式 {distributeMode} | 反向 {(reverseOrder ? "是" : "否")}";
            lastResultDetail = BuildTransformChangeReport(selectedObjects, beforeSnapshots);

            if (showSuccessDialogs)
                EditorUtility.DisplayDialog("成功", $"✓ 成功分布 {selectedObjects.Length} 个对象！", "确定");
            
            if (selectAfterAlign)
                Selection.objects = selectedObjects;
        }

        private void DistributeEvenly(List<Transform> transforms)
        {
            if (transforms.Count < 2) return;

            // 计算所有对象的总边界
            Bounds totalBounds = GetObjectBounds(transforms[0]);
            foreach (var transform in transforms)
            {
                totalBounds.Encapsulate(GetObjectBounds(transform));
            }

            Vector3 axis = GetDistributeAxis(distributeMode, transforms);
            axis.Normalize();
            GetBoundsProjection(totalBounds, axis, out float startPos, out float endPos);

            float totalDistance = endPos - startPos;
            if (totalDistance <= 0 || transforms.Count <= 1) return;

            // 计算所有对象在轴上的投影尺寸
            List<float> objectSizesOnAxis = new List<float>();
            float totalObjectSize = 0f;
            
            foreach (var t in transforms)
            {
                var bounds = GetObjectBounds(t);
                GetBoundsProjection(bounds, axis, out float minProj, out float maxProj);
                float size = maxProj - minProj;
                objectSizesOnAxis.Add(size);
                totalObjectSize += size;
            }

            // 可用空间 = 总距离 - 所有对象尺寸
            float availableSpace = totalDistance - totalObjectSize;
            if (availableSpace < 0) availableSpace = 0;
            
            float spacing = (transforms.Count > 1) ? availableSpace / (transforms.Count - 1) : 0;

            // 在目标范围内分布对象
            float currentPos = startPos;
            for (int i = 0; i < transforms.Count; i++)
            {
                var transform = transforms[i];
                var bounds = GetObjectBounds(transform);
                float halfSize = objectSizesOnAxis[i] * 0.5f;
                float targetCenterProj = currentPos + halfSize;
                GetBoundsProjection(bounds, axis, out float objMin, out float objMax);
                float currentCenterProj = (objMin + objMax) * 0.5f;
                float moveDistance = targetCenterProj - currentCenterProj;
                if (Mathf.Abs(moveDistance) > ProjectionEpsilon)
                    transform.position += axis * moveDistance;

                currentPos += objectSizesOnAxis[i] + spacing;
            }
        }

        private void DistributeWithSpacing(List<Transform> transforms)
        {
            if (transforms.Count < 2) return;

            // 计算所有对象的总边界
            Bounds totalBounds = GetObjectBounds(transforms[0]);
            foreach (var transform in transforms)
            {
                totalBounds.Encapsulate(GetObjectBounds(transform));
            }

            Vector3 axis = GetDistributeAxis(distributeMode, transforms);
            axis.Normalize();
            GetBoundsProjection(totalBounds, axis, out float startPos, out _);

            // 计算每个对象在指定轴上的投影尺寸
            List<float> objectSizes = new List<float>();

            foreach (var transform in transforms)
            {
                var bounds = GetObjectBounds(transform);
                GetBoundsProjection(bounds, axis, out float minProj, out float maxProj);
                float size = maxProj - minProj;
                objectSizes.Add(size);
            }

            // 计算间距：直接使用用户设定的间距值
            float spacing = distributionSpacing;

            float currentPos = startPos;

            for (int i = 0; i < transforms.Count; i++)
            {
                var transform = transforms[i];
                var bounds = GetObjectBounds(transform);
                GetBoundsProjection(bounds, axis, out float objMin, out float objMax);
                float targetCenterProj = currentPos + objectSizes[i] * 0.5f;
                float currentCenterProj = (objMin + objMax) * 0.5f;
                float moveDistance = targetCenterProj - currentCenterProj;
                if (Mathf.Abs(moveDistance) > ProjectionEpsilon)
                    transform.position += axis * moveDistance;

                currentPos += objectSizes[i] + spacing;
            }
        }

        private void SortTransformsByDistributionMode(List<Transform> transforms)
        {
            if (!maintainOrder) return;

            Vector3 axis = GetDistributeAxis(distributeMode, transforms);
            axis.Normalize();
            transforms.Sort((a, b) =>
                Vector3.Dot(GetObjectBounds(a).center, axis).CompareTo(Vector3.Dot(GetObjectBounds(b).center, axis)));
        }
        #endregion

        #region 动态间距调整
        [InfoBox("🎚️ 动态间距调整：实时控制对象间距\n" +
                "• 实时预览：拖动滑条即时看到效果\n" +
                "• 拖动会基于预览前位置重算，不会叠加漂移\n" +
                "• 仅间距模式：仅在间距分布模式下工作\n" +
                "• 满意后应用预览，不满意可一键清除", InfoMessageType.Info)]
        [PropertySpace(5)]
        
        private void OnDistributeModeChanged()
        {
            // 当切换到间距模式时，设置智能默认值
            if (IsSpacingDistribute() && enableDynamicSpacing)
            {
                // 如果当前distributionSpacing是默认值（10），则使用智能计算的值
                if (Mathf.Approximately(distributionSpacing, 10f))
                {
                    dynamicSpacing = CalculateOptimalSpacing();
                    distributionSpacing = dynamicSpacing;
                }
                else
                {
                    dynamicSpacing = distributionSpacing;
                }
            }
        }
        
        private void OnDynamicSpacingChanged()
        {
            if (!enableDynamicSpacing) return;
            
            // 只有在间距分布模式下才工作
            if (!IsSpacingDistribute()) return;
            
            var selectedObjects = GetValidSelection();
            if (selectedObjects.Length < 2) return;

            if (!ValidateCoordinateMode(selectedObjects, IsCameraDistributeMode(distributeMode)))
                return;

            if (isPreviewing && !IsSamePreviewSelection(selectedObjects))
                ClearDistributionPreview();

            if (!isPreviewing)
            {
                originalPositions.Clear();
                foreach (var obj in selectedObjects)
                    originalPositions[obj.transform] = obj.transform.position;

                Undo.SetCurrentGroupName("Dynamic Spacing Preview");
                previewUndoGroup = Undo.GetCurrentGroup();
                Undo.RecordObjects(selectedObjects.Select(obj => obj.transform).ToArray(), "Dynamic Spacing Preview");
                isPreviewing = true;
            }
            else
            {
                RestorePreviewPositions(false);
            }

            // 更新distributionSpacing为当前滑条值
            distributionSpacing = dynamicSpacing;

            // 执行实时分布
            var transforms = selectedObjects.Select(obj => obj.transform).ToList();
            SortTransformsByDistributionMode(transforms);
            
            if (reverseOrder)
                transforms.Reverse();

            try
            {
                DistributeWithSpacing(transforms);
                lastResultSummary = $"动态间距预览: {selectedObjects.Length} 个对象 | 间距 {distributionSpacing:F2}";
                lastResultDetail = "拖动只刷新预览；满意后点击“应用预览”，否则点击“清除预览”。\n" + BuildTransformResultDetail(selectedObjects);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"动态间距调整出错: {e.Message}");
                RestorePreviewPositions(false);
            }
        }

        private float CalculateOptimalSpacing()
        {
            var selectedObjects = GetValidSelection();
            if (selectedObjects.Length < 2) return 1f;

            // 计算所有对象在当前分布轴上的平均尺寸
            float totalSize = 0f;
            int axisCount = 0;
            Vector3 axis = GetDistributeAxis(distributeMode, selectedObjects.Select(obj => obj.transform).ToArray());
            axis.Normalize();

            foreach (var obj in selectedObjects)
            {
                var bounds = GetObjectBounds(obj.transform);
                GetBoundsProjection(bounds, axis, out float minProj, out float maxProj);
                totalSize += maxProj - minProj;
                axisCount++;
            }

            if (axisCount == 0) return 1f;

            float avgSize = totalSize / axisCount;
            // 返回平均尺寸的20%作为默认间距
            return Mathf.Max(0.1f, avgSize * 0.2f);
        }
        #endregion

        #region 预览功能
        [InfoBox("预览会临时移动 Transform，可应用或一键还原。", InfoMessageType.Info)]
        [PropertySpace(5)]
        
        private void PreviewDistributeObjects()
        {
            var selectedObjects = GetValidSelection();
            if (selectedObjects.Length < 2)
            {
                EditorUtility.DisplayDialog("提示", "预览功能需要至少2个对象！", "确定");
                return;
            }

            if (!ValidateCoordinateMode(selectedObjects, IsCameraDistributeMode(distributeMode)))
                return;

            // 如果已经在预览，先清除
            if (isPreviewing)
                ClearDistributionPreview();

            // 保存原始位置
            originalPositions.Clear();
            foreach (var obj in selectedObjects)
            {
                originalPositions[obj.transform] = obj.transform.position;
            }

            // 执行预览分布
            var transforms = selectedObjects.Select(obj => obj.transform).ToList();
            Undo.SetCurrentGroupName("Preview Distribution");
            previewUndoGroup = Undo.GetCurrentGroup();
            Undo.RecordObjects(transforms.Cast<UnityEngine.Object>().ToArray(), "Preview Distribution");

            SortTransformsByDistributionMode(transforms);
            
            if (reverseOrder)
                transforms.Reverse();

            switch (distributeMode)
            {
                case DistributeMode.HorizontalEven:
                case DistributeMode.VerticalEven:
                case DistributeMode.DepthEven:
                case DistributeMode.CameraHorizontalEven:
                case DistributeMode.CameraVerticalEven:
                case DistributeMode.CameraDepthEven:
                    DistributeEvenly(transforms);
                    break;
                case DistributeMode.HorizontalSpacing:
                case DistributeMode.VerticalSpacing:
                case DistributeMode.DepthSpacing:
                case DistributeMode.CameraHorizontalSpacing:
                case DistributeMode.CameraVerticalSpacing:
                case DistributeMode.CameraDepthSpacing:
                    DistributeWithSpacing(transforms);
                    break;
            }

            isPreviewing = true;
            lastResultSummary = $"分布预览已生成: {selectedObjects.Length} 个对象 | 模式 {distributeMode}";
            lastResultDetail = BuildTransformResultDetail(selectedObjects);
            EditorUtility.DisplayDialog("预览已生成", "对象已临时移动用于预览。\n点击“应用预览”保留效果，点击“清除预览”还原位置。", "知道了");
        }

        private void ClearDistributionPreview()
        {
            if (!isPreviewing) return;

            // 恢复原始位置
            Undo.SetCurrentGroupName("Clear Distribution Preview");
            RestorePreviewPositions(true);

            FinalizeTransformChanges(originalPositions.Keys
                .Where(transform => transform != null)
                .Select(transform => transform.gameObject));

            originalPositions.Clear();
            isPreviewing = false;
            previewUndoGroup = -1;
            lastResultSummary = "分布预览已清除";
            lastResultDetail = "已还原预览前记录的位置。";
        }

        private void ApplyDistributionPreview()
        {
            if (!isPreviewing) return;

            var selectedObjects = originalPositions.Keys
                .Where(transform => transform != null)
                .Select(transform => transform.gameObject)
                .ToArray();
            if (previewUndoGroup >= 0)
                Undo.CollapseUndoOperations(previewUndoGroup);

            var beforeSnapshots = originalPositions
                .Where(kvp => kvp.Key != null)
                .ToDictionary(kvp => kvp.Key, kvp => new TransformChangeSnapshot
                {
                    Position = kvp.Value,
                    LocalPosition = kvp.Key.localPosition,
                    LocalScale = kvp.Key.localScale,
                    Rotation = kvp.Key.rotation,
                    RectSize = kvp.Key is RectTransform rect ? rect.rect.size : Vector2.zero,
                    IsRectTransform = kvp.Key is RectTransform
                });

            originalPositions.Clear();
            isPreviewing = false;
            previewUndoGroup = -1;
            FinalizeTransformChanges(selectedObjects);
            lastResultSummary = $"分布预览已应用: {selectedObjects.Length} 个对象 | 模式 {distributeMode}";
            lastResultDetail = BuildTransformChangeReport(selectedObjects, beforeSnapshots);

            if (showSuccessDialogs)
                EditorUtility.DisplayDialog("成功", "✓ 预览效果已应用！", "确定");
            
            if (selectAfterAlign)
                Selection.objects = selectedObjects;
        }
        #endregion

        #region 匹配功能
        [InfoBox("匹配至少需要 2 个有效对象：1 个参考对象和 1 个以上目标。", InfoMessageType.Info)]
        [PropertySpace(5)]
        
        private void ExecuteMatch()
        {
            var selectedObjects = GetValidSelection();
            if (selectedObjects.Length < 2)
            {
                string message = selectedObjects.Length == 0 ?
                    "❌ 未选中任何对象\n\n请在场景或层级视图中选择至少2个GameObject" :
                    "❌ 选中对象不足\n\n匹配功能需要至少2个对象：\n• 1个参考对象（提供目标尺寸）\n• 1个或多个目标对象（被调整尺寸）\n\n当前有效选中：1个对象";
                EditorUtility.DisplayDialog("尺寸匹配提示", message, "确定");
                return;
            }
            
            // 检查是否选择了任何匹配选项
            if (!matchWidth && !matchHeight && !matchDepth && !matchRotation && !matchScale)
            {
                EditorUtility.DisplayDialog("尺寸匹配提示", 
                    "❌ 未选择任何匹配选项\n\n请至少勾选一个匹配选项：\n" +
                    "• 匹配宽度(X轴)\n" +
                    "• 匹配高度(Y轴)\n" +
                    "• 匹配深度(Z轴)\n" +
                    "• 匹配旋转角度\n" +
                    "• 匹配整体缩放", "确定");
                return;
            }

            GameObject referenceObject = GetReferenceObject(selectedObjects);
            if (referenceObject == null)
            {
                EditorUtility.DisplayDialog("尺寸匹配错误", "❌ 无法获取参考对象\n\n请检查选择设置和对象状态", "确定");
                return;
            }

            var targetObjects = selectedObjects
                .Where(obj => obj != null && obj != referenceObject)
                .ToArray();

            if (targetObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("尺寸匹配提示",
                    "❌ 没有可处理的目标对象\n\n当前参考对象占用了全部有效选区，请再选择至少一个目标对象。",
                    "确定");
                return;
            }

            if (!ConfirmTransformOperation("确认执行尺寸匹配", $"匹配到参考对象 {referenceObject.name}", targetObjects))
                return;

            Undo.RecordObjects(targetObjects.Select(obj => obj.transform).ToArray(), "Match Objects");
            var beforeSnapshots = CaptureTransformSnapshots(targetObjects);

            var referenceBounds = GetObjectBounds(referenceObject.transform);
            var referenceTransform = referenceObject.transform;

            foreach (var obj in targetObjects)
            {
                var transform = obj.transform;
                var rectTransform = transform as RectTransform;
                var referenceRect = referenceTransform as RectTransform;

                // 匹配Scale（优先级最高，会覆盖尺寸匹配）
                if (matchScale)
                {
                    transform.localScale = referenceTransform.localScale;
                }
                // 匹配RectTransform尺寸 (UI)
                else if (rectTransform != null && referenceRect != null)
                {
                    // UI对象的尺寸匹配
                    if (matchWidth) 
                    {
                        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, referenceRect.rect.width);
                    }
                    if (matchHeight) 
                    {
                        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, referenceRect.rect.height);
                    }
                    // UI对象也可以有深度（Z轴缩放）
                    if (matchDepth)
                    {
                        var scale = rectTransform.localScale;
                        scale.z = referenceRect.localScale.z;
                        rectTransform.localScale = scale;
                    }
                }
                // 匹配普通Transform尺寸
                else if (matchWidth || matchHeight || matchDepth)
                {
                    var objectBounds = GetObjectBounds(transform);
                    var scale = transform.localScale;

                    // 处理边界为零的情况（使用Scale直接匹配）
                    bool useDirectScale = objectBounds.size.magnitude < 0.001f;

                    if (matchWidth)
                    {
                        if (useDirectScale || objectBounds.size.x == 0)
                            scale.x = referenceTransform.localScale.x;
                        else
                            scale.x *= referenceBounds.size.x / objectBounds.size.x;
                    }
                    if (matchHeight)
                    {
                        if (useDirectScale || objectBounds.size.y == 0)
                            scale.y = referenceTransform.localScale.y;
                        else
                            scale.y *= referenceBounds.size.y / objectBounds.size.y;
                    }
                    if (matchDepth)
                    {
                        if (useDirectScale || objectBounds.size.z == 0)
                            scale.z = referenceTransform.localScale.z;
                        else
                            scale.z *= referenceBounds.size.z / objectBounds.size.z;
                    }

                    transform.localScale = scale;
                }

                // 匹配旋转
                if (matchRotation)
                {
                    transform.rotation = referenceTransform.rotation;
                }
            }

            FinalizeTransformChanges(targetObjects);
            lastResultSummary = $"尺寸匹配完成: {targetObjects.Length} 个目标 | 参考 {referenceObject.name}";
            lastResultDetail = $"参考对象: {referenceObject.name}\n" + BuildTransformChangeReport(targetObjects, beforeSnapshots);

            if (showSuccessDialogs)
                EditorUtility.DisplayDialog("成功", $"✓ 成功匹配 {targetObjects.Length} 个对象到参考对象！", "确定");
            
            if (selectAfterAlign)
                Selection.objects = selectedObjects;
        }
        #endregion

        #region 布景整理执行
        private void ExecuteSnapToSurface()
        {
            var selectedObjects = GetValidSelection();
            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("表面吸附提示", "请先选择至少一个要吸附的对象。", "确定");
                return;
            }

            if (!ValidateSurfaceSettings())
                return;

            var hittableObjects = selectedObjects
                .Where(obj => obj != null && TryFindSurfaceHit(obj, selectedObjects, out _))
                .ToArray();

            if (hittableObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("表面吸附提示",
                    "当前选区没有对象能命中表面。\n\n请检查射线层、检测距离，或确认目标表面带有 3D Collider。\n当前表面吸附使用 Physics.RaycastAll，不处理 Collider2D。",
                    "确定");
                return;
            }

            if (!ConfirmTransformOperation("确认吸附到表面", "吸附到表面", hittableObjects))
                return;

            Undo.RecordObjects(hittableObjects.Select(obj => obj.transform).ToArray(), "Snap Selection To Surface");
            var beforeSnapshots = CaptureTransformSnapshots(hittableObjects);

            var failed = new List<string>();
            foreach (var obj in hittableObjects)
            {
                if (!TryFindSurfaceHit(obj, selectedObjects, out var hit))
                {
                    failed.Add(obj.name);
                    continue;
                }

                var transform = obj.transform;
                var bounds = GetObjectBounds(transform);
                GetBoundsProjection(bounds, Vector3.up, out float bottom, out _);
                float bottomOffset = Vector3.Dot(transform.position, Vector3.up) - bottom;
                var targetPosition = transform.position;
                targetPosition.y = hit.point.y + bottomOffset + surfaceOffset;
                transform.position = targetPosition;

                if (alignToSurfaceNormal && hit.normal.sqrMagnitude > ProjectionEpsilon)
                    transform.rotation = Quaternion.FromToRotation(transform.up, hit.normal.normalized) * transform.rotation;
            }

            FinalizeTransformChanges(hittableObjects);
            lastResultSummary = $"表面吸附完成: {hittableObjects.Length - failed.Count}/{hittableObjects.Length} 个对象";
            lastResultDetail = BuildTransformChangeReport(hittableObjects, beforeSnapshots) +
                               (failed.Count > 0 ? "\n\n失败:\n" + string.Join("\n", failed) : "");
        }

        private void ExecuteGridSnap()
        {
            var selectedObjects = GetValidSelection();
            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("网格吸附提示", "请先选择至少一个对象。", "确定");
                return;
            }

            if (!IsFinite(gridSize) || gridSize.x <= 0f || gridSize.y <= 0f || gridSize.z <= 0f)
            {
                EditorUtility.DisplayDialog("网格吸附提示", "网格尺寸必须大于 0。", "确定");
                return;
            }

            if (!snapGridX && !snapGridY && !snapGridZ)
            {
                EditorUtility.DisplayDialog("网格吸附提示", "请至少勾选一个吸附轴。", "确定");
                return;
            }

            if (!ConfirmTransformOperation("确认网格吸附", "网格吸附", selectedObjects))
                return;

            Undo.RecordObjects(selectedObjects.Select(obj => obj.transform).ToArray(), "Snap Selection To Grid");
            var beforeSnapshots = CaptureTransformSnapshots(selectedObjects);

            foreach (var obj in selectedObjects)
            {
                var position = obj.transform.position;
                if (snapGridX) position.x = SnapValue(position.x, gridSize.x);
                if (snapGridY) position.y = SnapValue(position.y, gridSize.y);
                if (snapGridZ) position.z = SnapValue(position.z, gridSize.z);
                obj.transform.position = position;
            }

            FinalizeTransformChanges(selectedObjects);
            lastResultSummary = $"网格吸附完成: {selectedObjects.Length} 个对象 | 网格 {gridSize}";
            lastResultDetail = BuildTransformChangeReport(selectedObjects, beforeSnapshots);
        }

        private void ExecuteRandomDressing()
        {
            var selectedObjects = GetValidSelection();
            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("随机错落提示", "请先选择至少一个对象。", "确定");
                return;
            }

            if (!ValidateRandomSettings())
                return;

            if (randomUniformScaleRange.x <= 0f || randomUniformScaleRange.y <= 0f || randomUniformScaleRange.x > randomUniformScaleRange.y)
            {
                EditorUtility.DisplayDialog("随机错落提示", "统一缩放范围必须大于 0，且最小值不能超过最大值。", "确定");
                return;
            }

            if (!ConfirmTransformOperation("确认随机错落", "随机错落", selectedObjects))
                return;

            Undo.RecordObjects(selectedObjects.Select(obj => obj.transform).ToArray(), "Randomize Selection Dressing");
            var beforeSnapshots = CaptureTransformSnapshots(selectedObjects);
            var random = new System.Random(randomSeed);

            foreach (var obj in selectedObjects)
            {
                var transform = obj.transform;
                var offset = new Vector3(
                    NextRange(random, -Mathf.Abs(randomPositionRange.x), Mathf.Abs(randomPositionRange.x)),
                    NextRange(random, -Mathf.Abs(randomPositionRange.y), Mathf.Abs(randomPositionRange.y)),
                    NextRange(random, -Mathf.Abs(randomPositionRange.z), Mathf.Abs(randomPositionRange.z)));

                transform.position += offset;

                float yaw = NextRange(random, randomYawRange.x, randomYawRange.y);
                if (Mathf.Abs(yaw) > ProjectionEpsilon)
                    transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * transform.rotation;

                float scaleMultiplier = NextRange(random, randomUniformScaleRange.x, randomUniformScaleRange.y);
                if (!Mathf.Approximately(scaleMultiplier, 1f))
                    transform.localScale *= scaleMultiplier;
            }

            FinalizeTransformChanges(selectedObjects);
            lastResultSummary = $"随机错落完成: {selectedObjects.Length} 个对象 | 种子 {randomSeed}";
            lastResultDetail = BuildTransformChangeReport(selectedObjects, beforeSnapshots);
        }
        #endregion

        private string BuildTransformResultDetail(IEnumerable<GameObject> selectedObjects)
        {
            return "对象:\n" + SimpleToolsSafetyUtility.JoinPreview(selectedObjects?.Select(obj => obj != null ? obj.name : null), 12);
        }

        private Dictionary<Transform, TransformChangeSnapshot> CaptureTransformSnapshots(IEnumerable<GameObject> objects)
        {
            var snapshots = new Dictionary<Transform, TransformChangeSnapshot>();
            if (objects == null)
                return snapshots;

            foreach (var obj in objects)
            {
                if (obj == null || obj.transform == null)
                    continue;

                snapshots[obj.transform] = CaptureTransformSnapshot(obj.transform);
            }
            return snapshots;
        }

        private TransformChangeSnapshot CaptureTransformSnapshot(Transform transform)
        {
            var rect = transform as RectTransform;
            return new TransformChangeSnapshot
            {
                Position = transform.position,
                LocalPosition = transform.localPosition,
                LocalScale = transform.localScale,
                Rotation = transform.rotation,
                RectSize = rect != null ? rect.rect.size : Vector2.zero,
                IsRectTransform = rect != null
            };
        }

        private string BuildTransformChangeReport(IEnumerable<GameObject> objects, Dictionary<Transform, TransformChangeSnapshot> beforeSnapshots)
        {
            if (objects == null)
                return "没有对象。";

            var detailLines = new List<string>();
            int total = 0;
            int moved = 0;
            int scaled = 0;
            int rotated = 0;
            int resized = 0;
            int unchanged = 0;

            foreach (var obj in objects)
            {
                if (obj == null || obj.transform == null)
                    continue;

                total++;
                if (beforeSnapshots == null || !beforeSnapshots.TryGetValue(obj.transform, out var before))
                    continue;

                var after = CaptureTransformSnapshot(obj.transform);
                bool positionChanged = (after.Position - before.Position).sqrMagnitude > ProjectionEpsilon;
                bool scaleChanged = (after.LocalScale - before.LocalScale).sqrMagnitude > ProjectionEpsilon;
                bool rotationChanged = Quaternion.Angle(after.Rotation, before.Rotation) > 0.01f;
                bool rectChanged = after.IsRectTransform && (after.RectSize - before.RectSize).sqrMagnitude > ProjectionEpsilon;

                if (positionChanged) moved++;
                if (scaleChanged) scaled++;
                if (rotationChanged) rotated++;
                if (rectChanged) resized++;
                if (!positionChanged && !scaleChanged && !rotationChanged && !rectChanged)
                {
                    unchanged++;
                    continue;
                }

                if (detailLines.Count < 12)
                {
                    var changes = new List<string>();
                    if (positionChanged) changes.Add($"Pos {before.Position.ToString("F2")} -> {after.Position.ToString("F2")}");
                    if (scaleChanged) changes.Add($"Scale {before.LocalScale.ToString("F2")} -> {after.LocalScale.ToString("F2")}");
                    if (rotationChanged) changes.Add($"Rot {before.Rotation.eulerAngles.ToString("F1")} -> {after.Rotation.eulerAngles.ToString("F1")}");
                    if (rectChanged) changes.Add($"Size {before.RectSize.ToString("F1")} -> {after.RectSize.ToString("F1")}");
                    detailLines.Add($"{GetHierarchyPath(obj.transform)} | {string.Join("；", changes)}");
                }
            }

            string summary = $"变更统计: 对象 {total} | 移动 {moved} | 缩放 {scaled} | 旋转 {rotated} | UI尺寸 {resized} | 未变化 {unchanged}";
            if (detailLines.Count == 0)
                return summary + "\n没有检测到 Transform 变化。";

            return summary + "\n\n变更明细:\n" + string.Join("\n", detailLines);
        }

        private bool TryFindSurfaceHit(GameObject obj, IReadOnlyCollection<GameObject> currentSelection, out RaycastHit hit)
        {
            hit = default;
            if (obj == null)
                return false;

            var bounds = GetObjectBounds(obj.transform);
            var origin = bounds.center + Vector3.up * surfaceCastHeight;
            var ray = new Ray(origin, Vector3.down);
            var hits = Physics.RaycastAll(ray, surfaceCastHeight + surfaceCastDistance, surfaceLayerMask, QueryTriggerInteraction.Ignore)
                .OrderBy(item => item.distance);

            foreach (var candidate in hits)
            {
                if (candidate.collider == null)
                    continue;

                if (ignoreSelfColliders && IsSameObjectHierarchy(candidate.collider.transform, obj.transform))
                    continue;

                if (ignoreSelectionColliders && IsColliderInsideSelection(candidate.collider.transform, currentSelection, obj))
                    continue;

                hit = candidate;
                return true;
            }

            return false;
        }

        private bool IsColliderInsideSelection(Transform colliderTransform, IReadOnlyCollection<GameObject> currentSelection, GameObject owner)
        {
            if (colliderTransform == null || currentSelection == null)
                return false;

            foreach (var selected in currentSelection)
            {
                if (selected == null || selected == owner)
                    continue;

                if (colliderTransform.IsChildOf(selected.transform))
                    return true;
            }

            return false;
        }

        private bool IsSameObjectHierarchy(Transform colliderTransform, Transform objectTransform)
        {
            if (colliderTransform == null || objectTransform == null)
                return false;

            return colliderTransform == objectTransform ||
                   colliderTransform.IsChildOf(objectTransform) ||
                   objectTransform.IsChildOf(colliderTransform);
        }

        private float SnapValue(float value, float grid)
        {
            if (grid <= 0f)
                return value;
            return Mathf.Round(value / grid) * grid;
        }

        private float NextRange(System.Random random, float min, float max)
        {
            if (random == null)
                return min;
            if (max < min)
            {
                float temp = min;
                min = max;
                max = temp;
            }
            return min + (float)random.NextDouble() * (max - min);
        }

        private bool ValidateSurfaceSettings()
        {
            if (!IsFinite(surfaceCastHeight) || !IsFinite(surfaceCastDistance) || !IsFinite(surfaceOffset) ||
                surfaceCastHeight <= 0f || surfaceCastDistance <= 0f)
            {
                EditorUtility.DisplayDialog("表面吸附提示", "上方起点、检测距离必须是大于 0 的有效数字，表面偏移也必须是有效数字。", "确定");
                return false;
            }

            return true;
        }

        private bool ValidateRandomSettings()
        {
            if (!IsFinite(randomPositionRange) || !IsFinite(randomYawRange) || !IsFinite(randomUniformScaleRange))
            {
                EditorUtility.DisplayDialog("随机错落提示", "随机参数里存在无效数字，请检查位置扰动、旋转范围和缩放范围。", "确定");
                return false;
            }

            return true;
        }

        private bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        #region 边界计算
        [InfoBox("边界按当前模式从 Renderer、Collider、RectTransform 或 Transform 计算。", InfoMessageType.Info)]
        [PropertySpace(5)]
        
        private Bounds GetObjectBounds(Transform transform)
        {
            Bounds bounds;
            var rectTransform = transform as RectTransform;

            // RectTransform (UI) - 优先处理
            if (boundsMode == BoundsCalculationMode.RectTransform || 
                (boundsMode == BoundsCalculationMode.Auto && rectTransform != null))
            {
                if (rectTransform != null)
                {
                    var corners = new Vector3[4];
                    rectTransform.GetWorldCorners(corners);
                    
                    // 计算中心点（4个角的平均值）
                    Vector3 center = Vector3.zero;
                    foreach (var corner in corners)
                        center += corner;
                    center /= 4f;
                    
                    // 创建边界并扩展到所有角
                    bounds = new Bounds(center, Vector3.zero);
                    foreach (var corner in corners)
                        bounds.Encapsulate(corner);
                    
                    // 包含子对象（如果启用）
                    if (includeChildren)
                    {
                        var childRects = transform.GetComponentsInChildren<RectTransform>();
                        foreach (var childRect in childRects)
                        {
                            if (childRect == rectTransform) continue;
                            if (activeOnly && !childRect.gameObject.activeInHierarchy) continue;
                            
                            var childCorners = new Vector3[4];
                            childRect.GetWorldCorners(childCorners);
                            foreach (var corner in childCorners)
                                bounds.Encapsulate(corner);
                        }
                    }
                    
                    return bounds;
                }
            }

            // Renderer
            if (boundsMode == BoundsCalculationMode.Renderer || boundsMode == BoundsCalculationMode.Auto)
            {
                var renderers = includeChildren
                    ? transform.GetComponentsInChildren<Renderer>()
                    : new[] { transform.GetComponent<Renderer>() };

                Bounds? rendererBounds = null;
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    if (activeOnly && !renderer.gameObject.activeInHierarchy) continue;

                    EncapsulateBounds(ref rendererBounds, renderer.bounds);
                }

                if (rendererBounds.HasValue)
                    return rendererBounds.Value;
            }

            // Collider
            if (boundsMode == BoundsCalculationMode.Collider || boundsMode == BoundsCalculationMode.Auto)
            {
                var colliders = includeChildren
                    ? transform.GetComponentsInChildren<Collider>()
                    : new[] { transform.GetComponent<Collider>() };

                Bounds? colliderBounds = null;
                foreach (var collider in colliders)
                {
                    if (collider == null) continue;
                    if (activeOnly && !collider.gameObject.activeInHierarchy) continue;

                    EncapsulateBounds(ref colliderBounds, collider.bounds);
                }

                var colliders2D = includeChildren
                    ? transform.GetComponentsInChildren<Collider2D>()
                    : new[] { transform.GetComponent<Collider2D>() };

                foreach (var collider2D in colliders2D)
                {
                    if (collider2D == null) continue;
                    if (activeOnly && !collider2D.gameObject.activeInHierarchy) continue;

                    EncapsulateBounds(ref colliderBounds, collider2D.bounds);
                }

                if (colliderBounds.HasValue)
                    return colliderBounds.Value;
            }

            // Transform位置
            return new Bounds(transform.position, Vector3.zero);
        }

        private Bounds GetReferenceBounds(Transform[] transforms)
        {
            switch (alignReference)
            {
                case AlignReference.FirstSelected:
                    return GetObjectBounds(transforms[0]);
                case AlignReference.LastSelected:
                    return GetObjectBounds(transforms[transforms.Length - 1]);
                case AlignReference.ParentCenter:
                    if (transforms[0].parent != null)
                        return GetObjectBounds(transforms[0].parent);
                    break;
                case AlignReference.WorldCenter:
                    if (IsLocalSpace() && TryGetCommonParent(transforms, out var parent) && parent != null)
                        return new Bounds(parent.TransformPoint(Vector3.zero), Vector3.zero);
                    return new Bounds(Vector3.zero, Vector3.zero);
            }

            // AllBounds - 计算所有对象的组合边界
            Bounds? combinedBounds = null;
            foreach (var transform in transforms)
            {
                var bounds = GetObjectBounds(transform);
                EncapsulateBounds(ref combinedBounds, bounds);
            }
            return combinedBounds ?? new Bounds();
        }

        private void EncapsulateBounds(ref Bounds? combinedBounds, Bounds bounds)
        {
            if (combinedBounds == null)
            {
                combinedBounds = bounds;
                return;
            }

            var combined = combinedBounds.Value;
            combined.Encapsulate(bounds);
            combinedBounds = combined;
        }

        private Vector3 GetReferencePosition(Transform[] transforms)
        {
            return GetReferenceBounds(transforms).center;
        }

        private GameObject GetReferenceObject(GameObject[] objects)
        {
            switch (matchReference)
            {
                case MatchReferenceMode.FirstSelected:
                    return objects[0];
                case MatchReferenceMode.LastSelected:
                    return objects[objects.Length - 1];
                case MatchReferenceMode.Parent:
                    if (objects[0].transform.parent != null)
                        return objects[0].transform.parent.gameObject;
                    return null;
            }
            return objects[0];
        }

        private Vector3 GetAlignAxis(AlignMode mode, Transform[] transforms)
        {
            switch (mode)
            {
                case AlignMode.Left:
                case AlignMode.Right:
                case AlignMode.HorizontalCenter:
                    return GetSpaceAxis(Vector3.right, transforms);
                case AlignMode.Top:
                case AlignMode.Bottom:
                case AlignMode.VerticalCenter:
                    return GetSpaceAxis(Vector3.up, transforms);
                case AlignMode.Front:
                case AlignMode.Back:
                case AlignMode.DepthCenter:
                    return GetSpaceAxis(Vector3.forward, transforms);
                case AlignMode.CameraLeft:
                case AlignMode.CameraRight:
                case AlignMode.CameraHorizontalCenter:
                    return GetCameraRightVector();
                case AlignMode.CameraTop:
                case AlignMode.CameraBottom:
                case AlignMode.CameraVerticalCenter:
                    return GetCameraUpVector();
                case AlignMode.CameraFront:
                case AlignMode.CameraBack:
                case AlignMode.CameraDepthCenter:
                    return GetCameraForwardVector();
                default:
                    return Vector3.right;
            }
        }

        private Vector3 GetDistributeAxis(DistributeMode mode, List<Transform> transforms)
        {
            return GetDistributeAxis(mode, transforms != null ? transforms.ToArray() : null);
        }

        private Vector3 GetDistributeAxis(DistributeMode mode, Transform[] transforms)
        {
            switch (mode)
            {
                case DistributeMode.HorizontalEven:
                case DistributeMode.HorizontalSpacing:
                    return GetSpaceAxis(Vector3.right, transforms);
                case DistributeMode.VerticalEven:
                case DistributeMode.VerticalSpacing:
                    return GetSpaceAxis(Vector3.up, transforms);
                case DistributeMode.DepthEven:
                case DistributeMode.DepthSpacing:
                    return GetSpaceAxis(Vector3.forward, transforms);
                case DistributeMode.CameraHorizontalEven:
                case DistributeMode.CameraHorizontalSpacing:
                    return GetCameraRightVector();
                case DistributeMode.CameraVerticalEven:
                case DistributeMode.CameraVerticalSpacing:
                    return GetCameraUpVector();
                case DistributeMode.CameraDepthEven:
                case DistributeMode.CameraDepthSpacing:
                    return GetCameraForwardVector();
                default:
                    return Vector3.right;
            }
        }

        private Vector3 GetSpaceAxis(Vector3 worldAxis, Transform[] transforms)
        {
            if (IsCameraRelativeMode())
            {
                if (worldAxis == Vector3.right)
                    return GetCameraRightVector();
                if (worldAxis == Vector3.up)
                    return GetCameraUpVector();
                if (worldAxis == Vector3.forward)
                    return GetCameraForwardVector();
            }

            if (!IsLocalSpace())
                return worldAxis;

            if (TryGetCommonParent(transforms, out var parent) && parent != null)
                return parent.TransformDirection(worldAxis);

            return worldAxis;
        }

        private void GetBoundsProjection(Bounds bounds, Vector3 axis, out float min, out float max)
        {
            axis = axis.sqrMagnitude > ProjectionEpsilon ? axis.normalized : Vector3.right;
            min = float.MaxValue;
            max = float.MinValue;

            var center = bounds.center;
            var extents = bounds.extents;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                var corner = center + new Vector3(extents.x * x, extents.y * y, extents.z * z);
                float projection = Vector3.Dot(corner, axis);
                min = Mathf.Min(min, projection);
                max = Mathf.Max(max, projection);
            }

            if (min == float.MaxValue)
                min = max = Vector3.Dot(bounds.center, axis);
        }

        private bool ValidateCoordinateMode(GameObject[] selectedObjects, bool isCameraOperation)
        {
            if (!IsLocalSpace() || isCameraOperation)
                return true;

            var transforms = selectedObjects
                .Where(obj => obj != null)
                .Select(obj => obj.transform)
                .ToArray();

            if (TryGetCommonParent(transforms, out _))
                return true;

            EditorUtility.DisplayDialog("局部坐标模式不可用",
                "局部坐标模式要求本次处理的对象处在同一个父对象下。\n\n" +
                "当前选区存在多个父级，继续处理会让边界轴向和局部轴向不一致。\n" +
                "请改用世界坐标，或只选择同一父级下的对象。",
                "知道了");
            return false;
        }

        private bool ValidateAlignReference(GameObject[] selectedObjects)
        {
            if (alignReference != AlignReference.ParentCenter)
                return true;

            var first = selectedObjects != null && selectedObjects.Length > 0 ? selectedObjects[0] : null;
            if (first != null && first.transform.parent != null)
                return true;

            EditorUtility.DisplayDialog("参考对象不可用",
                "当前对齐参考设置为“父对象中心”，但第一个有效选中对象没有父对象。\n\n" +
                "请改用“所有对象边界 / 第一个选中对象 / 世界中心”，或选择带父级的对象。",
                "知道了");
            return false;
        }

        private bool TryGetCommonParent(Transform[] transforms, out Transform parent)
        {
            parent = null;
            if (transforms == null || transforms.Length == 0)
                return false;

            parent = transforms[0] != null ? transforms[0].parent : null;
            foreach (var transform in transforms)
            {
                if (transform == null || transform.parent != parent)
                    return false;
            }
            return true;
        }

        private bool IsCameraAlignMode(AlignMode mode)
        {
            return mode == AlignMode.CameraLeft ||
                   mode == AlignMode.CameraRight ||
                   mode == AlignMode.CameraTop ||
                   mode == AlignMode.CameraBottom ||
                   mode == AlignMode.CameraFront ||
                   mode == AlignMode.CameraBack ||
                   mode == AlignMode.CameraHorizontalCenter ||
                   mode == AlignMode.CameraVerticalCenter ||
                   mode == AlignMode.CameraDepthCenter;
        }

        private bool IsCameraDistributeMode(DistributeMode mode)
        {
            return mode == DistributeMode.CameraHorizontalEven ||
                   mode == DistributeMode.CameraVerticalEven ||
                   mode == DistributeMode.CameraDepthEven ||
                   mode == DistributeMode.CameraHorizontalSpacing ||
                   mode == DistributeMode.CameraVerticalSpacing ||
                   mode == DistributeMode.CameraDepthSpacing;
        }

        private bool IsSamePreviewSelection(GameObject[] selectedObjects)
        {
            if (selectedObjects == null || selectedObjects.Length != originalPositions.Count)
                return false;

            foreach (var obj in selectedObjects)
            {
                if (obj == null || !originalPositions.ContainsKey(obj.transform))
                    return false;
            }
            return true;
        }

        private void RestorePreviewPositions(bool recordUndo)
        {
            foreach (var kvp in originalPositions)
            {
                if (kvp.Key == null)
                    continue;

                if (recordUndo)
                    Undo.RecordObject(kvp.Key, "Clear Distribution Preview");

                kvp.Key.position = kvp.Value;
                EditorUtility.SetDirty(kvp.Key);
            }
        }

        private Vector3 GetCameraRightVector()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
            {
                return sceneView.camera.transform.right;
            }
            return Vector3.right;
        }

        private Vector3 GetCameraUpVector()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
            {
                return sceneView.camera.transform.up;
            }
            return Vector3.up;
        }

        private Vector3 GetCameraForwardVector()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
            {
                return sceneView.camera.transform.forward;
            }
            return Vector3.forward;
        }

        private bool IsCameraRelativeMode()
        {
            return coordinateMode == CoordinateMode.CameraRelative;
        }

        private bool IsWorldSpace()
        {
            return coordinateMode == CoordinateMode.WorldSpace;
        }

        private bool IsLocalSpace()
        {
            return coordinateMode == CoordinateMode.LocalSpace;
        }
        #endregion

        #region 辅助方法
        [InfoBox("🔧 辅助方法集合：专业工具函数\n" +
                "• 选择验证：过滤有效对象\n" +
                "• 模式判断：分布类型检测\n" +
                "• 信息显示：选择状态反馈\n" +
                "• 性能优化：高效对象处理", InfoMessageType.Info)]
        [PropertySpace(5)]
        
        private GameObject[] GetValidSelection()
        {
            var selection = Selection.gameObjects;
            if (selection == null) return new GameObject[0];

            var validObjects = selection.Where(obj => 
            {
                if (obj == null) return false;
                if (activeOnly && !obj.activeInHierarchy) return false;
                if (ignoreLocked && (obj.hideFlags & HideFlags.NotEditable) != 0) return false;
                if (protectPrefabAssets && PrefabUtility.IsPartOfPrefabAsset(obj)) return false;
                return true;
            }).ToArray();

            if (skipNestedSelection && validObjects.Length > 1)
            {
                var selectedTransforms = new HashSet<Transform>(validObjects.Select(obj => obj.transform));
                validObjects = validObjects
                    .Where(obj => !HasSelectedAncestor(obj.transform, selectedTransforms))
                    .ToArray();
            }

            return validObjects;
        }

        private bool HasSelectedAncestor(Transform transform, HashSet<Transform> selectedTransforms)
        {
            if (transform == null || selectedTransforms == null)
                return false;

            var parent = transform.parent;
            while (parent != null)
            {
                if (selectedTransforms.Contains(parent))
                    return true;
                parent = parent.parent;
            }
            return false;
        }

        private bool IsSpacingDistribute()
        {
            return distributeMode == DistributeMode.HorizontalSpacing ||
                   distributeMode == DistributeMode.VerticalSpacing ||
                   distributeMode == DistributeMode.DepthSpacing ||
                   distributeMode == DistributeMode.CameraHorizontalSpacing ||
                   distributeMode == DistributeMode.CameraVerticalSpacing ||
                   distributeMode == DistributeMode.CameraDepthSpacing;
        }

        private string GetSelectionInfo()
        {
            var selected = GetValidSelection();
            if (selected.Length == 0) return "未选中任何对象";
            
            var info = $"{selected.Length} 个对象";
            var rawSelection = Selection.gameObjects;
            int filtered = rawSelection != null ? Mathf.Max(0, rawSelection.Length - selected.Length) : 0;
            if (filtered > 0)
                info += $" | 已过滤 {filtered} 个";

            if (selected.Length > 0)
            {
                var firstObj = selected[0];
                var bounds = GetObjectBounds(firstObj.transform);
                info += $" | 首个: {firstObj.name} (尺寸: {bounds.size.ToString("F2")})";
            }
            return info;
        }
        #endregion
    }
    #endregion
}
