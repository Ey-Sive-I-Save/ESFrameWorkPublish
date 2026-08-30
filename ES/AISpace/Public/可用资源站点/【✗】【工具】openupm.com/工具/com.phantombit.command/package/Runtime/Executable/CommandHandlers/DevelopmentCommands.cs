using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using WellFired.Command.Unity.Runtime.Extensions;
using WellFired.Command.Unity.Runtime.Log;
using WellFired.Command.Unity.Runtime.Wrapper;
using StringExtensions = WellFired.Command.Unity.Runtime.Extensions.StringExtensions;

namespace WellFired.Command.Unity.Runtime.CommandHandlers
{
	/// <summary>
	/// This is a static class that the user can register use to register command wrappers.
	/// If you have any DevelopmentConsole Attributes inside your class, you will want to 
	/// call one of the following methods:
	/// 
	///	DevelopmentCommands.RegisterCommands(typeof(Cheats));
	///	DevelopmentCommands.RegisterCommands(this);
	/// 
	/// With a matching call to
	/// 
	///	DevelopmentCommands.UnregisterCommands(typeof(Cheats));
	///	DevelopmentCommands.UnregisterCommands(this);
	/// 
	/// </summary>
	public static class DevelopmentCommands
	{
		#region Fields
		private static readonly Dictionary<string, CommandWrapper> AllHandlers = new Dictionary<string, CommandWrapper>();
		private static readonly HashSet<Type> RegisteredHandlers = new HashSet<Type>();
		#endregion

		#region Delegates
		public static event Action<CommandWrapper> CommandHandlerAdded = delegate { };
		public static event Action<CommandWrapper> CommandHandlerRemoved = delegate { };
		public static event Action<CommandWrapper> CommandExecuted = delegate { }; 
		#endregion

		#region Properties
		public static IEnumerable<CommandWrapper> Handlers => AllHandlers.Values;
		#endregion
	
		#region Methods
		public static void HandleCommand(string commandLine)
		{
			var parts = StringExtensions.SplitCommandLine(commandLine).ToArray();
	
			if(parts.Length == 0)
				return;
	
			DebugLogHistory.Instance.LogMessage("--> " + commandLine);
	
			var command = parts[0];
			var commandLower = command.ToLower();

			if(!AllHandlers.TryGetValue(commandLower, out var commandWrapper))
			{
				var warning = $"Unable to find and execute command : {command}";
				DebugLogHistory.Instance.LogMessage(warning, LogType.Warning);
				return;
			}
	
			if(!commandWrapper.IsValid())
			{
				var error = $"A command Wrapper ({commandWrapper.MethodOrPropertyName}) was destroyed without being de-registered";
				throw new InvalidOperationException(error);
			}

			// The arguments are everything in the parts array after the first element.
			var arguments = parts.SubArray(1, parts.Length - 1);
			var methodParameters = commandWrapper.Parameters;
	
			if(arguments.Length > methodParameters.Length)
			{
				var warning = $"Invalid number of passed arguments, {command} expects {methodParameters.Length}";
				DebugLogHistory.Instance.LogMessage(warning, LogType.Warning);
				return;
			}
	
			commandWrapper.Invoke(arguments);
			CommandExecuted(commandWrapper);
		}
	
		public static CommandWrapper FindCommandFromPartial(string partialCommand, int index)
		{
			var matchingCommands = FindCommandFromPartial(partialCommand).ToArray();
	
			return matchingCommands.Length == 0 ? null : matchingCommands[index % matchingCommands.Length];
		}
	
		public static IEnumerable<CommandWrapper> FindCommandFromPartial(string partialCommand)
		{
			return AllHandlers.Values.Where(x => x.CommandName.StartsWith(partialCommand, StringComparison.CurrentCultureIgnoreCase)).OrderByDescending(x => x.CommandName.Length).ToArray();
		}
	
		public static CommandWrapper GetCommandWrapper(string commandName)
		{
			var commandParts = StringExtensions.SplitCommandLine(commandName).ToArray();
	
			if(commandParts.Length == 0)
				return null;

			AllHandlers.TryGetValue(commandParts[0].ToLower(), out var commandHandler);
			return commandHandler;
		}
	
		/// <summary>
		/// Call this method to register an object by type. Objects that are registered will be parsed for the
		/// ConsoleCommand attribute.
		/// </summary>
		/// <param name="type">The type of object that you would like to register with .Command</param>
		public static void Register(Type type)
		{
			RegisterCommandWrappers(type, null);
		}
	
		/// <summary>
		/// Call this method to register an object by instance. Objects that are registered will be parsed for the
		/// ConsoleCommand attribute.
		/// </summary>
		/// <param name="obj">The object that you would like to register with .Command</param>
		public static void Register(object obj)
		{
			RegisterCommandWrappers(obj.GetType(), obj);
		}
		
