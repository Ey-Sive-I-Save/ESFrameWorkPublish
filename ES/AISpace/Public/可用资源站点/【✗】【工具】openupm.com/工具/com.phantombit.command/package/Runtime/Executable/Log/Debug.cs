using System;
using UnityEngine;
// Bah
using Object = UnityEngine.Object;

// Disabled this here because I want this to appear simple to users.
// ReSharper disable once CheckNamespace
namespace WellFired.Command.Log
{
	public static class Debug
	{	
		public static void LogError(Enum filter, object message)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			var combinedMessage = $"[{filter}] {message}";
			UnityEngine.Debug.LogError(combinedMessage);
#endif
		}
	
		public static void LogError(object message)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.LogError(message);
#endif
		}
		
		public static void LogError(Enum filter, object message, Object ping)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			var combinedMessage = $"[{filter}] {message}";
			UnityEngine.Debug.LogError(combinedMessage, ping);
#endif
		}
		
		public static void LogError(object message, Object ping)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.LogError(message, ping);
#endif
		}
				
		public static void LogErrorFormat(Enum filter, string message, params object[] parameters)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			var formattedMessage = string.Format(message, parameters);
			var combinedMessage = $"[{filter}] {formattedMessage}";
			UnityEngine.Debug.LogError(combinedMessage);
#endif
		}
	
		public static void LogErrorFormat(string message, params object[] parameters)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.LogError(string.Format(message, parameters));
#endif
		}
		
		public static void LogWarning(Enum filter, object message)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			var combinedMessage = $"[{filter}] {message}";
			UnityEngine.Debug.LogWarning(combinedMessage);
#endif
		}
		
		public static void LogWarning(object message)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.LogWarning(message);
#endif
		}
		
		public static void LogWarningFormat(Enum filter, string message, params object[] parameters)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			var formattedMessage = string.Format(message, parameters);
			var combinedMessage = $"[{filter}] {formattedMessage}";
			UnityEngine.Debug.LogWarning(combinedMessage);
#endif
		}
	
		public static void LogWarningFormat(string message, params object[] parameters)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.LogWarning(string.Format(message, parameters));
#endif
		}
		
		public static void LogWarningFormat(Enum filter, string message, Object ping, params object[] parameters)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			var formattedMessage = string.Format(message, parameters);
			var combinedMessage = $"[{filter}] {formattedMessage}";
			UnityEngine.Debug.LogWarning(combinedMessage, ping);
#endif
		}
	
		public static void LogWarningFormat(string message, Object ping, params object[] parameters)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.LogWarning(string.Format(message, parameters), ping);
#endif
		}
		
		public static void LogWarningFormat(Enum filter, object message, Object ping)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			var combinedMessage = $"[{filter}] {message}";
			UnityEngine.Debug.LogWarning(combinedMessage, ping);
#endif
		}
		
		public static void LogWarning(object message, Object ping)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.LogWarning(message, ping);
#endif
		}
		
		public static void Log(Enum filter, object message)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			var combinedMessage = $"[{filter}] {message}";
			UnityEngine.Debug.Log(combinedMessage);
#endif
		}
		
		public static void Log(object message)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.Log(message);
#endif
		}

		public static void Log(Enum filter, string message, Color color)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			var colouredMessage = $"<color={color.ToString()}>{message}</color>";
			var combinedMessage = $"[{filter}] {colouredMessage}";
			UnityEngine.Debug.Log(combinedMessage);
#endif
		}
	
		public static void Log(string message, Color color)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.Log($"<color={color.ToString()}>{message}</color>");
#endif
		}
		
		public static void Log(Enum filter, string message, Object ping)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			var combinedMessage = $"[{filter}] {message}";
			UnityEngine.Debug.Log(combinedMessage, ping);
#endif
		}
	
		public static void Log(string message, Object ping)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.Log(message, ping);
#endif
		}
		
		public static void Log(Enum filter, string message, Object ping, Color color)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			var colouredMessage = $"<color={color.ToString()}>{message}</color>";
			var combinedMessage = $"[{filter}] {colouredMessage}";
			UnityEngine.Debug.Log(combinedMessage, ping);
#endif
		}
	
		public static void Log(string message, Object ping, Color color)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.Log($"<color={color.ToString()}>{message}</color>", ping);
