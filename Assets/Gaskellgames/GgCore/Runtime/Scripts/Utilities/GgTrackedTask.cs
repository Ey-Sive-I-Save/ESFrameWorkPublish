using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Gaskellgames
{
    [System.Serializable]
    internal class GgTrackedTask : IDisposable
    {
        #region Variables

        [SerializeField]
        [Tooltip("The script that the task was called from.")]
        internal string callerScript;
        
        [SerializeField]
        [Tooltip("The method that the task was called from.")]
        internal string callerMemberName;
        
        [SerializeField]
        [Tooltip("The line that the task was called from.")]
        internal int callerLineNumber;
        
        [SerializeField]
        [Tooltip("The type of GgTask.")]
        internal string taskType;
        
        [SerializeField]
        [Tooltip("The current status of the task.")]
        private TrackedTaskStatus status;
        
        [SerializeField]
        [Tooltip("The timeout duration, in seconds, of the task.")]
        internal int timeout;
        
        [SerializeField]
        [Tooltip("The update frequency, in milliseconds, of the task.")]
        internal int frequency;
        
        [SerializeField]
        [Tooltip("The system time when the task was called.")]
        internal DateTime datetime;
        
        [SerializeField]
        [Tooltip("The frame the task was called on.")]
        internal int frameCount;
        
        [SerializeField]
        [Tooltip("The thread ID used to manage the task.")]
        internal int managedThreadId;
        
        internal CancellationTokenSource cancellationTokenSource;

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Internal Functions

        internal GgTrackedTask(CancellationTokenSource cancellationTokenSource, int timeout, int frequency, string taskType, string callerMemberName, string callerFilepath, int callerLineNumber)
        {
            this.cancellationTokenSource = cancellationTokenSource;
            
            this.callerScript = Path.GetFileNameWithoutExtension(callerFilepath);
            this.callerMemberName = callerMemberName + "()";
            this.callerLineNumber = callerLineNumber;
            this.taskType = taskType;
            this.status = TrackedTaskStatus.InProgress;
            this.timeout = timeout;
            this.frequency = frequency;
            this.datetime = DateTime.Now;
            this.frameCount = Time.frameCount;
            this.managedThreadId = Thread.CurrentThread.ManagedThreadId;
        }
        
        /// <summary>
        /// Set the task as complete and cache the result
        /// </summary>
        internal TrackedTaskStatus Status
        {
            get => status;
            set => status = value;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Public Functions

        public void Dispose()
        {
            // we're not disposing the cancellationTokenSource here since it can cause null reference exception
        }

        #endregion
        
    } // class end
}
