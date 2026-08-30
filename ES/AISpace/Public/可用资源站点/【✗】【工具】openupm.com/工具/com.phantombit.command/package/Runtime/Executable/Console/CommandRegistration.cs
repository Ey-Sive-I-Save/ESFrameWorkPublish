using WellFired.Command.Unity.Runtime.CommandHandlers;

namespace WellFired.Command.Unity.Runtime.Console
{
	/// <summary>
	/// This class is here to provide users with a single easy location to register all command objects with .Command.
	/// 
	/// Note: You can Register Commands from anywhere you like, if you do it here however, you'll be able to keep the 
	/// registration / unregistration tidy, all methods in this static class will be called automatically for you.
	/// 
	/// </summary>
	public static class CommandRegistration 
	{
		/// <summary>
		/// Called automatically when .Command launches, you can add your own Registrations here.
		/// Don't forget to match your DevelopmentCommands.Register call with a DevelopmentCommands.Unregister call
		/// by calling DevelopmentCommands.Unregister in CommandRegistration.UnRegisterCommandsOnConsoleExit
		/// 
		/// Note: You can Register Commands from anywhere you like, if you do it here however, you'll be able to keep the 
		/// registration / unregistration tidy, this method will be called automatically for you at a sensible time.
		/// 
		/// </summary>
		public static void RegisterCommandsOnConsoleStartup(bool clearConsoleCommandEnabled, bool deviceIdCommandEnabled, bool inspectCommandEnabled, bool autoScrollCommandEnabled)
		{
			// Register Commands by type
			if(deviceIdCommandEnabled)
				DevelopmentCommands.Register(typeof(DeviceIdSampleCommands));
			
			if(clearConsoleCommandEnabled)
				DevelopmentCommands.Register(typeof(ClearConsoleSampleCommands));
			
			if(autoScrollCommandEnabled)
				DevelopmentCommands.Register(typeof(AutoScrollCommands));
			
			if(inspectCommandEnabled)
				DevelopmentCommands.Register(typeof(Inspect));
	
			// Register Commands by object
			if(DevelopmentConsole.Instance)
				DevelopmentCommands.Register(DevelopmentConsole.Instance);
		}
		
		/// <summary>
		/// Called automatically when .Command is destroyed, you can add your own Unregistrations here.
		/// 
		/// Note: You can Unregister Commands from anywhere you like, if you do it here however, you'll be able to keep the 
		/// registration / unregistration tidy, this method will be called automatically for you at a sensible time.
		/// 
		/// </summary>
		public static void UnRegisterCommandsOnConsoleExit()
		{
			// Un Register Commands by type
			DevelopmentCommands.Unregister(typeof(DeviceIdSampleCommands));
			DevelopmentCommands.Unregister(typeof(ClearConsoleSampleCommands));
			DevelopmentCommands.Unregister(typeof(AutoScrollCommands));
			DevelopmentCommands.Unregister(typeof(Inspect));
			
			// Un Register Commands by object
			if(DevelopmentConsole.Instance)
				DevelopmentCommands.Unregister(DevelopmentConsole.Instance);
		}
	}
}