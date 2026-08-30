using WellFired.Command.Unity.Runtime.CommandHandlers;
using WellFired.Command.Unity.Runtime.Console;

/// <summary>
/// This class is here to provide users with a single easy location to register all command objects with the Development Console.
/// It is for illustration purposes only, you can do something similar to this in your own code.
/// 
/// Note: You can Register Commands from anywhere in runtime code.
/// 
/// </summary>
public static class CommandRegistration 
{
	/// <summary>
	/// This class is here to provide users with a single easy location to register all command objects with the Development Console.
	/// It is for illustration purposes only, you can do something similar to this in your own code.
	/// 
	/// Note: You can Register Commands from anywhere in runtime code.
	/// 
	/// </summary>
	public static void RegisterCommandsOnConsoleStartup()
	{
		// Register Commands by type
		DevelopmentCommands.Register(typeof(DeviceIdSampleCommands));
		DevelopmentCommands.Register(typeof(ClearConsoleSampleCommands));
		DevelopmentCommands.Register(typeof(AutoScrollCommands));
		DevelopmentCommands.Register(typeof(Inspect));
	
		// Register Commands by object
		DevelopmentCommands.Register(DevelopmentConsole.Instance);
	}
		
	public static void UnRegisterCommandsOnConsoleExit()
	{
		DevelopmentCommands.Unregister(typeof(DeviceIdSampleCommands));
		DevelopmentCommands.Unregister(typeof(ClearConsoleSampleCommands));
		DevelopmentCommands.Unregister(typeof(AutoScrollCommands));
		DevelopmentCommands.Unregister(typeof(Inspect));
			
		// Un Register Commands by object
		DevelopmentCommands.Unregister(DevelopmentConsole.Instance);
	}
}