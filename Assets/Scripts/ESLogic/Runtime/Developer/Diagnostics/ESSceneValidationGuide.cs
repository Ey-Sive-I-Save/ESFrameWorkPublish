using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    /// <summary>
    /// 测试场景专用的可复用验收导视。
    /// 它不是游戏 HUD、没有全局单例、不会读取或改写角色输入，也不会向 Entity、Vehicle、Camera Prefab 注入组件。
    /// 每个测试场景只拥有自己的 Guide、Canvas、步骤和诊断结果。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ES/Diagnostics/Scene Validation Guide")]
    public sealed class ESSceneValidationGuide : MonoBehaviour
    {
        [Header("场景说明")]
        [TextArea(1, 3)] public string guideTitle = "ES 测试场景验收";
        [TextArea(1, 3)] public string guideSubtitle = "按步骤执行，并依据实时诊断定位失败边界。";
        public bool showRuntimeOverlay = true;
        public bool showSceneRouteGizmos = true;
        [Min(0.05f)] public float refreshInterval = 0.2f;

        [Header("运行时路线导视")]
        [Tooltip("显式指定用于投影场景路线标记的输出相机；未指定时只从 ES Camera 的 MainView 输出读取，不使用 Unity 的隐式主相机查询。")]
        public Camera worldGuideCamera;
        [Tooltip("显式指定路线进度的观察者；未指定时只读取 LocalControl 的当前 Entity。")]
        public Transform routeObserver;
        [Min(0.1f)] public float routeMarkerHeight = 1.2f;
        public bool autoSelectNearestStage = true;

        [Header("验收路线")]
        public List<ESSceneValidationStage> stages = new List<ESSceneValidationStage>();

        [Header("实时诊断")]
        public List<ESSceneValidationCheck> checks = new List<ESSceneValidationCheck>();

        private const int OverlaySortingOrder = 32000;
        private const float OverlayWidth = 560f;
        private const float OverlayHeight = 760f;

        private readonly StringBuilder textBuilder = new StringBuilder(4096);
        private readonly StringBuilder bindingTextBuilder = new StringBuilder(64);
        private readonly List<ESInputCompiledBinding> inputBindings = new List<ESInputCompiledBinding>(8);
        private readonly Dictionary<string, ESSceneValidationRuntimeResult> reportedResults
            = new Dictionary<string, ESSceneValidationRuntimeResult>(8, StringComparer.Ordinal);

        private GameObject overlayRoot;
        private Canvas overlayCanvas;
        private UnityEngine.UI.Text titleText;
        private UnityEngine.UI.Text subtitleText;
        private UnityEngine.UI.Text routeProgressText;
        private UnityEngine.UI.Text stageText;
        private UnityEngine.UI.Text diagnosticsText;
        private Font runtimeFont;
        private Camera resolvedWorldGuideCamera;
        private float nextRefreshTime;
        private int activeStageIndex = -1;
        private bool presentationDirty = true;
        private string lastRouteProgressText;
        private string lastStageText;
        private string lastDiagnosticsText;
        private readonly List<RuntimeRouteMarker> routeMarkers = new List<RuntimeRouteMarker>(8);

        /// <summary>
        /// 外部测试器、PlayMode 用例或专用场景驱动器可提交自定义检查结果。
        /// 这不是全局服务；调用方必须显式持有该场景 Guide 的引用。
        /// </summary>
        public bool ReportCheck(string checkId, ESSceneValidationCheckState state, string detail)
        {
            EnsureCollections();
            if (string.IsNullOrWhiteSpace(checkId))
                return false;

            for (int i = 0; i < checks.Count; i++)
            {
                ESSceneValidationCheck check = checks[i];
                if (check == null || !string.Equals(check.id, checkId, StringComparison.Ordinal))
                    continue;

                var result = new ESSceneValidationRuntimeResult(state, detail);
                reportedResults[checkId] = result;
                // 外部检查结果是场景验收 API 的一部分，不等待下一次 HUD 刷新才对
                // 读取方可见；后续 EvaluateChecks 会从同一份报告重放此状态。
                check.runtimeState = result.state;
                check.runtimeDetail = result.detail;
                presentationDirty = true;
                return true;
            }

            return false;
        }

        /// <summary>读取当前检查结果；调用不触发重新评估，避免被 HUD 或 Debug Window 重复驱动。</summary>
        public bool TryGetCheckState(string checkId, out ESSceneValidationCheckState state, out string detail)
        {
            EnsureCollections();
            state = ESSceneValidationCheckState.Pending;
            detail = string.Empty;
            if (string.IsNullOrWhiteSpace(checkId))
                return false;

            for (int i = 0; i < checks.Count; i++)
            {
                ESSceneValidationCheck check = checks[i];
                if (check == null || !string.Equals(check.id, checkId, StringComparison.Ordinal))
                    continue;

                state = check.runtimeState;
                detail = check.runtimeDetail;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 编辑器构建器的显式配置入口。工具直接调用此方法，不反射或跨程序集读取私有序列化字段。
        /// </summary>
        public void ConfigureForAuthoring(
            string title,
            string subtitle,
            IList<ESSceneValidationStage> configuredStages,
            IList<ESSceneValidationCheck> configuredChecks)
        {
            EnsureCollections();
            guideTitle = title ?? string.Empty;
            guideSubtitle = subtitle ?? string.Empty;
            reportedResults.Clear();
            activeStageIndex = -1;
            presentationDirty = true;

            stages.Clear();
            if (configuredStages != null)
            {
                for (int i = 0; i < configuredStages.Count; i++)
                    if (configuredStages[i] != null)
                        stages.Add(configuredStages[i]);
            }

            checks.Clear();
            if (configuredChecks != null)
            {
                for (int i = 0; i < configuredChecks.Count; i++)
                    if (configuredChecks[i] != null)
                        checks.Add(configuredChecks[i]);
            }
        }

        /// <summary>
        /// 仅用于配置、语言或输入绑定在运行时被显式替换的测试场景。
        /// Guide 的常规轮询以状态变化为条件刷新文本，避免在稳态反复构造 HUD 字符串。
        /// </summary>
        public void InvalidatePresentation()
        {
            presentationDirty = true;
        }

        private void OnEnable()
        {
            EnsureCollections();
            presentationDirty = true;
            if (!Application.isPlaying)
                return;

            CreateRuntimeOverlay();
            RefreshNow();
        }

        private void Start()
        {
            // 覆盖 GameManager 在 Awake 中完成模块装配、SceneBinding 在 Start 中注册的正常时序。
            if (Application.isPlaying)
                RefreshNow();
        }

        private void Update()
        {
            if (!Application.isPlaying || Time.unscaledTime < nextRefreshTime)
                return;

            RefreshNow();
        }

        private void LateUpdate()
        {
            // 路线标签只读取已绑定的场景 Landmark 与显式相机输出；它既不参与相机仲裁，
            // 也不接管任何玩家输入。放在 LateUpdate 是为了跟随当帧相机的最终位置平滑投影。
            if (Application.isPlaying && overlayRoot != null)
                UpdateRouteMarkers();
        }

        private void OnDisable()
        {
            DisposeRuntimeOverlay();
        }

        private void OnDestroy()
        {
            DisposeRuntimeOverlay();
        }

        private void RefreshNow()
        {
            EnsureCollections();
            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
            EvaluateChecks();

            if (!showRuntimeOverlay)
            {
                DisposeRuntimeOverlay();
                return;
            }

            CreateRuntimeOverlay();
            if (overlayRoot == null)
                return;

            int resolvedStageIndex = ResolveActiveStageIndex();
            if (activeStageIndex != resolvedStageIndex)
            {
                activeStageIndex = resolvedStageIndex;
                presentationDirty = true;
            }

            if (!presentationDirty)
                return;

            titleText.text = guideTitle;
            subtitleText.text = guideSubtitle;

            string routeProgress = BuildRouteProgressText();
            if (!string.Equals(routeProgress, lastRouteProgressText, StringComparison.Ordinal))
            {
                routeProgressText.text = routeProgress;
                lastRouteProgressText = routeProgress;
            }

            string stage = BuildActiveStageText();
            if (!string.Equals(stage, lastStageText, StringComparison.Ordinal))
            {
                stageText.text = stage;
                lastStageText = stage;
            }

            string diagnostics = BuildDiagnosticsText();
            if (!string.Equals(diagnostics, lastDiagnosticsText, StringComparison.Ordinal))
            {
                diagnosticsText.text = diagnostics;
                lastDiagnosticsText = diagnostics;
            }

            UpdateRouteMarkerPresentation();
            UpdateRouteMarkers();
            presentationDirty = false;
        }

        private void EnsureCollections()
        {
            stages ??= new List<ESSceneValidationStage>();
            checks ??= new List<ESSceneValidationCheck>();
        }

        private void EvaluateChecks()
        {
            for (int i = 0; i < checks.Count; i++)
            {
                ESSceneValidationCheck check = checks[i];
                if (check == null)
                    continue;

                ESSceneValidationCheckState state = EvaluateCheck(check, out string detail);
                if (check.latchPass && check.runtimeState == ESSceneValidationCheckState.Passed)
                {
                    state = ESSceneValidationCheckState.Passed;
                    detail = check.runtimeDetail;
                }

                string resolvedDetail = detail ?? string.Empty;
                if (check.runtimeState != state
                    || !string.Equals(check.runtimeDetail, resolvedDetail, StringComparison.Ordinal))
                {
                    check.runtimeState = state;
                    check.runtimeDetail = resolvedDetail;
                    presentationDirty = true;
                }
            }
        }

        private ESSceneValidationCheckState EvaluateCheck(ESSceneValidationCheck check, out string detail)
        {
            switch (check.kind)
            {
                case ESSceneValidationCheckKind.FrameworkReady:
                    detail = ESGameManager.IsReady ? "ESGameManager 已建立。" : "尚未建立 ESGameManager。";
                    return ESGameManager.IsReady ? ESSceneValidationCheckState.Passed : ESSceneValidationCheckState.Failed;

                case ESSceneValidationCheckKind.InputReady:
                {
                    ESInputModule input = ESGameManager.InputModule;
                    bool ready = input != null && input.IsBuilt && input.IsInputEnabled;
                    detail = ready ? "ESInput 已构建并启用。" : "Input 模块未构建或尚未启用。";
                    return ready ? ESSceneValidationCheckState.Passed : ESSceneValidationCheckState.Failed;
                }

                case ESSceneValidationCheckKind.CameraOutputReady:
                {
                    ESCameraModule camera = ESGameManager.Camera;
                    bool ready = camera != null
                                 && camera.TryGetOutputTransform(new ESCameraViewId(check.cameraViewKey), out _);
                    detail = ready ? "View 输出已由 ES Camera 注册。" : "Camera Module 或目标 View 尚未就绪。";
                    return ready ? ESSceneValidationCheckState.Passed : ESSceneValidationCheckState.Failed;
                }

                case ESSceneValidationCheckKind.LocalControlOwner:
                {
                    Entity target = ResolveEntity(check.target);
                    if (target == null)
                    {
                        detail = "检查未绑定目标 Entity。";
                        return ESSceneValidationCheckState.Failed;
                    }

                    Entity controlled = ESGameManager.LocalControl?.ControlledEntity;
                    if (ReferenceEquals(controlled, target))
                    {
                        detail = "目标角色拥有本地控制权。";
                        return ESSceneValidationCheckState.Passed;
                    }

                    detail = controlled == null ? "尚无本地控制实体。" : "本地控制权属于其他实体。";
                    return ESSceneValidationCheckState.Failed;
                }

                case ESSceneValidationCheckKind.EntityMounted:
                {
                    Entity target = ResolveEntity(check.target);
                    if (target == null || target.kcc == null || target.kcc.mountModule == null)
                    {
                        detail = "目标角色未配置骑乘模块。";
                        return ESSceneValidationCheckState.Failed;
                    }

                    bool mounted = target.kcc.mountModule.IsMounted;
                    detail = mounted ? "角色正处于有效 Mounted 状态。" : "等待角色进入 Mounted 状态。";
                    return mounted ? ESSceneValidationCheckState.Passed : ESSceneValidationCheckState.Pending;
                }

                case ESSceneValidationCheckKind.VehicleReady:
                {
                    VehicleController vehicle = ResolveVehicle(check.target);
                    if (vehicle == null)
                    {
                        detail = "检查未绑定 VehicleController。";
                        return ESSceneValidationCheckState.Failed;
                    }

                    bool ready = vehicle.IsReady;
                    detail = ready ? "VehicleController 已接管运动体。" : "VehicleController 尚未就绪或已禁用。";
                    return ready ? ESSceneValidationCheckState.Passed : ESSceneValidationCheckState.Failed;
                }

                case ESSceneValidationCheckKind.VehicleDriverOwner:
                {
                    VehicleController vehicle = ResolveVehicle(check.target);
                    Entity expectedDriver = ResolveEntity(check.expectedEntity);
                    if (vehicle == null || expectedDriver == null)
                    {
                        detail = "驾驶权检查缺少 VehicleController 或预期驾驶者。";
                        return ESSceneValidationCheckState.Failed;
                    }

                    if (ReferenceEquals(vehicle.CurrentDriver, expectedDriver))
                    {
                        detail = "当前驾驶权属于目标角色。";
                        return ESSceneValidationCheckState.Passed;
                    }

                    detail = vehicle.CurrentDriver == null ? "座位尚未取得驾驶权。" : "驾驶权属于其他实体。";
                    return ESSceneValidationCheckState.Pending;
                }

                case ESSceneValidationCheckKind.TargetActive:
                {
                    bool active = IsTargetActive(check.target);
                    detail = active ? "目标对象处于活动状态。" : "目标对象为空、已销毁或未激活。";
                    return active ? ESSceneValidationCheckState.Passed : ESSceneValidationCheckState.Failed;
                }

                case ESSceneValidationCheckKind.ManualObservation:
                    detail = string.IsNullOrWhiteSpace(check.manualHint) ? "请按预期结果人工观察。" : check.manualHint;
                    return ESSceneValidationCheckState.Information;

                case ESSceneValidationCheckKind.External:
                    if (reportedResults.TryGetValue(check.id, out ESSceneValidationRuntimeResult result))
                    {
                        detail = result.detail;
                        return result.state;
                    }

                    detail = "等待场景专用测试器提交结果。";
                    return ESSceneValidationCheckState.Pending;

                default:
                    detail = "未知检查类型。";
                    return ESSceneValidationCheckState.Failed;
            }
        }

        private int ResolveActiveStageIndex()
        {
            if (stages == null || stages.Count == 0)
                return -1;

            Transform observer = routeObserver;
            if (observer == null)
                observer = ESGameManager.LocalControl?.ControlledEntity?.transform;

            if (autoSelectNearestStage && observer != null)
            {
                int nearestIndex = -1;
                float nearestSqrDistance = float.MaxValue;
                for (int i = 0; i < stages.Count; i++)
                {
                    ESSceneValidationStage stage = stages[i];
                    if (stage == null || stage.landmark == null)
                        continue;

                    float sqrDistance = (stage.landmark.position - observer.position).sqrMagnitude;
                    if (sqrDistance < nearestSqrDistance)
                    {
                        nearestSqrDistance = sqrDistance;
                        nearestIndex = i;
                    }
                }

                if (nearestIndex >= 0)
                    return nearestIndex;
            }

            // 无观察者或路线尚无空间 Landmark 时，优先指向第一段未通过的步骤。
            for (int i = 0; i < stages.Count; i++)
                if (!IsStageComplete(stages[i]))
                    return i;

            return 0;
        }

        private string BuildRouteProgressText()
        {
            textBuilder.Clear();
            textBuilder.Append("路线总览  ");
            if (autoSelectNearestStage)
                textBuilder.Append("（自动聚焦最近区域）");

            for (int i = 0; i < stages.Count; i++)
            {
                ESSceneValidationStage stage = stages[i];
                if (stage == null)
                    continue;

                textBuilder.Append('\n')
                    .Append(i == activeStageIndex ? "▶ " : "  ")
                    .Append(i + 1).Append(". ")
                    .Append(stage.title)
                    .Append("  ")
                    .Append(GetStageToken(stage));
            }

            return textBuilder.ToString();
        }

        private string BuildActiveStageText()
        {
            textBuilder.Clear();
            if (activeStageIndex < 0 || activeStageIndex >= stages.Count || stages[activeStageIndex] == null)
            {
                textBuilder.Append("当前阶段\n尚未配置可用的验收路线。");
                return textBuilder.ToString();
            }

            ESSceneValidationStage stage = stages[activeStageIndex];
            textBuilder.Append("当前阶段  ").Append(activeStageIndex + 1).Append(" / ").Append(stages.Count)
                .Append("  ").Append(GetStageToken(stage))
                .Append('\n').Append(stage.title);
            if (stage.landmark != null)
                textBuilder.Append("  ·  目标：").Append(stage.landmark.name);

            AppendInputs(stage.inputActions);
            AppendParagraph("现在去做", stage.objective);
            AppendParagraph("通过表现", stage.expectedResult);
            AppendParagraph("失败定位", stage.failureHint);
            AppendStageCheckSummary(stage);

            return textBuilder.ToString();
        }

        private string BuildDiagnosticsText()
        {
            textBuilder.Clear();
            textBuilder.Append("当前阶段诊断");

            ESSceneValidationStage activeStage = activeStageIndex >= 0 && activeStageIndex < stages.Count
                ? stages[activeStageIndex]
                : null;
            if (activeStage != null && activeStage.checkIds != null && activeStage.checkIds.Length > 0)
            {
                for (int i = 0; i < activeStage.checkIds.Length; i++)
                    AppendCheckDiagnostic(activeStage.checkIds[i]);
            }
            else
            {
                // 未配置阶段检查时，只列出尚未通过的系统项，避免把长清单塞入固定高度诊断区。
                for (int i = 0; i < checks.Count; i++)
                {
                    ESSceneValidationCheck check = checks[i];
                    if (check != null && check.runtimeState != ESSceneValidationCheckState.Passed)
                        AppendCheckDiagnostic(check.id);
                }
            }

            return textBuilder.ToString();
        }

        private void AppendCheckDiagnostic(string checkId)
        {
            if (!TryFindCheck(checkId, out ESSceneValidationCheck check))
                return;

            textBuilder.Append('\n')
                .Append(GetStateToken(check.runtimeState))
                .Append(' ')
                .Append(string.IsNullOrWhiteSpace(check.title) ? check.id : check.title);
            if (!string.IsNullOrWhiteSpace(check.runtimeDetail))
                textBuilder.Append(" · ").Append(check.runtimeDetail);
        }

        private void AppendInputs(ESInputActionId[] actionIds)
        {
            if (actionIds == null || actionIds.Length == 0)
                return;

            textBuilder.Append("\n   输入：");
            for (int i = 0; i < actionIds.Length; i++)
            {
                if (i > 0)
                    textBuilder.Append("  ·  ");

                ESInputActionId actionId = actionIds[i];
                textBuilder.Append(GetActionLabel(actionId)).Append(' ').Append(DescribeBindings(actionId));
            }
        }

        private string DescribeBindings(ESInputActionId actionId)
        {
            ESInputModule input = ESGameManager.InputModule;
            if (input == null || input.GetRuntimeBindings(actionId, inputBindings) == 0)
                return "（未解析）";

            bindingTextBuilder.Clear();
            for (int i = 0; i < inputBindings.Count; i++)
            {
                ESInputCompiledBinding binding = inputBindings[i];
                if (binding.isComposite || binding.isPartOfComposite)
                    continue;

                string display = HumanizeInputPath(binding.effectivePath, binding.virtualControlId);
                if (string.IsNullOrWhiteSpace(display))
                    continue;

                if (bindingTextBuilder.Length > 0)
                    bindingTextBuilder.Append('/');
                bindingTextBuilder.Append(display);
            }

            return bindingTextBuilder.Length == 0 ? "（已配置）" : bindingTextBuilder.ToString();
        }

        private static string HumanizeInputPath(string inputPath, string virtualControlId)
        {
            if (!string.IsNullOrWhiteSpace(virtualControlId))
                return virtualControlId;
            if (string.IsNullOrWhiteSpace(inputPath))
                return string.Empty;

            return inputPath
                .Replace("<Keyboard>/", string.Empty)
                .Replace("<Mouse>/", "鼠标/")
                .Replace("<Gamepad>/", "手柄/")
                .Replace("#(", string.Empty)
                .Replace(")", string.Empty);
        }

        private static string GetActionLabel(ESInputActionId actionId)
        {
            switch (actionId)
            {
                case ESInputActionId.Move: return "移动";
                case ESInputActionId.Look: return "视角";
                case ESInputActionId.Jump: return "跳跃";
                case ESInputActionId.Climb: return "攀爬";
                case ESInputActionId.Mount: return "骑乘";
                case ESInputActionId.FlyVertical: return "垂直飞行";
                case ESInputActionId.Interact: return "交互";
                default: return actionId.ToString();
            }
        }

        private void AppendParagraph(string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                textBuilder.Append("\n   ").Append(label).Append("：").Append(value);
        }

        private void AppendStageCheckSummary(ESSceneValidationStage stage)
        {
            if (stage.checkIds == null || stage.checkIds.Length == 0)
                return;

            int total = 0;
            int passed = 0;
            int failed = 0;
            for (int i = 0; i < stage.checkIds.Length; i++)
            {
                if (!TryGetCheckState(stage.checkIds[i], out ESSceneValidationCheckState state, out _))
                    continue;

                total++;
                if (state == ESSceneValidationCheckState.Passed)
                    passed++;
                else if (state == ESSceneValidationCheckState.Failed)
                    failed++;
            }

            if (total > 0)
                textBuilder.Append("\n   状态：")
                    .Append(passed).Append('/').Append(total).Append(" 通过")
                    .Append(failed > 0 ? "，存在失败项" : string.Empty);
        }

        private bool IsStageComplete(ESSceneValidationStage stage)
        {
            if (stage == null || stage.checkIds == null || stage.checkIds.Length == 0)
                return false;

            bool hasCheck = false;
            for (int i = 0; i < stage.checkIds.Length; i++)
            {
                if (!TryFindCheck(stage.checkIds[i], out ESSceneValidationCheck check))
                    continue;

                hasCheck = true;
                if (check.runtimeState != ESSceneValidationCheckState.Passed
                    && check.runtimeState != ESSceneValidationCheckState.Information)
                    return false;
            }

            return hasCheck;
        }

        private string GetStageToken(ESSceneValidationStage stage)
        {
            if (stage == null || stage.checkIds == null || stage.checkIds.Length == 0)
                return "[待验证]";

            bool hasCheck = false;
            bool hasPending = false;
            bool hasInformation = false;
            for (int i = 0; i < stage.checkIds.Length; i++)
            {
                if (!TryFindCheck(stage.checkIds[i], out ESSceneValidationCheck check))
                    continue;

                hasCheck = true;
                switch (check.runtimeState)
                {
                    case ESSceneValidationCheckState.Failed:
                        return "[失败]";
                    case ESSceneValidationCheckState.Pending:
                        hasPending = true;
                        break;
                    case ESSceneValidationCheckState.Information:
                        hasInformation = true;
                        break;
                }
            }

            if (!hasCheck || hasPending)
                return "[进行中]";
            return hasInformation ? "[人工观察]" : "[通过]";
        }

        private bool TryFindCheck(string checkId, out ESSceneValidationCheck result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(checkId))
                return false;

            for (int i = 0; i < checks.Count; i++)
            {
                ESSceneValidationCheck check = checks[i];
                if (check != null && string.Equals(check.id, checkId, StringComparison.Ordinal))
                {
                    result = check;
                    return true;
                }
            }

            return false;
        }

        private void CreateRuntimeOverlay()
        {
            if (!Application.isPlaying || !showRuntimeOverlay || overlayRoot != null)
                return;

            presentationDirty = true;
            runtimeFont = CreateRuntimeChineseFont();
            overlayRoot = new GameObject("ES Scene Validation Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            overlayRoot.hideFlags = HideFlags.DontSave;
            overlayRoot.transform.SetParent(transform, false);

            overlayCanvas = overlayRoot.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = OverlaySortingOrder;
            CanvasScaler scaler = overlayRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Image panel = CreateImage("Panel", overlayRoot.transform, new Color(0.02f, 0.045f, 0.09f, 0.92f));
            SetAnchoredRect(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -24f), new Vector2(28f + OverlayWidth, -24f - OverlayHeight));
            Image accent = CreateImage("Accent", panel.transform, new Color(0.25f, 0.78f, 1f, 1f));
            SetAnchoredRect(accent.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(5f, 0f));

            titleText = CreateText("Title", panel.transform, 25, FontStyle.Bold, new Color(0.84f, 0.94f, 1f, 1f));
            SetAnchoredRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -48f), new Vector2(-24f, -14f));
            subtitleText = CreateText("Subtitle", panel.transform, 14, FontStyle.Normal, new Color(0.58f, 0.72f, 0.85f, 1f));
            SetAnchoredRect(subtitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -82f), new Vector2(-24f, -52f));

            routeProgressText = CreateText("Route Progress", panel.transform, 13, FontStyle.Normal, new Color(0.64f, 0.82f, 0.94f, 1f));
            SetAnchoredRect(routeProgressText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -172f), new Vector2(-24f, -94f));

            Image stageSurface = CreateImage("Current Stage Surface", panel.transform, new Color(0.06f, 0.12f, 0.20f, 0.88f));
            SetAnchoredRect(stageSurface.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(20f, 188f), new Vector2(-16f, -184f));
            stageText = CreateText("Current Stage", stageSurface.transform, 15, FontStyle.Normal, new Color(0.89f, 0.94f, 0.98f, 1f));
            SetAnchoredRect(stageText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 14f), new Vector2(-16f, -14f));

            diagnosticsText = CreateText("Diagnostics", panel.transform, 13, FontStyle.Normal, new Color(0.64f, 0.82f, 0.94f, 1f));
            SetAnchoredRect(diagnosticsText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(28f, 18f), new Vector2(-24f, 176f));

            CreateRouteMarkers();
        }

        private void DisposeRuntimeOverlay()
        {
            if (overlayRoot == null)
                return;

            GameObject root = overlayRoot;
            overlayRoot = null;
            overlayCanvas = null;
            resolvedWorldGuideCamera = null;
            titleText = null;
            subtitleText = null;
            routeProgressText = null;
            stageText = null;
            diagnosticsText = null;
            lastRouteProgressText = null;
            lastStageText = null;
            lastDiagnosticsText = null;
            routeMarkers.Clear();
            if (Application.isPlaying)
                Destroy(root);
            else
                DestroyImmediate(root);
        }

        private UnityEngine.UI.Text CreateText(string name, Transform parent, int size, FontStyle style, Color color)
        {
            GameObject node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
            node.transform.SetParent(parent, false);
            UnityEngine.UI.Text text = node.GetComponent<UnityEngine.UI.Text>();
            text.font = runtimeFont;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            node.transform.SetParent(parent, false);
            Image image = node.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void SetAnchoredRect(RectTransform transform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            transform.anchorMin = anchorMin;
            transform.anchorMax = anchorMax;
            transform.offsetMin = offsetMin;
            transform.offsetMax = offsetMax;
        }

        private void CreateRouteMarkers()
        {
            if (overlayRoot == null)
                return;

            for (int i = 0; i < stages.Count; i++)
            {
                ESSceneValidationStage stage = stages[i];
                if (stage == null || stage.landmark == null)
                    continue;

                Image markerSurface = CreateImage("Route Marker " + (i + 1), overlayRoot.transform, new Color(0.05f, 0.20f, 0.32f, 0.94f));
                markerSurface.raycastTarget = false;
                markerSurface.rectTransform.sizeDelta = new Vector2(152f, 42f);

                UnityEngine.UI.Text markerText = CreateText("Label", markerSurface.transform, 13, FontStyle.Bold, Color.white);
                markerText.raycastTarget = false;
                markerText.alignment = TextAnchor.MiddleCenter;
                SetAnchoredRect(markerText.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 3f), new Vector2(-8f, -3f));
                routeMarkers.Add(new RuntimeRouteMarker(i, markerSurface, markerText));
            }
        }

        private void UpdateRouteMarkerPresentation()
        {
            for (int i = 0; i < routeMarkers.Count; i++)
            {
                RuntimeRouteMarker marker = routeMarkers[i];
                ESSceneValidationStage stage = marker.stageIndex >= 0 && marker.stageIndex < stages.Count
                    ? stages[marker.stageIndex]
                    : null;
                if (stage != null)
                    marker.SetLabel((marker.stageIndex + 1).ToString("00") + "  " + stage.title);
            }
        }

        private void UpdateRouteMarkers()
        {
            if (overlayRoot == null || overlayCanvas == null || routeMarkers.Count == 0)
                return;

            Camera camera = ResolveWorldGuideCamera();
            if (camera == null)
            {
                SetRouteMarkersVisible(false);
                return;
            }

            RectTransform canvasRect = overlayCanvas.transform as RectTransform;
            for (int i = 0; i < routeMarkers.Count; i++)
            {
                RuntimeRouteMarker marker = routeMarkers[i];
                ESSceneValidationStage stage = marker.stageIndex >= 0 && marker.stageIndex < stages.Count
                    ? stages[marker.stageIndex]
                    : null;
                if (stage == null || stage.landmark == null)
                {
                    marker.SetVisible(false);
                    continue;
                }

                Vector3 worldPosition = stage.landmark.position + Vector3.up * routeMarkerHeight;
                Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);
                bool visible = screenPosition.z > 0.01f
                               && screenPosition.x >= 0f && screenPosition.x <= Screen.width
                               && screenPosition.y >= 0f && screenPosition.y <= Screen.height;
                marker.SetVisible(visible);
                if (!visible)
                    continue;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 localPosition);
                marker.rectTransform.anchoredPosition = localPosition;
                marker.SetActive(marker.stageIndex == activeStageIndex);
            }
        }

        private Camera ResolveWorldGuideCamera()
        {
            if (worldGuideCamera != null && worldGuideCamera.isActiveAndEnabled)
                return worldGuideCamera;

            if (worldGuideCamera != null)
                return null;

            if (resolvedWorldGuideCamera != null && resolvedWorldGuideCamera.isActiveAndEnabled)
                return resolvedWorldGuideCamera;

            resolvedWorldGuideCamera = null;

            ESCameraModule cameraModule = ESGameManager.Camera;
            if (cameraModule != null
                && cameraModule.TryGetOutputTransform(ESCameraViewId.Main, out Transform output)
                && output != null)
            {
                Camera outputCamera = output.GetComponent<Camera>();
                if (outputCamera != null && outputCamera.isActiveAndEnabled)
                {
                    resolvedWorldGuideCamera = outputCamera;
                    return outputCamera;
                }
            }

            return null;
        }

        private void SetRouteMarkersVisible(bool visible)
        {
            for (int i = 0; i < routeMarkers.Count; i++)
                routeMarkers[i].SetVisible(visible);
        }

        private static Font CreateRuntimeChineseFont()
        {
            try
            {
                Font font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans SC", "DengXian" },
                    16);
                if (font != null)
                    return font;
            }
            catch
            {
                // 目标机可能没有中文系统字体；下面使用 Unity 内置字体继续显示可读的回退文本。
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static bool IsTargetActive(UnityEngine.Object target)
        {
            if (target == null)
                return false;
            if (target is GameObject gameObject)
                return gameObject.activeInHierarchy;
            if (target is Behaviour behaviour)
                return behaviour.isActiveAndEnabled;
            if (target is Component component)
                return component.gameObject.activeInHierarchy;
            return true;
        }

        private static Entity ResolveEntity(UnityEngine.Object target)
        {
            if (target is Entity entity)
                return entity;
            if (target is Component component)
                return component.GetComponent<Entity>();
            if (target is GameObject gameObject)
                return gameObject.GetComponent<Entity>();
            return null;
        }

        private static VehicleController ResolveVehicle(UnityEngine.Object target)
        {
            if (target is VehicleController vehicle)
                return vehicle;
            if (target is Component component)
                return component.GetComponent<VehicleController>();
            if (target is GameObject gameObject)
                return gameObject.GetComponent<VehicleController>();
            return null;
        }

        private static string GetStateToken(ESSceneValidationCheckState state)
        {
            switch (state)
            {
                case ESSceneValidationCheckState.Passed: return "[通过]";
                case ESSceneValidationCheckState.Failed: return "[失败]";
                case ESSceneValidationCheckState.Information: return "[观察]";
                default: return "[等待]";
            }
        }

        private sealed class RuntimeRouteMarker
        {
            public readonly int stageIndex;
            public readonly Image surface;
            public readonly UnityEngine.UI.Text text;
            public readonly RectTransform rectTransform;
            private string lastLabel;
            private bool isActive;

            public RuntimeRouteMarker(int stageIndex, Image surface, UnityEngine.UI.Text text)
            {
                this.stageIndex = stageIndex;
                this.surface = surface;
                this.text = text;
                rectTransform = surface != null ? surface.rectTransform : null;
            }

            public void SetVisible(bool visible)
            {
                if (surface != null && surface.gameObject.activeSelf != visible)
                    surface.gameObject.SetActive(visible);
            }

            public void SetLabel(string label)
            {
                if (text == null || string.Equals(lastLabel, label, StringComparison.Ordinal))
                    return;

                text.text = label;
                lastLabel = label;
            }

            public void SetActive(bool active)
            {
                if (surface == null || isActive == active)
                    return;

                surface.color = active
                    ? new Color(0.16f, 0.66f, 0.97f, 0.98f)
                    : new Color(0.05f, 0.20f, 0.32f, 0.94f);
                isActive = active;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showSceneRouteGizmos || stages == null)
                return;

            for (int i = 0; i < stages.Count; i++)
            {
                ESSceneValidationStage stage = stages[i];
                if (stage == null || stage.landmark == null)
                    continue;

                Color color = stage.routeColor.a <= 0f ? new Color(0.28f, 0.82f, 1f, 1f) : stage.routeColor;
                Gizmos.color = color;
                Vector3 position = stage.landmark.position + Vector3.up * 1.1f;
                Gizmos.DrawWireSphere(position, 0.28f);
                Handles.color = color;
                Handles.Label(position + Vector3.up * 0.35f, (i + 1) + ". " + stage.title, EditorStyles.boldLabel);
            }
        }
#endif
    }

    [Serializable]
    public sealed class ESSceneValidationStage
    {
        public string id;
        public string title;
        public Transform landmark;
        public Color routeColor = new Color(0.28f, 0.82f, 1f, 1f);
        [TextArea(2, 5)] public string objective;
        [TextArea(2, 5)] public string expectedResult;
        [TextArea(2, 5)] public string failureHint;
        public ESInputActionId[] inputActions = Array.Empty<ESInputActionId>();
        public string[] checkIds = Array.Empty<string>();
    }

    public enum ESSceneValidationCheckKind
    {
        FrameworkReady = 0,
        InputReady = 1,
        CameraOutputReady = 2,
        LocalControlOwner = 3,
        EntityMounted = 4,
        VehicleReady = 5,
        VehicleDriverOwner = 6,
        TargetActive = 7,
        ManualObservation = 8,
        External = 9,
    }

    public enum ESSceneValidationCheckState
    {
        Pending = 0,
        Passed = 1,
        Failed = 2,
        Information = 3,
    }

    [Serializable]
    public sealed class ESSceneValidationCheck
    {
        public string id;
        public string title;
        public ESSceneValidationCheckKind kind;
        public UnityEngine.Object target;
        public UnityEngine.Object expectedEntity;
        public string cameraViewKey = "MainView";
        public bool latchPass;
        [TextArea(1, 3)] public string manualHint;

        [NonSerialized] public ESSceneValidationCheckState runtimeState;
        [NonSerialized] public string runtimeDetail;
    }

    public readonly struct ESSceneValidationRuntimeResult
    {
        public readonly ESSceneValidationCheckState state;
        public readonly string detail;

        public ESSceneValidationRuntimeResult(ESSceneValidationCheckState state, string detail)
        {
            this.state = state;
            this.detail = detail ?? string.Empty;
        }
    }
}
