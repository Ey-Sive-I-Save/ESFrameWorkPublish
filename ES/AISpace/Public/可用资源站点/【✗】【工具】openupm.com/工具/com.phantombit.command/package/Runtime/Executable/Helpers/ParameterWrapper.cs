using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WellFired.Command.Unity.Runtime.Log;
using WellFired.Command.Unity.Runtime.Suggestion;

namespace WellFired.Command.Unity.Runtime.Helpers
{
	public class ParameterWrapper
	{
		#region Fields
	    public string Name;
	    public bool IsOptional;
	    public Type Type;
	    public object DefaultValue;
		public ISuggestion SuggestionObject;
		#endregion

		#region Methods
		public List<string> GetParameterPossibleValues(string parameterValue, IEnumerable<string> lastTypedParameters)
	    {
	        IEnumerable<string> parameterOptions;
	
	        if (SuggestionObject != null)
	        {
				var suggestions = SuggestionObject.Suggestion(lastTypedParameters);
	            if (suggestions == null)
	            {
	                DebugLogHistory.Instance.LogMessage("Invalid return type from auto complete method", LogType.Error);
					parameterOptions = Helper.GetDefaultParameterPossibleOptions(Type);
	            }
	            else
	                parameterOptions = suggestions;
	        }
	        else
				parameterOptions = Helper.GetDefaultParameterPossibleOptions(Type);
	
	        var parameterOptionsList = parameterOptions.ToList();
	        var options = parameterOptionsList.Where(x => x.StartsWith(parameterValue, StringComparison.CurrentCultureIgnoreCase)).ToList();
	        var partialMatches = parameterOptionsList.Where(x => x.IndexOf(parameterValue, StringComparison.CurrentCultureIgnoreCase) >= 0);
	
	        options.AddRange(
	            parameterOptionsList.Where(x => !options.Contains(x)).Where(
	                x => x.IndexOf(parameterValue, StringComparison.CurrentCultureIgnoreCase) >= 0));

	        return options.Union(partialMatches).ToList();
	    }
		#endregion
	}
}