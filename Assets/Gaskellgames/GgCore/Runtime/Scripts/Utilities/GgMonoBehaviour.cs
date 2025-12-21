using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public abstract class GgMonoBehaviour : MonoBehaviour
    {
        #region VerboseLogs

        [SerializeField, HideInLine]
        [Tooltip("If verbose logs is true: info message logs will be displayed in the console alongside warning and error message logs for this script instance. [GgLogs.Info must also be enabled!]")]
        protected bool verboseLogs = false;
        
        /// <summary>
        /// If verbose logs is true: info message logs will be displayed in the console alongside warning and error message logs.
        /// </summary>
        /// <param name="logType">Type of log to show in the console.</param>
        /// <param name="format">String format of the log to be shown.</param>
        /// <param name="args">Arguments to be injected to the string format.</param>
        protected void Log(GgLogType logType, string format, params object[] args)
        {
            if (logType == GgLogType.Info && !verboseLogs) { return; }
            GgLogs.Log(this, logType, format, args);
        }
        
        /// <summary>
        /// If verbose logs is true: info message logs will be displayed in the console alongside warning and error message logs.
        /// </summary>
        /// <param name="messageColor">Color of the message to display in the console</param>
        /// <param name="logType">Type of log to show in the console.</param>
        /// <param name="format">String format of the log to be shown.</param>
        /// <param name="args">Arguments to be injected to the string format.</param>
        protected void Log(Color32 messageColor, GgLogType logType, string format, params object[] args)
        {
            if (logType == GgLogType.Info && !verboseLogs) { return; }
            GgLogs.Log(messageColor, this, logType, format, args);
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Conditional Gizmos

        [SerializeField, HideInLine]
        [Tooltip("Only show gizmos when this gameObject (or a parent gameObject) is selected.")]
        protected bool gizmosOnSelected;

        /// <summary>
        /// Method for drawing conditional gizmos. Invoked from base class's <see cref="OnDrawGizmos"/> and <see cref="OnDrawGizmosSelected"/>
        /// </summary>
        /// <param name="selected">True if OnDrawGizmosSelected, false if OnDrawGizmos</param>
        protected virtual void OnDrawGizmosConditional(bool selected)
        {
            
        }
        
        protected virtual void OnDrawGizmos()
        {
            if (!gizmosOnSelected)
            {
                OnDrawGizmosConditional(false);
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            OnDrawGizmosConditional(true);
        }

        #endregion
        
    } // class end
}
