using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    using ES;
    using Sirenix.Utilities;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;


    [InitializeOnLoad]
    public class ESEditorHandle
    {
        public static ESSimplePool<ESEditorHandleTask> TaskPool = new ESSimplePool<ESEditorHandleTask>(
            () => new ESEditorHandleTask(),
            (f) => { },
            5
            );
        public static Dictionary<string, int> singleKeys = new Dictionary<string, int>();
        public static Queue<ESEditorHandleTask> RunningTasks = new Queue<ESEditorHandleTask>();
        static bool registered = false;
        static ESEditorHandle()
        {
            if (!registered)
            {
                EditorApplication.update += Update;
                registered = true;
            }
        }
        private static void Update()
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
                            // 出错的 Task 必须从队列里踢出去，避免死循环
                            useTask.TryAutoPushedToPool();
                            RunningTasks.Dequeue();
                            // 异常发生，强制解锁，防止死锁！
                            if (!useTask.SingleKey.IsNullOrWhitespace())
                            {
                                singleKeys.Remove(useTask.SingleKey);
                            }
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
        public static void AddSimpleHandleTask(Action c, int waitframe = 3, string key = "")
        {
            if (c == null) return; // ✨ 极简的非空保护，加在最前面
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
        public static void AddRunningHandleTask(Action c, Func<bool> toExit, int MaxFrame = 1000, int waitframe = 3)
        {
            if (c == null) return; // ✨ 极简的非空保护，加在最前面
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
            RunningTasks.Clear();
            singleKeys.Clear();
            TaskPool.Clear();
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
            waitFrame = 2;
            action = null;
            MaxFrame = 1000;
            CanExit = () => false;
        }

        public void TryAutoPushedToPool()
        {
            ESEditorHandle.TaskPool.PushToPool(this);
        }
    }




}
