using System;
using UnityEngine;

namespace WellFired.Command.Unity.Runtime.UnityGui
{
	public class GuiBeginHorizontal : IDisposable
	{
		public GuiBeginHorizontal()
		{
			GUILayout.BeginHorizontal();
		}
		
		public GuiBeginHorizontal(params GUILayoutOption[] layoutOptions)
		{
			GUILayout.BeginHorizontal(layoutOptions);
		}
		
		public GuiBeginHorizontal(GUIStyle guiStyle, params GUILayoutOption[] layoutOptions)
		{
			GUILayout.BeginHorizontal(guiStyle, layoutOptions);
		}
		
		public void Dispose() 
		{
			GUILayout.EndHorizontal();
		}
	}
}