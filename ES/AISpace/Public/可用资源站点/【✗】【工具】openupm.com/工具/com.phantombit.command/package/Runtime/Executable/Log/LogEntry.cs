using System;
using UnityEngine;

namespace WellFired.Command.Unity.Runtime.Log
{
	public class LogEntry
	{
	    public LogType Type { get; }
		public string StackTrace { get; }
		public string LogMessage { get; }
		public string FirstLineOfLogMessage { get; }
		public float Time { get; }
		public Enum Filter { get; }
		public string FilterName { get; } = "";
	
	    public LogEntry(LogType type, string message, float time, string stackTrace = "")
	    {
	        Type = type;
	        LogMessage = message;
	        StackTrace = stackTrace;
	        FirstLineOfLogMessage = LogMessage.Split(new[] { '\n' }, 2)[0];
	        Time = time;

			Filter = null;
		    
		    var firstBrace = message.IndexOf('[');
		    var lastBrace = message.IndexOf(']');
		    if(firstBrace == 0 && lastBrace != -1)
		    {
			    var subMessage = message.Substring(firstBrace + 1, lastBrace - 1);
			    FilterName = subMessage;
		    }
	    }
	}
}