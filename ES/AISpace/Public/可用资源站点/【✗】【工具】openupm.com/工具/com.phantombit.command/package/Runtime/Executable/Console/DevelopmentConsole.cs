using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using WellFired.Command.Skins;
using WellFired.Command.Unity.Runtime.CommandHandlers;
using WellFired.Command.Unity.Runtime.Email;
using WellFired.Command.Unity.Runtime.Extensions;
using WellFired.Command.Unity.Runtime.Helpers;
using WellFired.Command.Unity.Runtime.Input;
using WellFired.Command.Unity.Runtime.Loaders;
using WellFired.Command.Unity.Runtime.Log;
using WellFired.Command.Unity.Runtime.Modals;
using WellFired.Command.Unity.Runtime.Suggestion;
using WellFired.Command.Unity.Runtime.UI.Windows;
using WellFired.Command.Unity.Runtime.UnityGui;
using WellFired.Command.Unity.Runtime.Wrapper;
using Helper = WellFired.Command.Unity.Runtime.UnityGui.Helper;

#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM_ENABLED
using UnityEngine.InputSystem;
#endif

namespace WellFired.Command.Unity.Runtime.Console
{
	/// <summary>
	/// Console is a MonoBehaviour that opens an in game .Command, this console
	/// can call any Property / Method that is marked up with the ConsoleCommandAttribute.
	/// 
	/// You will also get lots of nifty features, such as auto completion.
	/// </summary>
	public class DevelopmentConsole : MonoBehaviour
	{	
		private const string CloseButtonText = "Close";

		private bool _visible;
		private List<string> _recentCommands = new List<string>();

		/// <summary>
		/// Set this if you would like to change the message displayed on the 'open .Command' button.
		/// </summary>
		public string ShowDotCommandButtonMessage { get; [PublicAPI] set; } = "Show .Command (~)";
		
		/// <summary>
		/// Set this if you would like to change corner of the screen the 'open .Command' button is located.
		/// </summary>
		public DisplayCorner DisplayCorner { get; [PublicAPI] set; } = DisplayCorner.TopLeft;

		/// <summary>
		/// This Action will be triggered when the visible state of .Command changes.
		/// If it's called with true, it means .Command became visible, if it's called with false, it means
		/// .Command was hidden
		/// </summary>
		public Action<bool> VisibleStateChange = delegate {  };

		/// <summary>
		/// Gets or sets the singleton instance of .Command.
		/// </summary>
		/// <value>The instance.</value>
		public static DevelopmentConsole Instance { get; private set; }
	
		/// <summary>
		/// Is the console maximised
		/// </summary>
		/// <value><c>true</c> if this instance is maximized; otherwise, <c>false</c>.</value>
		[PublicAPI]
		public bool IsMaximized { get; set; }

		/// <summary>
		/// Should we draw the 'open .Command' button or not.
		/// </summary>
		/// <value><c>true</c> if draw show console button; otherwise, <c>false</c>.</value>
		[PublicAPI]
		public bool DrawShowDotCommandButton { get; set; }
		
		/// <summary>
		/// Has the user clicked on the Force Hide button. If so, they can re-open the console with the ~ key or by setting this value to false.
		/// </summary>
		/// <value><c>true</c> if force minimize; otherwise, <c>false</c>.</value>
		[PublicAPI]
		public bool ForceMinimize { get; set; }

		/// <summary>
		/// Since .Command is based on Unity legacy GUI, we use the new UI system to block input behind the console.
		/// This property allows to set the sorting order of the canvas used to block input. It is by default in front
		/// of everyting.
		/// </summary>
		[PublicAPI]
		public int InputBlockerSortingOrder
		{
			private get => _inputBlockerSortingOrder;
			set
			{
				_inputBlockerSortingOrder = value;
				_uiBlocker.SetSortingOrder(value);
			}
		}
		
