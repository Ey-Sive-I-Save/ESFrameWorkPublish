using System.Collections.Generic;
using WellFired.Command.Unity.Runtime.Suggestion;
using WellFired.Command.Unity.Runtime.UnityGui;

namespace WellFired.Command.Unity.Runtime.Console
{
    /// <summary>
    /// Present sensible scale suggestions to the user, + / - x around currentScale
    /// </summary>
    public class ScaleSuggestion : ISuggestion
    {
        public ScaleSuggestion() { }

        public IEnumerable<string> Suggestion(IEnumerable<string> previousArguments)
        {
            var suggestions = new List<string>();
            var currentScale = Helper.Scale;

            const float step = 0.1f;
            var iteration = step * -3;
            for (var n = 0; n < 7; n++)
            {
                var newSuggestion = currentScale + iteration;
                if (Helper.IsValidConsoleScale(newSuggestion))
                    suggestions.Add(newSuggestion.ToString("#.#"));
                
                iteration += 0.1f;
            }

            return suggestions;
        }
    }
}