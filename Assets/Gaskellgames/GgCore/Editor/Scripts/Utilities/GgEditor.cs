#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public abstract class GgEditor : Editor
    {
        #region VerboseLogs

        [Tooltip("If verbose logs is true: info message logs will be displayed in the console alongside warning and error message logs for this script instance. [GgLogs.Info must also be enabled!]")]
        protected bool VerboseLogs => GaskellgamesSettings_SO.Instance.ShowLogs;
        
        /// <summary>
        /// If verbose logs is true: info message logs will be displayed in the console alongside warning and error message logs.
        /// </summary>
        /// <param name="logType">Type of log to show in the console.</param>
        /// <param name="format">String format of the log to be shown.</param>
        /// <param name="args">Arguments to be injected to the string format.</param>
        protected void Log(GgLogType logType, string format, params object[] args)
        {
            if (logType == GgLogType.Info && !VerboseLogs) return;
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
            if (logType == GgLogType.Info && !VerboseLogs) return;
            GgLogs.Log(messageColor, this, logType, format, args);
        }

        #endregion

    } // class end
}

#endif