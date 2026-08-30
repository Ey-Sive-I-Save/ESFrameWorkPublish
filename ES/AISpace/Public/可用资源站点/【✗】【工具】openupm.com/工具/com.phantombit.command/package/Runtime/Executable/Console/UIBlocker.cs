using UnityEngine;
using UnityEngine.UI;

namespace WellFired.Command.Unity.Runtime.Console
{
	/// <summary>
	/// This class create a Unity UI canvas allowing to place input blockers below our Unity legacy GUI system.
	/// This ensure that input events happening in the Console are not used by element of the game. This is a temporary
	/// solution, in the future, .Command will use Unity most recent UI system.
	/// </summary>
	public class UIBlocker
	{
		private readonly Canvas _uiBlockerCanvas;
		private readonly RectTransform _openConsoleArea;
		private readonly RectTransform _consoleArea;
		private readonly RectTransform _filterArea;
		private readonly RectTransform _commandsArea;
		private readonly RectTransform _logEntryArea;
		private readonly RectTransform _screenArea;
		
		public UIBlocker(Transform parent)
		{
			_uiBlockerCanvas = new GameObject("InputBlocker", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
		    
			_uiBlockerCanvas.transform.SetParent(parent);
		   
			_uiBlockerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
			_uiBlockerCanvas.sortingOrder = 32767;

			_openConsoleArea = GetUIBlocker("OpenConsole");
			_filterArea = GetUIBlocker("Filters");
			_consoleArea = GetUIBlocker("Console");
			_commandsArea = GetUIBlocker("Commands");
			_logEntryArea = GetUIBlocker("Stacktrace");
			_screenArea = GetUIBlocker("Screen");
		}
		
		/// <summary>
		/// The blocking canvas is placed on top of every canvas by default. This function allows to change it.
		/// </summary>
		/// <param name="sortingOrder"></param>
		public void SetSortingOrder(int sortingOrder)
		{
			_uiBlockerCanvas.sortingOrder = sortingOrder;
		}
		
		/// <summary>
		/// Will set the size of the input blocker corresponding to the open console button.
		/// </summary>
		/// <param name="rect"></param>
		public void BlockOpenConsoleArea(Rect rect)
		{
			Block(rect, _openConsoleArea);
		}

		public void UnblockOpenConsoleArea()
		{
			Unblock(_openConsoleArea);
		}

		/// <summary>
		/// Will set the size of the input blocker corresponding to the console window.
		/// </summary>
		/// <param name="rect"></param>
		public void BlockConsoleArea(Rect rect)
		{
			Block(rect, _consoleArea);
		}

		public void UnblockConsoleArea()
		{
			Unblock(_consoleArea);
		}
		
		/// <summary>
		/// Will set the size of the input blocker corresponding to the filter window.
		/// </summary>
		/// <param name="rect"></param>
		public void BlockFilterArea(Rect rect)
		{
			if (rect == Rect.zero)
			{
				Unblock(_filterArea);
				return;
			}
			
			Block(rect, _filterArea);
		}
		
		/// <summary>
		/// Will set the size of the input blocker corresponding to the commands window.
		/// </summary>
		/// <param name="rect"></param>
		public void BlockCommandsArea(Rect rect)
		{
			if (rect == Rect.zero)
			{
				Unblock(_commandsArea);
				return;
			}
			
			Block(rect, _commandsArea);
		}
		
		
		/// <summary>
		/// Will set the size of the input blocker corresponding to the log entry window.
		/// </summary>
		/// <param name="rect"></param>
		public void BlockLogEntryArea(Rect rect)
		{
			if (rect == Rect.zero)
			{
				Unblock(_logEntryArea);
				return;
			}
			
			Block(rect, _logEntryArea);
		}

		public void BlockScreen()
		{
			Block(new Rect(0,0,Screen.width, Screen.height), _screenArea);
		}

		public void UnblockScreen()
		{
			Unblock(_screenArea);
		}

		private static void Unblock(RectTransform blocker)
		{
			blocker.gameObject.SetActive(false);
		}

		private static void Block(Rect rect, RectTransform blocker)
		{
			blocker.gameObject.SetActive(true);
			
			blocker.sizeDelta = rect.size;
			blocker.anchoredPosition = new Vector2(rect.position.x, - rect.position.y);
		}
		
		private RectTransform GetUIBlocker(string name)
		{
			var blocker = new GameObject(name, typeof(Image)).GetComponent<RectTransform>();
			blocker.anchorMax = Vector2.up;
			blocker.anchorMin = Vector2.up;
			blocker.sizeDelta = Vector2.zero;
			blocker.pivot = Vector2.up;
			blocker.SetParent(_uiBlockerCanvas.transform);
			blocker.GetComponent<Image>().color = Color.clear;
			blocker.gameObject.SetActive(false);

			return blocker;
		}
	}
}