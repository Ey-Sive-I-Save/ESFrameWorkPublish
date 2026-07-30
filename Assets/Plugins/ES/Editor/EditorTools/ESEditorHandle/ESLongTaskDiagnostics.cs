using System;
using System.Diagnostics;
using UnityEditor;

namespace ES
{
    /// <summary>仅用于验证 ESEditorLongTask 的进度、取消和编辑器响应；不会修改任何项目资产。</summary>
    internal static class ESLongTaskDiagnostics
    {
        private const string TestTaskKey = "ES.Diagnostics.LongTask.ReadSettings";

        [MenuItem("【ES】/开发与维护/性能诊断/运行长任务五秒测试")]
        private static void EnqueueFiveSecondTest()
        {
            EnqueueSettingsReadTest(5d, 120);
        }

        public static ESEditorLongTask EnqueueSettingsReadTest(double durationSeconds, int readCount)
        {
            if (durationSeconds <= 0d) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            if (readCount <= 0) throw new ArgumentOutOfRangeException(nameof(readCount));
            return ESEditorHandle.EnqueueLongTask(new ESGlobalSettingsReadTestLongTask(durationSeconds, readCount));
        }

        private sealed class ESGlobalSettingsReadTestLongTask : ESEditorLongTask
        {
            private readonly double durationSeconds;
            private readonly int readCount;
            private readonly Stopwatch stopwatch = new Stopwatch();
            private int readsCompleted;

            public ESGlobalSettingsReadTestLongTask(double durationSeconds, int readCount)
                : base("ES 长任务五秒测试", TestTaskKey)
            {
                this.durationSeconds = durationSeconds;
                this.readCount = readCount;
            }

            public override ESEditorLongTaskStepResult ProcessStep(ESEditorLongTaskContext context)
            {
                if (!stopwatch.IsRunning) stopwatch.Start();
                double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                int targetReads = Math.Min(readCount, (int)Math.Floor(elapsedSeconds / durationSeconds * readCount));
                while (readsCompleted < targetReads)
                {
                    // 只读全局配置，验证长任务跨帧执行，不写入、不保存、不触发资源刷新。
                    _ = ESGlobalResSetting.Instance;
                    readsCompleted++;
                    if (context.IsFrameBudgetExceeded) break;
                }
                SetProgress(readsCompleted, readCount, "读取全局资源设置 " + readsCompleted + "/" + readCount + "，已运行 " + elapsedSeconds.ToString("F1") + " 秒");
                if (elapsedSeconds < durationSeconds) return ESEditorLongTaskStepResult.Continue;

                while (readsCompleted < readCount)
                {
                    _ = ESGlobalResSetting.Instance;
                    readsCompleted++;
                    if (context.IsFrameBudgetExceeded) return ESEditorLongTaskStepResult.Continue;
                }
                stopwatch.Stop();
                UnityEngine.Debug.Log("[ESLongTask] 五秒测试完成：读取 ESGlobalResSetting " + readCount + " 次。");
                return ESEditorLongTaskStepResult.Complete;
            }

            protected override void OnFinish()
            {
                stopwatch.Stop();
                if (Status == ESEditorLongTaskStatus.Cancelled)
                    UnityEngine.Debug.Log("[ESLongTask] 五秒测试已取消：已读取 " + readsCompleted + "/" + readCount + " 次。");
            }
        }
    }
}
