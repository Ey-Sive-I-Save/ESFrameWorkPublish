using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ES.Editor
{
    public sealed class ESUIRiskAuditWindow : ESSinglePageIMGUIWindow<ESUIRiskAuditWindow>
    {
        private enum RiskLevel : byte
        {
            Error = 0,
            Warning = 1,
            Info = 2
        }

        private sealed class RiskItem
        {
            public RiskLevel level;
            public GameObject target;
            public string title;
            public string detail;
            public string suggestion;
            public string path;
        }

        private readonly List<RiskItem> risks = new List<RiskItem>(64);
        private GameObject uiRoot;
        private Vector2 scroll;
        private int graphicCount;
        private int canvasCount;
        private int layoutCount;

        [MenuItem("【ES】/验证与诊断/静态审计/打开 UI 风险体检")]
        private static void Open()
        {
            ESUIRiskAuditWindow window = GetWindow<ESUIRiskAuditWindow>("ES UI 风险体检");
            window.minSize = new Vector2(560f, 400f);
            window.maxSize = new Vector2(1400f, 1000f);
            window.Show();
        }

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent("ES UI 风险体检", "显式扫描指定 UI Root 的结构与性能风险");
        }
        public override string ESWindow_PresentationShortTitle => "UI审计";

        protected override string ESWindow_Subtitle => "指定 UI Root 的只读风险审计";
        protected override Vector2 ESWindow_MinSize => new Vector2(560f, 400f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(900f, 680f);
        protected override string ESWindow_PageStableId => "ui.risk-audit";
        protected override string ESWindow_PageTitle => "UI 风险体检";
        protected override string ESWindow_PageKeywords => "UI UGUI Canvas Layout Mask Raycast 性能 风险 审计";

        protected override void ESWindow_BuildPageActions(
            ICollection<ESMenuTreePageAction> actions)
        {
            actions.Add(new ESMenuTreePageAction(
                    "ui-risk.use-selection",
                    "使用当前选择",
                    "把 Hierarchy 当前选择设为 UI Root。",
                    context =>
                    {
                        uiRoot = Selection.activeGameObject;
                        ClearResults();
                        context.RefreshPageActions();
                        context.SetStatus(uiRoot != null ? "已更新 UI Root" : "当前没有选中 GameObject",
                            uiRoot != null ? ESMenuTreePageStatus.Info : ESMenuTreePageStatus.Warning);
                        Repaint();
                    })
                .WithUnityIcon("Linked")
                .WithPriority(100));
            actions.Add(new ESMenuTreePageAction(
                    "ui-risk.scan",
                    "扫描",
                    "只扫描当前指定 UI Root 的对象层级。",
                    context =>
                    {
                        Scan();
                        context.RefreshPageActions();
                        context.Notify(
                            risks.Count == 0 ? "UI Root 扫描完成，未发现已知风险" : $"UI Root 扫描完成：{risks.Count} 项",
                            risks.Exists(item => item.level == RiskLevel.Error)
                                ? ESMenuTreePageStatus.Error
                                : risks.Count > 0
                                    ? ESMenuTreePageStatus.Warning
                                    : ESMenuTreePageStatus.Info);
                    })
                .When(() => uiRoot != null)
                .WithUnityIcon("Search Icon")
                .WithPriority(90));
            actions.Add(new ESMenuTreePageAction(
                    "ui-risk.clear",
                    "清空结果",
                    "清除当前扫描结果，保留 UI Root。",
                    context =>
                    {
                        ClearResults();
                        context.RefreshPageActions();
                        context.SetStatus("已清空 UI 风险结果");
                        Repaint();
                    })
                .When(() => risks.Count > 0)
                .WithUnityIcon("TreeEditor.Trash")
                .WithPriority(20));
        }

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
            EditorGUILayout.LabelField("UI 风险体检", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "只检查当前指定的 UI Root，不会扫描场景外资产，也不会在窗口打开、OnGUI 或 ReloadDomain 时自动执行。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            GameObject nextRoot = (GameObject)EditorGUILayout.ObjectField(
                "UI Root",
                uiRoot,
                typeof(GameObject),
                true);
            if (EditorGUI.EndChangeCheck())
            {
                uiRoot = nextRoot;
                ClearResults();
                context.RefreshPageActions();
                context.SetStatus(uiRoot != null ? "已更新 UI Root" : "已清除 UI Root");
            }

            EditorGUILayout.Space();
            if (uiRoot == null)
            {
                EditorGUILayout.HelpBox("请在 Hierarchy 选择一个 UI 根节点，再点击“使用当前选择”。", MessageType.None);
                return;
            }

            DrawSummary();
            DrawRisks();
        }

        private void DrawSummary()
        {
            int errors = 0;
            int warnings = 0;
            int infos = 0;
            for (int i = 0; i < risks.Count; i++)
            {
                switch (risks[i].level)
                {
                    case RiskLevel.Error: errors++; break;
                    case RiskLevel.Warning: warnings++; break;
                    default: infos++; break;
                }
            }

            EditorGUILayout.LabelField("扫描结果", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"Graphic {graphicCount} / Canvas {canvasCount} / Layout {layoutCount} / 严重 {errors} / 警告 {warnings} / 提示 {infos}");
        }

        private void DrawRisks()
        {
            if (risks.Count == 0)
            {
                EditorGUILayout.HelpBox("尚未扫描，或当前范围没有发现已知风险。", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < risks.Count; i++)
            {
                RiskItem risk = risks[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(LevelName(risk.level) + " · " + risk.title, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("位置", risk.path);
                    EditorGUILayout.LabelField("原因", risk.detail, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("建议", risk.suggestion, EditorStyles.wordWrappedLabel);
                    if (risk.target != null && GUILayout.Button("定位对象", GUILayout.Width(88f)))
                    {
                        Selection.activeGameObject = risk.target;
                        EditorGUIUtility.PingObject(risk.target);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void Scan()
        {
            ClearResults();
            if (uiRoot == null)
                return;

            Transform[] transforms = uiRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
                ScanGameObject(transforms[i].gameObject);

            risks.Sort(CompareRisk);
            Repaint();
        }

        private void ScanGameObject(GameObject target)
        {
            int missingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target);
            if (missingScripts > 0)
            {
                Add(RiskLevel.Error, target, "存在丢失脚本",
                    $"该对象有 {missingScripts} 个 Missing Script，运行时行为和序列化数据都不可靠。",
                    "先确认原脚本身份并完成迁移，不要直接删除未知组件。");
            }

            Canvas canvas = target.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvasCount++;
                Canvas parentCanvas = target.transform.parent != null
                    ? target.transform.parent.GetComponentInParent<Canvas>()
                    : null;
                if (parentCanvas != null)
                {
                    Add(RiskLevel.Info, target, "嵌套 Canvas",
                        canvas.overrideSorting
                            ? "该 Canvas 开启了 Override Sorting，会形成独立排序和合批边界。"
                            : "该 Canvas 会形成独立 rebuild 范围，也可能切断原有批次。",
                        "确认它确实用于隔离高频变化区域；静态小节点通常不需要额外 Canvas。");
                }
            }

            Graphic graphic = target.GetComponent<Graphic>();
            if (graphic != null)
            {
                graphicCount++;
                if (graphic.raycastTarget && !HasInteractionHandlerInParents(target.transform))
                {
                    Add(RiskLevel.Warning, target, "非交互 Graphic 接收射线",
                        "Raycast Target 已开启，但当前对象到 UI Root 之间没有发现 Selectable 或事件处理器，可能白白增加射线检测并挡住后方控件。",
                        "如果它只负责显示，请关闭 Raycast Target；确实需要交互时保留并检查事件接收者。");
                }
            }

            LayoutGroup[] layoutGroups = target.GetComponents<LayoutGroup>();
            ContentSizeFitter fitter = target.GetComponent<ContentSizeFitter>();
            layoutCount += layoutGroups.Length + (fitter != null ? 1 : 0);
            if (layoutGroups.Length > 0 && fitter != null)
            {
                Add(RiskLevel.Warning, target, "LayoutGroup 与 ContentSizeFitter 同节点",
                    "两个组件可能同时控制 RectTransform 尺寸，造成重复布局、循环重建或抖动。",
                    "把尺寸控制拆到父子节点，明确谁控制宽度、谁控制高度，并用 Profiler 检查 Layout Rebuild。");
            }
            if (layoutGroups.Length > 1)
            {
                Add(RiskLevel.Warning, target, "同节点存在多个 LayoutGroup",
                    $"发现 {layoutGroups.Length} 个 LayoutGroup，它们可能重复控制同一批子节点。",
                    "通常只保留一个布局控制器；需要组合方向时拆成父子层级。");
            }
            if (fitter != null && layoutGroups.Length == 0 && HasLayoutGroupInParents(target.transform))
            {
                Add(RiskLevel.Warning, target, "ContentSizeFitter 与父级布局联动",
                    "当前节点使用 ContentSizeFitter，父级又存在 LayoutGroup，尺寸变化可能触发父子布局反复重建。",
                    "把尺寸计算集中到一个层级，或改用固定/受限尺寸；再用 Profiler 检查 Layout Rebuild 峰值。");
            }

            Mask mask = target.GetComponent<Mask>();
            RectMask2D rectMask = target.GetComponent<RectMask2D>();
            if (mask != null)
            {
                Add(RiskLevel.Info, target, "Mask 使用 Stencil",
                    "UGUI Mask 会引入 Stencil 状态，可能增加材质变体并切断合批。",
                    "如果只是矩形裁剪，优先评估 RectMask2D；非矩形裁剪再保留 Mask。");
            }
            if (mask != null || rectMask != null)
            {
                int maskDepth = CountMaskDepth(target.transform);
                if (maskDepth >= 3)
                {
                    Add(RiskLevel.Warning, target, "裁剪嵌套较深",
                        $"当前裁剪深度约为 {maskDepth}，Stencil/裁剪状态和材质分裂风险较高。",
                        "减少嵌套层级，合并可合并的裁剪区域，并用 Frame Debugger 查看批次变化。");
                }
            }

            Shadow[] meshEffects = target.GetComponents<Shadow>();
            if (meshEffects.Length > 0)
            {
                Add(RiskLevel.Warning, target, "阴影或描边增加 UI 顶点",
                    $"发现 {meshEffects.Length} 个 Shadow/Outline 类效果，会复制顶点并增加 Canvas 重建成本。",
                    "大量列表项中优先使用烘焙效果、Shader 或更少的装饰节点，并用 Profiler 验证顶点数。");
            }

            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup != null && canvasGroup.alpha <= 0.001f && canvasGroup.blocksRaycasts)
            {
                Add(RiskLevel.Warning, target, "透明 CanvasGroup 仍阻挡射线",
                    "Alpha 接近 0，但 Blocks Raycasts 仍开启，隐藏界面可能挡住后方按钮。",
                    "隐藏时同时关闭 Blocks Raycasts；不可交互时也关闭 Interactable。");
            }

            ScrollRect scrollRect = target.GetComponent<ScrollRect>();
            if (scrollRect != null && scrollRect.content != null && scrollRect.content.childCount >= 80
                && !HasVirtualizationMarker(scrollRect.content.gameObject))
            {
                Add(RiskLevel.Warning, target, "大列表未发现虚拟化标记",
                    $"Content 直接包含 {scrollRect.content.childCount} 个子节点，可能产生较高布局、Graphic 和射线成本。",
                    "只保留可见窗口附近的条目，使用对象池和异步预取；再用 Profiler 验证滚动峰值。");
            }
        }

        private bool HasInteractionHandlerInParents(Transform start)
        {
            Transform current = start;
            Transform rootTransform = uiRoot != null ? uiRoot.transform : null;
            while (current != null)
            {
                if (current.GetComponent<Selectable>() != null)
                    return true;

                MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                    if (behaviours[i] is IEventSystemHandler)
                        return true;

                if (current == rootTransform)
                    break;
                current = current.parent;
            }
            return false;
        }

        private int CountMaskDepth(Transform start)
        {
            int depth = 0;
            Transform current = start;
            Transform rootTransform = uiRoot != null ? uiRoot.transform : null;
            while (current != null)
            {
                if (current.GetComponent<Mask>() != null || current.GetComponent<RectMask2D>() != null)
                    depth++;
                if (current == rootTransform)
                    break;
                current = current.parent;
            }
            return depth;
        }

        private bool HasLayoutGroupInParents(Transform start)
        {
            Transform current = start != null ? start.parent : null;
            Transform rootTransform = uiRoot != null ? uiRoot.transform : null;
            while (current != null)
            {
                if (current.GetComponent<LayoutGroup>() != null)
                    return true;
                if (current == rootTransform)
                    break;
                current = current.parent;
            }
            return false;
        }

        private static bool HasVirtualizationMarker(GameObject content)
        {
            Component[] components = content.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;

                string name = component.GetType().Name;
                if (name.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Loop", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Recycle", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private void Add(RiskLevel level, GameObject target, string title, string detail, string suggestion)
        {
            risks.Add(new RiskItem
            {
                level = level,
                target = target,
                title = title,
                detail = detail,
                suggestion = suggestion,
                path = GetObjectPath(target != null ? target.transform : null)
            });
        }

        private string GetObjectPath(Transform target)
        {
            if (target == null)
                return "<对象已失效>";

            var names = new List<string>(8);
            Transform current = target;
            Transform rootTransform = uiRoot != null ? uiRoot.transform : null;
            while (current != null)
            {
                names.Add(current.name);
                if (current == rootTransform)
                    break;
                current = current.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        private void ClearResults()
        {
            risks.Clear();
            graphicCount = 0;
            canvasCount = 0;
            layoutCount = 0;
            scroll = Vector2.zero;
        }

        private static int CompareRisk(RiskItem left, RiskItem right)
        {
            int level = left.level.CompareTo(right.level);
            return level != 0 ? level : string.Compare(left.path, right.path, StringComparison.Ordinal);
        }

        private static string LevelName(RiskLevel level)
        {
            switch (level)
            {
                case RiskLevel.Error: return "严重";
                case RiskLevel.Warning: return "警告";
                default: return "提示";
            }
        }
    }
}
