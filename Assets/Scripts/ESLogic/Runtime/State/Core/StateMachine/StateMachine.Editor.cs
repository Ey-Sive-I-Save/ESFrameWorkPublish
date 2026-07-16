#if UNITY_EDITOR
using Sirenix.OdinInspector;
using System;
using UnityEngine;

// ============================================================================
// 文件：StateMachine.Editor.cs
// 作用：StateMachine 的编辑器扩展（Inspector 调试显示、运行时临时动画测试等）。
//
// Public（本文件定义的对外成员；按模块分组，“先功能、后成员”，便于扫读）：
//
// 【持续统计输出】
// - 是否持续输出：public bool enableContinuousStats
//
// 【临时动画测试（热拔插）】
// - 临时状态标识：public string testTempKey
// - 测试 Clip：public AnimationClip testClip
// - 目标层级：public StateLayerType testLayer
// - 播放速度倍率：public float testSpeed
// - 是否循环：public bool testLoopable
//
// 【调试信息】
// - 获取运行时调试信息：public string GetDebugInfo()
//
// Private/Internal：Inspector 初始化与按钮回调（仅编辑器可用）。
// ============================================================================

namespace ES
{
    public partial class StateMachine
    {
        /// <summary>
        /// 是否持续输出统计信息（用于调试）
        /// </summary>
        [TabGroup("SM_View", "诊断", Order = 3, TextColor = "@ESDesignUtility.ColorSelector.GetColor(\"雾橙\")")]
        [BoxGroup("SM_View/诊断/持续统计", ShowLabel = true)]
        [LabelText("持续输出统计"), Tooltip("每帧在控制台输出状态机统计信息")]
        [NonSerialized]
        public bool enableContinuousStats = false;

        [OnInspectorInit]
        private void EditorInitConfig()
        {
            if (config == null)
            {
                config = StateMachineConfig.Instance;
            }
        }

        /// <summary>
        /// 输出持续统计信息 - 简洁版，不干扰游戏运行
        /// </summary>
        private void OutputContinuousStats()
        {
            var sb = _continuousStatsBuilder;
            sb.Clear();
            sb.Append($"[Stats] 运行:{runningStates.Count} |");

            foreach (var layer in GetAllLayers())
            {
                if (layer.runningStates.Count > 0)
                {
                    sb.Append($" {layer.layerType}:{layer.runningStates.Count}");
                }
            }

            if (runningStates.Count > 0)
            {
                sb.Append(" | 状态:");
                foreach (var state in runningStates)
                {
                    sb.Append($" [{state.strKey}]");
                }
            }

            Debug.Log(sb.ToString());
        }

        [TabGroup("SM_View", "运行监控", Order = 1, TextColor = "@ESDesignUtility.ColorSelector.GetColor(\"雾绿\")")]
        [BoxGroup("SM_View/运行监控/运行总览")]
        [ShowInInspector, ReadOnly, LabelText("运行摘要"), MultiLineProperty(5)]
        private string InspectorRuntimeSummary
        {
            get
            {
                int connected = 0;
                int fadeIn = 0;
                int fadeOut = 0;
                foreach (var layer in GetAllLayers())
                {
                    if (layer == null) continue;
                    connected += layer.stateToSlotMap != null ? layer.stateToSlotMap.Count : 0;
                    fadeIn += layer.fadeInStates != null ? layer.fadeInStates.Count : 0;
                    fadeOut += layer.fadeOutStates != null ? layer.fadeOutStates.Count : 0;
                }

                return
                    $"状态机：{stateMachineKey}\n" +
                    $"初始化：{(isInitialized ? "是" : "否")}    运行：{(isRunning ? "运行中" : "未运行")}    支持状态：{currentSupportFlags}\n" +
                    $"注册状态：{RegisteredStateCount}    运行状态：{runningStates.Count}    已连接动画节点：{connected}    弱打断压制：{WeakInterruptRelationCount}\n" +
                    $"淡入：{fadeIn}    淡出：{fadeOut}    刷新版本：{_dirtyVersion}    最近刷新原因：{_lastDirtyReason}\n" +
                    $"动画图：{(playableGraph.IsValid() ? "有效" : "无效")} / {(playableGraph.IsValid() && playableGraph.IsPlaying() ? "播放中" : "未播放")}";
            }
        }

