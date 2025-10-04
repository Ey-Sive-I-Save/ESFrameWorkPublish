using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    using ES;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;


    namespace ES
    {
        [InitializeOnLoad]
        public class ESEditorHandle
        {
            public static ESSimplePool<ESEditorHandleTask> TaskPool = new ESSimplePool<ESEditorHandleTask>(
                () => new ESEditorHandleTask(),
                (f) => { },
                5
                );
            public static Queue<ESEditorHandleTask> RunningTasks = new Queue<ESEditorHandleTask>();
            static ESEditorHandle()
            {
                EditorApplication.update += Update;
            }
            private static void Update()
            {
                if (RunningTasks.Count > 0)
                {
                    var use = RunningTasks.Peek();
                    if (use != null)
                    {
                        use.waitFrame--;
                        if (use.waitFrame < 0)
                        {
                            use.action?.Invoke();
                            if (use.OnlyOnce || use.MaxFrame <= 0 || use.CanExit())
                            {
                                use.TryAutoPushedToPool();
                                RunningTasks.Dequeue();
                            }
                            else
                            {
                                use.MaxFrame--;
                            }
                        }
                    }
                    else
                    {
                        use.TryAutoPushedToPool();
                        RunningTasks.Dequeue();
                    }
                }
            }
            public static void AddSimpleHanldeTask(Action c, int waitframe = 3)
            {
                var use = TaskPool.GetInPool();
                use.waitFrame = waitframe;
                use.action = c;
                RunningTasks.Enqueue(use);
            }
            public static void AddRunningHanldeTask(Action c, Func<bool> toExit, int MaxFrame = 1000, int waitframe = 3)
            {
                var use = TaskPool.GetInPool();
                use.waitFrame = waitframe;
                use.action = c;
                use.CanExit = toExit;
                use.OnlyOnce = toExit != null ? false : true;
                use.MaxFrame = MaxFrame;
                RunningTasks.Enqueue(use);
            }
        }

        public class ESEditorHandleTask : IPoolablebAuto
        {
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

}
