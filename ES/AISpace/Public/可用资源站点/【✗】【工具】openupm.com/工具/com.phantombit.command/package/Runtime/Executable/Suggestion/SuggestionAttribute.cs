using System;

namespace WellFired.Command.Unity.Runtime.Suggestion
{
	/// <summary>
	/// This attribute can be used on parameters only.
	/// to specify that you will provide auto complete behaviour for that element. The type
	/// that is passed to the constructor must be a type that implements the ISuggestion
	/// interface.
	/// 
	/// Example usage : 
	/// private static void AddHealth([Suggestion(typeof(HealthSuggestion))]int healthToAdd)
	/// {
	/// 	Health.Add(healthToAdd);
	/// }
	/// 
	/// private class HealthSuggestion : ISuggestion
	/// {
	///		public IEnumerable<string/> Suggestion()
	///		{
	///			return new [] { "1", "10", "25", "50", "100" };			
	///		}
	///	}
	/// 
	/// </summary>
	[AttributeUsage(AttributeTargets.Parameter)]
	public class SuggestionAttribute : Attribute
	{
		#region Properties
	    public Type Type
	    {
	        get;
	        private set;
	    }
		#endregion
		
	    public SuggestionAttribute(Type type)
	    {
			var implementsInterface = typeof(ISuggestion).IsAssignableFrom(type);
			if(!implementsInterface)
				throw new Exception($"The type {type} passed as an SuggestionAttribute MUST implement the ISuggestion interface.");
	
	        Type = type;
	    }
	}
}