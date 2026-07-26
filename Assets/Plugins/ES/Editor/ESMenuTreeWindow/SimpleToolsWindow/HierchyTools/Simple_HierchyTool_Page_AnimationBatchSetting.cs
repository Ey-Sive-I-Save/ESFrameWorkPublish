using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor.Animations;


namespace ES
{

    #region 动画器批量设置工具
    [Serializable]
    public class Page_AnimationBatchSetting : ESWindowPageBase
    {
        [Serializable]
        public class SettingsData
        {
            public bool includeChildren;
            public bool addAnimatorIfMissing;
            public RuntimeAnimatorController animatorController;
            public AnimationClip defaultAnimationClip;
            public string animatorControllerGuid;
            public string animatorControllerPath;
            public string defaultAnimationClipGuid;
            public string defaultAnimationClipPath;
            public ControllerNullAction controllerNullAction;
            public ClipNullAction clipNullAction;
            public string assetGroupName;
            public bool enableApplySettings;
            public AnimatorUpdateMode updateMode;
            public AnimatorCullingMode cullingMode;
            public bool applyRootMotion;
            public string newClipName;
        }

        [Serializable]
        public class CreatedAnimationAssetRecord
        {
            [DisplayAsString, LabelText("类型")]
            public string assetType;

            [DisplayAsString, LabelText("路径")]
            public string assetPath;

            [DisplayAsString, LabelText("来源")]
            public string source;
        }

        [Serializable]
        public class AnimatorBatchPreviewRecord
        {
            public bool Enabled = true;
            public GameObject TargetObject;
            public string ObjectPath;
            public bool HasAnimator;
            public string CurrentController;
            public string ControllerStrategy;
            public string ClipStrategy;
            public string Risk;

            public string ToOneLine()
            {
                return $"{ObjectPath} | Animator:{SimpleToolsSafetyUtility.YesNo(HasAnimator)} | Controller:{CurrentController} | {ControllerStrategy} | {ClipStrategy} | {Risk}";
            }
        }
        #region 公共设置
        [Title("动画器批量设置工具", "批量设置Animator属性", bold: true, titleAlignment: TitleAlignments.Centered)]

        [DisplayAsString(fontSize: 13), HideLabel, GUIColor(0.72f, 0.86f, 0.86f)]
        public string readMe = "选择带有Animator的GameObject，\n设置动画属性，\n点击应用按钮批量修改";

        private string PanelSummary
        {
            get
            {
                int selectedCount = Selection.gameObjects != null ? Selection.gameObjects.Length : 0;
                var targets = SimpleToolsSafetyUtility.CollectTargets(Selection.gameObjects, includeChildren);
                int animatorCount = targets.Count(obj => obj != null && obj.GetComponent<Animator>() != null);
                int missingAnimatorCount = targets.Count - animatorCount;
                return $"当前选择: {selectedCount} 个对象 | 实际目标: {targets.Count} 个 | 已有 Animator: {animatorCount} | 可新增: {(addAnimatorIfMissing ? missingAnimatorCount : 0)}";
            }
        }

        [HideInInspector]
        [LabelText("包含子对象"), Space(5)]
        [PropertyTooltip("启用后，批量操作将递归应用到选中对象的子对象。")]
        public bool includeChildren = true;

        [HideInInspector]
        [LabelText("如果没有Animator则添加"), Space(5)]
        [PropertyTooltip("如果对象没有 Animator 组件，自动添加一个。")]
        public bool addAnimatorIfMissing = true;



        [HideInInspector]
        [LabelText("动画控制器"), AssetsOnly, Space(5)]
        [PropertyTooltip("指定要应用的 AnimatorController。如果为空，根据下方选项处理。")]
        public RuntimeAnimatorController animatorController;

        [HideInInspector]
        [LabelText("默认动画剪辑"), AssetsOnly, Space(5)]
        [PropertyTooltip("默认的 AnimationClip，用于创建新的 Controller。")]
        public AnimationClip defaultAnimationClip;

        public enum ControllerNullAction
        {
            [LabelText("忽略")]
            Ignore,
            [LabelText("创建新Controller（共用）")]
            CreateShared,
            [LabelText("创建新Controller（独立）")]
            CreateIndividual
        }

        [HideInInspector]
        [LabelText("Controller为空时"), Space(5)]
        [PropertyTooltip("当 AnimatorController 为空时，选择如何处理：忽略、创建共享或独立的新 Controller。")]
        public ControllerNullAction controllerNullAction = ControllerNullAction.CreateShared;

        public enum ClipNullAction
        {
            [LabelText("忽略")]
            Ignore,
            [LabelText("共享新AnimationClip")]
            CreateShared,
            [LabelText("独立的AnimationClip")]
            CreateIndividual
        }

        [HideInInspector]
        [LabelText("AnimationClip为空时"), Space(5)]
        [PropertyTooltip("当 AnimationClip 为空时，选择如何处理：忽略、创建共享或独立的 Clip。")]
        public ClipNullAction clipNullAction = ClipNullAction.CreateShared;

        [HideInInspector]
        [LabelText("资产分组"), Space(5)]
        [PropertyTooltip("新创建的资产将分组到此文件夹下，避免资源混乱。")]
        public string assetGroupName = "默认";

        [HideInInspector]
        [PropertyTooltip("显示将要应用设置的对象列表（包括添加 Animator 的对象）。")]
        public List<string> previewObjects = new List<string>();

        [HideInInspector]
        [FoldoutGroup("资产创建记录"), ShowInInspector, ReadOnly, LabelText("最近创建"), ListDrawerSettings(DraggableItems = false, ShowPaging = true, NumberOfItemsPerPage = 6)]
        public List<CreatedAnimationAssetRecord> createdAssetRecords = new List<CreatedAnimationAssetRecord>();

        private string lastResultSummary = "";
        private string lastResultDetail = "";
        private string animatorSearch = "";
        private int animatorStatusFilter = 0;
        private int animatorSortIndex = 1;
        private int animatorPageIndex = 0;
        private const int animatorPageSize = 12;
        private readonly List<AnimatorBatchPreviewRecord> animatorPreview = new List<AnimatorBatchPreviewRecord>();
        private static readonly string[] ControllerNullActionLabels = { "忽略", "共享Controller", "独立Controller" };
        private static readonly string[] ClipNullActionLabels = { "忽略", "共享Clip", "独立Clip" };
        private static readonly string[] AnimatorStatusFilterLabels = { "全部", "缺Animator", "空Controller", "会建资产", "高风险" };
        private static readonly string[] AnimatorSortLabels = { "路径", "风险", "Controller", "策略" };

