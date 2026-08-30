using UnityEngine;
using WellFired.Command.Skins;
using WellFired.Command.Unity.Runtime.Console;
using WellFired.Command.Unity.Runtime.Log;
using WellFired.Command.Unity.Runtime.UnityGui;
using Scroller = WellFired.Command.Unity.Runtime.UI.Scroller;

namespace WellFired.Command.Unity.Runtime.Modals
{
	public class LogHistoryGui
	{
		private readonly ISkinData _skinData;
		private readonly LogHistory _viewModel;
	    private Vector2 _scrollPosition = Vector2.zero;
	    private LogEntry _highlightedLogItem;
	    private readonly DevelopmentConsole _console;
		
		//Scrolling parameters
		private const float MaxReleaseSpeed = 400f;
		private const float DeceleratingSpeed = 300;
		private readonly Scroller _scroller;
		private bool _mouseDown;
		private const float DeltaThreshold = 5f;
		private Vector2 _delta;
		private bool _autoScrollingCancelled;
		//////////////////////

		private bool _autoScrolling;
		public bool AutoScrolling
		{
			get => _autoScrolling;
			set
			{
				if(value)
					_scrollPosition = new Vector2(0, Mathf.Infinity);
				
				_autoScrolling = value;
			}
		}

		public LogHistoryGui(ISkinData skinData, LogHistory viewModel, DevelopmentConsole console)
	    {
		    _skinData = skinData;
		    _viewModel = viewModel;
			_console = console;
		    _scroller = new Scroller(MaxReleaseSpeed, DeceleratingSpeed);
	    }

		public void Draw(ISkinData skinData, bool mouseUpOutsideOfWindow)
		{
		    _scrollPosition = Helper.BeginScrollView(_skinData, _scrollPosition, GUILayout.ExpandHeight(true), GUILayout.Width(Screen.width));
			
			if (Event.current.type == EventType.ScrollWheel && AutoScrolling)
			{
				_autoScrollingCancelled = true;
			}
	
	        var isHoveringOverItem = DrawConsoleItemList(skinData);
	
	        if(Event.current.type == EventType.Repaint && !isHoveringOverItem)
	            _highlightedLogItem = null;
	
	        GUILayout.EndScrollView();
		    
		    if(mouseUpOutsideOfWindow)
			    _mouseDown = false;
		    else
		    {
			    switch (Event.current.rawType)
			    {
				    case EventType.MouseDown:
					    _mouseDown = true;
					    break;
				    case EventType.MouseUp:
					    _mouseDown = false;
					    break;
				    case EventType.MouseDrag:
					    _delta += Event.current.delta;
					    break;
			    }
		    }

			if (Event.current.type == EventType.Repaint)
			{
				var listRect = GUILayoutUtility.GetLastRect();
				HandleScrolling(listRect);
				
				var maxScrollPosition = _viewModel.LogEntries.Count * Helper.EntryHeight(skinData) - listRect.height;

				if (_autoScrollingCancelled)
				{
					_scrollPosition = new Vector2(0, maxScrollPosition - 1f);
					_autoScrollingCancelled = false;
				}
				else
				{
					AutoScrolling = _scrollPosition.y >= maxScrollPosition;
					if(AutoScrolling)
						_scrollPosition = new Vector2(0, Mathf.Infinity);	
				}
			}
		}
		
		private void HandleScrolling(Rect listRect)
		{
			if (_scroller.State == Scroller.ScrollerState.Decelerating && _mouseDown)
			{
				_scroller.ZeroSpeed();
			}
			
			if (_scroller.State != Scroller.ScrollerState.Scrolling 
			    && _delta.sqrMagnitude > DeltaThreshold 
			    && _mouseDown
			    && listRect.Contains(Event.current.mousePosition))
			{
				_scroller.DoTouch(Event.current.mousePosition);
			}
			else if(_scroller.State == Scroller.ScrollerState.Scrolling && !_mouseDown)
			{
				_scroller.DoUntouch();
				_delta = Vector2.zero;
			}
			
			_scrollPosition -= _scroller.Update(Event.current.mousePosition, Time.deltaTime);
		}
	
	    private bool DrawConsoleItemList(ISkinData skinData)
	    {
	        var itemCount = _viewModel.LogEntries.Count;
	
		    var maxScrollPosition = itemCount * Helper.EntryHeight(skinData);
		    _scrollPosition = new Vector2(0, Mathf.Clamp(_scrollPosition.y, 0, maxScrollPosition));
	
	        // Reserve a rect the height of the list
			var listRect = GUILayoutUtility.GetRect(0, 1000, maxScrollPosition, maxScrollPosition);
	        var isOverAnItem = false;
			var firstIndexVisible = (int)(_scrollPosition.y / Helper.EntryHeight(skinData));
			var lastIndexVisible = Mathf.Clamp(firstIndexVisible + Screen.height / Helper.EntryHeight(skinData), 0, itemCount - 1);
	
	        for(var index = firstIndexVisible; index <= lastIndexVisible; ++index)
	        {
				var itemRect = new Rect(0, index * Helper.EntryHeight(skinData), listRect.width, Helper.EntryHeight(skinData));
	            var item = _viewModel.LogEntries[index];
	            isOverAnItem = DrawConsoleItem(skinData, itemRect, item);
	        }
	
	        GUI.color = UnityEngine.Color.white;
	
	        return isOverAnItem;
	    }
	
		private bool DrawConsoleItem(ISkinData skinData, Rect itemRect, LogEntry logEntry)
	    {
	        var isOverAnItem = false;
	
	        if(itemRect.Contains(Event.current.mousePosition))
	        {
		        // ReSharper disable once ConvertIfStatementToSwitchStatement
		        if (Event.current.type == EventType.MouseUp && _scroller.State != Scroller.ScrollerState.Scrolling)
		        {
			        // The user clicked on a row
			        _console.InspectLogEntry(logEntry);
		        }
		        else if (Event.current.type == EventType.Repaint)
		        {
			        // The user is hovering over a row
			        _highlightedLogItem = logEntry;
			        isOverAnItem = true;
		        }
	        }

		    if (Event.current.type != EventType.Repaint) 
			    return isOverAnItem;
		    
		    Helper.LogEntry(skinData, itemRect, logEntry.FirstLineOfLogMessage, logEntry.Type, false, logEntry == _highlightedLogItem, false, false);

		    return isOverAnItem;
	    }
	}
}