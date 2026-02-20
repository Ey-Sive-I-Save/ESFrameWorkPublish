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
// - 测试键：public string testTempKey
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

        // === 编辑器测试字段（临时动画热拔插）===
        [FoldoutGroup("临时动画测试", expanded: false)]
        [LabelText("测试键"), Tooltip("临时状态的唯一标识")]
        public string testTempKey = "测试动画";

        [FoldoutGroup("临时动画测试")]
        [LabelText("测试动画剪辑"), AssetsOnly]
        public AnimationClip testClip;

        [FoldoutGroup("临时动画测试")]
        [LabelText("目标层级")]
        public StateLayerType testLayer = StateLayerType.Main;

        [FoldoutGroup("临时动画测试")]
        [LabelText("播放速度"), Range(0.1f, 3f)]
        public float testSpeed = 1.0f;

        [FoldoutGroup("临时动画测试")]
        [LabelText("循环播放"), Tooltip("勾选后动画循环播放，不勾选则播放一次后自动退出")]
        public bool testLoopable = false;

        [FoldoutGroup("临时动画测试")]
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

        [FoldoutGroup("临时动画测试")]
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
            sb.AppendLine($"上下文ID: {stateContext?.contextID}");
            sb.AppendLine($"创建时间: {stateContext?.creationTime}");
            sb.AppendLine($"最后更新: {stateContext?.lastUpdateTime}");
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

            return sb.ToString();
        }

        [Button("输出调试信息", ButtonSizes.Large), PropertyOrder(-1)]
        private void DebugPrint()
        {
            Debug.Log(GetDebugInfo());
        }

        [Button("输出所有状态", ButtonSizes.Medium), PropertyOrder(-1)]
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

        [Button("测试RootMixer输出", ButtonSizes.Medium), PropertyOrder(-1)]
        private void DebugPrintRootMixer()
        {
            Debug.Log(GetRootMixerDebugInfo());
        }

        [Button("切换持续统计输出", ButtonSizes.Medium), PropertyOrder(-1)]
        [GUIColor("@enableContinuousStats ? new Color(0.4f, 1f, 0.4f) : new Color(0.7f, 0.7f, 0.7f)")]
        private void ToggleContinuousStats()
        {
            enableContinuousStats = !enableContinuousStats;
            Debug.Log($"[StateMachine] 持续统计输出: {(enableContinuousStats ? "开启" : "关闭")}");
        }

        [Button("打印临时动画列表", ButtonSizes.Medium), PropertyOrder(-1)]
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
                var clip = state.stateSharedData?.animationConfig?.calculator as StateAnimationMixCalculatorForSimpleClip;
                string clipName = clip?.clip?.name ?? "未知";
                sb.AppendLine($"[{kvp.Key}] Clip:{clipName} | 运行:{isRunning}");
            }
            Debug.Log(sb.ToString());
        }

        [Button("一键清除临时动画", ButtonSizes.Medium), PropertyOrder(-1)]
        private void DebugClearTemporaryAnimations()
        {
            ClearAllTemporaryAnimations();
        }

        #endregion
    }
}
#endif