#endif
		}
		
		public static void LogFormat(Enum filter, string message, params object[] parameters)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			var formattedMessage = string.Format(message, parameters);
			var combinedMessage = $"[{filter}] {formattedMessage}";
			UnityEngine.Debug.Log(combinedMessage);
#endif
		}
	
		public static void LogFormat(string message, params object[] parameters)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.Log(string.Format(message, parameters));
#endif
		}
		
		public static void LogFormat(Enum filter, string message, Color color, params object[] parameters)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			var formattedColouredMessage = $"<color={color.ToString()}>{string.Format(message, parameters)}</color>";
			var combinedMessage = $"[{filter}] {formattedColouredMessage}";
			UnityEngine.Debug.Log(combinedMessage);
#endif
		}
	
		public static void LogFormat(string message, Color color, params object[] parameters)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.Log($"<color={color.ToString()}>{string.Format(message, parameters)}</color>");
#endif
		}
		
		public static void LogFormat(Enum filter, string message, Object ping, params object[] parameters)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			var formattedMessage = string.Format(message, parameters);
			var combinedMessage = $"[{filter}] {formattedMessage}";
			UnityEngine.Debug.Log(combinedMessage, ping);
#endif
		}
	
		public static void LogFormat(string message, Object ping, params object[] parameters)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.Log(string.Format(message, parameters), ping);
#endif
		}
		
		public static void LogFormat(Enum filter, string message, Object ping, Color color, params object[] parameters)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			var formattedColouredMessage = $"<color={color.ToString()}>{string.Format(message, parameters)}</color>";
			var combinedMessage = $"[{filter}] {formattedColouredMessage}";
			UnityEngine.Debug.Log(combinedMessage, ping);
#endif
		}
	
		public static void LogFormat(string message, Object ping, Color color, params object[] parameters)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.Log($"<color={color.ToString()}>{string.Format(message, parameters)}</color>", ping);
#endif
		}
	
		public static void LogException(Exception exception)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.LogException(exception);
#endif
		}
		
		public static void ClearDeveloperConsole()
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.ClearDeveloperConsole();
#endif
		}
		
		public static void DebugBreak()
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.DebugBreak();
#endif
		}
	
		public static void DrawLine(Vector3 start, Vector3 end)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.DrawLine(start, end);
#endif
		}
		
		public static void DrawLine(Vector3 start, Vector3 end, UnityEngine.Color color)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.DrawLine(start, end, color);
#endif
		}
		
		public static void DrawLine(Vector3 start, Vector3 end, UnityEngine.Color color, float duration)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.DrawLine(start, end, color, duration);
#endif
		}
	
		public static void DrawLine(Vector3 start, Vector3 end, UnityEngine.Color color, float duration, bool depthTest)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.DrawLine(start, end, color, duration, depthTest);
#endif
		}
		
		public static void DrawRay(Vector3 start, Vector3 dir)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.DrawRay(start, dir);
#endif
		}
		
		public static void DrawRay(Vector3 start, Vector3 dir, UnityEngine.Color color)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.DrawRay(start, dir, color);
#endif
		}
		
		public static void DrawRay(Vector3 start, Vector3 dir, UnityEngine.Color color, float duration)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.DrawRay(start, dir, color, duration);
#endif
		}
	
		public static void DrawRay(Vector3 start, Vector3 dir, UnityEngine.Color color, float duration, bool depthTest)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.DrawRay(start, dir, color, duration, depthTest);
#endif
		}
		
		public static void Break()
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.Break();
#endif
		}
		
		public static void Assert(bool condition, string message = null, string message1 = null)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			if(!condition)
			{ 
				throw new Exception(message);
			}
#endif
		}
		
		public static void WriteLine(string line)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
	
#endif
		}
		
		public static void LogException(Exception exception, Object context)
		{
#if !DISABLE_DEVELOPMENT_CONSOLE
			UnityEngine.Debug.LogException(exception, context);
#endif
		}
		
		public enum Color
		{
			Black,
			Blue,
			Brown,
			Cyan,
			Darkblue,
			Fuchsia,
			Green,
			Grey,
			Lightblue,
			Lime,
			Magenta,
			Maroon,
			Navy,
			Olive,
			Orange,
			Purple,
			Red,
			Silver,
			Teal,
			White,
			Yellow
		}
	}
}
