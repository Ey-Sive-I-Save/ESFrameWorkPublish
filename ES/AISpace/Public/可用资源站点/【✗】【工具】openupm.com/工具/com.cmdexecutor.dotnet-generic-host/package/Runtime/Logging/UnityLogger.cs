using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Debug = UnityEngine.Debug;

namespace Microsoft.Extensions.Hosting.Unity.Logging
{
    /// <summary>
    /// Default logger which writes to Unity's Debug.Log
    /// </summary>
    internal class UnityLogger : Microsoft.Extensions.Logging.ILogger
    {
        private readonly string _name;
        private readonly UnityLoggerConfiguration _config;

        public IDisposable BeginScope<TState>(TState state) => default;

        public UnityLogger(string name, UnityLoggerConfiguration config)
        {
            _name = name;
            _config = config;
        }

#if UNITY_2022_2_OR_NEWER
        [UnityEngine.HideInCallstack]
#endif
        public bool IsEnabled(LogLevel logLevel) => true;

        [DebuggerStepThrough]
#if UNITY_2022_2_OR_NEWER
        [UnityEngine.HideInCallstack]
#endif
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            if (_config.EventId != 0 && _config.EventId != eventId.Id)
                return;

            switch (logLevel)
            {
                case LogLevel.Information:
                {
                    Debug.Log($"<color=green>INFO [{_name}] - {formatter(state, exception)}</color>");
                    break;
                }
                case LogLevel.Debug:
                {
                    Debug.Log($"<color=white>DEBUG [{_name}] - {formatter(state, exception)}</color>");
                    break;
                }
                case LogLevel.Trace:
                {
                    Debug.Log($"<color=gray>TRACE [{_name}] - {formatter(state, exception)}</color>");
                    break;
                }
                case LogLevel.Error:
                {
                    Debug.LogError($"<color=red>ERROR [{_name}] - {formatter(state, exception)}</color>");
                    break;
                }
                case LogLevel.Warning:
                {
                    Debug.LogWarning($"<color=yellow>WARN [{_name}] - {formatter(state, exception)}</color>");
                    break;
                }
                case LogLevel.Critical:
                {
                    Debug.LogError($"<color=purple>FATAL [{_name}] - {formatter(state, exception)}</color>");
                    break;
                }
                case LogLevel.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
            }
        }
    }
}