		public bool JustMadeVisible { get; set; }
		private FilterWindow FilterWindow { get; set; }
		private DebugLogHistory DebugLogHistory { get; set; }
		private LogHistory LogHistory { get; set; }
		private LogHistoryGui LogHistoryGui { get; set; }
		private CommandsWindow CommandsWindow { get; set; }
		private LogEntryPopupWindow LogEntryPopupWindow { get; set; }
		private IInputField ConsoleInputField { get; set; }
		private bool ShowOnException { get; set; }
		private bool ShowOnError { get; set; }
		private bool AutoOpen { get; set; }
		private bool ConsoleEnabled { get; set; }
		private EmailViewModal EmailViewModal { get; set; }
		private Filter Filter { get; set; }
		private Suggestions Suggestions { get; set; }
		private int _inputBlockerSortingOrder;
		internal bool EmailLogSupportEnabled { get; set; }

		private UIBlocker _uiBlocker;
		
		/// <summary>
		/// GUI.Window will not get mouse event if it occurs outside of its rect. This variable allows to inform it.
		/// It's ugly and a better way is welcome.
		/// </summary>
		private bool _mouseUpOutsideOfWindow;

		[PublicAPI]
		public bool IsVisible
		{
			get => _visible;
			set
			{
				if(!_visible && value)
					JustMadeVisible = true;

				var delta = value != _visible;
				_visible = value;

				if(delta)
					VisibleStateChange(IsVisible);
			}
		}

		/// <summary>
		/// You can query this in your game, to see if the game should accept Input. This will return false if the console is showing for any reason.
		/// </summary>
		/// <value><c>true</c> if should accept game input; otherwise, <c>false</c>.</value>
		[PublicAPI]
		public bool ShouldAcceptGameInput => !IsVisible;

		public IEnumerable<string> RecentCommands => _recentCommands;
		
		[PublicAPI]
		public ISkinData SkinData
		{
			get;
			set;
		}

		[PublicAPI]
		public static float ScreenWidth => Screen.width * 0.9f;

		/// <summary>
		/// This method allows you to add more filters to the consol at runtime, .Command calls this internally when you
		/// pass filters to load, but you can additionally call this if you need to append more. (I.E.) you load modules
		/// at runtime and can't be sure of which enums will be needed at load time.
		/// </summary>
		/// <param name="customFilterProvider">This type should be an enum, which holds the types you'd like to use
		/// when filtering.</param>
		[PublicAPI]
		public void AddCustomFilters(Type customFilterProvider)
		{
			if(customFilterProvider == null)
				return;
			
			if(!customFilterProvider.IsEnum)
				Debug.LogWarning($"{customFilterProvider} is not an enum, so it can't be used as a custom filter");
				
			var newFilters = Enum.GetNames(customFilterProvider).Select(o => new Filter.FilterState(o.ToString(), true));
			Filter.AddCustomFilters(newFilters);
		}

		/// <summary>
		/// Call this method to load a single instance of .Command. You can
		/// then access the instance through the Instance property.
		/// </summary>
		[PublicAPI]
		public static void Load(
			bool clearConsoleCommandEnabled = true, 
			bool deviceIdCommandEnabled = true, 
			bool inspectCommandEnabled = true, 
			bool emailLogSupportEnabled = true, 
			bool autoScrollEnabled = true,
			Type customFilterType = null)
		{
			if(Instance != null)
				return;
				
			var skin = SettingsData.Load();

			var debugConsoleGo = new GameObject(typeof(DevelopmentConsole).Name);
			Instance = debugConsoleGo.AddComponent<DevelopmentConsole>();
			DontDestroyOnLoad(debugConsoleGo);

			Instance.Begin(
				skin.skinData, 
				clearConsoleCommandEnabled, 
				deviceIdCommandEnabled, 
				inspectCommandEnabled, 
				emailLogSupportEnabled, 
				autoScrollEnabled);
			Instance.AddCustomFilters(customFilterType);
			
			DevelopmentCommands.CommandExecuted += Instance.OnCommandExecuted;

			if (EmailFactory.GetEmailSender(Instance.EmailLogSupportEnabled).CanSendEmail())
				DevelopmentCommands.Register(typeof(EmailLogHelper));
		}

		/// <summary>
		/// This method will stop .Command from auto opening if an error is fired,
	 	/// you can still open it manually.
		/// </summary>
		[PublicAPI]
		public void DisableAutoOpen()
		{
			AutoOpen = false;
			IsVisible = false;
		}

