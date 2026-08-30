using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WellFired.Command.Skins;
using WellFired.Command.Unity.Runtime.CommandHandlers;
using WellFired.Command.Unity.Runtime.Console;
using WellFired.Command.Unity.Runtime.Extensions;
using WellFired.Command.Unity.Runtime.Input;
using WellFired.Command.Unity.Runtime.UnityGui;

namespace WellFired.Command.Unity.Runtime.Helpers
{
	internal class Suggestions
	{
	    private class SuggestionButton
	    {
		    private readonly ISkinData _skinData;
		    private float _width;
	
	        public float Width
	        {
	            get
	            {
	                if (Math.Abs(_width) > 0.00001f)
	                    return _width;

		            if (Event.current.type == EventType.Layout)
		            {
			            var style = UnityGui.Helper.SuggestionButtonStyle(_skinData);
			            _width =  style.CalcSize(Content).x;
		            }
	
	                return _width;
	            }
	        }
	
	        public GUIContent Content { get; }
		    public bool AutomaticallyExecute { get; }
		    public string Input { get; }
			
			public SuggestionButton(ISkinData skinData, string label, string input, bool automaticallyExecute)
			{
				_skinData = skinData;
				Input = input + (automaticallyExecute ? "" : " ");
				AutomaticallyExecute = automaticallyExecute;
				Content = new GUIContent(label + (automaticallyExecute ? "" : "..."));
			}
	    }
	
	    private readonly List<SuggestionButton> _suggestionButtons = new List<SuggestionButton>();
		private readonly ISkinData _skinData;
		private readonly IInputField _inputField;
	    private string _lastInput;
	    private bool _firstRun = true;
	
		public Suggestions(ISkinData skinData, IInputField inputField)
		{
			_skinData = skinData;
			_inputField = inputField;
		}
	
	    public void Update()
	    {
		    if (!_firstRun && _inputField.PreviousCompleteInput == _lastInput) 
			    return;
		    
		    _firstRun = false;
		    _lastInput = _inputField.PreviousCompleteInput;
		    GenerateSuggestionButtons();
	    }
	
	    private void GenerateSuggestionButtons()
	    {
	        _suggestionButtons.Clear();

	        if(_lastInput.Contains(" ") && _lastInput.Trim().Length != 0)
	            GenerateParameterSuggestionButtons();
	        else if(_lastInput == string.Empty)
	            GenerateRecentCommandButtons();
	        else
	            GeneratePossibleCommandButtons();
		}
		
		public void Draw(ISkinData skinData, Rect rect)
		{
			const float margin = 20.0f;
			const int rowLimit = 2;
			
			var padding = Selector.OnPlatform(_skinData.ButtonSpacing, _skinData.ButtonSpacingTouch);

			var style = UnityGui.Helper.SuggestionButtonStyle(skinData);
			var itemHeight = style.CalcHeight(new GUIContent("["), 10.0f);       
			var x = rect.x;
			var y = rect.y;
			var rowsRemaining = rowLimit;
			var itemCount = _suggestionButtons.Count;
			
			foreach(var button in _suggestionButtons)
			{
				if(rowsRemaining == 0)
				{
					var moreItemsLabel = itemCount + " more...";
					var moreContent = new GUIContent(moreItemsLabel);
					var moreMaxWidth = style.CalcSize(moreContent).x;
					
					if(x + button.Width + moreMaxWidth > rect.width - margin)
					{
						var moreRect = new Rect(x, y, moreMaxWidth, itemHeight);
						UnityGui.Helper.Label(_skinData, moreRect, moreContent);
						GUI.Label(moreRect, moreContent, style);
						break;
					}
				}
				
				if(x + button.Width > rect.width - margin)
				{
					rowsRemaining--;
					
					y += itemHeight + padding;
					x = rect.x;
				}
				
				var itemRect = new Rect(x, y, button.Width, itemHeight);
				x += button.Width + padding;
				
				if(UnityGui.Helper.Button(_skinData, itemRect, button.Content.text))
				{
					if(button.AutomaticallyExecute)
					{
						DevelopmentCommands.HandleCommand(button.Input);
						_inputField.Input = "";
					}
					else
					{
						_inputField.Input = button.Input;
						Event.current.Use();
					}
						
					_inputField.FinaliseInput();
				}
				
				itemCount--;
			}
		}
		
	    private void GeneratePossibleCommandButtons()
	    {
			var matchingCommands = DevelopmentCommands.FindCommandFromPartial(_inputField.PreviousCompleteParameters[0]).ToArray();
	        foreach(var matchingCommand in matchingCommands)
	        {
	            var hasParameters = matchingCommand.Parameters.Length != 0;
	            _suggestionButtons.Add(new SuggestionButton(_skinData, matchingCommand.CommandName, matchingCommand.CommandName, !hasParameters));
	        }
	    }
	
	    private void GenerateParameterSuggestionButtons()
	    {
	        var lastTypedParameters = _inputField.PreviousCompleteParameters;
			var commandHandler = DevelopmentCommands.GetCommandWrapper(lastTypedParameters.First());
	
	        if(commandHandler == null)
	            return;
	
	        var commandHandlerParameters = commandHandler.Parameters.ToArray();
	        var currentParameterIndex = _inputField.CurrentParameterIndex;
	
	        if(currentParameterIndex >= commandHandlerParameters.Length)
	            return;
	
	        var parameterValue = currentParameterIndex + 1 < lastTypedParameters.Length ? lastTypedParameters[currentParameterIndex + 1] : "";
	        var parameterOptions = commandHandler.Parameters[currentParameterIndex].GetParameterPossibleValues(parameterValue, lastTypedParameters);
	
	        var isLastParameter = currentParameterIndex == commandHandlerParameters.Length - 1;
	        var commandUpToLastParameter = string.Join(" ", lastTypedParameters.SubArray(0, currentParameterIndex + 1));
	
	        _suggestionButtons.AddRange(parameterOptions.Select(x => new SuggestionButton(_skinData, x, commandUpToLastParameter + " " + x, isLastParameter)));
	    }
	
	    private void GenerateRecentCommandButtons()
	    {
		    var commands = DevelopmentConsole.Instance.RecentCommands.ToList();
		    if(!commands.Any())
			    commands.Add("ConsoleScale");
		    
	        foreach(var recentCommand in commands)
	        {
				var commandHandler = DevelopmentCommands.GetCommandWrapper(recentCommand);
	            if(commandHandler == null)
	                continue;
	
	            var hasParameters = commandHandler.Parameters.Length != 0;
	            _suggestionButtons.Add(new SuggestionButton(_skinData, commandHandler.CommandName, commandHandler.CommandName, !hasParameters));
	        }
	    }
	}
}