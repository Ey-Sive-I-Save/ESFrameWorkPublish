using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace WellFired.Command.Unity.Runtime.Extensions
{
	public static class StringExtensions
	{
		public static IEnumerable<string> Split(this string str, Func<char, bool> controller)
		{
			var nextPiece = 0;
			for(var c = 0; c < str.Length; c++)
			{
				if (controller(str[c]))
				{
					yield return str.Substring(nextPiece, c - nextPiece);
					nextPiece = c + 1;
				}
			}
			
			yield return str.Substring(nextPiece);
		}

		/// <summary>
		/// When passed a command, this method will split it into it's individual components.
		/// </summary>
		/// <returns>The seperated components</returns>
		public static IEnumerable<string> SplitCommandLine(string commandLine)
		{
			var inQuotes = false;
			return commandLine.Split(c =>
			{
				if (c == '\"')
					inQuotes = !inQuotes;
	
				return !inQuotes && c == ' ';
			})
			.Select(arg => arg.Trim().TrimMatchingQuotes('\"'))
			.Where(arg => !string.IsNullOrEmpty(arg));
		}

		/// <summary>
		/// This method will trim matching quotes
		/// </summary>
		/// <returns>The string contained within the matching quotes</returns>
		/// <param name="input">The string to trim</param>
		/// <param name="quote">The character that represents the quote</param>
		[PublicAPI]
		public static string TrimMatchingQuotes(this string input, char quote)
		{
			if ((input.Length >= 2) && (input[0] == quote) && (input[input.Length - 1] == quote))
				return input.Substring(1, input.Length - 2);
	
			return input;
		}
	}
}