		/// <summary>
		/// This method will make sure .Command auto opens if an error is fired.
		/// </summary>
		/// <param name="openOnException">Should .Command auto open on exception</param>
		/// <param name="openOnError">Should .Command auto open on error</param>
		[PublicAPI]
		public void EnableAutoOpen(bool openOnException = true, bool openOnError = false)
		{
			AutoOpen = true;
			ShowOnException = openOnException;
			ShowOnError = openOnError;
		}

		/// <summary>
		/// This method will hide all open popups.
		/// </summary>
		public void HideAllOpenPopups()
		{
			LogEntryPopupWindow.Hide();
			CommandsWindow.Hide();
			FilterWindow.Hide();
		}

		public void ClearTypedInput()
		{
			SetCommandInputTextAsIfUserHadTyped("");
			ConsoleInputField.LoseFocus();
		}

		/// <summary>
		/// You can call this method if you'd like to set input in .Command
		/// as though the user had typed it.
		/// </summary>
		/// <param name="text">The text to enter into .Command</param>
		public void SetCommandInputTextAsIfUserHadTyped(string text)
		{
			ConsoleInputField.Input = text;
			ConsoleInputField.FinaliseInput();
		}

		/// <summary>
		/// Checks the input for the close key and Closes the Development console if it is found.
		/// </summary>
		/// <returns>The Input without the close key.</returns>
		/// <param name="input">The Input.</param>
		public string CheckInputForTilde(string input)
		{
			if (input.Contains("`") || input.Contains("~"))
				IsVisible = false;
			
			return input.Replace("~", "").Replace("`", "");
		}

		/// <summary>
		/// Opens a the history of a specific Item.
		/// </summary>
		/// <param name="logEntry">Log Entry.</param>
		public void InspectLogEntry(LogEntry logEntry)
		{
			LogEntryPopupWindow.Show(logEntry);
			CommandsWindow.Hide();
		}

		/// <summary>
		/// Clears all output in .Commands Log History.
		/// </summary>
		[UsedImplicitly]
		public void Clear()
		{
			DebugLogHistory.Clear();
			LogHistoryGui.AutoScrolling = true;
		}
		
		/// <summary>
		/// Scroll to last entry and auto scroll.
		/// </summary>
		[UsedImplicitly]
		public bool AutoScroll
		{
			get => LogHistoryGui.AutoScrolling;
			set => LogHistoryGui.AutoScrolling = value;
		}

		/// <summary>
		/// Overrides the default scale of the consoles
		/// </summary>
		[ConsoleCommand(Description = "Overrides the consoles default scale")]
		[UsedImplicitly]
		private void ConsoleScale([Suggestion(typeof(ScaleSuggestion))] float newScale)
		{
			Helper.Scale = newScale;
		}
		
	    private void Begin(
		    ISkinData skinData, 
		    bool clearConsoleCommandEnabled, 
		    bool deviceIdCommandEnabled, 
		    bool inspectCommandEnabled, 
		    bool emailLogSupportEnabled, 
		    bool autoScrollEnabled)
	    {
		    EmailLogSupportEnabled = emailLogSupportEnabled;
		    DrawShowDotCommandButton = true;
		    SkinData = skinData;
		    
		    Filter = new Filter();
			ConsoleEnabled = true;
			ShowOnException = true;
		    ShowOnError = false;
		    AutoOpen = true;
			_recentCommands = new List<string>();

			DebugLogHistory = new DebugLogHistory(OnLogEntryAdded);

			FilterWindow = new FilterWindow(skinData, Filter);

			LogHistory = new LogHistory(DebugLogHistory, Filter);
			LogHistoryGui = new LogHistoryGui(skinData, LogHistory, this);
	        EmailViewModal = new EmailViewModal(LogHistory);

			FilterWindow.LogHistory = LogHistory;

			// Force a filter here.
			LogHistory.FilterLogItems();

			ConsoleInputField = InputFieldFactory.GetInputField(LogHistoryGui);

			Suggestions = new Suggestions(SkinData, ConsoleInputField);
	
	        _recentCommands = PlayerPrefs.GetString("com.wellfired.command.recentCommands").Split(',').Where(x => x != "").ToList();
		    
		    _uiBlocker = new UIBlocker(transform);
		    CommandsWindow = new CommandsWindow(SkinData);
		    LogEntryPopupWindow = new LogEntryPopupWindow(SkinData);
		    
		    CommandRegistration.RegisterCommandsOnConsoleStartup(
			    clearConsoleCommandEnabled, 
			    deviceIdCommandEnabled, 
			    inspectCommandEnabled, 
			    autoScrollEnabled);
	    }

