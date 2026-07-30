using System;
using System.Diagnostics;
using Sirenix.OdinInspector;
using UnityEngine;

#pragma warning disable 0414

namespace ES
{
    /// <summary>
    /// 专门验证 RuntimeWatch 的诊断视图。高成本和异常行为均需主动开启，
    /// 日常打开窗口不会制造额外噪音。
    /// </summary>
    [AddComponentMenu("ES Samples/Runtime Watch Video Case 5 - Diagnostics")]
    public class RuntimeWatchVideoCase_5_Diagnostics : MonoBehaviour
    {
        [Title("诊断控制")]
        [SerializeField, LabelText("显示观察项")]
        private bool visible = true;

        [SerializeField, LabelText("实时更新脉冲")]
        private bool liveUpdate = true;

        [SerializeField, LabelText("模拟慢 Getter")]
        private bool simulateSlowGetter;

        [SerializeField, LabelText("模拟读取失败")]
        private bool simulateReadFailure;

        [SerializeField, Range(2, 12), LabelText("模拟 Getter 耗时（毫秒）")]
        private int simulatedGetterCostMs = 3;

        [SerializeField, LabelText("操作次数")]
        private int operationCount;

        [SerializeField, LabelText("最近操作")]
        private string lastOperation = "等待诊断操作";

        private float pulse;
        private int transientSpike;

        [ESRuntimeWatch("视频案例/5 控制", "显示观察项", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("显示观察项")]
        public bool Visible
        {
            get => visible;
            set => visible = value;
        }

        [ESRuntimeWatch("视频案例/5 控制", "实时更新", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        [LabelText("实时更新")]
        public bool LiveUpdate
        {
            get => liveUpdate;
            set => liveUpdate = value;
        }

        [ESRuntimeWatch("视频案例/5 工业诊断", "诊断状态", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("诊断状态")]
        public string DiagnosticState => simulateReadFailure
            ? "读取异常已启用"
            : simulateSlowGetter
                ? $"慢 Getter 已启用（约 {simulatedGetterCostMs}ms）"
                : "健康 · 无异常";

        [ESRuntimeWatch("视频案例/5 工业诊断", "实时脉冲", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        [LabelText("实时脉冲")]
        public float Pulse => pulse;

        [ESRuntimeWatch("视频案例/5 工业诊断", "瞬时峰值", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        [LabelText("瞬时峰值")]
        public int TransientSpike => transientSpike;

        [ESRuntimeWatch("视频案例/5 工业诊断", "操作次数", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("操作次数")]
        public int OperationCount => operationCount;

        [ESRuntimeWatch("视频案例/5 工业诊断", "最近操作", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("最近操作")]
        public string LastOperation => lastOperation;

        [ESRuntimeWatch("视频案例/5 性能诊断", "受控慢 Getter", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        [LabelText("受控慢 Getter")]
        public string ControlledSlowGetter
        {
            get
            {
                if (!simulateSlowGetter)
                    return "正常（点击“切换慢 Getter”开始演示）";

                Stopwatch stopwatch = Stopwatch.StartNew();
                while (stopwatch.ElapsedMilliseconds < simulatedGetterCostMs)
                {
                    // 仅用于受控演示 RuntimeWatch 的慢 Getter 检测。
                }

                return $"本次读取约 {stopwatch.Elapsed.TotalMilliseconds:0.0}ms";
            }
        }

        [ESRuntimeWatch("视频案例/5 异常诊断", "受控读取异常", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [LabelText("受控读取异常")]
        public string ControlledReadFailure => simulateReadFailure
            ? throw new InvalidOperationException("RuntimeWatch 演示异常：这是可恢复的受控 Getter 错误。")
            : "正常（点击“切换读取异常”开始演示）";

        private void Update()
        {
            if (!liveUpdate)
                return;

            pulse = Mathf.PingPong(Time.time * 1.5f, 10f);
            if (transientSpike > 0)
                transientSpike--;
        }

        [ESRuntimeWatch("视频案例/5 诊断操作", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        [Button("切换慢 Getter")]
        [LabelText("切换慢 Getter")]
        public void ToggleSlowGetter()
        {
            simulateSlowGetter = !simulateSlowGetter;
            RecordOperation(simulateSlowGetter ? "已启用慢 Getter" : "已恢复正常 Getter");
        }

        [ESRuntimeWatch("视频案例/5 诊断操作", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [Button("切换读取异常")]
        [LabelText("切换读取异常")]
        public void ToggleReadFailure()
        {
            simulateReadFailure = !simulateReadFailure;
            RecordOperation(simulateReadFailure ? "已启用受控读取异常" : "已恢复正常读取");
        }

        [ESRuntimeWatch("视频案例/5 诊断操作", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        [Button("制造瞬时峰值")]
        [LabelText("制造瞬时峰值")]
        public void CreateTransientSpike()
        {
            transientSpike = 30;
            RecordOperation("已制造 30 帧瞬时峰值");
        }

        [ESRuntimeWatch("视频案例/5 诊断操作", showIf: "@this.visible", category: ESRuntimeWatchAttribute.CategoryDebug)]
        [Button("恢复全部诊断")]
        [LabelText("恢复全部诊断")]
        public void ResetDiagnostics()
        {
            simulateSlowGetter = false;
            simulateReadFailure = false;
            transientSpike = 0;
            RecordOperation("诊断状态已全部恢复");
        }

        [ESRuntimeWatch("视频案例/5 控制", category: ESRuntimeWatchAttribute.CategoryPerformance)]
        [Button("切换实时更新")]
        [LabelText("切换实时更新")]
        public void ToggleLiveUpdate()
        {
            liveUpdate = !liveUpdate;
        }

        private void RecordOperation(string operation)
        {
            operationCount++;
            lastOperation = operation;
        }
    }
}

#pragma warning restore 0414
