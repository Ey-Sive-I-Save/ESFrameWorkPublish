using System;
using System.IO;
using UnityEngine;
using WellFired.Command.Skins;
using WellFired.Command.Unity.Runtime.Console;
using WellFired.Command.Unity.Runtime.Email;
using WellFired.Command.Unity.Runtime.Log;
using WellFired.Command.Unity.Runtime.UnityGui;

namespace WellFired.Command.Unity.Runtime.UI.Windows
{
	public class LogEntryPopupWindow : PopupWindow
	{
		private readonly ISkinData _skinData;
		private Vector2 _scrollPosition;
	    private LogEntry _logEntry;

		public LogEntryPopupWindow(ISkinData skinData) : base(skinData, "Log Entry", 12)
		{
			_skinData = skinData;
		}
	
	    public virtual void Show(LogEntry logEntry)
	    {
	        base.Show();

		    _logEntry = logEntry ?? throw new InvalidOperationException("Log Entry can't be null");
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

				if(EmailFactory.GetEmailSender(DevelopmentConsole.Instance.EmailLogSupportEnabled).CanSendEmail() && Helper.Button(_skinData, "Email"))
				{
					var body = $"{_logEntry.LogMessage}\n\n{_logEntry.StackTrace}";
					
					var filename = Application.temporaryCachePath + "/log_" + DateTime.Now.ToString("o") + ".txt";
					var writer = new StreamWriter(filename);
					writer.WriteLine(_logEntry.LogMessage);
					writer.WriteLine(_logEntry.StackTrace);
					writer.Close();

					EmailFactory.GetEmailSender(DevelopmentConsole.Instance.EmailLogSupportEnabled).Email(
						filename, 
						"text/plain", 
						filename, 
						"", 
						"Email Log", 
						body);
				}
			}

			_scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
	        
			if(Application.platform == RuntimePlatform.IPhonePlayer)
	            GUI.enabled = false;

	        GUI.color = new UnityEngine.Color(1, 1, 1, 2);
	        Helper.TextArea(SkinData, new GUIContent(_logEntry.LogMessage + "\n" + _logEntry.StackTrace), GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
	        GUI.enabled = true;
	        GUILayout.EndScrollView();
	
	        GUI.FocusWindow(windowId);
	        GUI.BringWindowToFront(windowId);
		}
	}
}