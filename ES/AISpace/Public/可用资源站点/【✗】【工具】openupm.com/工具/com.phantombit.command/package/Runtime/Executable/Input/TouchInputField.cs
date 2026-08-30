using System.Linq;
using UnityEngine;
using WellFired.Command.Skins;
using WellFired.Command.Unity.Runtime.CommandHandlers;
using WellFired.Command.Unity.Runtime.Console;
using WellFired.Command.Unity.Runtime.Extensions;
using WellFired.Command.Unity.Runtime.Modals;
using WellFired.Command.Unity.Runtime.UnityGui;
using WellFired.Command.Unity.Runtime.Wrapper;

namespace WellFired.Command.Unity.Runtime.Input
{
	internal class TouchInputField : IInputField
	{
		public const string FocusOutId = "wellFired.command";
		private string _commandInput = string.Empty;

		public bool HasFocus => true;

		private LogHistoryGui LogHistoryView
		{
			get;
		}

		private TextEditor TextEditor
		{
			get;
			set;
		}

		private int TabIndex
		{
			get;
			set;
		}
		
		public Rect Rect
		{
			get;
			private set;
		}
		
		public string PreviousCompleteInput
		{
			get;
			private set;
		}

		public string[] PreviousCompleteParameters
		{
			get;
			private set;
		}
		
		public int CurrentParameterIndex
		{
			get
			{
				var currentParameter = PreviousCompleteParameters.Length - 2;
				
				if(PreviousCompleteInput.EndsWith(" "))
				{
					currentParameter++;
				}
				
				return currentParameter;
			}
		}

		public string Input
		{
			get => _commandInput;
			set
			{
				_commandInput = value;

				TextEditor.text = _commandInput;
				TextEditor.MoveLineEnd();

				var fakeEvent = new Event
				{
					keyCode = KeyCode.RightArrow,
					type = EventType.KeyDown
				};
				TextEditor.HandleKeyEvent(fakeEvent);
			}
		}
	
		public TouchInputField(LogHistoryGui logHistoryGuiView)
		{
			PreviousCompleteInput = string.Empty;
			PreviousCompleteParameters = new string[] {};
			LogHistoryView = logHistoryGuiView;
		}
	
		public void Draw(ISkinData skinData)
		{
			DevelopmentConsole.Instance.ForceMinimize = TouchScreenKeyboard.visible;
			
			if(Event.current.type == EventType.KeyDown)
			{
				if(Event.current.keyCode == KeyCode.Return)
				{
					if(_commandInput != string.Empty)
					{
						DevelopmentCommands.HandleCommand(_commandInput);
						_commandInput = string.Empty;
						LogHistoryView.AutoScrolling = true;
						FinaliseInput();
					}
				}
				else if(Event.current.keyCode == KeyCode.Tab)
					HandleTabKeyPressed();
			}
	
			if(Event.current.isKey && (Event.current.keyCode == KeyCode.Tab || Event.current.character == 9))
			{
				Event.current.type = EventType.Used;
			}

			GUI.SetNextControlName("Input");
			var previousCommandInput = _commandInput;
			var input = string.Empty;
			using (new GuiBeginHorizontal())
			{
				input = Helper.TextEntry(skinData, _commandInput, GUILayout.ExpandWidth(true));
				if (Helper.Button(skinData, "Execute", GUILayout.ExpandWidth(false)))
				{
					DevelopmentCommands.HandleCommand(_commandInput);
					Input = string.Empty;
					_commandInput = string.Empty;
					LogHistoryView.AutoScrolling = true;
					FinaliseInput();
					return;
				}
			}

			_commandInput = DevelopmentConsole.Instance.CheckInputForTilde(input);
	
			if(Event.current.type == EventType.Repaint)
			{
				Rect = GUILayoutUtility.GetLastRect();
			}

			if(DevelopmentConsole.Instance.JustMadeVisible)
			{
				DevelopmentConsole.Instance.JustMadeVisible = false; 
				GUI.FocusControl("Input");
				TextEditor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
			}

			if (previousCommandInput == _commandInput) 
				return;
			
			TabIndex = 0;
			FinaliseInput();
		}
	
		private void HandleTabKeyPressed()
		{
			if(PreviousCompleteParameters == null || PreviousCompleteParameters.Length == 0)
			{
				TabIndex++;
				return;
			}

			var currentParameter = PreviousCompleteParameters.Length - 2;
	
			if(PreviousCompleteInput.EndsWith(" "))
			{
				currentParameter++;
			}

			// Completing the command or the parameter
			if(currentParameter < 0)
			{
				var closestMatch = DevelopmentCommands.FindCommandFromPartial(PreviousCompleteParameters[0], TabIndex);
				if(closestMatch != null)
				{
					Input = closestMatch.CommandName;
				}

				TabIndex++;
				return;
			}

			var commandHandler = DevelopmentCommands.GetCommandWrapper(PreviousCompleteParameters[0]);
			if(commandHandler.Equals(default(CommandWrapper)))
			{
				TabIndex++;
				return;
			}

			var parameters = commandHandler.Parameters;
			if(currentParameter < parameters.Length)
			{
				var parameterValue = PreviousCompleteParameters.Length < currentParameter + 2 ? string.Empty : PreviousCompleteParameters[currentParameter + 1];
				var possibleParameters = parameters[currentParameter].GetParameterPossibleValues(parameterValue, PreviousCompleteParameters).ToArray();
				if(possibleParameters.Length != 0)
				{
					var closestMatchingParameterValue = possibleParameters[TabIndex % possibleParameters.Length];
					if(!string.IsNullOrEmpty(closestMatchingParameterValue))
					{
						var parts = string.Join(" ", PreviousCompleteParameters.SubArray(0, currentParameter + 1));
						Input = parts + " " + closestMatchingParameterValue;
					}
				}
			}

			TabIndex++;
		}
	
		public void LoseFocus()
		{
			TabIndex = 0;
			
			GUI.SetNextControlName(FocusOutId);
			GUI.Label(new Rect(-100, -100, 1, 1), "");
			GUI.FocusControl(FocusOutId);
		}
	
		public void Focus()
		{
			GUI.FocusControl("Input");
		}

		public void FinaliseInput()
		{
			PreviousCompleteParameters = StringExtensions.SplitCommandLine(Input).ToArray();
			PreviousCompleteInput = Input;
		}
	}
}