        [TabGroup("SM_View", "运行监控")]
        [BoxGroup("SM_View/运行监控/运行总览")]
        [ShowInInspector, ReadOnly, LabelText("弱打断关系"), MultiLineProperty(3)]
        private string InspectorWeakInterruptSummary => GetWeakInterruptSummary();

        [TabGroup("SM_View", "运行监控")]
        [BoxGroup("SM_View/运行监控/层级概览", ShowLabel = true)]
        [ShowInInspector, ReadOnly, LabelText("层级摘要"), MultiLineProperty(7)]
        private string InspectorLayerSummary
        {
            get
            {
                var sb = _continuousStatsBuilder;
                sb.Clear();
                foreach (var layer in GetAllLayers())
                {
                    if (layer == null) continue;
                    int running = layer.runningStates != null ? layer.runningStates.Count : 0;
                    int connected = layer.stateToSlotMap != null ? layer.stateToSlotMap.Count : 0;
                    int fadeIn = layer.fadeInStates != null ? layer.fadeInStates.Count : 0;
                    int fadeOut = layer.fadeOutStates != null ? layer.fadeOutStates.Count : 0;
                    sb.Append(layer.layerType)
                        .Append("  启用:").Append(layer.isEnabled ? "是" : "否")
                        .Append("  权重:").Append(layer.weight.ToString("F2"))
                        .Append("  运行:").Append(running)
                        .Append("  连接:").Append(connected)
                        .Append("  淡入:").Append(fadeIn)
                        .Append("  淡出:").Append(fadeOut)
                        .Append("  刷新标记:").Append(layer.dirtyFlags)
                        .AppendLine();
                }
                return sb.ToString();
            }
        }

        // === 编辑器测试字段（临时动画热拔插）===
        [TabGroup("SM_View", "测试工具", Order = 2, TextColor = "@ESDesignUtility.ColorSelector.GetColor(\"雾黄\")")]
        [BoxGroup("SM_View/测试工具/参数", ShowLabel = true)]
        [InfoBox("临时动画测试仅在运行时生效。", InfoMessageType.Warning, "@!UnityEngine.Application.isPlaying")]
        [LabelText("临时状态标识"), Tooltip("临时状态的唯一标识")]
        public string testTempKey = "测试动画";

        [BoxGroup("SM_View/测试工具/参数")]
        [LabelText("动画剪辑"), AssetsOnly]
        public AnimationClip testClip;

        [BoxGroup("SM_View/测试工具/参数")]
        [LabelText("目标层级")]
        public StateLayerType testLayer = StateLayerType.Main;

        [BoxGroup("SM_View/测试工具/参数")]
        [LabelText("播放速度"), Range(0.1f, 3f)]
        public float testSpeed = 1.0f;

        [BoxGroup("SM_View/测试工具/参数")]
        [LabelText("循环播放"), Tooltip("勾选后动画循环播放，不勾选则播放一次后自动退出")]
        public bool testLoopable = false;

        [TabGroup("SM_View", "测试工具")]
        [BoxGroup("SM_View/测试工具/当前状态", ShowLabel = true)]
        [ShowInInspector, ReadOnly, LabelText("临时动画数量")]
        private int InspectorTemporaryAnimationCount => _temporaryStates != null ? _temporaryStates.Count : 0;

        [BoxGroup("SM_View/测试工具/操作", ShowLabel = true)]
        [Button("添加临时动画", ButtonSizes.Medium), GUIColor(0.4f, 0.8f, 1f)]
        private void EditorAddTemporaryAnimation()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("请在运行时测试！");
                return;
            }

            if (testClip == null)
            {
                Debug.LogError("请先指定动画剪辑！");
                return;
            }

