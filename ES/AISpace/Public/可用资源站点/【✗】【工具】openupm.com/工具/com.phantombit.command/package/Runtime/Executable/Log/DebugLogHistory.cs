using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WellFired.Command.Unity.Runtime.Extensions;

namespace WellFired.Command.Unity.Runtime.Log
{
	public class DebugLogHistory : IDisposable
	{
		private const int LogHistoryLimit = 2000;

		private readonly List<LogEntry> _deferredLogItems = new List<LogEntry>();
	    private readonly List<LogEntry> _logItems = new List<LogEntry>();
	    private readonly DateTime _gameStartTime;

		public event Action<LogEntry> LogEntryAdded = delegate { };
		public event Action<LogEntry> LogEntryRemoved = delegate { };
	    public event Action LogHistoryCleared = delegate { };
	
	    public IList<LogEntry> LogItems => _logItems.AsReadOnly();
		public static DebugLogHistory Instance { get; private set; }

		public DebugLogHistory(Action<LogEntry> onLogEntryAdded)
		{
			if(Instance != null)
				throw new InvalidOperationException("DebugLogHistory is a singleton and has already been instantiated");
			
			_gameStartTime = DateTime.Now;
			Instance = this;
			
			LogHandler.RegisterLogCallback(HandleLoggingEvent);
			LogEntryAdded += onLogEntryAdded;
		}

	    public void Dispose()
	    {
	        if(Instance == this)
	            Instance = null;
	
	        LogHandler.UnRegisterLogCallback(HandleLoggingEvent);
			LogEntryAdded = null;
	    }
	
	    public void Update()
	    {
	        _logItems.AddRange(_deferredLogItems);
			
			while(_logItems.Count > LogHistoryLimit)
			{
				var firstItem = _logItems.First();
				LogEntryRemoved(firstItem);
				_logItems.RemoveAt(0);
			}
	
	    	_deferredLogItems.Clear();
	    }
	
	    public void Clear()
	    {
	        _deferredLogItems.Clear();
	        _logItems.Clear();
	
	        LogHistoryCleared();
		}

		public void LogException(string message)
		{
			var stackTrace = Environment.StackTrace;
			InternalHandleLoggingEvent(message, stackTrace, LogType.Exception);
		}

		public void LogMessage(string message, LogType type = LogType.Log)
		{
			InternalHandleLoggingEvent(message, "", type);
		}
		
		public void LogMessage(string message, string stacktrace, LogType type = LogType.Log)
		{
			InternalHandleLoggingEvent(message, stacktrace, type);
		}
	
	    private static string FilterStackTrace(string stackTrace)
	    {
	        var parts = stackTrace.Split('\n');
	        var isDebugLog = parts[0].StartsWith("Debug:Log");
	
	        if(isDebugLog && parts[1].StartsWith("UnityAppender:Append"))
	        {
	            var i = 2;
	            for (; i < parts.Length; ++i)
	                if (parts[i].StartsWith("log4net.Core."))
						break;
	            ++i;
	
	            return string.Join("\n", parts.SubArray(i, parts.Length - i));
	        }
	        else if(isDebugLog)
	            return string.Join("\n", parts.SubArray(1, parts.Length - 1));
	
	        return stackTrace;
	    }
	
	    private void HandleLoggingEvent(string logString, string stackTrace, LogType type)
	    {
	        InternalHandleLoggingEvent(logString, stackTrace, type);
	    }
	
	    private void InternalHandleLoggingEvent(string logString, string stackTrace, LogType type)
	    {
	        var timeSinceGameStart = DateTime.Now.Subtract(_gameStartTime).Ticks / (float)TimeSpan.TicksPerSecond;
	        var logItem = new LogEntry(type, logString, timeSinceGameStart, FilterStackTrace(stackTrace));
	        _deferredLogItems.Add(logItem);
			LogEntryAdded(logItem);
	    }
	}
}