		private void OnDestroy()
		{
			CommandRegistration.UnRegisterCommandsOnConsoleExit();
			DebugLogHistory?.Dispose();
			DevelopmentCommands.CommandExecuted -= OnCommandExecuted;
		}
		
		private void Update()
		{
			if(!ConsoleEnabled)
				return;
			
			DebugLogHistory.Update();
			
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM_ENABLED
        // New Input System
        if (Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame)
        {
            IsVisible = !IsVisible;
        }
#else
			// Old Input Manager
			if (UnityEngine.Input.GetKeyDown(KeyCode.BackQuote))
			{
				IsVisible = !IsVisible;
			}
#endif

			Suggestions.Update();
		}
	
	    private void OnCommandExecuted(CommandWrapper handler)
	    {
	        var commandLower = handler.CommandName.ToLower();
	
	        _recentCommands.Remove(commandLower);
	        _recentCommands.Insert(0, commandLower);
	
	        if(_recentCommands.Count > 12)
	            _recentCommands.RemoveAt(_recentCommands.Count - 1);
	
			PlayerPrefs.SetString("CachedConsoleCommands", string.Join(",", _recentCommands.ToArray()));
	        PlayerPrefs.Save();
	    }
	
	    private void OnGUI()
	    {
			if(!ConsoleEnabled)
	            return;
		    
		    if(!_visible)
			{
	            LogHistoryGui.AutoScrolling = true;
	            LogEntryPopupWindow.Hide();
	            CommandsWindow.Hide();

				if (DrawShowDotCommandButton)
				{
					DisplayDisplayButton();
				}
				else
				{
					_uiBlocker.UnblockOpenConsoleArea();
				}
				
				_uiBlocker.UnblockConsoleArea();
				_uiBlocker.UnblockScreen();

				return;
			}

		    _uiBlocker.UnblockOpenConsoleArea();

		    var commandsRect = CommandsWindow.Draw();
		    _uiBlocker.BlockCommandsArea(commandsRect);
		    
			var filterRect = FilterWindow.Draw();
		    
		    _uiBlocker.BlockFilterArea(filterRect);
		    
	        var logEntryRect = LogEntryPopupWindow.Draw();
		    _uiBlocker.BlockLogEntryArea(logEntryRect);
	
	        if(ConsoleInputField.HasFocus)
	        {
		        var commandTooltipBottomLeft = DrawCommandDescriptionTooltip(
			        new Vector2(
				        ConsoleInputField.Rect.xMin + Selector.OnPlatform(SkinData.ButtonSpacing, SkinData.ButtonSpacingTouch),
				        ConsoleInputField.Rect.yMax + Selector.OnPlatform(SkinData.ButtonSpacing, SkinData.ButtonSpacingTouch)
			        )
		        );
		        
				var currentParameterIndex = ConsoleInputField.CurrentParameterIndex;

	            var maxWidth = 0.0f;
	            if (currentParameterIndex >= 0)
	            {
	                var commandUpToLastParameter = ConsoleInputField.PreviousCompleteParameters.SubArray(0, currentParameterIndex + 1);
	                var commandUpToLastParameterAsString = string.Join(" ", commandUpToLastParameter);
	
					maxWidth = GUI.skin.label.CalcSize(new GUIContent(commandUpToLastParameterAsString)).x;
	            }
	
	            var parameterListBottomLeft = DrawParameterList(commandTooltipBottomLeft + new Vector2(maxWidth, 0));
				Suggestions.Draw(SkinData, new Rect(parameterListBottomLeft.x, parameterListBottomLeft.y, ScreenWidth - parameterListBottomLeft.x - 10, 400));
		        
		        _uiBlocker.BlockScreen();
	        }
	        else
	        {
		        _uiBlocker.UnblockScreen();
	        }
	
	        var screenHeight = IsMaximized ? Screen.height : Screen.height / 2.5f;
	        if(ForceMinimize)
				screenHeight = Screen.height / 4.5f;
		    
	        var windowRect = GUI.Window(99, new Rect(0, 0, Screen.width, screenHeight), WindowFunc, "", Helper.Window(SkinData));
		    
		    _uiBlocker.BlockConsoleArea(windowRect);

		    if (Event.current.rawType == EventType.MouseUp && !windowRect.Contains(Event.current.mousePosition))
			    _mouseUpOutsideOfWindow = true;
	
			if((LogEntryPopupWindow.IsVisible || CommandsWindow.IsVisible || FilterWindow.IsVisible) && ConsoleInputField.HasFocus)
	            ConsoleInputField.LoseFocus();
		}

