using JetBrains.Annotations;
using UnityEngine;
using WellFired.Command.Unity.Runtime.CommandHandlers;

namespace WellFired.Command.Unity.Runtime.Console
{
	public static class DeviceIdSampleCommands
	{
		/// <summary>
		/// A simple Sample Command that outputs the Device' ID.
		/// You can mark up the Method with the attribute ConsoleCommand, passing a custom Name of Description, as seen in this example.
		/// 
		/// [ConsoleCommand(Name = "DeviceID", Description = "Outputs the device's ID")]
		/// private static void DeviceID()
		/// {
		/// 	Debug.Log("Device ID: " + SystemInfo.deviceUniqueIdentifier);
		/// }
		/// </summary>
		[ConsoleCommand(Name = "DeviceID", Description = "Outputs the device's ID")]
		[UsedImplicitly]
		private static void DeviceId()
		{
			Command.Log.Debug.Log("Device ID: " + SystemInfo.deviceUniqueIdentifier);
		}
	}
}