		/// <summary>
		/// If you have called Register on an object, you should match that call with an unregister
		/// </summary>
		/// <param name="obj">The object that you would like to unregister from .Command</param>
		public static void Unregister(object obj)
		{
			if (!RegisteredHandlers.Contains(obj.GetType()))
				return;
			
			var itemsToRemove = AllHandlers.Where(i => i.Value.ObjectReference != null && i.Value.ObjectReference.Target == obj).Select(i => i.Key).ToList();
			
			foreach(var item in itemsToRemove)
			{
				CommandHandlerRemoved(AllHandlers[item]);
				AllHandlers.Remove(item);
			}
			
			RegisteredHandlers.Remove(obj.GetType());
		}
		
		/// <summary>
		/// If you have called Register on a type, you should match that call with an unregister
		/// </summary>
		/// <param name="type">The type of object that you would like to unregister from .Command</param>
		public static void Unregister(Type type)
		{
			if(!RegisteredHandlers.Contains(type))
				return;
			
			var itemsToRemove = AllHandlers.Where(i => i.Value.Type == type).Select(i => i.Key).ToList();
			foreach(var item in itemsToRemove)
			{
				CommandHandlerRemoved(AllHandlers[item]);
				AllHandlers.Remove(item);
			}
			
			RegisteredHandlers.Remove(type);
		}
		#endregion
	
		#region implementation
		private static void RegisterCommandWrappers(Type type, object obj)
		{
			if(RegisteredHandlers.Contains(type))
				return;
	
			RegisteredHandlers.Add(type);
			RegisterCommandHandlerMethods(type, obj);
			RegisterCommandHandlerProperties(type, obj);
		}
	
		private static void RegisterCommandHandlerMethods(Type type, object obj)
		{
			var methods = obj == null 
				? type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) 
				: type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

			foreach(var methodInfo in methods)
			{
				var attributes = methodInfo.GetCustomAttributes(typeof(ConsoleCommandAttribute), true);
	
				if(attributes.Length == 0)
					continue;

				if (!(attributes[0] is ConsoleCommandAttribute attribute))
					continue;
				
				var commandName = attribute.Name;
				if(string.IsNullOrEmpty(commandName))
					commandName = methodInfo.Name;
	
				RegisterMethodCommandHandler(type, obj, methodInfo, attribute, commandName);
			}
		}
	
		private static void RegisterCommandHandlerProperties(Type type, object obj)
		{
			var properties = obj == null 
				? type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) 
				: type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

			foreach(var property in properties)
			{
				var attributes = property.GetCustomAttributes(typeof(ConsoleCommandAttribute), true);
	
				if(attributes.Length == 0)
					continue;

				if (!(attributes[0] is ConsoleCommandAttribute attribute)) 
					continue;
				
				var commandName = attribute.Name;
				if(string.IsNullOrEmpty(commandName))
					commandName = property.Name;
	
				RegisterPropertyCommandHandler(type, obj, property, attribute, commandName);
			}
		}
	
		private static void RegisterMethodCommandHandler(Type type, object obj, MethodInfo method, ConsoleCommandAttribute attribute, string commandName)
		{
			System.Diagnostics.Debug.Assert(method.DeclaringType != null, "method.DeclaringType != null");
			var propertyName = method.DeclaringType?.FullName + "." + method.Name;

			if(!ValidateCommandHandlerName(commandName, propertyName))
				return;
	
			var commandHandler = new MethodCommandWrapper(commandName, attribute.Description, type, obj, method);
			AllHandlers.Add(commandName.ToLower(), commandHandler);	
			CommandHandlerAdded(commandHandler);
		}
	
		private static void RegisterPropertyCommandHandler(Type type, object obj, PropertyInfo property, ConsoleCommandAttribute attribute, string commandName)
		{
			System.Diagnostics.Debug.Assert(property.DeclaringType != null, "property.DeclaringType != null");
			var propertyName = property.DeclaringType?.FullName + "." + property.Name;
	
			if(!ValidateCommandHandlerName(commandName, propertyName)) 
				return;
	
			var commandHandler = new PropertyCommandWrapper(commandName, attribute.Description, type, obj, property);
			AllHandlers.Add(commandName.ToLower(), commandHandler);
			CommandHandlerAdded(commandHandler);
		}
	
		private static bool ValidateCommandHandlerName(string commandName, string propertyName)
		{
			var lowerCaseCommandName = commandName.ToLower();

			if(lowerCaseCommandName.Contains(" "))
			{
				var warning = $"Command named {lowerCaseCommandName} is invalid. Command names cannnot contain spaces";
				Debug.LogWarning(warning);
				return false;
			}

			if (!AllHandlers.ContainsKey(lowerCaseCommandName)) 
				return true;
			
			{
				var warning = $"Command named {lowerCaseCommandName} has already been registered. Ignoring {propertyName}.";
				Debug.LogWarning(warning);
				return false;
			}

		}
		#endregion
	}
}