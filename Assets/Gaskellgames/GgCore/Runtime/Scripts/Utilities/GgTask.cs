using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    public static class GgTask
    {
        #region Variables
        
        internal enum SortingType
        {
            CalledFrom,
            Type,
            Status,
            Timeout,
            Frequency,
            Time,
            Frame,
            Thread,
        }
        
        internal static List<GgTrackedTask> trackedTasks;
        internal static Action trackedTasksUpdated;
        
        private static bool isSortedAscending;
        private static SortingType sortBy;

        internal static bool DebugMode
        {
            get => Application.isEditor && GaskellgamesSettings_SO.Instance.DebugGgTrackedTasks;
            set
            {
                GaskellgamesSettings_SO.Instance.DebugGgTrackedTasks = value;
                GgLogs.Log(null, GgLogType.Info, "GgTask.DebugMode = {0}", value);
            }
        }

        internal static bool IsSortedAscending
        {
            get => isSortedAscending;
            set
            {
                isSortedAscending = value;
                HandleTasksUpdated();
            }
        }

        internal static SortingType SortBy
        {
            get => sortBy;
            set
            {
                sortBy = value;
                HandleTasksUpdated();
            }
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Private Functions
        
        private static GgTrackedTask NewTrackedTask(CancellationTokenSource cancellationTokenSource, int timeout, int frequency, string taskType, string callerMemberName, string callerFilepath, int callerLineNumber)
        {
            trackedTasks ??= new List<GgTrackedTask>();
            GgTrackedTask trackedTask = new GgTrackedTask
            (
                cancellationTokenSource,
                timeout,
                frequency,
                taskType,
                callerMemberName,
                callerFilepath,
                callerLineNumber
            );
            trackedTasks.Add(trackedTask);
            HandleTasksUpdated();

            return trackedTask;
        }
        
        private static void HandleTasksUpdated()
        {
            SortTrackedTasks();
            trackedTasksUpdated?.Invoke();
        }

        private static void SortTrackedTasks()
        {
            switch (sortBy)
            {
                case SortingType.CalledFrom:
                    SortByCalledFrom(IsSortedAscending);
                    break;
                
                case SortingType.Type:
                    SortByType(IsSortedAscending);
                    break;
                
                case SortingType.Status:
                    SortByStatus(IsSortedAscending);
                    break;
                
                case SortingType.Timeout:
                    SortByTimeout(IsSortedAscending);
                    break;
                
                case SortingType.Frequency:
                    SortByFrequency(IsSortedAscending);
                    break;
                
                case SortingType.Time:
                    SortByTime(IsSortedAscending);
                    break;
                
                case SortingType.Frame:
                    SortByFrame(IsSortedAscending);
                    break;
                
                case SortingType.Thread:
                    SortByThread(IsSortedAscending);
                    break;
            }
        }
        
        private static void SortByCalledFrom(bool ascending)
        {
            trackedTasks = ascending
                ? trackedTasks.OrderBy(trackedTask => trackedTask.callerScript).ToList()
                : trackedTasks.OrderByDescending(trackedTask => trackedTask.callerScript).ToList();
        }

        private static void SortByType(bool ascending)
        {
            trackedTasks = ascending
                ? trackedTasks.OrderBy(trackedTask => trackedTask.taskType).ToList()
                : trackedTasks.OrderByDescending(trackedTask => trackedTask.taskType).ToList();
        }

        private static void SortByStatus(bool ascending)
        {
            trackedTasks = ascending
                ? trackedTasks.OrderBy(trackedTask => trackedTask.Status).ToList()
                : trackedTasks.OrderByDescending(trackedTask => trackedTask.Status).ToList();
        }

        private static void SortByTimeout(bool ascending)
        {
            trackedTasks = ascending
                ? trackedTasks.OrderBy(trackedTask => trackedTask.timeout).ToList()
                : trackedTasks.OrderByDescending(trackedTask => trackedTask.timeout).ToList();
        }

        private static void SortByFrequency(bool ascending)
        {
            trackedTasks = ascending
                ? trackedTasks.OrderBy(trackedTask => trackedTask.frequency).ToList()
                : trackedTasks.OrderByDescending(trackedTask => trackedTask.frequency).ToList();
        }

        private static void SortByTime(bool ascending)
        {
            trackedTasks = ascending
                ? trackedTasks.OrderBy(trackedTask => trackedTask.datetime).ToList()
                : trackedTasks.OrderByDescending(trackedTask => trackedTask.datetime).ToList();
        }

        private static void SortByFrame(bool ascending)
        {
            trackedTasks = ascending
                ? trackedTasks.OrderBy(trackedTask => trackedTask.frameCount).ToList()
                : trackedTasks.OrderByDescending(trackedTask => trackedTask.frameCount).ToList();
        }

        private static void SortByThread(bool ascending)
        {
            trackedTasks = ascending
                ? trackedTasks.OrderBy(trackedTask => trackedTask.managedThreadId).ToList()
                : trackedTasks.OrderByDescending(trackedTask => trackedTask.managedThreadId).ToList();
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Internal Functions
        
        internal static void CancelAllTasks()
        {
            foreach (GgTrackedTask trackedTask in trackedTasks)
            {
                trackedTask.cancellationTokenSource?.Cancel();
            }
            HandleTasksUpdated();
        }
        
        internal static void ClearTrackedTasks()
        {
            for (int i = trackedTasks.Count - 1; i >= 0; i--)
            {
                if (trackedTasks[i].Status == TrackedTaskStatus.InProgress) { continue; }
                trackedTasks.RemoveAt(i);
            }
            HandleTasksUpdated();
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Public Functions
        
        /// <summary>
        /// Convert a task to a trackable GgTask.
        /// </summary>
        /// <param name="task">The task to convert to a trackable GgTask.</param>
        /// <param name="tokenSource">The cancellation token source used to cancel this task.</param>
        /// <param name="timeout">The timeout in seconds. [Default is -1, which is infinite timeout]</param>
        /// <param name="frequency">The frequency at which the condition will be checked. [Default is 25]</param>
        /// <returns><see cref="TaskResultType.Complete"/> if successful, <see cref="TaskResultType.Cancelled"/> if task stopped before completion due to cancellation token, <see cref="TaskResultType.Timeout"/> if task stopped before completion due to timeout condition.</returns>
        public static async Task<TaskResultType> AsGgTask(this Task task, CancellationTokenSource tokenSource, int timeout = -1, int frequency = 25, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilepath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            // add to tracked tasks
            GgTrackedTask trackedTask = NewTrackedTask(tokenSource, timeout, frequency, nameof(AsGgTask), callerMemberName, callerFilepath, callerLineNumber);
            
            // create disposable scope
            using (trackedTask)
            {
                // handle timeout
                if (0 <= timeout)
                {
                    if (task != await Task.WhenAny(task, Task.Delay(timeout * 1000)))
                    {
                        trackedTask.Status = TrackedTaskStatus.Timeout;
                        if (!DebugMode)
                        {
                            trackedTasks.Remove(trackedTask);
                            trackedTasksUpdated?.Invoke();
                        }
                        return TaskResultType.Timeout;
                    }
                }
                else
                {
                    await task;
                }

                // handle cancelled
                if (tokenSource.Token.IsCancellationRequested)
                {
                    trackedTask.Status = TrackedTaskStatus.Cancelled;
                    if (!DebugMode)
                    {
                        trackedTasks.Remove(trackedTask);
                        trackedTasksUpdated?.Invoke();
                    }
                    return TaskResultType.Cancelled;
                }

                // handle complete
                trackedTask.Status = TrackedTaskStatus.Complete;
                if (!DebugMode)
                {
                    trackedTasks.Remove(trackedTask);
                    trackedTasksUpdated?.Invoke();
                }
                return TaskResultType.Complete;
            }
        }
        
        /// <summary>
        /// Blocks while the condition is true, early return if a timeout or cancellation occurs.
        /// </summary>
        /// <param name="condition">The condition that will block.</param>
        /// <param name="tokenSource">The cancellation token source used to cancel this task.</param>
        /// <param name="timeout">The timeout in seconds. [Default is -1, which is infinite timeout]</param>
        /// <param name="frequency">The frequency at which the condition will be checked. [Default is 25]</param>
        /// <returns><see cref="TaskResultType.Complete"/> if successful, <see cref="TaskResultType.Cancelled"/> if task stopped before completion due to cancellation token, <see cref="TaskResultType.Timeout"/> if task stopped before completion due to timeout condition.</returns>
        public static async Task<TaskResultType> WaitWhile(Func<bool> condition, CancellationTokenSource tokenSource, int timeout = -1, int frequency = 25, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilepath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            // add to tracked tasks
            GgTrackedTask trackedTask = NewTrackedTask(tokenSource, timeout, frequency, nameof(WaitWhile), callerMemberName, callerFilepath, callerLineNumber);
            
            // create disposable scope
            using (trackedTask)
            {
                // if condition already met then return complete (if we don't return here, there may be a frame delay).
                if (!condition())
                {
                    trackedTask.Status = TrackedTaskStatus.Complete;
                    if (!DebugMode)
                    {
                        trackedTasks.Remove(trackedTask);
                        trackedTasksUpdated?.Invoke();
                    }
                    return TaskResultType.Complete;
                }

                // start task with set update frequency
                Task waitTask = Task.Run(async () =>
                {
                    while (condition()) await Task.Delay(frequency, tokenSource.Token);
                }, tokenSource.Token);

                // handle timeout
                if (0 <= timeout)
                {
                    if (waitTask != await Task.WhenAny(waitTask, Task.Delay(timeout * 1000)))
                    {
                        trackedTask.Status = TrackedTaskStatus.Timeout;
                        if (!DebugMode)
                        {
                            trackedTasks.Remove(trackedTask);
                            trackedTasksUpdated?.Invoke();
                        }
                        return TaskResultType.Timeout;
                    }
                }

                // handle cancelled
                if (tokenSource.Token.IsCancellationRequested)
                {
                    trackedTask.Status = TrackedTaskStatus.Cancelled;
                    if (!DebugMode)
                    {
                        trackedTasks.Remove(trackedTask);
                        trackedTasksUpdated?.Invoke();
                    }
                    return TaskResultType.Cancelled;
                }

                // handle complete
                trackedTask.Status = TrackedTaskStatus.Complete;
                if (!DebugMode)
                {
                    trackedTasks.Remove(trackedTask);
                    trackedTasksUpdated?.Invoke();
                }
                return TaskResultType.Complete;
            }
        }
        
        /// <summary>
        /// Blocks until the condition is true, early return if a timeout or cancellation occurs.
        /// </summary>
        /// <param name="condition">The condition that will block.</param>
        /// <param name="tokenSource">The cancellation token source used to cancel this task.</param>
        /// <param name="timeout">The timeout in seconds. [Default is -1, which is infinite timeout]</param>
        /// <param name="frequency">The frequency at which the condition will be checked. [Default is 25]</param>
        /// <returns><see cref="TaskResultType.Complete"/> if successful, <see cref="TaskResultType.Cancelled"/> if task stopped before completion due to cancellation token, <see cref="TaskResultType.Timeout"/> if task stopped before completion due to timeout condition.</returns>
        public static async Task<TaskResultType> WaitUntil(Func<bool> condition, CancellationTokenSource tokenSource, int timeout = -1, int frequency = 25, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilepath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            // add to tracked tasks
            GgTrackedTask trackedTask = NewTrackedTask(tokenSource, timeout, frequency, nameof(WaitUntil), callerMemberName, callerFilepath, callerLineNumber);
            
            // create disposable scope
            using (trackedTask)
            {
                // if condition already met then return complete (if we don't return here, there may be a frame delay).
                if (condition())
                {
                    trackedTask.Status = TrackedTaskStatus.Complete;
                    if (!DebugMode)
                    {
                        trackedTasks.Remove(trackedTask);
                        trackedTasksUpdated?.Invoke();
                    }
                    return TaskResultType.Complete;
                }
            
                // start task with set update frequency
                Task waitTask = Task.Run(async () => { while (!condition()) await Task.Delay(frequency, tokenSource.Token); }, tokenSource.Token);
            
                // handle timeout
                if (0 <= timeout)
                {
                    if (waitTask != await Task.WhenAny(waitTask, Task.Delay(timeout * 1000)))
                    {
                        trackedTask.Status = TrackedTaskStatus.Timeout;
                        if (!DebugMode)
                        {
                            trackedTasks.Remove(trackedTask);
                            trackedTasksUpdated?.Invoke();
                        }
                        return TaskResultType.Timeout;
                    }
                }
            
                // handle cancelled
                if (tokenSource.Token.IsCancellationRequested)
                {
                    trackedTask.Status = TrackedTaskStatus.Cancelled;
                    if (!DebugMode)
                    {
                        trackedTasks.Remove(trackedTask);
                        trackedTasksUpdated?.Invoke();
                    }
                    return TaskResultType.Cancelled;
                }
            
                // handle complete
                trackedTask.Status = TrackedTaskStatus.Complete;
                if (!DebugMode)
                {
                    trackedTasks.Remove(trackedTask);
                    trackedTasksUpdated?.Invoke();
                }
                return TaskResultType.Complete;
            }
        }
        
        /// <summary>
        /// Blocks until the delay is complete, early return if cancellation occurs.
        /// </summary>
        /// <param name="seconds"></param>
        /// <param name="tokenSource">The cancellation token source used to cancel this task.</param>
        /// <returns><see cref="TaskResultType.Complete"/> if successful, <see cref="TaskResultType.Cancelled"/> if task stopped before completion due to cancellation token, <see cref="TaskResultType.Timeout"/> if task stopped before completion due to timeout condition.</returns>
        public static async Task<TaskResultType> WaitForSeconds(float seconds, CancellationTokenSource tokenSource, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilepath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            // add to tracked tasks
            GgTrackedTask trackedTask = NewTrackedTask(tokenSource, -1, -1, nameof(WaitForSeconds), callerMemberName, callerFilepath, callerLineNumber);
            
            // create disposable scope
            using (trackedTask)
            {
                // if condition already met then return complete (if we don't return here, there may be a frame delay).
                if (seconds <= 0)
                {
                    trackedTask.Status = TrackedTaskStatus.Complete;
                    if (!DebugMode)
                    {
                        trackedTasks.Remove(trackedTask);
                        trackedTasksUpdated?.Invoke();
                    }
                    return TaskResultType.Complete;
                }

                // start task
                int milliseconds = (int)(seconds * 1000);
                Task waitTask = Task.Delay(milliseconds, tokenSource.Token);
                try
                {
                    await waitTask;
                }

                // handle cancelled
                catch (OperationCanceledException)
                {
                    trackedTask.Status = TrackedTaskStatus.Cancelled;
                    if (!DebugMode)
                    {
                        trackedTasks.Remove(trackedTask);
                        trackedTasksUpdated?.Invoke();
                    }
                    return TaskResultType.Cancelled;
                }

                // handle complete
                trackedTask.Status = TrackedTaskStatus.Complete;
                if (!DebugMode)
                {
                    trackedTasks.Remove(trackedTask);
                    trackedTasksUpdated?.Invoke();
                }
                return TaskResultType.Complete;
            }
        }
        
        /// <summary>
        /// Blocks for a single frame.
        /// </summary>
        /// <returns><see cref="TaskResultType.Complete"/> if successful, <see cref="TaskResultType.Cancelled"/> if task stopped before completion due to cancellation token, <see cref="TaskResultType.Timeout"/> if task stopped before completion due to timeout condition.</returns>
        public static async Task<TaskResultType> WaitUntilNextFrame([CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilepath = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            // add to tracked tasks
            GgTrackedTask trackedTask = NewTrackedTask(null, -1, -1, nameof(WaitUntilNextFrame), callerMemberName, callerFilepath, callerLineNumber);
            
            // create disposable scope
            using (trackedTask)
            {
                // start task
                int current = Time.frameCount;
                while (current == Time.frameCount)
                {
                    await Task.Yield();
                }

                // handle complete
                trackedTask.Status = TrackedTaskStatus.Complete;
                if (!DebugMode)
                {
                    trackedTasks.Remove(trackedTask);
                    trackedTasksUpdated?.Invoke();
                }
                return TaskResultType.Complete;
            }
        }
        
        #endregion
        
    } // class end
}
