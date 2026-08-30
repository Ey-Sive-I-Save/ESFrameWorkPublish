using System;
using System.Collections.Generic;
using UnityEngine;
using WellFired.Command.Skins;
using WellFired.Command.Unity.Runtime.CommandHandlers;
using WellFired.Command.Unity.Runtime.Console;
using WellFired.Command.Unity.Runtime.UnityGui;
using WellFired.Command.Unity.Runtime.Wrapper;

namespace WellFired.Command.Unity.Runtime.UI.Windows
{
	public class CommandsWindow : PopupWindow
	{
	    private Vector2 _scrollPosition = Vector2.zero;
	    private static readonly List<CommandWrapper> SortedCommandList = new List<CommandWrapper>();

	    public CommandsWindow(ISkinData skinData) : base(skinData, "Help", 10)
	    {
			DevelopmentCommands.CommandHandlerAdded += OnCommandHandlerAdded;
			DevelopmentCommands.CommandHandlerRemoved += OnCommandHandlerRemoved;
	
			SortedCommandList.AddRange(DevelopmentCommands.Handlers);
	        ResortRecentCommandList();
	    }
	
	    protected override void OnShow()
	    {
	        _scrollPosition = Vector2.zero;
	    }
		
		protected override void OnHide()
		{

		}

	    protected override void DrawWindow(int windowId)
		{
			using(Helper.HeaderBeginHorizontal(SkinData))
			{
				if(Helper.Button(SkinData, "Close"))
					Hide();
				
				GUILayout.FlexibleSpace();
			}

	        _scrollPosition = Helper.BeginScrollView(SkinData, _scrollPosition);

			using(Helper.BodyBeginVertical(SkinData))
			{
				GUILayout.Space(Selector.OnPlatform(SkinData.HeaderPaddingKeyboard, SkinData.HeaderPaddingTouch));
				
				foreach(var commandHandler in SortedCommandList)
				{
					var parameters = commandHandler.GetParametersAsString();

					using(Helper.BodyBeginHorizontal(SkinData))
					{
						GUILayout.Space(Selector.OnPlatform(SkinData.HeaderPaddingKeyboard, SkinData.HeaderPaddingTouch));
						
						if(Helper.Button(SkinData, commandHandler.CommandName + (parameters.Length != 0 ? " ..." : "")))
						{
							if(string.IsNullOrEmpty(parameters))
							{
								commandHandler.Invoke();
								DevelopmentConsole.Instance.ClearTypedInput();
								Hide();
							}
							else
							{
								DevelopmentConsole.Instance.SetCommandInputTextAsIfUserHadTyped(commandHandler.CommandName + " ");
								Hide();
							}
						}

						if (!string.IsNullOrEmpty(parameters))
						{
							var buttonWidth = Helper.LabelSizeWithContent(SkinData, new GUIContent(parameters)).x;
							Helper.Label(SkinData, new GUIContent(parameters), GUILayout.Width(buttonWidth));
						}

						if (!string.IsNullOrEmpty(commandHandler.Description))
						{
							var buttonWidth = Helper.LabelSizeWithContent(SkinData, new GUIContent(commandHandler.Description)).x;
							Helper.Label(SkinData, new GUIContent(commandHandler.Description), GUILayout.Width(buttonWidth));
						}
					
						GUILayout.FlexibleSpace();
					}
					
					GUILayout.Space(Selector.OnPlatform(SkinData.ButtonSpacing, SkinData.ButtonSpacingTouch));
				}
			}

	        GUILayout.EndScrollView();
	
	        GUI.FocusWindow(windowId);
	        GUI.BringWindowToFront(windowId);
	    }
	
	    private static void ResortRecentCommandList()
	    {
	        SortedCommandList.Sort((a, b) => string.Compare(a.CommandName.ToLower(), b.CommandName.ToLower(), StringComparison.Ordinal));
	    }
	
	    private static void OnCommandHandlerAdded(CommandWrapper commandHandler)
	    {
	        SortedCommandList.Add(commandHandler);
	        ResortRecentCommandList();
	    }
	
	    private static void OnCommandHandlerRemoved(CommandWrapper commandHandler)
	    {
	        SortedCommandList.Remove(commandHandler);
	        ResortRecentCommandList();
	    }
	}
}