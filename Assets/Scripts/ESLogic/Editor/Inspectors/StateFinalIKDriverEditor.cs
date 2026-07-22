#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ES
{
    [CustomEditor(typeof(StateFinalIKDriver))]
    public sealed class StateFinalIKDriverEditor : UnityEditor.Editor
    {
        private enum Page
        {
            Config,
            Diagnostics,
            Test,
            Advanced
        }

        private enum Fold
        {
            QuickStart,
            Runtime,
            Binding,
            Biped,
            Grounder,
            LookAt,
            Aim,
            FullBody,
            HitReaction,
            Recoil,
            DiagnosticsSummary,
            DiagnosticsRuntime,
            DiagnosticsTargets,
            RealtimeTest,
            Gizmos,
            AutoAdd,
            Presets
        }

        private static readonly Dictionary<string, MethodInfo> MethodCache = new Dictionary<string, MethodInfo>();

        private readonly Dictionary<Fold, bool> foldouts = new Dictionary<Fold, bool>
        {
            { Fold.QuickStart, true },
            { Fold.Runtime, false },
            { Fold.Binding, false },
            { Fold.Biped, false },
            { Fold.Grounder, false },
            { Fold.LookAt, false },
            { Fold.Aim, false },
            { Fold.FullBody, false },
            { Fold.HitReaction, false },
            { Fold.Recoil, false },
            { Fold.DiagnosticsSummary, true },
            { Fold.DiagnosticsRuntime, true },
            { Fold.DiagnosticsTargets, false },
            { Fold.RealtimeTest, true },
            { Fold.Gizmos, false },
            { Fold.AutoAdd, true },
            { Fold.Presets, false }
        };

        private Page selectedPage;
        private GUIStyle headerTitleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle badgeStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle miniLabelStyle;
        private bool initializedFoldouts;

        public override void OnInspectorGUI()
        {
            EnsureStyles();
            serializedObject.UpdateIfRequiredOrScript();

            var driver = (StateFinalIKDriver)target;
            InitializeFoldoutsForCurrentView();

            DrawOuterFrame(() =>
            {
                DrawHeader(driver);
                DrawNavigation();
                DrawPageBanner();
                GUILayout.Space(8f);

                switch (selectedPage)
                {
                    case Page.Config:
                        DrawConfigPage(driver);
                        break;
                    case Page.Diagnostics:
                        DrawDiagnosticsPage(driver);
                        break;
                    case Page.Test:
                        DrawTestPage();
                        break;
                    case Page.Advanced:
                        DrawAdvancedPage(driver);
                        break;
                }
            });

            serializedObject.ApplyModifiedProperties();
        }

        private void InitializeFoldoutsForCurrentView()
        {
            if (initializedFoldouts)
                return;

            initializedFoldouts = true;
            SetFold(Fold.QuickStart, true);
            SetFold(Fold.Binding, false);
        }

        private void DrawConfigPage(StateFinalIKDriver driver)
        {
            DrawCard(Fold.QuickStart, "快速接入", "普通角色优先从这里开始，不需要理解 Final IK 组件细节。", () =>
            {
                EditorGUILayout.HelpBox("推荐流程：选择角色类型 -> 自动挂载需要的 IK 组件 -> 从 Animator 填充骨骼 -> 应用到驱动。失败时再展开下方细项排查。", MessageType.Info);
                DrawInfoLine("当前关系", driver.DriverRelationSummary);
                DrawInfoLine("组件状态", BuildSetupStatus(driver));
                DrawButtons(
                    ("通用角色一键接入", () => RunGeneralCharacterSetup(driver)),
                    ("射击角色一键接入", () => RunShooterCharacterSetup(driver)),
                    ("性能压测一键关闭", ApplyPerformanceTestPreset));
            });

            DrawCard(Fold.Runtime, "运行控制", "初始化、重绑与全局保护开关。", () =>
            {
                DrawRow("autoDetectReferencesIfMissing", "自动识别骨骼引用", "warnWhenPoseHasWeightButNoIK", "有权重但无 IK 时警告");
                DrawProperty("rebindInterval", "热插拔重试间隔");
                DrawButtons(
                    ("重新绑定四肢驱动", () => InvokeDriver(driver, "ManualRebindBipedIK")),
                    ("应用初始参数", () => InvokeDriver(driver, "ApplyIKInitialSettingsFromInspector")));
            });

            DrawCard(Fold.Presets, "初始化预设", "写入常用默认值，适合新角色搭建、射击角色和性能压测。", () =>
            {
                EditorGUILayout.HelpBox("推荐流程：选预设 -> 自动填充骨骼 -> 应用到已挂载驱动 -> 只微调当前项目需要的卡片。预设只修改 Driver 配置，不删除组件。", MessageType.Info);
                DrawButtons(
                    ("通用角色默认", ApplyGeneralCharacterPreset),
                    ("射击角色默认", ApplyShooterCharacterPreset),
                    ("性能压测关闭", ApplyPerformanceTestPreset));
            });

            DrawCard(Fold.Binding, "统一骨骼绑定", "一次配置角色骨架，自动写入四肢、注视、瞄准、受击和后坐力驱动。", () =>
            {
                EditorGUILayout.HelpBox("可重复执行自动填充。检测到有效 Humanoid Animator 时会整体刷新绑定；未找到或 Avatar 不合法时保持旧绑定不变。", MessageType.Info);
                DrawProperty("useDriverBoneBinding", "启用 Driver 骨骼绑定");
                using (new EditorGUI.DisabledScope(!Bool("useDriverBoneBinding")))
                {
                    DrawBindingGrid();
                    DrawSubCard("写入范围", () =>
                    {
                        DrawRow("applyToBipedIKFromInspector", "写入四肢定位", "applyToFullBodyBipedIKFromInspector", "写入全身反应");
                        DrawRow("applyToLookAtIKFromInspector", "写入头眼注视", "applyToAimIKFromInspector", "写入武器瞄准");
                        DrawRow("applyToHitReactionFromInspector", "写入受击反馈", "applyToRecoilFromInspector", "写入后坐力");
                    });
                }
                DrawButtons(
                    ("从 Animator 自动填充", () => InvokeDriver(driver, "AutoFillDriverBoneBinding")),
                    ("应用到已挂载驱动", () => InvokeDriver(driver, "ApplyDriverBoneBindingFromInspector")),
                    ("清空骨骼绑定", () => InvokeDriver(driver, "ClearDriverBoneBinding")));
            });

            DrawCard(Fold.Biped, "【四肢定位】", "驱动左右手脚目标，并提供基础头身跟随兜底。", () =>
            {
                DrawProperty("enableBipedIK", "启用四肢定位");
                using (new EditorGUI.DisabledScope(!Bool("enableBipedIK")))
                {
                    DrawRow("limbWeightSmoothTime", "权重平滑", "limbLerpingRateRecoverTime", "速率恢复");
                    DrawRow("footRotationWeightMultiplier", "脚部旋转倍率", "driveGoalTargetsFromPose", "Pose 驱动目标点");
                    DrawRow("bipedLookAtDefaultBodyWeight", "身体注视权重", "bipedLookAtDefaultClampWeight", "注视限制");
                    DrawRow("bipedLookAtDefaultHeadWeight", "头部注视权重", "bipedLookAtDefaultEyesWeight", "眼部注视权重");
                }
                DrawButtons(("添加【四肢定位】组件", () => InvokeDriver(driver, "QuickAddComp_BipedIK")));
            });

            DrawCard(Fold.Grounder, "【脚步贴地】", "让脚步适应地形高度，依赖四肢定位。", () =>
            {
                DrawProperty("enableGrounderBipedIK", "启用脚步贴地");
                using (new EditorGUI.DisabledScope(!Bool("enableBipedIK") || !Bool("enableGrounderBipedIK")))
                {
                    DrawRow("grounderWeightSmoothTime", "权重平滑", "grounderLerpingRateRecoverTime", "速率恢复");
                    DrawRow("initGrounderWeight", "整体权重", "initGrounderMaxStep", "最大台阶");
                    DrawProperty("initGrounderSpeed", "接地速度");
                }
                DrawButtons(("添加【脚步贴地】组件", () => InvokeDriver(driver, "QuickAddComp_GrounderBipedIK")));
            });

            DrawCard(Fold.LookAt, "【头眼注视】", "控制头部、眼睛和脊柱朝向目标。", () =>
            {
                DrawProperty("enableLookAtIK", "启用头眼注视");
                using (new EditorGUI.DisabledScope(!Bool("enableLookAtIK")))
                {
                    DrawRow("lookAtWeightSmoothTime", "权重平滑", "lookAtLerpingRateRecoverTime", "速率恢复");
                    DrawRow("initLookAtWeight", "整体权重", "initLookAtClampWeight", "限制权重");
                    DrawRow("initLookAtHeadWeight", "头部权重", "initLookAtEyesWeight", "眼部权重");
                    DrawProperty("initLookAtSpineWeight", "脊柱权重");
                }
                DrawButtons(("添加【头眼注视】组件", () => InvokeDriver(driver, "QuickAddComp_LookAtIK")));
            });

            DrawCard(Fold.Aim, "【武器瞄准】", "配置瞄准方向、极向稳定和左右探头。", () =>
            {
                DrawProperty("enableAimIK", "启用武器瞄准");
                using (new EditorGUI.DisabledScope(!Bool("enableAimIK")))
                {
                    DrawRow("aimIKHeartbeatTimeout", "心跳超时", "aimIKDecayDuration", "衰减时长");
                    DrawRow("aimWeightSmoothTime", "权重平滑", "aimLerpingRateRecoverTime", "速率恢复");
                    DrawRow("useInitAimWeightOnBind", "绑定时使用初始权重", "initAimWeight", "初始权重");
                    DrawRow("initAimClampWeight", "限制权重", "initAimAxis", "瞄准轴");

                    DrawSubCard("瞄准节点", () =>
                    {
                        DrawProperty("aimControlledTransform", "瞄准方向节点");
                        DrawProperty("aimPoleTarget", "极向目标");
                        DrawRow("aimPoleAxis", "极向轴", "aimPoleWeight", "极向权重");
                    });

                    DrawSubCard("探头", () =>
                    {
                        DrawRow("aimPeekLeftAnchor", "左肩锚点", "aimPeekRightAnchor", "右肩锚点");
                        DrawProperty("aimPeekReferenceTransform", "探头参考");
                        DrawRow("aimPeekLeftLocalOffset", "左探头偏移", "aimPeekRightLocalOffset", "右探头偏移");
                    });
                }
                DrawButtons(
                    ("添加【武器瞄准】组件", () => InvokeDriver(driver, "QuickAddComp_AimIK")),
                    ("应用瞄准配置", () => InvokeDriver(driver, "ApplyDriverAimChainFromInspector")));
            });

            DrawCard(Fold.FullBody, "【全身反应】", "受击反馈和后坐力的全身骨骼基础。", () =>
            {
                DrawProperty("enableFullBodyBipedIK", "启用全身反应");
                DrawButtons(("添加【全身反应】组件", () => InvokeDriver(driver, "QuickAddComp_FullBodyBipedIK")));
            });

            DrawCard(Fold.HitReaction, "【受击反馈】", "角色被击中后的身体反馈，依赖全身反应。", () =>
            {
                DrawProperty("enableHitReaction", "启用受击反馈");
                using (new EditorGUI.DisabledScope(!Bool("enableFullBodyBipedIK") || !Bool("enableHitReaction")))
                {
                    DrawProperty("useDriverHitReactionSetup", "使用内置配置");
                    using (new EditorGUI.DisabledScope(!Bool("useDriverHitReactionSetup")))
                    {
                        DrawRow("driverHitReactionWeight", "整体权重", "driverHitReactionDuration", "持续时间");
                        DrawRow("driverHitReactionUpForce", "上抬力度", "driverHitReactionHeadAngle", "头部扭转");
                        DrawRow("hitBodyCollider", "身体碰撞", "hitHeadCollider", "头部碰撞");
                        DrawRow("hitLeftArmCollider", "左臂碰撞", "hitRightArmCollider", "右臂碰撞");
                        DrawRow("hitLeftLegCollider", "左腿碰撞", "hitRightLegCollider", "右腿碰撞");
                    }
                }
                DrawButtons(
                    ("添加【受击反馈】组件", () => InvokeDriver(driver, "QuickAddComp_HitReaction")),
                    ("识别碰撞体", () => InvokeDriver(driver, "AutoFillDriverHitReactionColliders")),
                    ("应用配置", () => InvokeDriver(driver, "ApplyDriverHitReactionFromInspector")),
                    ("清空配置", () => InvokeDriver(driver, "ClearDriverHitReactionSetup")));
            });

            DrawCard(Fold.Recoil, "【后坐力】", "武器开火时的手部、身体和旋转反馈。", () =>
            {
                DrawProperty("enableRecoil", "启用后坐力");
                using (new EditorGUI.DisabledScope(!Bool("enableFullBodyBipedIK") || !Bool("enableRecoil")))
                {
                    DrawProperty("useDriverRecoilSetup", "使用内置配置");
                    using (new EditorGUI.DisabledScope(!Bool("useDriverRecoilSetup")))
                    {
                        DrawRow("driverRecoilWeight", "整体权重", "driverRecoilDuration", "脉冲时长");
                        DrawRow("driverRecoilHandedness", "主手", "driverRecoilTwoHanded", "双手持握");
                        DrawRow("driverRecoilBlendTime", "混合时长", "driverRecoilMagnitudeRandom", "力度随机");
                        DrawRow("driverRecoilPrimaryOffset", "主手位移", "driverRecoilSecondaryOffset", "副手位移");
                        DrawRow("driverRecoilBodyOffset", "身体位移", "driverRecoilHandRotationOffset", "手部旋转");
                        DrawProperty("driverRecoilRotationRandom", "旋转随机");
                    }
                }
                DrawButtons(
                    ("添加【后坐力】组件", () => InvokeDriver(driver, "QuickAddComp_Recoil")),
                    ("应用配置", () => InvokeDriver(driver, "ApplyDriverRecoilFromInspector")),
                    ("清空配置", () => InvokeDriver(driver, "ClearDriverRecoilSetup")));
            });
        }

        private void DrawDiagnosticsPage(StateFinalIKDriver driver)
        {
            DrawCard(Fold.DiagnosticsSummary, "状态概览", "组件就绪、启用状态与求解顺序。", () =>
            {
                DrawInfoLine("能力集", driver.Capabilities.ToString());
                DrawInfoLine("启用状态", driver.DriverEnableStateSummary);
                DrawInfoLine("调度模式", driver.FinalIKScheduleMode.ToString());
                DrawInfoLine("商业顺序契约", driver.FinalIKCommercialOrderSummary);
                DrawInfoLine("求解顺序", driver.FinalIKScheduleSummary);
                DrawInfoLine("阻断说明", driver.FinalIKScheduleBlockSummary);
                DrawStatusGrid(driver);
            });

            DrawCard(Fold.DiagnosticsRuntime, "运行统计", "绑定、写入与求解调用计数。", () =>
            {
                DrawInfoLine("绑定尝试", driver.BindTryCount.ToString());
                DrawInfoLine("绑定成功", driver.BindSuccessCount.ToString());
                DrawInfoLine("Pose 写入", driver.ApplyCount.ToString());
                DrawInfoLine("Solver 更新", driver.SolverUpdateCount.ToString());
                DrawInfoLine("上次写入", driver.LastApplyTime.ToString("F3"));
                DrawInfoLine("上次求解", driver.LastSolverUpdateTime.ToString("F3"));
            });

            DrawCard(Fold.DiagnosticsTargets, "当前目标", "运行时目标点与状态机 IK 输出摘要。", () =>
            {
                DrawInfoLine("有激活 IK 权重", driver.HasActiveIK ? "是" : "否");
                DrawInfoLine("当前 Driver Pose", driver.CurrentPose.ToString());
                DrawInfoLine("状态 IK 贡献", driver.StateIKContributions);
                DrawInfoLine("左手目标", SafeName(driver.LeftHandTarget));
                DrawInfoLine("右手目标", SafeName(driver.RightHandTarget));
                DrawInfoLine("左脚目标", SafeName(driver.LeftFootTarget));
                DrawInfoLine("右脚目标", SafeName(driver.RightFootTarget));
            });

            DrawButtons(
                ("打开运行面板", () => InvokeDriver(driver, "OpenInteractionRuntimePanel")),
                ("补齐缺失组件", () => InvokeDriver(driver, "AutoAddMissingComponents")));
        }

        private void DrawTestPage()
        {
            DrawCard(Fold.RealtimeTest, "实时权重测试", "运行时直接覆盖测试权重，便于压力测试和效果验证。", () =>
            {
                DrawProperty("enableRealtimeWeightTest", "启用实时权重测试");
                using (new EditorGUI.DisabledScope(!Bool("enableRealtimeWeightTest")))
                {
                    DrawRow("realtimeLeftHandWeight", "左手", "realtimeRightHandWeight", "右手");
                    DrawRow("realtimeLeftFootWeight", "左脚", "realtimeRightFootWeight", "右脚");
                    DrawRow("realtimeLookAtWeight", "注视权重", "realtimeAimWeight", "瞄准权重");
                    DrawRow("realtimeLookAtTarget", "注视目标", "realtimeAimTarget", "瞄准目标");
                }

                if (!Application.isPlaying)
                    EditorGUILayout.HelpBox("实时测试只在运行时生效。", MessageType.Info);
            });

            DrawCard(Fold.Gizmos, "Scene / Game 可视化", "目标点、Hint、误差线与权重条。", () =>
            {
                DrawProperty("debugDrawIKGizmosInSceneAndGame", "启用 Scene/Game 可视化");
                using (new EditorGUI.DisabledScope(!Bool("debugDrawIKGizmosInSceneAndGame")))
                {
                    DrawRow("debugDrawIKTargetLines", "目标连线", "debugDrawIKHints", "Hint 点");
                    DrawRow("debugDrawIKWeightBars", "权重条", "debugIKGizmoSize", "显示尺寸");
                }
            });
        }

        private void DrawAdvancedPage(StateFinalIKDriver driver)
        {
            DrawCard(Fold.AutoAdd, "自动添加组件", "高级兜底项，默认不建议全部开启。", () =>
            {
                DrawProperty("logMissingComponentHints", "输出缺失组件提示");
                DrawRow("autoAddBipedIK", "四肢定位", "autoAddGrounderBipedIK", "脚步贴地");
                DrawRow("autoAddLookAtIK", "头眼注视", "autoAddAimIK", "武器瞄准");
                DrawRow("autoAddFullBodyBipedIK", "全身反应", "autoAddHitReaction", "受击反馈");
                DrawProperty("autoAddRecoil", "后坐力");
                DrawButtons(("补齐缺失组件", () => InvokeDriver(driver, "AutoAddMissingComponents")));
            });
        }

        private void DrawOuterFrame(Action content)
        {
            Rect outer = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(outer.x, outer.y, outer.width, outer.height), new Color(0.18f, 0.18f, 0.18f));
            GUILayout.Space(1f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                content?.Invoke();
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(2f);
        }

        private void DrawHeader(StateFinalIKDriver driver)
        {
            float headerHeight = EditorGUIUtility.currentViewWidth < 900f ? 84f : 58f;
            Rect rect = GUILayoutUtility.GetRect(0f, headerHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.13f, 0.14f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(0.35f, 0.66f, 0.92f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0.08f, 0.08f, 0.08f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 5f, rect.height), new Color(0.35f, 0.66f, 0.92f));

            bool compact = EditorGUIUtility.currentViewWidth < 900f;
            var titleRect = new Rect(rect.x + 14f, rect.y + 8f, compact ? rect.width - 28f : rect.width * 0.48f, 22f);
            var subRect = new Rect(rect.x + 14f, rect.y + 32f, compact ? rect.width - 28f : rect.width * 0.48f, 18f);
            GUI.Label(titleRect, "角色动作驱动器", headerTitleStyle);
            GUI.Label(subRect, driver.DriverRelationSummary, subtitleStyle);

            float chipY = compact ? rect.y + 52f : rect.y + 18f;
            float x = compact ? rect.x + 14f : rect.xMax - 520f;
            DrawBadge(ref x, chipY, driver.BoundEntity != null ? "已连Entity" : "未连Entity", driver.BoundEntity != null ? new Color(0.20f, 0.54f, 0.32f) : new Color(0.38f, 0.38f, 0.38f));
            DrawBadge(ref x, chipY, driver.BoundStateMachine != null ? "已连状态机" : "未连状态机", driver.BoundStateMachine != null ? new Color(0.18f, 0.48f, 0.72f) : new Color(0.30f, 0.30f, 0.30f));
            DrawBadge(ref x, chipY, Bool("enableAimIK") ? "瞄准开" : "瞄准关", Bool("enableAimIK") ? new Color(0.45f, 0.36f, 0.74f) : new Color(0.30f, 0.30f, 0.30f));
            if (!compact)
            {
                DrawBadge(ref x, chipY, Bool("enableBipedIK") ? "四肢开" : "四肢关", Bool("enableBipedIK") ? new Color(0.18f, 0.48f, 0.72f) : new Color(0.30f, 0.30f, 0.30f));
                DrawBadge(ref x, chipY, Bool("enableFullBodyBipedIK") ? "反应开" : "反应关", Bool("enableFullBodyBipedIK") ? new Color(0.70f, 0.47f, 0.22f) : new Color(0.30f, 0.30f, 0.30f));
            }
        }

        private void DrawNavigation()
        {
            string[] tabs = { "配置", "诊断", "测试", "高级" };
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                selectedPage = (Page)GUILayout.Toolbar((int)selectedPage, tabs, GUILayout.Height(28f), GUILayout.MaxWidth(460f));
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawPageBanner()
        {
            string title;
            string summary;
            Color tint;

            switch (selectedPage)
            {
                case Page.Config:
                    title = "配置";
                    summary = "保存型参数、骨骼绑定和组件应用操作。";
                    tint = new Color(0.24f, 0.52f, 0.78f);
                    break;
                case Page.Diagnostics:
                    title = "诊断";
                    summary = "只读运行监测，不和配置混在一起。";
                    tint = new Color(0.44f, 0.40f, 0.80f);
                    break;
                case Page.Test:
                    title = "测试";
                    summary = "可开关的实时测试与可视化验证。";
                    tint = new Color(0.24f, 0.68f, 0.54f);
                    break;
                default:
                    title = "高级";
                    summary = "自动补齐、兜底和维护向开关。";
                    tint = new Color(0.82f, 0.52f, 0.18f);
                    break;
            }

            bool compact = EditorGUIUtility.currentViewWidth < 620f;
            Rect rect = GUILayoutUtility.GetRect(0f, compact ? 42f : 28f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.19f, 0.20f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), tint);
            if (compact)
            {
                GUI.Label(new Rect(rect.x + 12f, rect.y + 5f, 90f, 18f), title, cardTitleStyle);
                GUI.Label(new Rect(rect.x + 12f, rect.y + 21f, rect.width - 24f, 16f), summary, miniLabelStyle);
            }
            else
            {
                GUI.Label(new Rect(rect.x + 12f, rect.y + 5f, 90f, 18f), title, cardTitleStyle);
                GUI.Label(new Rect(rect.x + 94f, rect.y + 6f, rect.width - 106f, 16f), summary, miniLabelStyle);
            }
        }

        private void DrawCard(Fold fold, string title, string summary, Action content)
        {
            bool open = GetFold(fold);
            bool compact = EditorGUIUtility.currentViewWidth < 760f;
            Rect head = GUILayoutUtility.GetRect(0f, compact ? 52f : 34f, GUILayout.ExpandWidth(true));
            head = EditorGUI.IndentedRect(head);
            EditorGUI.DrawRect(head, new Color(0.23f, 0.24f, 0.25f));
            EditorGUI.DrawRect(new Rect(head.x, head.yMax - 1f, head.width, 1f), new Color(0.13f, 0.13f, 0.13f));

            var foldRect = new Rect(head.x + 8f, head.y + 7f, 18f, 18f);
            var titleRect = new Rect(head.x + 30f, head.y + 5f, compact ? head.width - 42f : 180f, 20f);
            var summaryRect = compact
                ? new Rect(head.x + 30f, head.y + 27f, head.width - 42f, 18f)
                : new Rect(head.x + 210f, head.y + 7f, head.width - 218f, 18f);
            EditorGUI.LabelField(foldRect, open ? "▼" : "▶", EditorStyles.miniLabel);
            GUI.Label(titleRect, title, cardTitleStyle);
            GUI.Label(summaryRect, summary, miniLabelStyle);

            if (Event.current.type == EventType.MouseDown && head.Contains(Event.current.mousePosition))
            {
                open = !open;
                Event.current.Use();
            }

            SetFold(fold, open);
            if (!open)
            {
                GUILayout.Space(4f);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Space(3f);
                content?.Invoke();
                GUILayout.Space(2f);
            }

            GUILayout.Space(6f);
        }

        private void DrawSubCard(string title, Action content)
        {
            GUILayout.Space(4f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                content?.Invoke();
            }
        }

        private void DrawBindingGrid()
        {
            DrawProperty("bindingRoot", "Root");
            DrawSubCard("身体", () =>
            {
                DrawProperty("bindingPelvis", "骨盆");
                DrawProperty("bindingSpine", "脊柱");
                DrawProperty("bindingChest", "胸腔");
                DrawProperty("bindingNeck", "颈部");
                DrawProperty("bindingHead", "头部");
            });
            DrawSubCard("眼睛", () =>
            {
                DrawProperty("bindingLeftEye", "左眼");
                DrawProperty("bindingRightEye", "右眼");
            });

            DrawSubCard("左臂", () =>
            {
                DrawProperty("bindingLeftUpperArm", "上臂");
                DrawProperty("bindingLeftForearm", "前臂");
                DrawProperty("bindingLeftHand", "手");
            });
            DrawSubCard("右臂", () =>
            {
                DrawProperty("bindingRightUpperArm", "上臂");
                DrawProperty("bindingRightForearm", "前臂");
                DrawProperty("bindingRightHand", "手");
            });
            DrawSubCard("左腿", () =>
            {
                DrawProperty("bindingLeftThigh", "大腿");
                DrawProperty("bindingLeftCalf", "小腿");
                DrawProperty("bindingLeftFoot", "脚");
            });
            DrawSubCard("右腿", () =>
            {
                DrawProperty("bindingRightThigh", "大腿");
                DrawProperty("bindingRightCalf", "小腿");
                DrawProperty("bindingRightFoot", "脚");
            });
        }

        private void DrawStatusGrid(StateFinalIKDriver driver)
        {
            DrawRowText("四肢定位", Ready(driver.IsBipedIKReady), "脚步贴地", Ready(driver.IsGrounderReady), "头眼注视", Ready(driver.IsLookAtIKReady));
            DrawRowText("武器瞄准", Ready(driver.IsAimIKReady), "全身反应", Ready(driver.IsFullBodyBipedIKReady), "受击反馈", Ready(driver.IsHitReactionReady));
            DrawRowText("后坐力", Ready(driver.IsRecoilReady));
        }

        private void DrawProperty(string propertyName, string label)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                EditorGUILayout.HelpBox("缺少字段: " + propertyName, MessageType.Warning);
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        }

        private void DrawRow(params string[] propertyAndLabels)
        {
            if (EditorGUIUtility.currentViewWidth < 760f)
            {
                for (int i = 0; i < propertyAndLabels.Length; i += 2)
                    DrawProperty(propertyAndLabels[i], propertyAndLabels[i + 1]);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                int pairCount = propertyAndLabels.Length / 2;
                for (int i = 0; i < propertyAndLabels.Length; i += 2)
                {
                    DrawPropertyInColumn(propertyAndLabels[i], propertyAndLabels[i + 1], pairCount);
                }
            }
        }

        private void DrawPropertyInColumn(string propertyName, string label, int columnCount)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                return;

            float width = Mathf.Max(120f, (EditorGUIUtility.currentViewWidth - 58f) / Mathf.Max(1, columnCount));
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(width)))
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
            }
        }

        private void ApplyGeneralCharacterPreset()
        {
            Undo.RecordObject(target, "Apply StateFinalIKDriver General Preset");

            SetBool("enableBipedIK", true);
            SetBool("enableGrounderBipedIK", true);
            SetBool("enableLookAtIK", true);
            SetBool("enableAimIK", false);
            SetBool("enableFullBodyBipedIK", false);
            SetBool("enableHitReaction", false);
            SetBool("enableRecoil", false);

            SetFloat("limbWeightSmoothTime", 0.12f);
            SetFloat("limbLerpingRateRecoverTime", 0.2f);
            SetFloat("footRotationWeightMultiplier", 0.2f);
            SetBool("driveGoalTargetsFromPose", true);
            SetFloat("grounderWeightSmoothTime", 0.12f);
            SetFloat("grounderLerpingRateRecoverTime", 0.2f);
            SetFloat("initGrounderWeight", 1f);
            SetFloat("initGrounderMaxStep", 0.5f);
            SetFloat("initGrounderSpeed", 3f);
            SetFloat("lookAtWeightSmoothTime", 0.12f);
            SetFloat("lookAtLerpingRateRecoverTime", 0.2f);
            SetFloat("initLookAtWeight", 1f);
            SetFloat("initLookAtHeadWeight", 1f);
            SetFloat("initLookAtEyesWeight", 1f);
            SetFloat("initLookAtSpineWeight", 0.5f);
            SetFloat("initLookAtClampWeight", 0.5f);

            SetCommonSafeDefaults();
            CommitPreset("已应用通用角色默认。");
        }

        private void ApplyShooterCharacterPreset()
        {
            Undo.RecordObject(target, "Apply StateFinalIKDriver Shooter Preset");

            SetBool("enableBipedIK", true);
            SetBool("enableGrounderBipedIK", true);
            SetBool("enableLookAtIK", true);
            SetBool("enableAimIK", true);
            SetBool("enableFullBodyBipedIK", true);
            SetBool("enableHitReaction", true);
            SetBool("enableRecoil", true);

            SetBool("useInitAimWeightOnBind", false);
            SetFloat("initAimWeight", 0f);
            SetFloat("initAimClampWeight", 0.5f);
            SetVector3("initAimAxis", Vector3.forward);
            SetFloat("aimIKHeartbeatTimeout", 0.5f);
            SetFloat("aimWeightSmoothTime", 0.10f);
            SetFloat("aimLerpingRateRecoverTime", 0.18f);
            SetFloat("aimIKDecayDuration", 0.28f);

            SetBool("useDriverRecoilSetup", true);
            SetFloat("driverRecoilWeight", 1f);
            SetFloat("driverRecoilDuration", 0.18f);
            SetFloat("driverRecoilBlendTime", 0.08f);
            SetFloat("driverRecoilMagnitudeRandom", 0.08f);
            SetBool("driverRecoilTwoHanded", true);
            SetVector3("driverRecoilPrimaryOffset", new Vector3(0f, 0.02f, -0.06f));
            SetVector3("driverRecoilSecondaryOffset", new Vector3(0f, 0.01f, -0.035f));
            SetVector3("driverRecoilBodyOffset", new Vector3(0f, 0f, -0.015f));
            SetVector3("driverRecoilHandRotationOffset", new Vector3(-8f, 0f, 0f));
            SetVector3("driverRecoilRotationRandom", new Vector3(1.5f, 0.8f, 0.8f));

            SetBool("useDriverHitReactionSetup", false);
            SetCommonSafeDefaults();
            CommitPreset("已应用射击角色默认。请继续填充统一骨骼绑定和瞄准方向节点。");
        }

        private void ApplyPerformanceTestPreset()
        {
            Undo.RecordObject(target, "Apply StateFinalIKDriver Performance Preset");

            SetBool("enableBipedIK", false);
            SetBool("enableGrounderBipedIK", false);
            SetBool("enableLookAtIK", false);
            SetBool("enableAimIK", false);
            SetBool("enableFullBodyBipedIK", false);
            SetBool("enableHitReaction", false);
            SetBool("enableRecoil", false);
            SetBool("enableRealtimeWeightTest", false);
            SetBool("debugDrawIKGizmosInSceneAndGame", false);
            SetBool("warnWhenPoseHasWeightButNoIK", false);
            SetBool("logMissingComponentHints", false);
            SetBool("useDriverHitReactionSetup", false);
            SetBool("useDriverRecoilSetup", false);
            SetFloat("rebindInterval", 1f);

            SetAutoAddDefaults(false);
            CommitPreset("已应用性能压测关闭预设。");
        }

        private void RunGeneralCharacterSetup(StateFinalIKDriver driver)
        {
            ApplyGeneralCharacterPreset();
            InvokeDriver(driver, "QuickAddComp_BipedIK");
            InvokeDriver(driver, "QuickAddComp_GrounderBipedIK");
            InvokeDriver(driver, "QuickAddComp_LookAtIK");
            InvokeDriver(driver, "AutoFillDriverBoneBinding");
            InvokeDriver(driver, "ApplyDriverBoneBindingFromInspector");
            Debug.Log("[StateFinalIKDriverEditor] 通用角色一键接入完成。", driver);
        }

        private void RunShooterCharacterSetup(StateFinalIKDriver driver)
        {
            ApplyShooterCharacterPreset();
            InvokeDriver(driver, "QuickAddComp_BipedIK");
            InvokeDriver(driver, "QuickAddComp_GrounderBipedIK");
            InvokeDriver(driver, "QuickAddComp_LookAtIK");
            InvokeDriver(driver, "QuickAddComp_AimIK");
            InvokeDriver(driver, "QuickAddComp_FullBodyBipedIK");
            InvokeDriver(driver, "QuickAddComp_HitReaction");
            InvokeDriver(driver, "QuickAddComp_Recoil");
            InvokeDriver(driver, "AutoFillDriverBoneBinding");
            InvokeDriver(driver, "ApplyDriverBoneBindingFromInspector");
            InvokeDriver(driver, "ApplyDriverAimChainFromInspector");
            InvokeDriver(driver, "ApplyDriverHitReactionFromInspector");
            InvokeDriver(driver, "ApplyDriverRecoilFromInspector");
            Debug.Log("[StateFinalIKDriverEditor] 射击角色一键接入完成。若瞄准方向不正确，请展开“武器瞄准”微调瞄准方向节点和瞄准轴。", driver);
        }

        private string BuildSetupStatus(StateFinalIKDriver driver)
        {
            int ready = 0;
            int enabled = 0;

            CountFeature(Bool("enableBipedIK"), driver.IsBipedIKReady, ref enabled, ref ready);
            CountFeature(Bool("enableGrounderBipedIK"), driver.IsGrounderReady, ref enabled, ref ready);
            CountFeature(Bool("enableLookAtIK"), driver.IsLookAtIKReady, ref enabled, ref ready);
            CountFeature(Bool("enableAimIK"), driver.IsAimIKReady, ref enabled, ref ready);
            CountFeature(Bool("enableFullBodyBipedIK"), driver.IsFullBodyBipedIKReady, ref enabled, ref ready);
            CountFeature(Bool("enableHitReaction"), driver.IsHitReactionReady, ref enabled, ref ready);
            CountFeature(Bool("enableRecoil"), driver.IsRecoilReady, ref enabled, ref ready);

            if (enabled == 0)
                return "当前未启用 IK 功能";

            return $"已启用 {enabled} 项，就绪 {ready} 项";
        }

        private static void CountFeature(bool isEnabled, bool isReady, ref int enabled, ref int ready)
        {
            if (!isEnabled)
                return;

            enabled++;
            if (isReady)
                ready++;
        }

        private void SetCommonSafeDefaults()
        {
            SetBool("autoDetectReferencesIfMissing", true);
            SetBool("warnWhenPoseHasWeightButNoIK", true);
            SetBool("enableRealtimeWeightTest", false);
            SetBool("debugDrawIKGizmosInSceneAndGame", false);
            SetFloat("rebindInterval", 0.5f);
            SetAutoAddDefaults(false);
        }

        private void SetAutoAddDefaults(bool enabled)
        {
            SetBool("autoAddBipedIK", enabled);
            SetBool("autoAddGrounderBipedIK", enabled);
            SetBool("autoAddLookAtIK", enabled);
            SetBool("autoAddAimIK", enabled);
            SetBool("autoAddFullBodyBipedIK", enabled);
            SetBool("autoAddHitReaction", enabled);
            SetBool("autoAddRecoil", enabled);
        }

        private void CommitPreset(string message)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            Debug.Log("[StateFinalIKDriverEditor] " + message, target);
            serializedObject.UpdateIfRequiredOrScript();
        }

        private void SetBool(string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Boolean)
                property.boolValue = value;
        }

        private void SetFloat(string propertyName, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Float)
                property.floatValue = value;
        }

        private void SetVector3(string propertyName, Vector3 value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Vector3)
                property.vector3Value = value;
        }

        private void DrawInfoLine(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(112f));
                EditorGUILayout.SelectableLabel(value ?? string.Empty, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private void DrawRowText(params string[] labelsAndValues)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < labelsAndValues.Length; i += 2)
                {
                    GUILayout.Label(labelsAndValues[i], GUILayout.Width(82f));
                    GUILayout.Label(labelsAndValues[i + 1], EditorStyles.boldLabel, GUILayout.Width(72f));
                }
            }
        }

        private void DrawButtons(params (string Label, Action Callback)[] buttons)
        {
            if (buttons == null || buttons.Length == 0)
                return;

            int columns = Mathf.Clamp(Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - 72f) / 162f), 1, buttons.Length);
            int index = 0;

            while (index < buttons.Length)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int column = 0; column < columns && index < buttons.Length; column++, index++)
                    {
                        if (GUILayout.Button(buttons[index].Label, GUILayout.Height(24f), GUILayout.ExpandWidth(true), GUILayout.MinWidth(112f)))
                            buttons[index].Callback?.Invoke();
                    }
                }

                GUILayout.Space(2f);
            }
        }

        private bool Bool(string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null && property.propertyType == SerializedPropertyType.Boolean && property.boolValue;
        }

        private bool GetFold(Fold fold)
        {
            return foldouts.TryGetValue(fold, out bool value) ? value : true;
        }

        private void SetFold(Fold fold, bool value)
        {
            foldouts[fold] = value;
        }

        private void DrawBadge(ref float x, float y, string text, Color color)
        {
            Vector2 size = badgeStyle.CalcSize(new GUIContent(text));
            Rect rect = new Rect(x, y, size.x + 18f, 20f);
            EditorGUI.DrawRect(rect, color);
            GUI.Label(rect, text, badgeStyle);
            x = rect.xMax + 6f;
        }

        private void InvokeDriver(StateFinalIKDriver driver, string methodName)
        {
            serializedObject.ApplyModifiedProperties();

            MethodInfo method = GetDriverMethod(methodName);
            if (method == null)
            {
                Debug.LogError("[StateFinalIKDriverEditor] 找不到方法: " + methodName, driver);
                return;
            }

            method.Invoke(driver, null);
            EditorUtility.SetDirty(driver);
            serializedObject.UpdateIfRequiredOrScript();
        }

        private static MethodInfo GetDriverMethod(string methodName)
        {
            if (MethodCache.TryGetValue(methodName, out MethodInfo cached))
                return cached;

            MethodInfo method = typeof(StateFinalIKDriver).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodCache[methodName] = method;
            return method;
        }

        private void EnsureStyles()
        {
            if (headerTitleStyle != null)
                return;

            headerTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                normal = { textColor = new Color(0.92f, 0.95f, 0.98f) }
            };

            subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.64f, 0.68f, 0.72f) }
            };

            badgeStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                padding = new RectOffset(7, 7, 1, 1)
            };

            cardTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.92f, 0.94f, 0.96f) }
            };

            miniLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.66f, 0.68f, 0.70f) },
                clipping = TextClipping.Clip
            };
        }

        private static string Ready(bool ready)
        {
            return ready ? "就绪" : "未就绪";
        }

        private static string SafeName(UnityEngine.Object obj)
        {
            return obj != null ? obj.name : "未绑定";
        }
    }
}
#endif
