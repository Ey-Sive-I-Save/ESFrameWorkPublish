using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using WellFired.Command.Unity.Runtime.Helpers;
using WellFired.Command.Unity.Runtime.Log;
using WellFired.Command.Unity.Runtime.Suggestion;
using WellFired.Command.Unity.Runtime.Wrapper;

namespace WellFired.Command.Unity.Runtime.CommandHandlers
{
	/// <summary>
	/// The command wrapper that contains cached information about Methods.
	/// </summary>
	public class MethodCommandWrapper : CommandWrapper
	{
		#region Fields
	    private readonly MethodInfo _methodInfo;
		#endregion
	
		#region Properties
		public override ParameterWrapper[] Parameters { get; }
		#endregion
	
		#region Methods
		public MethodCommandWrapper(string commandName, string description, Type type, object referenceObject, MethodInfo methodInfo)
	        : base(commandName, description, type, referenceObject)
	    {
			var parameters = methodInfo.GetParameters();
			Parameters = parameters.Select(x => new ParameterWrapper()
	        {
	            IsOptional = x.IsOptional,
	            Name = x.Name,
	            Type = x.ParameterType,
	            DefaultValue = x.DefaultValue,
	            SuggestionObject = GetSuggestionMethod(x)
	        }).ToArray();
	
	        _methodInfo = methodInfo;
	
	        if(methodInfo.DeclaringType != null)
		        TheMethodOrPropertyName = methodInfo.DeclaringType.FullName + "." + methodInfo.Name;
	        else
		        TheMethodOrPropertyName = methodInfo.Name;
	    }
	
	    public override bool Equals(object otherObject)
	    {
	        var otherCommandHandler = otherObject as MethodCommandWrapper;
	        if(otherCommandHandler == null)
	            return false;
	
	        if(base.Equals(otherObject) == false)
	            return false;
	
	        return(_methodInfo == otherCommandHandler._methodInfo);
	    }
	
	    public override int GetHashCode()
	    {
	        return _methodInfo.GetHashCode() ^ base.GetHashCode();
	    }
	
	    public override void Invoke(params string[] arguments)
	    {
		    var invalidArguments = GetArgumentList(arguments, out var argumentValues);
	
	        if(invalidArguments)
	        {
				var warning = $"The command {CommandName} expects the arguments {GetParametersAsString()}";
				DebugLogHistory.Instance.LogMessage(warning, LogType.Warning);
	            return;
	        }

		    _methodInfo.Invoke(ObjectReference?.Target, argumentValues);
	    }
		#endregion

		#region Implementation
		protected bool GetArgumentList(string[] commandArguments, out object[] argumentValues)
		{
			var methodParameters = Parameters;

			argumentValues = new object[methodParameters.Length];
			var invalidArguments = false;
			for(var i = 0; i < methodParameters.Length; ++i)
			{
				var parameter = methodParameters[i];
				
				if(commandArguments.Length <= i)
				{
					if(parameter.IsOptional)
					{
						argumentValues[i] = parameter.DefaultValue;
						continue;
					}
					else
					{
						invalidArguments = true;
						break;
					}
				}
				
				var argument = commandArguments[i];
				try
				{
					argumentValues[i] = Helper.GetArgumentValueFromString(argument, parameter.Type);
				}
				catch(Exception)
				{
					invalidArguments = true;
					break;
				}
			}
			return invalidArguments;
		}
		
		protected static ISuggestion GetSuggestionMethod(ParameterInfo paramInfo)
		{
			var attributes = paramInfo.GetCustomAttributes(typeof(SuggestionAttribute), true);
			if(attributes.Length == 0)
				return null;
			
			var suggestionAttribute = (SuggestionAttribute)attributes[0];
			if(suggestionAttribute == null)
				return null;

			var suggestionType = suggestionAttribute.Type;
			return Activator.CreateInstance(suggestionType) as ISuggestion;
		}
		#endregion
	}
}