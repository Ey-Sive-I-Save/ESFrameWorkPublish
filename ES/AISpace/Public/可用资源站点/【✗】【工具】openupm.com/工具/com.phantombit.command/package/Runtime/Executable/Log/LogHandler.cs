using System.Collections.Generic;
using UnityEngine;

namespace WellFired.Command.Unity.Runtime.Log
{
	public static class LogHandler 
	{
		// ReSharper disable once CollectionNeverQueried.Local
		private static readonly List<Application.LogCallback> Callbacks = new List<Application.LogCallback>();
		
		public static void RegisterLogCallback(Application.LogCallback callback)
		{
			Callbacks.Add(callback);
			Application.logMessageReceived += callback;
		}
		
		public static void UnRegisterLogCallback(Application.LogCallback callback)
		{
			Callbacks.Remove(callback);
			Application.logMessageReceived -= callback;
		}
	}
}