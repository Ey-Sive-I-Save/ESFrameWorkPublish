using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using UnityEngine;
using WellFired.Command.Unity.Runtime.CommandHandlers;
using WellFired.Command.Unity.Runtime.Log;
using Object = UnityEngine.Object;

namespace WellFired.Command.Unity.Runtime.Console
{
	public static class Inspect
	{
		[PublicAPI]
		public static string WildcardToRegex(string pattern)
		{
			return pattern == string.Empty ? ".*" : $"^{Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".")}{"$"}";
		}
	
		private static IEnumerable<GameObject> FindMatchingGameObjects(string gameObjectName)
		{
			var regexp = WildcardToRegex(gameObjectName);
			var re = new Regex(regexp, RegexOptions.IgnoreCase);
			return Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Where(x => re.IsMatch(x.name));
		}
		
		[ConsoleCommand(Name = "InspectAllGameObjects", Description = "Lists all Game Objects in the scene")]
		[UsedImplicitly]
		private static void InspectAllGameObjects()
		{
			InspectGameObjectProperty(string.Empty, string.Empty, string.Empty);
		}
		
		[ConsoleCommand(Name = "InspectGameObject", Description = "Lists all Game Objects in the scene")]
		[PublicAPI]
		private static void InspectGameObject(string gameObjectName)
		{
			InspectGameObjectProperty(gameObjectName, string.Empty, string.Empty);
		}
		
		[ConsoleCommand(Name = "InspectGameObjectComponent", Description = "Lists all Game Objects in the scene")]
		[PublicAPI]
		private static void InspectGameObjectComponent(string gameObjectName, string component)
		{
			InspectGameObjectProperty(gameObjectName, component, string.Empty);
		}
	
		[ConsoleCommand(Name = "InspectGameObjectProperty", Description = "Allows you to inspect the values on a game object. Parameters may contain * and ? wildcards.")]
		[PublicAPI]
		private static void InspectGameObjectProperty(string gameObjectName, string componentName, string propertyName)
		{
			var matchingGameObjects = FindMatchingGameObjects(gameObjectName).ToList();
	
			if(matchingGameObjects.Count == 0)
			{
				DebugLogHistory.Instance.LogMessage("Couldn't find any game objects that match", LogType.Error);
				return;
			}
	
			if(gameObjectName == string.Empty)
			{
				DebugLogHistory.Instance.LogMessage("Click to see a list of all game objects in the scene: \n\t" + string.Join("\n\t", matchingGameObjects.Select(x => x.name).OrderBy(x => x).ToArray()));
				return;
			}
	        
			var stringBuilder = new StringBuilder();
			
			stringBuilder.AppendLine($"Click to see the result of your inspection of gameObject : {gameObjectName}");
			
			foreach(var gameObject in matchingGameObjects)
				OutputGameObject(gameObject, componentName, propertyName, stringBuilder); 
	
			DebugLogHistory.Instance.LogMessage(stringBuilder.ToString());
		}
	
		private static void OutputGameObject(GameObject gameObject, string componentName, string propertyName, StringBuilder stringBuilder)
		{
			var propertyNameRegExp = new Regex(WildcardToRegex(propertyName), RegexOptions.IgnoreCase);
			var componentRegExp = new Regex(WildcardToRegex(componentName), RegexOptions.IgnoreCase);
	        
			var componentProperties = (from component in gameObject.GetComponents<Component>()
				let type = component.GetType()
				where componentRegExp.IsMatch(type.Name)
				let fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
					.Where(field => propertyNameRegExp.IsMatch(field.Name))
					.ToList()
				let properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
					.Where(property => propertyNameRegExp.IsMatch(property.Name))
					.ToList()
				where fields.Count != 0 || properties.Count != 0
				select new ComponentProperties()
				{
					ComponentName = type.Name,
					Fields = fields,
					Properties = properties,
					Component = component
				}).ToList();

			if(componentProperties.Count == 0)
				return;
	
			stringBuilder.AppendLine(gameObject.name);
	
			for(var i = 0; i < componentProperties.Count; ++i)
			{
				if(i != 0)
				{
					stringBuilder.AppendLine();
				}
	
				var componentProps = componentProperties[i];
				stringBuilder.Append("    ");
				stringBuilder.AppendLine(componentProps.ComponentName);
	
				foreach(var field in componentProps.Fields)
				{
					try
					{
						var data = field.GetValue(componentProps.Component);
						stringBuilder.Append("        ");
						stringBuilder.Append(field.Name);
						stringBuilder.Append(" = ");
						stringBuilder.Append(data);
						stringBuilder.AppendLine();
					}
					catch (Exception)
					{
						// ignored
					}
				}
	
				foreach(var property in componentProps.Properties)
				{
					try
					{
						var value = property.GetValue(componentProps.Component, null);

						stringBuilder.Append("        ");
						stringBuilder.Append(property.Name);
						stringBuilder.Append(" = ");
						stringBuilder.Append(value);
						stringBuilder.AppendLine();
					}
					catch (Exception)
					{
						// ignored
					}
				}
			}
		}
	}
}