		private void DisplayDisplayButton()
		{
			var isBottom = DisplayCorner == DisplayCorner.BottomLeft || DisplayCorner == DisplayCorner.BottomRight;
			var isRight = DisplayCorner == DisplayCorner.TopRight || DisplayCorner == DisplayCorner.BottomRight;

			if (!isBottom)
			{
				if(!isRight)
					DrawToggleButton();
				else
				{
					GUILayout.BeginHorizontal(GUILayout.Width(Screen.width));
					GUILayout.FlexibleSpace();
					DrawToggleButton();
					GUILayout.EndHorizontal();
				}
			}

			if (!isBottom) 
				return;
			
			GUILayout.BeginVertical(GUILayout.Height(Screen.height));
			GUILayout.FlexibleSpace();

			if (isRight)
			{
				GUILayout.BeginHorizontal(GUILayout.Width(Screen.width));
				GUILayout.FlexibleSpace();
			}

			DrawToggleButton();
				
			if(isRight)
				GUILayout.EndHorizontal();
				
			GUILayout.EndVertical();
		}

		private void DrawToggleButton()
		{
			if (Helper.Button(SkinData, ShowDotCommandButtonMessage))
				IsVisible = true;
			
			_uiBlocker.BlockOpenConsoleArea(GUILayoutUtility.GetLastRect());
		}

		private void OnLogEntryAdded(LogEntry logEntry)
		{
			if (!AutoOpen)
				return;
			
	        switch (logEntry.Type)
	        {
		        case LogType.Exception when ShowOnException:
			        IsVisible = true;
			        break;
		        case LogType.Error when ShowOnError:
			        IsVisible = true;
			        break;
	        }
		}
	
	    private void WindowFunc(int windowId)
	    {
			var inFocus = !CommandsWindow.IsVisible && !LogEntryPopupWindow.IsVisible && !FilterWindow.IsVisible;
	
	        if(!inFocus)
	        {
	            // Makes the other windows modal by preventing input to the console when they're open
	            if(Event.current.isKey || Event.current.isMouse)
	                return;

	            Event.current.mousePosition = Vector2.zero;
	        }
	
			using(new GuiBeginVertical(GUILayout.Width(Screen.width)))
			{	
	        	DrawHeader();
				
	        	LogHistoryGui.Draw(SkinData, _mouseUpOutsideOfWindow);
				_mouseUpOutsideOfWindow = false;
				
	        	ConsoleInputField.Draw(SkinData);
			}

		    if (!inFocus) 
			    return;
		    
		    GUI.FocusWindow(windowId);
		    GUI.BringWindowToFront(windowId);
	    }
	 