            AddTemporaryAnimation(testTempKey, testClip, testLayer, testSpeed, testLoopable);
        }

        [BoxGroup("SM_View/测试工具/操作")]
        [Button("移除临时动画", ButtonSizes.Medium), GUIColor(1f, 0.7f, 0.4f)]
        private void EditorRemoveTemporaryAnimation()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("请在运行时测试！");
                return;
            }

            RemoveTemporaryAnimation(testTempKey);
        }

        #region 调试支持（可修改）

        /// <summary>
        /// 获取状态机调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"========== 状态机调试信息 ==========");
            sb.AppendLine($"状态机ID: {stateMachineKey}");
            sb.AppendLine($"运行状态: {(isRunning ? "运行中" : "已停止")}");
            sb.AppendLine($"宿主Entity: 无");
            sb.AppendLine($"\n========== 上下文信息 ==========");
            sb.AppendLine($"上下文ID: {(stateContext != null ? stateContext.contextID.ToString() : "")}");
            sb.AppendLine($"创建时间: {(stateContext != null ? stateContext.creationTime.ToString() : "")}");
            sb.AppendLine($"最后更新: {(stateContext != null ? stateContext.lastUpdateTime.ToString() : "")}");
            sb.AppendLine($"\n========== 状态统计 ==========");
            sb.AppendLine($"注册状态数(String): {stringToStateMap.Count}");
            sb.AppendLine($"注册状态数(Int): {intToStateMap.Count}");
            sb.AppendLine($"运行中状态总数: {runningStates.Count}");

            sb.AppendLine($"\n========== 层级状态 ==========");
            foreach (var layer in GetAllLayers())
            {
                sb.AppendLine($"- {layer.layerType}: {layer.runningStates.Count}个状态 | 权重:{layer.weight:F2} | {(layer.isEnabled ? "启用" : "禁用")}");
                foreach (var state in layer.runningStates)
                {
                    sb.AppendLine($"  └─ {state.strKey}");
                }
            }

            sb.AppendLine();
            sb.Append(GetIKDebugInfo());
            sb.AppendLine();
            sb.Append(GetMatchTargetDebugInfo());

            return sb.ToString();
        }

        public string GetIKDebugInfo()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(512);
            ref var pose = ref stateGeneralFinalIKDriverPose;
            sb.AppendLine("========== IK 诊断 ==========");
            sb.AppendLine($"总权重: {(pose.HasAnyWeight ? "有激活权重" : "无激活权重")}");
            AppendIKGoalSummary(sb, "左手", in pose.leftHand);
            AppendIKGoalSummary(sb, "右手", in pose.rightHand);
            AppendIKGoalSummary(sb, "左脚", in pose.leftFoot);
            AppendIKGoalSummary(sb, "右脚", in pose.rightFoot);
            sb.AppendLine($"LookAt | 权重={pose.lookAtWeight:F3}  速度={pose.lookAtLerpingRate:F2}  目标={pose.lookAtPosition:F3}  Body/Head/Eyes/Clamp={pose.lookAtBodyWeight:F2}/{pose.lookAtHeadWeight:F2}/{pose.lookAtEyesWeight:F2}/{pose.lookAtClampWeight:F2}");
            sb.AppendLine("贡献明细:");
            sb.AppendLine(string.IsNullOrEmpty(stateGeneralFinalIKContributionSummary) ? "未更新" : stateGeneralFinalIKContributionSummary);
            return sb.ToString();
        }

        private static void AppendIKGoalSummary(System.Text.StringBuilder sb, string label, in IKGoalPose goal)
        {
            sb.Append(label)
                .Append(" | 位置权重=").Append(goal.weight.ToString("F3"))
                .Append(" 旋转权重=").Append(goal.rotationWeight.ToString("F3"))
                .Append(" 速度=").Append(goal.lerpingRate.ToString("F2"))
                .Append(" 目标=").Append(goal.position.ToString("F3"))
                .Append(" Hint=").Append(goal.hintPosition.ToString("F3"))
                .AppendLine();
        }

        public string GetMatchTargetDebugInfo()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(512);
            sb.AppendLine("========== MatchTarget 诊断 ==========");
            int activeCount = 0;
            int inspectedCount = 0;
            foreach (var state in GetRunningStatesList())
            {
                if (state == null)
                    continue;

                inspectedCount++;
                if (state.IsMatchTargetActive)
                    activeCount++;

                sb.Append("  - ");
                sb.AppendLine(state.GetMatchTargetDebugSummary());
            }

            if (inspectedCount == 0)
                sb.AppendLine("  无运行状态");

            sb.AppendLine($"运行状态: {inspectedCount}    MatchTarget激活: {activeCount}");
            return sb.ToString();
        }

        [TabGroup("SM_View", "诊断")]
        [BoxGroup("SM_View/诊断/基础信息", ShowLabel = true)]
        [Button("输出调试信息", ButtonSizes.Large)]
        private void DebugPrint()
        {
            Debug.Log(GetDebugInfo());
        }

        [TabGroup("SM_View", "诊断")]
        [BoxGroup("SM_View/诊断/基础信息")]
        [Button("输出所有状态", ButtonSizes.Medium)]
        private void DebugPrintAllStates()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("========== 所有注册状态 ==========");
            foreach (var kvp in stringToStateMap)
            {
                sb.AppendLine($"[{kvp.Key}] -> {kvp.Value.GetType().Name} (运行:{kvp.Value.baseStatus == StateBaseStatus.Running})");
            }
            Debug.Log(sb.ToString());
        }

        [TabGroup("SM_View", "诊断")]
        [BoxGroup("SM_View/诊断/动画图诊断", ShowLabel = true)]
        [Button("输出根混合器信息", ButtonSizes.Medium)]
        private void DebugPrintRootMixer()
        {
            Debug.Log(GetRootMixerDebugInfo());
        }

        [TabGroup("SM_View", "诊断")]
        [BoxGroup("SM_View/诊断/动画图诊断")]
        [Button("输出 MatchTarget 诊断", ButtonSizes.Medium)]
        private void DebugPrintMatchTarget()
        {
            Debug.Log(GetMatchTargetDebugInfo());
        }

        [TabGroup("SM_View", "诊断")]
        [BoxGroup("SM_View/诊断/持续统计")]
        [Button("切换持续统计输出", ButtonSizes.Medium)]
        [GUIColor("@enableContinuousStats ? new Color(0.4f, 1f, 0.4f) : new Color(0.7f, 0.7f, 0.7f)")]
        private void ToggleContinuousStats()
        {
            enableContinuousStats = !enableContinuousStats;
            var dbg = StateMachineDebugSettings.Instance;
            if (dbg != null && dbg.IsStressTestSilentMode)
                return;

            Debug.Log($"[StateMachine] 持续统计输出: {(enableContinuousStats ? "开启" : "关闭")}");
        }

        [TabGroup("SM_View", "诊断")]
        [BoxGroup("SM_View/诊断/临时动画", ShowLabel = true)]
        [Button("打印临时动画列表", ButtonSizes.Medium)]
        private void DebugPrintTemporaryAnimations()
        {
            if (_temporaryStates.Count == 0)
            {
                Debug.Log("[TempAnim] 无临时动画");
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"========== 临时动画列表 ({_temporaryStates.Count}个) ==========");
            foreach (var kvp in _temporaryStates)
            {
                var state = kvp.Value;
                bool isRunning = state.baseStatus == StateBaseStatus.Running;
                var sharedData = state.stateSharedData;
                var animationConfig = sharedData != null ? sharedData.animationConfig : null;
                var clip = animationConfig != null ? animationConfig.calculator as StateAnimationMixCalculatorForSimpleClip : null;
                string clipName = "未知";
                if (clip != null && clip.clip != null)
                {
                    clipName = clip.clip.name;
                }
                sb.AppendLine($"[{kvp.Key}] Clip:{clipName} | 运行:{isRunning}");
            }
            Debug.Log(sb.ToString());
        }

        [TabGroup("SM_View", "诊断")]
        [BoxGroup("SM_View/诊断/临时动画")]
        [Button("清空临时动画", ButtonSizes.Medium)]
        private void DebugClearTemporaryAnimations()
        {
            ClearAllTemporaryAnimations();
        }

        #endregion
    }
}
#endif

