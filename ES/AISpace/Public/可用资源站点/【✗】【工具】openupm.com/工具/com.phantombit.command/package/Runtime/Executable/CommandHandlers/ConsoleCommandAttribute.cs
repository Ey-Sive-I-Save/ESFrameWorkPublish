using System;

namespace WellFired.Command.Unity.Runtime.CommandHandlers
{
	/// <summary>
	/// Place this attribute on any method that you want exposed to the .Command.
	/// 
	/// Example:
	/// 
	/// [ConsoleCommand(Name = "DeviceID", Description = "Outputs the device's ID")]
	/// private static void DeviceID()
	/// {
	/// 	Debug.Log(DebugLog.Filter.DevelopmentConsole, "Device ID:" + SystemInfo.deviceUniqueIdentifier);
	/// }
	/// 
	/// </summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
	public class ConsoleCommandAttribute : Attribute
	{
		#region Properties
		/// <summary>
		/// The Name of this attribute. .Command will use this as the actual command the user must type of select.
		/// </summary>
		/// <value>The name.</value>
	    public string Name
	    {
	        get;
	        set;
	    }
	
		/// <summary>
		/// This will give the user a nice overview of the command they are about to use.
		/// </summary>
		/// <value>A string value representing the description, try to make this as descriptive as possible.</value>
	    public string Description
	    {
	        get;
	        set;
	    }
		#endregion
	}
}