	    private Vector2 DrawParameterList(Vector2 topLeft)
	    {
	        if(string.IsNullOrEmpty(ConsoleInputField.Input))
	            return topLeft;
	
			var exactlyMatchingCommand = DevelopmentCommands.GetCommandWrapper(ConsoleInputField.Input);
	
	        if(exactlyMatchingCommand == null)
	            return topLeft;
	
	        var commandHandlerParameters = exactlyMatchingCommand.Parameters.ToArray();
	        var content = new List<string>();
	
	        var hasOptionalParameters = false;
		    var i = 0;
		    var currentParameter = 0;
	        foreach(var parameter in commandHandlerParameters)
	        {
	            if(content.Count != 0)
	                content.Add(", ");
	
	            var defaultValue = string.Empty;
	            if(parameter.IsOptional && !hasOptionalParameters)
	            {
	                hasOptionalParameters = true;
					content.Add("[");
	            }
	
	            if(parameter.IsOptional)
	                defaultValue = parameter.Type == typeof (string) ? " = \"\"" : " = " + parameter.DefaultValue;

		        var isCurrentParameter = i == ConsoleInputField.CurrentParameterIndex;
		        if (isCurrentParameter)
			        currentParameter = content.Count;
		        
	            var parameterText = $"{parameter.Type.Name} {parameter.Name}{defaultValue}";
	            content.Add(parameterText);
		        i++;
	        }
	
	        if(hasOptionalParameters)
				content.Add("]");

		    var style = Helper.SuggestionLabelStyle(SkinData);

		    // Now work out the width of all the text that we want to render
		    var totalWidth = 0.0f;
		    foreach(var item in content)
			    totalWidth += style.CalcSize(new GUIContent(item)).x;

		    var height = style.CalcSize(new GUIContent("]")).y;
		    var tooltipRect = new Rect(topLeft.x, topLeft.y, totalWidth + 8, height);
		
		    Helper.Label(SkinData, tooltipRect, new GUIContent());
	
		    var widthSoFar = 4.0f;
		    foreach(var item in content)
		    {
			    var maxWidth = style.CalcSize(new GUIContent(item)).x;
			    var itemRect = new Rect(topLeft.x + widthSoFar, tooltipRect.yMin, maxWidth, height);
			    widthSoFar += maxWidth;

			    var current = content.IndexOf(item) == currentParameter;
			    Helper.DrawArgument(SkinData, itemRect, new GUIContent(item), current);
		    }

		    return new Vector2(
			    tooltipRect.x + Selector.OnPlatform(SkinData.ButtonSpacing, SkinData.ButtonSpacingTouch),
			    tooltipRect.yMax + SkinData.ButtonSpacing + Selector.OnPlatform(SkinData.ButtonSpacing, SkinData.ButtonSpacingTouch)
		    );
	    }

		private Vector2 DrawCommandDescriptionTooltip(Vector2 topLeft)
	    {
	        if(string.IsNullOrEmpty(ConsoleInputField.Input.Trim()))
	            return topLeft;
	
			var exactlyMatchingCommand = DevelopmentCommands.GetCommandWrapper(ConsoleInputField.Input);
		    return !string.IsNullOrEmpty(exactlyMatchingCommand?.Description) ? Helper.DrawTooltip(SkinData, topLeft, exactlyMatchingCommand) : topLeft;
	    }
	
	    private void DrawHeader()
	    {
			using(Helper.HeaderBeginHorizontal(SkinData))
			{
				if(Helper.Button(SkinData, CloseButtonText, GUILayout.ExpandWidth(false)))
				{
					ConsoleInputField.LoseFocus();
					IsVisible = false;
				}

				Helper.Space(SkinData);
				
				if(ForceMinimize)
					GUI.enabled = false;

				if (Helper.Button(SkinData, IsMaximized ? "Minimize" : "Maximize", GUILayout.ExpandWidth(false)))
					IsMaximized = !IsMaximized;

				Helper.Space(SkinData);
				
				if(Helper.Button(SkinData, AutoOpen ? "Disable" : "Enable", GUILayout.ExpandWidth(false)))
				{
					AutoOpen = !AutoOpen;
					if (!AutoOpen)
						IsVisible = false;
				}

				Helper.ShrinkableSpace(SkinData);

				LogHistory.FilterString = Helper.SearchField(SkinData, LogHistory.FilterString, ScreenWidth);
					
				Helper.ShrinkableSpace(SkinData);

				if(Helper.Button(SkinData, $"Filters : {Filter.ActiveFilterCount}", GUILayout.ExpandWidth(false)))
				{
					FilterWindow.Show();
					LogEntryPopupWindow.Hide();
				}
				
				Helper.Space(SkinData);

				if (Helper.Button(SkinData, "Commands", GUILayout.ExpandWidth(false)))
				{
					CommandsWindow.Show();
					LogEntryPopupWindow.Hide();
				}
			}
		}

		private static class EmailLogHelper
		{
			[ConsoleCommand(Description = "Attached the current log as an email and sends it.")]
			[UsedImplicitly]
			private static void EmailLog()
			{
				var body = Instance.EmailViewModal.GetAsBody();
				EmailFactory.GetEmailSender(Instance.EmailLogSupportEnabled).Email("log.txt",
					"text/html",
					"log.htm",
					"",
					".Command Log",
					body);
			}
		}
	}
}