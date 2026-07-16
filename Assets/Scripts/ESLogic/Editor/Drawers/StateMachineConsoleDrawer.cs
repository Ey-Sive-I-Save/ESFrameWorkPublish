#if UNITY_EDITOR
using System;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public sealed class StateMachineConsoleDrawer : OdinValueDrawer<StateMachine>
    {
        private int selectedPage;
        private Vector2 stateScroll;
        private Vector2 layerScroll;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            StateMachine machine = ValueEntry.SmartValue;
            if (machine == null)
            {
                CallNextDrawer(label);
                return;
            }

            BeginOuterFrame();
            try
            {
                DrawHeader(machine, label);
                DrawToolbar();

                DrawSeparator();

                switch (selectedPage)
                {
                    case 0:
                        DrawConfigPage(machine);
                        break;
                    case 1:
                        DrawMonitorPage(machine);
                        break;
                    case 2:
                        DrawTestPage(machine);
                        break;
                    default:
                        DrawDiagnosticsPage(machine);
                        break;
                }
            }
            finally
            {
                EndOuterFrame();
            }
        }

        private void DrawHeader(StateMachine machine, GUIContent label)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 44f);
            string title = !string.IsNullOrEmpty(machine.stateMachineKey) ? machine.stateMachineKey : "未命名状态机";
            string propertyName = label != null && !string.IsNullOrEmpty(label.text) ? label.text : Property.NiceName;

            EditorGUI.DrawRect(rect, new Color(0.11f, 0.13f, 0.15f, 0.95f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), new Color(0.30f, 0.62f, 0.88f, 1f));

            Rect titleRect = new Rect(rect.x + 12f, rect.y + 4f, rect.width * 0.45f, 20f);
            Rect subtitleRect = new Rect(rect.x + 12f, rect.y + 24f, rect.width * 0.45f, 16f);
            Rect chipsRect = new Rect(rect.x + rect.width * 0.48f, rect.y + 8f, rect.width * 0.5f, 24f);

            EditorGUI.LabelField(titleRect, title, SirenixGUIStyles.BoldTitle);
            EditorGUI.LabelField(subtitleRect, propertyName, SirenixGUIStyles.LeftAlignedGreyMiniLabel);

            float x = chipsRect.x;
            DrawChip(ref x, chipsRect.y, machine.isInitialized ? "已初始化" : "未初始化", machine.isInitialized ? StatusColor.Green : StatusColor.Grey);
            DrawChip(ref x, chipsRect.y, machine.isRunning ? "运行中" : "未运行", machine.isRunning ? StatusColor.Green : StatusColor.Grey);
            DrawChip(ref x, chipsRect.y, $"支持状态 {machine.currentSupportFlags}", StatusColor.Blue);
            DrawChip(ref x, chipsRect.y, $"状态 {machine.RegisteredStateCount}", StatusColor.Grey);
            DrawChip(ref x, chipsRect.y, $"压制 {machine.WeakInterruptRelationCount}", machine.WeakInterruptRelationCount > 0 ? StatusColor.Blue : StatusColor.Grey);
        }

        private void DrawToolbar()
        {
            string[] pages = { "配置", "监控", "测试", "诊断" };
            selectedPage = GUILayout.Toolbar(selectedPage, pages, GUILayout.Height(28f));
        }

        private void DrawConfigPage(StateMachine machine)
        {
            BeginCard("基础配置");
            machine.stateMachineKey = EditorGUILayout.TextField("状态机标识", machine.stateMachineKey);
            machine.Config = (StateMachineConfig)EditorGUILayout.ObjectField("状态机配置", machine.Config, typeof(StateMachineConfig), false);
            machine.defaultStateKey = EditorGUILayout.TextField("默认状态", machine.defaultStateKey);
            EndCard();

            BeginCard("合并规则");
            machine.weakInterruptSuppressedWeightFactor = EditorGUILayout.Slider("弱打断压制权重", machine.weakInterruptSuppressedWeightFactor, 0f, 1f);
            EditorGUILayout.HelpBox("TryWeakInterrupt 会保留旧状态运行，只在混合权重上压低旧状态；新状态退出后自动恢复。", MessageType.None);
            EndCard();

            BeginCard("层级遮罩");
            machine.upperBodyMask = (AvatarMask)EditorGUILayout.ObjectField("上半身遮罩", machine.upperBodyMask, typeof(AvatarMask), false);
            machine.lowerBodyMask = (AvatarMask)EditorGUILayout.ObjectField("下半身遮罩", machine.lowerBodyMask, typeof(AvatarMask), false);
            machine.referencePoseClip = (AnimationClip)EditorGUILayout.ObjectField("参考姿态动画", machine.referencePoseClip, typeof(AnimationClip), false);
            EndCard();
        }

        private void DrawMonitorPage(StateMachine machine)
        {
            BeginCard("运行总览");
            EditorGUILayout.LabelField("运行状态", machine.isRunning ? "运行中" : "未运行");
            EditorGUILayout.LabelField("支持状态", machine.currentSupportFlags.ToString());
            EditorGUILayout.LabelField("注册状态", machine.RegisteredStateCount.ToString());
            EditorGUILayout.LabelField("运行状态", machine.GetRunningStateCount().ToString());
            EditorGUILayout.LabelField("激活事件", machine.ActivationEventCount.ToString());
            EditorGUILayout.LabelField("弱打断压制", machine.WeakInterruptRelationCount.ToString());
            EndCard();

            if (machine.WeakInterruptRelationCount > 0)
            {
                BeginCard("压制关系");
                EditorGUILayout.HelpBox(machine.GetWeakInterruptSummary(), MessageType.Info);
                EndCard();
            }

            BeginCard("层级概览");
            layerScroll = EditorGUILayout.BeginScrollView(layerScroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(190f));
            foreach (StateLayerRuntime layer in machine.LayerRuntimes)
            {
                if (layer == null) continue;
                DrawLayerRow(layer);
            }
            EditorGUILayout.EndScrollView();
            EndCard();

            BeginCard("运行状态列表");
            stateScroll = EditorGUILayout.BeginScrollView(stateScroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(220f));
            foreach (var kvp in machine.EnumerateRegisteredStatesByKey())
            {
                StateBase state = kvp.Value;
                if (state == null || state.baseStatus != StateBaseStatus.Running) continue;
                DrawStateRow(machine, kvp.Key, state);
            }
            EditorGUILayout.EndScrollView();
            EndCard();
        }

        private void DrawTestPage(StateMachine machine)
        {
            BeginCard("临时动画测试");
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                machine.testTempKey = EditorGUILayout.TextField("临时状态标识", machine.testTempKey);
                machine.testClip = (AnimationClip)EditorGUILayout.ObjectField("动画剪辑", machine.testClip, typeof(AnimationClip), false);
                machine.testLayer = (StateLayerType)EditorGUILayout.EnumPopup("目标层级", machine.testLayer);
                machine.testSpeed = EditorGUILayout.Slider("播放速度", machine.testSpeed, 0.1f, 3f);
                machine.testLoopable = EditorGUILayout.Toggle("循环播放", machine.testLoopable);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("添加临时动画", GUILayout.Height(26f)))
                {
                    if (machine.testClip == null)
                        Debug.LogError("请先指定动画剪辑。");
                    else
                        machine.AddTemporaryAnimation(machine.testTempKey, machine.testClip, machine.testLayer, machine.testSpeed, machine.testLoopable);
                }
                if (GUILayout.Button("移除临时动画", GUILayout.Height(26f)))
                {
                    machine.RemoveTemporaryAnimation(machine.testTempKey);
                }
                if (GUILayout.Button("清空临时动画", GUILayout.Height(26f)))
                {
                    machine.ClearAllTemporaryAnimations();
                }
                EditorGUILayout.EndHorizontal();
            }

            if (!Application.isPlaying)
                SirenixEditorGUI.WarningMessageBox("临时动画测试仅在运行时生效。");

            EditorGUILayout.LabelField("临时动画数量", machine.GetTemporaryAnimationCount().ToString());
            EndCard();
        }

        private void DrawDiagnosticsPage(StateMachine machine)
        {
            BeginCard("调试输出");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("输出调试信息", GUILayout.Height(26f)))
                Debug.Log(machine.GetDebugInfo());

            if (GUILayout.Button("输出根混合器信息", GUILayout.Height(26f)))
                Debug.Log(machine.GetRootMixerDebugInfo());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("输出所有状态", GUILayout.Height(26f)))
                PrintAllStates(machine);

            if (GUILayout.Button("输出临时动画", GUILayout.Height(26f)))
                PrintTemporaryAnimations(machine);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("输出 IK 诊断", GUILayout.Height(26f)))
                Debug.Log(machine.GetIKDebugInfo());

            if (GUILayout.Button("输出 MatchTarget 诊断", GUILayout.Height(26f)))
                Debug.Log(machine.GetMatchTargetDebugInfo());
            EditorGUILayout.EndHorizontal();
            EndCard();

            DrawIKSnapshotCard(machine);

            BeginCard("高级信息");
            EditorGUILayout.LabelField("动画图", machine.IsPlayableGraphValid ? "有效" : "无效");
            EditorGUILayout.LabelField("播放状态", machine.IsPlayableGraphPlaying ? "播放中" : "未播放");
            EditorGUILayout.HelpBox("如需查看完整原始字段，可暂时禁用 StateMachineConsoleDrawer 或用调试输出定位。", MessageType.Info);
            EndCard();
        }

        private static void DrawIKSnapshotCard(StateMachine machine)
        {
            BeginCard("IK 运行快照");
            StateGeneralFinalIKDriverPose pose = machine.stateGeneralFinalIKDriverPose;
            EditorGUILayout.LabelField("总状态", pose.HasAnyWeight ? "有激活 IK 权重" : "无激活 IK 权重");
            DrawIKGoalRow("左手", pose.leftHand);
            DrawIKGoalRow("右手", pose.rightHand);
            DrawIKGoalRow("左脚", pose.leftFoot);
            DrawIKGoalRow("右脚", pose.rightFoot);
            DrawLookAtRow(pose);

            if (!pose.HasAnyWeight)
                EditorGUILayout.HelpBox("当前 StateMachine 没有输出 IK 权重。若状态已启用 IK，请检查状态 Runtime IK 权重、层级权重、弱压制和 StateFinalIKDriver 绑定。", MessageType.None);
            EndCard();
        }

        private static void DrawIKGoalRow(string label, IKGoalPose goal)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 28f);
            Rect nameRect = new Rect(rect.x + 6f, rect.y + 4f, 46f, 18f);
            Rect barRect = new Rect(nameRect.xMax + 8f, rect.y + 7f, 120f, 12f);
            Rect weightRect = new Rect(barRect.xMax + 8f, rect.y + 4f, 92f, 18f);
            Rect posRect = new Rect(weightRect.xMax + 8f, rect.y + 4f, Mathf.Max(120f, rect.width - weightRect.xMax - 14f), 18f);

            bool active = goal.HasAnyWeight;
            EditorGUI.DrawRect(rect, active ? new Color(0.10f, 0.22f, 0.18f, 0.28f) : new Color(0.13f, 0.13f, 0.13f, 0.22f));
            EditorGUI.LabelField(nameRect, label, EditorStyles.boldLabel);
            DrawMiniWeightBar(barRect, Mathf.Max(goal.weight, goal.rotationWeight), active ? new Color(0.36f, 0.78f, 0.44f) : new Color(0.35f, 0.35f, 0.35f));
            EditorGUI.LabelField(weightRect, $"位 {goal.weight:F2} / 旋 {goal.rotationWeight:F2}", SirenixGUIStyles.LeftAlignedGreyMiniLabel);
            EditorGUI.LabelField(posRect, $"目标 {goal.position:F2}    Hint {goal.hintPosition:F2}    速度 {goal.lerpingRate:F2}", SirenixGUIStyles.LeftAlignedGreyMiniLabel);
        }

        private static void DrawLookAtRow(StateGeneralFinalIKDriverPose pose)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 28f);
            Rect nameRect = new Rect(rect.x + 6f, rect.y + 4f, 46f, 18f);
            Rect barRect = new Rect(nameRect.xMax + 8f, rect.y + 7f, 120f, 12f);
            Rect weightRect = new Rect(barRect.xMax + 8f, rect.y + 4f, 92f, 18f);
            Rect posRect = new Rect(weightRect.xMax + 8f, rect.y + 4f, Mathf.Max(120f, rect.width - weightRect.xMax - 14f), 18f);

            bool active = pose.lookAtWeight > 0.001f;
            EditorGUI.DrawRect(rect, active ? new Color(0.10f, 0.18f, 0.26f, 0.32f) : new Color(0.13f, 0.13f, 0.13f, 0.22f));
            EditorGUI.LabelField(nameRect, "注视", EditorStyles.boldLabel);
            DrawMiniWeightBar(barRect, pose.lookAtWeight, active ? new Color(0.34f, 0.62f, 0.92f) : new Color(0.35f, 0.35f, 0.35f));
            EditorGUI.LabelField(weightRect, $"权重 {pose.lookAtWeight:F2}", SirenixGUIStyles.LeftAlignedGreyMiniLabel);
            EditorGUI.LabelField(posRect, $"目标 {pose.lookAtPosition:F2}    速度 {pose.lookAtLerpingRate:F2}    身/头/眼/限 {pose.lookAtBodyWeight:F2}/{pose.lookAtHeadWeight:F2}/{pose.lookAtEyesWeight:F2}/{pose.lookAtClampWeight:F2}", SirenixGUIStyles.LeftAlignedGreyMiniLabel);
        }

        private static void DrawMiniWeightBar(Rect rect, float weight, Color fillColor)
        {
            float value = Mathf.Clamp01(weight);
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.08f, 1f));
            Rect fill = new Rect(rect.x, rect.y, rect.width * value, rect.height);
            EditorGUI.DrawRect(fill, fillColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(0.35f, 0.35f, 0.35f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0.35f, 0.35f, 0.35f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), new Color(0.35f, 0.35f, 0.35f));
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), new Color(0.35f, 0.35f, 0.35f));
        }

        private static void DrawLayerRow(StateLayerRuntime layer)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 26f);
            Rect name = new Rect(rect.x + 6f, rect.y + 4f, 90f, 18f);
            Rect enabled = new Rect(name.xMax + 8f, rect.y + 4f, 58f, 18f);
            Rect weight = new Rect(enabled.xMax + 8f, rect.y + 4f, 78f, 18f);
            Rect stats = new Rect(weight.xMax + 8f, rect.y + 4f, rect.width - weight.xMax - 20f, 18f);

            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f, 0.35f));
            EditorGUI.LabelField(name, layer.layerType.ToString(), EditorStyles.boldLabel);
            EditorGUI.LabelField(enabled, layer.isEnabled ? "启用" : "禁用", SirenixGUIStyles.LeftAlignedGreyMiniLabel);
            EditorGUI.LabelField(weight, $"权重 {layer.weight:F2}", SirenixGUIStyles.LeftAlignedGreyMiniLabel);
            EditorGUI.LabelField(stats,
                $"运行 {layer.runningStates.Count}    节点 {layer.stateToSlotMap.Count}    淡入 {layer.fadeInStates.Count}    淡出 {layer.fadeOutStates.Count}    刷新 {layer.dirtyFlags}",
                SirenixGUIStyles.LeftAlignedGreyMiniLabel);
        }

        private static void DrawStateRow(StateMachine machine, string key, StateBase state)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 24f);
            Rect keyRect = new Rect(rect.x + 6f, rect.y + 3f, 190f, 18f);
            Rect statusRect = new Rect(keyRect.xMax + 8f, rect.y + 3f, 90f, 18f);
            Rect weightRect = new Rect(statusRect.xMax + 8f, rect.y + 3f, 90f, 18f);
            Rect typeRect = new Rect(weightRect.xMax + 8f, rect.y + 3f, rect.width - weightRect.xMax - 14f, 18f);

            EditorGUI.DrawRect(rect, new Color(0.10f, 0.22f, 0.18f, 0.28f));
            EditorGUI.LabelField(keyRect, key, EditorStyles.boldLabel);
            EditorGUI.LabelField(statusRect, state.baseStatus.ToString(), SirenixGUIStyles.LeftAlignedGreyMiniLabel);
            EditorGUI.LabelField(weightRect, $"权重 {state.PlayableWeight:F2}", SirenixGUIStyles.LeftAlignedGreyMiniLabel);
            string typeText = machine != null && machine.IsStateWeakSuppressed(state)
                ? $"{state.GetType().Name}  /  压制中"
                : state.GetType().Name;
            EditorGUI.LabelField(typeRect, typeText, SirenixGUIStyles.LeftAlignedGreyMiniLabel);
        }

        private static void BeginCard(string title)
        {
            EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            DrawSeparator();
        }

        private static void EndCard()
        {
            EditorGUILayout.EndVertical();
            GUILayout.Space(4f);
        }

        private static void BeginOuterFrame()
        {
            GUILayout.Space(2f);
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            Rect top = EditorGUILayout.GetControlRect(false, 3f);
            EditorGUI.DrawRect(top, new Color(0.30f, 0.62f, 0.88f, 1f));

            Color old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.72f, 0.80f, 0.88f, 1f);
            EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);
            GUI.backgroundColor = old;

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Space(2f);
        }

        private static void EndOuterFrame()
        {
            GUILayout.Space(2f);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();

            Rect bottom = EditorGUILayout.GetControlRect(false, 10f);
            EditorGUI.DrawRect(new Rect(bottom.x, bottom.y, bottom.width, 3f), new Color(0.30f, 0.62f, 0.88f, 1f));
            EditorGUI.DrawRect(new Rect(bottom.x, bottom.y + 3f, bottom.width, 7f), new Color(0.11f, 0.13f, 0.15f, 0.95f));
            EditorGUI.DrawRect(new Rect(bottom.x, bottom.y, 4f, bottom.height), new Color(0.30f, 0.62f, 0.88f, 1f));
            EditorGUI.DrawRect(new Rect(bottom.xMax - 4f, bottom.y, 4f, bottom.height), new Color(0.30f, 0.62f, 0.88f, 1f));

            EditorGUILayout.EndVertical();
            GUILayout.Space(2f);
        }

        private static void DrawSeparator()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, new Color(0.28f, 0.28f, 0.28f, 0.65f));
        }

        private static void DrawChip(ref float x, float y, string text, StatusColor color)
        {
            Vector2 size = EditorStyles.miniButton.CalcSize(new GUIContent(text));
            size.x += 12f;
            Rect rect = new Rect(x, y, size.x, 20f);
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = GetColor(color);
            GUI.Label(rect, text, EditorStyles.miniButton);
            GUI.backgroundColor = old;
            x += size.x + 6f;
        }

        private static Color GetColor(StatusColor color)
        {
            switch (color)
            {
                case StatusColor.Green: return new Color(0.55f, 0.85f, 0.62f);
                case StatusColor.Blue: return new Color(0.55f, 0.72f, 0.95f);
                default: return new Color(0.72f, 0.72f, 0.72f);
            }
        }

        private static void PrintAllStates(StateMachine machine)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("========== 所有注册状态 ==========");
            foreach (var kvp in machine.EnumerateRegisteredStatesByKey())
            {
                StateBase state = kvp.Value;
                sb.AppendLine(state != null
                    ? $"[{kvp.Key}] -> {state.GetType().Name} (运行:{state.baseStatus == StateBaseStatus.Running})"
                    : $"[{kvp.Key}] -> <null>");
            }
            Debug.Log(sb.ToString());
        }

        private static void PrintTemporaryAnimations(StateMachine machine)
        {
            Debug.Log($"[TempAnim] 当前临时动画数量: {machine.GetTemporaryAnimationCount()}");
        }

        private enum StatusColor
        {
            Grey,
            Green,
            Blue
        }
    }
}
#endif