        [OnInspectorGUI, PropertyOrder(100)]
        private void DrawResultPanel()
        {
            DrawAnimatorWorkbench();
        }

        private void DrawAnimatorWorkbench()
        {
            DrawAnimatorHeader();
            DrawAnimatorTargetPanel();
            DrawAnimatorControllerPanel();
            DrawAnimatorClipPanel();
            DrawAnimatorPropertyPanel();
            DrawAnimatorPreviewActions();
            DrawAnimatorInsightPanel();
            DrawAnimatorFilters();
            DrawAnimatorPreviewTable();
            DrawAnimatorAssetRecords();
            DrawAnimatorReportPanel();
        }

        private void DrawAnimatorHeader()
        {
            var targets = GetSelectedTargets();
            int animatorCount = targets.Count(obj => obj != null && obj.GetComponent<Animator>() != null);
            int noAnimator = targets.Count - animatorCount;
            int nullController = targets.Count(obj =>
            {
                var animator = obj != null ? obj.GetComponent<Animator>() : null;
                return animator != null && animator.runtimeAnimatorController == null;
            });

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Animator 批量配置与资产生成台", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("用于批量添加 Animator、配置 Controller、创建 Controller/Clip，并在执行前复核对象、策略和风险。", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMetric("目标对象", targets.Count.ToString());
                    DrawMetric("已有Animator", animatorCount.ToString());
                    DrawMetric("缺Animator", noAnimator.ToString());
                    DrawMetric("空Controller", nullController.ToString());
                    DrawMetric("新建资产", createdAssetRecords.Count.ToString());
                }
            }
        }

        private void DrawAnimatorTargetPanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("目标范围", "从当前 Hierarchy 选中对象收集目标，可递归子对象。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                includeChildren = EditorGUILayout.Toggle("包含子对象", includeChildren);
                addAnimatorIfMissing = EditorGUILayout.Toggle("缺少 Animator 时自动添加", addAnimatorIfMissing);
                DrawInfoRow("当前选择", Selection.gameObjects == null || Selection.gameObjects.Length == 0 ? "未选择对象" : $"{Selection.gameObjects.Length} 个对象");
                DrawInfoRow("实际目标", $"{GetSelectedTargets().Count} 个对象");
            }
        }

        private void DrawAnimatorControllerPanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("Controller 策略", "指定已有 Controller，或在目标缺失时选择创建策略。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                animatorController = (RuntimeAnimatorController)EditorGUILayout.ObjectField("指定 Controller", animatorController, typeof(RuntimeAnimatorController), false);
                controllerNullAction = (ControllerNullAction)EditorGUILayout.Popup("缺失 Controller", (int)controllerNullAction, ControllerNullActionLabels);
                assetGroupName = EditorGUILayout.TextField("资产分组", assetGroupName);
            }
        }

        private void DrawAnimatorClipPanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("Controller 策略", "决定对象没有 Controller 时如何处理。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                defaultAnimationClip = (AnimationClip)EditorGUILayout.ObjectField("默认 Clip", defaultAnimationClip, typeof(AnimationClip), false);
                clipNullAction = (ClipNullAction)EditorGUILayout.Popup("缺失 Clip", (int)clipNullAction, ClipNullActionLabels);
                if (clipNullAction != ClipNullAction.Ignore)
                    newClipName = EditorGUILayout.TextField("新 Clip 名称", newClipName);
            }
        }

        private void DrawAnimatorPropertyPanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("Animator 属性", "仅在启用后批量修改 UpdateMode、CullingMode 和 RootMotion。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                enableApplySettings = EditorGUILayout.Toggle("启用属性批量设置", enableApplySettings);
                GUI.enabled = enableApplySettings;
                updateMode = (AnimatorUpdateMode)EditorGUILayout.EnumPopup("更新模式", updateMode);
                cullingMode = (AnimatorCullingMode)EditorGUILayout.EnumPopup("剔除模式", cullingMode);
                applyRootMotion = EditorGUILayout.Toggle("应用 Root Motion", applyRootMotion);
                GUI.enabled = true;
            }
        }

        private void DrawAnimatorPreviewActions()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("预览与执行", "高风险操作都先生成预览，再按当前选择执行。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("刷新预览", SimpleToolsActionTone.Primary, 32))
                        RefreshAnimatorPreview();
                    if (SimpleToolsPanelUtility.DrawActionButton("应用完整设置", SimpleToolsActionTone.Primary, 32))
                        ApplyAnimatorSettings();
                    if (SimpleToolsPanelUtility.DrawActionButton("创建并应用 Clip", SimpleToolsActionTone.Success, 32))
                        CreateAndApplyAnimationClip();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("添加 Animator", SimpleToolsActionTone.Success, 28))
                        AddAnimatorComponents();
                    if (SimpleToolsPanelUtility.DrawActionButton("替换空 Controller", SimpleToolsActionTone.Warning, 28))
                        ReplaceAnimatorControllers();
                    if (SimpleToolsPanelUtility.DrawActionButton("移除 Animator", SimpleToolsActionTone.Danger, 28))
                        RemoveAnimatorComponents();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawCompactButton("选中筛选结果", 104, 24, EditorStyles.miniButtonLeft))
                        SelectFilteredAnimatorPreview();
                    if (SimpleToolsPanelUtility.DrawCompactButton("导出设置", 76, 24, EditorStyles.miniButtonMid))
                        ExportSettings();
                    if (SimpleToolsPanelUtility.DrawCompactButton("导入设置", 76, 24, EditorStyles.miniButtonMid))
                        ImportSettings();
                    if (SimpleToolsPanelUtility.DrawCompactButton("重置设置", 76, 24, EditorStyles.miniButtonRight))
                        ResetToDefaultSettings();
                }
            }
        }

        private void DrawAnimatorInsightPanel()
        {
            if (animatorPreview.Count == 0)
                return;

            var rows = GetFilteredAnimatorPreview(false);
            SimpleToolsPanelUtility.DrawSectionTitle("预览与执行", "高风险操作都先生成预览，再按当前选择执行。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawInfoRow("Animator", $"已有 {rows.Count(r => r.HasAnimator)} | 缺少 {rows.Count(r => !r.HasAnimator)}");
                DrawInfoRow("Controller", GetControllerCountSummary(rows));
                DrawInfoRow("策略", BuildControllerStrategySummary(rows));
                DrawInfoRow("风险", BuildRiskSummary(rows));
            }
        }

        private void DrawAnimatorFilters()
        {
            if (animatorPreview.Count == 0)
                return;

            SimpleToolsPanelUtility.DrawSectionTitle("结果筛选", "搜索对象路径、当前 Controller、策略和风险。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("搜索", EditorStyles.miniBoldLabel, GUILayout.Width(42));
                    animatorSearch = EditorGUILayout.TextField(animatorSearch);
                    if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(48)))
                        animatorSearch = string.Empty;
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("状态", EditorStyles.miniBoldLabel, GUILayout.Width(34));
                    animatorStatusFilter = EditorGUILayout.Popup("状态", animatorStatusFilter, AnimatorStatusFilterLabels, GUILayout.Width(260));
                    GUILayout.Space(8);
                    EditorGUILayout.LabelField("排序", EditorStyles.miniBoldLabel, GUILayout.Width(34));
                    animatorSortIndex = EditorGUILayout.Popup("排序", animatorSortIndex, AnimatorSortLabels, GUILayout.Width(220));
                }
            }
        }

        private void DrawAnimatorPreviewTable()
        {
            if (animatorPreview.Count == 0)
                return;

            var rows = GetFilteredAnimatorPreview(true);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Animator 预览表  ({rows.Count}/{animatorPreview.Count})", EditorStyles.boldLabel);
                if (rows.Count == 0)
                {
                    EditorGUILayout.LabelField("当前筛选条件下没有对象。", EditorStyles.wordWrappedMiniLabel);
                    return;
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("对象路径", EditorStyles.miniBoldLabel, GUILayout.MinWidth(180));
                    EditorGUILayout.LabelField("Animator", EditorStyles.miniBoldLabel, GUILayout.Width(70));
                    EditorGUILayout.LabelField("当前Controller", EditorStyles.miniBoldLabel, GUILayout.Width(140));
                    EditorGUILayout.LabelField("Controller策略", EditorStyles.miniBoldLabel, GUILayout.Width(128));
                    EditorGUILayout.LabelField("Clip策略", EditorStyles.miniBoldLabel, GUILayout.Width(104));
                    EditorGUILayout.LabelField("风险", EditorStyles.miniBoldLabel, GUILayout.Width(120));
                    GUILayout.Space(48);
                }

                int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)rows.Count / animatorPageSize));
                animatorPageIndex = Mathf.Clamp(animatorPageIndex, 0, totalPages - 1);
                int start = animatorPageIndex * animatorPageSize;
                int end = Mathf.Min(start + animatorPageSize, rows.Count);
                for (int i = start; i < end; i++)
                    DrawAnimatorPreviewRow(rows[i]);
                DrawPager(ref animatorPageIndex, totalPages);
            }
        }

        private void DrawAnimatorPreviewRow(AnimatorBatchPreviewRecord row)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(row.ObjectPath, EditorStyles.miniLabel, GUILayout.MinWidth(180));
                EditorGUILayout.LabelField(row.HasAnimator ? "已有" : "缺少", EditorStyles.miniLabel, GUILayout.Width(70));
                EditorGUILayout.LabelField(row.CurrentController, EditorStyles.miniLabel, GUILayout.Width(140));
                EditorGUILayout.LabelField(row.ControllerStrategy, EditorStyles.miniLabel, GUILayout.Width(128));
                EditorGUILayout.LabelField(row.ClipStrategy, EditorStyles.miniLabel, GUILayout.Width(104));
                EditorGUILayout.LabelField(row.Risk, EditorStyles.miniLabel, GUILayout.Width(120));
                if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(44)))
                {
                    Selection.activeGameObject = row.TargetObject;
                    EditorGUIUtility.PingObject(row.TargetObject);
                }
            }
        }

        private void DrawAnimatorAssetRecords()
        {
            if (createdAssetRecords.Count == 0)
                return;

            SimpleToolsPanelUtility.DrawSectionTitle("资产创建记录", "记录 Controller / Clip 创建路径和来源策略。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                foreach (var record in createdAssetRecords.Take(10))
                    DrawInfoRow(record.assetType, $"{record.assetPath} | {record.source}");
            }
        }

        private void DrawAnimatorReportPanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("资产创建记录", "记录 Controller / Clip 创建路径和来源策略。");
            if (string.IsNullOrWhiteSpace(lastResultSummary) && string.IsNullOrWhiteSpace(lastResultDetail))
            {
                SimpleToolsPanelUtility.DrawEmptyState("还没有执行结果。刷新预览或执行一次操作后，这里会显示报告。");
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(lastResultSummary, EditorStyles.boldLabel);
                if (!string.IsNullOrWhiteSpace(lastResultDetail))
                    SimpleToolsPanelUtility.DrawCompactDetail("动画器处理详情", lastResultDetail);
            }
        }
        #endregion
        #region 辅助方法
        private void RefreshAnimatorPreview()
        {
            animatorPreview.Clear();
            var targets = GetSelectedTargets();
            foreach (var obj in targets.Where(obj => obj != null).Distinct())
                animatorPreview.Add(BuildAnimatorPreviewRecord(obj));

            previewObjects.Clear();
            previewObjects.AddRange(animatorPreview.Select(r => r.ObjectPath));
            animatorPageIndex = 0;
            lastResultSummary = $"Animator 预览完成: {animatorPreview.Count} 个对象";
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(animatorPreview.Select(r => r.ToOneLine()), 16);
        }

        private AnimatorBatchPreviewRecord BuildAnimatorPreviewRecord(GameObject obj)
        {
            var animator = obj.GetComponent<Animator>();
            bool hasAnimator = animator != null;
            bool controllerEmpty = animator == null || animator.runtimeAnimatorController == null;

            return new AnimatorBatchPreviewRecord
            {
                TargetObject = obj,
                ObjectPath = GetObjectPath(obj),
                HasAnimator = hasAnimator,
                CurrentController = animator != null && animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "<空>",
                ControllerStrategy = BuildControllerStrategy(hasAnimator, controllerEmpty),
                ClipStrategy = BuildClipStrategy(controllerEmpty),
                Risk = BuildAnimatorRisk(hasAnimator, controllerEmpty)
            };
        }

        private string BuildControllerStrategy(bool hasAnimator, bool controllerEmpty)
        {
            if (!hasAnimator && !addAnimatorIfMissing)
                return "跳过";
            if (!controllerEmpty)
                return "保留现有";
            if (animatorController != null)
                return "使用指定";
            switch (controllerNullAction)
            {
                case ControllerNullAction.CreateShared: return "创建共享";
                case ControllerNullAction.CreateIndividual: return "创建独立";
                default: return "忽略空Controller";
            }
        }

        private string BuildClipStrategy(bool controllerEmpty)
        {
            if (!controllerEmpty)
                return "不处理";
            if (animatorController != null)
                return defaultAnimationClip != null ? "已有Clip参考" : "不创建";
            switch (clipNullAction)
            {
                case ClipNullAction.CreateShared: return defaultAnimationClip != null ? "使用默认Clip" : "创建共享Clip";
                case ClipNullAction.CreateIndividual: return defaultAnimationClip != null ? "复用默认Clip" : "创建独立Clip";
                default: return "忽略Clip";
            }
        }

        private string BuildAnimatorRisk(bool hasAnimator, bool controllerEmpty)
        {
            var risks = new List<string>();
            if (!hasAnimator && addAnimatorIfMissing) risks.Add("新增组件");
            if (!hasAnimator && !addAnimatorIfMissing) risks.Add("跳过");
            if (controllerEmpty && animatorController == null && controllerNullAction != ControllerNullAction.Ignore) risks.Add("创建资产");
            if (clipNullAction == ClipNullAction.CreateIndividual && controllerEmpty) risks.Add("多Clip");
            if (enableApplySettings) risks.Add("改属性");
            return risks.Count == 0 ? "低" : string.Join("/", risks);
        }

        private List<AnimatorBatchPreviewRecord> GetFilteredAnimatorPreview(bool sorted)
        {
            IEnumerable<AnimatorBatchPreviewRecord> rows = animatorPreview.Where(PassesAnimatorFilter);
            if (sorted)
                rows = SortAnimatorPreview(rows);
            return rows.ToList();
        }

        private bool PassesAnimatorFilter(AnimatorBatchPreviewRecord row)
        {
            if (row == null)
                return false;

            switch (animatorStatusFilter)
            {
                case 1:
                    if (row.HasAnimator) return false;
                    break;
                case 2:
                    if (row.CurrentController != "<空>") return false;
                    break;
                case 3:
                    if (!row.Risk.Contains("创建资产")) return false;
                    break;
                case 4:
                    if (row.Risk == "低") return false;
                    break;
            }

            if (string.IsNullOrWhiteSpace(animatorSearch))
                return true;

            string keyword = animatorSearch.Trim();
            return ContainsIgnoreCase(row.ObjectPath, keyword) ||
                   ContainsIgnoreCase(row.CurrentController, keyword) ||
                   ContainsIgnoreCase(row.ControllerStrategy, keyword) ||
                   ContainsIgnoreCase(row.ClipStrategy, keyword) ||
                   ContainsIgnoreCase(row.Risk, keyword);
        }

        private IEnumerable<AnimatorBatchPreviewRecord> SortAnimatorPreview(IEnumerable<AnimatorBatchPreviewRecord> rows)
        {
            switch (animatorSortIndex)
            {
                case 1:
                    return rows.OrderByDescending(GetAnimatorRiskScore).ThenBy(r => r.ObjectPath, StringComparer.Ordinal);
                case 2:
                    return rows.OrderBy(r => r.CurrentController, StringComparer.Ordinal).ThenBy(r => r.ObjectPath, StringComparer.Ordinal);
                case 3:
                    return rows.OrderBy(r => r.ControllerStrategy, StringComparer.Ordinal).ThenBy(r => r.ObjectPath, StringComparer.Ordinal);
                default:
                    return rows.OrderBy(r => r.ObjectPath, StringComparer.Ordinal);
            }
        }

        private int GetAnimatorRiskScore(AnimatorBatchPreviewRecord row)
        {
            int score = 0;
            if (row == null) return score;
            if (row.Risk.Contains("创建资产")) score += 40;
            if (row.Risk.Contains("新增组件")) score += 30;
            if (row.Risk.Contains("多Clip")) score += 20;
            if (row.Risk.Contains("改属性")) score += 10;
            if (row.Risk.Contains("跳过")) score += 5;
            return score;
        }

        private void SelectFilteredAnimatorPreview()
        {
            var objects = GetFilteredAnimatorPreview(true)
                .Select(r => r.TargetObject)
                .Where(obj => obj != null)
                .Cast<UnityEngine.Object>()
                .ToArray();

            if (objects.Length == 0)
            {
                lastResultSummary = "选中失败";
                lastResultDetail = "当前筛选条件下没有对象。";
                return;
            }

            Selection.objects = objects;
            EditorGUIUtility.PingObject(objects[0]);
            lastResultSummary = $"已选中筛选结果: {objects.Length} 个";
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(objects.OfType<GameObject>().Select(GetObjectPath), 12);
        }

        private string BuildControllerStrategySummary(IEnumerable<AnimatorBatchPreviewRecord> rows)
        {
            return string.Join("  |  ", rows.GroupBy(r => r.ControllerStrategy).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}"));
        }

        private string GetControllerCountSummary(IEnumerable<AnimatorBatchPreviewRecord> rows)
        {
            int assignedCount = rows.Count(r => r.CurrentController != "<空>");
            int emptyCount = rows.Count(r => r.CurrentController == "<空>");
            return $"已有 {assignedCount} | 空 {emptyCount}";
        }

        private string BuildRiskSummary(IEnumerable<AnimatorBatchPreviewRecord> rows)
        {
            return string.Join("  |  ", rows.GroupBy(r => r.Risk).OrderByDescending(g => g.Count()).Take(6).Select(g => $"{g.Key} {g.Count()}"));
        }

        private string GetObjectPath(GameObject obj)
        {
            if (obj == null) return "<丢失对象>";
            var stack = new Stack<string>();
            var current = obj.transform;
            while (current != null)
            {
                stack.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", stack);
        }

        private bool ContainsIgnoreCase(string source, string keyword)
        {
            return !string.IsNullOrEmpty(source) && source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawMetric(string label, string value)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(72)))
            {
                EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(value) ? "-" : value, EditorStyles.boldLabel);
            }
        }

        private void DrawInfoRow(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Width(72));
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(value) ? "-" : value, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawPager(ref int pageIndex, int totalPages)
        {
            if (totalPages <= 1) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("上一页", EditorStyles.miniButtonLeft, GUILayout.Width(64)) && pageIndex > 0)
                    pageIndex--;
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"第 {pageIndex + 1} / {totalPages} 页", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(90));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("下一页", EditorStyles.miniButtonRight, GUILayout.Width(64)) && pageIndex < totalPages - 1)
                    pageIndex++;
            }
        }

        private string GetAnimationAssetFolder(string subFolder)
        {
            string root = ESGlobalEditorDefaultConfi.Instance?.Path_ResourceParent;
            if (string.IsNullOrWhiteSpace(root) || !SimpleToolsSafetyUtility.IsAssetPath(root))
                root = "Assets";

            string group = SanitizeAssetName(string.IsNullOrWhiteSpace(assetGroupName) ? "默认" : assetGroupName);
            return $"{SimpleToolsSafetyUtility.NormalizeAssetPath(root)}/{subFolder}/{group}";
        }

        private string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "NewAsset";

            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');

            return value.Trim();
        }

        private AnimatorController CreateAnimatorControllerAsset(string baseName, string source)
        {
            string folder = GetAnimationAssetFolder("AnimationControllers");
            if (!SimpleToolsSafetyUtility.EnsureAssetFolder(folder, out var error))
            {
                EditorUtility.DisplayDialog("创建失败", error, "知道了");
                return null;
            }

            string path = SimpleToolsSafetyUtility.GetUniqueAssetPath($"{folder}/{SanitizeAssetName(baseName)}.controller");
            var controller = new AnimatorController();
            AssetDatabase.CreateAsset(controller, path);
            controller.name = Path.GetFileNameWithoutExtension(path);
            EnsureControllerHasBaseLayer(controller);
            AssetDatabase.SaveAssets();
            RecordCreatedAsset("AnimatorController", path, source);
            return controller;
        }

        private AnimationClip CreateAnimationClipAsset(string baseName, string source)
        {
            string folder = GetAnimationAssetFolder("Animations");
            if (!SimpleToolsSafetyUtility.EnsureAssetFolder(folder, out var error))
            {
                EditorUtility.DisplayDialog("创建失败", error, "知道了");
                return null;
            }

            string path = SimpleToolsSafetyUtility.GetUniqueAssetPath($"{folder}/{SanitizeAssetName(baseName)}.anim");
            var clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
            clip.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.SaveAssets();
            RecordCreatedAsset("AnimationClip", path, source);
            return clip;
        }

        private void EnsureControllerHasBaseLayer(AnimatorController controller)
        {
            if (controller != null && controller.layers.Length == 0)
                controller.AddLayer("Base Layer");
        }

        private void RecordCreatedAsset(string type, string path, string source)
        {
            createdAssetRecords.Add(new CreatedAnimationAssetRecord
            {
                assetType = type,
                assetPath = path,
                source = source
            });
        }

        private List<GameObject> GetSelectedTargets()
        {
            return SimpleToolsSafetyUtility.CollectTargets(Selection.gameObjects, includeChildren);
        }

        private bool ConfirmTargetOperation(string title, string action, List<GameObject> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                EditorUtility.DisplayDialog("没有可处理对象", "当前选区下没有可处理的 GameObject。", "知道了");
                return false;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(targets.Select(obj => obj != null ? obj.name : "<丢失对象>"), 10);
            return EditorUtility.DisplayDialog(title,
                $"将{action} {targets.Count} 个对象。\n\n{preview}\n\n支持 Ctrl+Z 撤销。继续吗？",
                "开始处理", "取消");
        }
        #endregion

        #region 应用Animator设置
        [HideInInspector]
        [ToggleLeft, LabelText("启用"), LabelWidth(120)]
        public bool enableApplySettings = false;

        [HideInInspector]
        public AnimatorUpdateMode updateMode = AnimatorUpdateMode.Normal;

        [HideInInspector]
        public AnimatorCullingMode cullingMode = AnimatorCullingMode.AlwaysAnimate;

        [HideInInspector]
        public bool applyRootMotion = false;



        [HideInInspector]
        public string newClipName = "NewAnimation";

        public void ApplyAnimatorSettings()
        {
            createdAssetRecords.Clear();

            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }
            var allObjects = SimpleToolsSafetyUtility.CollectTargets(selectedObjects, includeChildren);
            var affectedTargets = allObjects
                .Where(obj => obj != null && (obj.GetComponent<Animator>() != null || addAnimatorIfMissing))
                .ToList();

            // 填充预览列表
            previewObjects.Clear();
            foreach (var obj in affectedTargets)
            {
                previewObjects.Add(obj.name);
            }

            if (!ConfirmTargetOperation("确认应用Animator设置", "应用 Animator 设置到", affectedTargets))
                return;

            RuntimeAnimatorController sharedController = null;
            AnimationClip sharedClip = null;

            // 检查是否需要创建sharedController
            bool needSharedController = false;
            foreach (var obj in affectedTargets)
            {
                var animator = obj.GetComponent<Animator>();
                if ((animator == null && addAnimatorIfMissing) ||
                    (animator != null && animator.runtimeAnimatorController == null))
                {
                    needSharedController = true;
                    break;
                }
            }

            if (animatorController == null && controllerNullAction == ControllerNullAction.CreateShared && needSharedController)
            {
                var controller = CreateAnimatorControllerAsset("NewAnimatorController", "应用Animator设置-共享Controller");
                if (controller == null)
                    return;

                sharedController = controller;

                // 根据 clipNullAction 创建剪辑
                if (clipNullAction == ClipNullAction.CreateShared)
                {
                    if (defaultAnimationClip != null)
                    {
                        sharedClip = defaultAnimationClip;
                    }
                    else
                    {
                        sharedClip = CreateAnimationClipAsset("SharedAnimationClip", "应用Animator设置-共享Clip");
                        if (sharedClip == null)
                            return;
                    }

                    var rootStateMachine = (sharedController as AnimatorController).layers[0].stateMachine;
                    var defaultState = rootStateMachine.AddState(sharedClip.name);
                    defaultState.motion = sharedClip;
                    AssetDatabase.SaveAssets();
                }
                else if (clipNullAction == ClipNullAction.Ignore)
                {
                    // 不创建剪辑。
                }
            }

            if (animatorController == null &&
                controllerNullAction == ControllerNullAction.CreateIndividual &&
                clipNullAction == ClipNullAction.CreateShared)
            {
                sharedClip = defaultAnimationClip != null
                    ? defaultAnimationClip
                    : CreateAnimationClipAsset("SharedAnimationClip", "应用Animator设置-独立Controller共享Clip");
                if (sharedClip == null)
                    return;
            }

            int modifiedCount = 0;
            bool cancelled = false;
            var changedTargets = new List<GameObject>();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("批量应用Animator设置");
            try
            {
                for (int i = 0; i < affectedTargets.Count; i++)
                {
                    var obj = affectedTargets[i];
                    float progress = (float)i / affectedTargets.Count;
                    if (EditorUtility.DisplayCancelableProgressBar("应用Animator设置", $"正在处理 {obj.name}...", progress))
                    {
                        cancelled = true;
                        break;
                    }

                    var animator = obj.GetComponent<Animator>();
                    bool changed = false;
                    if (addAnimatorIfMissing && animator == null)
                    {
                        animator = Undo.AddComponent<Animator>(obj);
                        changed = true;
                    }
                    if (animator != null)
                    {
                        Undo.RecordObject(animator, "Modify Animator");

                        // 应用settings
                        if (enableApplySettings)
                        {
                            if (animator.updateMode != updateMode)
                            {
                                animator.updateMode = updateMode;
                                changed = true;
                            }
                            if (animator.cullingMode != cullingMode)
                            {
                                animator.cullingMode = cullingMode;
                                changed = true;
                            }
                            if (animator.applyRootMotion != applyRootMotion)
                            {
                                animator.applyRootMotion = applyRootMotion;
                                changed = true;
                            }
                        }

                        // 如果Controller为null，则设置
                        if (animator.runtimeAnimatorController == null)
                        {
                            RuntimeAnimatorController controllerToUse = animatorController;
                            if (controllerToUse == null)
                            {
                                if (controllerNullAction == ControllerNullAction.CreateShared)
                                {
                                    controllerToUse = sharedController;
                                }
                                else if (controllerNullAction == ControllerNullAction.CreateIndividual)
                                {
                                    var controller = CreateAnimatorControllerAsset($"NewAnimatorController_{obj.name}", $"应用Animator设置-独立Controller:{obj.name}");
                                    if (controller == null)
                                        continue;

                                    controllerToUse = controller;

                                    // 根据 clipNullAction 创建剪辑
                                    if (clipNullAction == ClipNullAction.CreateIndividual)
                                    {
                                        AnimationClip clipToAdd;
                                        if (defaultAnimationClip != null)
                                        {
                                            clipToAdd = defaultAnimationClip;
                                        }
                                        else
                                        {
                                            clipToAdd = CreateAnimationClipAsset($"AnimationClip_{obj.name}", $"应用Animator设置-独立Clip:{obj.name}");
                                            if (clipToAdd == null)
                                                continue;
                                        }

                                        var rootStateMachine = (controllerToUse as AnimatorController).layers[0].stateMachine;
                                        var defaultState = rootStateMachine.AddState(clipToAdd.name);
                                        defaultState.motion = clipToAdd;
                                        AssetDatabase.SaveAssets();
                                    }
                                    else if (clipNullAction == ClipNullAction.CreateShared && sharedClip != null)
                                    {
                                        var rootStateMachine = (controllerToUse as AnimatorController).layers[0].stateMachine;
                                        var defaultState = rootStateMachine.AddState(sharedClip.name);
                                        defaultState.motion = sharedClip;
                                        AssetDatabase.SaveAssets();
                                    }
                                    else if (clipNullAction == ClipNullAction.Ignore)
                                    {
                                        // 不创建剪辑。
                                    }
                                }
                            }

                            if (controllerToUse != null && animator.runtimeAnimatorController != controllerToUse)
                            {
                                animator.runtimeAnimatorController = controllerToUse;
                                changed = true;
                            }
                        }

                        if (changed)
                        {
                            EditorUtility.SetDirty(animator);
                            changedTargets.Add(obj);
                            modifiedCount++;
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroup);
            }

            MarkScenesDirty(changedTargets);
            string completionState = cancelled ? "已取消" : "完成";
            lastResultSummary = $"Animator 设置{completionState}: 修改 {modifiedCount} 个 | 目标 {affectedTargets.Count} 个 | 新建资产 {createdAssetRecords.Count} 个";
            lastResultDetail = BuildAnimatorResultDetail(changedTargets);
            if (cancelled)
                lastResultDetail = "操作由用户取消；已修改的场景对象可通过 Ctrl+Z 撤销。已创建的资产请通过版本控制或手动删除回退。\n\n" + lastResultDetail;
            EditorUtility.DisplayDialog(
                cancelled ? "Animator 设置已取消" : "Animator 设置完成",
                cancelled
                    ? $"已在取消前修改 {modifiedCount} 个 Animator 组件"
                    : $"成功修改 {modifiedCount} 个 Animator 组件",
                "确定");
        }

        public void AddAnimatorComponents()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var allObjects = GetSelectedTargets();
            var targets = allObjects.Where(obj => obj != null && obj.GetComponent<Animator>() == null).ToList();
            if (!ConfirmTargetOperation("确认批量添加Animator", "添加 Animator 到", targets))
                return;

            int addedCount = 0;
            bool cancelled = false;
            var changedTargets = new List<GameObject>();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Batch Add Animator Components");
            try
            {
                for (int index = 0; index < targets.Count; index++)
                {
                    var obj = targets[index];
                    if (EditorUtility.DisplayCancelableProgressBar("添加 Animator", obj != null ? obj.name : "Missing Object", (float)index / targets.Count))
                    {
                        cancelled = true;
                        break;
                    }

                    if (obj == null || obj.GetComponent<Animator>() != null)
                        continue;

                    var animator = Undo.AddComponent<Animator>(obj);
                    if (animatorController != null)
                        animator.runtimeAnimatorController = animatorController;

                    EditorUtility.SetDirty(animator);
                    changedTargets.Add(obj);
                    addedCount++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroup);
            }

            MarkScenesDirty(changedTargets);
            string completionState = cancelled ? "cancelled" : "completed";
            lastResultSummary = $"Add Animator {completionState}: added {addedCount}, targets {targets.Count}.";
            lastResultDetail = BuildAnimatorResultDetail(changedTargets);
            if (cancelled)
                lastResultDetail = "操作由用户取消；已添加的场景组件可通过 Ctrl+Z 撤销。\n\n" + lastResultDetail;
            EditorUtility.DisplayDialog(cancelled ? "Add Animator cancelled" : "Add Animator complete", cancelled ? $"Added {addedCount} Animator components before cancellation." : $"Added {addedCount} Animator components.", "OK");
        }

        public void RemoveAnimatorComponents()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", "Select one or more GameObjects first.", "OK");
                return;
            }

            var allObjects = SimpleToolsSafetyUtility.CollectTargets(selectedObjects, includeChildren);
            var targets = allObjects.Where(obj => obj != null && obj.GetComponent<Animator>() != null).ToList();
            if (!ConfirmTargetOperation("Confirm batch Animator removal", "Remove Animator", targets))
                return;

            int removedCount = 0;
            bool cancelled = false;
            var changedTargets = new List<GameObject>();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Batch Remove Animator Components");
            try
            {
                for (int index = 0; index < targets.Count; index++)
                {
                    var obj = targets[index];
                    if (EditorUtility.DisplayCancelableProgressBar("移除 Animator", obj != null ? obj.name : "Missing Object", (float)index / targets.Count))
                    {
                        cancelled = true;
                        break;
                    }

                    var animator = obj != null ? obj.GetComponent<Animator>() : null;
                    if (animator == null)
                        continue;

                    Undo.DestroyObjectImmediate(animator);
                    changedTargets.Add(obj);
                    removedCount++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroup);
            }

            MarkScenesDirty(changedTargets);
            string completionState = cancelled ? "已取消" : "完成";
            lastResultSummary = $"移除 Animator {completionState}: 移除 {removedCount} 个 | 目标 {targets.Count} 个";
            lastResultDetail = BuildAnimatorResultDetail(changedTargets);
            if (cancelled)
                lastResultDetail = "操作由用户取消；已移除的场景组件可通过 Ctrl+Z 撤销。\n\n" + lastResultDetail;
            EditorUtility.DisplayDialog(cancelled ? "移除 Animator 已取消" : "移除 Animator 完成", cancelled ? $"取消前已移除 {removedCount} 个 Animator 组件。" : $"成功移除 {removedCount} 个 Animator 组件。", "确定");
        }

        private void MarkScenesDirty(IEnumerable<GameObject> targets)
        {
            if (targets == null)
                return;

            foreach (var scene in targets
                .Where(obj => obj != null && obj.scene.IsValid())
                .Select(obj => obj.scene)
                .Distinct())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        public void ReplaceAnimatorControllers()
        {
            createdAssetRecords.Clear();
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var allObjects = SimpleToolsSafetyUtility.CollectTargets(selectedObjects, includeChildren);
            var targets = allObjects
                .Where(obj => obj != null)
                .Where(obj =>
                {
                    var animator = obj.GetComponent<Animator>();
                    return animator != null && animator.runtimeAnimatorController == null;
                })
                .ToList();

            RuntimeAnimatorController controllerToUse = null;
            if (animatorController != null)
            {
                controllerToUse = animatorController;
            }
            bool willCreateController = animatorController == null && controllerNullAction == ControllerNullAction.CreateShared;

            int replacedCount = 0;
            if (controllerToUse == null && !willCreateController)
            {
                EditorUtility.DisplayDialog("没有可用Controller", "当前没有指定 Controller，且没有创建新的 Controller。", "知道了");
                return;
            }

            if (!ConfirmTargetOperation("确认替换AnimatorController", "设置 Controller 到", targets))
                return;

            if (willCreateController)
            {
                var controller = CreateAnimatorControllerAsset("NewAnimatorController", "替换AnimatorController-共享Controller");
                if (controller == null)
                    return;

                controllerToUse = controller;
            }

            bool cancelled = false;
            var changedTargets = new List<GameObject>();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Batch Replace Animator Controllers");
            try
            {
                for (int index = 0; index < targets.Count; index++)
                {
                    var obj = targets[index];
                    if (EditorUtility.DisplayCancelableProgressBar("替换 Animator Controller", obj != null ? obj.name : "Missing Object", (float)index / targets.Count))
                    {
                        cancelled = true;
                        break;
                    }

                    var animator = obj != null ? obj.GetComponent<Animator>() : null;
                    if (animator == null || animator.runtimeAnimatorController != null || controllerToUse == null)
                        continue;

                    Undo.RecordObject(animator, "Replace Animator Controller");
                    animator.runtimeAnimatorController = controllerToUse;
                    EditorUtility.SetDirty(animator);
                    changedTargets.Add(obj);
                    replacedCount++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroup);
            }

            MarkScenesDirty(changedTargets);
            string completionState = cancelled ? "已取消" : "完成";
            lastResultSummary = $"替换 Controller {completionState}: 替换 {replacedCount} 个 | 目标 {targets.Count} 个 | 新建资产 {createdAssetRecords.Count} 个";
            lastResultDetail = BuildAnimatorResultDetail(changedTargets);
            if (cancelled)
                lastResultDetail = "操作由用户取消；已修改的场景组件可通过 Ctrl+Z 撤销。新建资产请通过版本控制或手动删除回退。\n\n" + lastResultDetail;
            EditorUtility.DisplayDialog(cancelled ? "替换 Controller 已取消" : "替换 Controller 完成", cancelled ? $"取消前已替换 {replacedCount} 个空 Animator Controller。" : $"成功替换 {replacedCount} 个空 Animator Controller。", "确定");
        }

        public void ResetToDefaultSettings()
        {
            includeChildren = true;
            addAnimatorIfMissing = true;
            animatorController = null;
            defaultAnimationClip = null;
            controllerNullAction = ControllerNullAction.CreateShared;
            clipNullAction = ClipNullAction.CreateShared;
            enableApplySettings = false;
            updateMode = AnimatorUpdateMode.Normal;
            cullingMode = AnimatorCullingMode.AlwaysAnimate;
            applyRootMotion = false;
            newClipName = "NewAnimation";
            assetGroupName = "默认";
        }

        public void ExportSettings()
        {
            string path = EditorUtility.SaveFilePanel("导出设置", "", "AnimationBatchSettings.json", "json");
            if (!string.IsNullOrEmpty(path))
            {
                var settings = new SettingsData
                {
                    includeChildren = this.includeChildren,
                    addAnimatorIfMissing = this.addAnimatorIfMissing,
                    animatorController = this.animatorController,
                    defaultAnimationClip = this.defaultAnimationClip,
                    animatorControllerPath = AssetDatabase.GetAssetPath(this.animatorController),
                    animatorControllerGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(this.animatorController)),
                    defaultAnimationClipPath = AssetDatabase.GetAssetPath(this.defaultAnimationClip),
                    defaultAnimationClipGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(this.defaultAnimationClip)),
                    controllerNullAction = this.controllerNullAction,
                    clipNullAction = this.clipNullAction,
                    assetGroupName = this.assetGroupName,
                    enableApplySettings = this.enableApplySettings,
                    updateMode = this.updateMode,
                    cullingMode = this.cullingMode,
                    applyRootMotion = this.applyRootMotion,
                    newClipName = this.newClipName
                };
                try
                {
                    string json = JsonUtility.ToJson(settings, true);
                    File.WriteAllText(path, json, Encoding.UTF8);
                    lastResultSummary = "Animator 设置导出完成";
                    lastResultDetail = path;
                    EditorUtility.DisplayDialog("成功", "设置已导出！", "确定");
                }
                catch (Exception ex)
                {
                    lastResultSummary = "Animator 设置导出失败";
                    lastResultDetail = ex.Message;
                    EditorUtility.DisplayDialog("导出失败", $"无法写入 Animator 设置：\n{ex.Message}", "知道了");
                }
            }
        }

        public void ImportSettings()
        {
            string path = EditorUtility.OpenFilePanel("导入设置", "", "json");
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    var settings = JsonUtility.FromJson<SettingsData>(json);
                    if (settings == null)
                    {
                        EditorUtility.DisplayDialog("Import failed", "The settings file is empty or invalid.", "OK");
                        return;
                    }

                    includeChildren = settings.includeChildren;
                    addAnimatorIfMissing = settings.addAnimatorIfMissing;
                    animatorController = LoadAssetFromGuidOrPath<RuntimeAnimatorController>(settings.animatorControllerGuid, settings.animatorControllerPath) ?? settings.animatorController;
                    defaultAnimationClip = LoadAssetFromGuidOrPath<AnimationClip>(settings.defaultAnimationClipGuid, settings.defaultAnimationClipPath) ?? settings.defaultAnimationClip;
                    controllerNullAction = settings.controllerNullAction;
                    clipNullAction = settings.clipNullAction;
                    assetGroupName = settings.assetGroupName;
                    enableApplySettings = settings.enableApplySettings;
                    updateMode = settings.updateMode;
                    cullingMode = settings.cullingMode;
                    applyRootMotion = settings.applyRootMotion;
                    newClipName = settings.newClipName;
                    lastResultSummary = "Animator 设置导入完成";
                    lastResultDetail = path;
                    EditorUtility.DisplayDialog("成功", "设置已导入！", "确定");
                }
                catch (Exception ex)
                {
                    lastResultSummary = "Animator 设置导入失败";
                    lastResultDetail = ex.Message;
                    EditorUtility.DisplayDialog("导入失败", $"无法读取 Animator 设置：\n{ex.Message}", "知道了");
                }
            }
        }

        private T LoadAssetFromGuidOrPath<T>(string guid, string path) where T : UnityEngine.Object
        {
            if (!string.IsNullOrWhiteSpace(guid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(guidPath))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<T>(guidPath);
                    if (asset != null)
                        return asset;
                }
            }

            if (!string.IsNullOrWhiteSpace(path))
                return AssetDatabase.LoadAssetAtPath<T>(path);

            return null;
        }
        #endregion

        #region 创建AnimationClip
        public void CreateAndApplyAnimationClip()
        {
            createdAssetRecords.Clear();
            var selectedObjects = Selection.gameObjects;
            List<GameObject> allObjects = SimpleToolsSafetyUtility.CollectTargets(selectedObjects, includeChildren);
            if (selectedObjects != null && selectedObjects.Length > 0 &&
                !ConfirmTargetOperation("确认创建并应用AnimationClip", "应用 AnimationClip/Controller 到", allObjects))
                return;

            // 确保Controller存在
            if (animatorController == null)
            {
                var controller_ = CreateAnimatorControllerAsset("NewAnimatorController", "创建并应用Clip-自动Controller");
                if (controller_ == null)
                    return;

                animatorController = controller_;
            }

            // 获取所有对象。
            if (clipNullAction == ClipNullAction.CreateIndividual)
            {
                // 为每个对象创建独立的clip
                foreach (var obj in allObjects)
                {
                    var clip = CreateAnimationClipAsset($"{newClipName}_{obj.name}", $"创建并应用Clip-独立:{obj.name}");
                    if (clip == null)
                        continue;

                    // 添加到controller
                    var controller = animatorController as AnimatorController;
                    if (controller != null)
                    {
                        var rootStateMachine = controller.layers[0].stateMachine;
                        var state = rootStateMachine.AddState(clip.name);
                        state.motion = clip;
                    }
                }
                AssetDatabase.SaveAssets();
            }

            AnimationClip clipToUse = defaultAnimationClip;
            if (clipToUse == null && clipNullAction != ClipNullAction.CreateIndividual)
            {
                if (string.IsNullOrEmpty(newClipName))
                {
                    EditorUtility.DisplayDialog("错误", "请输入新Clip名称！", "确定");
                    return;
                }

                if (clipNullAction == ClipNullAction.CreateShared)
                {
                    clipToUse = CreateAnimationClipAsset(newClipName, "创建并应用Clip-共享");
                }
                else
                {
                    // 默认情况，假设CreateShared
                    clipToUse = CreateAnimationClipAsset(newClipName, "创建并应用Clip-默认共享");
                }

                if (clipToUse == null)
                    return;
            }

            // 添加到Controller（仅对非CreateIndividual的情况）
            if (clipToUse != null && clipNullAction != ClipNullAction.CreateIndividual)
            {
                var controller = animatorController as AnimatorController;
                if (controller != null)
                {
                    var rootStateMachine = controller.layers[0].stateMachine;
                    var state = rootStateMachine.AddState(clipToUse.name);
                    state.motion = clipToUse;
                    AssetDatabase.SaveAssets();
                }
            }

            // 应用到选中对象
            if (selectedObjects != null && selectedObjects.Length > 0)
            {
                int appliedCount = 0;
                foreach (var obj in allObjects)
                {
                    var animator = obj.GetComponent<Animator>();
                    if (addAnimatorIfMissing && animator == null)
                    {
                        animator = Undo.AddComponent<Animator>(obj);
                    }
                    if (animator != null)
                    {
                        Undo.RecordObject(animator, "Set Animator Controller");
                        animator.runtimeAnimatorController = animatorController;
                        EditorUtility.SetDirty(animator);
                        appliedCount++;
                    }
                }

                MarkScenesDirty(allObjects);
                lastResultSummary = $"AnimationClip 创建并应用完成: 应用 {appliedCount} 个对象 | 新建资产 {createdAssetRecords.Count} 个";
                lastResultDetail = BuildAnimatorResultDetail(allObjects);
                EditorUtility.DisplayDialog("成功", $"AnimationClip 已创建并应用到 {appliedCount} 个对象！", "确定");
            }
            else
            {
                lastResultSummary = $"AnimationClip 已应用到 Controller | 新建资产 {createdAssetRecords.Count} 个";
                lastResultDetail = BuildAnimatorResultDetail(null);
                EditorUtility.DisplayDialog("成功", $"AnimationClip 已应用到Controller！", "确定");
            }
        }

        private string BuildAnimatorResultDetail(IEnumerable<GameObject> targets)
        {
            var sections = new List<string>();
            if (targets != null)
                sections.Add("对象:\n" + SimpleToolsSafetyUtility.JoinPreview(targets.Select(obj => obj != null ? obj.name : null), 12));

            if (createdAssetRecords.Count > 0)
                sections.Add("新建资产:\n" + SimpleToolsSafetyUtility.JoinPreview(createdAssetRecords.Select(record => $"{record.assetType}: {record.assetPath}"), 12));

            return sections.Count == 0 ? "无详细项" : string.Join("\n\n", sections);
        }
        #endregion
    }
    #endregion

}
