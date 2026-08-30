using System;
using System.Collections.Generic;
using UnityEngine;

namespace WellFired.Command.Unity.Runtime.Helpers
{
	public static class Helper
	{
	    public static object GetArgumentValueFromString(string argument, Type type)
	    {
	        if (type == typeof(string))
	            return argument;
	        else if (type == typeof(int))
	            return int.Parse(argument);
	        else if (type == typeof(float))
	            return float.Parse(argument);
	        else if (type == typeof(bool))
	        {
	            if (argument == "1")
	                return true;
	            else if (argument == "0")
	                return false;
	            return bool.Parse(argument);
	        }
	        else if (type.IsEnum)
	            return Enum.Parse(type, argument, true);
	        else if (type == typeof(Vector3))
	        {
	            var parts = argument.Split(',');
	            if (parts.Length == 1)
	                return new Vector3(float.Parse(parts[0]), float.Parse(parts[0]), float.Parse(parts[0]));
	            else if (parts.Length == 3)
	                return new Vector3(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
	            else
	                throw new InvalidOperationException("Expected either 1 or 3 arguments.");
	        }
	        else if (type == typeof(Vector2))
	        {
	            var parts = argument.Split(',');
	            if (parts.Length == 1)
	                return new Vector2(float.Parse(parts[0]), float.Parse(parts[0]));
	            else if (parts.Length == 2)
	                return new Vector2(float.Parse(parts[0]), float.Parse(parts[1]));
	            else
	                throw new InvalidOperationException("Expected either 1 or 2 arguments.");
	        }
	
	        throw new InvalidOperationException($"Type {type} is not supported as a command handler argument.");
	    }
	
	    public static IEnumerable<string> GetDefaultParameterPossibleOptions(Type type)
	    {
	        var parameterOptions = new List<string>();
	
	        if (type.IsEnum)
	        {
	            parameterOptions.AddRange(Enum.GetNames(type));
	        }
	        else if (type == typeof(bool))
	        {
	            parameterOptions.Add("true");
	            parameterOptions.Add("false");
	        }
	
	        return parameterOptions;
		}
	}
}