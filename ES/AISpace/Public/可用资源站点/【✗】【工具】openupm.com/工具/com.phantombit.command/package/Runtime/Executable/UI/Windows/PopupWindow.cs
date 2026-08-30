using UnityEngine;
using WellFired.Command.Skins;
using WellFired.Command.Unity.Runtime.UnityGui;

namespace WellFired.Command.Unity.Runtime.UI.Windows
{
	public abstract class PopupWindow
	{
		private const float PopupDisplayRatio = 0.9f;

		private readonly string _windowTitle;
	    private readonly int _windowId;
		protected readonly ISkinData SkinData;
	
		public bool IsVisible 		{ get; private set; }
		public float WindowX => (Screen.width - WindowWidth) * 0.5f;
		public float WindowY => (Screen.height - WindowHeight) * 0.5f;
		public float WindowWidth => Screen.width * PopupDisplayRatio;
		public float WindowHeight => Screen.height * PopupDisplayRatio;
	
		protected abstract void OnShow();
		protected abstract void OnHide();
		protected abstract void DrawWindow(int windowId);

		public PopupWindow(ISkinData skinData, string windowTitle, int windowId)
		{
			SkinData = skinData;
	    	_windowTitle = windowTitle;
			_windowId = windowId;
	    }
	
	    public void Show()
		{
	        IsVisible = true;
	        Focus();
	        OnShow();
	    }
	
	    public void Hide()
	    {
			OnHide();
	        IsVisible = false;
	    }

	    public Rect Draw()
	    {
	        if(!IsVisible)
			{
				return Rect.zero;
			}

		    var windowStyle = Helper.Window(SkinData);
		    return GUI.Window(_windowId, new Rect(WindowX, WindowY, WindowWidth, WindowHeight), DrawWindow, new GUIContent(_windowTitle), windowStyle);
	    }

		private void Focus()
	    {
	        GUI.FocusWindow(_windowId);
	    }
	}
}