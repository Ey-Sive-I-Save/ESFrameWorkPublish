using System.Collections.Generic;

namespace WellFired.Command.Unity.Runtime.Suggestion
{
	/// <summary>
	/// The Interface for an IAutoCompletable Object, this is used by the DevelopmentConsole to 
	/// auto complete suggestions for the user. You can provide your own Auto Complete method,
	/// E.G.
	/// 
	/// class ConsoleSuggestionComplete : ISuggestion
	/// {
	///		public IEnumerable<string/> Suggestion(IEnumerable<string/> previousArguments)
	///		{
	///			return new [] { "-150", "-100", "-50", "+50", "+100", "+150" };
	///		}
	///	}
	///	
	///	[ConsoleCommand]
	///	private static void ConsoleSize([Suggestion(typeof(ConsoleSizeSuggestion))]int dpi)
	///	{
	/// 
	///	}
	/// </summary>
	public interface ISuggestion 
	{
		/// <summary>
		/// This method will be used by the .Command to determine
		/// the auto complete values that should be used.
		/// </summary>
		/// <param name="previousArguments">The Previous Arguments</param>
		IEnumerable<string> Suggestion(IEnumerable<string> previousArguments);
	}
}