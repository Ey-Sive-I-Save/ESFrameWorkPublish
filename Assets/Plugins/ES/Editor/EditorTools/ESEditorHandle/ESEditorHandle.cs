using System;
using System.Collections.Generic;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace ES
{


    public class ESEditorHandle : EditorInvoker_Level0
    {
        public static readonly ESSimplePool<ESEditorHandleTask> TaskPool = new ESSimplePool<ESEditorHandleTask>(
            () => new ESEditorHandleTask(),
            (f) => { },
            5
            );
        public static readonly Dictionary<string, int> singleKeys = new Dictionary<string, int>();
        public static readonly Queue<ESEditorHandleTask> RunningTasks = new Queue<ESEditorHandleTask>();

        /// <summary>
        /// Editor 长任务必须串行执行。AssetDatabase、SerializedObject 等 Editor API 不能被多个任务交错修改。
        /// </summary>
        private static readonly List<ESEditorLongTask> LongTasks = new List<ESEditorLongTask>(8);
        private static readonly Dictionary<string, ESEditorLongTask> LongTaskById = new Dictionary<string, ESEditorLongTask>(8);
        private static readonly Stopwatch LongTaskStopwatch = new Stopwatch();

        /// <summary>单个长任务一次 Update 可占用的主线程预算，单位毫秒。</summary>
        public static double LongTaskFrameBudgetMilliseconds = 4d;

        private static int nextLongTaskId;
        private static bool registered;

        public override void InitInvoke()
        {
            // 不在 Domain Reload 后常驻 update。真正入队任务时再订阅，空闲后立即退订。
        }

        private static void RegisterUpdate()
        {
            if (registered)
                return;

            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            registered = true;
        }

        private static void UnregisterUpdateIfIdle()
        {
            if (!registered || RunningTasks.Count > 0 || LongTasks.Count > 0)
                return;

            EditorApplication.update -= Update;
            registered = false;
        }

        private static void Update()
        {
            ProcessSimpleTask();
            ProcessLongTask();
            UnregisterUpdateIfIdle();
        }

        private static void ProcessSimpleTask()
        {
            if (RunningTasks.Count > 0)
            {
                var useTask = RunningTasks.Peek();
                if (useTask != null)
                {
                    useTask.waitFrame--;
                    if (useTask.waitFrame < 0)
                    {
                        try
                        {
                            useTask.action?.Invoke();
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"执行 Editor Task 时发生异常: {e}");
                            // 回池会重置 SingleKey，必须先保存并解锁，否则面板会永久显示“任务执行中”。
                            string failedTaskKey = useTask.SingleKey;
                            if (!failedTaskKey.IsNullOrWhitespace())
                                singleKeys.Remove(failedTaskKey);
                            // 出错的 Task 必须从队列里踢出去，避免死循环
                            RunningTasks.Dequeue();
                            useTask.TryAutoPushedToPool();
                            return;
                        }
                        if (useTask.OnlyOnce || useTask.MaxFrame <= 0 || useTask.CanExit())
                        {
                            if (!useTask.SingleKey.IsNullOrWhitespace())
                            {
                                if (singleKeys.TryGetValue(useTask.SingleKey, out var flag))
                                {
                                    if (flag > 0) singleKeys[useTask.SingleKey] = -1;
                                }
                            }
                            useTask.TryAutoPushedToPool();
                            RunningTasks.Dequeue();
                        }
                        else
                        {
                            useTask.MaxFrame--;
                        }
                    }
                }
                else
                {
                    //useTask.TryAutoPushedToPool();
                    RunningTasks.Dequeue();
                }
            }
        }

        /// <summary>
        /// 入队一个可分帧推进的编辑器长任务。任务本身只处理一个小批次，下一帧再继续；
        /// 不允许在 ProcessStep 内部执行不可中断的大型全量扫描。
        /// </summary>
        public static ESEditorLongTask EnqueueLongTask(ESEditorLongTask task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            if (task.Status == ESEditorLongTaskStatus.Running || task.Status == ESEditorLongTaskStatus.Queued)
                throw new InvalidOperationException("同一个 ESEditorLongTask 已经处于队列中。");

            if (!task.Key.IsNullOrWhitespace() && HasRunningLongTaskKey(task.Key))
                return GetLongTaskByKey(task.Key);

            task.InternalPrepareForQueue("editor-long-" + (++nextLongTaskId));
            InsertLongTaskByPriority(task);
            LongTaskById[task.Id] = task;
            RegisterUpdate();
            return task;
        }

        public static bool TryGetLongTask(string id, out ESEditorLongTask task)
        {
            task = null;
            return !id.IsNullOrWhitespace() && LongTaskById.TryGetValue(id, out task);
        }

        public static bool CancelLongTask(string id)
        {
            if (!TryGetLongTask(id, out ESEditorLongTask task) || task.IsFinished)
                return false;

            task.Cancel();
            RegisterUpdate();
            return true;
        }

        private static bool HasRunningLongTaskKey(string key)
        {
            for (int i = 0; i < LongTasks.Count; i++)
            {
                ESEditorLongTask task = LongTasks[i];
                if (!task.IsFinished && task.Key == key)
                    return true;
            }

            return false;
        }

        private static ESEditorLongTask GetLongTaskByKey(string key)
        {
            for (int i = 0; i < LongTasks.Count; i++)
            {
                ESEditorLongTask task = LongTasks[i];
                if (!task.IsFinished && task.Key == key)
                    return task;
            }

            return null;
        }

        private static void InsertLongTaskByPriority(ESEditorLongTask task)
        {
            int index = LongTasks.Count;
            for (int i = 0; i < LongTasks.Count; i++)
            {
                if (task.Priority > LongTasks[i].Priority)
                {
                    index = i;
                    break;
                }
            }

            LongTasks.Insert(index, task);
        }

        private static void ProcessLongTask()
        {
            if (LongTasks.Count <= 0)
                return;

            ESEditorLongTask task = LongTasks[0];
            if (task == null)
            {
                LongTasks.RemoveAt(0);
                return;
            }

            if (task.IsCancellationRequested)
            {
                FinishLongTask(task, ESEditorLongTaskStatus.Cancelled, null);
                return;
            }

            task.InternalBeginRunning();
            LongTaskStopwatch.Restart();
            ESEditorLongTaskContext context = new ESEditorLongTaskContext(task, LongTaskStopwatch, LongTaskFrameBudgetMilliseconds);
            try
            {
                ESEditorLongTaskStepResult result = task.ProcessStep(context);
                if (task.IsCancellationRequested)
                {
                    FinishLongTask(task, ESEditorLongTaskStatus.Cancelled, null);
                    return;
                }

                switch (result)
                {
                    case ESEditorLongTaskStepResult.Complete:
                        FinishLongTask(task, ESEditorLongTaskStatus.Succeeded, null);
                        return;
                    case ESEditorLongTaskStepResult.Fail:
                        FinishLongTask(task, ESEditorLongTaskStatus.Failed, task.LastError ?? new InvalidOperationException("编辑器长任务返回 Fail。"));
                        return;
                }

                UpdateLongTaskProgress(task);
            }
            catch (Exception exception)
            {
                FinishLongTask(task, ESEditorLongTaskStatus.Failed, exception);
            }
            finally
            {
                LongTaskStopwatch.Stop();
            }
        }

        private static void UpdateLongTaskProgress(ESEditorLongTask task)
        {
            float progress = task.Progress.Normalized;
            if (EditorUtility.DisplayCancelableProgressBar(task.Name, task.Progress.Message, progress))
                task.Cancel();
        }

        private static void FinishLongTask(ESEditorLongTask task, ESEditorLongTaskStatus status, Exception error)
        {
            LongTasks.Remove(task);
            task.InternalFinish(status, error);
            LongTaskById.Remove(task.Id);
            EditorUtility.ClearProgressBar();
        }
        public static void AddSimpleHandleTask(Action c, int waitframe = 3, string key = "")
        {
            if (c == null) return; // ✨ 极简的非空保护，加在最前面
            RegisterUpdate();
            if (!key.IsNullOrWhitespace())
            {
                if (singleKeys.TryGetValue(key, out var flag))
                {
                    if (flag > 0) return;
                    else singleKeys[key] = 1;
                }
                else
                {
                    singleKeys.Add(key, 1);
                }
            }
            var use = TaskPool.GetInPool();
            use.SingleKey = key;
            use.waitFrame = waitframe;
            use.action = c;
            RunningTasks.Enqueue(use);
        }

        public static bool IsSimpleTaskKeyActive(string key)
        {
            return !key.IsNullOrWhitespace()
                && singleKeys.TryGetValue(key, out int state)
                && state > 0;
        }

        /// <summary>用于 UI 防重入；长任务按 Key 串行，查询不产生枚举器或额外分配。</summary>
        public static bool IsLongTaskKeyActive(string key)
        {
            return !key.IsNullOrWhitespace() && HasRunningLongTaskKey(key);
        }
        public static void AddRunningHandleTask(Action c, Func<bool> toExit, int MaxFrame = 1000, int waitframe = 3)
        {
            if (c == null) return; // ✨ 极简的非空保护，加在最前面
            RegisterUpdate();
            var use = TaskPool.GetInPool();
            use.waitFrame = waitframe;
            use.action = c;
            use.CanExit = toExit;
            use.OnlyOnce = toExit != null ? false : true;
            use.MaxFrame = MaxFrame;
            RunningTasks.Enqueue(use);
        }
        public static void ForceClearAllTasks()
        {
            while (RunningTasks.Count > 0)
            {
                ESEditorHandleTask task = RunningTasks.Dequeue();
                task?.TryAutoPushedToPool();
            }

            for (int i = 0; i < LongTasks.Count; i++)
            {
                ESEditorLongTask task = LongTasks[i];
                task?.Cancel();
                task?.InternalFinish(ESEditorLongTaskStatus.Cancelled, null);
            }

            LongTasks.Clear();
            LongTaskById.Clear();
            singleKeys.Clear();
            TaskPool.Clear();
            EditorUtility.ClearProgressBar();
            UnregisterUpdateIfIdle();
            Debug.Log("已强制清理所有 ESEditorHandle 任务与 Key");
        }
    }

    public class ESEditorHandleTask : IPoolableAuto
    {
        public string SingleKey = "";
        public int waitFrame = 2;
        public Action action;
        public bool OnlyOnce = true;
        public int MaxFrame = 1000;
        public Func<bool> CanExit = () => false;
        public bool IsRecycled { get; set; }

        public void OnResetAsPoolable()
        {
            SingleKey = string.Empty;
            waitFrame = 2;
            action = null;
            OnlyOnce = true;
            MaxFrame = 1000;
            CanExit = () => false;
        }

        public void TryAutoPushedToPool()
        {
            ESEditorHandle.TaskPool.PushToPool(this);
        }
    }

    public enum ESEditorLongTaskStatus
    {
        None = 0,
        Queued = 1,
        Running = 2,
        Succeeded = 3,
        Failed = 4,
        Cancelled = 5
    }

    public enum ESEditorLongTaskStepResult
    {
        Continue = 0,
        Complete = 1,
        Fail = 2
    }

    public readonly struct ESEditorLongTaskProgress
    {
        public readonly int Current;
        public readonly int Total;
        public readonly string Message;

        public float Normalized
        {
            get
            {
                if (Total <= 0)
                    return 0f;

                return Mathf.Clamp01((float)Current / Total);
            }
        }

        public ESEditorLongTaskProgress(int current, int total, string message)
        {
            Current = Mathf.Max(0, current);
            Total = Mathf.Max(0, total);
            Message = message ?? string.Empty;
        }
    }

    /// <summary>
    /// 所有耗时 Editor 工作都应拆为可中断的小步骤。ProcessStep 每次调用后都必须尽快返回，
    /// 由 ESEditorHandle 在下一帧继续推进。
    /// </summary>
    public abstract class ESEditorLongTask
    {
        public string Id { get; private set; }
        public string Name { get; protected set; }
        public string Key { get; protected set; }
        public int Priority { get; protected set; }
        public ESEditorLongTaskStatus Status { get; private set; }
        public ESEditorLongTaskProgress Progress { get; private set; }
        public Exception LastError { get; private set; }
        public bool IsCancellationRequested { get; private set; }
        public bool IsFinished => Status == ESEditorLongTaskStatus.Succeeded
                                  || Status == ESEditorLongTaskStatus.Failed
                                  || Status == ESEditorLongTaskStatus.Cancelled;

        protected ESEditorLongTask(string name, string key = null, int priority = 0)
        {
            Name = string.IsNullOrEmpty(name) ? GetType().Name : name;
            Key = key ?? string.Empty;
            Priority = priority;
            Progress = new ESEditorLongTaskProgress(0, 0, Name);
        }

        /// <summary>处理一小批工作；不要在这里执行无上限循环或全量扫描。</summary>
        public abstract ESEditorLongTaskStepResult ProcessStep(ESEditorLongTaskContext context);

        public void Cancel()
        {
            if (!IsFinished)
                IsCancellationRequested = true;
        }

        protected void SetProgress(int current, int total, string message)
        {
            Progress = new ESEditorLongTaskProgress(current, total, message);
        }

        protected void SetFailure(Exception exception)
        {
            LastError = exception ?? new InvalidOperationException("编辑器长任务失败。");
        }

        /// <summary>成功、失败、取消都会调用一次。此处只释放任务自身资源，不要操作全局 EditorApplication.update。</summary>
        protected virtual void OnFinish()
        {
        }

        internal void InternalPrepareForQueue(string id)
        {
            Id = id;
            Status = ESEditorLongTaskStatus.Queued;
            LastError = null;
            IsCancellationRequested = false;
        }

        internal void InternalBeginRunning()
        {
            if (Status == ESEditorLongTaskStatus.Queued)
                Status = ESEditorLongTaskStatus.Running;
        }

        internal void InternalFinish(ESEditorLongTaskStatus status, Exception error)
        {
            if (IsFinished)
                return;

            Status = status;
            if (error != null)
                LastError = error;

            try
            {
                OnFinish();
            }
            catch (Exception finishException)
            {
                LastError = LastError ?? finishException;
                if (Status == ESEditorLongTaskStatus.Succeeded)
                    Status = ESEditorLongTaskStatus.Failed;
                Debug.LogException(finishException);
            }
        }
    }

    public readonly struct ESEditorLongTaskContext
    {
        private readonly Stopwatch stopwatch;
        private readonly double frameBudgetMilliseconds;

        public ESEditorLongTask Task { get; }
        public bool IsCancellationRequested => Task == null || Task.IsCancellationRequested;
        public bool IsFrameBudgetExceeded => stopwatch != null && stopwatch.Elapsed.TotalMilliseconds >= frameBudgetMilliseconds;
        public double ElapsedMilliseconds => stopwatch == null ? 0d : stopwatch.Elapsed.TotalMilliseconds;

        internal ESEditorLongTaskContext(ESEditorLongTask task, Stopwatch stopwatch, double frameBudgetMilliseconds)
        {
            Task = task;
            this.stopwatch = stopwatch;
            this.frameBudgetMilliseconds = Math.Max(0.1d, frameBudgetMilliseconds);
        }
    }




}
