using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

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

        
        [InfoBox("💡 支持3D对象、2D精灵、UI元素的智能对齐与分布\n⚡ 支持快捷键操作、实时预览、批量处理", InfoMessageType.None)]
        [PropertySpace(10)]
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
        #endregion

        #region 预览系统字段
        private Dictionary<Transform, Vector3> originalPositions = new Dictionary<Transform, Vector3>();
        private bool isPreviewing = false;
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
        [Button("← 左对齐", ButtonHeight = 35), GUIColor(0.3f, 0.8f, 0.8f)]
        private void QuickAlignLeft() { alignMode = AlignMode.Left; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/AlignButtons")]
        [Button("→ 右对齐", ButtonHeight = 35), GUIColor(0.3f, 0.8f, 0.8f)]
        private void QuickAlignRight() { alignMode = AlignMode.Right; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/AlignButtons")]
        [Button("↑ 上对齐", ButtonHeight = 35), GUIColor(0.3f, 0.8f, 0.8f)]
        private void QuickAlignTop() { alignMode = AlignMode.Top; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/AlignButtons")]
        [Button("↓ 下对齐", ButtonHeight = 35), GUIColor(0.3f, 0.8f, 0.8f)]
        private void QuickAlignBottom() { alignMode = AlignMode.Bottom; AlignObjects(); }

        [TabGroup("对齐", "基础对齐")]
        [HorizontalGroup("对齐/基础对齐/CenterButtons" )]
        [Button("⊟ 水平居中", ButtonHeight = 35), GUIColor(0.5f, 0.7f, 0.9f)]
        private void QuickAlignHCenter() { alignMode = AlignMode.HorizontalCenter; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/CenterButtons")]
        [Button("⊞ 垂直居中", ButtonHeight = 35), GUIColor(0.5f, 0.7f, 0.9f)]
        private void QuickAlignVCenter() { alignMode = AlignMode.VerticalCenter; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/CenterButtons")]
        [Button("◎ 深度居中", ButtonHeight = 35), GUIColor(0.5f, 0.7f, 0.9f)]
        private void QuickAlignDepthCenter() { alignMode = AlignMode.DepthCenter; AlignObjects(); }

        [TabGroup("对齐", "基础对齐")]
        [InfoBox("📦 深度对齐（Z轴）：前后位置对齐控制", InfoMessageType.None)]
        [HorizontalGroup("对齐/基础对齐/DepthButtons")]
        [Button("◀ 前对齐(近)", ButtonHeight = 35), GUIColor(0.6f, 0.8f, 0.6f)]
        private void QuickAlignFront() { alignMode = AlignMode.Front; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/DepthButtons")]
        [Button("▶ 后对齐(远)", ButtonHeight = 35), GUIColor(0.6f, 0.8f, 0.6f)]
        private void QuickAlignBack() { alignMode = AlignMode.Back; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/DepthButtons")]
        [Button("⬌ 深度居中", ButtonHeight = 35), GUIColor(0.6f, 0.8f, 0.6f)]
        private void QuickAlignDepthCenter2() { alignMode = AlignMode.DepthCenter; AlignObjects(); }

        [TabGroup("对齐", "基础对齐")]
        [HorizontalGroup("对齐/基础对齐/CameraAlignButtons")]
        [Button("⬅ 镜头左对齐", ButtonHeight = 35), GUIColor(0.4f, 0.6f, 0.8f)]
        private void QuickAlignCameraLeft() { alignMode = AlignMode.CameraLeft; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/CameraAlignButtons")]
        [Button("➡ 镜头右对齐", ButtonHeight = 35), GUIColor(0.4f, 0.6f, 0.8f)]
        private void QuickAlignCameraRight() { alignMode = AlignMode.CameraRight; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/CameraAlignButtons")]
        [Button("⬆ 镜头上对齐", ButtonHeight = 35), GUIColor(0.4f, 0.6f, 0.8f)]
        private void QuickAlignCameraTop() { alignMode = AlignMode.CameraTop; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/CameraAlignButtons")]
        [Button("⬇ 镜头下对齐", ButtonHeight = 35), GUIColor(0.4f, 0.6f, 0.8f)]
        private void QuickAlignCameraBottom() { alignMode = AlignMode.CameraBottom; AlignObjects(); }

        [TabGroup("对齐", "基础对齐")]
        [HorizontalGroup("对齐/基础对齐/CameraCenterButtons")]
        [Button("⬌ 镜头水平居中", ButtonHeight = 35), GUIColor(0.5f, 0.7f, 0.8f)]
        private void QuickAlignCameraHCenter() { alignMode = AlignMode.CameraHorizontalCenter; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/CameraCenterButtons")]
        [Button("⬍ 镜头垂直居中", ButtonHeight = 35), GUIColor(0.5f, 0.7f, 0.8f)]
        private void QuickAlignCameraVCenter() { alignMode = AlignMode.CameraVerticalCenter; AlignObjects(); }

        [HorizontalGroup("对齐/基础对齐/CameraCenterButtons")]
        [Button("⬊ 镜头深度居中", ButtonHeight = 35), GUIColor(0.5f, 0.7f, 0.8f)]
        private void QuickAlignCameraDepthCenter() { alignMode = AlignMode.CameraDepthCenter; AlignObjects(); }
        #endregion

        #region 分布设置
        [TabGroup("对齐", "智能分布")]
        [InfoBox("📐 智能分布系统：人性化对象排列\n" +
                "• 均匀分布：智能重新分布所有对象\n" +
                "• 固定间距：精确控制对象间距\n" +
                "• 预览功能：实时查看分布效果\n" +
                "• 多种模式：适应不同场景需求", InfoMessageType.Info)]

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
        [InfoBox("🎚️ 动态间距调整：实时控制对象间距\n" +
                "• 启用后拖动滑条即可实时预览分布效果\n" +
                "• 智能间距：自动计算基于对象尺寸的合适间距\n" +
                "• 同步间距：与下方固定间距保持一致\n" +
                "• 重置：快速清除额外间距\n" +
                "• 仅在间距分布模式下工作", InfoMessageType.Info)]

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
        [Button("🔄 同步间距值", ButtonHeight = 35), ShowIf("@enableDynamicSpacing && IsSpacingDistribute()")]
        private void SyncSpacingValues() { dynamicSpacing = distributionSpacing; }

        [HorizontalGroup("对齐/智能分布/DynamicSpacing")]
        [VerticalGroup("对齐/智能分布/DynamicSpacing/Right"), LabelWidth(100)]
        [Button("🎯 智能间距", ButtonHeight = 35), ShowIf("@enableDynamicSpacing && IsSpacingDistribute()")]
        private void AutoCalculateSpacing() { dynamicSpacing = CalculateOptimalSpacing(); }

        [HorizontalGroup("对齐/智能分布/DynamicSpacing")]
        [VerticalGroup("对齐/智能分布/DynamicSpacing/Right"), LabelWidth(100)]
        [Button("🔄 重置", ButtonHeight = 35), ShowIf("@enableDynamicSpacing && IsSpacingDistribute()")]
        private void ResetSpacing() { dynamicSpacing = 0f; }
        [HorizontalGroup("对齐/智能分布/PreviewButtons")]
        [Button("👁 预览分布", ButtonHeight = 35), GUIColor(0.4f, 0.8f, 0.6f)]
        private void PreviewDistribution() { PreviewDistributeObjects(); }

        [HorizontalGroup("对齐/智能分布/PreviewButtons")]
        [Button("✓ 应用预览", ButtonHeight = 35), GUIColor(0.6f, 0.8f, 0.4f), ShowIf("@isPreviewing")]
        private void ApplyPreview() { ApplyDistributionPreview(); }

        [HorizontalGroup("对齐/智能分布/PreviewButtons")]
        [Button("✖ 清除预览", ButtonHeight = 35), GUIColor(0.8f, 0.4f, 0.4f), ShowIf("@isPreviewing")]
        private void ClearPreview() { ClearDistributionPreview(); }

        [TabGroup("对齐", "智能分布")]
        [HorizontalGroup("对齐/智能分布/DistributeButtons" )]
        [Button("↔ 水平均匀", ButtonHeight = 35), GUIColor(0.7f, 0.5f, 0.9f)]
        private void QuickDistributeH() { distributeMode = DistributeMode.HorizontalEven; DistributeObjects(); }

        [HorizontalGroup("对齐/智能分布/DistributeButtons")]
        [Button("↕ 垂直均匀", ButtonHeight = 35), GUIColor(0.7f, 0.5f, 0.9f)]
        private void QuickDistributeV() { distributeMode = DistributeMode.VerticalEven; DistributeObjects(); }

        [HorizontalGroup("对齐/智能分布/DistributeButtons")]
        [Button("⇿ 深度均匀", ButtonHeight = 35), GUIColor(0.7f, 0.5f, 0.9f)]
        private void QuickDistributeD() { distributeMode = DistributeMode.DepthEven; DistributeObjects(); }

        [TabGroup("对齐", "智能分布")]
        [HorizontalGroup("对齐/智能分布/SpacingButtons")]
        [Button("⟷ 水平间距", ButtonHeight = 35), GUIColor(0.9f, 0.6f, 0.4f)]
        private void QuickDistributeHS() { distributeMode = DistributeMode.HorizontalSpacing; DistributeObjects(); }

        [HorizontalGroup("对齐/智能分布/SpacingButtons")]
        [Button("⟺ 垂直间距", ButtonHeight = 35), GUIColor(0.9f, 0.6f, 0.4f)]
        private void QuickDistributeVS() { distributeMode = DistributeMode.VerticalSpacing; DistributeObjects(); }

        [HorizontalGroup("对齐/智能分布/SpacingButtons")]
        [Button("⇆ 深度间距", ButtonHeight = 35), GUIColor(0.9f, 0.6f, 0.4f)]
        private void QuickDistributeDS() { distributeMode = DistributeMode.DepthSpacing; DistributeObjects(); }

        [TabGroup("对齐", "智能分布")]
        [HorizontalGroup("对齐/智能分布/CameraButtons")]
        [Button("📷 镜头水平均匀", ButtonHeight = 35), GUIColor(0.4f, 0.7f, 0.9f)]
        private void QuickDistributeCH() { distributeMode = DistributeMode.CameraHorizontalEven; DistributeObjects(); }

        [HorizontalGroup("对齐/智能分布/CameraButtons")]
        [Button("📷 镜头垂直均匀", ButtonHeight = 35), GUIColor(0.4f, 0.7f, 0.9f)]
        private void QuickDistributeCV() { distributeMode = DistributeMode.CameraVerticalEven; DistributeObjects(); }

        [HorizontalGroup("对齐/智能分布/CameraButtons")]
        [Button("📷 镜头深度均匀", ButtonHeight = 35), GUIColor(0.4f, 0.7f, 0.9f)]
        private void QuickDistributeCD() { distributeMode = DistributeMode.CameraDepthEven; DistributeObjects(); }

        [TabGroup("对齐", "智能分布")]
        [HorizontalGroup("对齐/智能分布/CameraSpacingButtons")]
        [Button("📐 镜头水平间距", ButtonHeight = 35), GUIColor(0.6f, 0.8f, 0.7f)]
        private void QuickDistributeCHS() { distributeMode = DistributeMode.CameraHorizontalSpacing; DistributeObjects(); }

        [HorizontalGroup("对齐/智能分布/CameraSpacingButtons")]
        [Button("📐 镜头垂直间距", ButtonHeight = 35), GUIColor(0.6f, 0.8f, 0.7f)]
        private void QuickDistributeCVS() { distributeMode = DistributeMode.CameraVerticalSpacing; DistributeObjects(); }

        [HorizontalGroup("对齐/智能分布/CameraSpacingButtons")]
        [Button("📐 镜头深度间距", ButtonHeight = 35), GUIColor(0.6f, 0.8f, 0.7f)]
        private void QuickDistributeCDS() { distributeMode = DistributeMode.CameraDepthSpacing; DistributeObjects(); }
        #endregion

        #region 匹配设置
        [TabGroup("对齐", "尺寸匹配")]
        [InfoBox("🔄 商业级尺寸匹配系统\n" +
                "✓ 完整支持：3D对象 / 2D精灵 / UI元素\n" +
                "✓ 精确匹配：宽度 / 高度 / 深度（Z轴）\n" +
                "✓ 变换匹配：旋转 / 整体缩放\n" +
                "✓ 智能处理：自动识别对象类型和边界\n" +
                "✓ 混合模式：支持UI与3D对象互相参考", InfoMessageType.Info)]
        [PropertySpace(5)]

        [TabGroup("对齐", "尺寸匹配")]
        [LabelText("匹配参考对象"), LabelWidth(120)]
        [InfoBox("📌 参考对象选择规则：\n" +
                "• 第一个选中：使用首个选择的对象作为参考\n" +
                "• 最后选中：使用最后选择的对象作为参考\n" +
                "• 父对象中心：使用共同父对象作为参考", InfoMessageType.None)]
        public AlignReference matchReference = AlignReference.FirstSelected;

        [TabGroup("对齐", "尺寸匹配")]
        [InfoBox("📏 尺寸匹配选项（精确控制）：\n" +
                "• 宽度(X)：水平尺寸，3D对象通过Scale调整，UI对象调整RectTransform\n" +
                "• 高度(Y)：垂直尺寸，智能识别边界类型\n" +
                "• 深度(Z)：纵深尺寸，支持3D对象和UI的Z轴缩放\n" +
                "• 边界为零时自动使用Scale直接匹配", InfoMessageType.Info)]
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
        [Button("✓ 执行匹配", ButtonHeight = 45), GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
        private void MatchObjects() { ExecuteMatch(); }
        #endregion

        #region 高级选项
        [TabGroup("对齐", "高级选项")]
        [InfoBox("⚙️ 高级选项：精细控制对齐行为\n" +
                "• 边界计算：包含子对象边界\n" +
                "• 对象过滤：活跃/锁定状态\n" +
                "• 操作反馈：撤销和选中控制\n" +
                "• 实时状态：当前选中信息", InfoMessageType.Info)]

        
        [TabGroup("对齐", "高级选项")]
        [LabelText("包含子对象"), PropertySpace(5)]
        [InfoBox("边界计算时是否包含子对象：\n• 开启：计算所有子对象的组合边界\n• 关闭：仅使用父对象边界\n• 适合：复杂对象和预制件")]
        public bool includeChildren = false;

        [TabGroup("对齐", "高级选项")]
        [LabelText("仅处理活跃对象"), PropertySpace(5)]
        [InfoBox("是否只处理激活状态的对象：\n• 开启：跳过未激活的对象\n• 关闭：处理所有选中对象\n• 推荐：开启以避免意外操作")]
        public bool activeOnly = true;

        [TabGroup("对齐", "高级选项")]
        [LabelText("忽略锁定对象"), PropertySpace(5)]
        [InfoBox("是否跳过锁定(HideFlags.NotEditable)的对象：\n• 开启：保护重要对象不被修改\n• 关闭：处理所有对象\n• 安全保护机制")]
        public bool ignoreLocked = true;

        [TabGroup("对齐", "高级选项")]
        [LabelText("对齐后选中"), PropertySpace(5)]
        [InfoBox("操作完成后是否保持对象选中状态：\n• 开启：继续选中已对齐对象\n• 关闭：取消选中\n• 便于连续操作")]
        public bool selectAfterAlign = true;

        [TabGroup("对齐", "高级选项")]
        [LabelText("显示成功提示"), PropertySpace(5)]
        [InfoBox("是否显示操作成功的提示对话框：\n• 开启：每次操作成功后显示提示\n• 关闭：静默执行，不显示成功提示\n• 减少弹窗干扰")]
        public bool showSuccessDialogs = false;

        [TabGroup("对齐", "高级选项")]
        [PropertySpace(10)]
        [InfoBox("🔄 撤销功能：\n• 撤销上次对齐/分布/匹配操作\n• 支持多次撤销\n• 快捷键：Ctrl+Z", InfoMessageType.Warning)]
        [Button("⟲ 撤销上次操作", ButtonHeight = 35)]
        private void UndoLastOperation() { Undo.PerformUndo(); }

        [TabGroup("对齐", "高级选项")]
        [PropertySpace(10)]
        [InfoBox("📊 实时状态监控：\n• 显示当前选中对象数量\n• 首个对象信息和尺寸\n• 帮助判断操作范围", InfoMessageType.None)]
        [InfoBox("当前选中: @GetSelectionInfo()", InfoMessageType.None)]
        #endregion

        #region 核心对齐功能
        [InfoBox("🚀 主执行按钮：\n" +
                "• 执行对齐：根据当前设置对齐选中对象\n" +
                "• 智能验证：自动检查选中对象数量\n" +
                "• 完整撤销：支持Ctrl+Z撤销操作\n" +
                "• 实时反馈：操作结果和成功提示", InfoMessageType.Info)]
        
        [Button("▶ 执行对齐", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
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

            Undo.RecordObjects(selectedObjects.Select(obj => obj.transform).ToArray(), "Align Objects");

            var transforms = selectedObjects.Select(obj => obj.transform).ToArray();
            var referencePosition = GetReferencePosition(transforms);
            var referenceBounds = GetReferenceBounds(transforms);

            foreach (var transform in transforms)
            {
                var objectBounds = GetObjectBounds(transform);
                // 始终在世界坐标系中计算
                var worldPosition = transform.position;
                Vector3 boundsOffset = objectBounds.center - worldPosition;

                // 获取相机相对向量
                Vector3 cameraRight = GetCameraRightVector();
                Vector3 cameraUp = GetCameraUpVector();
                Vector3 cameraForward = GetCameraForwardVector();

                switch (alignMode)
                {
                    case AlignMode.Left:
                        worldPosition.x = referenceBounds.min.x - boundsOffset.x;
                        break;
                    case AlignMode.Right:
                        worldPosition.x = referenceBounds.max.x - boundsOffset.x;
                        break;
                    case AlignMode.Top:
                        worldPosition.y = referenceBounds.max.y - boundsOffset.y;
                        break;
                    case AlignMode.Bottom:
                        worldPosition.y = referenceBounds.min.y - boundsOffset.y;
                        break;
                    case AlignMode.Front:
                        worldPosition.z = referenceBounds.min.z - boundsOffset.z;
                        break;
                    case AlignMode.Back:
                        worldPosition.z = referenceBounds.max.z - boundsOffset.z;
                        break;
                    case AlignMode.HorizontalCenter:
                        worldPosition.x = referenceBounds.center.x - boundsOffset.x;
                        break;
                    case AlignMode.VerticalCenter:
                        worldPosition.y = referenceBounds.center.y - boundsOffset.y;
                        break;
                    case AlignMode.DepthCenter:
                        worldPosition.z = referenceBounds.center.z - boundsOffset.z;
                        break;

                    // 相机相对对齐模式
                    case AlignMode.CameraLeft:
                        {
                            // 计算对象中心在相机右方向上的投影
                            float objProjection = Vector3.Dot(objectBounds.center, cameraRight);
                            float refProjection = Vector3.Dot(referenceBounds.min, cameraRight);
                            // 沿相机右方向移动对象
                            worldPosition += cameraRight * (refProjection - objProjection);
                        }
                        break;
                    case AlignMode.CameraRight:
                        {
                            float objProjection = Vector3.Dot(objectBounds.center, cameraRight);
                            float refProjection = Vector3.Dot(referenceBounds.max, cameraRight);
                            worldPosition += cameraRight * (refProjection - objProjection);
                        }
                        break;
                    case AlignMode.CameraTop:
                        {
                            float objProjection = Vector3.Dot(objectBounds.center, cameraUp);
                            float refProjection = Vector3.Dot(referenceBounds.max, cameraUp);
                            worldPosition += cameraUp * (refProjection - objProjection);
                        }
                        break;
                    case AlignMode.CameraBottom:
                        {
                            float objProjection = Vector3.Dot(objectBounds.center, cameraUp);
                            float refProjection = Vector3.Dot(referenceBounds.min, cameraUp);
                            worldPosition += cameraUp * (refProjection - objProjection);
                        }
                        break;
                    case AlignMode.CameraFront:
                        {
                            float objProjection = Vector3.Dot(objectBounds.center, cameraForward);
                            float refProjection = Vector3.Dot(referenceBounds.min, cameraForward);
                            worldPosition += cameraForward * (refProjection - objProjection);
                        }
                        break;
                    case AlignMode.CameraBack:
                        {
                            float objProjection = Vector3.Dot(objectBounds.center, cameraForward);
                            float refProjection = Vector3.Dot(referenceBounds.max, cameraForward);
                            worldPosition += cameraForward * (refProjection - objProjection);
                        }
                        break;
                    case AlignMode.CameraHorizontalCenter:
                        {
                            float objProjection = Vector3.Dot(objectBounds.center, cameraRight);
                            float refProjection = Vector3.Dot(referenceBounds.center, cameraRight);
                            worldPosition += cameraRight * (refProjection - objProjection);
                        }
                        break;
                    case AlignMode.CameraVerticalCenter:
                        {
                            float objProjection = Vector3.Dot(objectBounds.center, cameraUp);
                            float refProjection = Vector3.Dot(referenceBounds.center, cameraUp);
                            worldPosition += cameraUp * (refProjection - objProjection);
                        }
                        break;
                    case AlignMode.CameraDepthCenter:
                        {
                            float objProjection = Vector3.Dot(objectBounds.center, cameraForward);
                            float refProjection = Vector3.Dot(referenceBounds.center, cameraForward);
                            worldPosition += cameraForward * (refProjection - objProjection);
                        }
                        break;
                }

                // 始终设置世界坐标，Unity会自动处理局部坐标转换
                transform.position = worldPosition;
            }

            if (showSuccessDialogs)
                EditorUtility.DisplayDialog("成功", $"✓ 成功对齐 {selectedObjects.Length} 个对象！", "确定");
            
            if (selectAfterAlign)
                Selection.objects = selectedObjects;
        }
        #endregion

        #region 分布功能
        [InfoBox("📐 分布功能：均匀排列多个对象\n" +
                "• 均匀分布：对象间距自动计算\n" +
                "• 固定间距：自定义精确间距\n" +
                "• 智能排序：保持相对位置关系\n" +
                "• 至少需要3个对象", InfoMessageType.Info)]
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

            // 如果在预览模式，先清除预览
            if (isPreviewing)
                ClearDistributionPreview();

            Undo.RecordObjects(selectedObjects.Select(obj => obj.transform).ToArray(), "Distribute Objects");

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

            float startPos = 0f, endPos = 0f;
            Vector3 axis = Vector3.zero;

            switch (distributeMode)
            {
                case DistributeMode.HorizontalEven:
                    startPos = totalBounds.min.x;
                    endPos = totalBounds.max.x;
                    axis = Vector3.right;
                    break;
                case DistributeMode.VerticalEven:
                    startPos = totalBounds.min.y;
                    endPos = totalBounds.max.y;
                    axis = Vector3.up;
                    break;
                case DistributeMode.DepthEven:
                    startPos = totalBounds.min.z;
                    endPos = totalBounds.max.z;
                    axis = Vector3.forward;
                    break;
                case DistributeMode.CameraHorizontalEven:
                    {
                        Vector3 cameraRight = GetCameraRightVector();
                        axis = cameraRight;
                        // 计算边界在相机轴上的投影
                        Vector3[] corners = new Vector3[8];
                        corners[0] = new Vector3(totalBounds.min.x, totalBounds.min.y, totalBounds.min.z);
                        corners[1] = new Vector3(totalBounds.max.x, totalBounds.min.y, totalBounds.min.z);
                        corners[2] = new Vector3(totalBounds.min.x, totalBounds.max.y, totalBounds.min.z);
                        corners[3] = new Vector3(totalBounds.max.x, totalBounds.max.y, totalBounds.min.z);
                        corners[4] = new Vector3(totalBounds.min.x, totalBounds.min.y, totalBounds.max.z);
                        corners[5] = new Vector3(totalBounds.max.x, totalBounds.min.y, totalBounds.max.z);
                        corners[6] = new Vector3(totalBounds.min.x, totalBounds.max.y, totalBounds.max.z);
                        corners[7] = new Vector3(totalBounds.max.x, totalBounds.max.y, totalBounds.max.z);
                        startPos = float.MaxValue;
                        endPos = float.MinValue;
                        foreach (var corner in corners)
                        {
                            float proj = Vector3.Dot(corner, axis);
                            startPos = Mathf.Min(startPos, proj);
                            endPos = Mathf.Max(endPos, proj);
                        }
                    }
                    break;
                case DistributeMode.CameraVerticalEven:
                    {
                        Vector3 cameraUp = GetCameraUpVector();
                        axis = cameraUp;
                        Vector3[] corners = new Vector3[8];
                        corners[0] = new Vector3(totalBounds.min.x, totalBounds.min.y, totalBounds.min.z);
                        corners[1] = new Vector3(totalBounds.max.x, totalBounds.min.y, totalBounds.min.z);
                        corners[2] = new Vector3(totalBounds.min.x, totalBounds.max.y, totalBounds.min.z);
                        corners[3] = new Vector3(totalBounds.max.x, totalBounds.max.y, totalBounds.min.z);
                        corners[4] = new Vector3(totalBounds.min.x, totalBounds.min.y, totalBounds.max.z);
                        corners[5] = new Vector3(totalBounds.max.x, totalBounds.min.y, totalBounds.max.z);
                        corners[6] = new Vector3(totalBounds.min.x, totalBounds.max.y, totalBounds.max.z);
                        corners[7] = new Vector3(totalBounds.max.x, totalBounds.max.y, totalBounds.max.z);
                        startPos = float.MaxValue;
                        endPos = float.MinValue;
                        foreach (var corner in corners)
                        {
                            float proj = Vector3.Dot(corner, axis);
                            startPos = Mathf.Min(startPos, proj);
                            endPos = Mathf.Max(endPos, proj);
                        }
                    }
                    break;
                case DistributeMode.CameraDepthEven:
                    {
                        Vector3 cameraForward = GetCameraForwardVector();
                        axis = cameraForward;
                        Vector3[] corners = new Vector3[8];
                        corners[0] = new Vector3(totalBounds.min.x, totalBounds.min.y, totalBounds.min.z);
                        corners[1] = new Vector3(totalBounds.max.x, totalBounds.min.y, totalBounds.min.z);
                        corners[2] = new Vector3(totalBounds.min.x, totalBounds.max.y, totalBounds.min.z);
                        corners[3] = new Vector3(totalBounds.max.x, totalBounds.max.y, totalBounds.min.z);
                        corners[4] = new Vector3(totalBounds.min.x, totalBounds.min.y, totalBounds.max.z);
                        corners[5] = new Vector3(totalBounds.max.x, totalBounds.min.y, totalBounds.max.z);
                        corners[6] = new Vector3(totalBounds.min.x, totalBounds.max.y, totalBounds.max.z);
                        corners[7] = new Vector3(totalBounds.max.x, totalBounds.max.y, totalBounds.max.z);
                        startPos = float.MaxValue;
                        endPos = float.MinValue;
                        foreach (var corner in corners)
                        {
                            float proj = Vector3.Dot(corner, axis);
                            startPos = Mathf.Min(startPos, proj);
                            endPos = Mathf.Max(endPos, proj);
                        }
                    }
                    break;
            }

            float totalDistance = endPos - startPos;
            if (totalDistance <= 0 || transforms.Count <= 1) return;

            // 计算所有对象在轴上的投影尺寸
            List<float> objectSizesOnAxis = new List<float>();
            float totalObjectSize = 0f;
            
            foreach (var t in transforms)
            {
                var bounds = GetObjectBounds(t);
                // 计算该对象在指定轴上的投影尺寸
                Vector3[] corners = new Vector3[8];
                corners[0] = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
                corners[1] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
                corners[2] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
                corners[3] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
                corners[4] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
                corners[5] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
                corners[6] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
                corners[7] = new Vector3(bounds.max.x, bounds.max.y, bounds.max.z);
                
                float minProj = float.MaxValue;
                float maxProj = float.MinValue;
                foreach (var corner in corners)
                {
                    float proj = Vector3.Dot(corner, axis);
                    minProj = Mathf.Min(minProj, proj);
                    maxProj = Mathf.Max(maxProj, proj);
                }
                
                float size = maxProj - minProj;
                objectSizesOnAxis.Add(size);
                totalObjectSize += size;
            }

            // 可用空间 = 总距离 - 所有对象尺寸
            float availableSpace = totalDistance - totalObjectSize;
            if (availableSpace < 0) availableSpace = 0;
            
            float spacing = (transforms.Count > 1) ? availableSpace / (transforms.Count - 1) : 0;
            
            // 找到当前所有对象在轴上的投影范围
            float currentMinProj = float.MaxValue;
            float currentMaxProj = float.MinValue;
            foreach (var t in transforms)
            {
                var bounds = GetObjectBounds(t);
                Vector3[] corners = new Vector3[8];
                corners[0] = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
                corners[1] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
                corners[2] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
                corners[3] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
                corners[4] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
                corners[5] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
                corners[6] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
                corners[7] = new Vector3(bounds.max.x, bounds.max.y, bounds.max.z);
                foreach (var corner in corners)
                {
                    float proj = Vector3.Dot(corner, axis);
                    currentMinProj = Mathf.Min(currentMinProj, proj);
                    currentMaxProj = Mathf.Max(currentMaxProj, proj);
                }
            }
            
            // 计算需要的总体偏移（保持对象组的中心位置）
            float currentCenter = (currentMinProj + currentMaxProj) * 0.5f;
            float targetCenter = (startPos + endPos) * 0.5f;
            float globalOffset = targetCenter - currentCenter;
            
            // 在目标范围内分布对象
            float currentPos = startPos;
            for (int i = 0; i < transforms.Count; i++)
            {
                var transform = transforms[i];
                var bounds = GetObjectBounds(transform);
                
                // 计算对象中心应该在的投影位置
                float halfSize = objectSizesOnAxis[i] * 0.5f;
                float targetCenterProj = currentPos + halfSize;
                
                // 计算当前对象中心在轴上的投影
                float currentCenterProj = Vector3.Dot(bounds.center, axis);
                
                // 计算移动距离（目标投影位置 - 当前投影位置）
                float moveDistance = targetCenterProj - currentCenterProj;
                
                // 移动对象
                transform.position += axis * moveDistance;
                
                // 更新下一个对象的起始位置
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

            float startPos = 0f;
            Vector3 axis = Vector3.zero;

            switch (distributeMode)
            {
                case DistributeMode.HorizontalSpacing:
                    startPos = totalBounds.min.x;
                    axis = Vector3.right;
                    break;
                case DistributeMode.VerticalSpacing:
                    startPos = totalBounds.max.y; // 从上往下分布
                    axis = -Vector3.up;
                    break;
                case DistributeMode.DepthSpacing:
                    startPos = totalBounds.min.z;
                    axis = Vector3.forward;
                    break;
                case DistributeMode.CameraHorizontalSpacing:
                    {
                        Vector3 cameraRight = GetCameraRightVector();
                        axis = cameraRight;
                        // 计算边界在相机轴上的投影范围
                        Vector3[] corners = new Vector3[8];
                        corners[0] = new Vector3(totalBounds.min.x, totalBounds.min.y, totalBounds.min.z);
                        corners[1] = new Vector3(totalBounds.max.x, totalBounds.min.y, totalBounds.min.z);
                        corners[2] = new Vector3(totalBounds.min.x, totalBounds.max.y, totalBounds.min.z);
                        corners[3] = new Vector3(totalBounds.max.x, totalBounds.max.y, totalBounds.min.z);
                        corners[4] = new Vector3(totalBounds.min.x, totalBounds.min.y, totalBounds.max.z);
                        corners[5] = new Vector3(totalBounds.max.x, totalBounds.min.y, totalBounds.max.z);
                        corners[6] = new Vector3(totalBounds.min.x, totalBounds.max.y, totalBounds.max.z);
                        corners[7] = new Vector3(totalBounds.max.x, totalBounds.max.y, totalBounds.max.z);
                        startPos = float.MaxValue;
                        foreach (var corner in corners)
                        {
                            startPos = Mathf.Min(startPos, Vector3.Dot(corner, axis));
                        }
                    }
                    break;
                case DistributeMode.CameraVerticalSpacing:
                    {
                        Vector3 cameraUp = GetCameraUpVector();
                        axis = cameraUp;
                        Vector3[] corners = new Vector3[8];
                        corners[0] = new Vector3(totalBounds.min.x, totalBounds.min.y, totalBounds.min.z);
                        corners[1] = new Vector3(totalBounds.max.x, totalBounds.min.y, totalBounds.min.z);
                        corners[2] = new Vector3(totalBounds.min.x, totalBounds.max.y, totalBounds.min.z);
                        corners[3] = new Vector3(totalBounds.max.x, totalBounds.max.y, totalBounds.min.z);
                        corners[4] = new Vector3(totalBounds.min.x, totalBounds.min.y, totalBounds.max.z);
                        corners[5] = new Vector3(totalBounds.max.x, totalBounds.min.y, totalBounds.max.z);
                        corners[6] = new Vector3(totalBounds.min.x, totalBounds.max.y, totalBounds.max.z);
                        corners[7] = new Vector3(totalBounds.max.x, totalBounds.max.y, totalBounds.max.z);
                        startPos = float.MinValue;
                        foreach (var corner in corners)
                        {
                            startPos = Mathf.Max(startPos, Vector3.Dot(corner, axis));
                        }
                        axis = -cameraUp; // 垂直方向从上往下
                    }
                    break;
                case DistributeMode.CameraDepthSpacing:
                    {
                        Vector3 cameraForward = GetCameraForwardVector();
                        axis = cameraForward;
                        Vector3[] corners = new Vector3[8];
                        corners[0] = new Vector3(totalBounds.min.x, totalBounds.min.y, totalBounds.min.z);
                        corners[1] = new Vector3(totalBounds.max.x, totalBounds.min.y, totalBounds.min.z);
                        corners[2] = new Vector3(totalBounds.min.x, totalBounds.max.y, totalBounds.min.z);
                        corners[3] = new Vector3(totalBounds.max.x, totalBounds.max.y, totalBounds.min.z);
                        corners[4] = new Vector3(totalBounds.min.x, totalBounds.min.y, totalBounds.max.z);
                        corners[5] = new Vector3(totalBounds.max.x, totalBounds.min.y, totalBounds.max.z);
                        corners[6] = new Vector3(totalBounds.min.x, totalBounds.max.y, totalBounds.max.z);
                        corners[7] = new Vector3(totalBounds.max.x, totalBounds.max.y, totalBounds.max.z);
                        startPos = float.MaxValue;
                        foreach (var corner in corners)
                        {
                            startPos = Mathf.Min(startPos, Vector3.Dot(corner, axis));
                        }
                    }
                    break;
            }

            // 计算每个对象在指定轴上的投影尺寸
            List<float> objectSizes = new List<float>();
            float totalObjectSize = 0f;

            foreach (var transform in transforms)
            {
                var bounds = GetObjectBounds(transform);
                // 计算边界框在指定轴上的投影长度
                Vector3[] corners = new Vector3[8];
                corners[0] = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
                corners[1] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
                corners[2] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
                corners[3] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
                corners[4] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
                corners[5] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
                corners[6] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
                corners[7] = new Vector3(bounds.max.x, bounds.max.y, bounds.max.z);
                
                float minProj = float.MaxValue;
                float maxProj = float.MinValue;
                foreach (var corner in corners)
                {
                    float proj = Vector3.Dot(corner, axis);
                    minProj = Mathf.Min(minProj, proj);
                    maxProj = Mathf.Max(maxProj, proj);
                }
                
                float size = maxProj - minProj;
                objectSizes.Add(size);
                totalObjectSize += size;
            }

            // 计算间距：直接使用用户设定的间距值
            float spacing = distributionSpacing;

            // 从第一个对象开始分布，基于实际世界位置
            if (transforms.Count == 0) return;
            
            // 获取第一个对象的边界和中心
            var firstBounds = GetObjectBounds(transforms[0]);
            Vector3 firstCenter = firstBounds.center;
            
            // 检查是否为反向轴（垂直向下的情况）
            bool isReverseAxis = (distributeMode == DistributeMode.VerticalSpacing || 
                                  distributeMode == DistributeMode.CameraVerticalSpacing);
            
            // 计算第一个对象在轴上的边缘位置作为起点
            float firstEdgeOffset;
            if (isReverseAxis)
            {
                // 垂直模式：从对象上边缘开始
                firstEdgeOffset = objectSizes[0] * 0.5f;
            }
            else
            {
                // 水平/深度模式：从对象左/前边缘开始
                firstEdgeOffset = -objectSizes[0] * 0.5f;
            }
            
            // 起始参考点
            Vector3 startReference = firstCenter + axis * firstEdgeOffset;
            float accumulatedOffset = 0f;

            for (int i = 0; i < transforms.Count; i++)
            {
                var transform = transforms[i];
                var bounds = GetObjectBounds(transform);
                
                // 计算该对象应该的中心位置偏移
                float centerOffset;
                if (isReverseAxis)
                {
                    // 垂直：从上往下排列
                    centerOffset = -(accumulatedOffset + objectSizes[i] * 0.5f);
                }
                else
                {
                    // 水平/深度：从左往右/从前往后排列  
                    centerOffset = accumulatedOffset + objectSizes[i] * 0.5f;
                }
                
                // 计算目标位置
                Vector3 targetCenter = startReference + axis * centerOffset;
                
                // 移动对象到目标位置
                Vector3 moveOffset = targetCenter - bounds.center;
                transform.position += moveOffset;
                
                // 累计偏移（对象尺寸 + 间距）
                accumulatedOffset += objectSizes[i] + spacing;
            }
        }

        private void SortTransformsByDistributionMode(List<Transform> transforms)
        {
            if (!maintainOrder) return;

            switch (distributeMode)
            {
                case DistributeMode.HorizontalEven:
                case DistributeMode.HorizontalSpacing:
                    transforms.Sort((a, b) => a.position.x.CompareTo(b.position.x));
                    break;
                case DistributeMode.VerticalEven:
                case DistributeMode.VerticalSpacing:
                    transforms.Sort((a, b) => b.position.y.CompareTo(a.position.y));
                    break;
                case DistributeMode.DepthEven:
                case DistributeMode.DepthSpacing:
                    transforms.Sort((a, b) => a.position.z.CompareTo(b.position.z));
                    break;
                case DistributeMode.CameraHorizontalEven:
                case DistributeMode.CameraHorizontalSpacing:
                    {
                        Vector3 cameraRight = GetCameraRightVector();
                        transforms.Sort((a, b) => Vector3.Dot(a.position, cameraRight).CompareTo(Vector3.Dot(b.position, cameraRight)));
                    }
                    break;
                case DistributeMode.CameraVerticalEven:
                case DistributeMode.CameraVerticalSpacing:
                    {
                        Vector3 cameraUp = GetCameraUpVector();
                        transforms.Sort((a, b) => Vector3.Dot(b.position, cameraUp).CompareTo(Vector3.Dot(a.position, cameraUp)));
                    }
                    break;
                case DistributeMode.CameraDepthEven:
                case DistributeMode.CameraDepthSpacing:
                    {
                        Vector3 cameraForward = GetCameraForwardVector();
                        transforms.Sort((a, b) => Vector3.Dot(a.position, cameraForward).CompareTo(Vector3.Dot(b.position, cameraForward)));
                    }
                    break;
            }
        }
        #endregion

        #region 动态间距调整
        [InfoBox("🎚️ 动态间距调整：实时控制对象间距\n" +
                "• 实时预览：拖动滑条即时看到效果\n" +
                "• 自动应用：无需额外确认操作\n" +
                "• 仅间距模式：仅在间距分布模式下工作\n" +
                "• 撤销支持：可通过Ctrl+Z撤销", InfoMessageType.Info)]
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

            // 清除之前的预览
            if (isPreviewing)
                ClearDistributionPreview();

            // 记录撤销操作（仅在值真正改变时）
            Undo.RecordObjects(selectedObjects.Select(obj => obj.transform).ToArray(), "Dynamic Spacing Adjustment");

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
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"动态间距调整出错: {e.Message}");
                // 出错时恢复原始位置
                Undo.PerformUndo();
            }
        }

        private float CalculateOptimalSpacing()
        {
            var selectedObjects = GetValidSelection();
            if (selectedObjects.Length < 2) return 1f;

            // 计算所有对象的平均尺寸
            float totalSize = 0f;
            int axisCount = 0;

            foreach (var obj in selectedObjects)
            {
                var bounds = GetObjectBounds(obj.transform);
                
                switch (distributeMode)
                {
                    case DistributeMode.HorizontalSpacing:
                    case DistributeMode.CameraHorizontalSpacing:
                        totalSize += bounds.size.x;
                        axisCount++;
                        break;
                    case DistributeMode.VerticalSpacing:
                    case DistributeMode.CameraVerticalSpacing:
                        totalSize += bounds.size.y;
                        axisCount++;
                        break;
                    case DistributeMode.DepthSpacing:
                    case DistributeMode.CameraDepthSpacing:
                        totalSize += bounds.size.z;
                        axisCount++;
                        break;
                }
            }

            if (axisCount == 0) return 1f;

            float avgSize = totalSize / axisCount;
            // 返回平均尺寸的20%作为默认间距
            return Mathf.Max(0.1f, avgSize * 0.2f);
        }
        #endregion

        #region 预览功能
        [InfoBox("👁 预览功能：安全查看分布效果\n" +
                "• 临时预览：不影响实际对象\n" +
                "• 实时调整：修改参数即时更新\n" +
                "• 一键应用：确认效果后应用\n" +
                "• 轻松取消：不满意可立即撤销", InfoMessageType.Info)]
        [PropertySpace(5)]
        
        private void PreviewDistributeObjects()
        {
            var selectedObjects = GetValidSelection();
            if (selectedObjects.Length < 2)
            {
                EditorUtility.DisplayDialog("提示", "预览功能需要至少2个对象！", "确定");
                return;
            }

            // 如果已经在预览，先清除
            if (isPreviewing)
                ClearDistributionPreview();

            // 保存原始位置
            originalPositions.Clear();
            foreach (var obj in selectedObjects)
            {
                originalPositions[obj.transform] = IsWorldSpace() ? obj.transform.position : obj.transform.localPosition;
            }

            // 执行预览分布
            var transforms = selectedObjects.Select(obj => obj.transform).ToList();
            SortTransformsByDistributionMode(transforms);
            
            if (reverseOrder)
                transforms.Reverse();

            switch (distributeMode)
            {
                case DistributeMode.HorizontalEven:
                case DistributeMode.VerticalEven:
                case DistributeMode.DepthEven:
                    DistributeEvenly(transforms);
                    break;
                case DistributeMode.HorizontalSpacing:
                case DistributeMode.VerticalSpacing:
                case DistributeMode.DepthSpacing:
                    DistributeWithSpacing(transforms);
                    break;
            }

            isPreviewing = true;
            EditorUtility.DisplayDialog("预览", "✓ 分布预览已启用\n• 修改参数可实时更新\n• 点击'应用预览'确认效果\n• 点击'清除预览'取消", "确定");
        }

        private void ClearDistributionPreview()
        {
            if (!isPreviewing) return;

            // 恢复原始位置
            foreach (var kvp in originalPositions)
            {
                if (kvp.Key != null)
                {
                    if (IsWorldSpace())
                        kvp.Key.position = kvp.Value;
                    else
                        kvp.Key.localPosition = kvp.Value;
                }
            }

            originalPositions.Clear();
            isPreviewing = false;
        }

        private void ApplyDistributionPreview()
        {
            if (!isPreviewing) return;

            var selectedObjects = GetValidSelection();
            Undo.RecordObjects(selectedObjects.Select(obj => obj.transform).ToArray(), "Apply Distribution Preview");

            originalPositions.Clear();
            isPreviewing = false;

            if (showSuccessDialogs)
                EditorUtility.DisplayDialog("成功", "✓ 预览效果已应用！", "确定");
            
            if (selectAfterAlign)
                Selection.objects = selectedObjects;
        }
        #endregion

        #region 匹配功能
        [InfoBox("🔄 匹配功能：统一对象属性\n" +
                "• 尺寸匹配：宽度/高度/深度\n" +
                "• 变换匹配：旋转/缩放\n" +
                "• 智能适配：UI和3D对象\n" +
                "• 至少需要2个对象", InfoMessageType.Info)]
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

            Undo.RecordObjects(selectedObjects.Select(obj => obj.transform).ToArray(), "Match Objects");

            GameObject referenceObject = GetReferenceObject(selectedObjects);
            if (referenceObject == null)
            {
                EditorUtility.DisplayDialog("尺寸匹配错误", "❌ 无法获取参考对象\n\n请检查选择设置和对象状态", "确定");
                return;
            }

            var referenceBounds = GetObjectBounds(referenceObject.transform);
            var referenceTransform = referenceObject.transform;

            foreach (var obj in selectedObjects)
            {
                if (obj == referenceObject) continue;

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

            if (showSuccessDialogs)
                EditorUtility.DisplayDialog("成功", $"✓ 成功匹配 {selectedObjects.Length - 1} 个对象到参考对象！", "确定");
            
            if (selectAfterAlign)
                Selection.objects = selectedObjects;
        }
        #endregion

        #region 边界计算
        [InfoBox("📏 边界计算系统：智能边界检测\n" +
                "• 多模式支持：Renderer/Collider/RectTransform\n" +
                "• 自动检测：智能选择最适合的边界\n" +
                "• 子对象包含：可选计算子对象边界\n" +
                "• 坐标系适配：世界/本地坐标", InfoMessageType.Info)]
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
                var renderer = transform.GetComponent<Renderer>();
                if (renderer != null)
                {
                    bounds = renderer.bounds;
                    
                    if (includeChildren)
                    {
                        var childRenderers = transform.GetComponentsInChildren<Renderer>();
                        foreach (var childRenderer in childRenderers)
                        {
                            if (activeOnly && !childRenderer.gameObject.activeInHierarchy) continue;
                            bounds.Encapsulate(childRenderer.bounds);
                        }
                    }
                    return bounds;
                }
            }

            // Collider
            if (boundsMode == BoundsCalculationMode.Collider || boundsMode == BoundsCalculationMode.Auto)
            {
                var collider = transform.GetComponent<Collider>();
                if (collider != null)
                {
                    bounds = collider.bounds;
                    
                    if (includeChildren)
                    {
                        var childColliders = transform.GetComponentsInChildren<Collider>();
                        foreach (var childCollider in childColliders)
                        {
                            if (activeOnly && !childCollider.gameObject.activeInHierarchy) continue;
                            bounds.Encapsulate(childCollider.bounds);
                        }
                    }
                    return bounds;
                }
            }

            // Transform位置
            var position = IsWorldSpace() ? transform.position : transform.localPosition;
            return new Bounds(position, Vector3.zero);
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
                    return new Bounds(Vector3.zero, Vector3.zero);
            }

            // AllBounds - 计算所有对象的组合边界
            Bounds? combinedBounds = null;
            foreach (var transform in transforms)
            {
                var bounds = GetObjectBounds(transform);
                if (combinedBounds == null)
                    combinedBounds = bounds;
                else
                    combinedBounds.Value.Encapsulate(bounds);
            }
            return combinedBounds ?? new Bounds();
        }

        private Vector3 GetReferencePosition(Transform[] transforms)
        {
            return GetReferenceBounds(transforms).center;
        }

        private GameObject GetReferenceObject(GameObject[] objects)
        {
            switch (matchReference)
            {
                case AlignReference.FirstSelected:
                    return objects[0];
                case AlignReference.LastSelected:
                    return objects[objects.Length - 1];
                case AlignReference.ParentCenter:
                    if (objects[0].transform.parent != null)
                        return objects[0].transform.parent.gameObject;
                    break;
            }
            return objects[0];
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
                return true;
            }).ToArray();

            return validObjects;
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